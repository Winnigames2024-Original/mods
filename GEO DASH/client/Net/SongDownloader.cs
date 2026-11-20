using System.Net.Http;
using System.Text.RegularExpressions;
using GEO_DASH.Audio;

namespace GEO_DASH.Net
{
    /// <summary>
    /// Скачивание треков с Newgrounds по номеру, как это делает сама игра.
    /// Порядок попыток: уже скачанный файл → свой сервер → напрямую с Newgrounds.
    /// Если ничего не вышло, трек можно выбрать файлом вручную.
    /// </summary>
    public static class SongDownloader
    {
        static readonly HttpClient Http = CreateClient();

        static HttpClient CreateClient()
        {
            var c = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
                "Chrome/126.0 Safari/537.36");
            c.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://www.newgrounds.com/");
            c.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
            return c;
        }

        /// <summary>Папка со скачанными треками.</summary>
        public static string SongsDir => AppConfig.MusicDir;

        /// <summary>Путь, по которому лежит трек с этим номером.</summary>
        public static string PathOf(int id) => Path.Combine(SongsDir, id + ".mp3");

        /// <summary>Трек уже скачан и файл не пустой.</summary>
        public static bool Have(int id)
        {
            try
            {
                var p = PathOf(id);
                return File.Exists(p) && new FileInfo(p).Length > 1024;
            }
            catch { return false; }
        }

        /// <summary>Последняя ошибка — её показываем игроку.</summary>
        public static string LastError { get; private set; } = "";

        /// <summary>
        /// Достать трек. Вернёт путь к mp3 или null.
        /// progress — проценты для шкалы, log — короткие сообщения о ходе дела.
        /// </summary>
        public static async Task<string> GetAsync(int id, IProgress<int> progress = null,
                                                  IProgress<string> log = null,
                                                  CancellationToken ct = default)
        {
            LastError = "";
            if (id <= 0) { LastError = "Неверный номер трека"; return null; }

            if (Have(id)) { progress?.Report(100); log?.Report("Трек уже скачан"); return PathOf(id); }

            Directory.CreateDirectory(SongsDir);

            // 1. свой сервер: он уже умеет отдавать треки по номеру
            log?.Report("Спрашиваю у сервера…");
            try
            {
                var viaServer = await MusicPlayer.EnsureAsync(id, log);
                if (viaServer != null && Have(id)) { progress?.Report(100); return PathOf(id); }
            }
            catch { }

            // 2. напрямую с Newgrounds
            log?.Report("Ищу трек на Newgrounds…");
            string direct = await FindMp3Async(id, ct);
            if (direct == null)
            {
                if (string.IsNullOrEmpty(LastError)) LastError = "Не нашёл файл трека на странице";
                return null;
            }

            log?.Report("Качаю…");
            return await DownloadAsync(direct, PathOf(id), progress, ct) ? PathOf(id) : null;
        }

        /// <summary>Найти прямую ссылку на mp3 на странице трека.</summary>
        static async Task<string> FindMp3Async(int id, CancellationToken ct)
        {
            foreach (var page in new[]
                     {
                         $"https://www.newgrounds.com/audio/listen/{id}",
                         $"https://newgrounds.com/audio/listen/{id}"
                     })
            {
                try
                {
                    using var resp = await Http.GetAsync(page, ct);
                    if (!resp.IsSuccessStatusCode)
                    {
                        LastError = "Newgrounds ответил " + (int)resp.StatusCode;
                        continue;
                    }

                    string html = await resp.Content.ReadAsStringAsync(ct);

                    // ссылка встречается и как обычный адрес, и внутри JSON плеера
                    var m = Regex.Match(html,
                        @"https?:\\?/\\?/audio\.ngfiles\.com\\?/[^""'\\\s]+\.mp3[^""'\s]*",
                        RegexOptions.IgnoreCase);
                    if (!m.Success)
                        m = Regex.Match(html, @"https?://[^""'\s]*ngfiles\.com[^""'\s]+\.mp3[^""'\s]*",
                                        RegexOptions.IgnoreCase);
                    if (!m.Success) continue;

                    string url = m.Value.Replace("\\/", "/").Replace("\\u0026", "&").Replace("&amp;", "&");
                    return url;
                }
                catch (Exception ex) { LastError = ex.Message; }
            }
            return null;
        }

        /// <summary>Потоковое скачивание с процентами.</summary>
        static async Task<bool> DownloadAsync(string url, string path, IProgress<int> progress,
                                              CancellationToken ct)
        {
            try
            {
                using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    LastError = resp.StatusCode == System.Net.HttpStatusCode.Forbidden
                        ? "Newgrounds закрыл доступ (403)"
                        : "Сервер ответил " + (int)resp.StatusCode;
                    return false;
                }

                long total = resp.Content.Headers.ContentLength ?? 0, done = 0;
                int lastPct = -1;
                string tmp = path + ".part";

                using (var src = await resp.Content.ReadAsStreamAsync(ct))
                using (var dst = File.Create(tmp))
                {
                    var buf = new byte[128 * 1024];
                    int n;
                    while ((n = await src.ReadAsync(buf, ct)) > 0)
                    {
                        await dst.WriteAsync(buf.AsMemory(0, n), ct);
                        done += n;
                        if (total <= 0) continue;
                        int pct = (int)(done * 100 / total);
                        if (pct == lastPct) continue;     // проценты шлём только когда изменились
                        lastPct = pct;
                        progress?.Report(pct);
                    }
                }

                if (new FileInfo(tmp).Length < 1024) { File.Delete(tmp); LastError = "Пришёл пустой файл"; return false; }
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                progress?.Report(100);
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                try { if (File.Exists(path + ".part")) File.Delete(path + ".part"); } catch { }
                return false;
            }
        }

        /// <summary>
        /// Запасной путь: выбрать уже скачанный mp3 вручную.
        /// Файл копируется в папку треков под нужным номером.
        /// </summary>
        public static string PickManually(IWin32Window owner, int id)
        {
            try
            {
                using var d = new OpenFileDialog
                {
                    Title = "Выбери файл трека #" + id,
                    Filter = "Музыка (*.mp3;*.wav;*.ogg)|*.mp3;*.wav;*.ogg"
                };
                if (d.ShowDialog(owner) != DialogResult.OK) return null;

                Directory.CreateDirectory(SongsDir);
                string dst = PathOf(id);
                File.Copy(d.FileName, dst, true);
                return Have(id) ? dst : null;
            }
            catch (Exception ex) { LastError = ex.Message; return null; }
        }
    }
}
