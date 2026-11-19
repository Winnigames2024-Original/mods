using System.Text.Json;
using System.Text.Json.Serialization;

namespace GEO_DASH.Core
{
    /// <summary>Настройки уровня — то, что видно в окне «Настройки уровня» редактора.</summary>
    public class LevelSettings
    {
        public string Name { get; set; } = "Новый уровень";
        public string Description { get; set; } = "";
        public string Author { get; set; } = "";

        // стартовые режимы — «режимы игры в настройках уровня»
        public GameMode StartMode { get; set; } = GameMode.Cube;
        public SpeedTier StartSpeed { get; set; } = SpeedTier.Normal;
        public bool StartMini { get; set; }
        public bool StartDual { get; set; }
        public bool StartMirror { get; set; }
        public bool StartGravityUp { get; set; }

        /// <summary>Сложность, которую ставит автор (на сервере может переоценить админ).</summary>
        public Difficulty Difficulty { get; set; } = Difficulty.NA;

        // музыка по ID (Newgrounds)
        public int SongId { get; set; } = 0;
        public string SongName { get; set; } = "";
        public string SongArtist { get; set; } = "";
        public double SongOffsetSec { get; set; } = 0;
        public bool SongFadeIn { get; set; } = true;
        public bool SongFadeOut { get; set; } = true;

        // оформление
        public int BgId { get; set; } = 1;
        public int GroundId { get; set; } = 1;
        public int BgColor { get; set; } = unchecked((int)0xFF2040A0);
        public int GroundColor { get; set; } = unchecked((int)0xFF1A2E80);
        public int LineColor { get; set; } = unchecked((int)0xFFFFFFFF);
        public int ObjColor { get; set; } = unchecked((int)0xFF000000);
        public int Obj2Color { get; set; } = unchecked((int)0xFFFFFFFF);

        public bool TwoPlayer { get; set; }
        public bool AllowMultiJump { get; set; }
        public bool ReverseGameplay { get; set; }
        public bool PlatformerFreeMove { get; set; }
        public double GravityScale { get; set; } = 1.0;

        public double LengthUnits { get; set; } = 0; // считается автоматически
        public int Version { get; set; } = 1;
        public string LocalId { get; set; } = Guid.NewGuid().ToString("N");
        public int ServerId { get; set; } = 0;
        /// <summary>Пароль владельца уровня (для перезаписи на сервере).</summary>
        public string Password { get; set; } = "";

        /// <summary>Защита от копирования: 0 — нельзя, 1 — по паролю, 2 — свободно.</summary>
        public int CopyMode { get; set; } = 0;
        /// <summary>Пароль для копирования уровня в редактор (CopyMode = 1).</summary>
        public string CopyPassword { get; set; } = "";
    }

    /// <summary>Параметры триггера. Один класс на все типы — лишние поля просто не используются.</summary>
    public class TriggerData
    {
        public TriggerType Type { get; set; } = TriggerType.Move;
        public int TargetGroup { get; set; }
        public int CenterGroup { get; set; }
        public int SecondaryGroup { get; set; }

        public double MoveX { get; set; }
        public double MoveY { get; set; }
        public double Duration { get; set; } = 0.5;
        public EasingType Easing { get; set; } = EasingType.None;
        public double EaseRate { get; set; } = 2.0;
        public bool LockToPlayerX { get; set; }
        public bool LockToPlayerY { get; set; }

        public double Degrees { get; set; }
        public bool RotateLock { get; set; }

        public double ScaleX { get; set; } = 1;
        public double ScaleY { get; set; } = 1;

        public double Opacity { get; set; } = 1;
        public bool Activate { get; set; } = true;   // Toggle / Spawn / Stop / Trail
        public double SpawnDelay { get; set; }
        public bool TouchHold { get; set; }
        public bool DualTouch { get; set; }
        public bool MultiTrigger { get; set; }
        public bool TouchTriggered { get; set; }
        public bool SpawnTriggered { get; set; }

        public int ColorChannel { get; set; } = 1;
        public int Color { get; set; } = unchecked((int)0xFFFFFFFF);
        public double FadeTime { get; set; } = 0.5;
        public bool Blending { get; set; }
        public int CopyChannel { get; set; }
        public double PlayerColorMix { get; set; }

