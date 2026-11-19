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
        public static async Task<bool> DownloadAndApplyAsync(Info info, IProgress<string> prog = null,
                                                             IProgress<double> percent = null)
        {
            try
            {
                string url = string.IsNullOrEmpty(info.exe)
                    ? AppConfig.RawBase + "/download/GEO_DASH.exe"
                    : info.exe;

                prog?.Report("Скачиваю обновление…");
                string exePath = Environment.ProcessPath ?? Application.ExecutablePath;
                string dir = Path.GetDirectoryName(exePath)!;
                string newPath = Path.Combine(dir, "GEO DASH.new.exe");

                // качаем потоком, чтобы видеть проценты
                using (var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!resp.IsSuccessStatusCode)
                    {
                        prog?.Report("Сервер обновлений ответил " + (int)resp.StatusCode);
                        return false;
                    }
                    long total = resp.Content.Headers.ContentLength ?? 0;
                    long done = 0;
                    var buf = new byte[256 * 1024];

                    await using var src = await resp.Content.ReadAsStreamAsync();
                    await using (var dst = new FileStream(newPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        int n;
                        var last = DateTime.MinValue;
                        while ((n = await src.ReadAsync(buf)) > 0)
                        {
                            await dst.WriteAsync(buf.AsMemory(0, n));
                            done += n;
                            if (total > 0 && (DateTime.UtcNow - last).TotalMilliseconds > 120)
                            {
                                last = DateTime.UtcNow;
                                double k = done / (double)total;
                                percent?.Report(k);
                                prog?.Report($"Скачиваю обновление… {k * 100:0}%  ({done / 1048576.0:0.0} из {total / 1048576.0:0.0} МБ)");
                            }
                        }
                    }
                    percent?.Report(1);
                }

                var fi = new FileInfo(newPath);
                if (!fi.Exists || fi.Length < 1_000_000)
                {
                    prog?.Report("Файл обновления повреждён");
                    try { File.Delete(newPath); } catch { }
                    return false;
                }

                // это точно windows-программа? (подпись MZ в начале файла)
                await using (var check = File.OpenRead(newPath))
                {
                    if (check.ReadByte() != 'M' || check.ReadByte() != 'Z')
                    {
                        prog?.Report("Скачался не тот файл — обновление отменено");
                        check.Close();
                        try { File.Delete(newPath); } catch { }
                        return false;
                    }
                }

                prog?.Report("Готовлю замену файла…");
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
