using System.Text;

namespace GEO_DASH.Audio
{
    /// <summary>
    /// Собственный саундтрек: музыка синтезируется кодом при первом запуске
    /// (чужих треков в игре нет). Треки с отрицательными ID — встроенные.
    /// </summary>
    public static class Soundtrack
    {
        public const int SampleRate = 44100;

        public class TrackDef
        {
            public int Id;                 // отрицательный: -1, -2, -3
            public string Name;
            public string Artist = AppConfig.Brand;
            public double Bpm;
            public int Bars;
            public int[] Scale;            // полутона от корня
            public int Root;               // MIDI-нота корня
            public int[] Progression;      // ступени аккордов по тактам
            public double LeadDensity;
            public bool DoubleTime;
            public double Drive;
        }

        public static readonly TrackDef[] Tracks =
        {
            new TrackDef
            {
                Id = -1, Name = "Neon Start", Bpm = 128, Bars = 48, Root = 45,
                Scale = new[]{0,2,4,5,7,9,11}, Progression = new[]{0,5,3,4},
                LeadDensity = 0.55, Drive = 0.25
            },
            new TrackDef
            {
                Id = -2, Name = "Static Rush", Bpm = 152, Bars = 56, Root = 43,
                Scale = new[]{0,2,3,5,7,8,10}, Progression = new[]{0,3,5,4},
                LeadDensity = 0.75, DoubleTime = true, Drive = 0.45
            },
            new TrackDef
            {
                Id = -3, Name = "Hyperdrive", Bpm = 178, Bars = 64, Root = 41,
                Scale = new[]{0,1,3,5,7,8,10}, Progression = new[]{0,6,3,5},
                LeadDensity = 0.95, DoubleTime = true, Drive = 0.65
            }
        };

        public static TrackDef Get(int id)
        {
            foreach (var t in Tracks) if (t.Id == id) return t;
            return null;
        }

        public static string PathOf(int id) => Path.Combine(AppConfig.MusicDir, "builtin" + Math.Abs(id) + ".wav");

        public static bool IsBuiltIn(int id) => id < 0 && Get(id) != null;

        /// <summary>Генерирует трек, если его ещё нет на диске. Возвращает путь к wav.</summary>
        public static string Ensure(int id, IProgress<string> prog = null)
        {
            var def = Get(id);
            if (def == null) return null;
            var path = PathOf(id);
            if (File.Exists(path) && new FileInfo(path).Length > 200_000) return path;
            prog?.Report("Готовлю трек «" + def.Name + "»…");
            var pcm = Render(def);
            WriteWav(path, pcm);
            return path;
        }

        public static void EnsureAll(IProgress<string> prog = null)
        {
            foreach (var t in Tracks) Ensure(t.Id, prog);
        }

        // ---------------------------------------------------------------- синтез

        static double Freq(int midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);

        public static float[] Render(TrackDef d)
        {
            double spb = 60.0 / d.Bpm;             // длительность доли
            double stepLen = spb / 4.0;            // 16-е
            int steps = d.Bars * 16;
            int total = (int)(steps * stepLen * SampleRate) + SampleRate;
            var buf = new float[total];
            var rnd = new Random(d.Id * 7919);

            int[] chordTones = { 0, 2, 4 };

            for (int s = 0; s < steps; s++)
            {
                int bar = s / 16, beat = (s % 16) / 4, sixteenth = s % 16;
                int deg = d.Progression[bar % d.Progression.Length];
                int chordRoot = d.Root + d.Scale[deg % d.Scale.Length] + (deg >= d.Scale.Length ? 12 : 0);
                double t0 = s * stepLen;
                bool drop = bar % 16 >= 8;          // вторая половина — плотнее

                // бас
                if (sixteenth % 2 == 0)
                {
                    double len = stepLen * (d.DoubleTime ? 1.0 : 1.6);
                    AddVoice(buf, t0, len, Freq(chordRoot - 12), 0.34, Wave.Saw, 0.004, 0.09, d.Drive);
                }

                // кик
                if (sixteenth % 4 == 0) AddKick(buf, t0, 0.42);

                // снэр
                if (sixteenth % 8 == 4) AddNoise(buf, t0, 0.13, 0.3, 0.55);

                // хэт
                if (sixteenth % 2 == 1 && (drop || sixteenth % 4 == 1))
                    AddNoise(buf, t0, 0.04, 0.11, 0.9);

                // арпеджио/лид
                if (rnd.NextDouble() < (drop ? d.LeadDensity : d.LeadDensity * 0.6))
                {
                    int tone = chordTones[(s + bar) % chordTones.Length];
                    int oct = drop ? 12 : 0;
                    int note = chordRoot + d.Scale[(deg + tone) % d.Scale.Length] + 12 + oct;
                    AddVoice(buf, t0, stepLen * (d.DoubleTime ? 0.9 : 1.4), Freq(note),
                             drop ? 0.19 : 0.13, Wave.Square, 0.003, 0.05, d.Drive * 0.6);
                    // «эхо» через 3 шестнадцатых
                    AddVoice(buf, t0 + stepLen * 3, stepLen * 0.7, Freq(note), 0.06, Wave.Square, 0.003, 0.04, 0);
                }

                // пэд на первую долю такта
                if (sixteenth == 0)
                    foreach (var ct in chordTones)
                    {
                        int note = chordRoot + d.Scale[(deg + ct) % d.Scale.Length];
                        AddVoice(buf, t0, spb * 3.6, Freq(note), 0.055, Wave.Sine, 0.08, 0.6, 0);
                    }
            }

            // лёгкая компрессия/лимитер
            float peak = 0.0001f;
            foreach (var v in buf) peak = Math.Max(peak, Math.Abs(v));
            float norm = 0.89f / peak;
            for (int i = 0; i < buf.Length; i++)
            {
                float v = buf[i] * norm;
                buf[i] = (float)Math.Tanh(v * 1.25) * 0.92f;
            }
            return buf;
        }

