using System.Text;

namespace GEO_DASH.Audio
{
    /// <summary>
    /// Собственный саундтрек: музыка синтезируется кодом при первом запуске
    /// (чужих треков в игре нет). Треки с отрицательными ID — встроенные.
    /// Аранжировка как у настоящего трека: интро, разгон, дроп, брейк, второй дроп, финал.
    /// </summary>
    public static class Soundtrack
    {
        public const int SampleRate = 44100;

        public class TrackDef
        {
            public int Id;                 // отрицательный: -1, -2, -3 …
            public string Name;
            public string Artist = AppConfig.Brand;
            public double Bpm;
            public int Bars;
            public int[] Scale;            // полутона от корня
            public int Root;               // MIDI-нота корня
            public int[] Progression;      // ступени аккордов по тактам
            public int[] Motif;            // мелодия — ступени лада
            public double LeadDensity;
            public bool DoubleTime;
            public double Drive;
            public double Bright = 1.0;    // яркость фильтра
        }

        public static readonly TrackDef[] Tracks =
        {
            new TrackDef
            {
                Id = -1, Name = "Neon Start", Bpm = 128, Bars = 64, Root = 45,
                Scale = new[]{0,2,4,5,7,9,11}, Progression = new[]{0,5,3,4},
                Motif = new[]{0,2,4,2,5,4,2,1},
                LeadDensity = 0.55, Drive = 0.22, Bright = 0.95
            },
            new TrackDef
            {
                Id = -2, Name = "Static Rush", Bpm = 150, Bars = 72, Root = 43,
                Scale = new[]{0,2,3,5,7,8,10}, Progression = new[]{0,3,5,4},
                Motif = new[]{0,4,3,4,6,4,3,2},
                LeadDensity = 0.75, DoubleTime = true, Drive = 0.4, Bright = 1.1
            },
            new TrackDef
            {
                Id = -3, Name = "Hyperdrive", Bpm = 174, Bars = 80, Root = 41,
                Scale = new[]{0,1,3,5,7,8,10}, Progression = new[]{0,6,3,5},
                Motif = new[]{0,3,5,3,7,5,3,2},
                LeadDensity = 0.92, DoubleTime = true, Drive = 0.55, Bright = 1.2
            },
            new TrackDef
            {
                Id = -4, Name = "Violet Sky", Bpm = 118, Bars = 56, Root = 47,
                Scale = new[]{0,2,3,5,7,8,10}, Progression = new[]{0,5,2,4},
                Motif = new[]{4,2,0,2,4,5,4,2},
                LeadDensity = 0.45, Drive = 0.15, Bright = 0.8
            },
            new TrackDef
            {
                Id = -5, Name = "Afterburner", Bpm = 162, Bars = 76, Root = 40,
                Scale = new[]{0,2,3,5,7,9,10}, Progression = new[]{0,4,6,5},
                Motif = new[]{0,2,5,4,2,0,4,2},
                LeadDensity = 0.85, DoubleTime = true, Drive = 0.5, Bright = 1.05
            }
        };

        public static TrackDef Get(int id)
        {
            foreach (var t in Tracks) if (t.Id == id) return t;
            return null;
        }

        public static string PathOf(int id) => Path.Combine(AppConfig.MusicDir, "builtin" + Math.Abs(id) + ".wav");

        public static bool IsBuiltIn(int id) => id < 0 && Get(id) != null;

        static readonly object MakeLock = new();

