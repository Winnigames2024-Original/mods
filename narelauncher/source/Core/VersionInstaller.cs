using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace NareLauncher.Core
{
    public class InstallStatus
    {
        public string Text = "";
        public int Done;
        public int Total;
        public bool Indeterminate;
    }

    public static class VersionInstaller
    {
        /// <summary>
        /// Полная установка версии: json, client.jar, библиотеки, нативы, ассеты.
        /// Уже скачанные и совпавшие по SHA-1 файлы пропускаются.
        /// </summary>
        public static async Task<Json> InstallAsync(string versionId,
                                                    IProgress<InstallStatus> progress,
                                                    CancellationToken ct)
        {
            Paths.EnsureAll();
            Report(progress, "Читаю описание версии " + versionId + "…", true);

            var resolved = await MinecraftMeta.ResolveAsync(versionId, ct).ConfigureAwait(false);

            var all = new List<DownloadTask>();

            // ---- client.jar ----
            string jarId = resolved["jar"].AsString(null);
            if (string.IsNullOrEmpty(jarId)) jarId = resolved["inheritsFrom"].AsString(null);
            if (string.IsNullOrEmpty(jarId)) jarId = versionId;

            var client = resolved["downloads"]["client"];
            string jarPath = Paths.VersionJar(jarId);
            if (client.IsObject && !string.IsNullOrEmpty(client["url"].AsString(null)))
            {
                all.Add(new DownloadTask(client["url"].AsString(""), jarPath,
                                         client["sha1"].AsString(null),
                                         client["size"].AsLong(0),
                                         jarId + ".jar"));
            }
            else if (!File.Exists(jarPath))
            {
                throw new Exception("В описании версии нет ссылки на client.jar, и файл отсутствует: " + jarPath);
            }

            // ---- библиотеки ----
            Report(progress, "Составляю список библиотек…", true);
            var libs = LibraryUtil.ResolveAll(resolved, Features());
            foreach (var l in libs)
            {
                if (string.IsNullOrEmpty(l.Url))
                {
                    if (!File.Exists(l.Path))
                        Log.Warn("Нет ссылки на библиотеку " + l.Name + ", файл отсутствует.");
                    continue;
                }
                all.Add(new DownloadTask(l.Url, l.Path, l.Sha1, l.Size, l.Name));
            }

            // ---- конфиг логирования (log4j) ----
            var logFile = resolved["logging"]["client"]["file"];
            if (logFile.IsObject)
            {
                string lid = logFile["id"].AsString("client.xml");
                string lp = Path.Combine(Paths.AssetsDir, "log_configs", lid);
                string lu = logFile["url"].AsString(null);
                if (!string.IsNullOrEmpty(lu))
                    all.Add(new DownloadTask(lu, lp, logFile["sha1"].AsString(null), logFile["size"].AsLong(0), lid));
            }

            // ---- индекс ассетов ----
            Json assetIndex = Json.Null;
            string assetIndexId = resolved["assetIndex"]["id"].AsString(resolved["assets"].AsString(null));
            if (resolved["assetIndex"].IsObject && !string.IsNullOrEmpty(assetIndexId))
            {
                string idxPath = Path.Combine(Paths.AssetIndexesDir, assetIndexId + ".json");
                string idxUrl = resolved["assetIndex"]["url"].AsString(null);
                string idxSha = resolved["assetIndex"]["sha1"].AsString(null);

                bool need = !File.Exists(idxPath);
                if (!need && !string.IsNullOrEmpty(idxSha) && Settings.Current.VerifyHashes)
                    need = !string.Equals(Net.Sha1File(idxPath), idxSha, StringComparison.OrdinalIgnoreCase);

                if (need && !string.IsNullOrEmpty(idxUrl))
                {
                    Report(progress, "Скачиваю индекс ресурсов…", true);
                    await Net.DownloadFileAsync(idxUrl, idxPath, null, ct).ConfigureAwait(false);
                }
                assetIndex = Json.ParseFile(idxPath);
            }

            // ---- объекты ассетов ----
            if (assetIndex.IsObject)
            {
                var objects = assetIndex["objects"];
                foreach (var kv in objects.Pairs)
                {
                    string hash = kv.Value["hash"].AsString(null);
                    if (string.IsNullOrEmpty(hash) || hash.Length < 2) continue;
                    string sub = hash.Substring(0, 2);
                    string target = Path.Combine(Paths.AssetObjectsDir, sub, hash);
                    all.Add(new DownloadTask(MinecraftMeta.ResourcesUrl + sub + "/" + hash,
                                             target, hash, kv.Value["size"].AsLong(0), kv.Key));
                }
            }

            // ---- качаем ----
            var dl = new ParallelDownloader();
            var inner = new Progress<DownloadProgress>(p =>
            {
                Report(progress, "Загрузка файлов — " + p.Done + " / " + p.Total, false, p.Done, p.Total);
            });

            Report(progress, "Загрузка файлов…", false, 0, all.Count);
            await dl.RunAsync(all, inner, ct, "files").ConfigureAwait(false);

            if (dl.Failed.Count > 0)
            {
                Log.Warn("Не удалось скачать файлов: " + dl.Failed.Count);
                foreach (var f in dl.Failed) Log.Warn("  " + f);
            }

            // ---- нативы ----
            Report(progress, "Распаковываю нативные библиотеки…", true);
            ExtractNatives(versionId, libs);

            // ---- legacy/virtual ассеты ----
            if (assetIndex.IsObject)
            {
                if (assetIndex["virtual"].AsBool(false))
                {
                    Report(progress, "Готовлю virtual-ресурсы…", true);
                    CopyAssets(assetIndex, Path.Combine(Paths.AssetVirtualDir, assetIndexId ?? "legacy"));
                }
                if (assetIndex["map_to_resources"].AsBool(false))
                {
                    Report(progress, "Готовлю ресурсы для старой версии…", true);
                    CopyAssets(assetIndex, Path.Combine(Paths.InstanceDir(versionId), "resources"));
                }
            }

            Report(progress, "Версия " + versionId + " готова.", false, 1, 1);
            Log.Info("Версия установлена: " + versionId);
            return resolved;
        }

        public static Dictionary<string, bool> Features()
        {
            return new Dictionary<string, bool>
            {
                { "is_demo_user", Settings.Current.DemoMode },
                { "has_custom_resolution", !Settings.Current.Fullscreen },
                { "has_quick_plays_support", false },
                { "is_quick_play_singleplayer", false },
                { "is_quick_play_multiplayer", false },
                { "is_quick_play_realms", false }
            };
        }

        private static void Report(IProgress<InstallStatus> p, string text, bool indeterminate,
                                   int done = 0, int total = 0)
        {
            if (p == null) return;
            p.Report(new InstallStatus { Text = text, Indeterminate = indeterminate, Done = done, Total = total });
        }

        public static void ExtractNatives(string versionId, List<LibraryUtil.LibFile> libs)
        {
            string dir = Paths.NativesDir(versionId);
            Paths.Ensure(dir);

            foreach (var lib in libs)
            {
                if (!lib.Native) continue;
                if (!File.Exists(lib.Path)) continue;

                var exclude = new List<string>();
                foreach (var e in lib.ExtractRules["exclude"].Items)
                {
                    string s = e.AsString(null);
                    if (!string.IsNullOrEmpty(s)) exclude.Add(s);
                }

                try
                {
                    using (var zip = ZipFile.OpenRead(lib.Path))
                    {
                        foreach (var entry in zip.Entries)
                        {
                            if (string.IsNullOrEmpty(entry.Name)) continue;            // папка
                            string name = entry.FullName.Replace('\\', '/');
                            if (name.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase)) continue;

                            bool skip = false;
                            foreach (var ex in exclude)
                                if (name.StartsWith(ex.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)) { skip = true; break; }
                            if (skip) continue;

                            // интересуют только нативные бинарники
                            string ext = Path.GetExtension(name).ToLowerInvariant();
                            if (ext != ".dll" && ext != ".so" && ext != ".dylib" && ext != ".jnilib" && ext != ".cat") continue;

                            string target = Path.Combine(dir, Path.GetFileName(name));
                            if (File.Exists(target))
                            {
                                try
                                {
                                    var fi = new FileInfo(target);
                                    if (fi.Length == entry.Length) continue;
                                }
                                catch { }
                            }

                            try { entry.ExtractToFile(target, true); }
                            catch (IOException) { /* файл занят запущенной игрой */ }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("Не удалось распаковать нативы " + lib.Name + ": " + ex.Message);
                }
            }
        }

        private static void CopyAssets(Json index, string targetRoot)
        {
            foreach (var kv in index["objects"].Pairs)
            {
                string hash = kv.Value["hash"].AsString(null);
                if (string.IsNullOrEmpty(hash) || hash.Length < 2) continue;

                string src = Path.Combine(Paths.AssetObjectsDir, hash.Substring(0, 2), hash);
                if (!File.Exists(src)) continue;

                string dst = Path.Combine(targetRoot, kv.Key.Replace('/', '\\'));
                if (File.Exists(dst)) continue;

                try
                {
                    Paths.EnsureForFile(dst);
                    File.Copy(src, dst, false);
                }
                catch { }
            }
        }
    }
}
