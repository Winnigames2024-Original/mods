using System.Drawing.Drawing2D;
using System.Drawing.Text;
using GEO_DASH.Core;

namespace GEO_DASH.UI
{
    public enum GdIcon
    {
        None, Play, Edit, Online, Profile, Friends, Settings, Admin, Music, Save, Back,
        Coin, Star, Lock, Download, Daily, Weekly, Search, Plus, Trash, Copy, Upload, Refresh, Info, Check, Cross
    }

    /// <summary>Палитра и общие рисовалки «жёсткого» интерфейса.</summary>
    public static class Gd
    {
        public static readonly Color Deep = Color.FromArgb(14, 12, 34);
        public static readonly Color Deep2 = Color.FromArgb(30, 18, 66);
        /// <summary>Цвет фона главного меню — тот самый синий.</summary>
        public static readonly Color BgBlue = Color.FromArgb(40, 125, 255);
        public static readonly Color GroundBlue = Color.FromArgb(0, 102, 255);
        public static readonly Color Neon = Color.FromArgb(0, 230, 255);
        public static readonly Color Magenta = Color.FromArgb(255, 60, 190);
        public static readonly Color Lime = Color.FromArgb(130, 255, 120);
        public static readonly Color Gold = Color.FromArgb(255, 205, 60);
        public static readonly Color Red = Color.FromArgb(255, 80, 90);
        public static readonly Color Ink = Color.FromArgb(10, 8, 24);
        public static readonly Color Paper = Color.FromArgb(240, 244, 255);

        public static Font Title(float size) => new("Segoe UI Black", size, FontStyle.Bold, GraphicsUnit.Point);
        public static Font Bold(float size) => new("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Point);
        public static Font Reg(float size) => new("Segoe UI", size, FontStyle.Regular, GraphicsUnit.Point);

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            rad = Math.Min(rad, Math.Min(r.Width, r.Height) / 2f);
            if (rad <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
            p.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
            p.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
            p.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>Текст с чёрной обводкой — читаемо на любом фоне.</summary>
        public static void Outlined(Graphics g, string text, Font f, RectangleF r, Color fill,
                                    StringAlignment h = StringAlignment.Center,
                                    StringAlignment v = StringAlignment.Center, float thick = 3.2f)
        {
            if (string.IsNullOrEmpty(text)) return;
            using var sf = new StringFormat { Alignment = h, LineAlignment = v, Trimming = StringTrimming.EllipsisCharacter };
            using var path = new GraphicsPath();
            var em = f.SizeInPoints * g.DpiY / 72f;
            path.AddString(text, f.FontFamily, (int)f.Style, em, r, sf);
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(Ink, thick) { LineJoin = LineJoin.Round })
                g.DrawPath(pen, path);
            using (var br = new SolidBrush(fill))
                g.FillPath(br, path);
            g.SmoothingMode = old;
        }

        /// <summary>Плита интерфейса: градиент, чёрная обводка, блик сверху и тёмная кромка снизу.</summary>
        public static void Plate(Graphics g, RectangleF r, Color a, Color b, float rad = 14f, float border = 3f)
        {
            if (r.Width <= 2 || r.Height <= 2) return;
            using var path = Round(r, rad);
            using (var br = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2),
                       a, b, LinearGradientMode.Vertical))
                g.FillPath(br, path);

            // тёмная кромка снизу — объём, как у кнопок в игре
            var st = g.Save();
            g.SetClip(path);
            var low = new RectangleF(r.X, r.Bottom - r.Height * 0.26f, r.Width, r.Height * 0.26f);
            using (var br = new LinearGradientBrush(new RectangleF(low.X, low.Y - 1, low.Width, low.Height + 2),
                       Color.FromArgb(0, 0, 0, 0), Color.FromArgb(90, 0, 0, 0), LinearGradientMode.Vertical))
                g.FillRectangle(br, low);
            g.Restore(st);

