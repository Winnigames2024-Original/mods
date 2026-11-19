using System.Net.Http;

namespace GEO_DASH.Net
{
    /// <summary>Инфо о картинке с сайта.</summary>
    public class ImageInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Author { get; set; } = "";
        public long Size { get; set; }
        public string Ext { get; set; } = ".png";
    }

    /// <summary>
    /// Картинки из toolbox сайта: скачиваются в assets/images и дальше работают офлайн.
    /// </summary>
    public static class ImageLibrary
    {
        static readonly Dictionary<int, Image> _cache = new();
        static readonly HashSet<int> _loading = new();

        public static string Dir => AppConfig.Ensure(Path.Combine(AppConfig.DataDir, "images"));

        public static string PathOf(int id)
        {
            foreach (var ext in new[] { ".png", ".jpg", ".gif" })
            {
                var p = Path.Combine(Dir, id + ext);
                if (File.Exists(p)) return p;
            }
            return Path.Combine(Dir, id + ".png");
        }

        public static bool IsCached(int id)
        {
            var p = PathOf(id);
            return File.Exists(p) && new FileInfo(p).Length > 64;
        }

        /// <summary>Готовая картинка или null — тогда она качается в фоне.</summary>
        public static Image Get(int id)
        {
            if (id <= 0) return null;
            if (_cache.TryGetValue(id, out var img)) return img;

            if (IsCached(id))
            {
                try
                {
                    using var fs = File.OpenRead(PathOf(id));
                    var bmp = Image.FromStream(fs);
                    _cache[id] = bmp;
                    return bmp;
                }
                catch { }
            }

            if (_loading.Add(id)) _ = DownloadAsync(id);
            return null;
        }

        /// <summary>Скачать картинку по ID с сервера (или с сайта, если API недоступен).</summary>
        public static async Task<bool> DownloadAsync(int id)
        {
            try
            {
                var bytes = await ApiClient.DownloadImageAsync(id);
                if (bytes == null || bytes.Length < 64) return false;

                string ext = bytes[0] == 0xFF && bytes[1] == 0xD8 ? ".jpg"
                           : bytes[0] == 'G' && bytes[1] == 'I' ? ".gif" : ".png";
                var path = Path.Combine(Dir, id + ext);
                await File.WriteAllBytesAsync(path, bytes);
                _cache.Remove(id);
                return true;
            }
            catch { return false; }
            finally { _loading.Remove(id); }
        }

        public static void Forget(int id)
        {
            if (_cache.TryGetValue(id, out var img)) { img.Dispose(); _cache.Remove(id); }
        }
    }
}
