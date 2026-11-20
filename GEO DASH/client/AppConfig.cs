using System.Text;
using System.Text.Json;

namespace GEO_DASH
{
    /// <summary>Сохранённый аккаунт: заходим под ним без повторного ввода пароля.</summary>
    public class SavedAccount
    {
        public string Name { get; set; } = "";
        public string Token { get; set; } = "";
        public bool Admin { get; set; }
        public string LastSeen { get; set; } = "";
    }

    public class AppSettings
    {
        public string Token { get; set; } = "";
        /// <summary>Все аккаунты, под которыми уже заходили на этом компьютере.</summary>
        public List<SavedAccount> Accounts { get; set; } = new();
        public string UserName { get; set; } = "";
        public bool IsAdmin { get; set; }
        public double MusicVolume { get; set; } = 0.8;
        public double SfxVolume { get; set; } = 0.55;
        public bool ShowFps { get; set; } = true;
        public bool ShowHitboxes { get; set; }
        public bool AutoUpdate { get; set; } = true;
        public bool OfflineAutoDownload { get; set; } = true;
        public bool Fullscreen { get; set; } = true;
        public int Fps { get; set; } = 60;
        /// <summary>
        /// Высота картинки, в которой считается кадр: 540 — низкое, 720 —
        /// среднее, 0 — как есть, во всё разрешение экрана. На большом
        /// мониторе рисовать каждый пиксель градиентами дорого, поэтому кадр
        /// готовится в маленькой картинке и растягивается на экран.
        /// </summary>
        public int RenderHeight { get; set; } = 720;

        // ---- внешний вид кубика ----
        /// <summary>Номер встроенного рисунка (1..16).</summary>
        public int IconShape { get; set; } = 1;
        /// <summary>Основной цвет кубика.</summary>
        public int IconColor1 { get; set; } = unchecked((int)0xFF3CF2FF);
        /// <summary>Цвет рисунка на кубике.</summary>
        public int IconColor2 { get; set; } = unchecked((int)0xFF1B2A66);
        /// <summary>Своя картинка на кубике: файл на диске (пусто — встроенный рисунок).</summary>
        public string IconImage { get; set; } = "";
        public string UpdateChannel { get; set; } = "main";
        /// <summary>Где лежат кнопки в игре — настраивается в самой игре по кнопке «=».</summary>
        public Core.GuiLayout Gui { get; set; } = new();
    }

    public static class AppConfig
    {
        public const string AppName = "GEO DASH";
        public const string Brand = "Winnigames2024";
        public const string AdminUser = "Winnigames2024";

        // ------------------------------------------------------------------
        // Адрес сервера живёт только здесь, в коде. На формах он не
        // показывается и в settings.json не сохраняется — игроку он не нужен.
        // ------------------------------------------------------------------
        const string Host = "166.1.161.200";
        const int Port = 37534;

        /// <summary>Сайт игры — тоже только в коде.</summary>
        public const string SiteUrl = "https://geodash.winnigames2024.ru/";

        /// <summary>Как сервер подписан в интерфейсе — без адреса.</summary>
        public const string ServerLabel = "сервер Winnigames2024";

        /// <summary>Репозиторий обновлений (всё ставится/обновляется через GitHub + curl).</summary>
        public const string GitHubOwner = "Winnigames2024-Original";
        public const string GitHubRepo = "mods";
        public const string GitHubDir = "GEO DASH";

        public static string RawBase =>
            $"https://raw.githubusercontent.com/{GitHubOwner}/{GitHubRepo}/{Settings.UpdateChannel}/{Uri.EscapeDataString(GitHubDir)}";

        static string _dataDir;

        /// <summary>
        /// Все данные игры: %APPDATA%\Winnigames2024\Geo Dash\assets\
        /// (уровни, сохранённые уровни, песни, кэш). Папка создаётся сама.
        /// </summary>
        public static string DataDir
        {
            get
            {
                if (_dataDir != null) return _dataDir;
                var p = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    Brand, "Geo Dash", "assets");
                Directory.CreateDirectory(p);
                _dataDir = p;
                return _dataDir;
            }
        }

        /// <summary>
        /// Короткая запись в crash.log: по ней видно, до какого места дошла
        /// игра, если она вылетела без всякой ошибки.
        /// </summary>
        public static void Trace(string what)
        {
            try
            {
                File.AppendAllText(Path.Combine(DataDir, "crash.log"),
                    $"[{DateTime.Now:s}] {what}\n");
            }
            catch { }
        }

        // ---- чёрный ящик ----
        // Жёсткий вылет (не исключение, а падение процесса) не даёт ничего
        // записать: обработчики уже не выполняются. Поэтому текущий этап
        // лежит в файле, отображённом в память: запись туда почти бесплатна,
        // а содержимое остаётся на диске, даже если процесс убит.

