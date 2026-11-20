using System.Diagnostics;
using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    /// <summary>
    /// Окно лицензии. Без ключа в игру не пускаем: ключ берётся в аккаунте на
    /// сайте, у админа или по служебной ссылке выдачи. Один ключ — один аккаунт,
    /// навсегда и на всех устройствах игрока.
    /// </summary>
    public class LicenceForm : GdForm
    {
        const int CardX = 90, CardY = 190, CardW = 640, CardH = 300;

        readonly TextBox _key;
        readonly GdButton _go, _site, _again;
        string _msg = "Проверяю лицензию…";
        bool _busy = true, _noInstaller;

        public bool Allowed { get; private set; }

        public LicenceForm()
        {
            Text = "GEO DASH — Лицензия";
            ClientSize = new Size(820, 660);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BgA = Gd.BgBlue; BgId = 1; GroundId = 1;

            int fx = CardX + 28, fw = CardW - 56;

            _key = new TextBox
            {
                Left = fx, Top = CardY + 104, Width = fw, Font = Gd.Title(18f),
                BackColor = Color.FromArgb(22, 20, 52), ForeColor = Gd.Paper,
                BorderStyle = BorderStyle.FixedSingle, CharacterCasing = CharacterCasing.Upper,
                TextAlign = HorizontalAlignment.Center, MaxLength = 24
            };
            Host(_key);

            // набираем как угодно — приводим к GD-XXXX-XXXX-XXXX-XXXX на ходу
            _key.TextChanged += (s, e) =>
            {
                string pretty = Licence.Pretty(_key.Text);
                if (pretty != _key.Text)
                {
                    int tail = _key.Text.Length - _key.SelectionStart;
                    _key.Text = pretty;
                    _key.SelectionStart = Math.Max(0, pretty.Length - Math.Max(0, tail));
                }
                Content.Invalidate();
            };
            _key.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await ActivateAsync(); }
            };

            int bw = (fw - 12) / 2;
            _go = Add("АКТИВИРОВАТЬ", GdIcon.Check, fx, CardY + 176, bw, 58,
                Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100), async (s, e) => await ActivateAsync());
            _site = Add("ПОЛУЧИТЬ КЛЮЧ", GdIcon.Download, fx + bw + 12, CardY + 176, bw, 58,
                Color.FromArgb(255, 205, 90), Color.FromArgb(175, 120, 10),
                (s, e) => Open(AppConfig.SiteUrl + "account/"));

            _again = Add("ПРОВЕРИТЬ СНОВА", GdIcon.Refresh, fx, CardY + 244, bw, 44,
                Color.FromArgb(170, 175, 215), Color.FromArgb(70, 76, 130), async (s, e) => await CheckAsync());
            Add("СКАЧАТЬ УСТАНОВЩИК", GdIcon.Download, fx + bw + 12, CardY + 244, bw, 44,
                Color.FromArgb(150, 190, 255), Color.FromArgb(45, 80, 170),
                (s, e) => Open(AppConfig.SiteUrl + "download/")).FontSize = 9f;

            Add("ВЫХОД", GdIcon.Cross, 28, ClientSize.Height - 74, 150, 50,
                Color.FromArgb(220, 120, 120), Color.FromArgb(140, 40, 50),
                (s, e) => { Allowed = false; Close(); });

            Add("СМЕНИТЬ АККАУНТ", GdIcon.Profile, ClientSize.Width - 238, ClientSize.Height - 74, 210, 50,
                Color.FromArgb(170, 175, 215), Color.FromArgb(70, 76, 130),
                (s, e) =>
                {
                    Licence.Forget();
                    AppConfig.Settings.Token = "";
                    AppConfig.Save();
                    Allowed = false;
                    Restart = true;
                    Close();
                }).FontSize = 9f;

            Shown += async (s, e) => await CheckAsync();
        }

        /// <summary>Игрок решил войти под другим аккаунтом — игру нужно перезапустить.</summary>
        public bool Restart { get; private set; }

        static void Open(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }

        void Busy(bool on, string msg)
        {
            _busy = on;
            _msg = msg;
            _go.Enabled = _again.Enabled = !on;
            Content.Invalidate();
        }

        void Allow()
        {
            Allowed = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        async Task CheckAsync()
        {
            Busy(true, "Проверяю лицензию…");
            _noInstaller = !Licence.Installed && !AppConfig.IsAdminUser;

            bool ok = await Licence.CheckAsync();
            if (!ok && await Licence.TryInstallerKeyAsync()) ok = true;

            if (ok && !_noInstaller) { Allow(); return; }

            if (_noInstaller)
            {
                Busy(false, ok
                    ? "Лицензия есть, но игра запущена не из установленной копии"
                    : "Вы не ввели лицензионный ключ");
                return;
            }

            Busy(false, "Вы не ввели лицензионный ключ");
        }

        async Task ActivateAsync()
        {
            if (_noInstaller)
            {
                Busy(false, "Сначала поставьте игру установщиком — кнопка справа");
                return;
            }
            string k = _key.Text;
            if (!Licence.WellFormed(k))
            {
                Busy(false, "Ключ набран не полностью: GD- и 16 знаков");
                return;
            }

            Busy(true, "Активирую ключ…");
            string err = await Licence.ActivateAsync(k);
            if (err == null)
            {
                Busy(true, "Готово! Ключ закреплён за аккаунтом " + AppConfig.Settings.UserName);
                await Task.Delay(700);
                Allow();
                return;
            }
            Busy(false, err);
        }

        protected override void OnRender(Graphics g)
        {
            using (var f = Gd.Title(34f))
                Gd.Outlined(g, "ЛИЦЕНЗИОННЫЙ КЛЮЧ", f, new RectangleF(0, 48, ClientSize.Width, 52),
                    Gd.Paper, thick: 5f);
            using (var f = Gd.Bold(10.5f))
                Gd.Outlined(g, "аккаунт " + (AppConfig.Settings.UserName ?? "") + " · " + AppConfig.ServerLabel, f,
                    new RectangleF(0, 104, ClientSize.Width, 22), Color.FromArgb(230, 230, 245, 255), thick: 2.4f);

            // где взять ключ
            using (var f = Gd.Bold(10f))
            {
                var r = new RectangleF(50, 134, ClientSize.Width - 100, 46);
                Gd.Outlined(g, "ключ есть в вашем аккаунте на сайте, его может выдать " + AppConfig.AdminUser +
                               ", и он приходит сразу после покупки", f, r,
                    Color.FromArgb(235, 215, 230, 255), thick: 2.2f);
            }

            var card = new RectangleF(CardX, CardY, CardW, CardH);
            Gd.Glass(g, card, 20f);

            using (var fb = Gd.Bold(11f))
            {
                Gd.Outlined(g, "ВВЕДИТЕ КЛЮЧ", fb, new RectangleF(card.X + 30, card.Y + 70, 300, 24),
                    Gd.Paper, StringAlignment.Near, thick: 2.2f);

                Gd.Outlined(g, "один ключ — один аккаунт, навсегда и на всех ваших устройствах", fb,
                    new RectangleF(card.X + 16, card.Y + 18, card.Width - 32, 24), Gd.Gold, thick: 2.2f);

                if (Licence.WellFormed(_key.Text))
                    Gd.Outlined(g, "ключ набран верно", fb,
                        new RectangleF(card.X + 30, card.Y + 146, card.Width - 60, 22),
                        Gd.Lime, StringAlignment.Near, thick: 2f);
            }

            if (_noInstaller)
            {
                var warn = new RectangleF(50, card.Bottom + 18, ClientSize.Width - 100, 62);
                Gd.Plate(g, warn, Color.FromArgb(120, 40, 50), Color.FromArgb(70, 18, 28), 14f, 3.5f);
                using var f = Gd.Bold(10f);
                Gd.Outlined(g, "Игра запущена не из установленной копии. Скачайте установщик с сайта —" +
                               " он поставит игру и привяжет ключ.", f,
                    RectangleF.Inflate(warn, -14, -10), Gd.Gold, thick: 2.2f);
            }

            if (!string.IsNullOrEmpty(_msg))
                using (var f = Gd.Bold(11.5f))
                    Gd.Outlined(g, _msg, f, new RectangleF(30, ClientSize.Height - 142, ClientSize.Width - 60, 44),
                        _busy ? Color.FromArgb(230, 235, 245, 255) : Gd.Gold, thick: 2.4f);
        }

        /// <summary>
        /// Проверка при запуске. true — можно открывать меню. Если игрок решил
        /// сменить аккаунт, возвращаем false и выставляем Program.RestartAuth.
        /// </summary>
        public static bool Ensure()
        {
            using var f = new LicenceForm();
            f.ShowDialog();
            if (f.Restart) Program.RestartAuth = true;
            return f.Allowed;
        }
    }
}