        enum Wave { Sine, Square, Saw, Tri }

        static void AddVoice(float[] buf, double start, double dur, double freq, double amp,
                             Wave w, double atk, double rel, double drive)
        {
            int i0 = (int)(start * SampleRate);
            int n = (int)(dur * SampleRate);
            if (i0 < 0 || n <= 0) return;
            double ph = 0, inc = 2 * Math.PI * freq / SampleRate;
            double lp = 0;
            for (int i = 0; i < n; i++)
            {
                int idx = i0 + i;
                if (idx >= buf.Length) break;
                double t = (double)i / SampleRate;
                double env;
                if (t < atk) env = t / Math.Max(1e-5, atk);
                else env = Math.Max(0, 1 - (t - atk) / Math.Max(1e-5, dur - atk)) * Math.Exp(-(t - atk) / Math.Max(1e-4, rel));
                double v = w switch
                {
                    Wave.Sine => Math.Sin(ph),
                    Wave.Square => Math.Sign(Math.Sin(ph)) * 0.7,
                    Wave.Tri => 2 * Math.Abs(2 * (ph / (2 * Math.PI) % 1) - 1) - 1,
                    _ => 2 * (ph / (2 * Math.PI) % 1) - 1
                };
                if (drive > 0) v = Math.Tanh(v * (1 + drive * 4)) * 0.8;
                lp += (v - lp) * 0.45;             // простой фильтр, чтобы не резало слух
                buf[idx] += (float)(lp * amp * env);
                ph += inc;
            }
        }

        static void AddKick(float[] buf, double start, double amp)
        {
            int i0 = (int)(start * SampleRate);
            int n = (int)(0.17 * SampleRate);
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                int idx = i0 + i;
                if (idx < 0 || idx >= buf.Length) break;
                double t = (double)i / SampleRate;
                double f = 130 * Math.Exp(-t * 26) + 44;
                ph += 2 * Math.PI * f / SampleRate;
                double env = Math.Exp(-t * 15);
                buf[idx] += (float)(Math.Sin(ph) * amp * env);
            }
        }

        static void AddNoise(float[] buf, double start, double dur, double amp, double bright)
        {
            int i0 = (int)(start * SampleRate);
            int n = (int)(dur * SampleRate);
            var rnd = new Random(i0);
            double hp = 0, prev = 0;
            for (int i = 0; i < n; i++)
            {
                int idx = i0 + i;
                if (idx < 0 || idx >= buf.Length) break;
                double t = (double)i / SampleRate;
                double white = rnd.NextDouble() * 2 - 1;
                hp = bright * (hp + white - prev);
                prev = white;
                double env = Math.Exp(-t / (dur * 0.35));
                buf[idx] += (float)(hp * amp * env);
            }
        }

        // ---------------------------------------------------------------- wav

        public static void WriteWav(string path, float[] data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs, Encoding.ASCII);
            int bytes = data.Length * 2;
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + bytes);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)1);
            bw.Write(SampleRate);
            bw.Write(SampleRate * 2);
            bw.Write((short)2);
            bw.Write((short)16);
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(bytes);
            foreach (var f in data)
                bw.Write((short)(Math.Clamp(f, -1f, 1f) * 32000));
        }
    }
}
