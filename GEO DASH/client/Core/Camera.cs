namespace GEO_DASH.Core
{
    public class Camera
    {
        public double X, Y;
        public double Zoom = 1;
        public double OffsetX, OffsetY;
        public bool Static;
        public double ShakeStrength, ShakeInterval, ShakeTimer, ShakeT;
        public double ShakeOffX, ShakeOffY;

        double _targetZoom = 1, _zoomSpeed = 0;
        double _targetOffX, _targetOffY, _offSpeed = 0;
        readonly Random _rnd = new();

        /// <summary>Логический размер кадра в единицах мира (как в GD: 480 ед. по высоте).</summary>
        public const double ViewH = 480;

        public void Follow(Player p, double dt, double groundY, double ceilY)
        {
            if (Static) { ApplyShake(dt); return; }

            double targetX = p.X + 180;
            X += (targetX - X) * Math.Min(1, dt * 12);

            // вертикаль: камера тянется за игроком, но держит землю в кадре
            double half = ViewH / 2 / Zoom;
            double desired = Y;
            if (p.Y > Y + half * 0.45) desired = p.Y - half * 0.45;
            else if (p.Y < Y - half * 0.45) desired = p.Y + half * 0.45;

            bool freeFly = p.Mode is GameMode.Ship or GameMode.Ufo or GameMode.Wave;
            double k = freeFly ? dt * 7 : dt * 4.5;
            Y += (desired - Y) * Math.Min(1, k);

            double minY = groundY + half - 30;
            if (Y < minY) Y = minY;

            ApplyShake(dt);
            UpdateSmooth(dt);
        }

        void UpdateSmooth(double dt)
        {
            if (_zoomSpeed > 0) Zoom += (_targetZoom - Zoom) * Math.Min(1, dt * _zoomSpeed);
            else Zoom = _targetZoom;
            if (_offSpeed > 0)
            {
                OffsetX += (_targetOffX - OffsetX) * Math.Min(1, dt * _offSpeed);
                OffsetY += (_targetOffY - OffsetY) * Math.Min(1, dt * _offSpeed);
            }
            else { OffsetX = _targetOffX; OffsetY = _targetOffY; }
        }

        public void SetZoom(double z, double dur)
        {
            _targetZoom = Math.Clamp(z, 0.3, 3);
            _zoomSpeed = dur > 0 ? 1.0 / dur * 2.5 : 0;
        }

        public void SetOffset(double x, double y, double dur)
        {
            _targetOffX = x; _targetOffY = y;
            _offSpeed = dur > 0 ? 1.0 / dur * 2.5 : 0;
        }

        public void Shake(double strength, double interval, double dur)
        {
            ShakeStrength = strength; ShakeInterval = Math.Max(0.01, interval);
            ShakeTimer = dur; ShakeT = 0;
        }

        void ApplyShake(double dt)
        {
            if (ShakeTimer <= 0) { ShakeOffX = ShakeOffY = 0; return; }
            ShakeTimer -= dt;
            ShakeT -= dt;
            if (ShakeT <= 0)
            {
                ShakeT = ShakeInterval;
                ShakeOffX = (_rnd.NextDouble() * 2 - 1) * ShakeStrength;
                ShakeOffY = (_rnd.NextDouble() * 2 - 1) * ShakeStrength;
            }
        }

        public void Reset(double x, double y)
        {
            X = x + 180; Y = y; Zoom = 1; OffsetX = OffsetY = 0;
            _targetZoom = 1; _targetOffX = _targetOffY = 0;
            ShakeTimer = 0; ShakeOffX = ShakeOffY = 0; Static = false;
        }

        public double CamX => X + OffsetX + ShakeOffX;
        public double CamY => Y + OffsetY + ShakeOffY;
    }
}
