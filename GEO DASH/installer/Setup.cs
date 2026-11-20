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

        /// <summary>Сайт игры — там файлы всегда свежие, без кэша CDN.</summary>
        public const string SiteBase = "https://geodash.winnigames2024.ru";

        public static string UpdateJsonUrl => RawBase + "/update.json";
        public static string ExeUrl => RawBase + "/download/GEO_DASH.exe";

        public static string SiteStatusUrl => SiteBase + "/api.php?action=status";
        public static string SiteExeUrl => SiteBase + "/download/GEO_DASH.exe";

        /// <summary>Метка «поставлено установщиком» рядом с игрой: без неё игра просит ключ.</summary>
        public static string InstallMark(string dir) => Path.Combine(dir, "install.json");

        public const string InstallerMark = "GEO DASH Setup";

        /// <summary>Записать метку установки и ключ, который ввели в установщике.</summary>
        public static void WriteMark(string dir, string version, string key)
        {
            try
            {
                var o = new
                {
                    installedBy = InstallerMark,
                    version = version ?? "",
                    at = DateTime.Now.ToString("s"),
                    key = key ?? ""
                };
                File.WriteAllText(InstallMark(dir),
                    JsonSerializer.Serialize(o, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        /// <summary>Ключ, который уже лежит рядом с игрой (при переустановке).</summary>
        public static string ReadMarkKey(string dir)
        {
            try
            {
                var p = InstallMark(dir);
                if (!File.Exists(p)) return "";
                using var d = JsonDocument.Parse(File.ReadAllText(p));
                return d.RootElement.TryGetProperty("key", out var k) ? (k.GetString() ?? "") : "";
            }
            catch { return ""; }
        }

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

    /// <summary>Лицензионный ключ: тот же алфавит и та же контрольная сумма, что в игре.</summary>
    public static class Lic
    {
        const string Alpha = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(16);
            foreach (var ch in s.ToUpperInvariant())
            {
                char c = ch;
                if (c == 'O') c = '0';
                else if (c == 'I' || c == 'L') c = '1';
                else if (c == 'U') c = 'V';
                if (Alpha.IndexOf(c) >= 0) sb.Append(c);
            }
            var t = sb.ToString();
            if (t.StartsWith("GD") && t.Length - 2 >= 16) t = t.Substring(2);
            return t.Length > 16 ? t.Substring(0, 16) : t;
        }

        static char CheckChar(string body)
        {
            int sum = 0;
            for (int i = 0; i < body.Length; i++)
            {
                int v = Alpha.IndexOf(body[i]);
                sum += (v < 0 ? 0 : v) * (i + 2);
            }
            return Alpha[sum % 32];
        }

        public static bool WellFormed(string s)
        {
            var raw = Normalize(s);
            return raw.Length == 16 && CheckChar(raw.Substring(0, 15)) == raw[15];
        }

        public static string Pretty(string s)
        {
            var raw = Normalize(s);
            if (raw.Length == 0) return "";
            var sb = new System.Text.StringBuilder("GD");
            for (int i = 0; i < raw.Length; i++)
            {
                if (i % 4 == 0) sb.Append('-');
                sb.Append(raw[i]);
            }
            return sb.ToString();
        }
    }

    /// <summary>Скачивание и сравнение версий — общее для установщика и службы.</summary>
    public static class Net
    {
        static readonly HttpClient Http = new(new HttpClientHandler { UseProxy = false })
        { Timeout = TimeSpan.FromMinutes(10) };

        public static async Task<UpdateInfo> CheckAsync()
        {
            // сначала сайт: там версия обновляется в тот же миг, что и файлы
            try
            {
                using var d = JsonDocument.Parse(await Http.GetStringAsync(Cfg.SiteStatusUrl));
                var root = d.RootElement;
                if (root.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String)
                {
                    var info = new UpdateInfo { version = v.GetString() ?? "", exe = Cfg.SiteExeUrl };
                    if (root.TryGetProperty("builds", out var b) &&
                        b.TryGetProperty("exe", out var e) && e.ValueKind == JsonValueKind.Object &&
                        e.TryGetProperty("updated", out var up))
                        info.date = up.GetString() ?? "";
                    return info;
                }
            }
            catch { }

            try { return JsonSerializer.Deserialize<UpdateInfo>(await Http.GetStringAsync(Cfg.UpdateJsonUrl)); }
            catch { return null; }
        }

        /// <summary>Откуда качать игру: сайт, адрес из update.json, GitHub — по очереди.</summary>
        public static string[] GameUrls(UpdateInfo info)
        {
            var list = new List<string> { Cfg.SiteExeUrl };
            if (info != null && !string.IsNullOrEmpty(info.exe) && !list.Contains(info.exe)) list.Add(info.exe);
            // у GitHub свой кэш — добавляем метку времени, чтобы отдал свежий файл
            list.Add(Cfg.ExeUrl + "?v=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            return list.ToArray();
        }

        /// <summary>Пробуем адреса по порядку, пока файл не скачается.</summary>
        public static async Task<bool> DownloadFirstAsync(string[] urls, string path,
                                                          IProgress<string> log = null, IProgress<double> pct = null)
        {
            foreach (var u in urls)
            {
                if (await DownloadAsync(u, path, log, pct)) return true;
                log?.Report("Пробую другой адрес…");
            }
            return false;
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

        /// <summary>Интернет есть? Спрашиваем сайт, а если он молчит — GitHub.</summary>
        public static async Task<bool> OnlineAsync()
        {
            foreach (var url in new[] { Cfg.SiteStatusUrl, Cfg.UpdateJsonUrl })
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
                    using var r = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                    if (r.IsSuccessStatusCode) return true;
                }
                catch { }
            }
            return false;
        }
    }
}
