namespace GEO_DASH.Core
{
    public struct PlayerInput
    {
        public bool Hold;      // кнопка 1 удерживается
        public bool Pressed;   // кнопка 1 нажата в этом кадре
        public bool Released;
        public bool Hold2;     // кнопка 2 (вторая клавиша / ПКМ)
        public bool Pressed2;
        /// <summary>Платформер: -1 влево, +1 вправо, 0 стоим.</summary>
        public int MoveX;

        public static PlayerInput None => new PlayerInput();
    }

    /// <summary>Физические константы по режимам.</summary>
    public static class Phys
    {
        public const double CubeGravity = 2800;
        public const double CubeJump = 661;

        public const double BallGravity = 2500;
        public const double BallMaxFall = 1100;

        public const double ShipGravity = 1500;
        public const double ShipThrust = 3000;
        public const double ShipMaxV = 520;

        public const double UfoGravity = 2300;
        public const double UfoJump = 520;

        public const double WaveSlope = 1.0;      // tan(45°)
        public const double WaveSlopeMini = 2.0;

        public const double RobotGravity = 2800;
        public const double RobotJumpInit = 430;
        public const double RobotThrust = 3400;
        public const double RobotMaxHold = 0.28;

        public const double WormGravity = 2600;
        public const double WormMaxFall = 1200;

        public const double OrbYellow = 661;
        public const double OrbPink = 460;
        public const double OrbRed = 900;
        public const double OrbBlueFlip = 1.0;
        public const double OrbGreenFlip = 1.0;
        public const double PadYellow = 700;
        public const double PadPink = 500;
        public const double PadRed = 1000;
    }

    public class Player
    {
        public double X, Y;             // центр хитбокса, единицы мира
        public double VY;
        public int GravityDir = 1;      // 1 — вниз, -1 — вверх
        public GameMode Mode = GameMode.Cube;
        public SpeedTier Speed = SpeedTier.Normal;
        public bool Mini;
        public bool Mirror;
        public bool Dead;
        public bool Hidden;
        public bool NoClipWorm;         // текущий тик червяк внутри блока
        public bool OnGround;
        public bool OnCeiling;
        public double Rotation;
        public double GravityMul = 1.0;
        public int PlayerIndex;         // 0 — основной, 1 — второй в dual
        public double TrailTimer;
        public bool TrailOn = true;
        public double RobotHold;
        public bool RobotJumping;
        public double LastJumpTime;
        public double SpiderFlash;      // анимация прыжка паука
        public double WormBurrow;       // 0..1 — насколько «в земле»
        public List<PointF> WaveTrail = new();

        public double SizeScale => Mini ? 0.6 : 1.0;
        public double Size => 30 * SizeScale;

        public double SpeedUnits => Speeds.UnitsPerSecond(Speed);

        public RectangleF Box
        {
            get
            {
                double s = Size;
                return new RectangleF((float)(X - s / 2), (float)(Y - s / 2), (float)s, (float)s);
            }
        }

        /// <summary>Внутренний хитбокс для смертельных объектов (меньше, как в GD).</summary>
        public RectangleF InnerBox
        {
            get
            {
                double s = Size * 0.62;
                return new RectangleF((float)(X - s / 2), (float)(Y - s / 2), (float)s, (float)s);
            }
        }

        public void Reset(double x, double y, LevelSettings st)
        {
            X = x; Y = y; VY = 0; Dead = false;
            Mode = st.StartMode; Speed = st.StartSpeed;
            Mini = st.StartMini; Mirror = st.StartMirror;
            GravityDir = st.StartGravityUp ? -1 : 1;
            GravityMul = st.GravityScale <= 0 ? 1 : st.GravityScale;
            Rotation = 0; OnGround = false; OnCeiling = false;
            RobotHold = 0; RobotJumping = false; WormBurrow = 0;
            WaveTrail.Clear();
            Hidden = false;
        }

        public double Gravity => Mode switch
        {
            GameMode.Cube => Phys.CubeGravity,
            GameMode.Ball => Phys.BallGravity,
            GameMode.Ship => Phys.ShipGravity,
            GameMode.Ufo => Phys.UfoGravity,
            GameMode.Robot => Phys.RobotGravity,
            GameMode.Spider => Phys.CubeGravity,
            GameMode.Worm => Phys.WormGravity,
            _ => Phys.CubeGravity
        } * GravityMul * (Mini ? 1.15 : 1.0);

