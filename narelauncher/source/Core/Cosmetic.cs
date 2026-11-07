using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace NareLauncher.Core
{
    public enum CosmeticKind { Cape, Hat, Backpack }

    /// <summary>
    /// Вещь, которую игрок носит поверх скина: плащ, шляпа, рюкзак.
    ///
    /// Всё хранится одними картинками в папке лаунчера, а вид и название
    /// берутся из имени файла: «cape__Полночь.png». Никаких отдельных
    /// описаний — файл перенёс, и вещь переехала вместе с ним.
    /// </summary>
    public class Cosmetic
    {
        public CosmeticKind Kind = CosmeticKind.Cape;
        public string Name = "";
        public string Path = "";

        public string Id { get { return System.IO.Path.GetFileName(Path); } }

        public string KindRu
        {
            get
            {
                switch (Kind)
                {
                    case CosmeticKind.Hat: return "Шляпа";
                    case CosmeticKind.Backpack: return "Рюкзак";
                    default: return "Плащ";
                }
            }
        }

        public override string ToString() { return Name; }
    }

    public static class CosmeticStore
    {
        public static string Dir { get { return System.IO.Path.Combine(Paths.DataDir, "cosmetics"); } }

        private static readonly Dictionary<string, Bitmap> Textures =
            new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

        // ------------------------------------------------------------ список

        public static List<Cosmetic> All()
        {
            var list = new List<Cosmetic>();

            try
            {
                Paths.Ensure(Dir);
                EnsureReady();

                foreach (string file in Directory.GetFiles(Dir, "*.png"))
                {
                    var item = Parse(file);
                    if (item != null) list.Add(item);
                }
            }
            catch (Exception ex) { Log.Warn("Вещи не прочитались: " + ex.Message); }

            list.Sort(delegate(Cosmetic a, Cosmetic b)
            {
                int byKind = a.Kind.CompareTo(b.Kind);
                return byKind != 0 ? byKind : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            return list;
        }

        private static Cosmetic Parse(string file)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(file);
            int split = name.IndexOf("__", StringComparison.Ordinal);
            if (split <= 0) return null;

            string kind = name.Substring(0, split).ToLowerInvariant();
            string title = name.Substring(split + 2).Replace('_', ' ').Trim();
            if (title.Length == 0) return null;

            CosmeticKind parsed;
            if (kind == "cape") parsed = CosmeticKind.Cape;
            else if (kind == "hat") parsed = CosmeticKind.Hat;
            else if (kind == "backpack") parsed = CosmeticKind.Backpack;
            else return null;

            return new Cosmetic { Kind = parsed, Name = title, Path = file };
        }

        private static string Prefix(CosmeticKind kind)
        {
            switch (kind)
            {
                case CosmeticKind.Hat: return "hat";
                case CosmeticKind.Backpack: return "backpack";
                default: return "cape";
            }
        }

        /// <summary>Путь для новой вещи; если имя занято, добавляет номер.</summary>
        public static string PathFor(CosmeticKind kind, string name)
        {
            Paths.Ensure(Dir);

            string safe = Paths.SafeName(string.IsNullOrWhiteSpace(name) ? "Без имени" : name.Trim());
            string baseName = Prefix(kind) + "__" + safe;
            string path = System.IO.Path.Combine(Dir, baseName + ".png");

            int n = 2;
            while (File.Exists(path))
            {
                path = System.IO.Path.Combine(Dir, baseName + " " + n + ".png");
                n++;
            }

            return path;
        }

        public static void Remove(Cosmetic item)
        {
            if (item == null) return;

            Forget(item.Path);
            TakeOff(item);

            try { File.Delete(item.Path); }
            catch (Exception ex) { Log.Warn("Вещь не удалилась: " + ex.Message); }
        }

        // ------------------------------------------------------------ надето

        private static List<string> WornIds()
        {
            var list = new List<string>();
            string raw = Settings.Current.WornCosmetics ?? "";

            foreach (string part in raw.Split('|'))
                if (part.Trim().Length > 0) list.Add(part.Trim());

            return list;
        }

        public static bool IsWorn(Cosmetic item)
        {
            if (item == null) return false;
            foreach (string id in WornIds())
                if (string.Equals(id, item.Id, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// Надевает вещь. Двух плащей сразу не бывает, поэтому прежняя вещь
        /// того же вида снимается сама.
        /// </summary>
        public static void Wear(Cosmetic item)
        {
            if (item == null) return;

            var keep = new List<string>();
            foreach (string id in WornIds())
            {
                var other = Find(id);
                if (other != null && other.Kind == item.Kind) continue;
                if (string.Equals(id, item.Id, StringComparison.OrdinalIgnoreCase)) continue;
                keep.Add(id);
            }

            keep.Add(item.Id);
            Settings.Current.WornCosmetics = string.Join("|", keep.ToArray());
            Settings.Save();
        }

        public static void TakeOff(Cosmetic item)
        {
            if (item == null) return;

            var keep = new List<string>();
            foreach (string id in WornIds())
                if (!string.Equals(id, item.Id, StringComparison.OrdinalIgnoreCase)) keep.Add(id);

            Settings.Current.WornCosmetics = string.Join("|", keep.ToArray());
            Settings.Save();
        }

        public static Cosmetic Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            string path = System.IO.Path.Combine(Dir, id);
            return File.Exists(path) ? Parse(path) : null;
        }

        /// <summary>Всё, что сейчас надето и правда существует.</summary>
        public static List<Cosmetic> Worn()
        {
            var list = new List<Cosmetic>();
            foreach (string id in WornIds())
            {
                var item = Find(id);
                if (item != null) list.Add(item);
            }
            return list;
        }

        // ---------------------------------------------------------- картинки

        public static Bitmap Texture(Cosmetic item)
        {
            if (item == null) return null;

            Bitmap cached;
            if (Textures.TryGetValue(item.Path, out cached)) return cached;

            try
            {
                using (var raw = new Bitmap(item.Path))
                {
                    var copy = new Bitmap(raw);
                    Textures[item.Path] = copy;
                    return copy;
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Картинка вещи не открылась: " + ex.Message);
                Textures[item.Path] = null;
                return null;
            }
        }

        public static void Forget(string path)
        {
            Bitmap cached;
            if (path != null && Textures.TryGetValue(path, out cached))
            {
                if (cached != null) { try { cached.Dispose(); } catch { } }
                Textures.Remove(path);
            }
        }

        public static void ForgetAll()
        {
            foreach (var pair in Textures)
                if (pair.Value != null) { try { pair.Value.Dispose(); } catch { } }
            Textures.Clear();
        }

        // ------------------------------------------------------- заготовки

        /// <summary>Узоры, из которых собираются готовые вещи и те, что делает игрок.</summary>
        public enum Pattern { Сплошной, Полоса, Клетка, Градиент, Звезда, Кайма, Буква }

        private static bool ready;

        /// <summary>Кладёт набор готовых плащей, если папка пустая.</summary>
        public static void EnsureReady()
        {
            if (ready) return;
            ready = true;

            try
            {
                Paths.Ensure(Dir);
                if (Directory.GetFiles(Dir, "*.png").Length > 0) return;

                Make(CosmeticKind.Cape, "Полночь", Color.FromArgb(24, 32, 56), Color.FromArgb(96, 150, 255), Pattern.Градиент, "N");
                Make(CosmeticKind.Cape, "Закат", Color.FromArgb(58, 22, 40), Color.FromArgb(255, 150, 80), Pattern.Полоса, "N");
                Make(CosmeticKind.Cape, "Хвоя", Color.FromArgb(18, 46, 32), Color.FromArgb(90, 220, 150), Pattern.Кайма, "N");
                Make(CosmeticKind.Cape, "Звёздный", Color.FromArgb(14, 16, 30), Color.FromArgb(230, 235, 255), Pattern.Звезда, "N");
                Make(CosmeticKind.Cape, "Клетка", Color.FromArgb(40, 40, 46), Color.FromArgb(200, 70, 90), Pattern.Клетка, "N");
                Make(CosmeticKind.Cape, "Своя буква", Color.FromArgb(26, 26, 34), Color.FromArgb(120, 200, 255), Pattern.Буква, "N");

                Make(CosmeticKind.Hat, "Цилиндр", Color.FromArgb(22, 22, 28), Color.FromArgb(190, 60, 80), Pattern.Кайма, "N");
                Make(CosmeticKind.Backpack, "Походный", Color.FromArgb(62, 44, 28), Color.FromArgb(150, 110, 60), Pattern.Полоса, "N");

                Log.Info("Готовые вещи для игрока созданы.");
            }
            catch (Exception ex) { Log.Warn("Готовые вещи не создались: " + ex.Message); }
        }

        /// <summary>Рисует вещь и сохраняет её. Возвращает путь к файлу.</summary>
        public static string Make(CosmeticKind kind, string name,
                                  Color baseColor, Color accent, Pattern pattern, string letter)
        {
            string path = PathFor(kind, name);

            using (var image = Draw(kind, baseColor, accent, pattern, letter))
                image.Save(path, ImageFormat.Png);

            return path;
        }

        /// <summary>
        /// Картинка вещи. Плащ — развёртка 64×32 как у ванильных плащей, так
        /// что чужой плащ из интернета тоже подойдёт. Шляпа и рюкзак проще:
        /// один квадрат 16×16 ложится на все грани.
        /// </summary>
        public static Bitmap Draw(CosmeticKind kind, Color baseColor, Color accent,
                                  Pattern pattern, string letter)
        {
            if (kind == CosmeticKind.Cape)
            {
                var image = new Bitmap(64, 32, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(image))
                {
                    g.SmoothingMode = SmoothingMode.None;
                    g.Clear(Color.Transparent);

                    // наружная сторона, внутренняя и узкие рёбра
                    Paint(g, new Rectangle(1, 1, 10, 16), baseColor, accent, pattern, letter, true);
                    Paint(g, new Rectangle(12, 1, 10, 16), Darker(baseColor, 0.72f), accent, Pattern.Сплошной, letter, false);

                    using (var edge = new SolidBrush(Darker(baseColor, 0.85f)))
                    {
                        g.FillRectangle(edge, 0, 1, 1, 16);
                        g.FillRectangle(edge, 11, 1, 1, 16);
                        g.FillRectangle(edge, 1, 0, 10, 1);
                        g.FillRectangle(edge, 11, 0, 10, 1);
                    }
                }
                return image;
            }

            var box = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(box))
            {
                g.SmoothingMode = SmoothingMode.None;
                g.Clear(Color.Transparent);
                Paint(g, new Rectangle(0, 0, 16, 16), baseColor, accent, pattern, letter, true);
            }
            return box;
        }

        private static void Paint(Graphics g, Rectangle r, Color baseColor, Color accent,
                                  Pattern pattern, string letter, bool decorate)
        {
            using (var fill = new SolidBrush(baseColor))
                g.FillRectangle(fill, r);

            if (!decorate) return;

            using (var ink = new SolidBrush(accent))
            {
                switch (pattern)
                {
                    case Pattern.Полоса:
                        g.FillRectangle(ink, r.X, r.Y + r.Height / 2 - 1, r.Width, 2);
                        break;

                    case Pattern.Клетка:
                        for (int y = 0; y < r.Height; y += 4)
                            for (int x = (y / 4) % 2 == 0 ? 0 : 2; x < r.Width; x += 4)
                                g.FillRectangle(ink, r.X + x, r.Y + y, 2, 2);
                        break;

                    case Pattern.Градиент:
                        for (int y = 0; y < r.Height; y++)
                        {
                            float k = (float)y / Math.Max(1, r.Height - 1);
                            using (var line = new SolidBrush(Mix(baseColor, accent, k)))
                                g.FillRectangle(line, r.X, r.Y + y, r.Width, 1);
                        }
                        break;

                    case Pattern.Звезда:
                        int[,] stars = { { 2, 3 }, { 7, 2 }, { 4, 7 }, { 8, 9 }, { 2, 12 }, { 6, 13 } };
                        for (int i = 0; i < stars.GetLength(0); i++)
                        {
                            int x = r.X + Math.Min(r.Width - 1, stars[i, 0]);
                            int y = r.Y + Math.Min(r.Height - 1, stars[i, 1]);
                            g.FillRectangle(ink, x, y, 1, 1);
                            if (x > r.X) g.FillRectangle(ink, x - 1, y, 1, 1);
                            if (y > r.Y) g.FillRectangle(ink, x, y - 1, 1, 1);
                        }
                        break;

                    case Pattern.Кайма:
                        g.FillRectangle(ink, r.X, r.Y, r.Width, 1);
                        g.FillRectangle(ink, r.X, r.Bottom - 1, r.Width, 1);
                        g.FillRectangle(ink, r.X, r.Y, 1, r.Height);
                        g.FillRectangle(ink, r.Right - 1, r.Y, 1, r.Height);
                        break;

                    case Pattern.Буква:
                        string text = string.IsNullOrEmpty(letter) ? "N" : letter.Substring(0, 1);
                        using (var font = new Font(FontFamily.GenericSansSerif,
                                   Math.Max(5f, r.Height * 0.45f), FontStyle.Bold, GraphicsUnit.Pixel))
                        using (var format = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        })
                        {
                            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixel;
                            g.DrawString(text, font, ink, r, format);
                        }
                        break;
                }
            }
        }

        private static Color Darker(Color c, float k)
        {
            return Color.FromArgb(c.A, (int)(c.R * k), (int)(c.G * k), (int)(c.B * k));
        }

        private static Color Mix(Color a, Color b, float k)
        {
            return Color.FromArgb(255,
                (int)(a.R + (b.R - a.R) * k),
                (int)(a.G + (b.G - a.G) * k),
                (int)(a.B + (b.B - a.B) * k));
        }

        /// <summary>Кладёт чужую картинку как новую вещь.</summary>
        public static string Import(CosmeticKind kind, string name, string sourcePng)
        {
            string path = PathFor(kind, name);

            using (var raw = new Bitmap(sourcePng))
            using (var copy = new Bitmap(raw))
                copy.Save(path, ImageFormat.Png);

            return path;
        }
    }
}
