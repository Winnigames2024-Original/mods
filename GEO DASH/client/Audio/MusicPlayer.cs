using NAudio.Wave;
using GEO_DASH.Net;

namespace GEO_DASH.Audio
{
    /// <summary>Музыка по ID (Newgrounds) — качается через наш сервер и кэшируется локально.</summary>
    public static class MusicPlayer
    {
        static IWavePlayer _out;
        static AudioFileReader _reader;
        static int _currentId = -1;
        public static float Volume = 0.8f;
        public static bool Enabled = true;
        public static string LastError = "";
        public static int CurrentId => _currentId;

        /// <summary>Песни лежат в assets/music рядом с игрой.</summary>
        public static string CacheDir => AppConfig.MusicDir;

        public static string CachePath(int id) =>
            Soundtrack.IsBuiltIn(id) ? Soundtrack.PathOf(id) : Path.Combine(CacheDir, id + ".mp3");

        public static bool IsCached(int id)
        {
            var p = CachePath(id);
            return File.Exists(p) && new FileInfo(p).Length > 4096;
        }

        /// <summary>Скачивает трек по ID, если его ещё нет. Возвращает путь к файлу.
        /// Отрицательные ID — встроенный саундтрек (генерируется один раз в assets/music).</summary>
        public static async Task<string> EnsureAsync(int id, IProgress<string> prog = null)
        {
            if (Soundtrack.IsBuiltIn(id))
                return await Task.Run(() => Soundtrack.Ensure(id, prog));
            if (id <= 0) return null;
            var path = CachePath(id);
            if (IsCached(id)) return path;
            prog?.Report($"Скачиваю трек #{id}…");
            try
            {
                var bytes = await ApiClient.DownloadSongAsync(id);
                if (bytes == null || bytes.Length < 4096) { LastError = "Трек не найден на сервере"; return null; }
                await File.WriteAllBytesAsync(path, bytes);
                return path;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                try { if (File.Exists(path)) File.Delete(path); } catch { }
                return null;
            }
        }

        public static async Task<bool> PlayAsync(int id, double startSec = 0, double volume = 1, bool loop = true)
        {
            if (!Enabled) return false;
            if (id <= 0) { Stop(); return false; }
            var path = await EnsureAsync(id);
            if (path == null) return false;
            return PlayFile(path, startSec, volume, loop, id);
        }

        public static bool PlayFile(string path, double startSec, double volume, bool loop, int id = -1)
        {
            try
            {
                Stop();
                _reader = new AudioFileReader(path) { Volume = (float)Math.Clamp(volume * Volume, 0, 1) };
                if (startSec > 0 && startSec < _reader.TotalTime.TotalSeconds)
                    _reader.CurrentTime = TimeSpan.FromSeconds(startSec);
                _out = new WaveOutEvent { DesiredLatency = 150 };
                _out.Init(_reader);
                _out.PlaybackStopped += (s, e) =>
                {
                    if (loop && _reader != null)
                    {
                        try { _reader.Position = 0; _out?.Play(); } catch { }
                    }
                };
                _out.Play();
                _currentId = id;
                return true;
            }
            catch (Exception ex) { LastError = ex.Message; return false; }
        }

        public static void Stop()
        {
            try { _out?.Stop(); _out?.Dispose(); } catch { }
            try { _reader?.Dispose(); } catch { }
            _out = null; _reader = null; _currentId = -1;
        }

        public static void Pause() { try { _out?.Pause(); } catch { } }
        public static void Resume() { try { _out?.Play(); } catch { } }

        public static void SetVolume(double v)
        {
            Volume = (float)Math.Clamp(v, 0, 1);
            try { if (_reader != null) _reader.Volume = Volume; } catch { }
        }

        public static void Seek(double sec)
        {
            try { if (_reader != null && sec >= 0 && sec < _reader.TotalTime.TotalSeconds) _reader.CurrentTime = TimeSpan.FromSeconds(sec); }
            catch { }
        }

        public static double PositionSec => _reader?.CurrentTime.TotalSeconds ?? 0;
        public static double LengthSec => _reader?.TotalTime.TotalSeconds ?? 0;
        public static bool Playing => _out?.PlaybackState == PlaybackState.Playing;
    }
}
