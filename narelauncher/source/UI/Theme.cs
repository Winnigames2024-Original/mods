using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace NareLauncher.UI
{
    public enum Ico
    {
        Play, Versions, Mods, Account, Settings, Console,
        Minimize, Close, Folder, Download, Trash, Refresh, Search, Plus, Check, Arrow, Stop, Skin
    }

    public static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(10, 12, 17);
        public static readonly Color Sidebar = Color.FromArgb(14, 17, 24);
        public static readonly Color Panel = Color.FromArgb(18, 22, 31);
        public static readonly Color Card = Color.FromArgb(23, 28, 39);
        public static readonly Color CardHover = Color.FromArgb(30, 36, 50);
        public static readonly Color Border = Color.FromArgb(38, 45, 60);
        public static readonly Color Text = Color.FromArgb(232, 238, 248);
        public static readonly Color Muted = Color.FromArgb(135, 146, 168);
        public static readonly Color Dim = Color.FromArgb(94, 104, 124);
        public static readonly Color Accent = Color.FromArgb(94, 134, 255);
        public static readonly Color Accent2 = Color.FromArgb(155, 92, 255);
        public static readonly Color Good = Color.FromArgb(63, 191, 127);
        public static readonly Color Bad = Color.FromArgb(255, 92, 92);
        public static readonly Color Warn = Color.FromArgb(240, 180, 70);

        private static readonly Dictionary<string, Font> FontCache = new Dictionary<string, Font>();

        public static Font F(float size, FontStyle style = FontStyle.Regular)
        {
            string key = size.ToString("0.##") + "/" + (int)style;
            Font f;
            if (FontCache.TryGetValue(key, out f)) return f;

            string family = "Segoe UI";
            try { using (var test = new Font(family, size, style)) { } }
            catch { family = FontFamily.GenericSansSerif.Name; }

            f = new Font(family, size, style, GraphicsUnit.Point);
            FontCache[key] = f;
            return f;
        }

        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0 || r.Width <= 0 || r.Height <= 0)
            {
                path.AddRectangle(r);
                return path;
            }
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color color)
        {
            using (var p = Round(r, radius))
            using (var b = new SolidBrush(color))
                g.FillPath(b, p);
        }

        public static void FillRoundGradient(Graphics g, Rectangle r, int radius, Color a, Color b, float angle = 0f)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (var p = Round(r, radius))
            using (var brush = new LinearGradientBrush(r, a, b, angle))
                g.FillPath(brush, p);
        }

        public static void DrawRound(Graphics g, Rectangle r, int radius, Color color, float width = 1f)
        {
            using (var p = Round(r, radius))
            using (var pen = new Pen(color, width))
                g.DrawPath(pen, p);
        }

        public static void Text1(Graphics g, string text, Font font, Color color, Rectangle r,
                                 StringAlignment h = StringAlignment.Near,
                                 StringAlignment v = StringAlignment.Center,
                                 bool ellipsis = true)
        {
            if (string.IsNullOrEmpty(text)) return;
            using (var sf = new StringFormat())
            using (var b = new SolidBrush(color))
            {
                sf.Alignment = h;
                sf.LineAlignment = v;
                sf.Trimming = ellipsis ? StringTrimming.EllipsisCharacter : StringTrimming.None;
                sf.FormatFlags |= StringFormatFlags.NoWrap;
                g.DrawString(text, font, b, r, sf);
            }
        }

        public static void TextWrap(Graphics g, string text, Font font, Color color, Rectangle r)
        {
            if (string.IsNullOrEmpty(text)) return;
            using (var sf = new StringFormat())
            using (var b = new SolidBrush(color))
            {
                sf.Trimming = StringTrimming.EllipsisWord;
                g.DrawString(text, font, b, r, sf);
            }
        }

        public static Color Mix(Color a, Color b, float t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        // -------------------------------------------------------------- иконки

        public static void DrawIcon(Graphics g, Ico icon, Rectangle box, Color color, float stroke = 1.8f)
        {
            Smooth(g);
            using (var pen = new Pen(color, stroke))
            using (var brush = new SolidBrush(color))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                float x = box.X, y = box.Y, w = box.Width, h = box.Height;
                float cx = x + w / 2f, cy = y + h / 2f;

                switch (icon)
                {
                    case Ico.Play:
                        {
                            var pts = new[]
                            {
                                new PointF(x + w * 0.26f, y + h * 0.16f),
                                new PointF(x + w * 0.84f, cy),
                                new PointF(x + w * 0.26f, y + h * 0.84f)
                            };
                            g.FillPolygon(brush, pts);
                            break;
                        }
                    case Ico.Stop:
                        g.FillRectangle(brush, x + w * 0.24f, y + h * 0.24f, w * 0.52f, h * 0.52f);
                        break;

                    case Ico.Versions:
                        {
                            float s = w * 0.34f, gap = w * 0.12f;
                            g.DrawRectangle(pen, x + w * 0.12f, y + h * 0.12f, s, s);
                            g.DrawRectangle(pen, x + w * 0.12f + s + gap, y + h * 0.12f, s, s);
                            g.DrawRectangle(pen, x + w * 0.12f, y + h * 0.12f + s + gap, s, s);
                            g.DrawRectangle(pen, x + w * 0.12f + s + gap, y + h * 0.12f + s + gap, s, s);
                            break;
                        }

                    case Ico.Mods:
                        {
                            // стилизованный «пазл»
                            var path = new GraphicsPath();
                            path.AddRectangle(new RectangleF(x + w * 0.14f, y + h * 0.14f, w * 0.44f, h * 0.44f));
                            path.AddRectangle(new RectangleF(x + w * 0.42f, y + h * 0.42f, w * 0.44f, h * 0.44f));
                            g.DrawPath(pen, path);
                            path.Dispose();
                            break;
                        }

                    case Ico.Account:
                        g.DrawEllipse(pen, cx - w * 0.19f, y + h * 0.13f, w * 0.38f, h * 0.38f);
                        g.DrawArc(pen, cx - w * 0.33f, y + h * 0.56f, w * 0.66f, h * 0.62f, 200, 140);
                        break;

                    case Ico.Settings:
                        {
                            g.DrawEllipse(pen, cx - w * 0.17f, cy - h * 0.17f, w * 0.34f, h * 0.34f);
                            for (int i = 0; i < 8; i++)
                            {
                                double a = Math.PI * 2 * i / 8.0;
                                float r1 = w * 0.26f, r2 = w * 0.42f;
                                g.DrawLine(pen,
                                    cx + (float)Math.Cos(a) * r1, cy + (float)Math.Sin(a) * r1,
                                    cx + (float)Math.Cos(a) * r2, cy + (float)Math.Sin(a) * r2);
                            }
                            break;
                        }

                    case Ico.Console:
                        g.DrawRectangle(pen, x + w * 0.1f, y + h * 0.18f, w * 0.8f, h * 0.64f);
                        g.DrawLines(pen, new[]
                        {
                            new PointF(x + w * 0.26f, y + h * 0.38f),
                            new PointF(x + w * 0.40f, cy),
                            new PointF(x + w * 0.26f, y + h * 0.62f)
                        });
                        g.DrawLine(pen, x + w * 0.50f, y + h * 0.64f, x + w * 0.72f, y + h * 0.64f);
                        break;

                    case Ico.Skin:
                        {
                            // футболка: плечи, рукава и корпус
                            var path = new GraphicsPath();
                            path.AddLine(x + w * 0.30f, y + h * 0.18f, x + w * 0.12f, y + h * 0.34f);
                            path.AddLine(x + w * 0.12f, y + h * 0.34f, x + w * 0.24f, y + h * 0.50f);
                            path.AddLine(x + w * 0.24f, y + h * 0.50f, x + w * 0.30f, y + h * 0.44f);
                            path.AddLine(x + w * 0.30f, y + h * 0.44f, x + w * 0.30f, y + h * 0.84f);
                            path.AddLine(x + w * 0.30f, y + h * 0.84f, x + w * 0.70f, y + h * 0.84f);
                            path.AddLine(x + w * 0.70f, y + h * 0.84f, x + w * 0.70f, y + h * 0.44f);
                            path.AddLine(x + w * 0.70f, y + h * 0.44f, x + w * 0.76f, y + h * 0.50f);
                            path.AddLine(x + w * 0.76f, y + h * 0.50f, x + w * 0.88f, y + h * 0.34f);
                            path.AddLine(x + w * 0.88f, y + h * 0.34f, x + w * 0.70f, y + h * 0.18f);
                            path.CloseFigure();
                            g.DrawPath(pen, path);
                            path.Dispose();
                            g.DrawArc(pen, cx - w * 0.14f, y + h * 0.14f, w * 0.28f, h * 0.18f, 0, 180);
                            break;
                        }

                    case Ico.Minimize:
                        g.DrawLine(pen, x + w * 0.2f, cy, x + w * 0.8f, cy);
                        break;

                    case Ico.Close:
                        g.DrawLine(pen, x + w * 0.24f, y + h * 0.24f, x + w * 0.76f, y + h * 0.76f);
                        g.DrawLine(pen, x + w * 0.76f, y + h * 0.24f, x + w * 0.24f, y + h * 0.76f);
                        break;

                    case Ico.Folder:
                        {
                            var path = new GraphicsPath();
                            path.AddLine(x + w * 0.12f, y + h * 0.78f, x + w * 0.12f, y + h * 0.26f);
                            path.AddLine(x + w * 0.12f, y + h * 0.26f, x + w * 0.42f, y + h * 0.26f);
                            path.AddLine(x + w * 0.42f, y + h * 0.26f, x + w * 0.52f, y + h * 0.38f);
                            path.AddLine(x + w * 0.52f, y + h * 0.38f, x + w * 0.88f, y + h * 0.38f);
                            path.AddLine(x + w * 0.88f, y + h * 0.38f, x + w * 0.88f, y + h * 0.78f);
                            path.CloseFigure();
                            g.DrawPath(pen, path);
                            path.Dispose();
                            break;
                        }

                    case Ico.Download:
                        g.DrawLine(pen, cx, y + h * 0.16f, cx, y + h * 0.62f);
                        g.DrawLines(pen, new[]
                        {
                            new PointF(cx - w * 0.18f, y + h * 0.44f),
                            new PointF(cx, y + h * 0.64f),
                            new PointF(cx + w * 0.18f, y + h * 0.44f)
                        });
                        g.DrawLine(pen, x + w * 0.18f, y + h * 0.82f, x + w * 0.82f, y + h * 0.82f);
                        break;

                    case Ico.Trash:
                        g.DrawLine(pen, x + w * 0.16f, y + h * 0.26f, x + w * 0.84f, y + h * 0.26f);
                        g.DrawLine(pen, x + w * 0.38f, y + h * 0.18f, x + w * 0.62f, y + h * 0.18f);
                        g.DrawLines(pen, new[]
                        {
                            new PointF(x + w * 0.24f, y + h * 0.26f),
                            new PointF(x + w * 0.30f, y + h * 0.84f),
                            new PointF(x + w * 0.70f, y + h * 0.84f),
                            new PointF(x + w * 0.76f, y + h * 0.26f)
                        });
                        break;

                    case Ico.Refresh:
                        g.DrawArc(pen, x + w * 0.18f, y + h * 0.18f, w * 0.64f, h * 0.64f, 40, 280);
                        g.DrawLines(pen, new[]
                        {
                            new PointF(x + w * 0.66f, y + h * 0.10f),
                            new PointF(x + w * 0.84f, y + h * 0.30f),
                            new PointF(x + w * 0.60f, y + h * 0.36f)
                        });
                        break;

                    case Ico.Search:
                        g.DrawEllipse(pen, x + w * 0.16f, y + h * 0.16f, w * 0.48f, h * 0.48f);
                        g.DrawLine(pen, x + w * 0.60f, y + h * 0.60f, x + w * 0.86f, y + h * 0.86f);
                        break;

                    case Ico.Plus:
                        g.DrawLine(pen, cx, y + h * 0.2f, cx, y + h * 0.8f);
                        g.DrawLine(pen, x + w * 0.2f, cy, x + w * 0.8f, cy);
                        break;

                    case Ico.Check:
                        g.DrawLines(pen, new[]
                        {
                            new PointF(x + w * 0.2f, cy),
                            new PointF(x + w * 0.44f, y + h * 0.74f),
                            new PointF(x + w * 0.82f, y + h * 0.28f)
                        });
                        break;

                    case Ico.Arrow:
                        g.DrawLines(pen, new[]
                        {
                            new PointF(x + w * 0.3f, y + h * 0.42f),
                            new PointF(cx, y + h * 0.62f),
                            new PointF(x + w * 0.7f, y + h * 0.42f)
                        });
                        break;
                }
            }
        }

        /// <summary>Фирменный фон главного экрана.</summary>
        public static void PaintHeroBackground(Graphics g, Rectangle r)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            Smooth(g);

            using (var b = new LinearGradientBrush(r, Color.FromArgb(17, 21, 34), Color.FromArgb(10, 12, 18), 60f))
                g.FillRectangle(b, r);

            // мягкие цветные пятна
            DrawGlow(g, new PointF(r.Left + r.Width * 0.18f, r.Top + r.Height * 0.22f),
                     Math.Max(r.Width, r.Height) * 0.55f, Color.FromArgb(70, Accent));
            DrawGlow(g, new PointF(r.Right - r.Width * 0.12f, r.Bottom - r.Height * 0.08f),
                     Math.Max(r.Width, r.Height) * 0.6f, Color.FromArgb(60, Accent2));

            // лёгкая сетка
            using (var pen = new Pen(Color.FromArgb(10, 255, 255, 255)))
            {
                for (int x = r.Left; x < r.Right; x += 42) g.DrawLine(pen, x, r.Top, x, r.Bottom);
                for (int y = r.Top; y < r.Bottom; y += 42) g.DrawLine(pen, r.Left, y, r.Right, y);
            }
        }

        public static void DrawGlow(Graphics g, PointF center, float radius, Color color)
        {
            if (radius <= 1) return;
            var rect = new RectangleF(center.X - radius, center.Y - radius, radius * 2, radius * 2);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(rect);
                using (var brush = new PathGradientBrush(path))
                {
                    brush.CenterColor = color;
                    brush.SurroundColors = new[] { Color.FromArgb(0, color) };
                    g.FillEllipse(brush, rect);
                }
            }
        }

        public static void EnableDoubleBuffer(Control c)
        {
            try
            {
                typeof(Control).GetProperty("DoubleBuffered",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(c, true, null);
            }
            catch { }
        }
    }
}