            // блик сверху
            var top = new RectangleF(r.X + border, r.Y + border * 0.8f, r.Width - border * 2, r.Height * 0.40f);
            if (top.Width > 2 && top.Height > 2)
            {
                using var hp = Round(top, Math.Max(2f, rad * 0.75f));
                using var hb = new LinearGradientBrush(new RectangleF(top.X, top.Y - 1, top.Width, top.Height + 2),
                    Color.FromArgb(70, 255, 255, 255), Color.FromArgb(12, 255, 255, 255), LinearGradientMode.Vertical);
                g.FillPath(hb, hp);
            }

            using (var pen = new Pen(Ink, border) { LineJoin = LineJoin.Round })
                g.DrawPath(pen, path);
        }

        /// <summary>Кнопка игры: та же плита плюс «нажатие» — она проваливается и темнеет.</summary>
        public static void ButtonPlate(Graphics g, RectangleF r, Color c1, Color c2, float rad, bool pressed, bool hover)
        {
            if (r.Width <= 2 || r.Height <= 2) return;
            if (!pressed)
            {
                using var sp = Round(new RectangleF(r.X, r.Y + Math.Max(3f, r.Height * 0.07f), r.Width, r.Height), rad);
                using var sb = new SolidBrush(Color.FromArgb(110, 0, 0, 0));
                g.FillPath(sb, sp);
            }
            var a = pressed ? Lighten(c1, -0.22) : Lighten(c1, hover ? 0.20 : 0.08);
            var b = pressed ? Lighten(c2, -0.22) : c2;
            Plate(g, r, a, b, rad, 3.4f);
        }

