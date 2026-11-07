using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace NareLauncher.Core
{
    /// <summary>
    /// Картинки модов и ресурспаков. У мода это logoFile из его описания
    /// (mods.toml у Forge, fabric.mod.json у Fabric), у ресурспака — pack.png.
    ///
    /// Читаем в фоне и держим в памяти: распаковывать архив на каждую отрисовку
    /// списка нельзя, иначе прокрутка начнёт заикаться. Пока картинка не
    /// готова, список рисует прежнюю заглушку — буквой.
    /// </summary>
    public static class ModIcons
    {
        private class Entry
        {
            public Bitmap Image;
            public bool Ready;
            public long Stamp;
        }

        private static readonly Dictionary<string, Entry> Cache =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        private static readonly object Gate = new object();

        /// <summary>Зовётся, когда картинка догрузилась: список стоит перерисовать.</summary>
        public static event Action Loaded;

        /// <summary>
        /// Картинка файла или null. Первый вызов ставит чтение в очередь и
        /// сразу возвращает null — рисуйте заглушку, перерисовка придёт сама.
        /// </summary>
        public static Bitmap For(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            long stamp;
            try { stamp = File.GetLastWriteTimeUtc(path).Ticks; }
            catch { return null; }

            Entry entry;
            lock (Gate)
            {
                if (Cache.TryGetValue(path, out entry))
                {
                    if (entry.Stamp == stamp) return entry.Image;

                    // файл подменили — читаем заново
                    if (entry.Image != null) { try { entry.Image.Dispose(); } catch { } }
                    Cache.Remove(path);
                }

                entry = new Entry { Stamp = stamp };
                Cache[path] = entry;
            }

            Task.Run(() =>
            {
                Bitmap found = null;
                try { found = Read(path); }
                catch (Exception ex) { Log.Warn("Картинка не прочиталась: " + Path.GetFileName(path) + " — " + ex.Message); }

                lock (Gate)
                {
                    Entry current;
                    if (Cache.TryGetValue(path, out current) && current == entry)
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

        /// <summary>Убирает всё из памяти — например при смене сборки.</summary>
        public static void Forget()
        {
            lock (Gate)
            {
                foreach (var entry in Cache.Values)
                    if (entry.Image != null) { try { entry.Image.Dispose(); } catch { } }
                Cache.Clear();
            }
        }

        // ------------------------------------------------------------ чтение

        private static Bitmap Read(string path)
        {
            string ext = Path.GetExtension(path);
            if (string.Equals(ext, ".disabled", StringComparison.OrdinalIgnoreCase))
                ext = Path.GetExtension(Path.GetFileNameWithoutExtension(path));

            bool archive = string.Equals(ext, ".jar", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase);

            // ресурспак может лежать и просто папкой
            if (!archive && Directory.Exists(path))
            {
                string png = Path.Combine(path, "pack.png");
                return File.Exists(png) ? FromFile(png) : null;
            }

            if (!archive) return null;

            using (var zip = ZipFile.OpenRead(path))
            {
                // сначала то, что мод сам назвал своей картинкой
                string named = NamedLogo(zip);
                if (!string.IsNullOrEmpty(named))
                {
                    var hit = Find(zip, named);
                    if (hit != null) return FromEntry(hit);
                }

                string[] guesses = { "icon.png", "logo.png", "pack.png", "mod_logo.png", "modicon.png" };
                foreach (string guess in guesses)
                {
                    var hit = Find(zip, guess);
                    if (hit != null) return FromEntry(hit);
                }

                // assets/<что-то>/icon.png
                foreach (var candidate in zip.Entries)
                {
                    string name = candidate.FullName.Replace('\\', '/');
                    if (!name.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!name.EndsWith("/icon.png", StringComparison.OrdinalIgnoreCase)) continue;
                    return FromEntry(candidate);
                }
            }

            return null;
        }

        private static ZipArchiveEntry Find(ZipArchive zip, string name)
        {
            string want = name.Replace('\\', '/').TrimStart('/');

            foreach (var entry in zip.Entries)
            {
                string full = entry.FullName.Replace('\\', '/');
                if (string.Equals(full, want, StringComparison.OrdinalIgnoreCase)) return entry;
            }

            return null;
        }

        /// <summary>Достаёт logoFile из mods.toml или fabric.mod.json.</summary>
        private static string NamedLogo(ZipArchive zip)
        {
            string[] places =
            {
                "META-INF/mods.toml",
                "META-INF/neoforge.mods.toml",
                "fabric.mod.json",
                "quilt.mod.json"
            };

            foreach (string place in places)
            {
                var entry = Find(zip, place);
                if (entry == null) continue;

                try
                {
                    string text;
                    using (var reader = new StreamReader(entry.Open()))
                        text = reader.ReadToEnd();

                    string value = Between(text, "logoFile");
                    if (string.IsNullOrEmpty(value)) value = Between(text, "\"icon\"");
                    if (!string.IsNullOrEmpty(value)) return value;
                }
                catch { }
            }

            return null;
        }

        /// <summary>Значение в кавычках после ключа — хватает и для toml, и для json.</summary>
        private static string Between(string text, string key)
        {
            int at = text.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (at < 0) return null;

            int quote = -1;
            for (int i = at + key.Length; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n' || c == '\r') return null;
                if (c == '"' || c == '\'') { quote = i; break; }
            }
            if (quote < 0) return null;

            char closing = text[quote];
            int end = text.IndexOf(closing, quote + 1);
            if (end <= quote + 1) return null;

            string value = text.Substring(quote + 1, end - quote - 1).Trim();
            return value.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? value : null;
        }

        private static Bitmap FromEntry(ZipArchiveEntry entry)
        {
            using (var stream = entry.Open())
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                memory.Position = 0;
                using (var raw = new Bitmap(memory))
                    return Shrink(raw);
            }
        }

        private static Bitmap FromFile(string path)
        {
            using (var raw = new Bitmap(path))
                return Shrink(raw);
        }

        /// <summary>
        /// Храним копию не больше 128 пикселей: в списке иконка крошечная,
        /// а некоторые моды кладут логотип в 1024 и памяти уходит много.
        /// </summary>
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
