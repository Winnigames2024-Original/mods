using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace GeoDashSetup
{
    /// <summary>Общие настройки установщика и службы обновления.</summary>
    public static class Cfg
    {
        public const string AppName = "GEO DASH";
        public const string Brand = "Winnigames2024";
        public const string ServiceName = "GeoDashUpdate";
        public const string ServiceTitle = "GEO DASH — автообновление";
        public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\GeoDash";

        public const string RawBase =
            "https://raw.githubusercontent.com/Winnigames2024-Original/mods/main/GEO%20DASH";

        public static string UpdateJsonUrl => RawBase + "/update.json";
        public static string ExeUrl => RawBase + "/download/GEO_DASH.exe";

        public static string DefaultDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Brand, AppName);

        public static string GameExe(string dir) => Path.Combine(dir, "GEO DASH.exe");
        public static string SetupExe(string dir) => Path.Combine(dir, "GEO DASH Setup.exe");

        /// <summary>Куда служба пишет, что делала.</summary>
        public static string LogPath(string dir) => Path.Combine(dir, "update.log");

        /// <summary>Отдельная качалка обновлений рядом с игрой.</summary>
        public static string AutoUpdateExe(string dir) => Path.Combine(dir, "autoupdate.exe");

        /// <summary>
        /// Версия, которая сейчас стоит. Лежит в version.txt рядом с игрой:
        /// если в update.json версия больше — качаем новую.
        /// </summary>
        public static string VersionTxt(string dir) => Path.Combine(dir, "version.txt");

        public static string ReadVersion(string dir)
        {
            try
            {
                var p = VersionTxt(dir);
                if (File.Exists(p))
                {
                    var t = File.ReadAllText(p).Trim();
                    if (t.Length > 0) return t;
                }
            }
            catch { }
            return null;
        }

        public static void WriteVersion(string dir, string v)
        {
            try { File.WriteAllText(VersionTxt(dir), (v ?? "").Trim()); } catch { }
        }
    }

    /// <summary>Данные из update.json.</summary>
    public class UpdateInfo
    {
        public string version { get; set; } = "";
        public string exe { get; set; } = "";
        public string notes { get; set; } = "";
        public string date { get; set; } = "";
    }

    /// <summary>Скачивание и сравнение версий — общее для установщика и службы.</summary>
    public static class Net
    {
        static readonly HttpClient Http = new(new HttpClientHandler { UseProxy = false })
        { Timeout = TimeSpan.FromMinutes(10) };

        public static async Task<UpdateInfo> CheckAsync()
        {
            try { return JsonSerializer.Deserialize<UpdateInfo>(await Http.GetStringAsync(Cfg.UpdateJsonUrl)); }
            catch { return null; }
        }

        public static async Task<bool> DownloadAsync(string url, string path,
                                                     IProgress<string> log = null, IProgress<double> pct = null)
        {
            try
            {
                using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (!resp.IsSuccessStatusCode)
                {
                    log?.Report("Сервер ответил " + (int)resp.StatusCode);
                    return false;
                }
                long total = resp.Content.Headers.ContentLength ?? 0, done = 0;
                var tmp = path + ".part";
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                using (var src = await resp.Content.ReadAsStreamAsync())
                using (var dst = File.Create(tmp))
                {
                    var buf = new byte[256 * 1024];
                    int n;
                    while ((n = await src.ReadAsync(buf)) > 0)
                    {
                        await dst.WriteAsync(buf.AsMemory(0, n));
                        done += n;
                        if (total > 0) pct?.Report(done * 100.0 / total);
                    }
                }
                if (new FileInfo(tmp).Length < 1024 * 512) { File.Delete(tmp); return false; }
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                return true;
            }
            catch (Exception ex)
            {
                log?.Report("Ошибка загрузки: " + ex.Message);
                return false;
            }
        }

        /// <summary>Версия a новее версии b.</summary>
        public static bool IsNewer(string a, string b)
        {
            try { return Version.Parse(Norm(a)) > Version.Parse(Norm(b)); }
            catch { return !string.IsNullOrEmpty(a) && a != b; }
        }

        static string Norm(string v)
        {
            var p = (v ?? "").Trim().TrimStart('v', 'V').Split('.').ToList();
            while (p.Count < 3) p.Add("0");
            return string.Join('.', p.Take(4));
        }

        /// <summary>
        /// Версия установленной игры: сначала version.txt рядом с игрой,
        /// иначе — версия самого exe.
        /// </summary>
        public static string LocalVersion(string dir)
        {
            var txt = Cfg.ReadVersion(dir);
            if (!string.IsNullOrEmpty(txt)) return txt;
            try
            {
                var p = Cfg.GameExe(dir);
                if (!File.Exists(p)) return "0.0.0";
                return FileVersionInfo.GetVersionInfo(p).FileVersion ?? "0.0.0";
            }
            catch { return "0.0.0"; }
        }

        /// <summary>Интернет есть? Проверяем сам сервер обновлений.</summary>
        public static async Task<bool> OnlineAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
                using var r = await Http.GetAsync(Cfg.UpdateJsonUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                return r.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }
}
