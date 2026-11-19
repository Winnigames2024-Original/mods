using System.Text.Json;

namespace GEO_DASH
{
    public class AppSettings
    {
        public string ServerHost { get; set; } = "166.1.161.200";
        public int ServerPort { get; set; } = 37534;
        public string SiteUrl { get; set; } = "http://geodash.winnigames2024.ru/";
        public string Token { get; set; } = "";
        public string UserName { get; set; } = "";
        public bool IsAdmin { get; set; }
        public double MusicVolume { get; set; } = 0.8;
        public double SfxVolume { get; set; } = 0.55;
        public bool ShowFps { get; set; } = true;
        public bool ShowHitboxes { get; set; }
        public bool AutoUpdate { get; set; } = true;
        public bool OfflineAutoDownload { get; set; } = true;
        public bool Fullscreen { get; set; }
        public int Fps { get; set; } = 60;
        public string UpdateChannel { get; set; } = "main";
        /// <summary>Где лежат кнопки в игре — настраивается в самой игре по кнопке «=».</summary>
        public Core.GuiLayout Gui { get; set; } = new();
    }

    public static class AppConfig
    {
        public const string AppName = "GEO DASH";
        public const string Brand = "Winnigames2024";
        public const string AdminUser = "Winnigames2024";

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

        public static string LevelsDir => Ensure(Path.Combine(DataDir, "levels"));
        public static string SavedDir => Ensure(Path.Combine(DataDir, "saved"));
        public static string CacheDir => Ensure(Path.Combine(DataDir, "cache"));
        /// <summary>Песни: assets/songs — и скачанные с сайта, и встроенные.</summary>
        public static string MusicDir => Ensure(Path.Combine(DataDir, "songs"));
        public static string OfficialDir => Ensure(Path.Combine(DataDir, "official"));

        static string Ensure(string p) { Directory.CreateDirectory(p); return p; }

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

        public static string ApiBase => $"http://{Settings.ServerHost}:{Settings.ServerPort}";

        public static bool LoggedIn => !string.IsNullOrEmpty(Settings.Token);
        public static bool IsAdminUser =>
            string.Equals(Settings.UserName, AdminUser, StringComparison.OrdinalIgnoreCase) || Settings.IsAdmin;
    }
}