        public double PulseFadeIn { get; set; } = 0;
        public double PulseHold { get; set; } = 0.2;
        public double PulseFadeOut { get; set; } = 0.3;
        public bool PulseExclusive { get; set; }
        public bool PulseToGroup { get; set; } = true;

        public double ShakeStrength { get; set; } = 10;
        public double ShakeInterval { get; set; } = 0.05;

        public double FollowXMod { get; set; } = 1;
        public double FollowYMod { get; set; } = 1;
        public double FollowDelay { get; set; }
        public double FollowMaxSpeed { get; set; } = 0;

        public int AnimationId { get; set; }

        public int ItemId { get; set; } = 1;
        public int Count { get; set; } = 1;
        public int TargetCount { get; set; } = 1;
        public bool CountMultiActivate { get; set; }

        public int BlockA { get; set; }
        public int BlockB { get; set; }

        public double Chance { get; set; } = 50;
        public int GroupB { get; set; }

        public double TimerSeconds { get; set; } = 1;
        public bool TimerCountDown { get; set; } = true;

        public double CamOffsetX { get; set; }
        public double CamOffsetY { get; set; }
        public double CamZoom { get; set; } = 1;
        public bool CamStatic { get; set; }
        public bool CamFreeMode { get; set; }

        public int SongId { get; set; }
        public double SongVolume { get; set; } = 1;
        public double SongSpeed { get; set; } = 1;
        public double SongStartSec { get; set; }
        public bool SongLoop { get; set; }

        public int GravityDir { get; set; } = 0;  // 0 toggle, 1 down, -1 up
        public double GravityMul { get; set; } = 1;

        public double TeleportY { get; set; }
        public bool TeleportSmooth { get; set; }

        public SpeedTier Speed { get; set; } = SpeedTier.Normal;

        public GameMode OptMode { get; set; } = GameMode.Cube;
        public bool OptMini { get; set; }
        public bool OptDual { get; set; }
        public bool OptMirror { get; set; }
        public bool OptChangeMode { get; set; }

        public bool HidePlayerFlag { get; set; }
        public double Progress { get; set; }

        public TriggerData Clone() => (TriggerData)MemberwiseClone();
    }

    public class LevelObject
    {
        public int Id { get; set; }
        public int TypeId { get; set; }          // id из ObjectCatalog
        public double X { get; set; }
        public double Y { get; set; }
        public double Rotation { get; set; }
        public double ScaleX { get; set; } = 1;
        public double ScaleY { get; set; } = 1;
        public bool FlipX { get; set; }
        public bool FlipY { get; set; }
        public int ZOrder { get; set; }
        public int BaseColorChannel { get; set; } = 1;
        public int DetailColorChannel { get; set; } = 2;
        public double Opacity { get; set; } = 1;
        public List<int> Groups { get; set; } = new();
        public PortalKind Portal { get; set; } = PortalKind.None;
        public OrbKind Orb { get; set; } = OrbKind.Yellow;
        public PadKind Pad { get; set; } = PadKind.Yellow;
        public SpeedTier PortalSpeed { get; set; } = SpeedTier.Normal;
        public GameMode PortalMode { get; set; } = GameMode.Cube;
        public TriggerData Trigger { get; set; }
        public string Text { get; set; } = "";
        public int CoinIndex { get; set; }
        public double TeleportTargetY { get; set; }

        [JsonIgnore] public object RuntimeTag;

        public LevelObject Clone()
        {
            var o = (LevelObject)MemberwiseClone();
            o.Groups = new List<int>(Groups);
            o.Trigger = Trigger?.Clone();
            o.RuntimeTag = null;
            return o;
        }
    }

    public class ColorChannelDef
    {
        public int Id { get; set; }
        public int Color { get; set; } = unchecked((int)0xFFFFFFFF);
        public bool Blending { get; set; }
        public double Opacity { get; set; } = 1;
    }

    public class LevelData
    {
        public int Format { get; set; } = 1;
        public LevelSettings Settings { get; set; } = new();
        public List<LevelObject> Objects { get; set; } = new();
        public List<ColorChannelDef> Colors { get; set; } = new();

