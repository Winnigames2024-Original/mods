using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;

namespace NareLauncher.Core
{
    /// <summary>
    /// Скачивает серверный jar по версии и типу ядра:
    ///   vanilla — Mojang, paper — papermc.io, fabric — meta.fabricmc.net
    ///   (готовый server-launcher jar), forge — установщик Forge с maven,
    ///   запускается --installServer, и пишется nare_launch.txt с аргументами.
    /// </summary>
    public static class ServerJar
    {
        /// <summary>type: vanilla|paper|fabric|forge. Бросает исключение при неудаче.</summary>
        public static void Download(string type, string version, string folder, Action<string> log)
        {
            Directory.CreateDirectory(folder);
            string launchMarker = Path.Combine(folder, "nare_launch.txt");
            try { if (File.Exists(launchMarker)) File.Delete(launchMarker); } catch { }

            if (type == "forge")
            {
                InstallForge(version, folder, log);
            }
            else
            {
                string dest = Path.Combine(folder, "server.jar");
                string url;
                if (type == "paper") url = PaperUrl(version, log);
                else if (type == "fabric") url = FabricUrl(version, log);
                else url = VanillaUrl(version, log);

                if (string.IsNullOrEmpty(url)) throw new Exception("не нашёл ссылку на jar для версии " + version);
                if (log != null) log("Скачиваю: " + url);
                DownloadFile(url, dest);
                if (log != null) log("Готово: server.jar (" + (new FileInfo(dest).Length / 1024 / 1024) + " МБ)");
            }
            try { File.WriteAllText(Path.Combine(folder, "nare_core.txt"), type + "|" + version); } catch { }
        }

        // -------------------- Fabric --------------------

        private static string FabricUrl(string version, Action<string> log)
        {
            string loaders = Get("https://meta.fabricmc.net/v2/versions/loader/" + version);
            var la = Json.Parse(loaders);
            string loader = null;
            foreach (var it in la.Items)
            {
                if (it["loader"]["stable"].AsBool(false)) { loader = it["loader"]["version"].AsString(""); break; }
            }
            if (string.IsNullOrEmpty(loader))
                foreach (var it in la.Items) { loader = it["loader"]["version"].AsString(""); break; }
            if (string.IsNullOrEmpty(loader)) return null;

            string insts = Get("https://meta.fabricmc.net/v2/versions/installer");
            var ia = Json.Parse(insts);
            string installer = null;
            foreach (var it in ia.Items)
            {
                if (it["stable"].AsBool(false)) { installer = it["version"].AsString(""); break; }
            }
            if (string.IsNullOrEmpty(installer))
                foreach (var it in ia.Items) { installer = it["version"].AsString(""); break; }
            if (string.IsNullOrEmpty(installer)) return null;

            if (log != null) log("Fabric " + version + " · loader " + loader + " · installer " + installer);
            return "https://meta.fabricmc.net/v2/versions/loader/" + version + "/" + loader + "/" + installer + "/server/jar";
        }

        // -------------------- Forge --------------------

        private static void InstallForge(string mc, string folder, Action<string> log)
        {
            string promos = Get("https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json");
            var pj = Json.Parse(promos)["promos"];
            string forgeVer = pj[mc + "-recommended"].AsString("");
            if (string.IsNullOrEmpty(forgeVer)) forgeVer = pj[mc + "-latest"].AsString("");
            if (string.IsNullOrEmpty(forgeVer)) throw new Exception("Forge для " + mc + " не найден");

            string full = mc + "-" + forgeVer;
            string installerUrl = "https://maven.minecraftforge.net/net/minecraftforge/forge/" + full +
                                  "/forge-" + full + "-installer.jar";
            string installer = Path.Combine(folder, "forge-installer.jar");
            if (log != null) log("Forge " + full + " — качаю установщик...");
            DownloadFile(installerUrl, installer);

            if (log != null) log("Устанавливаю сервер Forge (может занять минуту)...");
            string java = ServerManager.JavaExe();
            RunJava(folder, "-jar \"forge-installer.jar\" --installServer", java, log);

            // найти аргументный файл (win_args.txt) и собрать команду запуска
            string argsRel = FindArgsFile(folder);
            if (string.IsNullOrEmpty(argsRel))
                throw new Exception("Forge установлен, но не нашёл win_args.txt — возможно, это старая версия Forge");

            var sb = new StringBuilder();
            if (File.Exists(Path.Combine(folder, "user_jvm_args.txt"))) sb.Append("@user_jvm_args.txt ");
            sb.Append("@").Append(argsRel.Replace('\\', '/')).Append(" nogui");
            File.WriteAllText(Path.Combine(folder, "nare_launch.txt"), sb.ToString());

            try { File.Delete(installer); } catch { }
            try { File.Delete(Path.Combine(folder, "forge-installer.jar.log")); } catch { }
            if (log != null) log("Forge готов. Жми «Запустить».");
        }

        private static string FindArgsFile(string folder)
        {
            string baseDir = Path.Combine(folder, "libraries", "net", "minecraftforge", "forge");
            if (!Directory.Exists(baseDir)) return null;
            foreach (var f in Directory.GetFiles(baseDir, "win_args.txt", SearchOption.AllDirectories))
            {
                string rel = f.Substring(folder.Length).TrimStart('\\', '/');
                return rel;
            }
            return null;
        }

        private static void RunJava(string folder, string args, string java, Action<string> log)
        {
            var psi = new ProcessStartInfo
            {
                FileName = java,
                Arguments = args,
                WorkingDirectory = folder,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            var pr = new Process { StartInfo = psi };
            pr.OutputDataReceived += (s, e) => { if (e.Data != null && log != null) log(e.Data); };
            pr.ErrorDataReceived += (s, e) => { if (e.Data != null && log != null) log(e.Data); };
            pr.Start();
            pr.BeginOutputReadLine();
            pr.BeginErrorReadLine();
            pr.WaitForExit();
            if (pr.ExitCode != 0) throw new Exception("установщик Forge вернул код " + pr.ExitCode);
        }

        // -------------------- Paper / Vanilla --------------------

        private static string PaperUrl(string version, Action<string> log)
        {
            // Новый Fill API v3 (старый v2 отключён — отдаёт 410).
            string json = Get("https://fill.papermc.io/v3/projects/paper/versions/" + version + "/builds");
            var root = Json.Parse(json);   // массив билдов, новейшие сверху
            long best = -1, bestAny = -1;
            string url = null, urlAny = null;
            foreach (var b in root.Items)
            {
                long id = b["id"].AsLong(0);
                string dl = b["downloads"]["server:default"]["url"].AsString("");
                if (string.IsNullOrEmpty(dl)) continue;
                if (id > bestAny) { bestAny = id; urlAny = dl; }
                if (b["channel"].AsString("") == "STABLE" && id > best) { best = id; url = dl; }
            }
            if (string.IsNullOrEmpty(url)) { url = urlAny; best = bestAny; }
            if (string.IsNullOrEmpty(url)) return null;
            if (log != null) log("Paper " + version + ", сборка " + best);
            return url;
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

        // -------------------- helpers --------------------

        private static void DownloadFile(string url, string dest)
        {
            using (var wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", "NareLauncher");
                wc.DownloadFile(url, dest);
            }
        }

        private static string Get(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "NareLauncher";
            req.Timeout = 20000;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return rd.ReadToEnd();
        }
    }
}
