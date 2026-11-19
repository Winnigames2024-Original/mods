namespace GEO_DASH.Core
{
    /// <summary>Один экранный эффект: сколько осталось и насколько сильный.</summary>
    public struct FxSlot
    {
        public double T, Dur, Str;

        public void Set(double dur, double str)
        {
            Dur = Math.Max(0.02, dur);
            T = Dur;
            Str = Math.Clamp(str, 0, 1);
        }

        public void Update(double dt) { if (T > 0) T = Math.Max(0, T - dt); }

        /// <summary>Сила прямо сейчас: к концу затухает.</summary>
        public double K => T <= 0 || Dur <= 0 ? 0 : Math.Clamp(T / Dur, 0, 1) * Str;
        public bool On => T > 0 && Str > 0.001;
    }

    public class Particle
    {
        public double X, Y, VX, VY, Life, MaxLife, Size;
        public Color Color;
    }

    /// <summary>
    /// Экранные эффекты как в 2.2: инверсия, ч/б, пиксели, глюк, хроматика,
    /// размытие, виньетка, вспышка, ударная волна, градиент и частицы.
    /// </summary>
    public class ScreenFx
    {
        public FxSlot Invert, Gray, Pixelate, Glitch, Chromatic, Blur, Vignette, Flash, Shock;
        public Color FlashColor = Color.White;
        public double ShockX, ShockY;

        /// <summary>Градиент поверх фона: держится, пока не выключат.</summary>
        public bool GradientOn;
        public Color GradTop = Color.FromArgb(0, 0, 0, 0), GradBottom = Color.FromArgb(0, 0, 0, 0);

        /// <summary>Множитель хода времени (TimeWarp).</summary>
        public double TimeScale = 1;

        public readonly List<Particle> Particles = new();

        public void Reset()
        {
            Invert = Gray = Pixelate = Glitch = Chromatic = Blur = Vignette = Flash = Shock = default;
            GradientOn = false;
            TimeScale = 1;
            Particles.Clear();
        }

        public bool AnyPost => Invert.On || Gray.On || Pixelate.On || Glitch.On || Chromatic.On || Blur.On;

        public void Burst(double x, double y, int count, Color col, double power)
        {
            var rnd = Random.Shared;
            for (int i = 0; i < count; i++)
            {
                double a = rnd.NextDouble() * Math.PI * 2;
                double v = power * (0.4 + rnd.NextDouble());
                Particles.Add(new Particle
                {
                    X = x, Y = y,
                    VX = Math.Cos(a) * v, VY = Math.Sin(a) * v,
                    Life = 0.5 + rnd.NextDouble() * 0.7,
                    MaxLife = 1.2,
                    Size = 4 + rnd.NextDouble() * 8,
                    Color = col
                });
            }
            if (Particles.Count > 600) Particles.RemoveRange(0, Particles.Count - 600);
        }

        public void Update(double dt)
        {
            Invert.Update(dt); Gray.Update(dt); Pixelate.Update(dt); Glitch.Update(dt);
            Chromatic.Update(dt); Blur.Update(dt); Vignette.Update(dt); Flash.Update(dt); Shock.Update(dt);

            for (int i = Particles.Count - 1; i >= 0; i--)
            {
                var p = Particles[i];
                p.Life -= dt;
                if (p.Life <= 0) { Particles.RemoveAt(i); continue; }
                p.X += p.VX * dt;
                p.Y += p.VY * dt;
                p.VY -= 420 * dt;          // мир Y вверх — частицы падают вниз
                p.VX *= 1 - Math.Min(0.9, dt * 1.2);
            }
        }
    }
}
