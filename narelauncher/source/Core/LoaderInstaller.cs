using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NareLauncher.Core
{
    public class LoaderVersion
    {
        public string Version = "";
        public bool Stable = true;
        public bool Recommended;

        public override string ToString()
        {
            return Version + (Recommended ? "  (рекомендуемая)" : (Stable ? "" : "  (нестабильная)"));
        }
    }

    public static class LoaderInstaller
    {
        public const string FabricMeta = "https://meta.fabricmc.net/v2/";
        public const string QuiltMeta = "https://meta.quiltmc.org/v3/";
        public const string ForgeMaven = "https://maven.minecraftforge.net/net/minecraftforge/forge/";
        public const string NeoMaven = "https://maven.neoforged.net/releases/net/neoforged/neoforge/";

        // ------------------------------------------------------------------ Fabric

        public static async Task<List<LoaderVersion>> GetFabricLoadersAsync(string gameVersion, CancellationToken ct)
        {
            var list = new List<LoaderVersion>();
            var j = await Net.GetJsonAsync(FabricMeta + "versions/loader/" + Net.UrlEncode(gameVersion), ct)
                             .ConfigureAwait(false);
            foreach (var it in j.Items)
            {
                list.Add(new LoaderVersion
                {
                    Version = it["loader"]["version"].AsString(""),
                    Stable = it["loader"]["stable"].AsBool(false)
                });
            }
            if (list.Count > 0) list[0].Recommended = true;
            return list;
        }

        public static async Task<string> InstallFabricAsync(string gameVersion, string loaderVersion, CancellationToken ct)
        {
            string url = FabricMeta + "versions/loader/" + Net.UrlEncode(gameVersion) + "/" +
                         Net.UrlEncode(loaderVersion) + "/profile/json";
            return await SaveProfileAsync(url, "fabric-loader-" + loaderVersion + "-" + gameVersion, ct)
                         .ConfigureAwait(false);
        }

        // ------------------------------------------------------------------ Quilt

        public static async Task<List<LoaderVersion>> GetQuiltLoadersAsync(string gameVersion, CancellationToken ct)
        {
            var list = new List<LoaderVersion>();
            var j = await Net.GetJsonAsync(QuiltMeta + "versions/loader/" + Net.UrlEncode(gameVersion), ct)
                             .ConfigureAwait(false);
            foreach (var it in j.Items)
            {
                string v = it["loader"]["version"].AsString("");
                list.Add(new LoaderVersion { Version = v, Stable = v.IndexOf("beta", StringComparison.OrdinalIgnoreCase) < 0 });
            }
            if (list.Count > 0) list[0].Recommended = true;
            return list;
        }

        public static async Task<string> InstallQuiltAsync(string gameVersion, string loaderVersion, CancellationToken ct)
        {
            string url = QuiltMeta + "versions/loader/" + Net.UrlEncode(gameVersion) + "/" +
                         Net.UrlEncode(loaderVersion) + "/profile/json";
            return await SaveProfileAsync(url, "quilt-loader-" + loaderVersion + "-" + gameVersion, ct)
                         .ConfigureAwait(false);
        }

        private static async Task<string> SaveProfileAsync(string url, string fallbackId, CancellationToken ct)
        {
            string text = await Net.GetStringAsync(url, ct).ConfigureAwait(false);
            var profile = Json.Parse(text);
            if (!profile.IsObject) throw new Exception("Сервер вернул некорректный профиль загрузчика.");

            string id = profile["id"].AsString(fallbackId);
            profile["id"] = Json.Of(id);

            string path = Paths.VersionJson(id);
            Paths.EnsureForFile(path);
            profile.SaveTo(path);
            Log.Info("Профиль загрузчика сохранён: " + id);
            return id;
        }

        // ------------------------------------------------------------------ Forge

        public static async Task<List<LoaderVersion>> GetForgeVersionsAsync(string gameVersion, CancellationToken ct)
        {
            var all = await GetMavenVersionsAsync(ForgeMaven + "maven-metadata.xml", ct).ConfigureAwait(false);

            string recommended = null, latest = null;
            try
            {
                var promos = await Net.GetJsonAsync(ForgeMaven + "promotions_slim.json", ct).ConfigureAwait(false);
                recommended = promos["promos"][gameVersion + "-recommended"].AsString(null);
                latest = promos["promos"][gameVersion + "-latest"].AsString(null);
            }
            catch { }

            var list = new List<LoaderVersion>();
            string prefix = gameVersion + "-";
            foreach (var v in all)
            {
                if (!v.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string forgeVer = v.Substring(prefix.Length);
                // у старых версий суффикс вида "10.13.4.1614-1.7.10"
                int cut = forgeVer.IndexOf('-');
                string shortVer = cut > 0 ? forgeVer.Substring(0, cut) : forgeVer;

                list.Add(new LoaderVersion
                {
                    Version = v,
                    Stable = true,
                    Recommended = (recommended != null && shortVer == recommended)
                });
            }

            list.Reverse();
            if (list.Count > 0 && recommended == null && latest == null) list[0].Recommended = true;
            return list;
        }

        public static Task<string> InstallForgeAsync(string fullVersion, IProgress<InstallStatus> progress, CancellationToken ct)
        {
            string url = ForgeMaven + fullVersion + "/forge-" + fullVersion + "-installer.jar";
            return RunInstallerAsync(url, "forge-" + fullVersion, progress, ct);
        }

        // ------------------------------------------------------------------ NeoForge

        public static async Task<List<LoaderVersion>> GetNeoForgeVersionsAsync(string gameVersion, CancellationToken ct)
        {
            var all = await GetMavenVersionsAsync(NeoMaven + "maven-metadata.xml", ct).ConfigureAwait(false);

            // 1.21.1 -> 21.1. ; 1.21 -> 21.0. ; 1.20.2 -> 20.2.
            string prefix = null;
            var parts = (gameVersion ?? "").Split('.');
            if (parts.Length >= 2 && parts[0] == "1")
            {
                string minor = parts[1];
                string patch = parts.Length >= 3 ? parts[2] : "0";
                prefix = minor + "." + patch + ".";
            }

            var list = new List<LoaderVersion>();
            foreach (var v in all)
            {
                if (prefix != null && !v.StartsWith(prefix, StringComparison.Ordinal)) continue;
                list.Add(new LoaderVersion
                {
                    Version = v,
                    Stable = v.IndexOf("beta", StringComparison.OrdinalIgnoreCase) < 0
                });
            }

            list.Reverse();
            if (list.Count > 0) list[0].Recommended = true;
            return list;
        }

        public static Task<string> InstallNeoForgeAsync(string version, IProgress<InstallStatus> progress, CancellationToken ct)
        {
            string url = NeoMaven + version + "/neoforge-" + version + "-installer.jar";
            return RunInstallerAsync(url, "neoforge-" + version, progress, ct);
        }

        // ------------------------------------------------------------------ Forge Visuals

        public const string VisualsSuffix = "-ForgeVisuals";

        /// <summary>
        /// Создаёт профиль поверх уже установленного Forge. Своя версия нужна,
        /// чтобы у сборки была отдельная папка модов и встроенный мод не попадал
        /// в обычный Forge.
        /// </summary>
        public static string CreateVisualsProfile(string forgeVersionId, string gameVersion)
        {
            string id = gameVersion + VisualsSuffix;

            var profile = Json.NewObject();
            profile.Set("id", id);
            profile.Set("inheritsFrom", forgeVersionId);
            profile.Set("type", "release");
            profile.Set("time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            profile.Set("releaseTime", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            profile["libraries"] = Json.NewArray();

            string path = Paths.VersionJson(id);
            Paths.EnsureForFile(path);
            profile.SaveTo(path);

            Log.Info("Создан профиль Forge Visuals: " + id + " (поверх " + forgeVersionId + ")");
            return id;
        }

        // ------------------------------------------------------------------ общее

        private static async Task<List<string>> GetMavenVersionsAsync(string metadataUrl, CancellationToken ct)
        {
            string xml = await Net.GetStringAsync(metadataUrl, ct).ConfigureAwait(false);
            var list = new List<string>();
            try
            {
                var doc = XDocument.Parse(xml);
                foreach (var el in doc.Descendants("version"))
                {
                    string v = (el.Value ?? "").Trim();
                    if (v.Length > 0) list.Add(v);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Не удалось разобрать maven-metadata: " + ex.Message);
            }
            return list;
        }

        /// <summary>
        /// Скачивает официальный установщик Forge/NeoForge и запускает его в режиме --installClient.
        /// После этого в versions появляется новая папка — её имя и возвращаем.
        /// </summary>
        private static async Task<string> RunInstallerAsync(string url, string title,
                                                            IProgress<InstallStatus> progress,
                                                            CancellationToken ct)
        {
            Paths.EnsureAll();
            EnsureLauncherProfiles();

            string jar = Path.Combine(Paths.CacheDir, title + "-installer.jar");

            if (progress != null)
                progress.Report(new InstallStatus { Text = "Скачиваю установщик " + title + "…", Indeterminate = true });

            await Net.DownloadFileAsync(url, jar, null, ct).ConfigureAwait(false);

            var before = new HashSet<string>(SafeVersionDirs(), StringComparer.OrdinalIgnoreCase);

            // установщику нужна полноценная Java (желательно та же, что и у игры)
            var java = JavaLocator.Pick(17) ?? JavaLocator.Pick(8);
            if (java == null)
            {
                if (progress != null)
                    progress.Report(new InstallStatus { Text = "Java не найдена — скачиваю Java 17…", Indeterminate = true });
                java = await JavaLocator.DownloadJavaAsync(17, progress, ct).ConfigureAwait(false);
            }

            string javaExe = Path.Combine(Path.GetDirectoryName(java.Path) ?? "", "java.exe");
            if (!File.Exists(javaExe)) javaExe = java.Path;

            if (progress != null)
                progress.Report(new InstallStatus { Text = "Запускаю установщик " + title + "…", Indeterminate = true });

            var psi = new ProcessStartInfo
            {
                FileName = javaExe,
                Arguments = "-jar \"" + jar + "\" --installClient \"" + Paths.GameRoot + "\"",
                WorkingDirectory = Paths.CacheDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            int exitCode = await Task.Run(() =>
            {
                using (var p = Process.Start(psi))
                {
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) Log.Info("[installer] " + e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) Log.Warn("[installer] " + e.Data); };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    p.WaitForExit();
                    return p.ExitCode;
                }
            }, ct).ConfigureAwait(false);

            if (exitCode != 0)
                throw new Exception("Установщик " + title + " завершился с кодом " + exitCode +
                                    ". Подробности — на вкладке «Консоль».");

            var after = SafeVersionDirs();
            string created = after.FirstOrDefault(d => !before.Contains(d));

            if (created == null)
            {
                // возможно, версия уже была установлена ранее
                string loaderName = title.Split('-')[0];                       // forge / neoforge
                string verPart = title.Substring(title.LastIndexOf('-') + 1);  // 47.2.0 / 21.1.57
                created = after.FirstOrDefault(d =>
                    d.IndexOf(loaderName, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    d.IndexOf(verPart, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (created == null)
                throw new Exception("Установщик отработал, но новая версия в папке versions не найдена.");

            Log.Info("Установлена версия: " + created);
            return created;
        }

        private static List<string> SafeVersionDirs()
        {
            var list = new List<string>();
            try
            {
                if (Directory.Exists(Paths.VersionsDir))
                    foreach (var d in Directory.GetDirectories(Paths.VersionsDir))
                        list.Add(Path.GetFileName(d));
            }
            catch { }
            return list;
        }

        /// <summary>Установщики Forge отказываются работать без launcher_profiles.json.</summary>
        private static void EnsureLauncherProfiles()
        {
            try
            {
                string path = Path.Combine(Paths.GameRoot, "launcher_profiles.json");
                if (File.Exists(path)) return;

                var j = Json.NewObject();
                j["profiles"] = Json.NewObject();
                j["settings"] = Json.NewObject();
                j.Set("clientToken", Guid.NewGuid().ToString("N"));
                j["authenticationDatabase"] = Json.NewObject();
                j.Set("selectedUser", "");
                var lv = Json.NewObject();
                lv.Set("name", "2.9.2");
                lv.Set("format", 21);
                lv.Set("profilesFormat", 2);
                j["launcherVersion"] = lv;
                j.Set("version", 3);

                Paths.EnsureForFile(path);
                j.SaveTo(path);
            }
            catch (Exception ex) { Log.Warn("launcher_profiles.json не создан: " + ex.Message); }
        }

        /// <summary>Список ванильных версий, для которых Fabric умеет работать.</summary>
        public static async Task<List<string>> GetFabricGameVersionsAsync(bool stableOnly, CancellationToken ct)
        {
            var list = new List<string>();
            var j = await Net.GetJsonAsync(FabricMeta + "versions/game", ct).ConfigureAwait(false);
            foreach (var it in j.Items)
            {
                if (stableOnly && !it["stable"].AsBool(false)) continue;
                list.Add(it["version"].AsString(""));
            }
            return list;
        }
    }
}
