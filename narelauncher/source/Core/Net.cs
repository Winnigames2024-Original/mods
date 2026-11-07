using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NareLauncher.Core
{
    public class DownloadTask
    {
        public string Url;
        public string Path;
        public string Sha1;
        public long Size;
        public string Title;

        public DownloadTask(string url, string path, string sha1 = null, long size = 0, string title = null)
        {
            Url = url; Path = path; Sha1 = sha1; Size = size;
            Title = title ?? System.IO.Path.GetFileName(path);
        }
    }

    public static class Net
    {
        private static HttpClient _client;
        private static readonly object Lock = new object();

        public static HttpClient Client
        {
            get
            {
                if (_client == null)
                {
                    lock (Lock)
                    {
                        if (_client == null)
                        {
                            try
                            {
                                ServicePointManager.SecurityProtocol =
                                    (SecurityProtocolType)3072 | (SecurityProtocolType)12288; // TLS 1.2 + 1.3
                            }
                            catch
                            {
                                try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { }
                            }
                            ServicePointManager.DefaultConnectionLimit = 64;

                            var handler = new HttpClientHandler();
                            try { handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate; }
                            catch { }

                            _client = new HttpClient(handler);
                            _client.Timeout = TimeSpan.FromMinutes(10);
                            _client.DefaultRequestHeaders.Add("User-Agent", "NareLauncher/1.0 (+windows)");
                        }
                    }
                }
                return _client;
            }
        }

        public static async Task<string> GetStringAsync(string url, CancellationToken ct = default(CancellationToken))
        {
            using (var resp = await Client.GetAsync(url, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }

        public static async Task<Json> GetJsonAsync(string url, CancellationToken ct = default(CancellationToken))
        {
            return Json.Parse(await GetStringAsync(url, ct).ConfigureAwait(false));
        }

        public static async Task<byte[]> GetBytesAsync(string url, CancellationToken ct = default(CancellationToken))
        {
            using (var resp = await Client.GetAsync(url, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }

        public static async Task<Json> PostFormJsonAsync(string url, Dictionary<string, string> form,
                                                         CancellationToken ct = default(CancellationToken))
        {
            using (var content = new FormUrlEncodedContent(form))
            using (var resp = await Client.PostAsync(url, content, ct).ConfigureAwait(false))
            {
                string body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                return Json.Parse(body);
            }
        }

        public static async Task<Json> PostJsonAsync(string url, Json payload,
                                                     CancellationToken ct = default(CancellationToken))
        {
            using (var content = new StringContent(payload.ToJson(false), Encoding.UTF8, "application/json"))
            using (var resp = await Client.PostAsync(url, content, ct).ConfigureAwait(false))
            {
                string body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                return Json.Parse(body);
            }
        }

        /// <summary>Скачивание одного файла во временный файл с последующим переименованием.</summary>
        public static async Task DownloadFileAsync(string url, string path,
                                                   Action<long, long> progress = null,
                                                   CancellationToken ct = default(CancellationToken))
        {
            Paths.EnsureForFile(path);
            string tmp = path + ".part";

            using (var resp = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                long total = resp.Content.Headers.ContentLength.HasValue ? resp.Content.Headers.ContentLength.Value : -1;
                long read = 0;

                using (var src = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var dst = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, true))
                {
                    var buffer = new byte[1 << 16];
                    int n;
                    while ((n = await src.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                    {
                        await dst.WriteAsync(buffer, 0, n, ct).ConfigureAwait(false);
                        read += n;
                        if (progress != null) progress(read, total);
                    }
                }
            }

            if (File.Exists(path))
            {
                try { File.Delete(path); } catch { }
            }
            File.Move(tmp, path);
        }

        public static string Sha1File(string path)
        {
            try
            {
                using (var sha = SHA1.Create())
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16))
                {
                    var hash = sha.ComputeHash(fs);
                    var sb = new StringBuilder(hash.Length * 2);
                    foreach (var b in hash) sb.Append(b.ToString("x2"));
                    return sb.ToString();
                }
            }
            catch { return null; }
        }

        public static string UrlEncode(string s)
        {
            return Uri.EscapeDataString(s ?? "");
        }
    }

    public class DownloadProgress
    {
        public int Done;
        public int Total;
        public string Current = "";
        public long Bytes;
        public string Stage = "";
    }

    /// <summary>Многопоточная очередь загрузок с проверкой SHA-1 и пропуском готовых файлов.</summary>
    public class ParallelDownloader
    {
        private int _done;
        private int _total;
        private long _bytes;
        private string _current = "";
        private readonly List<string> _failed = new List<string>();
        private readonly object _lock = new object();

        public IList<string> Failed { get { return _failed; } }

        public async Task RunAsync(IList<DownloadTask> tasks,
                                   IProgress<DownloadProgress> progress,
                                   CancellationToken ct,
                                   string stage = "")
        {
            _done = 0;
            _total = tasks.Count;
            _failed.Clear();
            if (_total == 0) return;

            int threads = Math.Max(1, Math.Min(Settings.Current.DownloadThreads, _total));
            int index = -1;

            var workers = new Task[threads];
            for (int w = 0; w < threads; w++)
            {
                workers[w] = Task.Run(async delegate
                {
                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();
                        int i = Interlocked.Increment(ref index);
                        if (i >= tasks.Count) break;
                        var t = tasks[i];

                        try
                        {
                            if (!NeedsDownload(t))
                            {
                                Bump(t, progress, stage);
                                continue;
                            }

                            Exception last = null;
                            for (int attempt = 0; attempt < 3; attempt++)
                            {
                                try
                                {
                                    await Net.DownloadFileAsync(t.Url, t.Path, null, ct).ConfigureAwait(false);
                                    last = null;
                                    break;
                                }
                                catch (OperationCanceledException) { throw; }
                                catch (Exception ex)
                                {
                                    last = ex;
                                    await Task.Delay(400 * (attempt + 1), ct).ConfigureAwait(false);
                                }
                            }

                            if (last != null)
                            {
                                lock (_lock) _failed.Add(t.Title + " — " + last.Message);
                                Log.Warn("Не скачано: " + t.Url + " (" + last.Message + ")");
                            }

                            Bump(t, progress, stage);
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex)
                        {
                            lock (_lock) _failed.Add(t.Title + " — " + ex.Message);
                            Bump(t, progress, stage);
                        }
                    }
                }, ct);
            }

            await Task.WhenAll(workers).ConfigureAwait(false);
        }

        private void Bump(DownloadTask t, IProgress<DownloadProgress> progress, string stage)
        {
            int d = Interlocked.Increment(ref _done);
            Interlocked.Add(ref _bytes, t.Size);
            _current = t.Title;
            if (progress != null)
            {
                progress.Report(new DownloadProgress
                {
                    Done = d,
                    Total = _total,
                    Current = _current,
                    Bytes = Interlocked.Read(ref _bytes),
                    Stage = stage
                });
            }
        }

        private static bool NeedsDownload(DownloadTask t)
        {
            if (!File.Exists(t.Path)) return true;
            try
            {
                var fi = new FileInfo(t.Path);
                if (t.Size > 0 && fi.Length != t.Size) return true;
                if (fi.Length == 0) return true;
                if (Settings.Current.VerifyHashes && !string.IsNullOrEmpty(t.Sha1))
                {
                    string h = Net.Sha1File(t.Path);
                    if (h == null) return true;
                    if (!string.Equals(h, t.Sha1, StringComparison.OrdinalIgnoreCase)) return true;
                }
                return false;
            }
            catch { return true; }
        }
    }
}
