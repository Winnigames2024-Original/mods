using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NareLauncher.Core
{
    /// <summary>
    /// Моды, которые лежат прямо в релизах на GitHub. Там живёт половина
    /// небольших модов, которых нет ни на Modrinth, ни на CurseForge.
    /// Ключ не нужен, но без него GitHub даёт 60 запросов в час — поэтому
    /// результаты держим в памяти до перезапуска.
    /// </summary>
    public class GitHubSource : IModSource
    {
        public string Id { get { return "github"; } }
        public string Title { get { return "GitHub"; } }

        public bool Ready(out string problem) { problem = null; return true; }
        public bool Supports(string projectType) { return true; }

        private static readonly Dictionary<string, List<ModFile>> Releases =
            new Dictionary<string, List<ModFile>>(StringComparer.OrdinalIgnoreCase);

        private static string Mask(string projectType) { return projectType == "mod" ? ".jar" : ".zip"; }

        public async Task<List<ModHit>> SearchAsync(string query, string projectType, string gameVersion,
                                                     string loader, int offset, int limit, CancellationToken ct)
        {
            var words = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(query)) words.Append(query.Trim()).Append(' ');

            words.Append("minecraft ");
            switch (projectType)
            {
                case "resourcepack": words.Append("resourcepack"); break;
                case "shader": words.Append("shader"); break;
                case "datapack": words.Append("datapack"); break;
                default: words.Append("mod"); break;
            }

            if (!string.IsNullOrWhiteSpace(loader) && projectType == "mod") words.Append(' ').Append(loader);

            int page = limit > 0 ? offset / limit + 1 : 1;

            string url = "https://api.github.com/search/repositories?sort=stars&order=desc"
                       + "&per_page=" + Math.Max(1, Math.Min(50, limit))
                       + "&page=" + Math.Max(1, Math.Min(10, page))
                       + "&q=" + Net.UrlEncode(words.ToString().Trim());

            Json json;
            try
            {
                json = await Net.GetJsonAsync(url, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new Exception("GitHub не ответил (" + ex.Message +
                                    "). Без ключа он разрешает 60 запросов в час — попробуйте через минуту.", ex);
            }

            var list = new List<ModHit>();
            foreach (var repo in json["items"].Items)
            {
                string full = repo["full_name"].AsString("");
                if (string.IsNullOrEmpty(full)) continue;

                list.Add(new ModHit
                {
                    SourceId = Id,
                    ProjectId = full,
                    Slug = full,
                    Title = repo["name"].AsString(full),
                    Description = repo["description"].AsString("без описания"),
                    Author = repo["owner"]["login"].AsString(""),
                    IconUrl = repo["owner"]["avatar_url"].AsString(""),
                    ProjectType = projectType,
                    Downloads = repo["stargazers_count"].AsLong(0)
                });
            }
            return list;
        }

        public async Task<List<ModFile>> GetFilesAsync(ModHit hit, string gameVersion, string loader,
                                                        CancellationToken ct)
        {
            string key = hit.ProjectId + "|" + hit.ProjectType;

            List<ModFile> all;
            lock (Releases)
            {
                if (!Releases.TryGetValue(key, out all)) all = null;
            }

            if (all == null)
            {
                var json = await Net.GetJsonAsync(
                    "https://api.github.com/repos/" + hit.ProjectId + "/releases?per_page=30",
                    ct).ConfigureAwait(false);

                all = new List<ModFile>();
                string mask = Mask(hit.ProjectType);

                foreach (var release in json.Items)
                {
                    string tag = release["tag_name"].AsString("");

                    foreach (var asset in release["assets"].Items)
                    {
                        string name = asset["name"].AsString("");
                        if (!name.EndsWith(mask, StringComparison.OrdinalIgnoreCase)) continue;
                        if (name.IndexOf("source", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                        all.Add(new ModFile
                        {
                            ProjectId = hit.ProjectId,
                            VersionId = asset["id"].AsLong(0).ToString(),
                            Name = release["name"].AsString(tag),
                            VersionNumber = tag,
                            FileName = name,
                            Url = asset["browser_download_url"].AsString(""),
                            Size = asset["size"].AsLong(0),
                            Channel = release["prerelease"].AsBool(false) ? "beta" : "release"
                        });
                    }
                }

                lock (Releases) Releases[key] = all;
            }

            // сначала то, где в имени файла стоит нужная версия игры
            if (!string.IsNullOrWhiteSpace(gameVersion))
            {
                var fit = new List<ModFile>();
                var rest = new List<ModFile>();

                foreach (var f in all)
                {
                    if (f.FileName.IndexOf(gameVersion, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        f.VersionNumber.IndexOf(gameVersion, StringComparison.OrdinalIgnoreCase) >= 0)
                        fit.Add(f);
                    else rest.Add(f);
                }

                if (fit.Count > 0)
                {
                    fit.AddRange(rest);
                    return fit;
                }
            }

            return all;
        }

        public async Task<List<string>> InstallAsync(ModFile file, string projectType, string versionId,
                                                      string gameVersion, string loader,
                                                      Action<string> status, CancellationToken ct)
        {
            if (status != null) status("Скачиваю " + file.FileName + "…");

            string path = await ModrinthApi.DownloadAsync(file, projectType, versionId, ct)
                                           .ConfigureAwait(false);

            // GitHub ничего не обещает про совместимость — проверяем сами по jar
            if (projectType == "mod" && status != null)
            {
                var about = ModJar.Read(path);
                bool? fits = about.Fits(gameVersion, loader);

                if (fits == false)
                    status("Поставлен, но мод заявляет " + about.Short + " — для этой сборки он не подходит.");
                else if (about.Known)
                    status("Поставлен: " + about.Short);
            }

            return new List<string> { file.FileName };
        }
    }

    /// <summary>
    /// Наш собственный список модов: catalog.json рядом с модом на GitHub
    /// Pages. Туда можно дописать любой мод руками — лаунчер покажет его
    /// здесь, со своими версиями и описанием.
    /// </summary>
    public class NareCatalogSource : IModSource
    {
        public const string CatalogUrl =
            "https://winnigames2024-original.github.io/mods/narevisuals/catalog.json";

        public string Id { get { return "nare"; } }
        public string Title { get { return "Наш список"; } }

        public bool Ready(out string problem) { problem = null; return true; }
        public bool Supports(string projectType) { return true; }

        private class Entry
        {
            public ModHit Hit;
            public List<ModFile> Files = new List<ModFile>();
            public List<string> Games = new List<string>();
            public string Loader = "";
        }

        private static List<Entry> _cache;
        private static DateTime _when;

        private static async Task<List<Entry>> LoadAsync(CancellationToken ct)
        {
            if (_cache != null && (DateTime.UtcNow - _when).TotalMinutes < 10) return _cache;

            var json = await Net.GetJsonAsync(CatalogUrl + "?t=" + DateTime.UtcNow.Ticks, ct)
                                .ConfigureAwait(false);

            var list = new List<Entry>();
            foreach (var mod in json["mods"].Items)
            {
                var entry = new Entry
                {
                    Loader = mod["loader"].AsString(""),
                    Hit = new ModHit
                    {
                        SourceId = "nare",
                        ProjectId = mod["id"].AsString(""),
                        Slug = mod["id"].AsString(""),
                        Title = mod["title"].AsString(""),
                        Description = mod["description"].AsString(""),
                        Author = mod["author"].AsString(""),
                        IconUrl = mod["icon"].AsString(""),
                        ProjectType = mod["type"].AsString("mod")
                    }
                };

                foreach (var g in mod["game"].Items)
                {
                    string s = g.AsString(null);
                    if (!string.IsNullOrEmpty(s)) entry.Games.Add(s);
                }
                if (entry.Games.Count == 0)
                {
                    string one = mod["game"].AsString(null);
                    if (!string.IsNullOrEmpty(one)) entry.Games.Add(one);
                }

                foreach (var r in mod["releases"].Items)
                {
                    entry.Files.Add(new ModFile
                    {
                        ProjectId = entry.Hit.ProjectId,
                        VersionId = r["version"].AsString(""),
                        Name = r["notes"].AsString(entry.Hit.Title),
                        VersionNumber = r["version"].AsString(""),
                        FileName = r["file"].AsString(""),
                        Url = r["url"].AsString(""),
                        Sha1 = r["sha1"].AsString(""),
                        Size = r["size"].AsLong(0),
                        Channel = r["channel"].AsString("release")
                    });
                }

                if (!string.IsNullOrEmpty(entry.Hit.ProjectId)) list.Add(entry);
            }

            _cache = list;
            _when = DateTime.UtcNow;
            return list;
        }

        /// <summary>Подходит ли запись к сборке: берём то, что сам мод пишет про версии.</summary>
        private static bool Fits(Entry entry, string gameVersion, string loader)
        {
            if (!string.IsNullOrEmpty(entry.Loader) && !string.IsNullOrEmpty(loader) &&
                !string.Equals(entry.Loader, loader, StringComparison.OrdinalIgnoreCase)) return false;

            if (entry.Games.Count == 0 || string.IsNullOrWhiteSpace(gameVersion)) return true;

            foreach (var g in entry.Games)
            {
                bool? covers = ModJar.Covers(g, gameVersion);
                if (covers == true || covers == null) return true;
            }
            return false;
        }

        public async Task<List<ModHit>> SearchAsync(string query, string projectType, string gameVersion,
                                                     string loader, int offset, int limit, CancellationToken ct)
        {
            var all = await LoadAsync(ct).ConfigureAwait(false);
            var list = new List<ModHit>();

            foreach (var entry in all)
            {
                if (entry.Hit.ProjectType != projectType) continue;
                if (!Fits(entry, gameVersion, loader)) continue;

                if (!string.IsNullOrWhiteSpace(query) &&
                    (entry.Hit.Title + " " + entry.Hit.Description)
                        .IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) < 0) continue;

                list.Add(entry.Hit);
            }

            if (offset > 0) list.RemoveRange(0, Math.Min(offset, list.Count));
            if (list.Count > limit) list.RemoveRange(limit, list.Count - limit);
            return list;
        }

        public async Task<List<ModFile>> GetFilesAsync(ModHit hit, string gameVersion, string loader,
                                                        CancellationToken ct)
        {
            var all = await LoadAsync(ct).ConfigureAwait(false);

            foreach (var entry in all)
                if (entry.Hit.ProjectId == hit.ProjectId) return entry.Files;

            return new List<ModFile>();
        }

        public async Task<List<string>> InstallAsync(ModFile file, string projectType, string versionId,
                                                      string gameVersion, string loader,
                                                      Action<string> status, CancellationToken ct)
        {
            if (status != null) status("Скачиваю " + file.FileName + "…");

            await ModrinthApi.DownloadAsync(file, projectType, versionId, ct).ConfigureAwait(false);
            Log.Info("Из нашего списка поставлено: " + file.FileName);
            return new List<string> { file.FileName };
        }
    }

    /// <summary>
    /// Прямая ссылка. Вставили в поиск адрес jar или zip — лаунчер скачает
    /// именно его. Нужно, когда мод лежит на сайте автора и больше нигде.
    /// </summary>
    public class DirectUrlSource : IModSource
    {
        public string Id { get { return "url"; } }
        public string Title { get { return "Прямая ссылка"; } }

        public bool Ready(out string problem) { problem = null; return true; }
        public bool Supports(string projectType) { return true; }

        private static bool Looks(string text, string projectType)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            string url = text.Trim();
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return false;

            string clean = url.Split('?')[0].Split('#')[0];
            return clean.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
                || clean.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        }

        private static string FileOf(string url)
        {
            string clean = url.Split('?')[0].Split('#')[0];
            int slash = clean.LastIndexOf('/');
            string name = slash >= 0 && slash + 1 < clean.Length ? clean.Substring(slash + 1) : "";
            return name == "" ? "download.jar" : Uri.UnescapeDataString(name);
        }

        public Task<List<ModHit>> SearchAsync(string query, string projectType, string gameVersion,
                                              string loader, int offset, int limit, CancellationToken ct)
        {
            var list = new List<ModHit>();
            if (offset > 0 || !Looks(query, projectType)) return Task.FromResult(list);

            string url = query.Trim();
            list.Add(new ModHit
            {
                SourceId = Id,
                ProjectId = url,
                Title = FileOf(url),
                Description = url,
                Author = new Uri(url).Host,
                ProjectType = projectType
            });

            return Task.FromResult(list);
        }

        public Task<List<ModFile>> GetFilesAsync(ModHit hit, string gameVersion, string loader,
                                                  CancellationToken ct)
        {
            var list = new List<ModFile>
            {
                new ModFile
                {
                    ProjectId = hit.ProjectId,
                    VersionId = "url",
                    Name = FileOf(hit.ProjectId),
                    VersionNumber = "по ссылке",
                    FileName = FileOf(hit.ProjectId),
                    Url = hit.ProjectId
                }
            };
            return Task.FromResult(list);
        }

        public async Task<List<string>> InstallAsync(ModFile file, string projectType, string versionId,
                                                      string gameVersion, string loader,
                                                      Action<string> status, CancellationToken ct)
        {
            if (status != null) status("Скачиваю " + file.FileName + "…");

            string path = await ModrinthApi.DownloadAsync(file, projectType, versionId, ct)
                                           .ConfigureAwait(false);

            if (projectType == "mod" && status != null)
            {
                var about = ModJar.Read(path);
                bool? fits = about.Fits(gameVersion, loader);

                if (fits == false)
                    status("Скачан, но заявляет " + about.Short + " — для этой сборки не подходит.");
                else if (about.Known)
                    status("Скачан: " + about.Label + " " + about.Version + " · " + about.Short);
            }

            return new List<string> { file.FileName };
        }
    }
}
