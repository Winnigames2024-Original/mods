using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace NareLauncher.Core
{
    /// <summary>Один слой скина — такая же картинка 64×64, как и сам скин.</summary>
    public class SkinLayer
    {
        public string Name = "Слой";
        public Bitmap Image;
        public bool Visible = true;
        public int Opacity = 100;      // 0..100

        public SkinLayer Clone()
        {
            return new SkinLayer
            {
                Name = Name,
                Visible = Visible,
                Opacity = Opacity,
                Image = Image == null ? SkinFile.NewImage() : new Bitmap(Image)
            };
        }
    }

    /// <summary>
    /// Скин со слоями. Видимые слои складываются снизу вверх в одну текстуру 64×64 —
    /// её показывает просмотр, её же отдаём игре и сохраняем в PNG.
    /// Документ целиком (все слои, их имена, видимость, прозрачность и тип рук)
    /// хранится в собственном формате .nareskin.
    /// </summary>
    public class SkinDocument
    {
        public const string Extension = ".nareskin";
        private const string Magic = "NARESKIN";
        private const int FormatVersion = 1;

        /// <summary>Слои снизу вверх: нулевой — самый нижний.</summary>
        public readonly List<SkinLayer> Layers = new List<SkinLayer>();

        /// <summary>Готовая склейка слоёв — то, что видно на холсте и на модели.</summary>
        public readonly SkinFile Flat = new SkinFile { Image = SkinFile.NewImage() };

        public int Active;

        public SkinArms Arms
        {
            get { return Flat.Arms; }
            set { Flat.Arms = value; }
        }

        public string Name
        {
            get { return Flat.Name; }
            set { Flat.Name = value; }
        }

        public SkinLayer Current
        {
            get
            {
                if (Layers.Count == 0)
                    Layers.Add(new SkinLayer { Name = "Слой 1", Image = SkinFile.NewImage() });

                if (Active < 0) Active = 0;
                if (Active >= Layers.Count) Active = Layers.Count - 1;
                return Layers[Active];
            }
        }

        // ------------------------------------------------------------ создание

        public static SkinDocument FromSkin(SkinFile skin)
        {
            var doc = new SkinDocument();
            doc.Arms = skin.Arms;
            doc.Name = skin.Name;
            doc.Layers.Add(new SkinLayer { Name = "Основа", Image = new Bitmap(skin.Image) });
            doc.Active = 0;
            doc.Rebuild();
            return doc;
        }

        // ------------------------------------------------------------ склейка

        public void Rebuild()
        {
            if (Flat.Image == null) Flat.Image = SkinFile.NewImage();

            using (var g = Graphics.FromImage(Flat.Image))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.SmoothingMode = SmoothingMode.None;

                g.CompositingMode = CompositingMode.SourceCopy;
                g.FillRectangle(Brushes.Transparent, 0, 0, SkinFile.Size, SkinFile.Size);
                g.CompositingMode = CompositingMode.SourceOver;

                var whole = new Rectangle(0, 0, SkinFile.Size, SkinFile.Size);

                foreach (var layer in Layers)
                {
                    if (!layer.Visible || layer.Image == null || layer.Opacity <= 0) continue;

                    if (layer.Opacity >= 100)
                    {
                        g.DrawImage(layer.Image, whole, 0, 0, SkinFile.Size, SkinFile.Size, GraphicsUnit.Pixel);
                        continue;
                    }

                    using (var attrs = new ImageAttributes())
                    {
                        var matrix = new ColorMatrix();
                        matrix.Matrix33 = layer.Opacity / 100f;
                        attrs.SetColorMatrix(matrix);
                        g.DrawImage(layer.Image, whole, 0, 0, SkinFile.Size, SkinFile.Size,
                                    GraphicsUnit.Pixel, attrs);
                    }
                }
            }
        }

        // ------------------------------------------------------------ слои

        public SkinLayer AddLayer()
        {
            var layer = new SkinLayer { Name = "Слой " + (Layers.Count + 1), Image = SkinFile.NewImage() };
            int at = Math.Min(Layers.Count, Math.Max(0, Active + 1));
            Layers.Insert(at, layer);
            Active = at;
            Rebuild();
            return layer;
        }

        public void DuplicateLayer()
        {
            if (Layers.Count == 0) return;

            var copy = Current.Clone();
            copy.Name = Current.Name + " (копия)";
            Layers.Insert(Active + 1, copy);
            Active++;
            Rebuild();
        }

        public bool RemoveLayer()
        {
            if (Layers.Count <= 1) return false;

            var layer = Layers[Active];
            Layers.RemoveAt(Active);
            if (layer.Image != null) layer.Image.Dispose();

            if (Active >= Layers.Count) Active = Layers.Count - 1;
            Rebuild();
            return true;
        }

        public bool MoveLayer(int delta)
        {
            int to = Active + delta;
            if (to < 0 || to >= Layers.Count) return false;

            var layer = Layers[Active];
            Layers.RemoveAt(Active);
            Layers.Insert(to, layer);
            Active = to;
            Rebuild();
            return true;
        }

        /// <summary>Сливает активный слой с тем, что под ним.</summary>
        public bool MergeDown()
        {
            if (Active <= 0 || Layers.Count < 2) return false;

            var upper = Layers[Active];
            var lower = Layers[Active - 1];

            using (var g = Graphics.FromImage(lower.Image))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.SmoothingMode = SmoothingMode.None;

                if (upper.Opacity >= 100)
                {
                    g.DrawImage(upper.Image, new Rectangle(0, 0, SkinFile.Size, SkinFile.Size),
                                0, 0, SkinFile.Size, SkinFile.Size, GraphicsUnit.Pixel);
                }
                else
                {
                    using (var attrs = new ImageAttributes())
                    {
                        var matrix = new ColorMatrix();
                        matrix.Matrix33 = upper.Opacity / 100f;
                        attrs.SetColorMatrix(matrix);
                        g.DrawImage(upper.Image, new Rectangle(0, 0, SkinFile.Size, SkinFile.Size),
                                    0, 0, SkinFile.Size, SkinFile.Size, GraphicsUnit.Pixel, attrs);
                    }
                }
            }

            Layers.RemoveAt(Active);
            upper.Image.Dispose();
            Active--;
            Rebuild();
            return true;
        }

        // ------------------------------------------------------------ файлы

        /// <summary>Готовая текстура для игры — только склейка, без слоёв.</summary>
        public void SavePng(string path)
        {
            Rebuild();
            Flat.Save(path);
        }

        /// <summary>Свой формат: слои целиком, чтобы работу можно было продолжить.</summary>
        public void SaveNare(string path)
        {
            Paths.EnsureForFile(path);

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(fs, Encoding.UTF8))
            {
                w.Write(Encoding.ASCII.GetBytes(Magic));
                w.Write(FormatVersion);
                w.Write((int)Arms);
                w.Write(Name ?? "skin");
                w.Write(Layers.Count);

                foreach (var layer in Layers)
                {
                    w.Write(layer.Name ?? "Слой");
                    w.Write(layer.Visible);
                    w.Write(layer.Opacity);

                    byte[] png = ToPng(layer.Image);
                    w.Write(png.Length);
                    w.Write(png);
                }
            }
        }

        public static SkinDocument LoadNare(string path)
        {
            using (var fs = File.OpenRead(path))
            using (var r = new BinaryReader(fs, Encoding.UTF8))
            {
                if (fs.Length < 16) throw new InvalidDataException("Файл пуст или повреждён.");

                string magic = Encoding.ASCII.GetString(r.ReadBytes(Magic.Length));
                if (magic != Magic)
                    throw new InvalidDataException("Это не файл " + Extension + ".");

                int version = r.ReadInt32();
                if (version > FormatVersion)
                    throw new InvalidDataException("Файл сделан более новой версией лаунчера (" + version + ").");

                var doc = new SkinDocument();
                doc.Arms = (SkinArms)r.ReadInt32();
                doc.Name = r.ReadString();

                int count = r.ReadInt32();
                if (count < 0 || count > 512) throw new InvalidDataException("Странное число слоёв: " + count);

                for (int i = 0; i < count; i++)
                {
                    var layer = new SkinLayer();
                    layer.Name = r.ReadString();
                    layer.Visible = r.ReadBoolean();
                    layer.Opacity = Math.Max(0, Math.Min(100, r.ReadInt32()));

                    int length = r.ReadInt32();
                    if (length < 0 || length > 8 * 1024 * 1024)
                        throw new InvalidDataException("Повреждён слой «" + layer.Name + "».");

                    byte[] png = r.ReadBytes(length);
                    using (var ms = new MemoryStream(png))
                    using (var bmp = new Bitmap(ms))
                        layer.Image = SkinFile.Normalize(bmp);

                    doc.Layers.Add(layer);
                }

                if (doc.Layers.Count == 0)
                    doc.Layers.Add(new SkinLayer { Name = "Основа", Image = SkinFile.NewImage() });

                doc.Active = doc.Layers.Count - 1;
                doc.Rebuild();
                return doc;
            }
        }

        /// <summary>Открывает и .nareskin, и обычный PNG — по расширению файла.</summary>
        public static SkinDocument Open(string path)
        {
            if (string.Equals(Path.GetExtension(path), Extension, StringComparison.OrdinalIgnoreCase))
                return LoadNare(path);

            var skin = SkinFile.FromFile(path);
            return FromSkin(skin);
        }

        private static byte[] ToPng(Bitmap image)
        {
            using (var ms = new MemoryStream())
            {
                (image ?? SkinFile.NewImage()).Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }
}
