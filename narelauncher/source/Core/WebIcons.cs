using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NareLauncher.Core
{
    /// <summary>
    /// Картинки из интернета — логотипы модов в поиске. Качаем в фоне, держим
    /// в памяти и складываем на диск, чтобы при следующем поиске не ходить за
    /// теми же файлами снова.
    ///
    /// Устроено как <see cref="ModIcons"/>: первый вызов возвращает null и
    /// ставит загрузку в очередь, а когда картинка приедет, список
    /// перерисовывается по событию.
    /// </summary>
    public static class WebIcons
    {
        private class Entry
        {
            public Bitmap Image;
            public bool Ready;
        }

        private static readonly Dictionary<string, Entry> Cache =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        private static readonly object Gate = new object();

        // чтобы двадцать строк списка не открыли двадцать соединений разом
        private static readonly SemaphoreSlim Lane = new SemaphoreSlim(4);

        /// <summary>Картинка догрузилась — список стоит перерисовать.</summary>
        public static event Action Loaded;

        /// <summary>
        /// Картинка по адресу или null. Повторный вызов для адреса, который
        /// не удалось прочитать, тоже вернёт null и ничего качать не станет.
        /// </summary>
        public static Bitmap For(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return null;

            Entry entry;
            lock (Gate)
            {
                if (Cache.TryGetValue(url, out entry)) return entry.Image;

                entry = new Entry();
                Cache[url] = entry;
            }

            Task.Run(async delegate
            {
                Bitmap found = null;

                try { found = await Fetch(url); }
                catch (Exception ex) { Log.Warn("Картинка не скачалась: " + url + " — " + ex.Message); }

                lock (Gate)
                {
                    Entry current;
                    if (Cache.TryGetValue(url, out current) && current == entry)
                    {
                        entry.Image = found;
                        entry.Ready = true;
                    }
                    else if (found != null) { try { found.Dispose(); } catch { } }
                }

                var handler = Loaded;
                if (handler != null) handler();
            });

            return null;
        }

        public static void Forget()
        {
            lock (Gate)
            {
                foreach (var entry in Cache.Values)
                    if (entry.Image != null) { try { entry.Image.Dispose(); } catch { } }
                Cache.Clear();
            }
        }

        // ------------------------------------------------------------ загрузка

        private static async Task<Bitmap> Fetch(string url)
        {
            string file = CachedPath(url);

            // лежит на диске с прошлого раза
            if (File.Exists(file))
            {
                try { return Load(File.ReadAllBytes(file)); }
                catch { try { File.Delete(file); } catch { } }
            }

            byte[] data;
            await Lane.WaitAsync();
            try { data = await Net.GetBytesAsync(url); }
            finally { Lane.Release(); }

            if (data == null || data.Length == 0) return null;

            // svg и webp GDI+ не умеет — такие просто пропускаем
            Bitmap picture = Load(data);
            if (picture == null) return null;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllBytes(file, data);
            }
            catch { /* не записалось — ничего страшного, будет качаться каждый раз */ }

            return picture;
        }

        private static Bitmap Load(byte[] data)
        {
            try
            {
                using (var memory = new MemoryStream(data))
                using (var raw = new Bitmap(memory))
                    return Shrink(raw);
            }
            catch { return null; }
        }

        private static string CachedPath(string url)
        {
            string name;
            using (var sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(url));
                var text = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) text.Append(b.ToString("x2"));
                name = text.ToString();
            }

            return Path.Combine(Paths.CacheDir, "icons", name + ".img");
        }

        /// <summary>Держим копию не больше 128 пикселей — в списке иконка мелкая.</summary>
        private static Bitmap Shrink(Bitmap raw)
        {
            int side = Math.Max(raw.Width, raw.Height);
            if (side <= 128) return new Bitmap(raw);

            float k = 128f / side;
            int w = Math.Max(1, (int)(raw.Width * k));
            int h = Math.Max(1, (int)(raw.Height * k));

            var small = new Bitmap(w, h);
            using (var g = Graphics.FromImage(small))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(raw, 0, 0, w, h);
            }
            return small;
        }
    }
}
