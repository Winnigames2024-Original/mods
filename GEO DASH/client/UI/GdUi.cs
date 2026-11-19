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

        public static void Plate(Graphics g, RectangleF r, Color a, Color b, float rad = 14f, float border = 3f)
        {
            using var path = Round(r, rad);
            using (var br = new LinearGradientBrush(r, a, b, LinearGradientMode.Vertical))
                g.FillPath(br, path);
            using (var pen = new Pen(Ink, border) { LineJoin = LineJoin.Round })
                g.DrawPath(pen, path);
            // блик
            var top = new RectangleF(r.X + border, r.Y + border, r.Width - border * 2, r.Height * 0.32f);
            using var hp = Round(top, rad * 0.7f);
            using var hb = new SolidBrush(Color.FromArgb(46, 255, 255, 255));
            g.FillPath(hb, hp);
        }

        public static void Glass(Graphics g, RectangleF r, float rad = 16f)
        {
            using var path = Round(r, rad);
            using (var br = new SolidBrush(Color.FromArgb(150, 16, 14, 40)))
                g.FillPath(br, path);
            using (var pen = new Pen(Color.FromArgb(120, 140, 170, 255), 2f))
                g.DrawPath(pen, path);
        }

        // ---------------------------------------------------- анимированный фон

        public static void Background(Graphics g, Rectangle r, double t, Color c1, Color c2)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (var br = new LinearGradientBrush(r, c1, c2, LinearGradientMode.ForwardDiagonal))
                g.FillRectangle(br, r);

            // «пол» как в игре
            int floor = (int)(r.Height * 0.82);
            using (var br = new LinearGradientBrush(new Rectangle(0, floor, r.Width, r.Height - floor),
                       Color.FromArgb(200, 20, 16, 54), Color.FromArgb(255, 8, 6, 22), LinearGradientMode.Vertical))
                g.FillRectangle(br, new Rectangle(0, floor, r.Width, r.Height - floor));
            using (var pen = new Pen(Color.FromArgb(180, Neon), 3f))
                g.DrawLine(pen, 0, floor, r.Width, floor);

            float step = 86;
            float off = (float)(-(t * 90) % step);
            using (var pen = new Pen(Color.FromArgb(26, 255, 255, 255), 2f))
                for (float x = off; x < r.Width + step; x += step)
                    g.DrawLine(pen, x, 0, x + 140, floor);

            using (var pen = new Pen(Color.FromArgb(60, Neon), 2f))
                for (float x = off; x < r.Width + step; x += step)
                    g.DrawLine(pen, x, floor, x - 80, r.Height);

            // парящие фигуры
            for (int i = 0; i < 7; i++)
            {
                double p = (t * 0.25 + i * 0.37) % 1.0;
                float x = (float)(r.Width * (1.1 - p * 1.25));
                float y = (float)(r.Height * (0.12 + 0.52 * ((i * 0.17 + Math.Sin(t * 0.5 + i) * 0.1 + 1) % 1)));
                float s = 18 + i % 3 * 12;
                using var br = new SolidBrush(Color.FromArgb(30, i % 2 == 0 ? Neon : Magenta));
                if (i % 3 == 0) g.FillEllipse(br, x, y, s, s);
                else if (i % 3 == 1) g.FillRectangle(br, x, y, s, s);
                else g.FillPolygon(br, new[] { new PointF(x, y + s), new PointF(x + s, y + s), new PointF(x + s / 2, y) });
            }
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
                float target = _down ? -1f : (_hover ? 1f : 0f);
                _anim += (target - _anim) * 0.28f;
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

            float pop = _anim * 3f;
            var r = new RectangleF(2 - pop * 0.3f, 3 - pop, Width - 4 + pop * 0.6f, Height - 6 + pop);
            if (r.Width <= 2 || r.Height <= 2) return;

            // тень
            using (var sp = Gd.Round(new RectangleF(r.X + 2, r.Y + 5, r.Width, r.Height), Circle ? r.Height / 2 : Radius))
            using (var sb = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
                g.FillPath(sb, sp);

            Gd.Plate(g, r, Gd.Lighten(Color1, _hover ? 0.18 : 0.04), Color2, Circle ? r.Height / 2 : Radius, 3.2f);

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
                using var fc = Gd.Title(Math.Max(6.5f, Math.Min(9.5f, r.Width * 0.085f)));
                Gd.Outlined(g, Caption, fc,
                    new RectangleF(r.X + 2, r.Bottom - r.Height * 0.34f, r.Width - 4, r.Height * 0.3f),
                    Gd.Paper, thick: 2.4f);
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
            MouseLeave += (s, e) => _hover = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float pulse = (float)(1 + Math.Sin(_t * 2.4) * 0.025) * (_hover ? 1.06f : 1f);
            float size = Math.Min(Width, Height) * 0.9f * pulse;
            var r = new RectangleF(Width / 2f - size / 2, Height / 2f - size / 2, size, size);

            for (int i = 3; i >= 1; i--)
            {
                using var pen = new Pen(Color.FromArgb(30 + i * 10, Gd.Neon), 3f);
                var rr = r; rr.Inflate(i * 7f, i * 7f);
                g.DrawEllipse(pen, rr);
            }

            using (var br = new LinearGradientBrush(r, Color.FromArgb(120, 255, 170), Color.FromArgb(20, 160, 110), LinearGradientMode.Vertical))
                g.FillEllipse(br, r);
            using (var pen = new Pen(Gd.Ink, 5f)) g.DrawEllipse(pen, r);
            using (var br = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
                g.FillPie(br, r.X + 6, r.Y + 6, r.Width - 12, r.Height - 12, 190, 160);

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
        public Color BgA = Gd.Deep2, BgB = Gd.Deep;

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
                Gd.Background(e.Graphics, Content.ClientRectangle, T, BgA, BgB);
                OnRender(e.Graphics);
            };

            _anim.Tick += (s, e) => { T += 0.033; Content.Invalidate(false); };
            Shown += (s, e) => _anim.Start();
            FormClosed += (s, e) => _anim.Stop();
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
