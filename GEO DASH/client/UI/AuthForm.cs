using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    /// <summary>
    /// Вход в игру: заходим сами под сохранённым аккаунтом, иначе вход или регистрация.
    /// Аккаунты запоминаются — под каждым можно вернуться в один клик.
    /// </summary>
    public class AuthForm : GdForm
    {
        const int CardX = 140, CardY = 148, CardW = 540, CardH = 384;

        readonly TextBox _name, _pass;
        readonly GdButton _login, _reg, _offline, _retry;
        readonly List<GdButton> _saved = new();
        string _msg = "Проверяю вход…";
        bool _busy = true;

        /// <summary>Игру можно запускать: вошли или работаем офлайн под сохранённым аккаунтом.</summary>
        public bool Allowed { get; private set; }

        public AuthForm()
        {
            Text = "GEO DASH — Вход";
            ClientSize = new Size(820, 660);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BgA = Gd.BgBlue; BgId = 1; GroundId = 1;

            int fx = CardX + 28, fw = CardW - 56;

            _name = Field(fx, CardY + 92, fw, AppConfig.Settings.UserName, false);
            _pass = Field(fx, CardY + 172, fw, "", true);

            int bw = (fw - 12) / 2;
            _login = Add("ВОЙТИ", GdIcon.Check, fx, CardY + 226, bw, 56,
                Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100), async (s, e) => await Go(false));
            _reg = Add("РЕГИСТРАЦИЯ", GdIcon.Plus, fx + bw + 12, CardY + 226, bw, 56,
                Color.FromArgb(120, 200, 255), Color.FromArgb(25, 95, 190), async (s, e) => await Go(true));

            _retry = Add("ПОВТОРИТЬ", GdIcon.Refresh, fx, CardY + 294, bw, 48,
                Color.FromArgb(170, 175, 215), Color.FromArgb(70, 76, 130), async (s, e) => await AutoAsync());
            _offline = Add("ИГРАТЬ ОФЛАЙН", GdIcon.Download, fx + bw + 12, CardY + 294, bw, 48,
                Color.FromArgb(255, 200, 90), Color.FromArgb(170, 115, 10), (s, e) => Allow());
            _offline.Visible = false;

            BuildSavedButtons();

            Add("ВЫХОД", GdIcon.Cross, 28, ClientSize.Height - 74, 150, 50,
                Color.FromArgb(220, 120, 120), Color.FromArgb(140, 40, 50),
                (s, e) => { Allowed = false; Close(); });

            _name.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await Go(false); } };
            _pass.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await Go(false); } };
            Shown += async (s, e) => await AutoAsync();
        }

        TextBox Field(int x, int y, int w, string val, bool pass)
        {
            var t = new TextBox
            {
                Left = x, Top = y, Width = w, Text = val, Font = Gd.Bold(13f),
                BackColor = Color.FromArgb(22, 20, 52), ForeColor = Gd.Paper,
                BorderStyle = BorderStyle.FixedSingle, UseSystemPasswordChar = pass
            };
            return Host(t);
        }

        /// <summary>Кнопки сохранённых аккаунтов — вход одним нажатием.</summary>
        void BuildSavedButtons()
        {
            foreach (var b in _saved) { Content.Controls.Remove(b); b.Dispose(); }
            _saved.Clear();

            var list = AppConfig.Settings.Accounts
                .Where(a => !string.IsNullOrWhiteSpace(a.Name) && !string.IsNullOrEmpty(a.Token))
                .Take(4).ToList();
            if (list.Count == 0) return;

            int y = CardY + CardH + 46;
            int w = Math.Min(180, (CardW - (list.Count - 1) * 10) / list.Count);
            int x = CardX + (CardW - (w * list.Count + (list.Count - 1) * 10)) / 2;

            foreach (var a in list)
            {
                var acc = a;
                var b = Add(acc.Name, GdIcon.Profile, x, y, w, 46,
                    Color.FromArgb(150, 190, 255), Color.FromArgb(45, 80, 170),
                    async (s, e) =>
                    {
                        AppConfig.UseAccount(acc);
                        _name.Text = acc.Name;
                        await AutoAsync();
                    });
                b.FontSize = 9.5f;
                _saved.Add(b);
                x += w + 10;
            }
        }

        void Busy(bool on, string msg)
        {
            _busy = on;
            _msg = msg;
            _login.Enabled = _reg.Enabled = !on;
            Content.Invalidate();
        }

        void Allow()
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
                Busy(false, AppConfig.Settings.Accounts.Count > 0
                    ? "Выбери аккаунт внизу или войди заново"
                    : "Войди в аккаунт или создай новый — без него в игру не пустим");
                return;
            }

            Busy(true, "Захожу как " + AppConfig.Settings.UserName + "…");
            var me = await ApiClient.MeAsync();
            if (me.Ok && me.Data != null)
            {
                AppConfig.Settings.UserName = me.Data.Name;
                AppConfig.Settings.IsAdmin = me.Data.Admin;
                AppConfig.RememberAccount(me.Data.Name, AppConfig.Settings.Token, me.Data.Admin);
                Allow();
                return;
            }

            if (!ApiClient.Online)
            {
                // сети нет, но аккаунт уже сохранён — заходим офлайн
                Allow();
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
                AppConfig.RememberAccount(AppConfig.Settings.UserName, r.Data.Token, AppConfig.Settings.IsAdmin);
                Allow();
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
                Gd.Outlined(g, "GEO DASH", f, new RectangleF(0, 42, ClientSize.Width, 56), Gd.Paper, thick: 5f);
            using (var f = Gd.Bold(10.5f))
                Gd.Outlined(g, "вход в аккаунт · " + AppConfig.ServerLabel, f,
                    new RectangleF(0, 102, ClientSize.Width, 22), Color.FromArgb(230, 230, 245, 255), thick: 2.4f);

            var card = new RectangleF(CardX, CardY, CardW, CardH);
            Gd.Glass(g, card, 20f);

            using (var fh = Gd.Bold(10f))
                Gd.Outlined(g, "вход запоминается — дальше игра заходит сама", fh,
                    new RectangleF(card.X + 16, card.Y + 16, card.Width - 32, 24), Gd.Gold, thick: 2.2f);

            using var fb = Gd.Bold(11f);
            Gd.Outlined(g, "ИМЯ", fb, new RectangleF(CardX + 30, CardY + 64, 220, 24),
                Gd.Paper, StringAlignment.Near, thick: 2.2f);
            Gd.Outlined(g, "ПАРОЛЬ", fb, new RectangleF(CardX + 30, CardY + 144, 220, 24),
                Gd.Paper, StringAlignment.Near, thick: 2.2f);

            if (_saved.Count > 0)
                Gd.Outlined(g, "сохранённые аккаунты", fb,
                    new RectangleF(CardX, CardY + CardH + 16, CardW, 24),
                    Color.FromArgb(225, 200, 215, 255), thick: 2.2f);

            if (!string.IsNullOrEmpty(_msg))
                Gd.Outlined(g, _msg, fb, new RectangleF(30, ClientSize.Height - 134, ClientSize.Width - 60, 44),
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
