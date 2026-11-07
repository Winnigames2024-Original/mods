using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NareLauncher.Core
{
    public class ModHit
    {
        public string ProjectId = "";
        public string Slug = "";
        public string Title = "";
        public string Description = "";
        public string Author = "";
        public string IconUrl = "";
        public string ProjectType = "mod";
        public long Downloads;
        public string SourceId = "modrinth";
        public List<string> Categories = new List<string>();

        public string DownloadsText
        {
            get
            {
                if (Downloads >= 1000000) return (Downloads / 1000000.0).ToString("0.#") + " млн";
                if (Downloads >= 1000) return (Downloads / 1000.0).ToString("0.#") + " тыс.";
                return Downloads.ToString();
            }
        }
    }

    public class ModFile
    {
        public string VersionId = "";
        public string ProjectId = "";
        public string Name = "";
        public string VersionNumber = "";
        public string FileName = "";
        public string Url = "";
        public string Sha1 = "";
        public long Size;
        public string Channel = "release";
        /// <summary>Для локального источника: путь к файлу на диске вместо ссылки.</summary>
        public string LocalPath = "";
        public List<string> RequiredDependencies = new List<string>();

        public override string ToString()
        {
            return VersionNumber + " — " + Name;
        }
    }

    public static class ModrinthApi
    {
        public const string Api = "https://api.modrinth.com/v2/";

        /// <summary>Типы проектов, которые умеет ставить лаунчер.</summary>
        public static readonly string[] ProjectTypes = { "mod", "resourcepack", "shader", "datapack" };

        public static string ProjectTypeRu(string type)
        {
            switch (type)
            {
                case "mod": return "Моды";
                case "resourcepack": return "Ресурспаки";
                case "shader": return "Шейдеры";
                case "datapack": return "Датапаки";
                default: return type;
            }
        }

        /// <summary>
        /// Загрузчик, по которому фильтруются файлы на Modrinth.
        /// У модов это fabric/forge/..., а у ресурспаков, шейдеров и датапаков
        /// совсем другие значения ("minecraft", "iris", "datapack"), поэтому
        /// фильтр по модлоадеру для них отключён — иначе список выходит пустым.
        /// </summary>
        public static string VersionLoader(string projectType, string modLoader)
        {
            return projectType == "mod" ? modLoader : null;
        }

        /// <summary>
        /// Один фильтр. Значение берём в кавычки: без них Modnrinth отвечает
        /// 400, если внутри оказался пробел — а в названии своей сборки
        /// пробел дело обычное.
        /// </summary>
        private static string Facet(string name, string value)
        {
            return "[\"" + name + ":\\\"" + value.Trim().Replace("\\", "").Replace("\"", "") + "\\\"\"]";
        }

        private static string Facets(string projectType, string gameVersion, string loader)
        {
            var groups = new List<string>();
            if (!string.IsNullOrWhiteSpace(projectType)) groups.Add(Facet("project_type", projectType));

            // фильтр версии ставим только если это действительно номер версии
            if (ModJar.LooksLikeGameVersion(gameVersion)) groups.Add(Facet("versions", gameVersion));

            // категория-загрузчик есть только у модов
            if (!string.IsNullOrWhiteSpace(loader) && projectType == "mod")
                groups.Add(Facet("categories", loader));

            return "[" + string.Join(",", groups.ToArray()) + "]";
        }

        public static async Task<List<ModHit>> SearchAsync(string query, string projectType,
                                                           string gameVersion, string loader,
                                                           int offset, int limit, CancellationToken ct)
        {
            var sb = new StringBuilder(Api + "search?");
            sb.Append("limit=").Append(limit);
            sb.Append("&offset=").Append(offset);
            sb.Append("&index=").Append(string.IsNullOrWhiteSpace(query) ? "downloads" : "relevance");
            if (!string.IsNullOrWhiteSpace(query)) sb.Append("&query=").Append(Net.UrlEncode(query));
            sb.Append("&facets=").Append(Net.UrlEncode(Facets(projectType, gameVersion, loader)));

            var j = await Net.GetJsonAsync(sb.ToString(), ct).ConfigureAwait(false);

            var list = new List<ModHit>();
            foreach (var h in j["hits"].Items)
            {
                var hit = new ModHit
                {
                    ProjectId = h["project_id"].AsString(""),
                    Slug = h["slug"].AsString(""),
                    Title = h["title"].AsString(""),
                    Description = h["description"].AsString(""),
                    Author = h["author"].AsString(""),
                    IconUrl = h["icon_url"].AsString(""),
                    ProjectType = h["project_type"].AsString(projectType),
                    Downloads = h["downloads"].AsLong(0)
                };
                foreach (var c in h["categories"].Items)
                {
                    string s = c.AsString(null);
                    if (s != null) hit.Categories.Add(s);
                }
                list.Add(hit);
            }
            return list;
        }

        public static async Task<List<ModFile>> GetFilesAsync(string projectId, string gameVersion,
                                                              string loader, CancellationToken ct)
        {
            var sb = new StringBuilder(Api + "project/" + Net.UrlEncode(projectId) + "/version");
            var q = new List<string>();
            if (ModJar.LooksLikeGameVersion(gameVersion))
                q.Add("game_versions=" + Net.UrlEncode("[\"" + gameVersion.Trim() + "\"]"));
            if (!string.IsNullOrEmpty(loader))
                q.Add("loaders=" + Net.UrlEncode("[\"" + loader + "\"]"));
            if (q.Count > 0) sb.Append('?').Append(string.Join("&", q.ToArray()));

            var j = await Net.GetJsonAsync(sb.ToString(), ct).ConfigureAwait(false);

            var list = new List<ModFile>();
            foreach (var v in j.Items)
            {
                Json primary = Json.Null;
                foreach (var f in v["files"].Items)
                {
                    if (f["primary"].AsBool(false)) { primary = f; break; }
                    if (!primary.Exists) primary = f;
                }
                if (!primary.IsObject) continue;

                var mf = new ModFile
                {
                    VersionId = v["id"].AsString(""),
                    ProjectId = v["project_id"].AsString(projectId),
                    Name = v["name"].AsString(""),
                    VersionNumber = v["version_number"].AsString(""),
                    Channel = v["version_type"].AsString("release"),
                    FileName = primary["filename"].AsString(""),
                    Url = primary["url"].AsString(""),
                    Sha1 = primary["hashes"]["sha1"].AsString(""),
                    Size = primary["size"].AsLong(0)
                };

                foreach (var d in v["dependencies"].Items)
                {
                    if (d["dependency_type"].AsString("") != "required") continue;
                    string pid = d["project_id"].AsString(null);
                    if (!string.IsNullOrEmpty(pid)) mf.RequiredDependencies.Add(pid);
                }

                list.Add(mf);
            }
            return list;
        }

        /// <summary>Скачивает файл мода в нужную папку версии.</summary>
        public static async Task<string> DownloadAsync(ModFile file, string projectType, string versionId,
                                                       CancellationToken ct)
        {
            string dir = TargetDir(projectType, versionId);
            Paths.Ensure(dir);

            string target = Path.Combine(dir, file.FileName);
            await Net.DownloadFileAsync(file.Url, target, null, ct).ConfigureAwait(false);

            Log.Info("Установлено: " + file.FileName + " -> " + dir);
            return target;
        }

        public static string TargetDir(string projectType, string versionId)
        {
            switch (projectType)
            {
                case "resourcepack": return Paths.ResourcePacksDir(versionId);
                case "shader": return Paths.ShaderPacksDir(versionId);
                case "datapack": return Path.Combine(Paths.InstanceDir(versionId), "datapacks");
                default: return Paths.ModsDir(versionId);
            }
        }

        /// <summary>Ставит мод вместе с обязательными зависимостями.</summary>
        public static async Task<List<string>> InstallWithDependenciesAsync(
            ModFile file, string projectType, string versionId, string gameVersion, string loader,
            Action<string> status, CancellationToken ct)
        {
            var installed = new List<string>();
            var queue = new Queue<ModFile>();
            var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            queue.Enqueue(file);

            while (queue.Count > 0)
            {
                var f = queue.Dequeue();
                if (!done.Add(f.ProjectId + "/" + f.VersionId)) continue;

                if (status != null) status("Скачиваю " + f.FileName + "…");
                await DownloadAsync(f, projectType, versionId, ct).ConfigureAwait(false);
                installed.Add(f.FileName);

                foreach (var dep in f.RequiredDependencies)
                {
                    if (done.Contains(dep)) continue;
                    try
                    {
                        var depFiles = await GetFilesAsync(dep, gameVersion, loader, ct).ConfigureAwait(false);
                        if (depFiles.Count > 0)
                        {
                            done.Add(dep);
                            queue.Enqueue(depFiles[0]);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("Зависимость " + dep + " не поставлена: " + ex.Message);
                    }
                }
            }

            return installed;
        }
    }

    // =====================================================================
    //  Источники модов. Выбираются на вкладке «Моды и паки», как источник
    //  пакетов в NuGet: переключили — каталог подгрузился сам.
    // =====================================================================

    public interface IModSource
    {
        string Id { get; }
        string Title { get; }

        /// <summary>Готов ли источник к работе; иначе problem — что нужно сделать.</summary>
        bool Ready(out string problem);

        /// <summary>Поддерживает ли источник такой тип содержимого.</summary>
        bool Supports(string projectType);

        Task<List<ModHit>> SearchAsync(string query, string projectType, string gameVersion,
                                       string loader, int offset, int limit, CancellationToken ct);

        Task<List<ModFile>> GetFilesAsync(ModHit hit, string gameVersion, string loader, CancellationToken ct);

        Task<List<string>> InstallAsync(ModFile file, string projectType, string versionId,
                                        string gameVersion, string loader,
                                        Action<string> status, CancellationToken ct);
    }

    /// <summary>Modrinth — работает без ключей и настроек.</summary>
    public class ModrinthSource : IModSource
    {
        public string Id { get { return "modrinth"; } }
        public string Title { get { return "Modrinth"; } }

        public bool Ready(out string problem) { problem = null; return true; }
        public bool Supports(string projectType) { return true; }

        public async Task<List<ModHit>> SearchAsync(string query, string projectType, string gameVersion,
                                                    string loader, int offset, int limit, CancellationToken ct)
        {
            var hits = await ModrinthApi.SearchAsync(query, projectType, gameVersion,
                                                     ModrinthApi.VersionLoader(projectType, loader),
                                                     offset, limit, ct).ConfigureAwait(false);
            foreach (var h in hits) h.SourceId = Id;
            return hits;
        }

        public Task<List<ModFile>> GetFilesAsync(ModHit hit, string gameVersion, string loader, CancellationToken ct)
        {
            return ModrinthApi.GetFilesAsync(hit.ProjectId, gameVersion,
                                             ModrinthApi.VersionLoader(hit.ProjectType, loader), ct);
        }

        public Task<List<string>> InstallAsync(ModFile file, string projectType, string versionId,
                                               string gameVersion, string loader,
                                               Action<string> status, CancellationToken ct)
        {
            return ModrinthApi.InstallWithDependenciesAsync(file, projectType, versionId, gameVersion,
                                                            ModrinthApi.VersionLoader(projectType, loader),
                                                            status, ct);
        }
    }

    /// <summary>CurseForge. Их API открыт только по ключу — он задаётся в настройках.</summary>
    public class CurseForgeSource : IModSource
    {
        public const string Api = "https://api.curseforge.com/v1/";
        private const int GameMinecraft = 432;

        public string Id { get { return "curseforge"; } }
        public string Title { get { return "CurseForge"; } }

        public bool Ready(out string problem)
        {
            if (string.IsNullOrWhiteSpace(Settings.Current.CurseForgeKey))
            {
                problem = "Для CurseForge нужен ключ API — впишите его в «Настройках».";
                return false;
            }
            problem = null;
            return true;
        }

        public bool Supports(string projectType) { return ClassId(projectType) > 0; }

        private static int ClassId(string projectType)
        {
            switch (projectType)
            {
                case "mod": return 6;
                case "resourcepack": return 12;
                case "shader": return 6552;
                case "datapack": return 6945;
                default: return 0;
            }
        }

        private static int LoaderId(string loader)
        {
            if (string.IsNullOrEmpty(loader)) return 0;
            switch (loader)
            {
                case "forge": return 1;
                case "fabric": return 4;
                case "quilt": return 5;
                case "neoforge": return 6;
                default: return 0;
            }
        }

        private static async Task<Json> CallAsync(string url, CancellationToken ct)
        {
            using (var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url))
            {
                request.Headers.Add("x-api-key", Settings.Current.CurseForgeKey.Trim());
                request.Headers.Add("Accept", "application/json");

                using (var response = await Net.Client.SendAsync(request, ct).ConfigureAwait(false))
                {
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                        throw new Exception("CurseForge ответил " + (int)response.StatusCode +
                                            ((int)response.StatusCode == 403 ? " — ключ API не принят." : ""));
                    return Json.Parse(body);
                }
            }
        }

        public async Task<List<ModHit>> SearchAsync(string query, string projectType, string gameVersion,
                                                    string loader, int offset, int limit, CancellationToken ct)
        {
            var sb = new StringBuilder(Api + "mods/search?gameId=" + GameMinecraft);
            sb.Append("&classId=").Append(ClassId(projectType));
            if (!string.IsNullOrEmpty(gameVersion)) sb.Append("&gameVersion=").Append(Net.UrlEncode(gameVersion));

            int loaderId = projectType == "mod" ? LoaderId(loader) : 0;
            if (loaderId > 0) sb.Append("&modLoaderType=").Append(loaderId);

            if (!string.IsNullOrWhiteSpace(query)) sb.Append("&searchFilter=").Append(Net.UrlEncode(query));
            sb.Append("&sortField=2&sortOrder=desc");
            sb.Append("&index=").Append(offset).Append("&pageSize=").Append(limit);

            var json = await CallAsync(sb.ToString(), ct).ConfigureAwait(false);

            var list = new List<ModHit>();
            foreach (var m in json["data"].Items)
            {
                var hit = new ModHit
                {
                    SourceId = Id,
                    ProjectId = m["id"].AsLong(0).ToString(),
                    Slug = m["slug"].AsString(""),
                    Title = m["name"].AsString(""),
                    Description = m["summary"].AsString(""),
                    IconUrl = m["logo"]["thumbnailUrl"].AsString(""),
                    ProjectType = projectType,
                    Downloads = m["downloadCount"].AsLong(0)
                };

                var authors = m["authors"];
                if (authors.Count > 0) hit.Author = authors[0]["name"].AsString("");

                list.Add(hit);
            }
            return list;
        }

        public async Task<List<ModFile>> GetFilesAsync(ModHit hit, string gameVersion, string loader,
                                                       CancellationToken ct)
        {
            var sb = new StringBuilder(Api + "mods/" + hit.ProjectId + "/files?pageSize=30");
            if (!string.IsNullOrEmpty(gameVersion)) sb.Append("&gameVersion=").Append(Net.UrlEncode(gameVersion));

            int loaderId = hit.ProjectType == "mod" ? LoaderId(loader) : 0;
            if (loaderId > 0) sb.Append("&modLoaderType=").Append(loaderId);

            var json = await CallAsync(sb.ToString(), ct).ConfigureAwait(false);

            var list = new List<ModFile>();
            foreach (var f in json["data"].Items)
            {
                string url = f["downloadUrl"].AsString(null);
                if (string.IsNullOrEmpty(url)) continue;   // автор запретил стороннюю раздачу

                var file = new ModFile
                {
                    VersionId = f["id"].AsLong(0).ToString(),
                    ProjectId = hit.ProjectId,
                    Name = f["displayName"].AsString(""),
                    VersionNumber = f["displayName"].AsString(""),
                    FileName = f["fileName"].AsString(""),
                    Url = url,
                    Size = f["fileLength"].AsLong(0),
                    Channel = f["releaseType"].AsInt(1) == 1 ? "release" : "beta"
                };

                foreach (var h in f["hashes"].Items)
                    if (h["algo"].AsInt(0) == 1) file.Sha1 = h["value"].AsString("");

                foreach (var d in f["dependencies"].Items)
                    if (d["relationType"].AsInt(0) == 3)                   // 3 = обязательная
                        file.RequiredDependencies.Add(d["modId"].AsLong(0).ToString());

                list.Add(file);
            }
            return list;
        }

        public async Task<List<string>> InstallAsync(ModFile file, string projectType, string versionId,
                                                     string gameVersion, string loader,
                                                     Action<string> status, CancellationToken ct)
        {
            var installed = new List<string>();

            if (status != null) status("Скачиваю " + file.FileName + "…");
            await ModrinthApi.DownloadAsync(file, projectType, versionId, ct).ConfigureAwait(false);
            installed.Add(file.FileName);

            foreach (var dep in file.RequiredDependencies)
            {
                try
                {
                    var depHit = new ModHit { ProjectId = dep, ProjectType = projectType, SourceId = Id };
                    var depFiles = await GetFilesAsync(depHit, gameVersion, loader, ct).ConfigureAwait(false);
                    if (depFiles.Count == 0) continue;

                    if (status != null) status("Зависимость: " + depFiles[0].FileName + "…");
                    await ModrinthApi.DownloadAsync(depFiles[0], projectType, versionId, ct).ConfigureAwait(false);
                    installed.Add(depFiles[0].FileName);
                }
                catch (Exception ex) { Log.Warn("Зависимость " + dep + " не поставлена: " + ex.Message); }
            }

            return installed;
        }
    }

    /// <summary>Папка на диске: свои скачанные моды ставятся тем же способом.</summary>
    public class LocalFolderSource : IModSource
    {
        public string Id { get { return "local"; } }
        public string Title { get { return "Папка на диске"; } }

        public bool Ready(out string problem)
        {
            string dir = Settings.Current.LocalModsFolder;
            if (string.IsNullOrWhiteSpace(dir) || !System.IO.Directory.Exists(dir))
            {
                problem = "Укажите папку с модами в «Настройках».";
                return false;
            }
            problem = null;
            return true;
        }

        public bool Supports(string projectType) { return true; }

        public Task<List<ModHit>> SearchAsync(string query, string projectType, string gameVersion,
                                              string loader, int offset, int limit, CancellationToken ct)
        {
            var list = new List<ModHit>();
            string dir = Settings.Current.LocalModsFolder;

            if (!string.IsNullOrWhiteSpace(dir) && System.IO.Directory.Exists(dir))
            {
                string mask = projectType == "mod" ? "*.jar" : "*.zip";
                var files = System.IO.Directory.GetFiles(dir, mask);
                Array.Sort(files);

                foreach (var f in files)
                {
                    string name = System.IO.Path.GetFileName(f);
                    if (!string.IsNullOrWhiteSpace(query) &&
                        name.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) < 0) continue;

                    var info = new System.IO.FileInfo(f);
                    list.Add(new ModHit
                    {
                        SourceId = Id,
                        ProjectId = f,
                        Title = System.IO.Path.GetFileNameWithoutExtension(f),
                        Description = (info.Length / 1024) + " КБ · " + info.LastWriteTime.ToString("dd.MM.yyyy"),
                        Author = dir,
                        ProjectType = projectType
                    });
                }
            }

            return Task.FromResult(list);
        }

        public Task<List<ModFile>> GetFilesAsync(ModHit hit, string gameVersion, string loader, CancellationToken ct)
        {
            var list = new List<ModFile>();
            if (System.IO.File.Exists(hit.ProjectId))
            {
                list.Add(new ModFile
                {
                    ProjectId = hit.ProjectId,
                    Name = hit.Title,
                    VersionNumber = "файл",
                    FileName = System.IO.Path.GetFileName(hit.ProjectId),
                    LocalPath = hit.ProjectId,
                    Size = new System.IO.FileInfo(hit.ProjectId).Length
                });
            }
            return Task.FromResult(list);
        }

        public Task<List<string>> InstallAsync(ModFile file, string projectType, string versionId,
                                               string gameVersion, string loader,
                                               Action<string> status, CancellationToken ct)
        {
            string target = ModrinthApi.TargetDir(projectType, versionId);
            Paths.Ensure(target);

            if (status != null) status("Копирую " + file.FileName + "…");
            System.IO.File.Copy(file.LocalPath,
                                System.IO.Path.Combine(target, file.FileName), true);

            return Task.FromResult(new List<string> { file.FileName });
        }
    }

    public static class ModSources
    {
        public static readonly IModSource[] All =
        {
            new ModrinthSource(),
            new CurseForgeSource(),
            new GitHubSource(),
            new NareCatalogSource(),
            new DirectUrlSource(),
            new LocalFolderSource()
        };

        public static IModSource ById(string id)
        {
            foreach (var s in All)
                if (s.Id == id) return s;
            return All[0];
        }
    }
}
