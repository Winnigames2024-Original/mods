using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using GEO_DASH.Core;

namespace GEO_DASH.Render
{
    /// <summary>
    /// Готовые картинки блоков и шипов. Самые частые объекты рисуются один раз
    /// в Bitmap и дальше только копируются — в кадре не создаётся ни кисти,
    /// ни пера. На уровне из тысяч блоков это и даёт 120+ кадров.
    /// </summary>
    public static class TileCache
    {
        readonly struct Key : IEquatable<Key>
        {
            public readonly DrawStyle Style;
            public readonly int Color, Detail, W, H;

            public Key(DrawStyle s, Color c, Color d, int w, int h)
            { Style = s; Color = c.ToArgb(); Detail = d.ToArgb(); W = w; H = h; }

            public bool Equals(Key o) => Style == o.Style && Color == o.Color
                                         && Detail == o.Detail && W == o.W && H == o.H;
            public override bool Equals(object o) => o is Key k && Equals(k);
            public override int GetHashCode() => HashCode.Combine((int)Style, Color, Detail, W, H);
        }

        static readonly Dictionary<Key, Bitmap> Cache = new();

        /// <summary>Максимальный размер тайла, который имеет смысл кэшировать.</summary>
        const int MaxSide = 220;

        /// <summary>Эти фигуры простые и повторяются тысячами — их и кэшируем.</summary>
        public static bool Cacheable(DrawStyle s) =>
            s is DrawStyle.Block or DrawStyle.Slab or DrawStyle.Brick or DrawStyle.Glass
              or DrawStyle.Spike or DrawStyle.SpikeSmall or DrawStyle.SpikeHalf;

        public static void Clear()
        {
            foreach (var b in Cache.Values) b.Dispose();
            Cache.Clear();
        }

        /// <summary>
        /// Нарисовать объект из кэша. Возвращает false, если такой объект
        /// кэшировать не стоит — тогда рисуем его как раньше.
        /// </summary>
        public static bool Draw(Graphics g, DrawStyle style, Color c, Color detail, RectangleF r)
        {
            if (!Cacheable(style)) return false;

            int w = (int)Math.Round(r.Width), h = (int)Math.Round(r.Height);
            if (w < 2 || h < 2 || w > MaxSide || h > MaxSide) return false;

            var key = new Key(style, c, detail, w, h);
            if (!Cache.TryGetValue(key, out var bm))
            {
                if (Cache.Count > 900) Clear();
                bm = Make(style, c, detail, w, h);
                Cache[key] = bm;
            }

            // тайлы уже в нужном размере: копируем как есть, без пересчёта
            var oldPix = g.PixelOffsetMode;
            g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            g.DrawImageUnscaled(bm, (int)Math.Round(r.X), (int)Math.Round(r.Y));
            g.PixelOffsetMode = oldPix;
            return true;
        }

        static Bitmap Make(DrawStyle style, Color c, Color detail, int w, int h)
        {
            var bm = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(bm);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var r = new RectangleF(0.5f, 0.5f, w - 1, h - 1);
            float line = Math.Max(1.5f, h * 0.09f);

            using var fill = new SolidBrush(c);
            using var edge = new Pen(detail, line);

            switch (style)
            {
                case DrawStyle.Block:
                case DrawStyle.Slab:
                    g.FillRectangle(fill, r);
                    g.DrawRectangle(edge, r.X, r.Y, r.Width, r.Height);
                    break;

                case DrawStyle.Brick:
                    g.FillRectangle(fill, r);
                    g.DrawRectangle(edge, r.X, r.Y, r.Width, r.Height);
                    using (var thin = new Pen(Color.FromArgb(detail.A / 2, detail), Math.Max(1f, line * 0.5f)))
                    {
                        g.DrawLine(thin, r.X, r.Y + r.Height / 2, r.Right, r.Y + r.Height / 2);
                        g.DrawLine(thin, r.X + r.Width / 2, r.Y, r.X + r.Width / 2, r.Y + r.Height / 2);
                        g.DrawLine(thin, r.X + r.Width / 4, r.Y + r.Height / 2, r.X + r.Width / 4, r.Bottom);
                    }
                    break;

                case DrawStyle.Glass:
                    using (var br = new SolidBrush(Color.FromArgb(Math.Min(140, (int)c.A), c)))
                        g.FillRectangle(br, r);
                    g.DrawRectangle(edge, r.X, r.Y, r.Width, r.Height);
                    break;

                case DrawStyle.Spike:
                    Spike(g, r, fill, edge, 1f);
                    break;
                case DrawStyle.SpikeSmall:
                    Spike(g, new RectangleF(r.X, r.Y + r.Height * 0.5f, r.Width, r.Height * 0.5f), fill, edge, 1f);
                    break;
                case DrawStyle.SpikeHalf:
                    Spike(g, new RectangleF(r.X + r.Width * 0.25f, r.Y + r.Height * 0.35f,
                                            r.Width * 0.5f, r.Height * 0.65f), fill, edge, 1f);
                    break;
            }
            return bm;
        }

        static void Spike(Graphics g, RectangleF r, Brush fill, Pen edge, float k)
        {
            var p = new[]
            {
                new PointF(r.X, r.Bottom),
                new PointF(r.X + r.Width / 2, r.Y),
                new PointF(r.Right, r.Bottom)
            };
            g.FillPolygon(fill, p);
            g.DrawPolygon(edge, p);
        }
    }
}
