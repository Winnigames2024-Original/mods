using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;

namespace NareLauncher.Core
{
    public enum SkinArms { Wide, Slim }

    /// <summary>
    /// Скин игрока: всегда 64×64 в 32-битном формате. Старые скины 64×32
    /// достраиваются до нового формата зеркальным копированием руки и ноги.
    /// </summary>
    public class SkinFile
    {
        public Bitmap Image;
        public SkinArms Arms = SkinArms.Wide;
        public string Name = "skin";

        public const int Size = 64;

        public static SkinFile Blank(SkinArms arms)
        {
            var skin = new SkinFile { Arms = arms, Image = NewCanvas() };
            PaintBase(skin.Image, arms);
            return skin;
        }

        /// <summary>Пустая прозрачная картинка 64×64 — основа для скина и для слоёв.</summary>
        public static Bitmap NewImage() { return NewCanvas(); }

        private static Bitmap NewCanvas()
        {
            var bmp = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
                g.Clear(Color.Transparent);
            return bmp;
        }

        public SkinFile Clone()
        {
            return new SkinFile
            {
                Arms = Arms,
                Name = Name,
                Image = new Bitmap(Image)
            };
        }

        // ------------------------------------------------------------ файлы

        public static SkinFile FromFile(string path)
        {
            using (var loaded = new Bitmap(path))
            {
                var skin = FromBitmap(loaded);
                skin.Name = Path.GetFileNameWithoutExtension(path);
                return skin;
            }
        }

        public static SkinFile FromBitmap(Bitmap source)
        {
            var skin = new SkinFile { Image = Normalize(source) };
            skin.Arms = GuessArms(skin.Image);
            return skin;
        }

        public void Save(string path)
        {
            Paths.EnsureForFile(path);
            using (var copy = new Bitmap(Image))
                copy.Save(path, ImageFormat.Png);
        }

