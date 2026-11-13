using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace NareLauncher.Core
{
    /// <summary>
    /// Настоящее видео для эфира: захват экрана/окна через ffmpeg → webm (vp8+opus),
    /// нарезка на init-сегмент и кластеры, отправка чанками на /stream/media/push.
    /// Если ffmpeg не найден — молча не запускается (вызывающий код падает на кадры)
    /// и в фоне один раз скачивает ffmpeg.exe, чтобы в следующий раз было видео.
    /// </summary>
    public sealed class StreamEncoder : IDisposable
    {
        private readonly string _token, _streamId;
        private readonly Action<string> _status;
        private Process _ff;
        private Thread _reader;
        private volatile bool _run;

        public bool Running { get { try { return _ff != null && !_ff.HasExited; } catch { return false; } } }

        public StreamEncoder(string token, string streamId, Action<string> status)
        {
            _token = token; _streamId = streamId; _status = status ?? delegate { };
        }

        // --- где лежит ffmpeg.exe (рядом с лаунчером, в LocalAppData или в PATH) ---
        public static string LocalFfmpegPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "NareLauncher", "ffmpeg.exe");
        }
        public static string FindFfmpeg()
        {
            try
            {
                string dir = Path.GetDirectoryName(Application.ExecutablePath);
                string near = Path.Combine(dir, "ffmpeg.exe");
                if (File.Exists(near)) return near;
            }
            catch { }
            try { string la = LocalFfmpegPath(); if (File.Exists(la)) return la; } catch { }
            try
            {
                foreach (var p in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
                {
                    if (p.Trim().Length == 0) continue;
                    string cand = Path.Combine(p.Trim(), "ffmpeg.exe");
                    if (File.Exists(cand)) return cand;
                }
            }
            catch { }
            return null;
        }

        private static bool _downloading;
        // Один раз в фоне качаем сборку ffmpeg и достаём bin/ffmpeg.exe в LocalAppData.
        public static void EnsureFfmpegAsync(Action<string> status)
        {
            if (_downloading || FindFfmpeg() != null) return;
            _downloading = true;
            var th = new Thread(() =>
            {
                string tmpZip = Path.Combine(Path.GetTempPath(), "nare_ffmpeg_" + Guid.NewGuid().ToString("N") + ".zip");
                try
                {
                    status("Скачиваю кодек видео (один раз)…");
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                    // лёгкая win64-сборка ffmpeg с готовым ffmpeg.exe в bin/
                    const string url = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
                    using (var wc = new WebClient()) wc.DownloadFile(url, tmpZip);
                    string dest = LocalFfmpegPath();
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    using (var za = ZipFile.OpenRead(tmpZip))
                    {
                        ZipArchiveEntry entry = null;
                        foreach (var e in za.Entries)
                            if (e.FullName.EndsWith("/bin/ffmpeg.exe", StringComparison.OrdinalIgnoreCase) ||
                                e.FullName.EndsWith("\\bin\\ffmpeg.exe", StringComparison.OrdinalIgnoreCase) ||
                                e.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase)) { entry = e; break; }
                        if (entry != null) entry.ExtractToFile(dest, true);
                    }
                    status(File.Exists(dest) ? "Кодек видео готов — следующий эфир пойдёт настоящим видео." : "Не удалось распаковать ffmpeg.");
                }
                catch (Exception ex) { status("Кодек видео не скачался: " + ex.Message + " (пока идут кадры)"); }
                finally { try { if (File.Exists(tmpZip)) File.Delete(tmpZip); } catch { } _downloading = false; }
            });
            th.IsBackground = true; th.Start();
        }

        // --- запуск захвата. fullScreen=true → весь экран, иначе окно по заголовку ---
        public bool Start(bool fullScreen, string windowTitle, int bitrateKbps, int fps)
        {
            string ffmpeg = FindFfmpeg();
            if (ffmpeg == null) { EnsureFfmpegAsync(_status); return false; }

            if (fps < 15) fps = 15; if (fps > 360) fps = 360;
            string input = fullScreen
                ? "-f gdigrab -framerate " + fps + " -i desktop"
                : "-f gdigrab -framerate " + fps + " -i title=\"" + (windowTitle ?? "").Replace("\"", "") + "\"";
            // тихая аудио-дорожка opus, чтобы webm всегда был vp8+opus (совпадает с веб-студией и плеером)
            string silent = "-f lavfi -i anullsrc=r=48000:cl=stereo";
            string args = input + " " + silent +
                " -c:v libvpx -deadline realtime -cpu-used 5 -b:v " + bitrateKbps + "k" +
                " -g " + fps + " -keyint_min " + fps + " -auto-alt-ref 0 -pix_fmt yuv420p" +
                " -c:a libopus -b:a 32k" +
                " -f webm -live 1 -cluster_time_limit 500 pipe:1";

            var psi = new ProcessStartInfo(ffmpeg, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            try { _ff = Process.Start(psi); }
            catch (Exception ex) { _status("ffmpeg не запустился: " + ex.Message); return false; }

            _run = true;
            _reader = new Thread(ReadLoop) { IsBackground = true }; _reader.Start();
            // вычитываем stderr, иначе буфер переполнится и ffmpeg встанет
            var errT = new Thread(() => { try { var s = _ff.StandardError; while (_run && !_ff.HasExited) { if (s.ReadLine() == null) break; } } catch { } });
            errT.IsBackground = true; errT.Start();
            return true;
        }

        // Читаем webm из stdout и режем на init + кластеры по сигнатуре Cluster (0x1F43B675).
        private void ReadLoop()
        {
            var stdout = _ff.StandardOutput.BaseStream;
            byte[] rb = new byte[65536];
            var buf = new List<byte>(1 << 20);
            int segStart = 0, scan = 0;
            bool initSent = false;
            try
            {
                while (_run)
                {
                    int n = stdout.Read(rb, 0, rb.Length);
                    if (n <= 0) break;
                    for (int k = 0; k < n; k++) buf.Add(rb[k]);
                    while (scan + 3 < buf.Count)
                    {
                        if (buf[scan] == 0x1F && buf[scan + 1] == 0x43 && buf[scan + 2] == 0xB6 && buf[scan + 3] == 0x75 && scan > segStart)
                        {
                            int len = scan - segStart;
                            var seg = new byte[len];
                            buf.CopyTo(segStart, seg, 0, len);
                            SendChunk(seg, !initSent);   // первый сегмент = init
                            initSent = true;
                            segStart = scan;
                        }
                        scan++;
                    }
                    // компактим прочитанное, чтобы список не рос бесконечно
                    if (segStart > (1 << 20))
                    {
                        buf.RemoveRange(0, segStart);
                        scan -= segStart; segStart = 0;
                    }
                    // защита от гигантского «кластера» (если сигнатура не нашлась)
                    if (buf.Count > (8 << 20)) { buf.Clear(); scan = 0; segStart = 0; }
                }
            }
            catch { }
        }

        private void SendChunk(byte[] data, bool init)
        {
            if (data == null || data.Length == 0) return;
            string b64 = Convert.ToBase64String(data);
            if (b64.Length > 2500000) return;   // слишком большой чанк пропускаем
            string body = "{\"token\":\"" + _token + "\",\"id\":\"" + _streamId + "\",\"init\":" +
                          (init ? "true" : "false") + ",\"chunk\":\"" + b64 + "\"}";
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("http://" + NareNet.Where + "/stream/media/push");
                req.Method = "POST"; req.ContentType = "application/json"; req.Timeout = 15000;
                var bytes = Encoding.UTF8.GetBytes(body);
                req.ContentLength = bytes.Length;
                using (var s = req.GetRequestStream()) s.Write(bytes, 0, bytes.Length);
                using (var resp = (HttpWebResponse)req.GetResponse()) { }
            }
            catch { }
        }

        public void Dispose()
        {
            _run = false;
            try { if (_ff != null && !_ff.HasExited) _ff.Kill(); } catch { }
            try { if (_ff != null) _ff.Dispose(); } catch { }
            _ff = null;
        }
    }
}