        /// <summary>
        /// Генерирует трек, если его ещё нет на диске. Возвращает путь к wav.
        /// Пишем через временный файл и под замком: иначе два потока могли
        /// начать писать один и тот же wav, и трек оставался битым навсегда.
        /// </summary>
        public static string Ensure(int id, IProgress<string> prog = null)
        {
            var def = Get(id);
            if (def == null) return null;
            var path = PathOf(id);

            lock (MakeLock)
            {
                if (File.Exists(path) && new FileInfo(path).Length > 200_000) return path;
                prog?.Report("Готовлю трек «" + def.Name + "»…");
                try
                {
                    var pcm = Render(def);
                    var tmp = path + ".tmp";
                    WriteWav(tmp, pcm);
                    if (new FileInfo(tmp).Length < 200_000) { File.Delete(tmp); return null; }
                    if (File.Exists(path)) File.Delete(path);
                    File.Move(tmp, path);
                    return path;
                }
                catch
                {
                    try { if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp"); } catch { }
                    return File.Exists(path) ? path : null;
                }
            }
        }

        public static void EnsureAll(IProgress<string> prog = null)
        {
            foreach (var t in Tracks) Ensure(t.Id, prog);
        }

        // ---------------------------------------------------------------- аранжировка

        /// <summary>Часть трека: 0 интро, 1 разгон, 2 дроп, 3 брейк, 4 дроп-2, 5 финал.</summary>
        static int SectionOf(int bar, int bars)
        {
            double p = (double)bar / Math.Max(1, bars);
            if (p < 0.12) return 0;
            if (p < 0.25) return 1;
            if (p < 0.50) return 2;
            if (p < 0.62) return 3;
            if (p < 0.88) return 4;
            return 5;
        }

        static double Freq(int midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);

        public static float[] Render(TrackDef d)
        {
            double spb = 60.0 / d.Bpm;
            double stepLen = spb / 4.0;                 // шестнадцатая
            int steps = d.Bars * 16;
            int total = (int)(steps * stepLen * SampleRate) + SampleRate * 2;

            var drums = new float[total];               // бочка и перкуссия
            var music = new float[total];               // бас, аккорды — их прижимает бочка
            var lead = new float[total];                // мелодия — уходит в эхо
            var duck = new float[total];                // сайдчейн
            for (int i = 0; i < total; i++) duck[i] = 1f;

            int[] chordTones = { 0, 2, 4, 6 };

            for (int s = 0; s < steps; s++)
            {
                int bar = s / 16, six = s % 16;
                int sec = SectionOf(bar, d.Bars);
                bool full = sec == 2 || sec == 4;                 // дроп
                bool quiet = sec == 0 || sec == 3 || sec == 5;    // спокойные части
                bool build = sec == 1;
                double t0 = s * stepLen;

                int deg = d.Progression[bar % d.Progression.Length];
                int chordRoot = d.Root + d.Scale[deg % d.Scale.Length];
                double energy = full ? 1.0 : build ? 0.75 : 0.5;

                // ---------------- бочка
                bool kick = quiet && sec != 3 ? six % 8 == 0 : six % 4 == 0;
                if (sec == 3) kick = six == 0;                      // брейк — только на раз
                if (build && bar % 4 == 3 && six >= 8) kick = six % 2 == 0;   // разгон к дропу
                if (kick)
                {
                    AddKick(drums, t0, full ? 0.95 : 0.7);
                    Duck(duck, t0, full ? 0.72 : 0.5, spb * 0.55);
                }

                // ---------------- хлопок
                if (!quiet && six % 8 == 4) AddClap(drums, t0, full ? 0.55 : 0.4);

                // ---------------- хэты
                if (!quiet || sec == 5)
                {
                    if (six % 2 == 1) AddNoise(drums, t0, 0.035, full ? 0.17 : 0.11, 0.92);
                    if (full && six % 4 == 2) AddNoise(drums, t0, 0.09, 0.1, 0.85);   // открытый
                }

                // ---------------- крэш в начале части
                if (six == 0 && bar > 0 && SectionOf(bar - 1, d.Bars) != sec)
                    AddNoise(drums, t0, 1.1, 0.33, 0.6);

                // ---------------- бас
                if (!quiet || sec == 5)
                {
                    bool note = d.DoubleTime ? six % 2 == 0 : six % 2 == 0 && six % 8 != 6;
                    if (note)
                    {
                        int oct = (six % 8 == 4) ? 0 : -12;
                        double len = stepLen * (d.DoubleTime ? 1.05 : 1.5);
                        Voice(music, t0, len, Freq(chordRoot - 24 + (oct == 0 ? 12 : 0)),
                              0.40 * energy, 1, 0.003, 0.07,
                              420 * d.Bright, 160 * d.Bright, d.Drive);
                    }
                }
                else if (six == 0)
                {
                    Voice(music, t0, spb * 2.0, Freq(chordRoot - 24), 0.22, 0, 0.02, 0.5, 300, 160, 0);
                }

                // ---------------- аккорды
                if (six % 8 == 0)
                {
                    double amp = (full ? 0.115 : quiet ? 0.085 : 0.10);
                    double dur = spb * (full ? 1.9 : 3.6);
                    for (int k = 0; k < (full ? 4 : 3); k++)
                    {
                        int tone = chordTones[k % chordTones.Length];
                        int note = chordRoot + d.Scale[(deg + tone) % d.Scale.Length] + (tone >= 4 ? 12 : 0);
                        Voice(music, t0, dur, Freq(note), amp, full ? 3 : 0,
                              full ? 0.01 : 0.12, 0.55, 2400 * d.Bright, 900 * d.Bright, 0);
                    }
                }

                // ---------------- мелодия
                bool playLead = sec switch
                {
                    0 => six % 8 == 0,
                    1 => six % 4 == 0,
                    3 => six % 8 == 0,
                    5 => six % 4 == 0,
                    _ => six % 2 == 0 || (six % 4 == 1 && d.LeadDensity > 0.8)
                };
                if (playLead)
                {
                    int mi = (s / (d.DoubleTime ? 1 : 2)) % d.Motif.Length;
                    int step2 = d.Motif[mi] + deg;
                    int note = chordRoot + d.Scale[step2 % d.Scale.Length]
                               + 12 * (1 + (step2 / d.Scale.Length)) + (full ? 12 : 0);
                    double amp = full ? 0.21 : quiet ? 0.12 : 0.16;
                    Voice(lead, t0, stepLen * (d.DoubleTime ? 1.1 : 1.8), Freq(note), amp,
                          full ? 3 : 2, 0.004, 0.09, 3200 * d.Bright, 1400 * d.Bright, d.Drive * 0.7);
                }

                // ---------------- разгон: шум вверх
                if (build && bar % 8 == 7)
                {
                    double p = six / 16.0;
                    AddNoise(drums, t0, stepLen * 1.2, 0.05 + 0.18 * p, 0.5 + 0.45 * p);
                }
            }

            // ---------------- сведение
            var outBuf = new float[total];
            // эхо для мелодии
            int delay = (int)(stepLen * 3 * SampleRate);
            var echo = new float[total];
            for (int i = 0; i < total; i++)
            {
                float v = lead[i];
                if (i >= delay) v += echo[i - delay] * 0.42f;
                echo[i] = v;
            }

            for (int i = 0; i < total; i++)
                outBuf[i] = drums[i] + music[i] * duck[i] + lead[i] + echo[i] * 0.3f * duck[i];

            // мягкий компрессор и лимитер
            float peak = 0.0001f;
            for (int i = 0; i < total; i++) peak = Math.Max(peak, Math.Abs(outBuf[i]));
            float norm = 0.82f / peak;
            float env = 0;
            for (int i = 0; i < total; i++)
            {
                float v = outBuf[i] * norm;
                float a = Math.Abs(v);
                env += (a - env) * (a > env ? 0.02f : 0.0012f);
                float gain = env > 0.55f ? 0.55f / env : 1f;
                v *= 0.6f + 0.4f * gain;
                outBuf[i] = (float)Math.Tanh(v * 1.35) * 0.95f;
            }

            // плавный вход и выход
            int fade = SampleRate / 2;
            for (int i = 0; i < fade && i < total; i++)
            {
                outBuf[i] *= i / (float)fade;
                outBuf[total - 1 - i] *= i / (float)fade;
            }
            return outBuf;
        }

        // ---------------------------------------------------------------- голоса

        /// <summary>Сайдчейн: бочка прижимает бас и аккорды.</summary>
        static void Duck(float[] duck, double start, double depth, double len)
        {
            int i0 = (int)(start * SampleRate);
            int n = (int)(len * SampleRate);
            for (int i = 0; i < n; i++)
            {
                int idx = i0 + i;
                if (idx < 0 || idx >= duck.Length) break;
                double t = (double)i / n;
                float v = (float)(1 - depth * (1 - t) * (1 - t));
                if (v < duck[idx]) duck[idx] = v;
            }
        }

        /// <summary>kind: 0 синус, 1 пила, 2 квадрат, 3 супер-пила (5 голосов).</summary>
        static void Voice(float[] buf, double start, double dur, double freq, double amp,
                          int kind, double atk, double rel, double cutStart, double cutEnd, double drive)
        {
            int i0 = (int)(start * SampleRate);
            int n = (int)(dur * SampleRate);
            if (i0 < 0 || n <= 0 || freq <= 1) return;

            const int Voices = 5;
            var ph = new double[Voices];
            var inc = new double[Voices];
            int used = kind == 3 ? Voices : 1;
            for (int v = 0; v < used; v++)
            {
                double det = kind == 3 ? 1 + (v - 2) * 0.0035 : 1;
                inc[v] = 2 * Math.PI * freq * det / SampleRate;
                ph[v] = v * 0.37;
            }

            double lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                int idx = i0 + i;
                if (idx >= buf.Length) break;
                double t = (double)i / SampleRate;

                double envv;
                if (t < atk) envv = t / Math.Max(1e-5, atk);
                else
                {
                    double k = (t - atk) / Math.Max(1e-5, dur - atk);
                    envv = Math.Max(0, 1 - k) * Math.Exp(-(t - atk) / Math.Max(1e-4, rel * 3));
                }
                if (envv <= 0.0001) continue;

                double raw = 0;
                for (int v = 0; v < used; v++)
                {
                    double p = ph[v] / (2 * Math.PI) % 1.0;
                    raw += kind switch
                    {
                        0 => Math.Sin(ph[v]),
                        2 => (p < 0.5 ? 1 : -1) * 0.62,
                        _ => 2 * p - 1
                    };
                    ph[v] += inc[v];
                }
                raw /= used;
                if (drive > 0) raw = Math.Tanh(raw * (1 + drive * 5)) * 0.82;

                // фильтр с огибающей — «живой» тембр
                double cut = cutStart + (cutEnd - cutStart) * Math.Min(1, t / Math.Max(0.02, dur));
                double a = 1 - Math.Exp(-2 * Math.PI * Math.Clamp(cut, 60, 16000) / SampleRate);
                lp += (raw - lp) * a;
                lp2 += (lp - lp2) * a;

                buf[idx] += (float)(lp2 * amp * envv);
            }
        }

