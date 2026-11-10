using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace NareLauncher.Core
{
    public class JavaInstall
    {
        public string Path;        // полный путь к javaw.exe
        public int Major;
        public string Version = "";
        public string Vendor = "";

        public override string ToString()
        {
            return "Java " + (Major > 0 ? Major.ToString() : "?") +
                   (string.IsNullOrEmpty(Version) ? "" : " (" + Version + ")") + " — " + Path;
        }
    }

    public static class JavaLocator
    {
        private static List<JavaInstall> _cache;

        public static List<JavaInstall> FindAll(bool force = false)
        {
            if (_cache != null && !force) return _cache;

            var found = new Dictionary<string, JavaInstall>(StringComparer.OrdinalIgnoreCase);
            var roots = new List<string>();

            void AddRoot(string p)
            {
                if (!string.IsNullOrEmpty(p) && Directory.Exists(p)) roots.Add(p);
            }

            AddRoot(Environment.GetEnvironmentVariable("JAVA_HOME"));
            AddRoot(Paths.RuntimeDir);

            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pfx = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            string[] vendorDirs =
            {
                "Java", "Eclipse Adoptium", "Eclipse Foundation", "AdoptOpenJDK", "Zulu",
                "BellSoft", "Amazon Corretto", "Microsoft", "Semeru", "RedHat", "Graalvm", "JetBrains"
            };

            foreach (var baseDir in new[] { pf, pfx, local, Path.Combine(local, "Programs") })
            {
                if (string.IsNullOrEmpty(baseDir)) continue;
                foreach (var vd in vendorDirs)
                {
                    string d = Path.Combine(baseDir, vd);
                    if (!Directory.Exists(d)) continue;
                    AddRoot(d);
                    SafeAddChildren(roots, d);
                }
            }

            // IDE и ручные установки
            SafeAddChildren(roots, Path.Combine(user, ".jdks"));
            SafeAddChildren(roots, Path.Combine(user, ".gradle", "jdks"));
            // Java, которую ставит официальный лаунчер Mojang
            SafeAddChildren(roots, Path.Combine(appdata, ".minecraft", "runtime"), 3);
            SafeAddChildren(roots, Path.Combine(Paths.GameRoot, "runtime"), 3);
            SafeAddChildren(roots, Paths.RuntimeDir, 3);

            // Реестр
            foreach (var key in new[]
            {
                @"SOFTWARE\JavaSoft\Java Runtime Environment",
                @"SOFTWARE\JavaSoft\Java Development Kit",
                @"SOFTWARE\JavaSoft\JDK",
                @"SOFTWARE\JavaSoft\JRE"
            })
            {
                AddFromRegistry(roots, Registry.LocalMachine, key);
                AddFromRegistry(roots, Registry.CurrentUser, key);
            }

            foreach (var root in roots)
            {
                foreach (var exe in CandidateExes(root))
                {
                    if (found.ContainsKey(exe)) continue;
                    var info = Probe(exe);
                    if (info != null) found[exe] = info;
                }
            }

            // java из PATH
            try
            {
                string pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
                foreach (var p in pathVar.Split(';'))
                {
                    if (string.IsNullOrWhiteSpace(p)) continue;
                    string exe = Path.Combine(p.Trim(), "javaw.exe");
                    if (File.Exists(exe) && !found.ContainsKey(exe))
                    {
                        var info = Probe(exe);
                        if (info != null) found[exe] = info;
                    }
                }
            }
            catch { }

            var list = new List<JavaInstall>(found.Values);
            list.Sort((a, b) => b.Major.CompareTo(a.Major));
            _cache = list;
            return list;
        }

        private static void SafeAddChildren(List<string> roots, string dir, int depth = 1)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                foreach (var sub in Directory.GetDirectories(dir))
                {
                    roots.Add(sub);
                    if (depth > 1) SafeAddChildren(roots, sub, depth - 1);
                }
            }
            catch { }
        }

        private static void AddFromRegistry(List<string> roots, RegistryKey hive, string path)
        {
            try
            {
                using (var key = hive.OpenSubKey(path))
                {
                    if (key == null) return;
                    foreach (var sub in key.GetSubKeyNames())
                    {
                        using (var k2 = key.OpenSubKey(sub))
                        {
                            if (k2 == null) continue;
                            var home = k2.GetValue("JavaHome") as string;
                            if (!string.IsNullOrEmpty(home) && Directory.Exists(home)) roots.Add(home);
                        }
                    }
                }
            }
            catch { }
        }

        private static IEnumerable<string> CandidateExes(string root)
        {
            string a = Path.Combine(root, "bin", "javaw.exe");
            if (File.Exists(a)) yield return a;
            string b = Path.Combine(root, "javaw.exe");
            if (File.Exists(b)) yield return b;
        }

        /// <summary>Определяет версию Java по файлу release (быстро) или запуском java -version.</summary>
        public static JavaInstall Probe(string javawPath)
        {
            if (string.IsNullOrEmpty(javawPath) || !File.Exists(javawPath)) return null;

            var info = new JavaInstall { Path = javawPath };
            try
            {
                string home = Path.GetDirectoryName(Path.GetDirectoryName(javawPath));
                string release = home == null ? null : Path.Combine(home, "release");
                if (release != null && File.Exists(release))
                {
                    foreach (var line in File.ReadAllLines(release))
                    {
                        if (line.StartsWith("JAVA_VERSION=", StringComparison.OrdinalIgnoreCase))
                            info.Version = line.Substring(13).Trim().Trim('"');
                        else if (line.StartsWith("IMPLEMENTOR=", StringComparison.OrdinalIgnoreCase))
                            info.Vendor = line.Substring(12).Trim().Trim('"');
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(info.Version))
            {
                try
                {
                    string javaExe = Path.Combine(Path.GetDirectoryName(javawPath) ?? "", "java.exe");
                    if (!File.Exists(javaExe)) javaExe = javawPath;

                    var psi = new ProcessStartInfo(javaExe, "-version")
                    {
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using (var p = Process.Start(psi))
                    {
                        string output = p.StandardError.ReadToEnd() + p.StandardOutput.ReadToEnd();
                        p.WaitForExit(5000);
                        var m = Regex.Match(output, "version \"([^\"]+)\"");
                        if (m.Success) info.Version = m.Groups[1].Value;
                    }
                }
                catch { return null; }
            }

            info.Major = ParseMajor(info.Version);
            if (info.Major <= 0) return null;
            return info;
        }

        public static int ParseMajor(string version)
        {
            if (string.IsNullOrEmpty(version)) return 0;
            var m = Regex.Match(version, @"^(\d+)");
            if (!m.Success) return 0;
            int major = int.Parse(m.Groups[1].Value);
            if (major == 1)
            {
                var m2 = Regex.Match(version, @"^1\.(\d+)");
                if (m2.Success) return int.Parse(m2.Groups[1].Value);
            }
            return major;
        }

        /// <summary>Какая Java требуется версии игры.</summary>
#if !SERVERMANAGER_EXE
        public static int RequiredMajor(Json resolved)
        {
            int major = resolved["javaVersion"]["majorVersion"].AsInt(0);
            if (major > 0) return major;

            // Для старых версий без javaVersion
            string id = MinecraftMeta.BaseGameVersion(resolved, resolved["id"].AsString(""));
            var m = Regex.Match(id ?? "", @"^1\.(\d+)");
            if (m.Success)
            {
                int minor = int.Parse(m.Groups[1].Value);
                if (minor >= 21) return 21;
                if (minor >= 20) return 17;
                if (minor >= 18) return 17;
                if (minor == 17) return 16;
                return 8;
            }
            return 8;
        }

        /// <summary>Подбирает подходящую Java: сначала точное совпадение, потом ближайшую старшую.</summary>
        public static JavaInstall Pick(int requiredMajor)
        {
            if (!string.IsNullOrEmpty(Settings.Current.JavaPath) && File.Exists(Settings.Current.JavaPath))
            {
                var manual = Probe(Settings.Current.JavaPath);
                if (manual != null) return manual;
                return new JavaInstall { Path = Settings.Current.JavaPath, Major = requiredMajor };
            }

            var all = FindAll();
            JavaInstall exact = null, higher = null, any = null;
            foreach (var j in all)
            {
                if (any == null) any = j;
                if (j.Major == requiredMajor && exact == null) exact = j;
                if (j.Major > requiredMajor && (higher == null || j.Major < higher.Major)) higher = j;
            }
            return exact ?? higher ?? any;
        }
#endif

        public const string AdoptiumTemplate =
            "https://api.adoptium.net/v3/binary/latest/{0}/ga/windows/x64/jre/hotspot/normal/eclipse";

        /// <summary>Скачивает и распаковывает JRE нужной версии в папку runtime лаунчера.</summary>
#if !SERVERMANAGER_EXE
        public static async Task<JavaInstall> DownloadJavaAsync(int major, IProgress<InstallStatus> progress,
                                                                CancellationToken ct)
        {
            Paths.Ensure(Paths.RuntimeDir);
            string zip = Path.Combine(Paths.CacheDir, "jre-" + major + ".zip");
            string target = Path.Combine(Paths.RuntimeDir, "jre-" + major);

            if (progress != null)
                progress.Report(new InstallStatus { Text = "Скачиваю Java " + major + "…", Indeterminate = true });

            string url = string.Format(AdoptiumTemplate, major);
            await Net.DownloadFileAsync(url, zip, null, ct).ConfigureAwait(false);

            if (progress != null)
                progress.Report(new InstallStatus { Text = "Распаковываю Java " + major + "…", Indeterminate = true });

            if (Directory.Exists(target))
            {
                try { Directory.Delete(target, true); } catch { }
            }
            Paths.Ensure(target);

            await Task.Run(() => ZipFile.ExtractToDirectory(zip, target), ct).ConfigureAwait(false);
            try { File.Delete(zip); } catch { }

            // внутри архива один корневой каталог
            string exe = null;
            foreach (var dir in Directory.GetDirectories(target))
            {
                string candidate = Path.Combine(dir, "bin", "javaw.exe");
                if (File.Exists(candidate)) { exe = candidate; break; }
            }
            if (exe == null)
            {
                string candidate = Path.Combine(target, "bin", "javaw.exe");
                if (File.Exists(candidate)) exe = candidate;
            }
            if (exe == null) throw new Exception("В скачанном архиве Java не найден javaw.exe");

            _cache = null;
            var info = Probe(exe);
            Log.Info("Java скачана: " + exe);
            return info ?? new JavaInstall { Path = exe, Major = major };
        }
#endif
    }
}
