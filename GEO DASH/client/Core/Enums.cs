namespace GEO_DASH.Core
{
    /// <summary>Режимы игры. Worm — наш новый режим (червяк).</summary>
    public enum GameMode
    {
        Cube = 0,
        Ship = 1,
        Ball = 2,
        Ufo = 3,
        Wave = 4,
        Robot = 5,
        Spider = 6,
        Worm = 7
    }

    /// <summary>Скорости. Hyper (+5) и Ultra (+6) — сверх обычных четырёх.</summary>
    public enum SpeedTier
    {
        Slow = 0,     // 0.5x
        Normal = 1,   // 1x
        Fast = 2,     // 2x
        Faster = 3,   // 3x
        Fastest = 4,  // 4x
        Hyper = 5,    // 5x  (+5)
        Ultra = 6     // 6x  (+6)
    }

    public enum Difficulty
    {
        NA = 0,
        Auto = 1,
        Easy = 2,
        Normal = 3,
        Hard = 4,
        Harder = 5,
        Insane = 6,
        EasyDemon = 7,
        MediumDemon = 8,
        HardDemon = 9,
        InsaneDemon = 10,
        ExtremeDemon = 11
    }

    public enum ObjKind
    {
        Block = 0,
        Hazard = 1,
        Portal = 2,
        Orb = 3,
        Pad = 4,
        Trigger = 5,
        Deco = 6,
        Coin = 7,
        Slope = 8,
        Text = 9,
        Finish = 10,
        /// <summary>Вода и лава — зоны, в которых куб плавает.</summary>
        Liquid = 11
    }

    public enum PortalKind
    {
        None = 0,
        ModeCube, ModeShip, ModeBall, ModeUfo, ModeWave, ModeRobot, ModeSpider, ModeWorm,
        GravityUp, GravityDown, GravityToggle,
        Mini, Big,
        Dual, Single,
        Mirror, Unmirror,
        SpeedSlow, SpeedNormal, SpeedFast, SpeedFaster, SpeedFastest, SpeedHyper, SpeedUltra,
        Teleport
    }

    public enum OrbKind
    {
        Yellow = 0, Pink, Blue, Green, Red, Black, Dash, GravityDash, Spider, Toggle
    }

    public enum PadKind
    {
        Yellow = 0, Pink, Blue, Red, Spider
    }

    /// <summary>Все триггеры.</summary>
    public enum TriggerType
    {
        Move = 0,
        Rotate,
        Scale,
        Alpha,
        Toggle,
        Spawn,
        Stop,
        Pulse,
        Color,
        BgColor,
        GroundColor,
        Shake,
        Follow,
        FollowPlayerY,
        Animate,
        Touch,
        Count,
        InstantCount,
        Pickup,
        Collision,
        InstantCollision,
        Random,
        Timer,
        Camera,
        CameraZoom,
        Song,
        Gravity,
        Teleport,
        OnDeath,
        Trail,
        HidePlayer,
        ShowPlayer,
        Options,
        Checkpoint,
        End,
        Reverse,
        Keyframe,
        SpeedChange
    }

    public enum EasingType
    {
        None = 0, EaseInOut, EaseIn, EaseOut, ElasticInOut, ElasticIn, ElasticOut,
        BounceInOut, BounceIn, BounceOut, ExponentialInOut, SineInOut, BackInOut
    }

    public static class Speeds
    {
        /// <summary>Множители к базовой скорости (311.58 ед/с) для каждого тира.</summary>
        public static readonly double[] Mul = { 0.807, 1.0, 1.243, 1.502, 1.849, 2.25, 2.70 };

        public const double Base = 311.58;

        public static double UnitsPerSecond(SpeedTier t)
        {
            int i = (int)t;
            if (i < 0) i = 0;
            if (i >= Mul.Length) i = Mul.Length - 1;
            return Base * Mul[i];
        }

        public static string Label(SpeedTier t) => t switch
        {
            SpeedTier.Slow => "0.5x",
            SpeedTier.Normal => "1x",
            SpeedTier.Fast => "2x",
            SpeedTier.Faster => "3x",
            SpeedTier.Fastest => "4x",
            SpeedTier.Hyper => "5x (+5)",
            SpeedTier.Ultra => "6x (+6)",
            _ => "1x"
        };
    }

    public static class Difficulties
    {
        public static string Label(Difficulty d) => d switch
        {
            Difficulty.NA => "Без оценки",
            Difficulty.Auto => "Auto",
            Difficulty.Easy => "Easy",
            Difficulty.Normal => "Normal",
            Difficulty.Hard => "Hard",
            Difficulty.Harder => "Harder",
            Difficulty.Insane => "Insane",
            Difficulty.EasyDemon => "Easy Demon",
            Difficulty.MediumDemon => "Medium Demon",
            Difficulty.HardDemon => "Hard Demon",
            Difficulty.InsaneDemon => "Insane Demon",
            Difficulty.ExtremeDemon => "Extreme Demon",
            _ => "?"
        };

        public static int Stars(Difficulty d) => d switch
        {
            Difficulty.Auto => 1,
            Difficulty.Easy => 2,
            Difficulty.Normal => 3,
            Difficulty.Hard => 5,
            Difficulty.Harder => 7,
            Difficulty.Insane => 9,
            Difficulty.EasyDemon => 10,
            Difficulty.MediumDemon => 10,
            Difficulty.HardDemon => 10,
            Difficulty.InsaneDemon => 10,
            Difficulty.ExtremeDemon => 10,
            _ => 0
        };

        public static bool IsDemon(Difficulty d) => d >= Difficulty.EasyDemon;

        public static Color Tint(Difficulty d) => d switch
        {
            Difficulty.Auto => Color.FromArgb(120, 220, 255),
            Difficulty.Easy => Color.FromArgb(90, 220, 130),
            Difficulty.Normal => Color.FromArgb(120, 190, 255),
            Difficulty.Hard => Color.FromArgb(255, 205, 90),
            Difficulty.Harder => Color.FromArgb(255, 140, 70),
            Difficulty.Insane => Color.FromArgb(255, 90, 120),
            _ => d >= Difficulty.EasyDemon ? Color.FromArgb(200, 60, 230) : Color.FromArgb(160, 160, 160)
        };
    }

    public static class Modes
    {
        public static string Label(GameMode m) => m switch
        {
            GameMode.Cube => "Куб",
            GameMode.Ship => "Корабль",
            GameMode.Ball => "Шар",
            GameMode.Ufo => "НЛО",
            GameMode.Wave => "Волна",
            GameMode.Robot => "Робот",
            GameMode.Spider => "Паук",
            GameMode.Worm => "Червяк",
            _ => "?"
        };
    }
}