        /// <summary>Вертикальная физика до разрешения столкновений.
        /// Ось Y направлена вверх: VY &gt; 0 — движение вверх.</summary>
        public void Integrate(double dt, PlayerInput inp, ref SpiderRequest spider)
        {
            double g = -Gravity * GravityDir;

            switch (Mode)
            {
                case GameMode.Cube:
                    VY += g * dt;
                    if (inp.Hold && OnGround) Jump(Phys.CubeJump);
                    break;

                case GameMode.Ship:
                    VY += (inp.Hold ? Phys.ShipThrust * GravityDir : g) * dt;
                    VY = Clamp(VY, -Phys.ShipMaxV, Phys.ShipMaxV);
                    break;

                case GameMode.Ball:
                    VY += g * dt;
                    VY = Clamp(VY, -Phys.BallMaxFall, Phys.BallMaxFall);
                    if (inp.Pressed && (OnGround || OnCeiling))
                    {
                        GravityDir = -GravityDir;
                        VY = 0;
                        OnGround = OnCeiling = false;
                    }
                    break;

                case GameMode.Ufo:
                    VY += g * dt;
                    if (inp.Pressed) Jump(Phys.UfoJump);
                    break;

                case GameMode.Wave:
                    {
                        double slope = Mini ? Phys.WaveSlopeMini : Phys.WaveSlope;
                        VY = (inp.Hold ? 1 : -1) * GravityDir * SpeedUnits * slope;
                        break;
                    }

                case GameMode.Robot:
                    VY += g * dt;
                    if (inp.Pressed && OnGround)
                    {
                        RobotJumping = true; RobotHold = 0;
                        VY = Phys.RobotJumpInit * GravityDir;
                        OnGround = false;
                    }
                    if (RobotJumping)
                    {
                        if (inp.Hold && RobotHold < Phys.RobotMaxHold)
                        {
                            RobotHold += dt;
                            VY += Phys.RobotThrust * GravityDir * dt;
                        }
                        else RobotJumping = false;
                    }
                    break;

                case GameMode.Spider:
                    VY += g * dt;
                    if (inp.Pressed)
                    {
                        spider.Requested = true;   // телепорт на противоположную поверхность
                        spider.FlipTo = -GravityDir;
                    }
                    break;

                case GameMode.Worm:
                    // червяк: клик 1 — мгновенно переворачивает гравитацию,
                    // сквозь блоки проходит (burrow), шипы убивают
                    if (inp.Pressed)
                    {
                        GravityDir = -GravityDir;
                        VY = 0;
                        OnGround = OnCeiling = false;
                    }
                    if (NoClipWorm)
                    {
                        VY = 0;             // внутри блока ползём строго вперёд
                        WormBurrow = Math.Min(1, WormBurrow + dt * 6);
                    }
                    else
                    {
                        VY += g * dt;
                        VY = Clamp(VY, -Phys.WormMaxFall, Phys.WormMaxFall);
                        WormBurrow = Math.Max(0, WormBurrow - dt * 6);
                    }
                    break;
            }
        }

        public void Jump(double power, bool force = false)
        {
            VY = power * GravityDir;
            OnGround = false;
            OnCeiling = false;
        }

        public void FlipGravity()
        {
            GravityDir = -GravityDir;
            OnGround = OnCeiling = false;
        }

        public void UpdateRotation(double dt)
        {
            switch (Mode)
            {
                case GameMode.Cube:
                case GameMode.Robot:
                case GameMode.Spider:
                    if (!OnGround && !OnCeiling)
                        Rotation += 418 * dt * GravityDir * (Mirror ? -1 : 1);
                    else
                        Rotation = SnapAngle(Rotation);
                    break;
                case GameMode.Ball:
                    Rotation += SpeedUnits * dt * 1.6 * GravityDir * (Mirror ? -1 : 1);
                    break;
                case GameMode.Ship:
                case GameMode.Ufo:
                    {
                        double target = -Clamp(VY * 0.045, -32, 32) * GravityDir;
                        Rotation += (target - Rotation) * Math.Min(1, dt * 14);
                        break;
                    }
                case GameMode.Wave:
                    Rotation = 0;
                    break;
                case GameMode.Worm:
                    {
                        double target = NoClipWorm ? 0 : -Clamp(VY * 0.03, -24, 24) * GravityDir;
                        Rotation += (target - Rotation) * Math.Min(1, dt * 12);
                        break;
                    }
            }
            if (Rotation > 3600 || Rotation < -3600) Rotation %= 360;
        }

        static double SnapAngle(double a)
        {
            double r = Math.Round(a / 90.0) * 90.0;
            return r;
        }

        public static double Clamp(double v, double a, double b) => v < a ? a : (v > b ? b : v);
    }

    public struct SpiderRequest
    {
        public bool Requested;
        public int FlipTo;
    }
}