        static void AddKick(float[] buf, double start, double amp)
        {
            int i0 = (int)(start * SampleRate);
            int n = (int)(0.3 * SampleRate);
            double ph = 0;
            var rnd = new Random(i0);
            for (int i = 0; i < n; i++)
            {
                int idx = i0 + i;
                if (idx < 0 || idx >= buf.Length) break;
                double t = (double)i / SampleRate;
                double f = 165 * Math.Exp(-t * 34) + 46;
                ph += 2 * Math.PI * f / SampleRate;
                double body = Math.Sin(ph) * Math.Exp(-t * 9.5);
                double click = (rnd.NextDouble() * 2 - 1) * Math.Exp(-t * 300) * 0.35;
                buf[idx] += (float)(Math.Tanh((body + click) * 1.6) * amp);
            }
        }

        static void AddClap(float[] buf, double start, double amp)
        {
            // три коротких всплеска подряд — как настоящий клэп
            for (int k = 0; k < 3; k++)
                AddNoise(buf, start + k * 0.011, 0.055, amp * (k == 2 ? 1 : 0.55), 0.78);
            AddNoise(buf, start + 0.03, 0.16, amp * 0.5, 0.6);
        }

        static void AddNoise(float[] buf, double start, double dur, double amp, double bright)
        {
            int i0 = (int)(start * SampleRate);
            int n = (int)(dur * SampleRate);
            var rnd = new Random(i0 + 17);
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
