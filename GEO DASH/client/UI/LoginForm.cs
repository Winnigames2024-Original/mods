using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    public class LoginForm : GdForm
    {
        readonly TextBox _name, _pass;
        string _msg = "";

        public LoginForm()
        {
            Text = "GEO DASH — Аккаунт";
            ClientSize = new Size(560, 420);

            _name = new TextBox
            {
                Left = 150, Top = 150, Width = 260, Font = Gd.Bold(12f),
                BackColor = Color.FromArgb(24, 20, 54), ForeColor = Gd.Paper,
                BorderStyle = BorderStyle.FixedSingle, Text = AppConfig.Settings.UserName
            };
            _pass = new TextBox
            {
                Left = 150, Top = 205, Width = 260, Font = Gd.Bold(12f),
                BackColor = Color.FromArgb(24, 20, 54), ForeColor = Gd.Paper,
                BorderStyle = BorderStyle.FixedSingle, UseSystemPasswordChar = true
            };
            Controls.Add(_name); Controls.Add(_pass);

            Add("ВОЙТИ", GdIcon.Check, 150, 255, 125, 52, Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100),
                async (s, e) => await Go(false));
            Add("РЕГИСТРАЦИЯ", GdIcon.Plus, 285, 255, 125, 52, Color.FromArgb(120, 190, 255), Color.FromArgb(30, 90, 190),
                async (s, e) => await Go(true));
            Add("ВЫЙТИ ИЗ АККАУНТА", GdIcon.Cross, 150, 320, 260, 44, Color.FromArgb(200, 110, 110), Color.FromArgb(120, 40, 50),
                (s, e) =>
                {
                    AppConfig.Settings.Token = ""; AppConfig.Settings.UserName = ""; AppConfig.Settings.IsAdmin = false;
                    AppConfig.Save(); _msg = "Вышли из аккаунта"; Invalidate();
                });

            _pass.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) await Go(false); };
        }

        async Task Go(bool register)
        {
            string n = _name.Text.Trim(), p = _pass.Text;
            if (n.Length < 3 || p.Length < 3) { _msg = "Имя и пароль — от 3 символов"; Invalidate(); return; }
            _msg = register ? "Регистрирую…" : "Вхожу…"; Invalidate();

            var r = register ? await ApiClient.RegisterAsync(n, p) : await ApiClient.LoginAsync(n, p);
            if (r.Ok && r.Data != null && r.Data.Ok)
            {
                AppConfig.Settings.Token = r.Data.Token;
                AppConfig.Settings.UserName = r.Data.Account?.Name ?? n;
                AppConfig.Settings.IsAdmin = r.Data.Account?.Admin ?? false;
                AppConfig.Save();
                _msg = "Готово! Привет, " + AppConfig.Settings.UserName;
                Invalidate();
                await Task.Delay(500);
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                _msg = r.Ok ? (r.Data?.Error ?? "Ошибка") : r.Error;
                if (!ApiClient.Online)
                    _msg += "\nБез сети вход невозможен, но играть офлайн можно.";
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Header(g, "АККАУНТ", AppConfig.Settings.ServerHost + ":" + AppConfig.Settings.ServerPort);
            using var f = Gd.Bold(11f);
            Gd.Outlined(g, "Имя", f, new RectangleF(60, 150, 90, 24), Gd.Paper, StringAlignment.Near, thick: 2.2f);
            Gd.Outlined(g, "Пароль", f, new RectangleF(60, 205, 90, 24), Gd.Paper, StringAlignment.Near, thick: 2.2f);
            if (!string.IsNullOrEmpty(_msg))
                Gd.Outlined(g, _msg, f, new RectangleF(40, 376, ClientSize.Width - 80, 40), Gd.Gold, thick: 2.2f);
        }
    }
}
