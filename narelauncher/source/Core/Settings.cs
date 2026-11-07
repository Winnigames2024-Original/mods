using System;
using System.IO;

namespace NareLauncher.Core
{
    public class Settings
    {
        public string GameRoot = "";
        public string JavaPath = "";              // пусто = автопоиск
        public int RamMb = 4096;
        public string JvmArgs = "-XX:+UseG1GC -XX:+UnlockExperimentalVMOptions -XX:G1NewSizePercent=20 -XX:MaxGCPauseMillis=50 -Dfile.encoding=UTF-8";
        public bool IsolateVersions = true;       // своя папка (и свои моды) для каждой версии
        public bool CloseOnLaunch = false;
        public bool ShowConsole = true;
        public string SelectedVersion = "";
        public int WindowWidth = 1280;
        public int WindowHeight = 720;
        public bool Fullscreen = false;
        public bool ShowSnapshots = false;
        public bool ShowOldVersions = false;
        public int DownloadThreads = 12;
        public bool VerifyHashes = true;
        public bool DemoMode = false;

        /// <summary>Передавать игре --visuals: без него мод NareVisuals не показывается.</summary>
        public bool Visuals = true;

        /// <summary>Самому подкладывать свежий NareVisuals в сборку при запуске.</summary>
        public bool AutoUpdateVisuals = true;

        /// <summary>Брать свежий мод с GitHub, а не только встроенный.</summary>
        public bool UpdateFromGitHub = true;

        /// <summary>Своё описание обновлений, если нужен другой адрес.</summary>
        public string ModUpdateUrl = "";

        /// <summary>Версия мода, которую мы сами поставили с GitHub.</summary>
        public string ModVersionFromGitHub = "";

        /// <summary>Имя файла мода, который мы поставили с GitHub.</summary>
        public string ModFileFromGitHub = "";

        /// <summary>
        /// Какую версию визуалов держать. Пусто — всегда последняя: лаунчер
        /// сам подтянет свежую. Иначе ставится ровно эта, даже если вышла
        /// новее.
        /// </summary>
        public string VisualsVersion = "";

        /// <summary>Надетые вещи — имена файлов через «|».</summary>
        public string WornCosmetics = "";

        /// <summary>Выбранный источник модов: modrinth / curseforge / local.</summary>
        public string ModSourceId = "modrinth";
        /// <summary>Ключ API CurseForge — без него их каталог недоступен.</summary>
        public string CurseForgeKey = "";
        /// <summary>Папка со своими скачанными модами для источника «Папка на диске».</summary>
        public string LocalModsFolder = "";

        /// <summary>Команды, которые мод отправит сам при входе в мир или на сервер.</summary>
        public string JoinCommands = "";

        public static Settings Current = new Settings();

