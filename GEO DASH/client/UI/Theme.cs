using System.Drawing.Drawing2D;

namespace GEO_DASH.UI
{
    public static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(18, 20, 32);
        public static readonly Color Bg2 = Color.FromArgb(26, 29, 46);
        public static readonly Color Panel = Color.FromArgb(32, 36, 56);
        public static readonly Color Accent = Color.FromArgb(90, 180, 255);
        public static readonly Color Accent2 = Color.FromArgb(255, 170, 70);
        public static readonly Color Good = Color.FromArgb(90, 220, 140);
        public static readonly Color Bad = Color.FromArgb(255, 95, 110);
        public static readonly Color Text = Color.FromArgb(235, 238, 250);
        public static readonly Color Dim = Color.FromArgb(150, 158, 185);

        public static Font H1 = new("Segoe UI", 22f, FontStyle.Bold);
        public static Font H2 = new("Segoe UI", 14f, FontStyle.Bold);
        public static Font Body = new("Segoe UI", 10f);
        public static Font Mono = new("Consolas", 9.5f);

        public static void Apply(Form f, string title)
        {
            f.Text = AppConfig.AppName + " — " + title;
            f.BackColor = Bg;
            f.ForeColor = Text;
            f.Font = Body;
            f.StartPosition = FormStartPosition.CenterParent;
            f.KeyPreview = true;
        }

        public static Button Btn(string text, int x, int y, int w = 220, int h = 42, Color? col = null)
        {
            var b = new Button
            {
                Text = text,
                Left = x, Top = y, Width = w, Height = h,
                FlatStyle = FlatStyle.Flat,
                BackColor = col ?? Panel,
                ForeColor = Text,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(70, 80, 120);
            b.FlatAppearance.MouseOverBackColor = Lighten(b.BackColor, 0.18);
            b.FlatAppearance.MouseDownBackColor = Lighten(b.BackColor, -0.1);
            return b;
        }

        public static Label Lbl(string text, int x, int y, int w = 300, Font font = null, Color? col = null)
            => new Label
            {
                Text = text, Left = x, Top = y, Width = w, AutoSize = false, Height = (font ?? Body).Height + 6,
                Font = font ?? Body, ForeColor = col ?? Text, BackColor = Color.Transparent
            };

        public static TextBox Tb(int x, int y, int w = 220, string val = "", bool pass = false)
            => new TextBox
            {
                Left = x, Top = y, Width = w, Text = val,
                BackColor = Bg2, ForeColor = Text, BorderStyle = BorderStyle.FixedSingle,
                UseSystemPasswordChar = pass
            };

        public static ComboBox Cb(int x, int y, int w = 220)
            => new ComboBox
            {
                Left = x, Top = y, Width = w, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Bg2, ForeColor = Text, FlatStyle = FlatStyle.Flat
            };

        public static CheckBox Chk(string text, int x, int y, bool val = false, int w = 220)
            => new CheckBox
            {
                Text = text, Left = x, Top = y, Width = w, Checked = val,
                ForeColor = Text, BackColor = Color.Transparent, FlatStyle = FlatStyle.Flat
            };

        public static NumericUpDown Num(int x, int y, double min, double max, double val, int dec = 0, int w = 100)
        {
            var n = new NumericUpDown
            {
                Left = x, Top = y, Width = w,
                Minimum = (decimal)min, Maximum = (decimal)max,
                DecimalPlaces = dec, Increment = dec > 0 ? 0.1m : 1m,
                BackColor = Bg2, ForeColor = Text, BorderStyle = BorderStyle.FixedSingle
            };
            n.Value = (decimal)Math.Clamp(val, min, max);
            return n;
        }

        public static ListView Lv(int x, int y, int w, int h)
        {
            var lv = new ListView
            {
                Left = x, Top = y, Width = w, Height = h,
                View = View.Details, FullRowSelect = true, GridLines = false,
                BackColor = Bg2, ForeColor = Text, BorderStyle = BorderStyle.FixedSingle,
                HideSelection = false, OwnerDraw = false, MultiSelect = false
            };
            return lv;
        }

        public static Panel Card(int x, int y, int w, int h)
            => new Panel { Left = x, Top = y, Width = w, Height = h, BackColor = Panel };

        public static Color Lighten(Color c, double k)
        {
            int f(int v) => Math.Clamp((int)(v + (k > 0 ? (255 - v) * k : v * k)), 0, 255);
            return Color.FromArgb(c.A, f(c.R), f(c.G), f(c.B));
        }

        public static void DrawHeader(Graphics g, Rectangle r, string title, string sub)
        {
            using var br = new LinearGradientBrush(r, Color.FromArgb(40, 60, 140), Color.FromArgb(22, 26, 48), LinearGradientMode.Horizontal);
            g.FillRectangle(br, r);
            using var tb = new SolidBrush(Text);
            g.DrawString(title, H1, tb, r.X + 16, r.Y + 10);
            using var sb = new SolidBrush(Dim);
            g.DrawString(sub, Body, sb, r.X + 20, r.Y + 46);
        }
    }
}
