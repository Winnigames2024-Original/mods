using System.Drawing.Drawing2D;

namespace GEO_DASH.UI
{
    /// <summary>Свои окна вместо системных MessageBox: плита с обводкой, свои кнопки.</summary>
    public class GdDialog : Form
    {
        readonly GdPanel _panel = new() { Dock = DockStyle.Fill };
        string _title, _message;
        int _result = -1;
        Point _drag;
        bool _dragging;

        public TextBox Input { get; private set; }

        GdDialog(string title, string message, int w, int h)
        {
            _title = title;
            _message = message ?? "";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(w, h);
            BackColor = Color.FromArgb(16, 14, 38);
            ShowInTaskbar = false;
            KeyPreview = true;

            Controls.Add(_panel);
            _panel.Paint += (s, e) => Draw(e.Graphics);
            _panel.MouseDown += (s, e) => { _dragging = true; _drag = e.Location; };
            _panel.MouseUp += (s, e) => _dragging = false;
            _panel.MouseMove += (s, e) =>
            {
                if (_dragging) Location = new Point(Location.X + e.X - _drag.X, Location.Y + e.Y - _drag.Y);
            };

            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { _result = -1; Close(); }
                if (e.KeyCode == Keys.Enter && Input != null) { _result = 0; Close(); }
            };
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        void Draw(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(6, 6, _panel.Width - 12, _panel.Height - 12);

            using (var sh = Gd.Round(new RectangleF(r.X + 4, r.Y + 7, r.Width, r.Height), 20f))
            using (var sb = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                g.FillPath(sb, sh);

            Gd.Plate(g, r, Color.FromArgb(52, 54, 108), Color.FromArgb(24, 22, 58), 20f, 4f);

            using (var f = Gd.Title(17f))
                Gd.Outlined(g, _title, f, new RectangleF(r.X + 18, r.Y + 14, r.Width - 36, 34), Gd.Paper, thick: 3.4f);

            if (!string.IsNullOrEmpty(_message))
            {
                using var f2 = Gd.Bold(11f);
                using var br = new SolidBrush(Color.FromArgb(235, 220, 230, 255));
                using var sf = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.Word };
                g.DrawString(_message, f2, br,
                    new RectangleF(r.X + 24, r.Y + 56, r.Width - 48, r.Height - 150), sf);
            }
        }

        // ------------------------------------------------------------ окна

        /// <summary>Сообщение с кнопками. Возвращает индекс кнопки (-1 — закрыли).</summary>
        public static int Show(IWin32Window owner, string title, string message, params string[] buttons)
        {
            if (buttons == null || buttons.Length == 0) buttons = new[] { "OK" };
            int h = 210 + Math.Max(0, (message ?? "").Length / 60) * 18;
            var d = new GdDialog(title, message, 560, h);
            d.AddButtons(buttons, h - 78);
            d.ShowDialog(owner);
            int r = d._result;
            d.Dispose();
            return r;
        }

        public static void Info(IWin32Window owner, string title, string message)
            => Show(owner, title, message, "OK");

        public static bool Confirm(IWin32Window owner, string title, string message)
            => Show(owner, title, message, "ДА", "НЕТ") == 0;

        /// <summary>Ввод строки. null — отмена.</summary>
        public static string Prompt(IWin32Window owner, string title, string message,
                                    string def = "", bool password = false)
        {
            var d = new GdDialog(title, message, 560, 250);
            d.Input = new TextBox
            {
                Left = 40, Top = 118, Width = 480, Font = Gd.Bold(13f),
                BackColor = Color.FromArgb(20, 18, 48), ForeColor = Gd.Paper,
                BorderStyle = BorderStyle.FixedSingle, Text = def ?? "",
                UseSystemPasswordChar = password
            };
            d._panel.Controls.Add(d.Input);
            d.AddButtons(new[] { "OK", "ОТМЕНА" }, 170);
            d.Shown += (s, e) => d.Input.Focus();
            d.ShowDialog(owner);
            string res = d._result == 0 ? d.Input.Text : null;
            d.Dispose();
            return res;
        }

        /// <summary>Список вариантов. Возвращает индекс или -1.</summary>
        public static int Choose(IWin32Window owner, string title, params string[] items)
        {
            int rows = Math.Min(items.Length, 7);
            int h = 110 + rows * 54 + 70;
            var d = new GdDialog(title, "", 520, h);

            var box = new Panel
            {
                Left = 28, Top = 66, Width = 464, Height = rows * 54 + 6,
                AutoScroll = items.Length > rows, BackColor = Color.Transparent
            };
            d._panel.Controls.Add(box);

            for (int i = 0; i < items.Length; i++)
            {
                int idx = i;
                var b = new GdButton
                {
                    Text = items[i], Left = 0, Top = i * 54, Width = items.Length > rows ? 440 : 460, Height = 48,
                    Color1 = Color.FromArgb(96, 150, 240), Color2 = Color.FromArgb(32, 62, 150),
                    FontSize = 11f
                };
                b.Click += (s, e) => { d._result = idx; d.Close(); };
                box.Controls.Add(b);
            }

            var cancel = new GdButton
            {
                Text = "ЗАКРЫТЬ", Icon = GdIcon.Cross, Left = 160, Top = h - 66, Width = 200, Height = 50,
                Color1 = Color.FromArgb(170, 170, 200), Color2 = Color.FromArgb(70, 70, 110)
            };
            cancel.Click += (s, e) => { d._result = -1; d.Close(); };
            d._panel.Controls.Add(cancel);

            d.ShowDialog(owner);
            int r = d._result;
            d.Dispose();
            return r;
        }

        void AddButtons(string[] buttons, int top)
        {
            int bw = Math.Min(200, (ClientSize.Width - 80) / buttons.Length - 12);
            int total = bw * buttons.Length + 14 * (buttons.Length - 1);
            int x = (ClientSize.Width - total) / 2;

            for (int i = 0; i < buttons.Length; i++)
            {
                int idx = i;
                bool first = i == 0;
                var b = new GdButton
                {
                    Text = buttons[i], Left = x, Top = top, Width = bw, Height = 52,
                    Color1 = first ? Color.FromArgb(120, 255, 170) : Color.FromArgb(170, 170, 200),
                    Color2 = first ? Color.FromArgb(20, 150, 100) : Color.FromArgb(70, 70, 110),
                    FontSize = 11.5f
                };
                b.Click += (s, e) => { _result = idx; Close(); };
                _panel.Controls.Add(b);
                x += bw + 14;
            }
        }
    }
}
