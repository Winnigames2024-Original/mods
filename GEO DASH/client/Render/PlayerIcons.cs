using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using GEO_DASH.Core;

namespace GEO_DASH.Render
{
    /// <summary>
    /// Иконки игрока для всех режимов. Каждая рисуется один раз в картинку 64×64
    /// и дальше в игровом цикле только копируется — так кадр не тратится на фигуры.
    /// Кубик и шар берут рисунок из набора иконок, остальные режимы нарисованы здесь.
    /// </summary>
    public static class PlayerIcons
    {
        const int Size = 64;
        static readonly Dictionary<(GameMode, int, int, int, bool), Bitmap> Cache = new();

        static readonly Color Ink = Color.FromArgb(255, 8, 6, 20);

        /// <summary>Сбросить кэш — например, когда игрок поменял иконку.</summary>
        public static void Clear()
        {
            foreach (var b in Cache.Values) b.Dispose();
            Cache.Clear();
        }

        /// <summary>Готовая картинка режима в заданных цветах.</summary>
        public static Bitmap Get(GameMode mode, Color c1, Color c2, int shape = 1, bool custom = false)
        {
            var key = (mode, c1.ToArgb(), c2.ToArgb(), shape, custom);
            if (Cache.TryGetValue(key, out var bm)) return bm;

            bm = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bm))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                Draw(g, mode, new RectangleF(3, 3, Size - 6, Size - 6), c1, c2, shape);
            }

            if (Cache.Count > 64) Clear();
            Cache[key] = bm;
            return bm;
        }

        /// <summary>
        /// Нарисовать игрока: берём картинку из кэша, поворачиваем и,
        /// если гравитация перевёрнута, отражаем по вертикали.
        /// </summary>
        public static void DrawPlayer(Graphics g, GameMode mode, RectangleF bounds,
                                      float angle, bool flipped, Color c1, Color c2, int shape = 1)
        {
            var img = Get(mode, c1, c2, shape, IconKit.HasCustom);
            var st = g.Save();
            g.TranslateTransform(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            g.RotateTransform(angle);
            if (flipped) g.ScaleTransform(1, -1);
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.DrawImage(img, new RectangleF(-bounds.Width / 2, -bounds.Height / 2,
                                            bounds.Width, bounds.Height));
            g.Restore(st);
        }

        // ------------------------------------------------------------ фигуры

        static void Draw(Graphics g, GameMode mode, RectangleF r, Color c1, Color c2, int shape)
        {
            switch (mode)
            {
                case GameMode.Cube: IconKit.Draw(g, r, shape, c1, c2); break;
                case GameMode.Ball: IconKit.Draw(g, r, shape, c1, c2, round: true); break;
                case GameMode.Ship: Ship(g, r, c1, c2, shape); break;
                case GameMode.Ufo: Ufo(g, r, c1, c2, shape); break;
                case GameMode.Wave: Wave(g, r, c1, c2); break;
                case GameMode.Robot: Robot(g, r, c1, c2, shape); break;
                case GameMode.Spider: Spider(g, r, c1, c2); break;
                default: Worm(g, r, c1, c2); break;
            }
        }

        static Pen Outline(float w = 3f) => new(Ink, w) { LineJoin = LineJoin.Round };

        static Color Light(Color c) => Mix(c, Color.White, 0.45);
        static Color Dark(Color c) => Mix(c, Color.Black, 0.45);

        static Color Mix(Color a, Color b, double k) => Color.FromArgb(
            a.A, (int)(a.R + (b.R - a.R) * k), (int)(a.G + (b.G - a.G) * k), (int)(a.B + (b.B - a.B) * k));

        /// <summary>Светящийся визор — общая деталь всех иконок.</summary>
        static void Visor(Graphics g, RectangleF r, Color glow)
        {
            using var br = new LinearGradientBrush(r, Light(glow), glow, LinearGradientMode.Vertical);
            using var path = Round(r, r.Height * 0.45f);
            g.FillPath(br, path);
            using var pen = Outline(2.2f);
            g.DrawPath(pen, path);
        }

        static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            rad = Math.Max(1, Math.Min(rad, Math.Min(r.Width, r.Height) / 2));
            p.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
            p.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
            p.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
            p.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
            p.CloseFigure();
            return p;
        }

        // ---- корабль: обтекаемый корпус, крыло снизу, сопло сзади, мини-куб внутри
        static void Ship(Graphics g, RectangleF r, Color c1, Color c2, int shape)
        {
            float w = r.Width, h = r.Height;
            var body = new[]
            {
                new PointF(r.X - w * 0.02f, r.Y + h * 0.78f),
                new PointF(r.X + w * 0.22f, r.Y + h * 0.30f),
                new PointF(r.Right + w * 0.02f, r.Y + h * 0.46f),
                new PointF(r.X + w * 0.58f, r.Y + h * 0.84f)
            };
            using (var br = new LinearGradientBrush(r, Light(c2), Dark(c2), LinearGradientMode.Vertical))
                g.FillPolygon(br, body);
            using (var pen = Outline()) g.DrawPolygon(pen, body);

            // крыло
            var wing = new[]
            {
                new PointF(r.X + w * 0.12f, r.Y + h * 0.76f),
                new PointF(r.X + w * 0.52f, r.Y + h * 0.80f),
                new PointF(r.X + w * 0.30f, r.Bottom)
            };
            using (var br = new SolidBrush(Dark(c2))) g.FillPolygon(br, wing);
            using (var pen = Outline(2.2f)) g.DrawPolygon(pen, wing);

            // сопло
            var noz = new RectangleF(r.X - w * 0.06f, r.Y + h * 0.56f, w * 0.14f, h * 0.18f);
            using (var br = new SolidBrush(Color.FromArgb(255, 255, 190, 70))) g.FillEllipse(br, noz);
            using (var pen = Outline(2f)) g.DrawEllipse(pen, noz);

            // мини-куб пилота
            var cab = new RectangleF(r.X + w * 0.30f, r.Y + h * 0.28f, w * 0.34f, h * 0.34f);
            IconKit.Draw(g, cab, shape, c1, Dark(c1));
        }

        // ---- НЛО: купол с мини-кубом и тарелка с огнями
        static void Ufo(Graphics g, RectangleF r, Color c1, Color c2, int shape)
        {
            float w = r.Width, h = r.Height;

            var cab = new RectangleF(r.X + w * 0.30f, r.Y + h * 0.10f, w * 0.40f, h * 0.40f);
            IconKit.Draw(g, cab, shape, c1, Dark(c1));

            // стеклянный купол
            var dome = new RectangleF(r.X + w * 0.20f, r.Y + h * 0.02f, w * 0.60f, h * 0.58f);
            using (var br = new SolidBrush(Color.FromArgb(90, 210, 240, 255)))
                g.FillEllipse(br, dome);
            using (var pen = Outline(2.4f)) g.DrawEllipse(pen, dome);

            // тарелка
            var disk = new RectangleF(r.X - w * 0.04f, r.Y + h * 0.50f, w * 1.08f, h * 0.34f);
            using (var br = new LinearGradientBrush(disk, Light(c2), Dark(c2), LinearGradientMode.Vertical))
                g.FillEllipse(br, disk);
            using (var pen = Outline()) g.DrawEllipse(pen, disk);

            // огни снизу
            for (int i = 0; i < 3; i++)
            {
                var lamp = new RectangleF(r.X + w * (0.22f + i * 0.26f), r.Y + h * 0.70f, w * 0.12f, h * 0.12f);
                using var br = new SolidBrush(Color.FromArgb(255, 255, 220, 120));
                g.FillEllipse(br, lamp);
            }
        }

        // ---- волна: острая стрела с двойным контуром и ядром
        static void Wave(Graphics g, RectangleF r, Color c1, Color c2)
        {
            float w = r.Width, h = r.Height;
            var tri = new[]
            {
                new PointF(r.X + w * 0.06f, r.Y + h * 0.10f),
                new PointF(r.Right - w * 0.02f, r.Y + h * 0.50f),
                new PointF(r.X + w * 0.06f, r.Y + h * 0.90f)
            };
            using (var br = new LinearGradientBrush(r, Light(c1), Dark(c1), LinearGradientMode.Vertical))
                g.FillPolygon(br, tri);
            using (var pen = Outline()) g.DrawPolygon(pen, tri);

            var inner = new[]
            {
                new PointF(r.X + w * 0.22f, r.Y + h * 0.28f),
                new PointF(r.X + w * 0.74f, r.Y + h * 0.50f),
                new PointF(r.X + w * 0.22f, r.Y + h * 0.72f)
            };
            using (var pen = new Pen(c2, Math.Max(1.6f, w * 0.055f)) { LineJoin = LineJoin.Round })
                g.DrawPolygon(pen, inner);

            var core = new RectangleF(r.X + w * 0.30f, r.Y + h * 0.42f, w * 0.16f, h * 0.16f);
            using (var br = new SolidBrush(Light(c2))) g.FillEllipse(br, core);
        }

        // ---- робот: голова-куб с визором и две шарнирные ноги
        static void Robot(Graphics g, RectangleF r, Color c1, Color c2, int shape)
        {
            float w = r.Width, h = r.Height;

            var head = new RectangleF(r.X + w * 0.08f, r.Y, w * 0.84f, h * 0.56f);
            using (var br = new LinearGradientBrush(head, Light(c1), Dark(c1), LinearGradientMode.Vertical))
            using (var path = Round(head, w * 0.12f))
            {
                g.FillPath(br, path);
                using var pen = Outline();
                g.DrawPath(pen, path);
            }
            Visor(g, new RectangleF(r.X + w * 0.20f, r.Y + h * 0.16f, w * 0.60f, h * 0.18f), c2);

            // ноги: бедро и голень
            for (int i = 0; i < 2; i++)
            {
                float x = r.X + w * (i == 0 ? 0.18f : 0.56f);
                var hip = new RectangleF(x, r.Y + h * 0.56f, w * 0.26f, h * 0.20f);
                var shin = new RectangleF(x + w * 0.04f, r.Y + h * 0.74f, w * 0.18f, h * 0.26f);
                using var br = new SolidBrush(Dark(c1));
                using var pen = Outline(2.2f);
                using (var p1 = Round(hip, w * 0.06f)) { g.FillPath(br, p1); g.DrawPath(pen, p1); }
                using (var p2 = Round(shin, w * 0.05f)) { g.FillPath(br, p2); g.DrawPath(pen, p2); }
            }
        }

        // ---- паук: корпус с четырьмя острыми лапами
        static void Spider(Graphics g, RectangleF r, Color c1, Color c2)
        {
            float w = r.Width, h = r.Height;

            using (var pen = new Pen(Ink, Math.Max(2.4f, w * 0.085f)) { EndCap = LineCap.Round })
            {
                for (int i = -1; i <= 1; i += 2)
                    for (int k = 0; k < 2; k++)
                    {
                        float y = r.Y + h * (0.52f + k * 0.18f);
                        float kx = r.X + w / 2;
                        g.DrawLine(pen, kx, y, kx + i * w * (0.42f + k * 0.08f), y + h * 0.16f);
                        g.DrawLine(pen, kx + i * w * (0.42f + k * 0.08f), y + h * 0.16f,
                                        kx + i * w * (0.50f + k * 0.10f), r.Bottom);
                    }
            }

            var body = new RectangleF(r.X + w * 0.16f, r.Y + h * 0.10f, w * 0.68f, h * 0.52f);
            using (var br = new LinearGradientBrush(body, Light(c1), Dark(c1), LinearGradientMode.Vertical))
            using (var path = Round(body, w * 0.16f))
            {
                g.FillPath(br, path);
                using var pen = Outline();
                g.DrawPath(pen, path);
            }

            // два злых глаза
            for (int i = 0; i < 2; i++)
            {
                var eye = new RectangleF(r.X + w * (0.28f + i * 0.26f), r.Y + h * 0.26f, w * 0.18f, h * 0.14f);
                using var br = new SolidBrush(Light(c2));
                g.FillEllipse(br, eye);
                using var pen = Outline(1.8f);
                g.DrawEllipse(pen, eye);
            }
        }

        // ---- червяк: голова-бур с зубцами и сегменты тела к хвосту
        static void Worm(Graphics g, RectangleF r, Color c1, Color c2)
        {
            float w = r.Width, h = r.Height;

            // хвост: сегменты, уменьшающиеся назад
            for (int i = 3; i >= 1; i--)
            {
                float k = 1f - i * 0.17f;
                float d = h * 0.46f * k;
                float cx = r.X + w * (0.34f - i * 0.10f);
                float cy = r.Y + h * 0.5f + (i % 2 == 0 ? h * 0.05f : -h * 0.05f);
                var seg = new RectangleF(cx - d / 2, cy - d / 2, d, d);
                using var br = new SolidBrush(Mix(c1, Color.Black, 0.12 * i));
                using var path = Round(seg, d * 0.34f);
                g.FillPath(br, path);
                using var pen = Outline(2f);
                g.DrawPath(pen, path);
            }

            // голова-бур
            var head = new RectangleF(r.X + w * 0.28f, r.Y + h * 0.16f, w * 0.52f, h * 0.68f);
            using (var br = new LinearGradientBrush(head, Light(c1), Dark(c1), LinearGradientMode.Vertical))
            using (var path = Round(head, w * 0.18f))
            {
                g.FillPath(br, path);
                using var pen = Outline();
                g.DrawPath(pen, path);
            }

            // зубцы бура спереди
            using (var br = new SolidBrush(Light(c2)))
            using (var pen = Outline(1.8f))
            {
                for (int i = 0; i < 3; i++)
                {
                    float y = r.Y + h * (0.24f + i * 0.22f);
                    var tooth = new[]
                    {
                        new PointF(r.X + w * 0.78f, y),
                        new PointF(r.Right, y + h * 0.11f),
                        new PointF(r.X + w * 0.78f, y + h * 0.22f)
                    };
                    g.FillPolygon(br, tooth);
                    g.DrawPolygon(pen, tooth);
                }
            }

            // визор
            Visor(g, new RectangleF(r.X + w * 0.36f, r.Y + h * 0.36f, w * 0.34f, h * 0.16f), c2);
        }

        // ------------------------------------------------------------ портал червяка

        /// <summary>
        /// Портал режима «Червяк»: фиолетово-оранжевая рамка с бурильными зубцами.
        /// Рисуется прямо на экране, без кэша — порталов в кадре мало.
        /// </summary>
        public static void DrawWormPortal(Graphics g, RectangleF r)
        {
            var violet = Color.FromArgb(255, 150, 60, 255);
            var orange = Color.FromArgb(255, 255, 150, 40);

            using (var br = new LinearGradientBrush(r, violet, orange, LinearGradientMode.Vertical))
                g.FillEllipse(br, r);
            using (var pen = new Pen(Ink, Math.Max(2.5f, r.Width * 0.09f))) g.DrawEllipse(pen, r);

            var inner = new RectangleF(r.X + r.Width * 0.22f, r.Y + r.Height * 0.14f,
                                       r.Width * 0.56f, r.Height * 0.72f);
            using (var br = new SolidBrush(Color.FromArgb(190, 20, 10, 40))) g.FillEllipse(br, inner);
            using (var pen = new Pen(Color.FromArgb(230, 255, 210, 140), Math.Max(1.6f, r.Width * 0.05f)))
                g.DrawEllipse(pen, inner);

            // зубцы по кругу
            using (var br = new SolidBrush(orange))
            {
                float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
                float ra = r.Width * 0.46f, rb = r.Height * 0.46f;
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4;
                    float x = cx + (float)Math.Cos(a) * ra, y = cy + (float)Math.Sin(a) * rb;
                    g.FillEllipse(br, x - r.Width * 0.05f, y - r.Height * 0.05f,
                                  r.Width * 0.1f, r.Height * 0.1f);
                }
            }
        }
    }
}
