using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace NareLauncher.Core
{
    /// <summary>
    /// Самообновление лаунчера. Скачиваем новый exe, проверяем его по sha1 и
    /// подменяем через маленький .bat: запущенный exe сам себя переписать не
    /// может, поэтому bat ждёт выхода процесса, копирует файл и запускает
    /// снова. Ничего в фоне не остаётся — bat удаляет сам себя.
    /// </summary>
    public static class SelfUpdate
    {
        public const string ManifestUrl =
            "https://raw.githubusercontent.com/Winnigames2024-Original/mods/main/narelauncher/update.json";

        public class Info
        {
            public string Version = "";
            public string Url = "";
            public string Sha1 = "";
            public long Size;
            public string Notes = "";
        }

        public static string CurrentVersion
        {
            get
            {
                try
                {
                    var v = Assembly.GetExecutingAssembly().GetName().Version;
                    return v.Major + "." + v.Minor + (v.Build > 0 ? "." + v.Build : "");
                }
                catch { return "0"; }
            }
        }

        public static async Task<Info> LatestAsync(CancellationToken ct)
        {
            var j = await Net.GetJsonAsync(ManifestUrl + "?t=" + DateTime.UtcNow.Ticks, ct).ConfigureAwait(false);
            return new Info
            {
                Version = j["version"].AsString(""),
                Url = j["url"].AsString(""),
                Sha1 = j["sha1"].AsString(""),
                Size = j["size"].AsLong(0),
                Notes = j["notes"].AsString("")
            };
        }

        /// <summary>Новее ли та версия, что на сервере, чем наша.</summary>
        public static bool IsNewer(string other)
        {
            return Compare(other, CurrentVersion) > 0;
        }

        private static int Compare(string a, string b)
        {
            var x = (a ?? "").Split('.');
            var y = (b ?? "").Split('.');
            int n = Math.Max(x.Length, y.Length);
            for (int i = 0; i < n; i++)
            {
                int xi = i < x.Length ? Num(x[i]) : 0;
                int yi = i < y.Length ? Num(y[i]) : 0;
                if (xi != yi) return xi < yi ? -1 : 1;
            }
            return 0;
        }

        private static int Num(string s)
        {
            int v = 0;
            foreach (char c in s) { if (!char.IsDigit(c)) break; v = v * 10 + (c - '0'); }
            return v;
        }

        /// <summary>
        /// Скачивает новый exe и запускает подмену. После успеха процесс сам
        /// завершится и перезапустится уже обновлённым.
        /// </summary>
        public static async Task ApplyAsync(Info info, Action<string> status, CancellationToken ct)
        {
            if (info == null || string.IsNullOrEmpty(info.Url)) throw new Exception("нет ссылки на обновление");

            string target;
            try { target = Process.GetCurrentProcess().MainModule.FileName; }
            catch { target = Path.Combine(Paths.LauncherDir, "NareLauncher.exe"); }

            string fresh = Path.Combine(Path.GetTempPath(), "NareLauncher.new." + Guid.NewGuid().ToString("N") + ".exe");

            if (status != null) status("Скачиваю новую версию…");
            await Net.DownloadFileAsync(info.Url, fresh, null, ct).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(info.Sha1))
            {
                string real = NareNet.Sha1Of(fresh);
                if (!string.Equals(real, info.Sha1, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(fresh); } catch { }
                    throw new Exception("скачанный файл не сошёлся по sha1");
                }
            }

            if (status != null) status("Применяю обновление…");

            string bat = Path.Combine(Path.GetTempPath(), "nare-update." + Guid.NewGuid().ToString("N") + ".bat");
            string script =
                "@echo off\r\n" +
                "chcp 65001 >nul\r\n" +
                "timeout /t 2 /nobreak >nul\r\n" +
                ":loop\r\n" +
                "move /y \"" + fresh + "\" \"" + target + "\" >nul 2>&1\r\n" +
                "if errorlevel 1 ( timeout /t 1 /nobreak >nul & goto loop )\r\n" +
                "start \"\" \"" + target + "\"\r\n" +
                "del \"%~f0\"\r\n";

            File.WriteAllText(bat, script, new System.Text.UTF8Encoding(false));

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c \"" + bat + "\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Paths.LauncherDir
            };
            Process.Start(psi);

            Log.Info("Запущена подмена exe на версию " + info.Version);
        }
    }
}
