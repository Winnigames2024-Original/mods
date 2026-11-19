using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace GEO_DASH.Audio
{
    /// <summary>Звуки генерируются кодом (никаких чужих файлов): щелчки, прыжки, смерть, монета.</summary>
    public static class Sfx
    {
        class Cached
        {
            public float[] Data;
            public WaveFormat Format;
        }

        class CachedProvider : ISampleProvider
        {
            readonly Cached _s; int _pos; readonly float _vol;
            public CachedProvider(Cached s, float vol) { _s = s; _vol = vol; }
            public WaveFormat WaveFormat => _s.Format;
            public int Read(float[] buffer, int offset, int count)
            {
                int avail = _s.Data.Length - _pos;
                int n = Math.Min(avail, count);
                for (int i = 0; i < n; i++) buffer[offset + i] = _s.Data[_pos + i] * _vol;
                _pos += n;
                return n;
            }
        }

        static readonly WaveFormat Fmt = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
        static MixingSampleProvider _mixer;
        static IWavePlayer _out;
        static readonly Dictionary<string, Cached> _bank = new();
        public static float Volume = 0.55f;
        public static bool Enabled = true;
        static bool _failed;

        public static void Init()
        {
            if (_out != null || _failed) return;
            try
            {
                _mixer = new MixingSampleProvider(Fmt) { ReadFully = true };
                _out = new WaveOutEvent { DesiredLatency = 80 };
                _out.Init(_mixer);
                _out.Play();
                Build();
            }
            catch { _failed = true; }
        }

        public static void Play(string name)
        {
            if (!Enabled || _failed) return;
            Init();
            if (_mixer == null) return;
            if (!_bank.TryGetValue(name, out var s)) return;
            try { _mixer.AddMixerInput(new CachedProvider(s, Volume)); } catch { }
        }

        static void Build()
        {
            _bank["jump"] = Tone(new[] { (520.0, 0.05), (720.0, 0.05) }, 0.25, Wave.Square);
            _bank["orb"] = Tone(new[] { (660.0, 0.04), (990.0, 0.06), (1320.0, 0.05) }, 0.22, Wave.Sine);
            _bank["portal"] = Tone(new[] { (300.0, 0.06), (600.0, 0.06), (900.0, 0.08) }, 0.2, Wave.Saw);
            _bank["coin"] = Tone(new[] { (1046.0, 0.05), (1318.0, 0.05), (1568.0, 0.1) }, 0.2, Wave.Sine);
            _bank["death"] = Noise(0.32, 0.3);
            _bank["win"] = Tone(new[] { (523.0, 0.12), (659.0, 0.12), (784.0, 0.12), (1046.0, 0.26) }, 0.22, Wave.Sine);
            _bank["click"] = Tone(new[] { (900.0, 0.02) }, 0.12, Wave.Square);
        }

        enum Wave { Sine, Square, Saw }

        static Cached Tone((double freq, double dur)[] parts, double amp, Wave w)
        {
            int total = 0;
            foreach (var p in parts) total += (int)(p.dur * 44100);
            var data = new float[total];
            int idx = 0;
            foreach (var p in parts)
            {
                int n = (int)(p.dur * 44100);
                for (int i = 0; i < n; i++)
                {
                    double t = i / 44100.0;
                    double ph = 2 * Math.PI * p.freq * t;
                    double v = w switch
                    {
                        Wave.Sine => Math.Sin(ph),
                        Wave.Square => Math.Sign(Math.Sin(ph)),
                        _ => 2 * (p.freq * t % 1.0) - 1
                    };
                    double env = Math.Min(1, i / 120.0) * (1 - (double)i / n);
                    data[idx + i] = (float)(v * amp * env);
                }
                idx += n;
            }
            return new Cached { Data = data, Format = Fmt };
        }

        static Cached Noise(double dur, double amp)
        {
            int n = (int)(dur * 44100);
            var data = new float[n];
            var rnd = new Random(1234);
            double low = 0;
            for (int i = 0; i < n; i++)
            {
                double white = rnd.NextDouble() * 2 - 1;
                low = low * 0.86 + white * 0.14;
                double env = 1 - (double)i / n;
                data[i] = (float)(low * amp * env * env);
            }
            return new Cached { Data = data, Format = Fmt };
        }

        public static void Dispose()
        {
            try { _out?.Stop(); _out?.Dispose(); } catch { }
            _out = null; _mixer = null;
        }
    }
}
