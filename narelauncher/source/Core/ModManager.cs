using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace NareLauncher.Core
{
    public class LocalMod
    {
        public string Path = "";
        public string FileName = "";
        public long Size;
        public bool Enabled = true;

        public string SizeText
        {
            get
            {
                if (Size >= 1048576) return (Size / 1048576.0).ToString("0.#") + " МБ";
                if (Size >= 1024) return (Size / 1024.0).ToString("0") + " КБ";
                return Size + " Б";
            }
        }

        public override string ToString() { return FileName; }
    }

    /// <summary>
    /// Моды хранятся отдельно для каждой версии: при включённой изоляции это
    /// .minecraft\instances\&lt;версия&gt;\mods.
    /// </summary>
    public static class ModManager
    {
        public static List<LocalMod> List(string projectType, string versionId)
        {
            var result = new List<LocalMod>();
            string dir = ModrinthApi.TargetDir(projectType, versionId);
            if (!Directory.Exists(dir)) return result;

            string[] masks = projectType == "mod"
                ? new[] { "*.jar", "*.jar.disabled" }
                : new[] { "*.zip", "*.zip.disabled" };

            foreach (var mask in masks)
            {
                foreach (var f in Directory.GetFiles(dir, mask))
                {
                    var fi = new FileInfo(f);
                    result.Add(new LocalMod
                    {
                        Path = f,
                        FileName = fi.Name,
                        Size = fi.Length,
                        Enabled = !fi.Name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                    });
                }
            }

            result.Sort((a, b) => string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        public static void Toggle(LocalMod mod)
        {
            try
            {
                if (mod.Enabled)
                {
                    string target = mod.Path + ".disabled";
                    if (File.Exists(target)) File.Delete(target);
                    File.Move(mod.Path, target);
                }
                else
                {
                    string target = mod.Path.Substring(0, mod.Path.Length - ".disabled".Length);
                    if (File.Exists(target)) File.Delete(target);
                    File.Move(mod.Path, target);
                }
            }
            catch (Exception ex) { Log.Error("Не удалось переключить мод " + mod.FileName, ex); }
        }

        public static void Delete(LocalMod mod)
        {
            try { File.Delete(mod.Path); }
            catch (Exception ex) { Log.Error("Не удалось удалить мод " + mod.FileName, ex); }
        }

        /// <summary>Копирует файл мода, выбранный пользователем вручную.</summary>
        public static void AddLocalFile(string sourcePath, string projectType, string versionId)
        {
            string dir = ModrinthApi.TargetDir(projectType, versionId);
            Paths.Ensure(dir);
            File.Copy(sourcePath, Path.Combine(dir, Path.GetFileName(sourcePath)), true);
        }

        public static void OpenFolder(string projectType, string versionId)
        {
            string dir = ModrinthApi.TargetDir(projectType, versionId);
            Paths.Ensure(dir);
            try { Process.Start("explorer.exe", "\"" + dir + "\""); }
            catch (Exception ex) { Log.Error("Не удалось открыть папку", ex); }
        }
    }

    /// <summary>
    /// Моды, поставляемые вместе с лаунчером. Берутся из встроенных ресурсов
    /// (Resources\Mods\*.jar со сборочным действием «Внедрённый ресурс»),
    /// а если их там нет — из папки Resources\Mods рядом с exe.
    /// Используется сборкой Forge Visuals.
    /// </summary>
    public static class BundledMods
    {
        private const string ResourcePrefix = "NareLauncher.Resources.Mods.";

        /// <summary>
        /// Где искать .jar, если он не вшит в exe: рядом с exe, в данных лаунчера
        /// и выше по дереву — чтобы при запуске из bin\Debug находилась папка
        /// Resources\Mods самого проекта.
        /// </summary>
        public static List<string> Folders()
        {
            var list = new List<string>();
            try
            {
                string dir = Paths.LauncherDir;
                for (int up = 0; up < 4 && !string.IsNullOrEmpty(dir); up++)
                {
                    list.Add(Path.Combine(dir, "Resources", "Mods"));
                    var parent = Directory.GetParent(dir);
                    if (parent == null) break;
                    dir = parent.FullName;
                }
                list.Add(Path.Combine(Paths.DataDir, "Mods"));
            }
            catch { }
            return list;
        }

        public static string FolderFallback
        {
            get { return Path.Combine(Paths.DataDir, "Mods"); }
        }

        /// <summary>Имена доступных встроенных модов.</summary>
        public static List<string> Available()
        {
            var names = new List<string>();

            try
            {
                var asm = Assembly.GetExecutingAssembly();
                foreach (var res in asm.GetManifestResourceNames())
                {
                    if (!res.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!res.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)) continue;
                    names.Add(res.Substring(ResourcePrefix.Length));
                }
            }
            catch (Exception ex) { Log.Warn("Встроенные моды не прочитаны: " + ex.Message); }

            foreach (var folder in Folders())
            {
                try
                {
                    if (!Directory.Exists(folder)) continue;
                    foreach (var f in Directory.GetFiles(folder, "*.jar"))
                    {
                        string n = Path.GetFileName(f);
                        if (!names.Contains(n)) names.Add(n);
                    }
                }
                catch { }
            }

            return names;
        }

        /// <summary>Кладёт встроенные моды в папку модов указанной сборки. Возвращает их число.</summary>
        public static int InstallInto(string versionId, Action<string> status)
        {
            string dir = Paths.ModsDir(versionId);
            Paths.Ensure(dir);

            int count = 0;
            var asm = Assembly.GetExecutingAssembly();

            try
            {
                foreach (var res in asm.GetManifestResourceNames())
                {
                    if (!res.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!res.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)) continue;

                    string fileName = res.Substring(ResourcePrefix.Length);
                    string target = Path.Combine(dir, fileName);

                    if (status != null) status("Распаковываю встроенный мод " + fileName + "…");

                    using (var src = asm.GetManifestResourceStream(res))
                    {
                        if (src == null) continue;
                        using (var dst = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
                            src.CopyTo(dst);
                    }
                    count++;
                    Log.Info("Встроенный мод установлен: " + fileName);
                }
            }
            catch (Exception ex) { Log.Error("Не удалось распаковать встроенные моды", ex); }

            foreach (var folder in Folders())
            {
                try
                {
                    if (!Directory.Exists(folder)) continue;

                    foreach (var f in Directory.GetFiles(folder, "*.jar"))
                    {
                        string target = Path.Combine(dir, Path.GetFileName(f));
                        if (File.Exists(target)) continue;

                        if (status != null) status("Копирую мод " + Path.GetFileName(f) + "…");
                        File.Copy(f, target, true);
                        count++;
                        Log.Info("Мод из папки " + folder + " установлен: " + Path.GetFileName(f));
                    }
                }
                catch (Exception ex) { Log.Error("Не удалось скопировать моды из " + folder, ex); }
            }

            return count;
        }

        // ------------------------------------------------------------ обновление

        /// <summary>Один исходник мода: встроенный в exe или лежащий в папке.</summary>
        public class JarSource
        {
            public string Name;
            public byte[] Data;
            public DateTime Stamp;
            public string From;
        }

        /// <summary>
        /// Все доступные .jar с их датами. Если один и тот же мод есть и внутри exe,
        /// и в папке, берём тот, что новее.
        /// </summary>
        public static List<JarSource> Sources()
        {
            var best = new Dictionary<string, JarSource>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var asm = Assembly.GetExecutingAssembly();
                DateTime stamp = DateTime.MinValue;
                try { stamp = File.GetLastWriteTimeUtc(asm.Location); }
                catch { }

                foreach (var res in asm.GetManifestResourceNames())
                {
                    if (!res.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!res.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)) continue;

                    using (var src = asm.GetManifestResourceStream(res))
                    {
                        if (src == null) continue;
                        using (var ms = new MemoryStream())
                        {
                            src.CopyTo(ms);
                            string name = res.Substring(ResourcePrefix.Length);
                            best[name] = new JarSource
                            {
                                Name = name,
                                Data = ms.ToArray(),
                                Stamp = stamp,
                                From = "внутри лаунчера"
                            };
                        }
                    }
                }
            }
            catch (Exception ex) { Log.Warn("Встроенные моды не прочитаны: " + ex.Message); }

            foreach (var folder in Folders())
            {
                try
                {
                    if (!Directory.Exists(folder)) continue;

                    foreach (var file in Directory.GetFiles(folder, "*.jar"))
                    {
                        string name = Path.GetFileName(file);
                        DateTime stamp = File.GetLastWriteTimeUtc(file);

                        JarSource current;
                        if (best.TryGetValue(name, out current) && current.Stamp >= stamp) continue;

                        best[name] = new JarSource
                        {
                            Name = name,
                            Data = File.ReadAllBytes(file),
                            Stamp = stamp,
                            From = folder
                        };
                    }
                }
                catch (Exception ex) { Log.Warn("Папка модов " + folder + ": " + ex.Message); }
            }

            return new List<JarSource>(best.Values);
        }

        /// <summary>
        /// Обновляет встроенные моды в сборке: заменяет файл, только если наш новее
        /// и реально отличается. Возвращает короткий отчёт для строки состояния.
        /// </summary>
        public static string Refresh(string versionId)
        {
            return Refresh(versionId, null);
        }

        /// <param name="skipPrefix">
        /// Имена, начинающиеся так, не трогаем: этим модом уже ведает
        /// обновление с GitHub, и встроенная копия только помешает.
        /// </param>
        public static string Refresh(string versionId, string skipPrefix)
        {
            if (string.IsNullOrEmpty(versionId)) return "Сначала выберите сборку.";

            var sources = Sources();
            if (sources.Count == 0) return "Встроенных модов нет — нечего обновлять.";

            string dir = Paths.ModsDir(versionId);
            Paths.Ensure(dir);

            int updated = 0, added = 0, same = 0, kept = 0;
            var names = new List<string>();

            foreach (var source in sources)
            {
                if (!string.IsNullOrEmpty(skipPrefix)
                    && source.Name.StartsWith(skipPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Info("Мод " + source.Name + " берём с GitHub — встроенный не кладём.");
                    continue;
                }

                string target = Path.Combine(dir, source.Name);

                try
                {
                    if (!File.Exists(target))
                    {
                        File.WriteAllBytes(target, source.Data);
                        added++;
                        names.Add(source.Name);
                        Log.Info("Мод добавлен: " + source.Name + " (" + source.From + ")");
                        continue;
                    }

                    if (SameBytes(target, source.Data)) { same++; continue; }

                    DateTime installed = File.GetLastWriteTimeUtc(target);
                    if (installed > source.Stamp.AddSeconds(2))
                    {
                        // в папке сборки лежит файл свежее нашего — не трогаем
                        kept++;
                        Log.Info("Мод " + source.Name + " в сборке новее встроенного — оставлен как есть.");
                        continue;
                    }

                    File.WriteAllBytes(target, source.Data);
                    updated++;
                    names.Add(source.Name);
                    Log.Info("Мод обновлён: " + source.Name + " (" + source.From + ")");
                }
                catch (Exception ex) { Log.Error("Не удалось обновить " + source.Name, ex); }
            }

            var report = new List<string>();
            if (updated > 0) report.Add("обновлено " + updated);
            if (added > 0) report.Add("добавлено " + added);
            if (same > 0) report.Add("без изменений " + same);
            if (kept > 0) report.Add("оставлено новее " + kept);

            string tail = names.Count > 0 ? " — " + string.Join(", ", names.ToArray()) : "";
            return "Визуалы: " + string.Join(", ", report.ToArray()) + tail + ".";
        }


        // ------------------------------------------------- одна версия на мод

        /// <summary>
        /// Оставляет в папке модов по одному jar на мод. Версии набегают с
        /// разных сторон — что-то скачано с GitHub, что-то положено руками,
        /// что-то осталось от прежней сборки, — а Forge от двух одинаковых
        /// модов просто не стартует. Старые уходят, самый свежий остаётся.
        /// </summary>
        public static string KeepOneVersion(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;

            var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (string path in Directory.GetFiles(dir, "*.jar"))
                {
                    string key = FamilyOf(Path.GetFileName(path));

                    List<string> same;
                    if (!groups.TryGetValue(key, out same))
                    {
                        same = new List<string>();
                        groups[key] = same;
                    }
                    same.Add(path);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Папку модов не прочитать: " + ex.Message);
                return null;
            }

            var dropped = new List<string>();

            foreach (var pair in groups)
            {
                var same = pair.Value;
                if (same.Count < 2) continue;

                string keep = same[0];
                foreach (string path in same)
                    if (Newer(path, keep)) keep = path;

                foreach (string path in same)
                {
                    if (string.Equals(path, keep, StringComparison.OrdinalIgnoreCase)) continue;

                    try
                    {
                        File.Delete(path);
                        dropped.Add(Path.GetFileName(path));
                        Log.Info("Лишняя версия мода убрана: " + Path.GetFileName(path)
                               + " (оставлен " + Path.GetFileName(keep) + ")");
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("Лишняя версия не убралась: " + Path.GetFileName(path)
                               + " — " + ex.Message);
                    }
                }
            }

            // запасные копии прежних сборок Forge не грузит, но в папке они
            // мозолят глаза — убираем те, чей мод уже обновился
            try
            {
                foreach (string path in Directory.GetFiles(dir, "*.jar.old"))
                {
                    try { File.Delete(path); }
                    catch { }
                }
            }
            catch { }

            if (dropped.Count == 0) return null;
            return "Убраны лишние версии модов: " + string.Join(", ", dropped.ToArray()) + ".";
        }

        /// <summary>
        /// Имя мода без версии: всё до первого дефиса, за которым идёт цифра.
        /// «forge_narevisuals-1.3.0.jar» и «forge_narevisuals-2.0.0.jar» —
        /// один мод, «jei-forge-1.21.4.jar» не путается с «jei-fabric».
        /// </summary>
        private static string FamilyOf(string fileName)
        {
            for (int i = 0; i < fileName.Length - 1; i++)
                if (fileName[i] == '-' && char.IsDigit(fileName[i + 1]))
                    return fileName.Substring(0, i);

            return Path.GetFileNameWithoutExtension(fileName);
        }

        /// <summary>Сравниваем по номеру версии, а при равенстве — по дате файла.</summary>
        private static bool Newer(string path, string than)
        {
            int[] a = VersionOf(Path.GetFileName(path));
            int[] b = VersionOf(Path.GetFileName(than));

            int len = Math.Max(a.Length, b.Length);
            for (int i = 0; i < len; i++)
            {
                int x = i < a.Length ? a[i] : 0;
                int y = i < b.Length ? b[i] : 0;
                if (x != y) return x > y;
            }

            try { return File.GetLastWriteTimeUtc(path) > File.GetLastWriteTimeUtc(than); }
            catch { return false; }
        }

        /// <summary>Числа из версии в имени файла: «мод-1.3.0.jar» -> 1, 3, 0.</summary>
        private static int[] VersionOf(string fileName)
        {
            var numbers = new List<int>();
            int value = -1;

            string name = Path.GetFileNameWithoutExtension(fileName);
            int start = FamilyOf(fileName).Length;

            for (int i = start; i < name.Length; i++)
            {
                char c = name[i];
                if (c >= '0' && c <= '9')
                    value = (value < 0 ? 0 : value * 10) + (c - '0');
                else
                {
                    if (value >= 0) numbers.Add(value);
                    value = -1;
                }
            }
            if (value >= 0) numbers.Add(value);

            return numbers.ToArray();
        }

        private static bool SameBytes(string path, byte[] data)
        {
            try
            {
                var info = new FileInfo(path);
                if (info.Length != data.Length) return false;

                byte[] current = File.ReadAllBytes(path);
                for (int i = 0; i < current.Length; i++)
                    if (current[i] != data[i]) return false;

                return true;
            }
            catch { return false; }
        }
    }
}
