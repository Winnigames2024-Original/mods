using System.Drawing.Drawing2D;

namespace GEO_DASH.Render
{
    /// <summary>
    /// Внешний вид игрока: встроенные рисунки кубика и своя картинка.
    /// Рисунок один и тот же в игре, в меню и в профиле.
    /// </summary>
    public static class IconKit
    {
        /// <summary>Сколько встроенных рисунков.</summary>
        public const int Count = 16;

        public static string Name(int id) => id switch
        {
            1 => "Классика",
            2 => "Рамка",
            3 => "Углы",
            4 => "Полосы",
            5 => "Шахматы",
            6 => "Глаз",
            7 => "Стрела",
            8 => "Звезда",
            9 => "Кольцо",
            10 => "Клык",
            11 => "Крест",
            12 => "Улыбка",
            13 => "Пиксель",
            14 => "Молния",
            15 => "Сердце",
            16 => "Череп",
            _ => "Рисунок " + id
        };

        static Image _custom;
        static string _customKey = "";

        /// <summary>Своя картинка для кубика: файл на диске или картинка из toolbox.</summary>
        public static void SetCustom(string key, Image img)
        {
            if (_customKey == key) return;
            _custom?.Dispose();
            _custom = img;
            _customKey = key ?? "";
        }

        public static bool HasCustom => _custom != null;
        public static void ClearCustom() { _custom?.Dispose(); _custom = null; _customKey = ""; }

        /// <summary>
        /// Нарисовать кубик игрока: подложка цветом c1, рисунок цветом c2, чёрная обводка.
        /// Если выбрана своя картинка — она ложится поверх подложки.
        /// </summary>
        public static void Draw(Graphics g, RectangleF r, int shape, Color c1, Color c2, bool round = false)
        {
            if (r.Width <= 1 || r.Height <= 1) return;
            float w = r.Width, h = r.Height;
            Color ink = Color.FromArgb(235, 8, 6, 20);
            float pen = Math.Max(1.6f, w * 0.10f);

            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // подложка
            using (var path = Body(r, round))
            {
                using (var br = new LinearGradientBrush(
                           new RectangleF(r.X, r.Y - 1, w, h + 2),
                           Blend(c1, Color.White, 0.18), Blend(c1, Color.Black, 0.22), LinearGradientMode.Vertical))
                    g.FillPath(br, path);
            }

            // своя картинка вместо рисунка
            if (_custom != null)
            {
                var st = g.Save();
                using (var clip = Body(Inflate(r, -pen * 0.5f), round))
                    g.SetClip(clip);
                g.DrawImage(_custom, Inflate(r, -pen * 0.5f));
                g.Restore(st);
            }
            else Pattern(g, r, shape, c2);

            // блик сверху и обводка — как в игре
            using (var br = new SolidBrush(Color.FromArgb(55, 255, 255, 255)))
                g.FillRectangle(br, r.X + w * 0.1f, r.Y + h * 0.08f, w * 0.8f, h * 0.16f);
            using (var path = Body(r, round))
            using (var p2 = new Pen(ink, pen) { LineJoin = LineJoin.Round })
                g.DrawPath(p2, path);

            g.SmoothingMode = old;
        }

        static GraphicsPath Body(RectangleF r, bool round)
        {
            var p = new GraphicsPath();
            if (round) p.AddEllipse(r);
            else
            {
                float rad = Math.Max(2f, r.Width * 0.14f);
                p.AddArc(r.X, r.Y, rad, rad, 180, 90);
                p.AddArc(r.Right - rad, r.Y, rad, rad, 270, 90);
                p.AddArc(r.Right - rad, r.Bottom - rad, rad, rad, 0, 90);
                p.AddArc(r.X, r.Bottom - rad, rad, rad, 90, 90);
                p.CloseFigure();
            }
            return p;
        }

        static void Pattern(Graphics g, RectangleF r, int shape, Color c)
        {
            float w = r.Width, h = r.Height, cx = r.X + w / 2, cy = r.Y + h / 2;
            using var br = new SolidBrush(c);
            using var pen = new Pen(c, Math.Max(1.4f, w * 0.12f)) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };

