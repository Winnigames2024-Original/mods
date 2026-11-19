using GEO_DASH.Audio;
using GEO_DASH.Core;
using GEO_DASH.Net;
using GEO_DASH.Update;

namespace GEO_DASH.UI
{
    public class MainForm : GdForm
    {
        GdPlayButton _play;
        string _status = "Запуск…";
        AccountDto _me;
        readonly System.Windows.Forms.Timer _poll = new() { Interval = 15000 };

        public MainForm()
        {
            Text = AppConfig.AppName + " — " + AppConfig.Brand;
            ClientSize = new Size(1180, 720);
            MinimumSize = new Size(1000, 640);
            StartPosition = FormStartPosition.CenterScreen;

            BuildUi();
            Resize += (s, e) => Layout2();
            Shown += async (s, e) => await StartupAsync();
            _poll.Tick += async (s, e) => await RefreshAccountAsync(false);
            _poll.Start();
            FormClosed += (s, e) => { MusicPlayer.Stop(); Sfx.Dispose(); };
        }

        GdButton _bEditor, _bOnline, _bProfile, _bFriends, _bSettings, _bAdmin, _bSaved, _bDaily, _bSync;

        void BuildUi()
        {
            _play = new GdPlayButton { Size = new Size(210, 210) };
            _play.Click += (s, e) => { Sfx.Play("click"); Open(new PlayMenuForm()); };
            Controls.Add(_play);

            _bEditor = Add("СОЗДАТЬ", GdIcon.Edit, 0, 0, 190, 64, Color.FromArgb(255, 170, 60), Color.FromArgb(180, 90, 10),
                (s, e) => Open(new LevelListForm(LevelListMode.My)), "редактор уровней");
            _bOnline = Add("ОНЛАЙН", GdIcon.Online, 0, 0, 190, 64, Color.FromArgb(80, 160, 255), Color.FromArgb(20, 70, 180),
                (s, e) => Open(new OnlineForm()), "уровни игроков");
            _bSaved = Add("SAVED", GdIcon.Download, 0, 0, 190, 64, Color.FromArgb(120, 230, 160), Color.FromArgb(30, 130, 80),
                (s, e) => Open(new LevelListForm(LevelListMode.Saved)), "офлайн-уровни");
            _bDaily = Add("DAILY / WEEKLY", GdIcon.Daily, 0, 0, 190, 64, Color.FromArgb(210, 130, 255), Color.FromArgb(110, 40, 180),
                (s, e) => Open(new DailyForm()), "уровень дня и недели");

            _bProfile = Add("", GdIcon.Profile, 0, 0, 62, 62, Color.FromArgb(90, 190, 255), Color.FromArgb(20, 90, 170),
                (s, e) => Open(new ProfileForm(AppConfig.Settings.UserName)));
            _bFriends = Add("", GdIcon.Friends, 0, 0, 62, 62, Color.FromArgb(255, 140, 200), Color.FromArgb(150, 40, 110),
                (s, e) => Open(new FriendsForm()));
            _bSettings = Add("", GdIcon.Settings, 0, 0, 62, 62, Color.FromArgb(160, 170, 200), Color.FromArgb(70, 80, 120),
                (s, e) => Open(new SettingsForm()));
            _bSync = Add("", GdIcon.Refresh, 0, 0, 62, 62, Color.FromArgb(120, 220, 180), Color.FromArgb(30, 120, 100),
                async (s, e) => await SyncAsync());
            _bAdmin = Add("", GdIcon.Admin, 0, 0, 62, 62, Color.FromArgb(255, 110, 110), Color.FromArgb(150, 30, 40),
                (s, e) => Open(new AdminForm()));
            _bAdmin.Visible = AppConfig.IsAdminUser;

            Layout2();
        }

