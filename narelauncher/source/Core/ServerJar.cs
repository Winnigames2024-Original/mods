using System;
using System.IO;
using System.Net;
using System.Text;

namespace NareLauncher.Core
{
    /// <summary>
    /// Скачивает серверный jar: ванильный с Mojang или Paper с papermc.io —
    /// «сам с сайта», по версии. Кладёт как server.jar в папку сервера.
    /// </summary>
    public static class ServerJar
    {
        /// <summary>type: "paper" или "vanilla". Бросает исключение при неудаче.</summary>
        public static void Download(string type, string version, string folder, Action<string> log)
        {
            Directory.CreateDirectory(folder);
            string dest = Path.Combine(folder, "server.jar");
            string url = (type == "paper")
                ? PaperUrl(version, log)
                : VanillaUrl(version, log);
            if (string.IsNullOrEmpty(url)) throw new Exception("не нашёл ссылку на jar для версии " + version);
            if (log != null) log("Скачиваю: " + url);
            using (var wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", "NareLauncher");
                wc.DownloadFile(url, dest);
            }
            if (log != null) log("Готово: " + dest + " (" + (new FileInfo(dest).Length / 1024 / 1024) + " МБ)");
        }

        private static string PaperUrl(string version, Action<string> log)
        {
            string buildsJson = Get("https://api.papermc.io/v2/projects/paper/versions/" + version + "/builds");
            var root = Json.Parse(buildsJson);
            var builds = root["builds"];
            long best = -1;
            string file = null;
            foreach (var b in builds.Items)
            {
                long num = b["build"].AsLong(0);
                if (num >= best)
                {
                    best = num;
                    file = b["downloads"]["application"]["name"].AsString("");
                }
            }
            if (best < 0 || string.IsNullOrEmpty(file)) return null;
            if (log != null) log("Paper " + version + ", сборка " + best);
            return "https://api.papermc.io/v2/projects/paper/versions/" + version +
                   "/builds/" + best + "/downloads/" + file;
        }

        private static string VanillaUrl(string version, Action<string> log)
        {
            string manifest = Get("https://launchermeta.mojang.com/mc/game/version_manifest_v2.json");
            var root = Json.Parse(manifest);
            string verUrl = null;
            foreach (var v in root["versions"].Items)
            {
                if (v["id"].AsString("") == version) { verUrl = v["url"].AsString(""); break; }
            }
            if (string.IsNullOrEmpty(verUrl)) return null;
            string verJson = Get(verUrl);
            var vj = Json.Parse(verJson);
            string serverUrl = vj["downloads"]["server"]["url"].AsString("");
            if (log != null) log("Vanilla " + version);
            return string.IsNullOrEmpty(serverUrl) ? null : serverUrl;
        }

        private static string Get(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "NareLauncher";
            req.Timeout = 15000;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return rd.ReadToEnd();
        }
    }
}
