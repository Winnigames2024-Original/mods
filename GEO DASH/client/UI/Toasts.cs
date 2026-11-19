using System.Drawing.Drawing2D;

namespace GEO_DASH.UI
{
    /// <summary>
    /// Уведомления в углу экрана — как в Steam: выезжают слева снизу,
    /// висят несколько секунд и уезжают обратно. Не перехватывают мышь,
    /// поверх игры не мешают.
    /// </summary>
    public static class Toasts
    {
        /// <summary>Картинка слева в уведомлении.</summary>
        public enum Kind { Info, Ok, Warn, Star, Chat, Download }

        class Item
        {
            public string Title = "", Text = "";
            public Kind Icon;
            public double Life;        // сколько ещё висеть
            public double Anim;        // 0 — спрятан, 1 — выехал
            public bool Closing;
        }

        const int W = 330, H = 78, Gap = 8, Edge = 18;

        static readonly List<Item> Items = new();
        static ToastWindow _win;

        /// <summary>Показать уведомление.</summary>
        public static void Show(string title, string text = "", Kind icon = Kind.Info, double seconds = 4.5)
        {
            if (string.IsNullOrWhiteSpace(title)) return;

            void Add()
            {
                Items.Add(new Item { Title = title, Text = text ?? "", Icon = icon, Life = seconds });
                while (Items.Count > 4) Items.RemoveAt(0);
                Ensure();
            }

            var owner = Form.ActiveForm ?? Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Visible);
            if (owner != null && owner.InvokeRequired) { try { owner.BeginInvoke(Add); } catch { } }
            else Add();
        }

        public static void Ok(string title, string text = "") => Show(title, text, Kind.Ok);
        public static void Warn(string title, string text = "") => Show(title, text, Kind.Warn);

        static void Ensure()
        {
            if (_win != null && !_win.IsDisposed) { _win.Bump(); return; }
            _win = new ToastWindow();
            _win.Show();
        }

        /// <summary>Окошко без рамки, которое рисует стопку уведомлений.</summary>
        class ToastWindow : Form
        {
            readonly System.Windows.Forms.Timer _t = new() { Interval = 16 };
            DateTime _last = DateTime.Now;

            public ToastWindow()
            {
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                TopMost = true;
                StartPosition = FormStartPosition.Manual;
                BackColor = Color.Magenta;
                TransparencyKey = Color.Magenta;
                DoubleBuffered = true;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                         | ControlStyles.OptimizedDoubleBuffer, true);
                Place();
                _t.Tick += (s, e) => Step();
                _t.Start();
            }

            /// <summary>Мышь сквозь окно — уведомления ничего не перекрывают.</summary>
            protected override CreateParams CreateParams
            {
                get
                {
                    var cp = base.CreateParams;
                    cp.ExStyle |= 0x80 | 0x20 | 0x08000000;   // TOOLWINDOW | TRANSPARENT | NOACTIVATE
                    return cp;
                }
            }

            protected override bool ShowWithoutActivation => true;

            public void Bump() { Place(); Invalidate(); }

            void Place()
            {
                var scr = Screen.FromPoint(Cursor.Position) ?? Screen.PrimaryScreen;
                int h = (H + Gap) * Math.Max(1, Items.Count) + Gap;
                Bounds = new Rectangle(scr.WorkingArea.Left + Edge,
                                       scr.WorkingArea.Bottom - h - Edge,
                                       W + Edge, h);
            }

            void Step()
            {
                double dt = (DateTime.Now - _last).TotalSeconds;
                _last = DateTime.Now;
                if (dt <= 0 || dt > 0.3) dt = 0.016;

                for (int i = Items.Count - 1; i >= 0; i--)
                {
                    var it = Items[i];
                    if (!it.Closing)
                    {
                        it.Anim = Math.Min(1, it.Anim + dt * 5);
                        it.Life -= dt;
                        if (it.Life <= 0) it.Closing = true;
                    }
                    else
                    {
                        it.Anim -= dt * 5;
                        if (it.Anim <= 0) Items.RemoveAt(i);
                    }
                }

                if (Items.Count == 0) { _t.Stop(); Close(); _win = null; return; }
                Place();
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Magenta);