            switch (Math.Clamp(shape, 1, Count))
            {
                case 1:   // классика — квадрат внутри
                    g.FillRectangle(br, Inflate(r, -w * 0.3f));
                    break;
                case 2:   // рамка
                    g.DrawRectangle(pen, r.X + w * 0.22f, r.Y + h * 0.22f, w * 0.56f, h * 0.56f);
                    break;
                case 3:   // углы
                    foreach (var (sx, sy) in new[] { (0f, 0f), (1f, 0f), (0f, 1f), (1f, 1f) })
                        g.FillRectangle(br, r.X + (w * 0.16f) + sx * w * 0.45f, r.Y + (h * 0.16f) + sy * h * 0.45f,
                                        w * 0.23f, h * 0.23f);
                    break;
                case 4:   // полосы
                    for (int i = 0; i < 3; i++)
                        g.FillRectangle(br, r.X + w * 0.18f, r.Y + h * (0.22f + i * 0.22f), w * 0.64f, h * 0.11f);
                    break;
                case 5:   // шахматы
                    for (int y = 0; y < 3; y++)
                        for (int x = 0; x < 3; x++)
                            if ((x + y) % 2 == 0)
                                g.FillRectangle(br, r.X + w * (0.18f + x * 0.22f), r.Y + h * (0.18f + y * 0.22f),
                                                w * 0.2f, h * 0.2f);
                    break;
                case 6:   // глаз
                    g.FillEllipse(br, r.X + w * 0.2f, r.Y + h * 0.32f, w * 0.6f, h * 0.36f);
                    using (var b2 = new SolidBrush(Color.FromArgb(230, 10, 8, 24)))
                        g.FillEllipse(b2, cx - w * 0.1f, cy - h * 0.1f, w * 0.2f, h * 0.2f);
                    break;
                case 7:   // стрела
                    g.FillPolygon(br, new[]
                    {
                        new PointF(r.X + w * 0.24f, r.Y + h * 0.2f),
                        new PointF(r.X + w * 0.78f, cy),
                        new PointF(r.X + w * 0.24f, r.Y + h * 0.8f)
                    });
                    break;
                case 8:   // звезда
                    g.FillPolygon(br, Star(cx, cy, w * 0.38f, w * 0.17f, 5));
                    break;
                case 9:   // кольцо
                    g.DrawEllipse(pen, r.X + w * 0.22f, r.Y + h * 0.22f, w * 0.56f, h * 0.56f);
                    break;
                case 10:  // клык
                    g.FillPolygon(br, new[]
                    {
                        new PointF(r.X + w * 0.2f, r.Y + h * 0.24f),
                        new PointF(r.X + w * 0.8f, r.Y + h * 0.24f),
                        new PointF(cx, r.Y + h * 0.82f)
                    });
                    break;
                case 11:  // крест
                    g.DrawLine(pen, r.X + w * 0.26f, r.Y + h * 0.26f, r.X + w * 0.74f, r.Y + h * 0.74f);
                    g.DrawLine(pen, r.X + w * 0.74f, r.Y + h * 0.26f, r.X + w * 0.26f, r.Y + h * 0.74f);
                    break;
                case 12:  // улыбка
                    g.FillEllipse(br, r.X + w * 0.26f, r.Y + h * 0.28f, w * 0.12f, h * 0.14f);
                    g.FillEllipse(br, r.X + w * 0.62f, r.Y + h * 0.28f, w * 0.12f, h * 0.14f);
                    g.DrawArc(pen, r.X + w * 0.26f, r.Y + h * 0.4f, w * 0.48f, h * 0.36f, 20, 140);
                    break;
                case 13:  // пиксель
                    for (int y = 0; y < 4; y++)
                        for (int x = 0; x < 4; x++)
                            if (((x * 7 + y * 3) % 5) < 2)
                                g.FillRectangle(br, r.X + w * (0.14f + x * 0.18f), r.Y + h * (0.14f + y * 0.18f),
                                                w * 0.16f, h * 0.16f);
                    break;
                case 14:  // молния
                    g.FillPolygon(br, new[]
                    {
                        new PointF(cx + w * 0.08f, r.Y + h * 0.16f),
                        new PointF(r.X + w * 0.3f, cy + h * 0.04f),
                        new PointF(cx, cy + h * 0.04f),
                        new PointF(cx - w * 0.06f, r.Y + h * 0.84f),
                        new PointF(r.X + w * 0.72f, cy - h * 0.04f),
                        new PointF(cx + w * 0.02f, cy - h * 0.04f)
                    });
                    break;
                case 15:  // сердце
                    {
                        var p = new GraphicsPath();
                        p.AddEllipse(r.X + w * 0.22f, r.Y + h * 0.26f, w * 0.3f, h * 0.3f);
                        p.AddEllipse(r.X + w * 0.48f, r.Y + h * 0.26f, w * 0.3f, h * 0.3f);
                        p.AddPolygon(new[]
                        {
                            new PointF(r.X + w * 0.22f, r.Y + h * 0.46f),
                            new PointF(cx, r.Y + h * 0.82f),
                            new PointF(r.X + w * 0.78f, r.Y + h * 0.46f)
                        });
                        g.FillPath(br, p);
                        p.Dispose();
                        break;
                    }
                default:  // череп
                    g.FillEllipse(br, r.X + w * 0.2f, r.Y + h * 0.2f, w * 0.6f, h * 0.46f);
                    g.FillRectangle(br, r.X + w * 0.32f, r.Y + h * 0.6f, w * 0.36f, h * 0.18f);
                    using (var b2 = new SolidBrush(Color.FromArgb(235, 10, 8, 24)))
                    {
                        g.FillEllipse(b2, r.X + w * 0.3f, r.Y + h * 0.33f, w * 0.14f, h * 0.16f);
                        g.FillEllipse(b2, r.X + w * 0.56f, r.Y + h * 0.33f, w * 0.14f, h * 0.16f);
                    }
                    break;
            }
        }

        static PointF[] Star(float cx, float cy, float rOut, float rIn, int points)
        {
            var pts = new PointF[points * 2];
            for (int i = 0; i < pts.Length; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / points;
                float rad = i % 2 == 0 ? rOut : rIn;
                pts[i] = new PointF(cx + (float)(Math.Cos(a) * rad), cy + (float)(Math.Sin(a) * rad));
            }
            return pts;
        }

        static RectangleF Inflate(RectangleF r, float d) =>
            new(r.X - d, r.Y - d, Math.Max(1, r.Width + d * 2), Math.Max(1, r.Height + d * 2));

        static Color Blend(Color a, Color b, double k) => Color.FromArgb(
            a.A,
            (int)(a.R + (b.R - a.R) * k),
            (int)(a.G + (b.G - a.G) * k),
            (int)(a.B + (b.B - a.B) * k));
    }
}
