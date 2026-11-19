namespace GEO_DASH.Core
{
    /// <summary>Каналы цветов с плавными переходами (Color trigger) и пульсами (Pulse trigger).</summary>
    public class ColorSystem
    {
        public class Chan
        {
            public Color Current = Color.White;
            public Color From = Color.White;
            public Color To = Color.White;
            public double T, Dur;
            public bool Blending;
            public double Opacity = 1;

            // пульс
            public Color PulseColor = Color.White;
            public double PulseT, PulseFadeIn, PulseHold, PulseFadeOut;
            public bool PulseActive;

            public Color Effective
            {
                get
                {
                    Color c = Current;
                    if (PulseActive)
                    {
                        double k = PulseFactor();
                        c = Lerp(c, PulseColor, k);
                    }
                    return c;
                }
            }

            public double PulseFactor()
            {
                double t = PulseT;
                if (t < PulseFadeIn && PulseFadeIn > 0) return t / PulseFadeIn;
                t -= PulseFadeIn;
                if (t < PulseHold) return 1;
                t -= PulseHold;
                if (PulseFadeOut <= 0) return 0;
                return Math.Max(0, 1 - t / PulseFadeOut);
            }

            public static Color Lerp(Color a, Color b, double k)
            {
                k = Math.Clamp(k, 0, 1);
                return Color.FromArgb(
                    (int)(a.A + (b.A - a.A) * k),
                    (int)(a.R + (b.R - a.R) * k),
                    (int)(a.G + (b.G - a.G) * k),
                    (int)(a.B + (b.B - a.B) * k));
            }
        }

        public readonly Dictionary<int, Chan> Channels = new();

        public const int ChBg = 1000;
        public const int ChGround = 1001;
        public const int ChLine = 1002;
        public const int ChObj = 1;
        public const int ChObj2 = 2;
        public const int ChP1 = 1005;
        public const int ChP2 = 1006;

        public void Init(LevelData lv)
        {
            Channels.Clear();
            Set(ChBg, Color.FromArgb(lv.Settings.BgColor));
            Set(ChGround, Color.FromArgb(lv.Settings.GroundColor));
            Set(ChLine, Color.FromArgb(lv.Settings.LineColor));
            Set(ChObj, Color.FromArgb(lv.Settings.ObjColor));
            Set(ChObj2, Color.FromArgb(lv.Settings.Obj2Color));
            Set(ChP1, Color.FromArgb(255, 70, 220, 255));
            Set(ChP2, Color.FromArgb(255, 255, 220, 70));
            foreach (var c in lv.Colors) Set(c.Id, Color.FromArgb(c.Color), c.Blending, c.Opacity);
        }

        public Chan Get(int id)
        {
            if (!Channels.TryGetValue(id, out var c))
            {
                c = new Chan();
                Channels[id] = c;
            }
            return c;
        }

        public void Set(int id, Color col, bool blending = false, double opacity = 1)
        {
            var c = Get(id);
            c.Current = c.From = c.To = col;
            c.T = c.Dur = 0;
            c.Blending = blending;
            c.Opacity = opacity;
        }

        public void FadeTo(int id, Color col, double dur, bool blending, double opacity)
        {
            var c = Get(id);
            c.From = c.Current;
            c.To = col;
            c.Dur = Math.Max(0, dur);
            c.T = 0;
            c.Blending = blending;
            c.Opacity = opacity;
            if (c.Dur <= 0) c.Current = col;
        }

        public void Pulse(int id, Color col, double fi, double hold, double fo)
        {
            var c = Get(id);
            c.PulseColor = col;
            c.PulseFadeIn = fi; c.PulseHold = hold; c.PulseFadeOut = fo;
            c.PulseT = 0; c.PulseActive = true;
        }

        public void Update(double dt)
        {
            foreach (var c in Channels.Values)
            {
                if (c.Dur > 0)
                {
                    c.T += dt;
                    double k = Math.Clamp(c.T / c.Dur, 0, 1);
                    c.Current = Chan.Lerp(c.From, c.To, k);
                    if (k >= 1) { c.Dur = 0; c.Current = c.To; }
                }
                if (c.PulseActive)
                {
                    c.PulseT += dt;
                    if (c.PulseT > c.PulseFadeIn + c.PulseHold + c.PulseFadeOut) c.PulseActive = false;
                }
            }
        }

        public Color Eff(int id) => Get(id).Effective;
    }
}
