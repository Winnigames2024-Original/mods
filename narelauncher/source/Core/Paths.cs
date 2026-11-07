using System;
using System.IO;

namespace NareLauncher.Core
{
    /// <summary>Все пути лаунчера в одном месте.</summary>
    public static class Paths
    {
        /// <summary>Папка, где лежит NareLauncher.exe.</summary>
        public static string LauncherDir
        {
            get { return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'); }
        }

        public static string AppData
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); }
        }

        /// <summary>Данные самого лаунчера: %APPDATA%\NareLauncher.</summary>
        public static string DataDir { get { return Path.Combine(AppData, "NareLauncher"); } }

        /// <summary>Папка игры по умолчанию — та же, что и у официального лаунчера.</summary>
        public static string DefaultGameRoot { get { return Path.Combine(AppData, ".minecraft"); } }

        public static string ConfigFile { get { return Path.Combine(DataDir, "narelauncher.json"); } }
        public static string AccountsFile { get { return Path.Combine(DataDir, "accounts.json"); } }
        public static string LogFile { get { return Path.Combine(DataDir, "launcher.log"); } }

        /// <summary>Корень игры (.minecraft). Берётся из настроек.</summary>
        public static string GameRoot
        {
            get
            {
                var s = Settings.Current.GameRoot;
                if (string.IsNullOrEmpty(s)) s = DefaultGameRoot;
                return s.TrimEnd('\\');
            }
        }

        /// <summary>
        /// Переезд с прежних версий лаунчера, которые держали настройки
        /// рядом с exe (а значит, теряли их при очистке bin).
        /// </summary>
        public static void MigrateOldData()
        {
            try
            {
                Ensure(DataDir);
                foreach (var name in new[] { "narelauncher.json", "accounts.json" })
                {
                    string oldFile = Path.Combine(LauncherDir, name);
                    string newFile = Path.Combine(DataDir, name);
                    if (File.Exists(oldFile) && !File.Exists(newFile))
                        File.Copy(oldFile, newFile, false);
                }
            }
            catch { /* перенос не критичен */ }
        }

        public static string VersionsDir { get { return Path.Combine(GameRoot, "versions"); } }
        public static string LibrariesDir { get { return Path.Combine(GameRoot, "libraries"); } }
        public static string AssetsDir { get { return Path.Combine(GameRoot, "assets"); } }
        public static string AssetIndexesDir { get { return Path.Combine(AssetsDir, "indexes"); } }
        public static string AssetObjectsDir { get { return Path.Combine(AssetsDir, "objects"); } }
        public static string AssetVirtualDir { get { return Path.Combine(AssetsDir, "virtual"); } }
        public static string CacheDir { get { return Path.Combine(DataDir, "cache"); } }
        public static string RuntimeDir { get { return Path.Combine(DataDir, "runtime"); } }
        public static string InstancesDir { get { return Path.Combine(GameRoot, "instances"); } }

        public static string VersionDir(string id) { return Path.Combine(VersionsDir, id); }
        public static string VersionJson(string id) { return Path.Combine(VersionDir(id), id + ".json"); }
        public static string VersionJar(string id) { return Path.Combine(VersionDir(id), id + ".jar"); }
        public static string NativesDir(string id) { return Path.Combine(VersionDir(id), "natives"); }

        /// <summary>
        /// Рабочая папка конкретной версии. Если включена изоляция — у каждой версии
        /// своя папка с собственными mods / saves / resourcepacks / options.txt.
        /// </summary>
        public static string InstanceDir(string versionId)
        {
            if (Settings.Current.IsolateVersions && !string.IsNullOrEmpty(versionId))
                return Path.Combine(InstancesDir, SafeName(versionId));
            return GameRoot;
        }

        public static string ModsDir(string versionId) { return Path.Combine(InstanceDir(versionId), "mods"); }
        public static string ResourcePacksDir(string versionId) { return Path.Combine(InstanceDir(versionId), "resourcepacks"); }
        public static string ShaderPacksDir(string versionId) { return Path.Combine(InstanceDir(versionId), "shaderpacks"); }
        public static string SavesDir(string versionId) { return Path.Combine(InstanceDir(versionId), "saves"); }

        public static string SafeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unknown";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        /// <summary>
        /// Если игра лежит внутри папки лаунчера (например, в bin\Debug), переносим её
        /// в %APPDATA%\.minecraft — иначе очистка решения сносит версии и миры.
        /// </summary>
        public static void MigrateGameFolder()
        {
            try
            {
                string current = (Settings.Current.GameRoot ?? "").TrimEnd('\\');
                string target = DefaultGameRoot.TrimEnd('\\');

                if (current.Length == 0) { Settings.Current.GameRoot = target; Settings.Save(); return; }
                if (string.Equals(current, target, StringComparison.OrdinalIgnoreCase)) return;

                // переносим только то, что лежит внутри папки лаунчера
                if (!current.StartsWith(LauncherDir, StringComparison.OrdinalIgnoreCase)) return;

                if (!Directory.Exists(current))
                {
                    Settings.Current.GameRoot = target;
                    Settings.Save();
                    return;
                }

                if (!Directory.Exists(target))
                {
                    string parent = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                    Directory.Move(current, target);
                    Log.Info("Папка игры перенесена в " + target);
                }
                else
                {
                    MoveMissing(current, target);
                    Log.Info("Папка игры объединена с " + target);
                }

                Settings.Current.GameRoot = target;
                Settings.Save();
            }
            catch (Exception ex)
            {
                Log.Error("Не удалось перенести папку игры", ex);
            }
        }

        /// <summary>Переносит из from в to только то, чего там ещё нет.</summary>
        private static void MoveMissing(string from, string to)
        {
            Ensure(to);

            foreach (var file in Directory.GetFiles(from))
            {
                string dst = Path.Combine(to, Path.GetFileName(file));
                try { if (!File.Exists(dst)) File.Move(file, dst); }
                catch { }
            }

            foreach (var dir in Directory.GetDirectories(from))
            {
                string dst = Path.Combine(to, Path.GetFileName(dir));
                if (!Directory.Exists(dst))
                {
                    try { Directory.Move(dir, dst); continue; }
                    catch { }
                }
                MoveMissing(dir, dst);
            }
        }

        public static void Ensure(string dir)
        {
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        public static void EnsureForFile(string file)
        {
            var d = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(d)) Ensure(d);
        }

        public static void EnsureAll()
        {
            Ensure(DataDir);
            Ensure(GameRoot);
            Ensure(VersionsDir);
            Ensure(LibrariesDir);
            Ensure(AssetsDir);
            Ensure(AssetIndexesDir);
            Ensure(AssetObjectsDir);
            Ensure(CacheDir);
        }
    }
}