                int y = ClientSize.Height - Gap;
                for (int i = Items.Count - 1; i >= 0; i--)
                {
                    var it = Items[i];
                    y -= H;
                    float k = (float)Ease(it.Anim);
                    float x = -W * (1 - k);
                    DrawToast(g, new RectangleF(x, y, W, H), it, k);
                    y -= Gap;
                }
            }

            static double Ease(double t) => 1 - Math.Pow(1 - Math.Clamp(t, 0, 1), 3);

            static void DrawToast(Graphics g, RectangleF r, Item it, float k)
            {
                int alpha = (int)(255 * Math.Clamp(k, 0, 1));
                if (alpha < 6) return;

                var (c1, c2, icon) = Look(it.Icon);

                using (var path = Round(r, 12f))
                {
                    using (var br = new LinearGradientBrush(
                               new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2),
                               Color.FromArgb(alpha, 26, 24, 54), Color.FromArgb(alpha, 14, 12, 34),
                               LinearGradientMode.Vertical))
                        g.FillPath(br, path);
                    using (var pen = new Pen(Color.FromArgb(alpha, 6, 5, 16), 3f)) g.DrawPath(pen, path);
                }

                // цветная полоса слева — как у уведомлений Steam
                var strip = new RectangleF(r.X + 6, r.Y + 8, 6, r.Height - 16);
                using (var br = new LinearGradientBrush(strip, Color.FromArgb(alpha, c1),
                                                        Color.FromArgb(alpha, c2), LinearGradientMode.Vertical))
                    g.FillRectangle(br, strip);

                // иконка
                var ir = new RectangleF(r.X + 22, r.Y + r.Height / 2 - 17, 34, 34);
                using (var br = new SolidBrush(Color.FromArgb(alpha, c1))) g.FillEllipse(br, ir);
                using (var pen = new Pen(Color.FromArgb(alpha, 8, 6, 20), 2.4f)) g.DrawEllipse(pen, ir);
                Gd.Icon(g, icon, new RectangleF(ir.X + 7, ir.Y + 7, 20, 20), Color.FromArgb(alpha, 255, 255, 255));

                // текст
                using var ft = Gd.Title(12f);
                using var fb = Gd.Reg(9.5f);
                var tr = new RectangleF(r.X + 68, r.Y + 12, r.Width - 80, 22);
                Gd.Outlined(g, it.Title, ft, tr, Color.FromArgb(alpha, 240, 244, 255),
                            StringAlignment.Near, thick: 2.4f);

                if (!string.IsNullOrEmpty(it.Text))
                {
                    using var br = new SolidBrush(Color.FromArgb((int)(alpha * 0.85), 198, 208, 240));
                    using var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter };
                    g.DrawString(it.Text, fb, br,
                                 new RectangleF(r.X + 68, r.Y + 36, r.Width - 80, 34), sf);
                }
            }

            static (Color, Color, GdIcon) Look(Kind k) => k switch
            {
                Kind.Ok => (Color.FromArgb(90, 230, 150), Color.FromArgb(20, 140, 90), GdIcon.Check),
                Kind.Warn => (Color.FromArgb(255, 190, 80), Color.FromArgb(170, 110, 10), GdIcon.Info),
                Kind.Star => (Color.FromArgb(255, 210, 90), Color.FromArgb(180, 130, 20), GdIcon.Star),
                Kind.Chat => (Color.FromArgb(130, 190, 255), Color.FromArgb(30, 90, 190), GdIcon.Online),
                Kind.Download => (Color.FromArgb(180, 140, 255), Color.FromArgb(90, 50, 180), GdIcon.Download),
                _ => (Color.FromArgb(120, 170, 255), Color.FromArgb(30, 70, 170), GdIcon.Info)
            };

            static GraphicsPath Round(RectangleF r, float rad)
            {
                var p = new GraphicsPath();
                p.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
                p.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
                p.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
                p.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
                p.CloseFigure();
                return p;
            }
        }
    }
}