        public static void Load()
        {
            var s = new Settings();
            var j = Json.ParseFile(Paths.ConfigFile);
            if (j.IsObject)
            {
                s.GameRoot = j["gameRoot"].AsString(s.GameRoot);
                s.JavaPath = j["javaPath"].AsString(s.JavaPath);
                s.RamMb = j["ramMb"].AsInt(s.RamMb);
                s.JvmArgs = j["jvmArgs"].AsString(s.JvmArgs);
                s.IsolateVersions = j["isolateVersions"].AsBool(s.IsolateVersions);
                s.CloseOnLaunch = j["closeOnLaunch"].AsBool(s.CloseOnLaunch);
                s.ShowConsole = j["showConsole"].AsBool(s.ShowConsole);
                s.SelectedVersion = j["selectedVersion"].AsString(s.SelectedVersion);
                s.WindowWidth = j["windowWidth"].AsInt(s.WindowWidth);
                s.WindowHeight = j["windowHeight"].AsInt(s.WindowHeight);
                s.Fullscreen = j["fullscreen"].AsBool(s.Fullscreen);
                s.ShowSnapshots = j["showSnapshots"].AsBool(s.ShowSnapshots);
                s.ShowOldVersions = j["showOldVersions"].AsBool(s.ShowOldVersions);
                s.DownloadThreads = j["downloadThreads"].AsInt(s.DownloadThreads);
                s.VerifyHashes = j["verifyHashes"].AsBool(s.VerifyHashes);
                s.DemoMode = j["demoMode"].AsBool(s.DemoMode);
                s.ModSourceId = j["modSourceId"].AsString(s.ModSourceId);
                s.CurseForgeKey = j["curseForgeKey"].AsString(s.CurseForgeKey);
                s.LocalModsFolder = j["localModsFolder"].AsString(s.LocalModsFolder);
                s.AutoUpdateVisuals = j["autoUpdateVisuals"].AsBool(s.AutoUpdateVisuals);
                s.UpdateFromGitHub = j["updateFromGitHub"].AsBool(s.UpdateFromGitHub);
                s.ModUpdateUrl = j["modUpdateUrl"].AsString(s.ModUpdateUrl);
                s.ModVersionFromGitHub = j["modVersionFromGitHub"].AsString(s.ModVersionFromGitHub);
                s.ModFileFromGitHub = j["modFileFromGitHub"].AsString(s.ModFileFromGitHub);
                s.VisualsVersion = j["visualsVersion"].AsString(s.VisualsVersion);
                s.WornCosmetics = j["wornCosmetics"].AsString(s.WornCosmetics);
            }

            // прежние сборки клали игру рядом с exe — переводим на стандартный %APPDATA%\.minecraft
            string legacyRoot = Path.Combine(Paths.LauncherDir, ".minecraft");
            if (string.IsNullOrEmpty(s.GameRoot) ||
                (string.Equals(s.GameRoot.TrimEnd('\\'), legacyRoot.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) &&
                 !Directory.Exists(Path.Combine(legacyRoot, "versions"))))
            {
                s.GameRoot = Paths.DefaultGameRoot;
            }

            if (s.RamMb < 512) s.RamMb = 512;
            if (s.DownloadThreads < 1) s.DownloadThreads = 1;
            if (s.DownloadThreads > 32) s.DownloadThreads = 32;

            Current = s;
        }

        public static void Save()
        {
            try
            {
                var s = Current;
                var j = Json.NewObject();
                j.Set("gameRoot", s.GameRoot);
                j.Set("javaPath", s.JavaPath);
                j.Set("ramMb", s.RamMb);
                j.Set("jvmArgs", s.JvmArgs);
                j.Set("isolateVersions", s.IsolateVersions);
                j.Set("closeOnLaunch", s.CloseOnLaunch);
                j.Set("showConsole", s.ShowConsole);
                j.Set("selectedVersion", s.SelectedVersion);
                j.Set("windowWidth", s.WindowWidth);
                j.Set("windowHeight", s.WindowHeight);
                j.Set("fullscreen", s.Fullscreen);
                j.Set("showSnapshots", s.ShowSnapshots);
                j.Set("showOldVersions", s.ShowOldVersions);
                j.Set("downloadThreads", s.DownloadThreads);
                j.Set("verifyHashes", s.VerifyHashes);
                j.Set("demoMode", s.DemoMode);
                j.Set("modSourceId", s.ModSourceId);
                j.Set("curseForgeKey", s.CurseForgeKey);
                j.Set("localModsFolder", s.LocalModsFolder);
                j.Set("autoUpdateVisuals", s.AutoUpdateVisuals);
                j.Set("updateFromGitHub", s.UpdateFromGitHub);
                j.Set("modUpdateUrl", s.ModUpdateUrl);
                j.Set("modVersionFromGitHub", s.ModVersionFromGitHub);
                j.Set("modFileFromGitHub", s.ModFileFromGitHub);
                j.Set("visualsVersion", s.VisualsVersion);
                j.Set("wornCosmetics", s.WornCosmetics);
                j.SaveTo(Paths.ConfigFile);
            }
            catch (Exception ex) { Log.Error("Не удалось сохранить настройки", ex); }
        }
    }
}