        [JsonIgnore] private int _nextId = 1;

        public int NextObjectId()
        {
            foreach (var o in Objects) if (o.Id >= _nextId) _nextId = o.Id + 1;
            return _nextId++;
        }

        public void Add(LevelObject o)
        {
            if (o.Id == 0) o.Id = NextObjectId();
            Objects.Add(o);
        }

        public double RightEdge()
        {
            double max = 30 * 40;
            foreach (var o in Objects) if (o.X + 30 > max) max = o.X + 30;
            return max;
        }

        public ColorChannelDef GetChannel(int id)
        {
            foreach (var c in Colors) if (c.Id == id) return c;
            var def = new ColorChannelDef { Id = id, Color = id == 1 ? unchecked((int)0xFF000000) : unchecked((int)0xFFFFFFFF) };
            Colors.Add(def);
            return def;
        }

        // Пишем все поля (кроме null): формат .gdl читает и Android-клиент,
        // поэтому пропуски значений по умолчанию недопустимы.
        static readonly JsonSerializerOptions Opt = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        static readonly JsonSerializerOptions OptPretty = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        public string ToJson(bool pretty = false) => JsonSerializer.Serialize(this, pretty ? OptPretty : Opt);

        public static LevelData FromJson(string json)
        {
            try
            {
                var lv = JsonSerializer.Deserialize<LevelData>(json, Opt);
                if (lv == null) return new LevelData();
                lv.Settings ??= new LevelSettings();
                lv.Objects ??= new List<LevelObject>();
                lv.Colors ??= new List<ColorChannelDef>();
                return lv;
            }
            catch { return new LevelData(); }
        }

        public void Save(string path)
        {
            Settings.LengthUnits = RightEdge();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, ToJson(true));
        }

        public static LevelData Load(string path) => FromJson(File.ReadAllText(path));

        public LevelData DeepClone() => FromJson(ToJson());

        /// <summary>Небольшой стартовый уровень, чтобы с первого запуска было во что играть.</summary>
        public static LevelData CreateDemo()
        {
            var lv = new LevelData();
            lv.Settings.Name = "Добро пожаловать в GEO DASH";
            lv.Settings.Author = "Winnigames2024";
            lv.Settings.Difficulty = Difficulty.Easy;
            lv.Settings.Description = "Стартовый уровень: куб, шипы, порталы режимов и скорости до 6x.";

            void Block(int gx, int gy) => lv.Add(new LevelObject { TypeId = 1, X = gx * 30, Y = gy * 30 });
            void Spike(int gx, int gy) => lv.Add(new LevelObject { TypeId = 20, X = gx * 30, Y = gy * 30 });
            void Portal(int gx, int gy, PortalKind k) => lv.Add(new LevelObject { TypeId = 60, X = gx * 30, Y = gy * 30, Portal = k });
            void Orb(int gx, int gy, OrbKind k) => lv.Add(new LevelObject { TypeId = 40, X = gx * 30, Y = gy * 30, Orb = k });

            for (int i = 10; i < 20; i++) Spike(i * 2, 0);
            for (int i = 0; i < 6; i++) Block(26 + i, 0);
            Spike(34, 0);
            Spike(40, 0); Spike(41, 0);
            for (int i = 0; i < 4; i++) Block(46 + i, 1);
            Orb(52, 3, OrbKind.Yellow);
            Portal(58, 1, PortalKind.SpeedFast);
            Spike(64, 0); Spike(70, 0);
            Portal(76, 1, PortalKind.ModeShip);
            for (int i = 0; i < 20; i++) { Block(82 + i, 6); Block(82 + i, -1); }
            Portal(104, 1, PortalKind.ModeWorm);
            for (int i = 0; i < 24; i++) Block(108 + i, 0);
            for (int i = 0; i < 6; i++) Block(112 + i, 1);
            Spike(120, 2);
            Portal(134, 1, PortalKind.ModeCube);
            Portal(140, 1, PortalKind.SpeedHyper);
            for (int i = 0; i < 4; i++) Spike(146 + i * 4, 0);
            lv.Add(new LevelObject { TypeId = 90, X = 170 * 30, Y = 0 }); // финиш
            return lv;
        }
    }
}
