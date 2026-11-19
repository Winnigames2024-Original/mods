using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace GEO_DASH.Update
{
    /// <summary>Обновление через GitHub (raw + curl): version.json → новый exe → замена при выходе.</summary>
    public static class Updater
    {
        static readonly HttpClient Http = new(new HttpClientHandler { UseProxy = false })
        { Timeout = TimeSpan.FromMinutes(5) };

        public class Info
        {
            public string version { get; set; } = "";
            public string exe { get; set; } = "";
            public string apk { get; set; } = "";
            public string notes { get; set; } = "";
            public string date { get; set; } = "";
        }

        public static string CurrentVersion =>
            Application.ProductVersion.Split('+')[0];

        public static async Task<Info> CheckAsync()
        {
            try
            {
                var url = AppConfig.RawBase + "/update.json";
                var txt = await Http.GetStringAsync(url);
                return JsonSerializer.Deserialize<Info>(txt);
            }
            catch { return null; }
        }

        public static bool IsNewer(string remote, string local)
        {
            if (string.IsNullOrWhiteSpace(remote)) return false;
            try
            {
                var a = Version.Parse(Normalize(remote));
                var b = Version.Parse(Normalize(local));
                return a > b;
            }
            catch { return remote != local; }
        }

        static string Normalize(string v)
        {
            var parts = v.Trim().TrimStart('v', 'V').Split('.');
            while (parts.Length < 3) parts = parts.Append("0").ToArray();
            return string.Join('.', parts.Take(4));
        }

        /// <summary>Качает новый exe рядом и подменяет его батником после выхода.</summary>
        public static async Task<bool> DownloadAndApplyAsync(Info info, IProgress<string> prog = null)
        {
            try
            {
                string url = string.IsNullOrEmpty(info.exe)
                    ? AppConfig.RawBase + "/client/GEO%20DASH.exe"
                    : info.exe;

                prog?.Report("Скачиваю обновление…");
                var bytes = await Http.GetByteArrayAsync(url);
                if (bytes.Length < 100_000) { prog?.Report("Файл обновления повреждён"); return false; }

                string exePath = Environment.ProcessPath ?? Application.ExecutablePath;
                string dir = Path.GetDirectoryName(exePath)!;
                string newPath = Path.Combine(dir, "GEO DASH.new.exe");
                await File.WriteAllBytesAsync(newPath, bytes);

                string bat = Path.Combine(Path.GetTempPath(), "geodash_update.bat");
                await File.WriteAllTextAsync(bat,
                    "@echo off\r\n" +
                    "chcp 65001 >nul\r\n" +
                    "echo Обновление GEO DASH...\r\n" +
                    ":wait\r\n" +
                    "timeout /t 1 /nobreak >nul\r\n" +
                    $"tasklist /fi \"imagename eq {Path.GetFileName(exePath)}\" | find /i \"{Path.GetFileName(exePath)}\" >nul && goto wait\r\n" +
                    $"move /y \"{newPath}\" \"{exePath}\" >nul\r\n" +
                    $"start \"\" \"{exePath}\"\r\n" +
                    "del \"%~f0\"\r\n");

                Process.Start(new ProcessStartInfo
                {
                    FileName = bat,
                    CreateNoWindow = true,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                return true;
            }
            catch (Exception ex)
            {
                prog?.Report("Ошибка обновления: " + ex.Message);
                return false;
            }
        }

        /// <summary>Та же установка, что и на сервере — одной командой curl (для показа пользователю).</summary>
        public static string CurlInstallCommand =>
            $"curl -fsSL \"https://raw.githubusercontent.com/{AppConfig.GitHubOwner}/{AppConfig.GitHubRepo}/main/GEO%20DASH/install.sh\" | sudo bash";
    }
}
