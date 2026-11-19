using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    /// <summary>
    /// Вход в игру: авто-вход по сохранённому входу, иначе вход или регистрация.
    /// Без аккаунта в игру не пускаем — кроме случая, когда сети нет, а аккаунт уже был.
    /// </summary>
    public class AuthForm : GdForm
    {
        readonly TextBox _name, _pass;
        readonly GdButton _login, _reg, _offline, _retry;
        string _msg = "Проверяю вход…";
        bool _busy = true;

        /// <summary>Игру можно запускать: вошли или работаем офлайн под сохранённым аккаунтом.</summary>
        public bool Allowed { get; private set; }

        public AuthForm()
        {
            Text = "GEO DASH — Вход";
            ClientSize = new Size(720, 560);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BgA = Gd.BgBlue; BgId = 1; GroundId = 1;

            _name = Field(230, 232, AppConfig.Settings.UserName, false);
            _pass = Field(230, 292, "", true);

            _login = Add("ВОЙТИ", GdIcon.Check, 230, 350, 125, 54,
                Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100), async (s, e) => await Go(false));
            _reg = Add("РЕГИСТРАЦИЯ", GdIcon.Plus, 365, 350, 125, 54,
                Color.FromArgb(120, 200, 255), Color.FromArgb(25, 95, 190), async (s, e) => await Go(true));

            _retry = Add("ПОВТОРИТЬ", GdIcon.Refresh, 230, 414, 125, 46,
                Color.FromArgb(170, 175, 215), Color.FromArgb(70, 76, 130), async (s, e) => await AutoAsync());
            _offline = Add("ИГРАТЬ ОФЛАЙН", GdIcon.Download, 365, 414, 125, 46,
                Color.FromArgb(255, 200, 90), Color.FromArgb(170, 115, 10), (s, e) => Allow("офлайн"));
            _offline.Visible = false;

            Add("ВЫХОД", GdIcon.Cross, 24, ClientSize.Height - 72, 140, 48,
                Color.FromArgb(220, 120, 120), Color.FromArgb(140, 40, 50),
                (s, e) => { Allowed = false; Close(); });

            _pass.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) await Go(false); };
            Shown += async (s, e) => await AutoAsync();
        }

        TextBox Field(int x, int y, string val, bool pass)
        {
            var t = new TextBox
            {
                Left = x, Top = y, Width = 260, Text = val, Font = Gd.Bold(13f),
                BackColor = Color.FromArgb(22, 20, 52), ForeColor = Gd.Paper,
                BorderStyle = BorderStyle.FixedSingle, UseSystemPasswordChar = pass
            };
            return Host(t);
        }

        void Busy(bool on, string msg)
        {
            _busy = on;
            _msg = msg;
            _login.Enabled = _reg.Enabled = !on;
            Content.Invalidate();
        }

        void Allow(string how)
        {
            Allowed = true;
            _msg = "Входим…";
            Content.Invalidate();
            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>Авто-вход: сохранённый вход проверяем на сервере, без сети — пускаем по кэшу.</summary>
        async Task AutoAsync()
        {
            _offline.Visible = false;
            if (!AppConfig.LoggedIn)
            {
                Busy(false, "Войди в аккаунт или создай новый — без него в игру не пустим");
                return;
            }

            Busy(true, "Захожу как " + AppConfig.Settings.UserName + "…");
            var me = await ApiClient.MeAsync();
            if (me.Ok && me.Data != null)
            {
                AppConfig.Settings.UserName = me.Data.Name;
                AppConfig.Settings.IsAdmin = me.Data.Admin;
                AppConfig.Save();
                Allow("авто");
                return;
            }

            if (!ApiClient.Online)
            {
                // сети нет, но аккаунт уже сохранён — заходим офлайн
                Allow("офлайн");
                return;
            }

            AppConfig.Settings.Token = "";
            AppConfig.Save();
            Busy(false, "Сохранённый вход больше не подходит — войди заново");
        }

        async Task Go(bool register)
        {
            string n = _name.Text.Trim(), p = _pass.Text;
            if (n.Length < 3 || p.Length < 3) { Busy(false, "Имя и пароль — от 3 символов"); return; }

            Busy(true, register ? "Регистрирую…" : "Вхожу…");
            var r = register ? await ApiClient.RegisterAsync(n, p) : await ApiClient.LoginAsync(n, p);
            if (r.Ok && r.Data != null && r.Data.Ok)
            {
                AppConfig.Settings.Token = r.Data.Token;
                AppConfig.Settings.UserName = r.Data.Account?.Name ?? n;
                AppConfig.Settings.IsAdmin = r.Data.Account?.Admin ?? false;
                AppConfig.Save();
                Allow("вход");
                return;
            }

            string err = r.Ok ? (r.Data?.Error ?? "Ошибка") : r.Error;
            if (!ApiClient.Online)
            {
                err = "Сервер недоступен. Проверь интернет и нажми «ПОВТОРИТЬ».";
                _offline.Visible = !string.IsNullOrEmpty(AppConfig.Settings.UserName) && AppConfig.LoggedIn;
            }
            Busy(false, err);
        }

        protected override void OnRender(Graphics g)
        {
            using (var f = Gd.Title(40f))
                Gd.Outlined(g, "GEO DASH", f, new RectangleF(0, 44, ClientSize.Width, 56), Gd.Paper, thick: 5f);
            using (var f = Gd.Bold(11f))
                Gd.Outlined(g, "вход в аккаунт · " + AppConfig.Settings.ServerHost + ":" + AppConfig.Settings.ServerPort, f,
                    new RectangleF(0, 104, ClientSize.Width, 22), Color.FromArgb(230, 230, 245, 255), thick: 2.4f);

            var card = new RectangleF(170, 150, 380, 330);
            Gd.Glass(g, card, 20f);

            using var fb = Gd.Bold(11.5f);
            Gd.Outlined(g, "Имя", fb, new RectangleF(190, 232, 90, 24), Gd.Paper, StringAlignment.Near, thick: 2.2f);
            Gd.Outlined(g, "Пароль", fb, new RectangleF(190, 292, 90, 24), Gd.Paper, StringAlignment.Near, thick: 2.2f);
            Gd.Outlined(g, "вход запоминается — дальше игра заходит сама", fb,
                new RectangleF(card.X, card.Y + 22, card.Width, 24), Gd.Gold, thick: 2.2f);

            if (!string.IsNullOrEmpty(_msg))
                Gd.Outlined(g, _msg, fb, new RectangleF(30, 492, ClientSize.Width - 60, 44),
                    _busy ? Color.FromArgb(230, 235, 245, 255) : Gd.Gold, thick: 2.4f);
        }

        /// <summary>Показать окно входа при запуске. true — можно открывать меню.</summary>
        public static bool Ensure()
        {
            using var f = new AuthForm();
            f.ShowDialog();
            return f.Allowed;
        }
    }
}
