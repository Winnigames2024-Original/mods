using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace NareLauncher.Core
{
    /// <summary>
    /// Трансляция прямо из лаунчера — как OBS: захват экрана/окна через ffmpeg,
    /// кодирование в H.264 и отправка потоком по RTMP на наш сервер
    /// (rtmp://ХОСТ/live/КЛЮЧ). Сервер сам раздаёт это зрителям как HLS.
    /// Если ffmpeg не найден — один раз скачивает его в фоне.
    /// </summary>
    public sealed class StreamEncoder : IDisposable
    {
        private readonly Action<string> _status;
        private Process _ff;
        private volatile bool _run;

        public bool Running { get { try { return _ff != null && !_ff.HasExited; } catch { return false; } } }

        public StreamEncoder(Action<string> status) { _status = status ?? delegate { }; }

        // --- где лежит ffmpeg.exe (рядом с лаунчером, в LocalAppData или в PATH) ---
        public static string LocalFfmpegPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "NareLauncher", "ffmpeg.exe");
        }
        public static string FindFfmpeg()
        {
            try { string near = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "ffmpeg.exe"); if (File.Exists(near)) return near; } catch { }
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
                    status(File.Exists(dest) ? "Кодек видео готов — запусти эфир ещё раз." : "Не удалось распаковать ffmpeg.");
                }
                catch (Exception ex) { status("Кодек видео не скачался: " + ex.Message); }
                finally { try { if (File.Exists(tmpZip)) File.Delete(tmpZip); } catch { } _downloading = false; }
            });
            th.IsBackground = true; th.Start();
        }

        // --- запуск RTMP-трансляции (как OBS). rtmpUrl = rtmp://host/live/КЛЮЧ ---
        public bool StartRtmp(string rtmpUrl, bool fullScreen, string windowTitle, int bitrateKbps, int fps)
        {
            string ffmpeg = FindFfmpeg();
            if (ffmpeg == null) { EnsureFfmpegAsync(_status); return false; }
            if (string.IsNullOrEmpty(rtmpUrl)) { _status("нет адреса трансляции"); return false; }
            if (fps < 15) fps = 15; if (fps > 120) fps = 120;   // для RTMP разумный предел
            if (bitrateKbps < 800) bitrateKbps = 800;

            string input = fullScreen
                ? "-f gdigrab -framerate " + fps + " -i desktop"
                : "-f gdigrab -framerate " + fps + " -i title=\"" + (windowTitle ?? "").Replace("\"", "") + "\"";
            // тихая аудиодорожка, чтобы в потоке всегда были и видео, и звук (позже — реальный микрофон)
            string silent = "-f lavfi -i anullsrc=r=44100:cl=stereo";
            string args = input + " " + silent +
                " -c:v libx264 -preset veryfast -tune zerolatency -pix_fmt yuv420p" +
                " -b:v " + bitrateKbps + "k -maxrate " + bitrateKbps + "k -bufsize " + (bitrateKbps * 2) + "k" +
                " -g " + (fps * 2) + " -keyint_min " + fps +
                " -c:a aac -b:a 128k -ar 44100" +
                " -f flv \"" + rtmpUrl + "\"";

            var psi = new ProcessStartInfo(ffmpeg, args)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            try { _ff = Process.Start(psi); }
            catch (Exception ex) { _status("ffmpeg не запустился: " + ex.Message); return false; }

            _run = true;
            // вычитываем вывод, чтобы процесс не завис на заполненном буфере
            new Thread(() => { try { var s = _ff.StandardError; while (_run && !_ff.HasExited) { if (s.ReadLine() == null) break; } } catch { } }) { IsBackground = true }.Start();
            new Thread(() => { try { var s = _ff.StandardOutput; while (_run && !_ff.HasExited) { if (s.ReadLine() == null) break; } } catch { } }) { IsBackground = true }.Start();
            return true;
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