        void Layout2()
        {
            int cx = ClientSize.Width / 2, cy = ClientSize.Height / 2 + 10;
            if (_play != null) _play.Location = new Point(cx - _play.Width / 2, cy - _play.Height / 2 - 10);

            if (_bEditor == null) return;
            _bEditor.Location = new Point(cx - 420, cy - 80);
            _bOnline.Location = new Point(cx - 420, cy + 0);
            _bSaved.Location = new Point(cx + 230, cy - 80);
            _bDaily.Location = new Point(cx + 230, cy + 0);

            int y = ClientSize.Height - 86;
            int bx = cx - 165;
            foreach (var b in new[] { _bProfile, _bFriends, _bSettings, _bSync, _bAdmin })
            {
                if (b == null) continue;
                b.Location = new Point(bx, y);
                if (b.Visible) bx += 72;
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var f = Gd.Title(44f))
                Gd.Outlined(g, "GEO DASH", f, new RectangleF(0, 28, ClientSize.Width, 60), Gd.Paper, thick: 5f);
            using (var f = Gd.Bold(11f))
                Gd.Outlined(g, "by " + AppConfig.Brand + "   ·   v" + Updater.CurrentVersion, f,
                    new RectangleF(0, 90, ClientSize.Width, 22), Gd.Neon, thick: 2.4f);

            // карточка аккаунта
            var card = new RectangleF(ClientSize.Width - 304, 16, 288, 92);
            Gd.Glass(g, card);
            string name = AppConfig.LoggedIn ? AppConfig.Settings.UserName : "Гость";
            using (var f = Gd.Title(14f))
                Gd.Outlined(g, name, f, new RectangleF(card.X + 14, card.Y + 8, card.Width - 28, 26), Gd.Paper, StringAlignment.Near, thick: 2.6f);

            var m = _me ?? LocalStore.CachedMe();
            int stars = m?.Stars ?? 0, coins = m?.Coins ?? 0, demons = m?.Demons ?? 0;
            float ix = card.X + 14, iy = card.Y + 42;
            void Stat(GdIcon ic, string val, Color c)
            {
                Gd.Icon(g, ic, new RectangleF(ix, iy, 22, 22), c);
                using var f = Gd.Bold(11f);
                Gd.Outlined(g, val, f, new RectangleF(ix + 24, iy, 54, 22), Gd.Paper, StringAlignment.Near, thick: 2.2f);
                ix += 84;
            }
            Stat(GdIcon.Star, stars.ToString(), Gd.Gold);
            Stat(GdIcon.Coin, coins.ToString(), Gd.Gold);
            Stat(GdIcon.Admin, demons.ToString(), Color.FromArgb(220, 110, 255));

            if (!AppConfig.LoggedIn)
            {
                using var f = Gd.Bold(9f);
                Gd.Outlined(g, "нажми на иконку профиля, чтобы войти", f,
                    new RectangleF(card.X + 10, card.Bottom - 22, card.Width - 20, 18), Gd.Neon, thick: 2f);
            }

            // статус внизу
            var bar = new RectangleF(0, ClientSize.Height - 26, ClientSize.Width, 26);
            using (var br = new SolidBrush(Color.FromArgb(170, 8, 6, 22))) g.FillRectangle(br, bar);
            using (var f = Gd.Bold(9f))
            {
                Gd.Outlined(g, _status, f, new RectangleF(10, bar.Y, ClientSize.Width - 240, 24), Gd.Paper, StringAlignment.Near, thick: 2f);
                var (txt, col) = ApiClient.Online
                    ? ("● ОНЛАЙН  " + AppConfig.Settings.ServerHost + ":" + AppConfig.Settings.ServerPort, Gd.Lime)
                    : ("● ОФЛАЙН — играем на локальных данных", Gd.Gold);
                Gd.Outlined(g, txt, f, new RectangleF(ClientSize.Width - 420, bar.Y, 410, 24), col, StringAlignment.Far, thick: 2f);
            }

            if (LocalStore.Queue.Count > 0)
            {
                using var f = Gd.Bold(9f);
                Gd.Outlined(g, "в очереди на отправку: " + LocalStore.Queue.Count, f,
                    new RectangleF(10, ClientSize.Height - 50, 400, 20), Gd.Gold, StringAlignment.Near, thick: 2f);
            }
        }

        void Open(Form f)
        {
            Sfx.Play("click");
            using (f) f.ShowDialog(this);
            _bAdmin.Visible = AppConfig.IsAdminUser;
            Layout2();
            Invalidate();
        }

        // ------------------------------------------------------------ запуск

        async Task StartupAsync()
        {
            Sfx.Init();
            Sfx.Volume = (float)AppConfig.Settings.SfxVolume;
            MusicPlayer.SetVolume(AppConfig.Settings.MusicVolume);

            Status("Готовлю встроенный саундтрек…");
            await Task.Run(() => Soundtrack.EnsureAll(new Progress<string>(Status)));
            Status("Готовлю основные уровни…");
            await Task.Run(() => OfficialLevels.EnsureFiles());

            _ = MusicPlayer.PlayAsync(-1, 0, 0.6, true);

            await SyncAsync();
            if (AppConfig.Settings.AutoUpdate) await CheckUpdateAsync();
        }

        async Task SyncAsync()
        {
            Status("Синхронизация…");
            await LocalStore.SyncAsync(new Progress<string>(Status));
            await RefreshAccountAsync(true);
            Status(ApiClient.Online ? "Готово" : "Офлайн-режим: доступны локальные и сохранённые уровни");
            Invalidate();
        }

        async Task RefreshAccountAsync(bool force)
        {
            if (!AppConfig.LoggedIn) { _me = null; Invalidate(); return; }
            var r = await ApiClient.MeAsync();
            if (r.Ok) _me = r.Data;
            else _me = LocalStore.CachedMe();
            Invalidate();
        }

        async Task CheckUpdateAsync()
        {
            var info = await Updater.CheckAsync();
            if (info == null) return;
            if (!Updater.IsNewer(info.version, Updater.CurrentVersion)) return;
            var ask = MessageBox.Show(
                $"Есть новая версия {info.version} (у тебя {Updater.CurrentVersion}).\n\n{info.notes}\n\nОбновить сейчас?",
                "Обновление GEO DASH", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (ask != DialogResult.Yes) return;
            Status("Скачиваю обновление…");
            if (await Updater.DownloadAndApplyAsync(info, new Progress<string>(Status)))
                Application.Exit();
        }

        void Status(string s)
        {
            if (InvokeRequired) { try { BeginInvoke(() => Status(s)); } catch { } return; }
            _status = s;
            Invalidate();
        }
    }
}
