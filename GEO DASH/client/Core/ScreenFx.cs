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

    /// <summary>
    /// Частица. Это структура в заранее выделенном массиве: за кадр не создаётся
    /// ни одного объекта, поэтому сборщик мусора не дёргает игру посреди уровня.
    /// </summary>
    public struct Particle
    {
        public double X, Y, VX, VY, Life, MaxLife, Size;
        public int Argb;
        public bool Alive;

        public Color Color => Color.FromArgb(Argb);
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

        /// <summary>Пул частиц: живые помечены флагом, место переиспользуется.</summary>
        public readonly Particle[] Particles = new Particle[256];
        int _next;

        public void Reset()
        {
            Invert = Gray = Pixelate = Glitch = Chromatic = Blur = Vignette = Flash = Shock = default;
            GradientOn = false;
            TimeScale = 1;
            for (int i = 0; i < Particles.Length; i++) Particles[i].Alive = false;
            _next = 0;
        }

        public bool AnyPost => Invert.On || Gray.On || Pixelate.On || Glitch.On || Chromatic.On || Blur.On;

        public void Burst(double x, double y, int count, Color col, double power)
        {
            var rnd = Random.Shared;
            int argb = col.ToArgb();
            for (int i = 0; i < count; i++)
            {
                // берём следующее место по кругу: самые старые частицы вытесняются
                ref var p = ref Particles[_next];
                _next = (_next + 1) % Particles.Length;

                double a = rnd.NextDouble() * Math.PI * 2;
                double v = power * (0.4 + rnd.NextDouble());
                p.X = x; p.Y = y;
                p.VX = Math.Cos(a) * v; p.VY = Math.Sin(a) * v;
                p.Life = 0.5 + rnd.NextDouble() * 0.7;
                p.MaxLife = 1.2;
                p.Size = 4 + rnd.NextDouble() * 8;
                p.Argb = argb;
                p.Alive = true;
            }
        }

        public void Update(double dt)
        {
            Invert.Update(dt); Gray.Update(dt); Pixelate.Update(dt); Glitch.Update(dt);
            Chromatic.Update(dt); Blur.Update(dt); Vignette.Update(dt); Flash.Update(dt); Shock.Update(dt);

            for (int i = 0; i < Particles.Length; i++)
            {
                ref var p = ref Particles[i];
                if (!p.Alive) continue;
                p.Life -= dt;
                if (p.Life <= 0) { p.Alive = false; continue; }
                p.X += p.VX * dt;
                p.Y += p.VY * dt;
                p.VY -= 420 * dt;          // мир Y вверх — частицы падают вниз
                p.VX *= 1 - Math.Min(0.9, dt * 1.2);
            }
        }
    }
}