        /// <summary>Тёмная панель под списки и карточки — как окна в игре.</summary>
        public static void Glass(Graphics g, RectangleF r, float rad = 16f)
        {
            if (r.Width <= 2 || r.Height <= 2) return;
            using var path = Round(r, rad);
            using (var br = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2),
                       Color.FromArgb(205, 36, 26, 74), Color.FromArgb(215, 18, 13, 44), LinearGradientMode.Vertical))
                g.FillPath(br, path);
            using (var pen = new Pen(Color.FromArgb(70, 255, 255, 255), 2f))
                g.DrawPath(pen, Round(new RectangleF(r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6), Math.Max(2f, rad - 3)));
            using (var pen = new Pen(Ink, 3.2f) { LineJoin = LineJoin.Round })
                g.DrawPath(pen, path);
        }

        // ---------------------------------------------------- анимированный фон

        public static void Background(Graphics g, Rectangle r, double t, Color c1, Color c2)
            => Background(g, r, t, 1, 1, c1, c2);

        /// <summary>Фон экрана как в игре: ровный цвет с едущим узором и пол со своей текстурой.</summary>
        public static void Background(Graphics g, Rectangle r, double t, int bgId, int groundId, Color bg, Color ground)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            float scale = Math.Clamp(r.Height / 620f, 0.65f, 1.9f);

            GdBackdrop.DrawBackground(g, r, bgId, bg, t * 42, Math.Sin(t * 0.25) * 10, scale);

            float gh = Math.Max(56f, r.Height * 0.155f);
            GdBackdrop.DrawGround(g, new RectangleF(r.X, r.Bottom - gh, r.Width, gh),
                groundId, ground, Color.White, t * 170, scale);
        }

        // ---------------------------------------------------- иконки

        public static void Icon(Graphics g, GdIcon ic, RectangleF r, Color col)
        {
            if (ic == GdIcon.None) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var br = new SolidBrush(col);
            using var pen = new Pen(col, Math.Max(2f, r.Width * 0.11f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, s = Math.Min(r.Width, r.Height);

            switch (ic)
            {
                case GdIcon.Play:
                    g.FillPolygon(br, new[]
                    {
                        new PointF(cx - s * 0.26f, cy - s * 0.34f),
                        new PointF(cx + s * 0.34f, cy),
                        new PointF(cx - s * 0.26f, cy + s * 0.34f)
                    });
                    break;
                case GdIcon.Edit:
                    g.DrawLine(pen, cx - s * 0.28f, cy + s * 0.28f, cx + s * 0.22f, cy - s * 0.22f);
                    g.FillPolygon(br, new[]
                    {
                        new PointF(cx - s * 0.34f, cy + s * 0.34f),
                        new PointF(cx - s * 0.30f, cy + s * 0.12f),
                        new PointF(cx - s * 0.12f, cy + s * 0.30f)
                    });
                    break;
                case GdIcon.Online:
                    g.DrawEllipse(pen, cx - s * 0.3f, cy - s * 0.3f, s * 0.6f, s * 0.6f);
                    g.DrawLine(pen, cx - s * 0.3f, cy, cx + s * 0.3f, cy);
                    g.DrawArc(pen, cx - s * 0.15f, cy - s * 0.3f, s * 0.3f, s * 0.6f, 0, 360);
                    break;
                case GdIcon.Profile:
                    g.FillEllipse(br, cx - s * 0.18f, cy - s * 0.32f, s * 0.36f, s * 0.36f);
                    g.FillPie(br, cx - s * 0.32f, cy + s * 0.0f, s * 0.64f, s * 0.6f, 180, 180);
                    break;
                case GdIcon.Friends:
                    g.FillEllipse(br, cx - s * 0.34f, cy - s * 0.28f, s * 0.3f, s * 0.3f);
                    g.FillEllipse(br, cx + s * 0.06f, cy - s * 0.28f, s * 0.3f, s * 0.3f);
                    g.FillPie(br, cx - s * 0.44f, cy + s * 0.02f, s * 0.5f, s * 0.46f, 180, 180);
                    g.FillPie(br, cx - s * 0.04f, cy + s * 0.02f, s * 0.5f, s * 0.46f, 180, 180);
                    break;
                case GdIcon.Settings:
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        g.DrawLine(pen, (float)(cx + Math.Cos(a) * s * 0.18f), (float)(cy + Math.Sin(a) * s * 0.18f),
                                        (float)(cx + Math.Cos(a) * s * 0.34f), (float)(cy + Math.Sin(a) * s * 0.34f));
                    }
                    g.DrawEllipse(pen, cx - s * 0.16f, cy - s * 0.16f, s * 0.32f, s * 0.32f);
                    break;
                case GdIcon.Admin:
                    g.FillPolygon(br, new[]
                    {
                        new PointF(cx, cy - s * 0.34f), new PointF(cx + s * 0.32f, cy - s * 0.12f),
                        new PointF(cx + s * 0.22f, cy + s * 0.32f), new PointF(cx - s * 0.22f, cy + s * 0.32f),
                        new PointF(cx - s * 0.32f, cy - s * 0.12f)
                    });
                    break;
                case GdIcon.Music:
                    g.FillEllipse(br, cx - s * 0.3f, cy + s * 0.08f, s * 0.22f, s * 0.2f);
                    g.FillEllipse(br, cx + s * 0.06f, cy - s * 0.02f, s * 0.22f, s * 0.2f);
                    g.DrawLine(pen, cx - s * 0.09f, cy + s * 0.16f, cx - s * 0.09f, cy - s * 0.28f);
                    g.DrawLine(pen, cx + s * 0.27f, cy + s * 0.06f, cx + s * 0.27f, cy - s * 0.34f);
                    g.DrawLine(pen, cx - s * 0.09f, cy - s * 0.28f, cx + s * 0.27f, cy - s * 0.34f);
                    break;
                case GdIcon.Save:
                case GdIcon.Download:
                    g.DrawLine(pen, cx, cy - s * 0.3f, cx, cy + s * 0.16f);
                    g.DrawLines(pen, new[] { new PointF(cx - s * 0.18f, cy - s * 0.02f), new PointF(cx, cy + s * 0.18f), new PointF(cx + s * 0.18f, cy - s * 0.02f) });
                    g.DrawLine(pen, cx - s * 0.3f, cy + s * 0.32f, cx + s * 0.3f, cy + s * 0.32f);
                    break;
                case GdIcon.Upload:
                    g.DrawLine(pen, cx, cy + s * 0.3f, cx, cy - s * 0.16f);
                    g.DrawLines(pen, new[] { new PointF(cx - s * 0.18f, cy + s * 0.02f), new PointF(cx, cy - s * 0.18f), new PointF(cx + s * 0.18f, cy + s * 0.02f) });
                    break;
                case GdIcon.Back:
                    g.DrawLines(pen, new[] { new PointF(cx + s * 0.16f, cy - s * 0.26f), new PointF(cx - s * 0.16f, cy), new PointF(cx + s * 0.16f, cy + s * 0.26f) });
                    break;
                case GdIcon.Coin:
                    DrawCoin(g, new RectangleF(cx - s * 0.34f, cy - s * 0.34f, s * 0.68f, s * 0.68f), false);
                    break;
                case GdIcon.Star:
                    DrawStar(g, new RectangleF(cx - s * 0.36f, cy - s * 0.36f, s * 0.72f, s * 0.72f), Gold);
                    break;
                case GdIcon.Lock:
                    g.DrawArc(pen, cx - s * 0.18f, cy - s * 0.34f, s * 0.36f, s * 0.36f, 180, 180);
                    using (var p2 = Round(new RectangleF(cx - s * 0.26f, cy - s * 0.06f, s * 0.52f, s * 0.4f), s * 0.08f))
                        g.FillPath(br, p2);
                    break;
                case GdIcon.Daily:
                case GdIcon.Weekly:
                    using (var p2 = Round(new RectangleF(cx - s * 0.3f, cy - s * 0.28f, s * 0.6f, s * 0.58f), s * 0.07f))
                        g.DrawPath(pen, p2);
                    g.DrawLine(pen, cx - s * 0.3f, cy - s * 0.1f, cx + s * 0.3f, cy - s * 0.1f);
                    if (ic == GdIcon.Weekly) g.DrawLine(pen, cx, cy - s * 0.1f, cx, cy + s * 0.3f);
                    break;
                case GdIcon.Search:
                    g.DrawEllipse(pen, cx - s * 0.3f, cy - s * 0.3f, s * 0.44f, s * 0.44f);
                    g.DrawLine(pen, cx + s * 0.12f, cy + s * 0.12f, cx + s * 0.32f, cy + s * 0.32f);
                    break;
                case GdIcon.Plus:
                    g.DrawLine(pen, cx - s * 0.26f, cy, cx + s * 0.26f, cy);
                    g.DrawLine(pen, cx, cy - s * 0.26f, cx, cy + s * 0.26f);
                    break;
                case GdIcon.Trash:
                    g.DrawRectangle(pen, cx - s * 0.2f, cy - s * 0.18f, s * 0.4f, s * 0.46f);
                    g.DrawLine(pen, cx - s * 0.3f, cy - s * 0.24f, cx + s * 0.3f, cy - s * 0.24f);
                    break;
                case GdIcon.Copy:
                    g.DrawRectangle(pen, cx - s * 0.28f, cy - s * 0.3f, s * 0.38f, s * 0.44f);
                    g.DrawRectangle(pen, cx - s * 0.1f, cy - s * 0.14f, s * 0.38f, s * 0.44f);
                    break;
                case GdIcon.Refresh:
                    g.DrawArc(pen, cx - s * 0.28f, cy - s * 0.28f, s * 0.56f, s * 0.56f, 30, 280);
                    g.FillPolygon(br, new[] { new PointF(cx + s * 0.3f, cy - s * 0.3f), new PointF(cx + s * 0.08f, cy - s * 0.2f), new PointF(cx + s * 0.32f, cy - s * 0.02f) });
                    break;
                case GdIcon.Info:
                    g.DrawEllipse(pen, cx - s * 0.3f, cy - s * 0.3f, s * 0.6f, s * 0.6f);
                    g.DrawLine(pen, cx, cy - s * 0.04f, cx, cy + s * 0.18f);
                    g.FillEllipse(br, cx - s * 0.04f, cy - s * 0.2f, s * 0.08f, s * 0.08f);
                    break;
                case GdIcon.Check:
                    g.DrawLines(pen, new[] { new PointF(cx - s * 0.26f, cy), new PointF(cx - s * 0.06f, cy + s * 0.22f), new PointF(cx + s * 0.28f, cy - s * 0.24f) });
                    break;
                case GdIcon.Cross:
                    g.DrawLine(pen, cx - s * 0.22f, cy - s * 0.22f, cx + s * 0.22f, cy + s * 0.22f);
                    g.DrawLine(pen, cx + s * 0.22f, cy - s * 0.22f, cx - s * 0.22f, cy + s * 0.22f);
                    break;
            }
        }

        public static void DrawStar(Graphics g, RectangleF r, Color col)
        {
            var pts = new PointF[10];
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, R = Math.Min(r.Width, r.Height) / 2;
            for (int i = 0; i < 10; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / 5;
                double rr = i % 2 == 0 ? R : R * 0.46;
                pts[i] = new PointF((float)(cx + Math.Cos(a) * rr), (float)(cy + Math.Sin(a) * rr));
            }
            using var br = new SolidBrush(col);
            g.FillPolygon(br, pts);
            using var pen = new Pen(Ink, Math.Max(1.4f, R * 0.14f)) { LineJoin = LineJoin.Round };
            g.DrawPolygon(pen, pts);
        }

        public static void DrawCoin(Graphics g, RectangleF r, bool got)
        {
            var c = got ? Gold : Color.FromArgb(90, 90, 110);
            using (var br = new LinearGradientBrush(r, Lighten(c, 0.4), c, LinearGradientMode.Vertical))
                g.FillEllipse(br, r);
            using (var pen = new Pen(Ink, Math.Max(1.4f, r.Width * 0.12f)))
                g.DrawEllipse(pen, r);
            using (var pen = new Pen(Color.FromArgb(200, 255, 255, 255), Math.Max(1f, r.Width * 0.08f)))
                g.DrawArc(pen, r.X + r.Width * 0.22f, r.Y + r.Height * 0.2f, r.Width * 0.56f, r.Height * 0.6f, 150, 120);
        }

        /// <summary>«Лицо» сложности — свой рисунок, по принципу: чем сложнее, тем злее.</summary>
        public static void DrawFace(Graphics g, RectangleF r, Difficulty d)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var col = Difficulties.Tint(d);
            using (var br = new LinearGradientBrush(r, Lighten(col, 0.35), col, LinearGradientMode.Vertical))
                g.FillEllipse(br, r);
            using (var pen = new Pen(Ink, Math.Max(2f, r.Width * 0.09f)))
                g.DrawEllipse(pen, r);

            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, s = r.Width;
            using var ink = new SolidBrush(Ink);
            using var pen2 = new Pen(Ink, Math.Max(2f, s * 0.07f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };

            bool demon = Difficulties.IsDemon(d);
            float ey = cy - s * 0.08f;
            if (demon)
            {
                // рога и злые глаза
                g.FillPolygon(ink, new[] { new PointF(r.X + s * 0.12f, r.Y + s * 0.18f), new PointF(r.X + s * 0.3f, r.Y + s * 0.02f), new PointF(r.X + s * 0.34f, r.Y + s * 0.22f) });
                g.FillPolygon(ink, new[] { new PointF(r.Right - s * 0.12f, r.Y + s * 0.18f), new PointF(r.Right - s * 0.3f, r.Y + s * 0.02f), new PointF(r.Right - s * 0.34f, r.Y + s * 0.22f) });
                g.FillPolygon(ink, new[] { new PointF(cx - s * 0.26f, ey - s * 0.04f), new PointF(cx - s * 0.06f, ey + s * 0.04f), new PointF(cx - s * 0.26f, ey + s * 0.1f) });
                g.FillPolygon(ink, new[] { new PointF(cx + s * 0.26f, ey - s * 0.04f), new PointF(cx + s * 0.06f, ey + s * 0.04f), new PointF(cx + s * 0.26f, ey + s * 0.1f) });
                g.DrawArc(pen2, cx - s * 0.2f, cy + s * 0.14f, s * 0.4f, s * 0.22f, 0, 180);
            }
            else
            {
                g.FillEllipse(ink, cx - s * 0.22f, ey - s * 0.05f, s * 0.11f, s * 0.13f);
                g.FillEllipse(ink, cx + s * 0.11f, ey - s * 0.05f, s * 0.11f, s * 0.13f);
                if (d <= Difficulty.Normal) g.DrawArc(pen2, cx - s * 0.2f, cy + s * 0.0f, s * 0.4f, s * 0.26f, 0, 180);
                else if (d <= Difficulty.Harder) g.DrawLine(pen2, cx - s * 0.18f, cy + s * 0.18f, cx + s * 0.18f, cy + s * 0.18f);
                else g.DrawArc(pen2, cx - s * 0.2f, cy + s * 0.12f, s * 0.4f, s * 0.26f, 180, 180);
            }
        }

        public static Color Lighten(Color c, double k)
        {
            int f(int v) => Math.Clamp((int)(v + (k > 0 ? (255 - v) * k : v * k)), 0, 255);
            return Color.FromArgb(c.A, f(c.R), f(c.G), f(c.B));
        }
    }

    /// <summary>Кнопка в стиле игры: плита с обводкой, иконка, подпись, подпрыгивает при наведении.</summary>
    public class GdButton : Control
    {
        public GdIcon Icon = GdIcon.None;
        public Color Color1 = Color.FromArgb(70, 120, 255);
        public Color Color2 = Color.FromArgb(30, 50, 160);
        public float Radius = 14f;
        public float FontSize = 11f;
        public bool Circle;
        public string Sub = "";
        /// <summary>Подпись под иконкой для кнопок без текста.</summary>
        public string Caption = "";
        bool _hover, _down;
        float _anim;
        readonly System.Windows.Forms.Timer _t = new() { Interval = 16 };

        public GdButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(200, 52);
            _t.Tick += (s, e) =>
            {
                float target = _hover ? 1f : 0f;
                _anim += (target - _anim) * 0.30f;
                if (Math.Abs(target - _anim) < 0.01f) { _anim = target; _t.Stop(); }
                Invalidate();
            };
            MouseEnter += (s, e) => { _hover = true; _t.Start(); };
            MouseLeave += (s, e) => { _hover = false; _down = false; _t.Start(); };
            MouseDown += (s, e) => { _down = true; _t.Start(); };
            MouseUp += (s, e) => { _down = false; _t.Start(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            // кнопка «проваливается» при нажатии и чуть растёт под курсором — как в игре
            float k = _down ? 0.93f : 1f + Math.Max(0, _anim) * 0.045f;
            float w = (Width - 5) * k, h = (Height - 8) * k;
            var r = new RectangleF((Width - w) / 2f, (Height - h) / 2f + (_down ? 3f : 0f), w, h);
            if (r.Width <= 2 || r.Height <= 2) return;

            float rad = Circle ? r.Height / 2 : Math.Min(Radius, Math.Min(r.Width, r.Height) * 0.28f);
            Gd.ButtonPlate(g, r, Color1, Color2, rad, _down, _hover);

            bool iconOnly = string.IsNullOrEmpty(Text);
            bool hasCaption = iconOnly && !string.IsNullOrEmpty(Caption);
            if (Icon != GdIcon.None)
            {
                float s = Math.Min(r.Height * (iconOnly ? (hasCaption ? 0.50f : 0.62f) : 0.56f), r.Width * 0.6f);
                var ir = iconOnly
                    ? new RectangleF(r.X + r.Width / 2 - s / 2,
                                     r.Y + r.Height * (hasCaption ? 0.08f : 0.5f) - (hasCaption ? 0 : s / 2), s, s)
                    : new RectangleF(r.X + 12, r.Y + r.Height / 2 - s / 2, s, s);
                Gd.Icon(g, Icon, ir, Gd.Paper);
            }

            // подпись под картинкой — что это за кнопка
            if (hasCaption)
            {
                bool hasSub = !string.IsNullOrEmpty(Sub);
                float fs = Math.Clamp(Math.Min(r.Height * 0.115f, r.Width * 0.095f), 6.5f, 14f);
                using var fc = Gd.Title(fs);
                float ch = fs * 1.9f;
                float cy = hasSub ? r.Bottom - ch - fs * 1.5f : r.Bottom - r.Height * 0.32f;
                Gd.Outlined(g, Caption, fc, new RectangleF(r.X + 3, cy, r.Width - 6, ch), Gd.Paper, thick: 2.6f);
                if (hasSub)
                {
                    using var fsb = Gd.Bold(Math.Max(6f, fs * 0.66f));
                    Gd.Outlined(g, Sub, fsb, new RectangleF(r.X + 3, cy + ch - 2, r.Width - 6, fs * 1.5f),
                        Color.FromArgb(225, 235, 240, 255), thick: 2.1f);
                }
            }

            if (!iconOnly)
            {
                float tx = Icon == GdIcon.None ? r.X : r.X + Math.Min(r.Height * 0.62f, r.Width * 0.6f) + 14;
                var tr = new RectangleF(tx, r.Y + (string.IsNullOrEmpty(Sub) ? 0 : -r.Height * 0.08f),
                                        r.Right - tx - 10, r.Height);
                using var f = Gd.Title(FontSize);
                Gd.Outlined(g, Text, f, tr, Gd.Paper,
                    Icon == GdIcon.None ? StringAlignment.Center : StringAlignment.Near);
                if (!string.IsNullOrEmpty(Sub))
                {
                    using var f2 = Gd.Bold(FontSize * 0.72f);
                    var sr = new RectangleF(tx, r.Y + r.Height * 0.52f, r.Right - tx - 10, r.Height * 0.4f);
                    Gd.Outlined(g, Sub, f2, sr, Color.FromArgb(220, 230, 240, 255),
                        Icon == GdIcon.None ? StringAlignment.Center : StringAlignment.Near, StringAlignment.Center, 2.4f);
                }
            }
        }
    }

    /// <summary>Большая круглая кнопка PLAY с треугольником.</summary>
    public class GdPlayButton : Control
    {
        bool _hover;
        double _t;
        readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };

        public GdPlayButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(190, 190);
            _timer.Tick += (s, e) => { _t += 0.016; Invalidate(); };
            _timer.Start();
            MouseEnter += (s, e) => _hover = true;
            MouseLeave += (s, e) => { _hover = false; _down = false; };
            MouseDown += (s, e) => _down = true;
            MouseUp += (s, e) => _down = false;
        }

        bool _down;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float pulse = (float)(1 + Math.Sin(_t * 2.2) * 0.02) * (_down ? 0.93f : _hover ? 1.05f : 1f);
            float size = Math.Min(Width, Height) * 0.88f * pulse;
            var r = new RectangleF(Width / 2f - size / 2, Height / 2f - size / 2 + (_down ? 4 : 0), size, size);

            // тень под кнопкой
            if (!_down)
                using (var sb = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                    g.FillEllipse(sb, r.X, r.Y + size * 0.06f, r.Width, r.Height);

            using (var br = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2),
                       Color.FromArgb(150, 255, 190), Color.FromArgb(10, 150, 95), LinearGradientMode.Vertical))
                g.FillEllipse(br, r);

            // верхний блик и нижняя тень внутри — объём
            using (var br = new SolidBrush(Color.FromArgb(80, 255, 255, 255)))
                g.FillPie(br, r.X + size * 0.08f, r.Y + size * 0.05f, r.Width - size * 0.16f, r.Height * 0.62f, 180, 180);
            using (var br = new SolidBrush(Color.FromArgb(60, 0, 40, 20)))
                g.FillPie(br, r.X + size * 0.06f, r.Y + size * 0.35f, r.Width - size * 0.12f, r.Height * 0.6f, 0, 180);

            using (var pen = new Pen(Gd.Ink, 6f)) g.DrawEllipse(pen, r);
            using (var pen = new Pen(Color.FromArgb(120, 255, 255, 255), 3f))
                g.DrawEllipse(pen, r.X + 7, r.Y + 7, r.Width - 14, r.Height - 14);

            float s = size * 0.34f;
            var cx = r.X + r.Width / 2 + s * 0.08f;
            var cy = r.Y + r.Height / 2;
            var tri = new[]
            {
                new PointF(cx - s * 0.55f, cy - s),
                new PointF(cx + s * 0.85f, cy),
                new PointF(cx - s * 0.55f, cy + s)
            };
            using (var br = new SolidBrush(Gd.Paper)) g.FillPolygon(br, tri);
            using (var pen = new Pen(Gd.Ink, 4f) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, tri);
        }
    }

    /// <summary>Панель с двойной буферизацией — на ней строятся все экраны.</summary>
    public class GdPanel : Panel
    {
        public GdPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Gd.Deep;
        }
    }

    /// <summary>
    /// Окно-оболочка: вся начинка экрана лежит на панели <see cref="Content"/>,
    /// фон и заголовок рисуются на ней же.
    /// </summary>
    public class GdForm : Form
    {
        protected double T;
        readonly System.Windows.Forms.Timer _anim = new() { Interval = 33 };
        /// <summary>Цвет фона экрана.</summary>
        public Color BgA = Gd.BgBlue;
        /// <summary>Цвет пола; если не задан — считается по цвету фона.</summary>
        public Color BgB = Color.Empty;
        /// <summary>Номер узора фона (1..20) и пола (1..10) — как в игре.</summary>
        public int BgId = 1, GroundId = 1;

        /// <summary>Панель-основа экрана: сюда добавляются все контролы.</summary>
        protected readonly GdPanel Content = new() { Dock = DockStyle.Fill };

        public GdForm()
        {
            BackColor = Gd.Deep;
            ForeColor = Gd.Paper;
            Font = Gd.Reg(10f);
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;

            Controls.Add(Content);
            Content.Paint += (s, e) =>
            {
                Gd.Background(e.Graphics, Content.ClientRectangle, T, BgId, GroundId,
                    BgA, BgB.A == 0 ? GdBackdrop.GroundOf(BgA) : BgB);
                OnRender(e.Graphics);
            };

            _anim.Tick += (s, e) => { T += 0.033; Content.Invalidate(false); };
            Shown += (s, e) => _anim.Start();
            FormClosed += (s, e) => _anim.Stop();
        }

        /// <summary>
        /// Слоёная отрисовка окна: дочерние кнопки и картинки больше не мигают
        /// (фон и контролы попадают на экран одним куском).
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;   // WS_EX_COMPOSITED
                return cp;
            }
        }

        /// <summary>Открыть другой экран: внутри оболочки — в панели MainForm, иначе окном.</summary>
        protected void Open(Form f) => Nav.Go(f);

        /// <summary>Закрыть экран: встроенный — вернуться назад, обычный — закрыть окно.</summary>
        protected void CloseScreen()
        {
            if (Nav.IsEmbedded(this)) Nav.Back();
            else Close();
        }

        /// <summary>Рисование экрана поверх фона (вместо OnPaint формы).</summary>
        protected virtual void OnRender(Graphics g) { }

        protected void Header(Graphics g, string title, string sub = "")
        {
            using var f = Gd.Title(26f);
            Gd.Outlined(g, title, f, new RectangleF(0, 14, ClientSize.Width, 44), Gd.Paper);
            if (!string.IsNullOrEmpty(sub))
            {
                using var f2 = Gd.Bold(10f);
                Gd.Outlined(g, sub, f2, new RectangleF(0, 58, ClientSize.Width, 22), Color.FromArgb(220, 190, 210, 255), thick: 2.2f);
            }
        }

        /// <summary>Добавить контрол на панель экрана.</summary>
        protected T2 Host<T2>(T2 c) where T2 : Control
        {
            Content.Controls.Add(c);
            c.BringToFront();
            return c;
        }

        protected GdButton Add(string text, GdIcon ic, int x, int y, int w, int h, Color c1, Color c2, EventHandler click, string sub = "")
        {
            var b = new GdButton
            {
                Text = text, Icon = ic, Left = x, Top = y, Width = w, Height = h,
                Color1 = c1, Color2 = c2, Sub = sub
            };
            if (click != null) b.Click += click;
            return Host(b);
        }
    }
}
