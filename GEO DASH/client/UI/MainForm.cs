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

        /// <summary>Панель, в которой живут все экраны игры (как activity на телефоне).</summary>
        readonly Panel _screens = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(12, 10, 30) };

        public MainForm()
        {
            Text = AppConfig.AppName + " — " + AppConfig.Brand;
            ClientSize = new Size(1180, 720);
            MinimumSize = new Size(1000, 640);
            StartPosition = FormStartPosition.CenterScreen;

            Controls.Add(_screens);
            _screens.BringToFront();
            Nav.Attach(this, _screens);

            FullScreen.Hook(this);
            // полный экран включаем и до показа, и после: так он встаёт на любом экране
            Load += (s, e) => { if (AppConfig.Settings.Fullscreen) FullScreen.Apply(this, true); };
            Shown += (s, e) =>
            {
                if (AppConfig.Settings.Fullscreen && !FullScreen.IsOn(this)) FullScreen.Apply(this, true);
                Layout2();
                Content.Invalidate();
            };

            BuildUi();
            Resize += (s, e) => Layout2();
            Shown += async (s, e) => await StartupAsync();
            _poll.Tick += async (s, e) =>
            {
                await RefreshAccountAsync(false);
                CheckMaintenance();
            };
            _poll.Start();
            FormClosed += (s, e) => { MusicPlayer.Stop(); Sfx.Dispose(); };

            // музыка меню не играет, когда игра свёрнута или ушла в фон
            Deactivate += (s, e) => BeginInvoke(() => { if (ActiveForm == null) MusicPlayer.Pause(); });
            Activated += (s, e) => MusicPlayer.Resume();
            Resize += (s, e) => { if (WindowState == FormWindowState.Minimized) MusicPlayer.Pause(); };
        }

        bool _closingForMaintenance;

        /// <summary>
        /// Сервер закрыли на обслуживание — показываем, что случилось, и выходим.
        /// Иначе игрок остался бы в меню, где ничего не работает.
        /// </summary>
        void CheckMaintenance()
        {
            if (_closingForMaintenance || string.IsNullOrEmpty(ApiClient.Maintenance)) return;
            if (AppConfig.IsAdminUser) return;      // админу дают доделать дела

            _closingForMaintenance = true;
            MusicPlayer.Stop();
            GdDialog.Info(this, "СЕРВЕР ОБНОВЛЯЕТСЯ", ApiClient.Maintenance +
                "\n\nИгра закроется. Зайдите через пару минут.");
            Close();
        }

        GdButton _bEditor, _bOnline, _bProfile, _bFriends, _bSettings, _bAdmin, _bSaved, _bDaily, _bSync, _bChat, _bIcons;

        void BuildUi()
        {
            _play = new GdPlayButton { Size = new Size(210, 210) };
            _play.Click += (s, e) => { Sfx.Play("click"); Open(new PlayMenuForm()); };
            Host(_play);

            _bEditor = Add("EDITOR", GdIcon.Edit, 0, 0, 190, 64, Color.FromArgb(255, 170, 60), Color.FromArgb(180, 90, 10),
                (s, e) => Open(new EditorMenuForm()), "мои уровни и редактор");
            _bOnline = Add("CREATOR", GdIcon.Online, 0, 0, 190, 64, Color.FromArgb(80, 160, 255), Color.FromArgb(20, 70, 180),
                (s, e) => Open(new CreatorForm()), "поиск, daily, игроки");
            _bSaved = Add("SAVED", GdIcon.Download, 0, 0, 190, 64, Color.FromArgb(120, 230, 160), Color.FromArgb(30, 130, 80),
                (s, e) => Open(new LevelListForm(LevelListMode.Saved)), "офлайн-уровни");
            _bDaily = Add("DAILY / WEEKLY", GdIcon.Daily, 0, 0, 190, 64, Color.FromArgb(210, 130, 255), Color.FromArgb(110, 40, 180),
                (s, e) => Open(new DailyForm()), "уровень дня и недели");

            _bProfile = Add("", GdIcon.Profile, 0, 0, 84, 86, Color.FromArgb(90, 190, 255), Color.FromArgb(20, 90, 170),
                (s, e) => Open(new ProfileForm(AppConfig.Settings.UserName)));
            _bProfile.Caption = "ПРОФИЛЬ";
            _bFriends = Add("", GdIcon.Friends, 0, 0, 84, 86, Color.FromArgb(255, 140, 200), Color.FromArgb(150, 40, 110),
                (s, e) => Open(new FriendsForm()));
            _bFriends.Caption = "ДРУЗЬЯ";
            _bSettings = Add("", GdIcon.Settings, 0, 0, 84, 86, Color.FromArgb(160, 170, 200), Color.FromArgb(70, 80, 120),
                (s, e) => Open(new SettingsForm()));
            _bSettings.Caption = "НАСТРОЙКИ";
            _bSync = Add("", GdIcon.Refresh, 0, 0, 84, 86, Color.FromArgb(120, 220, 180), Color.FromArgb(30, 120, 100),
                async (s, e) => await SyncAsync());
            _bSync.Caption = "СИНХРО";
            _bIcons = Add("", GdIcon.Star, 0, 0, 84, 86, Color.FromArgb(255, 200, 90), Color.FromArgb(170, 115, 10),
                (s, e) => Open(new IconsForm()));
            _bIcons.Caption = "ИКОНКИ";
            _bChat = Add("", GdIcon.Online, 0, 0, 84, 86, Color.FromArgb(120, 200, 255), Color.FromArgb(25, 95, 190),
                (s, e) => Open(new ChatListForm()));
            _bChat.Caption = "ЧАТЫ";
            _bAdmin = Add("", GdIcon.Admin, 0, 0, 84, 86, Color.FromArgb(255, 110, 110), Color.FromArgb(150, 30, 40),
                (s, e) => Open(new AdminForm()));
            _bAdmin.Caption = "АДМИНКА";
            _bAdmin.Visible = AppConfig.IsAdminUser;

            Layout2();
        }

        void Layout2()
        {
            float k = UiK;                       // в полном экране всё крупнее
            int cx = ClientSize.Width / 2, cy = ClientSize.Height / 2 + (int)(10 * k);
            if (_play != null) _play.Location = new Point(cx - _play.Width / 2, cy - _play.Height / 2 - (int)(10 * k));

            if (_bEditor == null) return;
            int side = (int)(420 * k), right = (int)(230 * k), row = (int)(80 * k);
            _bEditor.Location = new Point(cx - side, cy - row);
            _bOnline.Location = new Point(cx - side, cy + 0);
            _bSaved.Location = new Point(cx + right, cy - row);
            _bDaily.Location = new Point(cx + right, cy + 0);

            int y = ClientSize.Height - (int)(112 * k);
            int count = 6 + (AppConfig.IsAdminUser ? 1 : 0);
            int stepX = (int)(92 * k);
            int bx = cx - (count * stepX - (int)(8 * k)) / 2;
            foreach (var b in new[] { _bProfile, _bIcons, _bFriends, _bChat, _bSettings, _bSync, _bAdmin })
            {
                if (b == null) continue;
                b.Location = new Point(bx, y);
                if (b.Visible) bx += stepX;
            }
            Content.Invalidate();
        }

        /// <summary>После масштабирования кнопки нужно расставить заново.</summary>
        protected override void OnUiScaled() => Layout2();

        protected override void OnRender(Graphics g)
        {
                        using (var f = Gd.Title(44f))
                Gd.Outlined(g, "GEO DASH", f, new RectangleF(0, 28, ClientSize.Width, 60), Gd.Paper, thick: 5f);
            using (var f = Gd.Bold(11f))
                Gd.Outlined(g, "by " + AppConfig.Brand, f,
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
                Gd.Outlined(g, _status, f, new RectangleF(84, bar.Y, ClientSize.Width - 320, 24), Gd.Paper, StringAlignment.Near, thick: 2f);
                var (txt, col) = ApiClient.Online
                    ? ("● ОНЛАЙН · " + AppConfig.ServerLabel, Gd.Lime)
                    : ("● ОФЛАЙН — играем на локальных данных", Gd.Gold);
                Gd.Outlined(g, txt, f, new RectangleF(ClientSize.Width - 420, bar.Y, 410, 24), col, StringAlignment.Far, thick: 2f);
            }

            // версия игры — мелким текстом в самом углу слева снизу
            using (var fv = Gd.Bold(8f))
                Gd.Outlined(g, "v" + Updater.CurrentVersion, fv,
                    new RectangleF(10, ClientSize.Height - 25, 80, 24),
                    Color.FromArgb(220, 190, 205, 240), StringAlignment.Near, thick: 1.8f);

            if (LocalStore.Queue.Count > 0)
            {
                using var f = Gd.Bold(9f);
                Gd.Outlined(g, "в очереди на отправку: " + LocalStore.Queue.Count, f,
                    new RectangleF(10, ClientSize.Height - 50, 400, 20), Gd.Gold, StringAlignment.Near, thick: 2f);
            }
        }

        /// <summary>Открыть экран внутри панели — окон больше не появляется.</summary>
        new void Open(Form f)
        {
            Sfx.Play("click");
            f.FormClosed += (s, e) =>
            {
                if (IsDisposed) return;
                _bAdmin.Visible = AppConfig.IsAdminUser;
                Layout2();
                Content.Invalidate();
            };
            Nav.Go(f);
        }

        // ------------------------------------------------------------ запуск

        async Task StartupAsync()
        {
            Sfx.Init();
            LoadIcon();
            Sfx.Volume = (float)AppConfig.Settings.SfxVolume;
            MusicPlayer.SetVolume(AppConfig.Settings.MusicVolume);

            Status("Готовлю встроенный саундтрек…");
            await Task.Run(() => Soundtrack.EnsureAll(new Progress<string>(Status)));
            Status("Готовлю основные уровни…");
            await Task.Run(() => OfficialLevels.EnsureFiles());

            _ = MusicPlayer.PlayAsync(-1, 0, 0.6, true);

            // игру открыли файлом уровня — сразу показываем его
            if (!string.IsNullOrEmpty(Program.OpenLevelPath))
            {
                var p = Program.OpenLevelPath;
                Program.OpenLevelPath = null;
                Program.OpenLevelFile(p);
            }

            await SyncAsync();
            if (AppConfig.Settings.AutoUpdate) await CheckUpdateAsync();
        }

        async Task SyncAsync()
        {
            Status("Синхронизация…");
            await LocalStore.SyncAsync(new Progress<string>(Status));
            await RefreshAccountAsync(true);
            Status(ApiClient.Online ? "Готово" : "Офлайн-режим: доступны локальные и сохранённые уровни");
            Content.Invalidate();
        }

        async Task RefreshAccountAsync(bool force)
        {
            if (!AppConfig.LoggedIn) { _me = null; Content.Invalidate(); return; }
            var r = await ApiClient.MeAsync();
            if (r.Ok) _me = r.Data;
            else _me = LocalStore.CachedMe();
            Content.Invalidate();
        }

        /// <summary>Проверка обновления — только когда сервер онлайн; показывает диалог обновления.</summary>
        async Task CheckUpdateAsync()
        {
            if (!ApiClient.Online && !await ApiClient.PingAsync()) return;

            var info = await Updater.CheckAsync();
            if (info == null) return;
            if (!Updater.IsNewer(info.version, Updater.CurrentVersion))
            {
                Status("Версия последняя: " + Updater.CurrentVersion);
                return;
            }

            Status($"Доступно обновление {info.version}");
            Toasts.Show("Есть обновление " + info.version, info.notes, Toasts.Kind.Download, 7);
            using var f = new UpdateForm(info);
            f.ShowDialog(this);
            Content.Invalidate();
        }

        /// <summary>Своя картинка кубика подхватывается при запуске.</summary>
        static void LoadIcon()
        {
            try
            {
                var p = AppConfig.Settings.IconImage;
                if (!string.IsNullOrWhiteSpace(p) && File.Exists(p))
                    Render.IconKit.SetCustom(p, Image.FromFile(p));
            }
            catch { }
        }

        void Status(string s)
        {
            if (InvokeRequired) { try { BeginInvoke(() => Status(s)); } catch { } return; }
            _status = s;
            Content.Invalidate();
        }
    }
}