        /// <summary>Приводит картинку к 64×64, достраивая старый формат 64×32.</summary>
        public static Bitmap Normalize(Bitmap source)
        {
            var result = NewCanvas();

            using (var g = Graphics.FromImage(result))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

                if (source.Width == 64 && source.Height == 64)
                {
                    g.DrawImage(source, 0, 0, 64, 64);
                    return result;
                }

                if (source.Width == 64 && source.Height == 32)
                {
                    g.DrawImage(source, new Rectangle(0, 0, 64, 32), new Rectangle(0, 0, 64, 32), GraphicsUnit.Pixel);
                    MirrorLimb(g, source, 44, 16, 36, 48);   // правая рука -> левая
                    MirrorLimb(g, source, 0, 16, 20, 48);    // правая нога -> левая
                    return result;
                }

                // что-то нестандартное — просто вписываем
                g.DrawImage(source, new Rectangle(0, 0, 64, 64),
                            new Rectangle(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
            }

            return result;
        }

        /// <summary>Зеркальная копия конечности для скинов старого формата.</summary>
        private static void MirrorLimb(Graphics g, Bitmap source, int sx, int sy, int dx, int dy)
        {
            // верх и низ
            Copy(g, source, sx + 4, sy, 4, 4, dx, dy, true);
            Copy(g, source, sx + 8, sy, 4, 4, dx + 4, dy, true);
            // бока меняются местами
            Copy(g, source, sx + 8, sy + 4, 4, 12, dx, dy + 4, true);
            Copy(g, source, sx + 4, sy + 4, 4, 12, dx + 4, dy + 4, true);
            Copy(g, source, sx, sy + 4, 4, 12, dx + 8, dy + 4, true);
            Copy(g, source, sx + 12, sy + 4, 4, 12, dx + 12, dy + 4, true);
        }

        private static void Copy(Graphics g, Bitmap source, int sx, int sy, int w, int h,
                                 int dx, int dy, bool flip)
        {
            var src = new Rectangle(sx, sy, w, h);
            if (flip)
            {
                using (var part = source.Clone(src, PixelFormat.Format32bppArgb))
                {
                    part.RotateFlip(RotateFlipType.RotateNoneFlipX);
                    g.DrawImage(part, new Rectangle(dx, dy, w, h),
                                new Rectangle(0, 0, w, h), GraphicsUnit.Pixel);
                }
            }
            else g.DrawImage(source, new Rectangle(dx, dy, w, h), src, GraphicsUnit.Pixel);
        }

        /// <summary>У тонкой модели правый край рукава прозрачный.</summary>
        public static SkinArms GuessArms(Bitmap image)
        {
            try
            {
                for (int y = 20; y < 32; y++)
                    if (image.GetPixel(47, y).A > 8) return SkinArms.Wide;
                return SkinArms.Slim;
            }
            catch { return SkinArms.Wide; }
        }

        // ------------------------------------------------------------ Стив и Алекс

        /// <summary>
        /// Берёт стандартный скин из client.jar установленной версии — это файлы
        /// самой игры на диске пользователя. Если версии нет, рисуем свою заготовку.
        /// </summary>
        public static SkinFile Default(bool slim, string versionId)
        {
            string[] names = slim
                ? new[] { "assets/minecraft/textures/entity/player/slim/alex.png",
                          "assets/minecraft/textures/entity/alex.png" }
                : new[] { "assets/minecraft/textures/entity/player/wide/steve.png",
                          "assets/minecraft/textures/entity/steve.png" };

            foreach (var jar in ClientJars(versionId))
            {
                try
                {
                    using (var zip = ZipFile.OpenRead(jar))
                    {
                        foreach (var name in names)
                        {
                            var entry = zip.GetEntry(name);
                            if (entry == null) continue;

                            using (var stream = entry.Open())
                            using (var memory = new MemoryStream())
                            {
                                stream.CopyTo(memory);
                                memory.Position = 0;
                                using (var bmp = new Bitmap(memory))
                                {
                                    var skin = FromBitmap(bmp);
                                    skin.Arms = slim ? SkinArms.Slim : SkinArms.Wide;
                                    skin.Name = slim ? "alex" : "steve";
                                    return skin;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) { Log.Warn("Не удалось прочитать скин из " + jar + ": " + ex.Message); }
            }

            Log.Info("Стандартный скин не найден в файлах игры — используем свою заготовку.");
            var blank = Blank(slim ? SkinArms.Slim : SkinArms.Wide);
            blank.Name = slim ? "alex" : "steve";
            return blank;
        }

        private static IEnumerable<string> ClientJars(string versionId)
        {
            if (!string.IsNullOrEmpty(versionId))
            {
                string direct = Paths.VersionJar(versionId);
                if (File.Exists(direct)) yield return direct;

                var resolved = Json.ParseFile(Paths.VersionJson(versionId));
                string parent = resolved["inheritsFrom"].AsString(null);
                if (!string.IsNullOrEmpty(parent))
                {
                    string p = Paths.VersionJar(parent);
                    if (File.Exists(p)) yield return p;
                }
            }

            if (!Directory.Exists(Paths.VersionsDir)) yield break;

            var dirs = Directory.GetDirectories(Paths.VersionsDir);
            Array.Sort(dirs);
            Array.Reverse(dirs);

            foreach (var dir in dirs)
            {
                string jar = Path.Combine(dir, Path.GetFileName(dir) + ".jar");
                if (File.Exists(jar)) yield return jar;
            }
        }

        /// <summary>Своя простая заготовка, если файлов игры рядом нет.</summary>
        private static void PaintBase(Bitmap image, SkinArms arms)
        {
            int armW = arms == SkinArms.Slim ? 3 : 4;

            Color skin = Color.FromArgb(255, 226, 186, 150);
            Color hair = Color.FromArgb(255, 74, 52, 38);
            Color shirt = Color.FromArgb(255, 74, 120, 190);
            Color pants = Color.FromArgb(255, 58, 62, 96);
            Color shoes = Color.FromArgb(255, 48, 44, 42);
            Color eye = Color.FromArgb(255, 60, 90, 150);

            using (var g = Graphics.FromImage(image))
            {
                Fill(g, SkinLayout.BoxUv(0, 0, 8, 8, 8), skin);          // голова
                Fill(g, new[] { new Rectangle(8, 8, 8, 3) }, hair);      // чёлка
                Fill(g, new[] { new Rectangle(8, 0, 8, 8) }, hair);      // макушка
                Fill(g, new[] { new Rectangle(24, 8, 8, 8) }, hair);     // затылок
                Fill(g, new[] { new Rectangle(0, 8, 8, 3), new Rectangle(16, 8, 8, 3) }, hair);

                Fill(g, new[] { new Rectangle(10, 12, 2, 1), new Rectangle(14, 12, 2, 1) }, eye);

                Fill(g, SkinLayout.BoxUv(16, 16, 8, 12, 4), shirt);      // туловище
                Fill(g, SkinLayout.BoxUv(40, 16, armW, 12, 4), shirt);   // правая рука
                Fill(g, SkinLayout.BoxUv(32, 48, armW, 12, 4), shirt);   // левая рука
                Fill(g, new[] { new Rectangle(44, 16, armW, 4), new Rectangle(36, 48, armW, 4) }, skin);

                Fill(g, SkinLayout.BoxUv(0, 16, 4, 12, 4), pants);       // правая нога
                Fill(g, SkinLayout.BoxUv(16, 48, 4, 12, 4), pants);      // левая нога
                Fill(g, new[] { new Rectangle(0, 28, 16, 4), new Rectangle(16, 60, 16, 4) }, shoes);
            }
        }

        private static void Fill(Graphics g, Rectangle[] rects, Color color)
        {
            using (var brush = new SolidBrush(color))
                foreach (var r in rects)
                    if (r.Width > 0 && r.Height > 0) g.FillRectangle(brush, r);
        }
    }
}
