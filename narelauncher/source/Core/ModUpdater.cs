using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NareLauncher.Core
{
    /// <summary>
    /// Обновление мода NareVisuals прямо с GitHub.
    ///
    /// Лаунчер читает маленький файл-описание (update.json), сравнивает его
    /// с тем, что лежит в сборке, и качает jar только если он правда другой.
    /// Если интернета нет или GitHub недоступен — ничего страшного: остаётся
    /// тот мод, который уже стоит, а в сборку лягут встроенные моды, как и
    /// раньше.
    /// </summary>
    public static class ModUpdater
    {
        /// <summary>Описание свежей версии мода.</summary>
        public const string DefaultManifest =
            "https://raw.githubusercontent.com/Winnigames2024-Original/mods/main/narevisuals/update.json";

        public class Release
        {
            public string Version = "";
            public string File = "";
            public string Url = "";
            public string Sha1 = "";
            public string Notes = "";
            public long Size;
        }

        /// <summary>
        /// Начало имени файла, которым распоряжается GitHub, например
        /// "forge_narevisuals-". Встроенные моды с таким же началом в сборку
        /// не кладутся: два jar одного мода — и Forge не запустится.
        /// </summary>
        private static string managed;

        /// <summary>
        /// Если GitHub не ответил, помним прошлый раз: встроенная копия всё
        /// равно не должна лезть поверх того, что мы оттуда поставили.
        /// </summary>
        public static string ManagedPrefix
        {
            get { return managed ?? Prefix(Settings.Current.ModFileFromGitHub); }
            private set { managed = value; }
        }

        /// <summary>«forge_narevisuals-1.1.1.jar» -> «forge_narevisuals-».</summary>
        public static string Prefix(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;

            int dash = file.LastIndexOf('-');
            return dash > 0 ? file.Substring(0, dash + 1) : null;
        }

        /// <summary>Список всех выпусков лежит рядом с манифестом.</summary>
        private static string CatalogUrl
        {
            get
            {
                string url = ManifestUrl;
                int slash = url.LastIndexOf('/');
                return slash > 0 ? url.Substring(0, slash + 1) + "versions.json" : null;
            }
        }

        private static string ManifestUrl
        {
            get
            {
                string own = Settings.Current.ModUpdateUrl;
                return string.IsNullOrWhiteSpace(own) ? DefaultManifest : own.Trim();
            }
        }

        // ------------------------------------------------------------ проверка

        /// <summary>Спрашивает GitHub, что там сейчас лежит. null — не ответил.</summary>
        public static async Task<Release> CheckAsync(CancellationToken ct)
        {
            try
            {
                var json = await Net.GetJsonAsync(ManifestUrl, ct).ConfigureAwait(false);
                if (!json.IsObject) return null;

                var release = new Release
                {
                    Version = json["version"].AsString(""),
                    File = json["file"].AsString(""),
                    Url = json["url"].AsString(""),
                    Sha1 = json["sha1"].AsString("").ToLowerInvariant(),
                    Notes = json["notes"].AsString(""),
                    Size = json["size"].AsLong(0)
                };

                if (string.IsNullOrEmpty(release.File) || string.IsNullOrEmpty(release.Url)) return null;
                return release;
            }
            catch (Exception ex)
            {
                Log.Warn("GitHub не ответил про мод: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Все выпуски мода, свежий первым. Если списка на сервере нет —
        /// отдаём один выпуск из обычного манифеста, чтобы окно выбора
        /// не оказалось пустым.
        /// </summary>
        public static async Task<List<Release>> ListAsync(CancellationToken ct)
        {
            var list = new List<Release>();

            try
            {
                string url = CatalogUrl;
                if (!string.IsNullOrEmpty(url))
                {
                    var json = await Net.GetJsonAsync(url, ct).ConfigureAwait(false);
                    var items = json["releases"];

                    if (items.IsArray)
                    {
                        for (int i = 0; i < items.Count; i++)
                        {
                            var item = items[i];
                            var release = new Release
                            {
                                Version = item["version"].AsString(""),
                                File = item["file"].AsString(""),
                                Url = item["url"].AsString(""),
                                Sha1 = item["sha1"].AsString("").ToLowerInvariant(),
                                Notes = item["notes"].AsString(""),
                                Size = item["size"].AsLong(0)
                            };

                            if (!string.IsNullOrEmpty(release.File) && !string.IsNullOrEmpty(release.Url))
                                list.Add(release);
                        }
                    }
                }
            }
            catch (Exception ex) { Log.Warn("Список версий мода не пришёл: " + ex.Message); }

            if (list.Count == 0)
            {
                var one = await CheckAsync(ct).ConfigureAwait(false);
                if (one != null) list.Add(one);
            }

            return list;
        }

        /// <summary>
        /// Какой выпуск ставим: закреплённый в настройках или самый свежий.
        /// Пустое «закреплено» значит «всегда последняя».
        /// </summary>
        private static async Task<Release> WantedAsync(CancellationToken ct)
        {
            string pin = (Settings.Current.VisualsVersion ?? "").Trim();
            if (pin.Length == 0) return await CheckAsync(ct).ConfigureAwait(false);

            var all = await ListAsync(ct).ConfigureAwait(false);
            foreach (var release in all)
                if (string.Equals(release.Version, pin, StringComparison.OrdinalIgnoreCase))
                    return release;

            Log.Warn("Закреплённая версия мода " + pin + " не найдена — беру свежую.");
            return await CheckAsync(ct).ConfigureAwait(false);
        }

        // ---------------------------------------------------------- обновление

        /// <summary>
        /// Докладывает свежий мод в сборку. Возвращает строку для статуса —
        /// её не стыдно показать человеку как есть.
        /// </summary>
        public static async Task<string> UpdateAsync(string versionId,
                                                     IProgress<InstallStatus> progress,
                                                     CancellationToken ct)
        {
            if (string.IsNullOrEmpty(versionId)) return "Сначала выберите сборку.";

            var release = await WantedAsync(ct).ConfigureAwait(false);
            if (release == null) return null;      // молча отдаём работу встроенным модам

            ManagedPrefix = Prefix(release.File);

            string dir = Paths.ModsDir(versionId);
            Paths.Ensure(dir);
            string target = Path.Combine(dir, release.File);

            // Решаем по версии, а не по самому файлу. Иначе любая своя пересборка
            // мода выглядит как «файл не тот» и честная свежая сборка каждый раз
            // откатывается обратно на выложенную.
            bool missing = !File.Exists(target);
            bool fresh = !string.Equals(release.Version, Settings.Current.ModVersionFromGitHub,
                                        StringComparison.OrdinalIgnoreCase);

            // Прежние версии убираем всегда, а не только после закачки. Иначе
            // руками положенный старый jar остаётся лежать рядом со свежим, и
            // Forge отказывается стартовать: два одинаковых мода.
            DropOld(dir, release.File);

            if (!missing && !fresh)
            {
                if (Same(target, release))
                {
                    Log.Info("Мод с GitHub уже стоит: " + release.File + " " + release.Version);
                    return "Мод уже свежий: " + release.Version;
                }

                Log.Info("В сборке лежит своя сборка мода — с GitHub ничего не трогаем.");
                return "В сборке своя сборка мода — оставил её.";
            }

            if (progress != null)
                progress.Report(new InstallStatus
                {
                    Text = "Качаю мод " + release.Version + " с GitHub…",
                    Indeterminate = true
                });

            string temp = Path.Combine(dir, release.File + ".new");

            try
            {
                await Net.DownloadFileAsync(release.Url, temp, null, ct).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(release.Sha1))
                {
                    string got = (Net.Sha1File(temp) ?? "").ToLowerInvariant();
                    if (got != release.Sha1)
                    {
                        try { File.Delete(temp); } catch { }
                        Log.Warn("Скачанный мод не сошёлся по контрольной сумме — не ставим.");
                        return "Скачанный файл повреждён — оставил прежний мод.";
                    }
                }

                // прежний jar не выбрасываем, а отодвигаем: вдруг новый окажется хуже
                if (File.Exists(target))
                {
                    string backup = target + ".old";
                    try { if (File.Exists(backup)) File.Delete(backup); } catch { }
                    try { File.Move(target, backup); }
                    catch { try { File.Delete(target); } catch { } }
                }

                // отодвинуть не вышло — значит просто убираем с дороги
                if (File.Exists(target))
                {
                    try { File.Delete(target); }
                    catch (Exception ex)
                    {
                        try { File.Delete(temp); } catch { }
                        Log.Error("Старый мод занят и не убирается", ex);
                        return "Старый мод занят другой программой — закройте игру и попробуйте снова.";
                    }
                }

                File.Move(temp, target);
                DropOld(dir, release.File);

                Settings.Current.ModVersionFromGitHub = release.Version;
                Settings.Current.ModFileFromGitHub = release.File;
                Settings.Save();

                Log.Info("Мод обновлён с GitHub: " + release.File + " " + release.Version);
                return "Мод обновлён с GitHub: " + release.Version
                     + (string.IsNullOrEmpty(release.Notes) ? "" : " — " + release.Notes);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                Log.Error("Не удалось скачать мод с GitHub", ex);
                return "Скачать мод не вышло: " + ex.Message;
            }
        }

        /// <summary>
        /// Убирает прежние версии того же мода. Имя jar меняется вместе с
        /// версией, и если старый файл останется рядом, Forge увидит два
        /// одинаковых мода и откажется запускаться.
        /// </summary>
        private static void DropOld(string dir, string keep)
        {
            string prefix = Prefix(keep);
            if (prefix == null) return;

            try
            {
                foreach (string path in Directory.GetFiles(dir, prefix + "*.jar"))
                {
                    if (string.Equals(Path.GetFileName(path), keep, StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        File.Delete(path);
                        Log.Info("Убран прежний jar мода: " + Path.GetFileName(path));
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("Прежний jar не убрался: " + Path.GetFileName(path) + " — " + ex.Message);
                    }
                }
            }
            catch { }
        }

        /// <summary>Тот ли это файл, что уже лежит в сборке.</summary>
        private static bool Same(string path, Release release)
        {
            if (!File.Exists(path)) return false;

            if (!string.IsNullOrEmpty(release.Sha1))
            {
                string got = (Net.Sha1File(path) ?? "").ToLowerInvariant();
                return got == release.Sha1;
            }

            // контрольной суммы в описании нет — сверим хотя бы размер
            try { return release.Size > 0 && new FileInfo(path).Length == release.Size; }
            catch { return false; }
        }
    }
}