        static System.IO.MemoryMappedFiles.MemoryMappedFile _stageFile;
        static System.IO.MemoryMappedFiles.MemoryMappedViewAccessor _stage;
        static string _stageLast = "";
        static string _stageNow = "";

        static void StageOpen()
        {
            if (_stage != null) return;
            try
            {
                var path = Path.Combine(DataDir, "stage.bin");
                var bytes = File.Exists(path) ? File.ReadAllBytes(path) : Array.Empty<byte>();
                _stageLast = bytes.Length > 1
                    ? Encoding.UTF8.GetString(bytes).TrimEnd('\0').Trim()
                    : "";

                // ссылку держим в статике: иначе сборщик мусора вправе
                // закрыть отображение прямо посреди игры
                _stageFile = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(
                    path, FileMode.Create, null, 256);
                _stage = _stageFile.CreateViewAccessor(0, 256);
            }
            catch { }
        }

        /// <summary>Что игра делает прямо сейчас — переживает даже жёсткий вылет.</summary>
        public static void Stage(string what)
        {
            _stageNow = what ?? "";
            StageOpen();
            if (_stage == null) return;
            try
            {
                var b = new byte[256];
                var t = Encoding.UTF8.GetBytes(what ?? "");
                Array.Copy(t, b, Math.Min(t.Length, 255));
                _stage.WriteArray(0, b, 0, 256);
            }
            catch { }
        }

        /// <summary>Чем игра занята прямо сейчас.</summary>
        public static string LastStageNow() => _stageNow;

        /// <summary>Чем игра была занята перед прошлым вылетом (пусто — вышли штатно).</summary>
        public static string LastStage()
        {
            StageOpen();
            return _stageLast;
        }

        /// <summary>Штатный выход: чёрный ящик чистим, чтобы не слать ложных отчётов.</summary>
        public static void StageClear()
        {
            Stage("");
            try { _stage?.Flush(); } catch { }
        }

        public static string LevelsDir => Ensure(Path.Combine(DataDir, "levels"));
        public static string SavedDir => Ensure(Path.Combine(DataDir, "saved"));
        public static string CacheDir => Ensure(Path.Combine(DataDir, "cache"));
        /// <summary>Песни: assets/songs — и скачанные с сайта, и встроенные.</summary>
        public static string MusicDir => Ensure(Path.Combine(DataDir, "songs"));
        public static string OfficialDir => Ensure(Path.Combine(DataDir, "official"));

        public static string Ensure(string p) { Directory.CreateDirectory(p); return p; }

        static AppSettings _settings;
        public static AppSettings Settings
        {
            get
            {
                if (_settings == null) Load();
                return _settings;
            }
        }

        static string SettingsPath => Path.Combine(DataDir, "settings.json");

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    _settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
                else _settings = new AppSettings();
            }
            catch { _settings = new AppSettings(); }
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(SettingsPath,
                    JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public static string ApiBase => $"http://{Host}:{Port}";

        public static bool LoggedIn => !string.IsNullOrEmpty(Settings.Token);
        public static bool IsAdminUser =>
            string.Equals(Settings.UserName, AdminUser, StringComparison.OrdinalIgnoreCase) || Settings.IsAdmin;

        // --------------------------------------------------------- аккаунты

        /// <summary>Запомнить аккаунт, под которым вошли, чтобы заходить в один клик.</summary>
        public static void RememberAccount(string name, string token, bool admin)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            var list = Settings.Accounts;
            var a = list.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (a == null) { a = new SavedAccount { Name = name }; list.Add(a); }
            a.Token = token ?? "";
            a.Admin = admin;
            a.LastSeen = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            // последний вход — первым в списке
            list.Remove(a);
            list.Insert(0, a);
            while (list.Count > 8) list.RemoveAt(list.Count - 1);
            Save();
        }

        /// <summary>Переключиться на сохранённый аккаунт.</summary>
        public static void UseAccount(SavedAccount a)
        {
            if (a == null) return;
            Settings.UserName = a.Name;
            Settings.Token = a.Token;
            Settings.IsAdmin = a.Admin;
            Save();
        }

        /// <summary>Убрать аккаунт из списка сохранённых.</summary>
        public static void ForgetAccount(string name)
        {
            Settings.Accounts.RemoveAll(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (string.Equals(Settings.UserName, name, StringComparison.OrdinalIgnoreCase))
            {
                Settings.UserName = "";
                Settings.Token = "";
                Settings.IsAdmin = false;
            }
            Save();
        }
    }
}
