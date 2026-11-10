using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace NareLauncher.Core
{
    public class ModProject
    {
        public string Slug = "", Title = "", Desc = "";
        public int Downloads;
    }

    /// <summary>
    /// Каталог плагинов и модов с Modrinth (публичный бесплатный API).
    /// Поиск по типу (plugin/mod), ядру и версии; прямое скачивание jar.
    /// </summary>
    public static class ModrinthClient
    {
        private const string UA = "NareLauncher/1.0 (minecraft launcher)";

        public static List<ModProject> Search(string query, string projectType, string loaderCat, string mcVersion)
        {
            var list = new List<ModProject>();
            var facets = new List<string>();
            facets.Add("[\"project_type:" + projectType + "\"]");
            if (!string.IsNullOrEmpty(loaderCat)) facets.Add("[\"categories:" + loaderCat + "\"]");
            if (!string.IsNullOrEmpty(mcVersion)) facets.Add("[\"versions:" + mcVersion + "\"]");
            string url = "https://api.modrinth.com/v2/search?limit=40&index=downloads"
                       + "&query=" + Uri.EscapeDataString(query ?? "")
                       + "&facets=" + Uri.EscapeDataString("[" + string.Join(",", facets) + "]");
            string json = Get(url);
            if (json == null) return list;
            var j = Json.Parse(json);
            foreach (var h in j["hits"].Items)
            {
                list.Add(new ModProject
                {
                    Slug = h["slug"].AsString(h["project_id"].AsString("")),
                    Title = h["title"].AsString(""),
                    Desc = h["description"].AsString(""),
                    Downloads = h["downloads"].AsInt(0)
                });
            }
            return list;
        }

        /// <summary>Вернёт {url, filename} для установки или null.</summary>
        public static string[] Resolve(string slug, string[] loaders, string mcVersion)
        {
            string lo = "[";
            for (int i = 0; i < loaders.Length; i++) { if (i > 0) lo += ","; lo += "\"" + loaders[i] + "\""; }
            lo += "]";
            string baseUrl = "https://api.modrinth.com/v2/project/" + Uri.EscapeDataString(slug) + "/version?loaders=" + Uri.EscapeDataString(lo);

            string[] r = Pick(baseUrl + (string.IsNullOrEmpty(mcVersion) ? "" : "&game_versions=" + Uri.EscapeDataString("[\"" + mcVersion + "\"]")));
            if (r != null) return r;
            // запасной вариант — без фильтра по версии
            return Pick(baseUrl);
        }

        private static string[] Pick(string url)
        {
            string json = Get(url);
            if (json == null) return null;
            var arr = Json.Parse(json);
            foreach (var v in arr.Items)
            {
                string[] firstAny = null;
                foreach (var f in v["files"].Items)
                {
                    var pair = new[] { f["url"].AsString(""), f["filename"].AsString("") };
                    if (firstAny == null) firstAny = pair;
                    if (f["primary"].AsBool(false) && pair[0].Length > 0) return pair;
                }
                if (firstAny != null && firstAny[0].Length > 0) return firstAny;
            }
            return null;
        }

        public static void Download(string url, string dest)
        {
            using (var wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", UA);
                wc.DownloadFile(url, dest);
            }
        }

        private static string Get(string url)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = UA;
                req.Timeout = 20000;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    return rd.ReadToEnd();
            }
            catch { return null; }
        }
    }
}
