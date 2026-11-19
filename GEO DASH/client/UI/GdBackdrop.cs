using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace GEO_DASH.UI
{
    /// <summary>
    /// Фон и пол как в Geometry Dash: ровный цвет, поверх — повторяющийся узор,
    /// который едет за камерой, снизу пол со своей текстурой и яркой линией.
    /// Узор рисуется один раз в маленькую картинку-маску и дальше просто тиражируется.
    /// </summary>
    public static class GdBackdrop
    {
        public const int BgCount = 20;
        public const int GroundCount = 10;
        const int BgTile = 128;
        const int GrTile = 64;

        /// <summary>Классический синий фон меню.</summary>
        public static readonly Color MenuBg = Color.FromArgb(40, 125, 255);
        public static readonly Color MenuGround = Color.FromArgb(0, 102, 255);

        static readonly Dictionary<int, Bitmap> _bg = new();
        static readonly Dictionary<int, Bitmap> _gr = new();

        public static Color Mix(Color a, Color b, double k)
        {
            k = Math.Clamp(k, 0, 1);
            return Color.FromArgb(a.A,
                (int)(a.R + (b.R - a.R) * k),
                (int)(a.G + (b.G - a.G) * k),
                (int)(a.B + (b.B - a.B) * k));
        }

        /// <summary>Цвет пола по цвету фона — как в игре, темнее и насыщеннее.</summary>
        public static Color GroundOf(Color bg) => Mix(bg, Color.FromArgb(0, 0, 20), 0.42);

        // ------------------------------------------------------------ маски

        static Bitmap Mask(Dictionary<int, Bitmap> cache, int id, int size, Action<Graphics, int, int> paint)
        {
            if (cache.TryGetValue(id, out var b) && b != null) return b;
            b = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                paint(g, id, size);
            }
            cache[id] = b;
            return b;
        }

        static void Dots(Graphics g, int s, int step, float r, bool fill, float thick = 6f)
        {
            using var br = new SolidBrush(Color.White);
            using var pen = new Pen(Color.White, thick);
            for (int y = step / 2; y < s; y += step)
                for (int x = step / 2; x < s; x += step)
                {
                    var rr = new RectangleF(x - r, y - r, r * 2, r * 2);
                    if (fill) g.FillEllipse(br, rr); else g.DrawEllipse(pen, rr);
                }
        }

        static void PaintBg(Graphics g, int id, int s)
        {
            using var br = new SolidBrush(Color.White);
            using var pen = new Pen(Color.White, 8f) { LineJoin = LineJoin.Round, StartCap = LineCap.Square, EndCap = LineCap.Square };

            switch (((id - 1) % BgCount + BgCount) % BgCount)
            {
                case 0: // широкие диагональные полосы
                    pen.Width = 18;
                    for (float x = -s; x <= s * 2; x += 36) g.DrawLine(pen, x - 4, -4, x + s + 4, s + 4);
                    break;
                case 1: // крупная клетка
                    pen.Width = 7;
                    for (int i = 0; i <= s; i += 64) { g.DrawLine(pen, i, -2, i, s + 2); g.DrawLine(pen, -2, i, s + 2, i); }
                    break;
                case 2: // круги
                    Dots(g, s, 64, 21, true);
                    break;
                case 3: // шеврон
                    pen.Width = 10;
                    for (int y = 0; y <= s; y += 32)
                        for (int x = -64; x <= s; x += 64)
                            g.DrawLines(pen, new[] { new PointF(x, y + 16), new PointF(x + 32, y - 8), new PointF(x + 64, y + 16) });
                    break;
                case 4: // кирпич
                    pen.Width = 6;
                    for (int y = 0; y <= s; y += 32)
                    {
                        g.DrawLine(pen, -2, y, s + 2, y);
                        int off = (y / 32) % 2 == 0 ? 0 : 32;
                        for (int x = off; x <= s; x += 64) g.DrawLine(pen, x, y, x, y + 32);
                    }
                    break;
                case 5: // треугольники
                    for (int y = 0; y <= s; y += 64)
                        for (int x = -32; x <= s; x += 64)
                            g.FillPolygon(br, new[] { new PointF(x, y + 48), new PointF(x + 32, y + 8), new PointF(x + 64, y + 48) });
                    break;
                case 6: // мелкая сетка по диагонали
                    pen.Width = 4;
                    for (float x = -s; x <= s * 2; x += 24) { g.DrawLine(pen, x - 4, -4, x + s + 4, s + 4); g.DrawLine(pen, x + s + 4, -4, x - 4, s + 4); }
                    break;
                case 7: // соты
                    pen.Width = 5;
                    Dots(g, s, 42, 17, false, 5f);
                    break;
                case 8: // кольца
                    pen.Width = 7;
                    for (int c = 0; c < 5; c++)
                    {
                        float cx = c == 4 ? s / 2f : (c % 2 == 0 ? 0 : s);
                        float cy = c == 4 ? s / 2f : (c < 2 ? 0 : s);
                        for (float r = 18; r <= 56; r += 19) g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
                    }
                    break;
                case 9: // вертикальные полосы
                    pen.Width = 22;
                    for (int x = 0; x <= s; x += 64) g.DrawLine(pen, x, -2, x, s + 2);
                    break;
                case 10: // шахматка
                    for (int y = 0; y < s; y += 32)
                        for (int x = 0; x < s; x += 32)
                            if (((x / 32) + (y / 32)) % 2 == 0) g.FillRectangle(br, x, y, 32, 32);
                    break;
                case 11: // звёздочки
                    for (int y = 32; y < s; y += 64)
                        for (int x = 32; x < s; x += 64)
                        {
                            var pts = new PointF[8];
                            for (int i = 0; i < 8; i++)
                            {
                                double a = -Math.PI / 2 + i * Math.PI / 4;
                                double rr = i % 2 == 0 ? 22 : 8;
                                pts[i] = new PointF((float)(x + Math.Cos(a) * rr), (float)(y + Math.Sin(a) * rr));
                            }
                            g.FillPolygon(br, pts);
                        }
                    break;
                case 12: // волны
                    pen.Width = 8;
                    for (int y = 0; y <= s; y += 32)
                    {
                        var pts = new PointF[33];
                        for (int i = 0; i <= 32; i++)
                            pts[i] = new PointF(i * (s / 32f), (float)(y + Math.Sin(i / 32.0 * Math.PI * 2) * 11));
                        g.DrawLines(pen, pts);
                    }
                    break;
                case 13: // сетка плюс диагональ
                    pen.Width = 4;
                    for (int i = 0; i <= s; i += 32) { g.DrawLine(pen, i, -2, i, s + 2); g.DrawLine(pen, -2, i, s + 2, i); }
                    pen.Width = 12;
                    for (float x = -s; x <= s * 2; x += 64) g.DrawLine(pen, x - 4, -4, x + s + 4, s + 4);
                    break;
                case 14: // чешуя
                    pen.Width = 7;
                    for (int y = 0; y <= s; y += 32)
                        for (int x = -32; x <= s; x += 32)
                            g.DrawArc(pen, x, y - 16, 32, 32, 0, 180);
                    break;
                case 15: // ромбы
                    for (int y = 0; y <= s; y += 64)
                        for (int x = 0; x <= s; x += 64)
                            g.FillPolygon(br, new[]
                            {
                                new PointF(x, y - 24), new PointF(x + 24, y), new PointF(x, y + 24), new PointF(x - 24, y)
                            });
                    break;
                case 16: // плитка со скруглением
                    for (int y = 0; y <= s; y += 64)
                        for (int x = 0; x <= s; x += 64)
                            using (var p = Gd.Round(new RectangleF(x - 22, y - 22, 44, 44), 10))
                                g.FillPath(br, p);
                    break;
                case 17: // тонкие обратные диагонали
                    pen.Width = 5;
                    for (float x = -s; x <= s * 2; x += 20) g.DrawLine(pen, x + s + 4, -4, x - 4, s + 4);
                    break;
                case 18: // кресты
                    pen.Width = 9;
                    for (int y = 32; y < s; y += 64)
                        for (int x = 32; x < s; x += 64)
                        {
                            g.DrawLine(pen, x - 18, y, x + 18, y);
                            g.DrawLine(pen, x, y - 18, x, y + 18);
                        }
                    break;
                default: // блоки «пикселями»
                    var rnd = new Random(7);
                    for (int y = 0; y < s; y += 16)
                        for (int x = 0; x < s; x += 16)
                            if (rnd.Next(100) < 34)
                                using (var b2 = new SolidBrush(Color.FromArgb(120 + rnd.Next(135), 255, 255, 255)))
                                    g.FillRectangle(b2, x, y, 16, 16);
                    break;
            }
        }

        static void PaintGround(Graphics g, int id, int s)
        {
            using var br = new SolidBrush(Color.White);
            using var pen = new Pen(Color.White, 5f) { StartCap = LineCap.Square, EndCap = LineCap.Square };

            switch (((id - 1) % GroundCount + GroundCount) % GroundCount)
            {
                case 0: // классика: клетка с внутренним квадратом
                    pen.Width = 5;
                    for (int i = 0; i <= s; i += 32) { g.DrawLine(pen, i, -2, i, s + 2); g.DrawLine(pen, -2, i, s + 2, i); }
                    pen.Width = 3;
                    for (int y = 0; y < s; y += 32)
                        for (int x = 0; x < s; x += 32)
                            g.DrawRectangle(pen, x + 9, y + 9, 14, 14);
                    break;
                case 1: // диагонали
                    pen.Width = 8;
                    for (float x = -s; x <= s * 2; x += 24) g.DrawLine(pen, x - 2, -2, x + s + 2, s + 2);
                    break;
                case 2: // вертикальные плашки
                    for (int x = 0; x <= s; x += 32) g.FillRectangle(br, x - 6, -2, 12, s + 4);
                    break;
                case 3: // кирпич
                    pen.Width = 5;
                    for (int y = 0; y <= s; y += 32)
                    {
                        g.DrawLine(pen, -2, y, s + 2, y);
                        int off = (y / 32) % 2 == 0 ? 0 : 16;
                        for (int x = off; x <= s; x += 32) g.DrawLine(pen, x, y, x, y + 32);
                    }
                    break;
                case 4: // шахматка
                    for (int y = 0; y < s; y += 16)
                        for (int x = 0; x < s; x += 16)
                            if (((x / 16) + (y / 16)) % 2 == 0) g.FillRectangle(br, x, y, 16, 16);
                    break;
                case 5: // точки
                    Dots(g, s, 32, 9, true);
                    break;
                case 6: // зигзаг
                    pen.Width = 7;
                    for (int y = 0; y <= s; y += 32)
                        for (int x = -32; x <= s; x += 32)
                            g.DrawLines(pen, new[] { new PointF(x, y + 16), new PointF(x + 16, y), new PointF(x + 32, y + 16) });
                    break;
                case 7: // крупные плиты
                    pen.Width = 7;
                    for (int i = 0; i <= s; i += 64) { g.DrawLine(pen, i, -2, i, s + 2); g.DrawLine(pen, -2, i, s + 2, i); }
                    break;
                case 8: // мелкая сетка
                    pen.Width = 3;
                    for (int i = 0; i <= s; i += 16) { g.DrawLine(pen, i, -2, i, s + 2); g.DrawLine(pen, -2, i, s + 2, i); }
                    break;
                default: // косые кресты
                    pen.Width = 6;
                    for (int y = 0; y <= s; y += 32)
                        for (int x = 0; x <= s; x += 32)
                        {
                            g.DrawLine(pen, x, y, x + 32, y + 32);
                            g.DrawLine(pen, x + 32, y, x, y + 32);
                        }
                    break;
            }
        }

        // ------------------------------------------------------------ тиражирование

        static void FillTiled(Graphics g, RectangleF area, Bitmap mask, Color tint, float offX, float offY, float scale)
        {
            if (area.Width <= 0 || area.Height <= 0 || tint.A <= 2) return;
            var m = new ColorMatrix(new[]
            {
                new[] { 0f, 0f, 0f, 0f, 0f },
                new[] { 0f, 0f, 0f, 0f, 0f },
                new[] { 0f, 0f, 0f, 0f, 0f },
                new[] { 0f, 0f, 0f, tint.A / 255f, 0f },
                new[] { tint.R / 255f, tint.G / 255f, tint.B / 255f, 0f, 1f }
            });
            using var attr = new ImageAttributes();
            attr.SetColorMatrix(m);
            attr.SetWrapMode(WrapMode.Tile);
            using var brush = new TextureBrush(mask, new Rectangle(0, 0, mask.Width, mask.Height), attr)
            {
                WrapMode = WrapMode.Tile
            };
            brush.ScaleTransform(scale, scale);
            brush.TranslateTransform(offX, offY);
            var old = g.PixelOffsetMode;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.FillRectangle(brush, area);
            g.PixelOffsetMode = old;
        }

        static float Wrap(double v, float period)
        {
            if (period <= 0.01f) return 0;
            float r = (float)(v % period);
            return r > 0 ? r - period : r;
        }

        // ------------------------------------------------------------ фон

        /// <summary>Фон экрана: ровный цвет + узор, который едет влево.</summary>
        public static void DrawBackground(Graphics g, RectangleF area, int bgId, Color bg,
                                          double scrollX, double scrollY, float scale)
        {
            if (area.Width <= 0 || area.Height <= 0) return;
            using (var br = new LinearGradientBrush(
                       new RectangleF(area.X, area.Y - 1, area.Width, area.Height + 2),
                       Mix(bg, Color.White, 0.10), Mix(bg, Color.Black, 0.18), LinearGradientMode.Vertical))
                g.FillRectangle(br, area);

            int idx = ((bgId - 1) % BgCount + BgCount) % BgCount;
            var mask = Mask(_bg, idx + 1, BgTile, PaintBg);
            bool light = idx % 3 == 2;
            var tint = light
                ? Color.FromArgb(46, Mix(bg, Color.White, 0.75))
                : Color.FromArgb(70, Mix(bg, Color.Black, 0.55));

            float period = BgTile * scale;
            FillTiled(g, area, mask, tint, Wrap(-scrollX, period) / Math.Max(0.01f, scale),
                      Wrap(scrollY, period) / Math.Max(0.01f, scale), scale);

            // мягкое затемнение к низу — объекты читаются лучше
            using var dark = new LinearGradientBrush(
                new RectangleF(area.X, area.Y + area.Height * 0.45f, area.Width, area.Height * 0.55f + 1),
                Color.FromArgb(0, 0, 0, 0), Color.FromArgb(70, 0, 0, 20), LinearGradientMode.Vertical);
            g.FillRectangle(dark, new RectangleF(area.X, area.Y + area.Height * 0.45f, area.Width, area.Height * 0.55f));
        }

        /// <summary>
        /// Пол: яркая линия сверху, светлая кромка, текстура и затемнение вниз.
        /// <paramref name="flip"/> — потолок (всё зеркально).
        /// </summary>
        public static void DrawGround(Graphics g, RectangleF area, int groundId, Color col, Color line,
                                      double scrollX, float scale, bool flip = false)
        {
            if (area.Width <= 0 || area.Height <= 0) return;

            float edge = Math.Max(2f, 4f * scale);
            float lineW = Math.Max(2f, 3.2f * scale);

            using (var br = new LinearGradientBrush(
                       new RectangleF(area.X, area.Y - 1, area.Width, area.Height + 2),
                       flip ? Mix(col, Color.Black, 0.55) : col,
                       flip ? col : Mix(col, Color.Black, 0.55),
                       LinearGradientMode.Vertical))
                g.FillRectangle(br, area);

            int idx = ((groundId - 1) % GroundCount + GroundCount) % GroundCount;
            var mask = Mask(_gr, idx + 1, GrTile, PaintGround);
            float period = GrTile * scale;
            FillTiled(g, area, mask, Color.FromArgb(70, Mix(col, Color.Black, 0.6)),
                      Wrap(-scrollX, period) / Math.Max(0.01f, scale), 0, scale);

            float top = flip ? area.Bottom - edge : area.Y;
            using (var br = new SolidBrush(Mix(col, Color.White, 0.45)))
                g.FillRectangle(br, area.X, top, area.Width, edge);

            float ly = flip ? area.Bottom - edge - lineW / 2 : area.Y + lineW / 2;
            using (var pen = new Pen(line, lineW))
                g.DrawLine(pen, area.X, ly, area.Right, ly);

            // тень под кромкой
            float sh = Math.Max(3f, 10f * scale);
            var shr = flip
                ? new RectangleF(area.X, area.Bottom - edge - sh, area.Width, sh)
                : new RectangleF(area.X, area.Y + edge, area.Width, sh);
            using (var br = new LinearGradientBrush(
                       new RectangleF(shr.X, shr.Y - 1, shr.Width, shr.Height + 2),
                       flip ? Color.FromArgb(0, 0, 0, 0) : Color.FromArgb(110, 0, 0, 0),
                       flip ? Color.FromArgb(110, 0, 0, 0) : Color.FromArgb(0, 0, 0, 0),
                       LinearGradientMode.Vertical))
                g.FillRectangle(br, shr);
        }
    }
}
