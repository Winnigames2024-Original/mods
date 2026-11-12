using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NareLauncher.Core;
using NareLauncher.UI.Pages;

namespace NareLauncher.UI
{
    public class MainForm : Form
    {
        private const int TitleHeight = 46;
        private const int SidebarWidth = 218;
        private const int ResizeBorder = 6;

        private Panel _sidebar;
        private Panel _content;
        private Panel _titleBar;
        private readonly List<NavButton> _nav = new List<NavButton>();
        private ClickFxLayer _clickFx;
        private readonly List<PageBase> _pages = new List<PageBase>();

        public HomePage Home;
        public VersionsPage VersionsTab;
        public ModsPage ModsTab;
        public InstancePage InstanceTab;
        public ServersPage ServersTab;
        public HostPage HostTab;
        public VideosPage VideosTab;
        public SkinPage SkinTab;
        public CosmeticsPage CosmeticsTab;
        public AccountsPage AccountsTab;
        public SettingsPage SettingsTab;
        public ConsolePage ConsoleTab;
        public TogetherPage TogetherTab;
        public AdminPage AdminTab;
        public AdServersPage AdTab;
        public ChatsPage ChatsTab;
        public ProfilePage ProfileTab;

        private NareLabel _accountChip;

        private BanOverlay _banOverlay;
        private ToastHost _toasts;
        private NotifyIcon _tray;
        private long _chatSeen = -1;       // игрок: время последнего увиденного ответа админа
        private long _adminChatSeen = -1;  // админ: время последнего увиденного сообщения игрока
        private readonly System.Windows.Forms.Timer _guard = new System.Windows.Forms.Timer { Interval = 15000 };
        private readonly System.Windows.Forms.Timer _eventTimer = new System.Windows.Forms.Timer { Interval = 45000 };
        private readonly System.Collections.Generic.HashSet<string> _eventSeen = new System.Collections.Generic.HashSet<string>();
        private Panel _eventBanner;
        private System.Drawing.Image _bannerImg;
        private int _warnShown = -1;     // сколько предупреждений уже показали (с диска, на игрока)
        private long _broadcastShown = -1; // время последнего показанного объявления (с диска)
        private Panel _adminBar;          // верхняя полоса-объявление от админа
        private long _restartHandledL = -1; // время отработанного перезапуска лаунчера
        private bool _updatePrompting;   // чтобы окно обновления не всплывало дважды
        private long _updateHandledAt = -1; // время последнего отработанного экстренного обновления
        private bool _loggingOut;        // идёт выход после отвязки устройства
        private string _guardNick = "";  // чей статус мы отслеживаем

        public MainForm()
        {
            Text = "NareLauncher";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1120, 700);
            Size = new Size(1460, 900);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(9.5f);
            DoubleBuffered = true;
            KeyPreview = true;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            BuildUi();
            Navigate(0);
        }

        // ------------------------------------------------------------ сборка

        private void BuildUi()
        {
            _content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            _sidebar = new Panel { Dock = DockStyle.Left, Width = SidebarWidth, BackColor = Theme.Sidebar };
            _titleBar = new Panel { Dock = DockStyle.Top, Height = TitleHeight, BackColor = Theme.Sidebar };

            _banOverlay = new BanOverlay();
            _content.Controls.Add(_banOverlay);

            _toasts = new ToastHost();
            _content.Controls.Add(_toasts);
            _eventBanner = new Panel { Dock = DockStyle.Top, Height = 0, BackColor = Theme.Panel, Visible = false };
            _content.Controls.Add(_eventBanner);
            _eventBanner.BringToFront();

            // системный значок в трее — для уведомлений Windows (как у Telegram)
            try
            {
                _tray = new NotifyIcon
                {
                    Icon = this.Icon ?? System.Drawing.SystemIcons.Application,
                    Text = "Nare Launcher",
                    Visible = true
                };
                _tray.BalloonTipClicked += (s, e) => { ShowFromTray(); OpenChats(); };
                _tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowFromTray(); };
            }
            catch { _tray = null; }

            // порядок важен: Fill добавляется первым
            Controls.Add(_content);
            Controls.Add(_sidebar);
            Controls.Add(_titleBar);

            BuildTitleBar();
            BuildSidebar();

            Home = new HomePage { Host = this };
            VersionsTab = new VersionsPage { Host = this };
            ModsTab = new ModsPage { Host = this };
            InstanceTab = new InstancePage { Host = this };
            ServersTab = new ServersPage { Host = this };
            TogetherTab = new TogetherPage { Host = this };
            HostTab = new HostPage { Host = this };
            VideosTab = new VideosPage { Host = this };
            AdTab = new AdServersPage { Host = this };
            AdminTab = new AdminPage { Host = this };
            ChatsTab = new ChatsPage { Host = this };
            ProfileTab = new ProfilePage { Host = this };
            SkinTab = new SkinPage { Host = this };
            CosmeticsTab = new CosmeticsPage { Host = this };
            AccountsTab = new AccountsPage { Host = this };
            SettingsTab = new SettingsPage { Host = this };
            ConsoleTab = new ConsolePage { Host = this };

            _pages.Add(Home);
            _pages.Add(VersionsTab);
            _pages.Add(ModsTab);
            _pages.Add(InstanceTab);
            _pages.Add(ServersTab);
            _pages.Add(TogetherTab);
            _pages.Add(HostTab);
            _pages.Add(VideosTab);
            _pages.Add(SkinTab);
            _pages.Add(CosmeticsTab);
            _pages.Add(AccountsTab);
            _pages.Add(SettingsTab);
            _pages.Add(ConsoleTab);
            _pages.Add(ProfileTab);
            _pages.Add(AdTab);
            _pages.Add(ChatsTab);
            _pages.Add(AdminTab);

            foreach (var p in _pages)
            {
                p.Visible = false;
                _content.Controls.Add(p);
            }
        }

        private void BuildTitleBar()
        {
            _titleBar.Paint += (s, e) =>
            {
                var g = e.Graphics;
                Theme.Smooth(g);
                using (var b = new SolidBrush(Theme.Sidebar))
                    g.FillRectangle(b, _titleBar.ClientRectangle);

                // логотип
                var box = new Rectangle(18, (TitleHeight - 22) / 2, 22, 22);
                Theme.FillRoundGradient(g, box, 7, Theme.Accent, Theme.Accent2, 35f);
                Theme.Text1(g, "N", Theme.F(11f, FontStyle.Bold), Color.White, box,
                    StringAlignment.Center, StringAlignment.Center);

                Theme.Text1(g, "NARE", Theme.F(11.5f, FontStyle.Bold), Theme.Text,
                    new Rectangle(50, 0, 70, TitleHeight));
                Theme.Text1(g, "LAUNCHER", Theme.F(11.5f, FontStyle.Regular), Theme.Dim,
                    new Rectangle(99, 0, 120, TitleHeight));

                using (var pen = new Pen(Color.FromArgb(28, 255, 255, 255)))
                    g.DrawLine(pen, 0, TitleHeight - 1, _titleBar.Width, TitleHeight - 1);
            };

            _titleBar.MouseDown += TitleDrag;

            var close = new NareButton
            {
                Style = BtnStyle.Ghost,
                Icon = Ico.Close,
                Text = "",
                Size = new Size(44, 30),
                Radius = 8
            };
            var min = new NareButton
            {
                Style = BtnStyle.Ghost,
                Icon = Ico.Minimize,
                Text = "",
                Size = new Size(44, 30),
                Radius = 8
            };

            close.Click += (s, e) => Close();
            min.Click += (s, e) => WindowState = FormWindowState.Minimized;

            _titleBar.Controls.Add(close);
            _titleBar.Controls.Add(min);

            EventHandler layout = (s, e) =>
            {
                close.Location = new Point(_titleBar.Width - 54, (TitleHeight - close.Height) / 2);
                min.Location = new Point(_titleBar.Width - 104, (TitleHeight - min.Height) / 2);
            };
            _titleBar.Resize += layout;
            layout(this, EventArgs.Empty);
        }

        private void TitleDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); // WM_NCLBUTTONDOWN / HTCAPTION
        }

        private void BuildSidebar()
        {
            string[] titles = { "Играть", "Версии", "Моды и паки", "Сборка", "Серверы", "Вместе", "Мои серверы", "Видео", "Скин", "Вещи", "Аккаунты", "Настройки", "Консоль", "Профиль", "Публичные", "Чаты", "Админ" };
            Ico[] icons = { Ico.Play, Ico.Versions, Ico.Mods, Ico.Folder, Ico.Server, Ico.Users, Ico.Folder, Ico.Video, Ico.Skin, Ico.Skin, Ico.Account, Ico.Settings, Ico.Console, Ico.Account, Ico.Search, Ico.Chat, Ico.Settings };

            int y = 12;
            for (int i = 0; i < titles.Length; i++)
            {
                int index = i;
                var btn = new NavButton
                {
                    Text = titles[i],
                    Icon = icons[i],
                    Bounds = new Rectangle(8, y, SidebarWidth - 16, 36)
                };
                btn.Click += (s, e) => Navigate(index);
                _sidebar.Controls.Add(btn);
                _nav.Add(btn);
                y += 39;
            }

            // Кнопка студии трансляции — открывает отдельное окно, не вкладку
            var studioBtn = new NavButton { Text = "Эфир", Icon = Ico.Video, Bounds = new Rectangle(8, y, SidebarWidth - 16, 36) };
            studioBtn.Click += (s, e) => OpenStudio();
            _sidebar.Controls.Add(studioBtn);
            y += 39;

            // Быстрые ссылки на разделы сайта (открываются в браузере)
            foreach (var lk in new[] { new[] { "Медиа", "/media/" }, new[] { "Каналы", "/channels/" } })
            {
                string url = "https://narelauncher.winnigames2024.ru" + lk[1];
                var lb = new NavButton { Text = lk[0], Icon = Ico.Chat, Bounds = new Rectangle(8, y, SidebarWidth - 16, 36) };
                lb.Click += (s, e) => { try { System.Diagnostics.Process.Start(url); } catch { } };
                _sidebar.Controls.Add(lb);
                y += 39;
            }

            _accountChip = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted)
            {
                Bounds = new Rectangle(18, 0, SidebarWidth - 36, 40),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _sidebar.Controls.Add(_accountChip);

            var version = new NareLabel("v" + GameLauncher.LauncherVersion, 8.5f, FontStyle.Regular, Theme.Dim)
            {
                Bounds = new Rectangle(18, 0, 120, 20)
            };
            _sidebar.Controls.Add(version);

            EventHandler bottom = (s, e) =>
            {
                _accountChip.Location = new Point(18, _sidebar.Height - 62);
                version.Location = new Point(18, _sidebar.Height - 30);
            };
            _sidebar.Resize += bottom;
            bottom(this, EventArgs.Empty);

            _sidebar.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(28, 255, 255, 255)))
                    e.Graphics.DrawLine(pen, _sidebar.Width - 1, 0, _sidebar.Width - 1, _sidebar.Height);
            };
        }

        /// <summary>Открыть студию трансляции (захват экрана/окна).</summary>
        public void OpenStudio()
        {
            try { new StreamForm().Show(); }
            catch (Exception ex) { ShowError("Не удалось открыть студию трансляции", ex); }
        }

        // ------------------------------------------------------------ навигация

        public void Navigate(int index)
        {
            if (index < 0 || index >= _pages.Count) return;

            for (int i = 0; i < _nav.Count; i++)
            {
                _nav[i].Active = (i == index);
                _nav[i].Invalidate();
            }

            for (int i = 0; i < _pages.Count; i++)
                _pages[i].Visible = (i == index);

            _pages[index].BringToFront();
            if (_eventBanner != null && _eventBanner.Visible) _eventBanner.BringToFront();
            try { _pages[index].PageShown(); }
            catch (Exception ex) { Log.Error("Ошибка на странице " + index, ex); }
        }

        /// <summary>Персонаж на главной перечитывает скин и надетые вещи.</summary>
        public void RefreshPlayer()
        {
            try { if (Home != null) Home.RefreshPlayer(); }
            catch (Exception ex) { Log.Warn("Персонаж не обновился: " + ex.Message); }
        }

        public void RefreshAccountChip()
        {
            var acc = AccountStore.Selected;
            _accountChip.Text = acc == null ? "Аккаунт не выбран" : "Вы вошли как  " + acc.Name;
            _accountChip.ForeColor = acc == null ? Theme.Warn : Theme.Muted;
            RefreshAdminNav();
        }

        /// <summary>
        /// Следит, чтобы у активного аккаунта была сессия на сервере. Нет
        /// пароля — открывает окно ввода. Возвращает false, если так и не вошли.
        /// </summary>
        public bool EnsureAuth()
        {
            var acc = AccountStore.Selected;
            if (acc == null) return false;
            if (!string.IsNullOrEmpty(acc.Token)) return true;

            using (var form = new AuthForm(acc))
            {
                form.ShowDialog(this);
                if (form.Done)
                {
                    RefreshAccountChip();
                    CheckPolicy();
                    return true;
                }
            }
            return false;
        }

        /// <summary>Кнопка «Админ» видна только администратору.</summary>
        public void RefreshAdminNav()
        {
            var acc = AccountStore.Selected;
            bool admin = (acc != null && acc.Admin) || NareAuth.Last.IsAdmin || NareAuth.Last.IsModerator;

            int adminIndex = _pages.IndexOf(AdminTab);
            if (adminIndex >= 0 && adminIndex < _nav.Count)
            {
                _nav[adminIndex].Visible = admin;
                _nav[adminIndex].Invalidate();
            }
        }

        /// <summary>Открывает окно управления аккаунтами и обновляет интерфейс после закрытия.</summary>
        public void ManageAccounts()
        {
            using (var form = new AccountsForm())
                form.ShowDialog(this);

            AccountStore.Save();
            RefreshAccountChip();
            AccountsTab.ReloadList();
            Home.Invalidate();
        }

        public void ShowError(string title, Exception ex)
        {
            ShowError(title, ex, null);
        }

        /// <summary>
        /// Своё окно ошибки. retry — что сделать по кнопке «Повторить»;
        /// если передать null, кнопки не будет.
        /// </summary>
        public void ShowError(string title, Exception ex, Action retry)
        {
            Log.Error(title, ex);

            try
            {
                using (var form = new ErrorForm(title, ex, retry))
                    form.ShowDialog(this);
            }
            catch (Exception own)
            {
                Log.Warn("Окно ошибки не открылось: " + own.Message);
                MessageBox.Show(this, title + Environment.NewLine + Environment.NewLine
                                    + (ex == null ? "" : ex.Message),
                    "NareLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void ShowInfo(string text)
        {
            MessageBox.Show(this, text, "NareLauncher", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public bool Confirm(string text)
        {
            return MessageBox.Show(this, text, "NareLauncher",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        // ------------------------------------------------------------ окно

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            RefreshAccountChip();

            // слой с откликом на щелчки — отдельное прозрачное окно поверх
            try { _clickFx = new ClickFxLayer(this); }
            catch (Exception ex) { Log.Warn("Эффекты щелчка не включились: " + ex.Message); }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // пришли по ссылке с сайта: ник, установка версии и запуск
            var link = PlayLink.Pending;
            PlayLink.Pending = null;

            if (link != null && link.HasVersion)
            {
                string what = "Minecraft " + link.Version + " · " + PlayLink.LoaderRu(link.Loader);
                if (!EnsureAccount(what)) return;

                Navigate(0);
                Home.PlayFromLink(link);
                return;
            }

            // ссылка «зайти на сервер» с сайта: версии нет, но есть адрес —
            // запускаем выбранную версию и сразу коннектимся на сервер
            if (link != null && !string.IsNullOrEmpty(link.Server))
            {
                if (!EnsureAccount("вход на сервер " + link.Server)) return;
                Navigate(0);
                Home.LaunchOn(link.Server);
                return;
            }

            // ссылка «студия трансляции» с сайта (narelauncher://stream)
            if (link != null && link.OpenStudio)
            {
                if (EnsureAccount("трансляцию")) OpenStudio();
                return;
            }

            // Первый запуск: без аккаунта играть нельзя — сразу открываем окно
            if (AccountStore.Accounts.Count == 0)
                ManageAccounts();

            // у активного аккаунта нет пароля на сервере — попросим сразу
            var active = AccountStore.Selected;
            if (active != null && string.IsNullOrEmpty(active.Token)) EnsureAuth();

            _guard.Tick += delegate { CheckPolicy(); };
            _guard.Start();
            _eventTimer.Tick += delegate { CheckEvents(); };
            _eventTimer.Start();
            var evKick = new System.Windows.Forms.Timer { Interval = 2500 };
            evKick.Tick += delegate { evKick.Stop(); evKick.Dispose(); CheckEvents(); };
            evKick.Start();
            var minuteTimer = new System.Windows.Forms.Timer { Interval = 60000 };
            minuteTimer.Tick += delegate { ClaimMinuteReward(); };
            minuteTimer.Start();

            // удалённое управление лаунчером с сайта (работает только пока включено в настройках)
            Core.RemoteControl.Start(this);

            CheckPolicy();
        }

        /// <summary>
        /// Спрашивает сервер про текущий аккаунт: бан, предупреждения,
        /// объявление и экстренное обновление. Если сервер молчит — живём
        /// как обычно, из-за недоступного сервера никого не запираем.
        /// </summary>
        private async void CheckPolicy()
        {
            var acc = AccountStore.Selected;
            string nick = acc == null ? "" : acc.Name;

            if (nick != _guardNick)      // сменили аккаунт — показываем всё заново
            {
                _guardNick = nick;
                _warnShown = -1;
                _broadcastShown = -1;
                _chatSeen = -1;
                _adminChatSeen = -1;
            }

            if (string.IsNullOrEmpty(nick)) { _banOverlay.Hide1(); return; }

            PlayerPolicy policy;
            string tok = acc == null ? null : acc.Token;
            try { policy = await NareAuth.StatusAsync(nick, System.Threading.CancellationToken.None, tok); }
            catch { return; }            // сервер недоступен — не трогаем ничего

            if (!policy.Known) return;

            // устройство отвязали в другом месте — выходим из аккаунта на этом ПК
            if (policy.LoggedOut && acc != null && !string.IsNullOrEmpty(acc.Token) && !_loggingOut)
            {
                _loggingOut = true;
                acc.Token = "";
                AccountStore.Save();
                _banOverlay.Hide1();
                MessageBox.Show(this,
                    "Это устройство отвязали от аккаунта. Войдите снова, если это были вы.",
                    "Устройство отвязано", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _loggingOut = false;
                EnsureAuth();
                return;
            }

            NareAuth.Last = policy;

            // сервер может назначить игрока админом/модератором — подхватываем
            if (acc != null && acc.Admin != policy.IsAdmin)
            {
                acc.Admin = policy.IsAdmin;
                AccountStore.Save();
            }
            RefreshAdminNav();

            // сервер — хозяин игрового ника: подхватываем его на аккаунт
            if (!string.IsNullOrEmpty(policy.Username) && acc != null
                && !string.Equals(acc.Username, policy.Username, StringComparison.Ordinal)
                && !string.Equals(policy.Username, acc.Name, StringComparison.Ordinal))
            {
                acc.Username = policy.Username;
                AccountStore.Save();
            }

            RefreshAdminNav();

            // бан
            if (policy.Banned)
            {
                _banOverlay.Show(nick, policy.BanReason, policy.BanLeft);
            }
            else
            {
                _banOverlay.Hide1();
            }

            // предупреждения — показываем новые
            if (_warnShown < 0) _warnShown = (int)EmergencyMark.LoadKey("warns_" + nick);
            if (_warnShown > policy.Warnings.Count) _warnShown = policy.Warnings.Count;
            for (int i = _warnShown; i < policy.Warnings.Count; i++)
            {
                MessageBox.Show(this, policy.Warnings[i],
                    "Предупреждение от администратора", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            if (policy.Warnings.Count != _warnShown)
            {
                _warnShown = policy.Warnings.Count;
                EmergencyMark.SaveKey("warns_" + nick, _warnShown);
            }

            // объявление от админа — верхняя полоса, пока активно (по времени)
            bool bcActive = policy.HasBroadcast && !string.IsNullOrEmpty(policy.Broadcast)
                && (policy.BroadcastUntil == 0 || NowMs() < policy.BroadcastUntil);
            if (bcActive) ShowAdminBar(policy.BroadcastNick, policy.Broadcast);
            else HideAdminBar();

            // перезапуск лаунчера по команде админа
            if (_restartHandledL < 0) _restartHandledL = EmergencyMark.LoadKey("restartL");
            if (policy.RestartLaunchers > _restartHandledL)
            {
                _restartHandledL = policy.RestartLaunchers;
                EmergencyMark.SaveKey("restartL", policy.RestartLaunchers);
                try { Application.Restart(); } catch { }
                try { Environment.Exit(0); } catch { }
            }

            // экстренное обновление — срабатывает по времени объявления, а не по
            // номеру версии: смысл в том, чтобы закрыть игру у всех здесь и сейчас
            if (_updateHandledAt < 0) _updateHandledAt = EmergencyMark.Load();

            if (policy.HasUpdate && policy.Critical && !_updatePrompting
                && policy.UpdateAt > _updateHandledAt)
            {
                _updatePrompting = true;
                _updateHandledAt = policy.UpdateAt;
                EmergencyMark.Save(policy.UpdateAt);   // чтобы после перезапуска не повторялось
                using (var form = new EmergencyUpdateForm(policy.UpdateVersion, policy.Changelog))
                    form.ShowDialog(this);
                _updatePrompting = false;
            }

            await CheckChats(acc, policy);
        }

        /// <summary>Уведомления о новых сообщениях в чате — как в Steam.</summary>
        private async System.Threading.Tasks.Task CheckChats(Account acc, PlayerPolicy policy)
        {
            if (acc == null || string.IsNullOrEmpty(acc.Token) || _toasts == null) return;
            try
            {
                if (policy.IsAdmin)
                {
                    string me = acc.Name;
                    var chats = await NareAuth.AdminChatsAsync(acc.Token, System.Threading.CancellationToken.None);
                    long maxAt = 0; NareAuth.ChatSummary newest = null;
                    foreach (var c in chats)
                    {
                        // чужие закреплённые чаты не дёргают — только нераспределённые и свои
                        bool mine = string.IsNullOrEmpty(c.Assigned) || string.Equals(c.Assigned, me, StringComparison.Ordinal);
                        if (!c.LastAdmin && mine && c.At > maxAt) { maxAt = c.At; newest = c; }
                    }

                    if (_adminChatSeen < 0) _adminChatSeen = maxAt;      // базовая отметка — без уведомления
                    else if (maxAt > _adminChatSeen && newest != null)
                    {
                        _adminChatSeen = maxAt;
                        var who = newest.Nick;
                        Notify(who + " написал", newest.Last);
                    }
                }
                else
                {
                    var msgs = await NareAuth.BanChatAsync(acc.Token, System.Threading.CancellationToken.None);
                    long maxAt = 0; NareAuth.ChatMessage newest = null;
                    foreach (var m in msgs)
                        if (m.Admin && m.At > maxAt) { maxAt = m.At; newest = m; }

                    if (_chatSeen < 0) _chatSeen = maxAt;
                    else if (maxAt > _chatSeen && newest != null)
                    {
                        _chatSeen = maxAt;
                        Notify("Ответ администратора", newest.Text);
                    }
                }
            }
            catch { }
        }

        /// <summary>Открыть вкладку «Чаты».</summary>
        public void OpenChats()
        {
            int i = _pages.IndexOf(ChatsTab);
            if (i >= 0) Navigate(i);
        }

        private void ShowFromTray()
        {
            try
            {
                if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
                Show();
                Activate();
                BringToFront();
            }
            catch { }
        }

        /// <summary>
        /// Уведомление о новом сообщении: всплывашка в окне + системное уведомление
        /// Windows в трее (как у Telegram) + звук.
        /// </summary>
        // ---- эвенты: монеты за заход, уведомление, картинки на главном меню ----
        private void CheckEvents()
        {
            var acc = AccountStore.Selected;
            if (acc == null || string.IsNullOrEmpty(acc.Token)) return;
            string tok = acc.Token;
            new System.Threading.Thread(() =>
            {
                try
                {
                    var list = NareLauncher.Core.NareEvents.Active(tok);
                    foreach (var ev in list)
                    {
                        if (ev.Joined || _eventSeen.Contains(ev.Id)) continue;
                        _eventSeen.Add(ev.Id);
                        var jr = NareLauncher.Core.NareEvents.Join(tok, ev.Id);
                        if (jr.Ok)
                        {
                            string t = string.IsNullOrEmpty(jr.NotifyTitle) ? ("Эвент: " + jr.Name) : jr.NotifyTitle;
                            string b = jr.NotifyText ?? "";
                            if (jr.Awarded > 0) b = (b.Length > 0 ? b + "\n" : "") + "+" + jr.Awarded + " монет";
                            if (t.Length > 0) { try { BeginInvoke((Action)(() => Notify(t, b))); } catch { } }
                        }
                    }
                    try { BeginInvoke((Action)(() => ShowEventBanner(list))); } catch { }
                }
                catch { }
            })
            { IsBackground = true }.Start();
        }

        private void ShowEventBanner(System.Collections.Generic.List<NareLauncher.Core.NareEvent> list)
        {
            NareLauncher.Core.NareEvent ev = null;
            foreach (var x in list) if (x.Active) { ev = x; break; }
            if (ev == null) { _eventBanner.Visible = false; _eventBanner.Height = 0; try { _content.PerformLayout(); } catch { } return; }

            _eventBanner.Controls.Clear();
            Color accent = ParseHex(ev.Accent, Theme.Accent);
            bool hasImgs = ev.Images != null && ev.Images.Count > 0;
            _eventBanner.Tag = accent;
            _eventBanner.Paint -= EventBannerPaint; _eventBanner.Paint += EventBannerPaint;

            var close = new Label { Text = "✕", Font = Theme.F(12f, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, BackColor = Color.Transparent, Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            close.Click += (s, e) => { _eventBanner.Visible = false; _eventBanner.Height = 0; };

            if (hasImgs)
            {
                _bannerImg = null;
                _eventBanner.BackColor = Darken(accent);
                _eventBanner.Height = 120;
                LoadBannerCover(ev.Images[0]);
                close.Location = new Point(_eventBanner.Width - 32, 8);
                _eventBanner.Controls.Add(close); close.BringToFront();
            }
            else
            {
                _bannerImg = null;
                _eventBanner.BackColor = Theme.Panel;
                _eventBanner.Height = 64;
                var title = new Label { Text = ev.Name, Font = Theme.F(13f, FontStyle.Bold), ForeColor = Theme.Text, AutoSize = false, BackColor = Color.Transparent, Bounds = new Rectangle(18, 9, _eventBanner.Width - 80, 22), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                var desc = new Label { Text = ev.Desc, ForeColor = Theme.Dim, AutoSize = false, BackColor = Color.Transparent, Bounds = new Rectangle(18, 33, _eventBanner.Width - 80, 18), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                close.Location = new Point(_eventBanner.Width - 30, 10);
                _eventBanner.Controls.Add(title); _eventBanner.Controls.Add(desc); _eventBanner.Controls.Add(close);
            }
            // Кликер 67 — отдельная кнопка на эвенте «67»
            bool is67 = (ev.Id != null && ev.Id.IndexOf("67", StringComparison.Ordinal) >= 0)
                        || (ev.Name != null && ev.Name.IndexOf("67", StringComparison.Ordinal) >= 0);
            var acc67 = AccountStore.Selected;
            if (is67 && acc67 != null && !string.IsNullOrEmpty(acc67.Token))
            {
                var play = new Button
                {
                    Text = "🪙 Кликер 67",
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.White,
                    BackColor = Darken(accent),
                    Font = Theme.F(10f, FontStyle.Bold),
                    Size = new Size(140, 32),
                    Cursor = Cursors.Hand,
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Left
                };
                play.FlatAppearance.BorderColor = accent;
                play.Location = new Point(16, _eventBanner.Height - 42);
                play.Click += (s, e) =>
                {
                    try { using (var f = new NareClickerForm(acc67.Token, accent)) f.ShowDialog(this); }
                    catch (Exception ex) { Notify("Кликер", "Не открылся: " + ex.Message); }
                };
                _eventBanner.Controls.Add(play); play.BringToFront();
            }

            _eventBanner.Visible = true;
            _eventBanner.BringToFront();
            try { _content.PerformLayout(); } catch { }
        }

        private static long NowMs() { return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); }

        /// <summary>Ежеминутная награда за онлайн — тихо начисляется, при начислении лёгкий тост.</summary>
        private async void ClaimMinuteReward()
        {
            var acc = AccountStore.Selected;
            if (acc == null || string.IsNullOrEmpty(acc.Token)) return;
            try
            {
                int got = await NareAuth.MinuteRewardAsync(acc.Token, System.Threading.CancellationToken.None);
                if (got > 0 && _toasts != null) _toasts.Push("Награда за онлайн", "+" + got + " монет", null);
            }
            catch { }
        }

        /// <summary>Верхняя полоса-объявление от админа (как плашка в Telegram).</summary>
        private void ShowAdminBar(string nick, string text)
        {
            if (_adminBar == null)
            {
                _adminBar = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Theme.Accent };
                var lbl = new Label { Name = "txt", AutoSize = false, Dock = DockStyle.Fill, ForeColor = Color.White, BackColor = Color.Transparent, Font = Theme.F(10f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(16, 0, 40, 0) };
                var x = new Label { Text = "✕", AutoSize = true, ForeColor = Color.White, BackColor = Color.Transparent, Cursor = Cursors.Hand, Font = Theme.F(11f, FontStyle.Bold), Anchor = AnchorStyles.Right | AnchorStyles.Top };
                x.Click += (s, e) => HideAdminBar();
                _adminBar.Controls.Add(lbl);
                _adminBar.Controls.Add(x);
                _adminBar.Resize += (s, e) => { x.Location = new Point(_adminBar.Width - 28, 9); };
                _content.Controls.Add(_adminBar);
                x.Location = new Point(_adminBar.Width - 28, 9);
            }
            var t = _adminBar.Controls["txt"] as Label;
            if (t != null) t.Text = "📢 " + (string.IsNullOrEmpty(nick) ? "" : nick + ": ") + text;
            _adminBar.Visible = true;
            _adminBar.BringToFront();
            try { _content.PerformLayout(); } catch { }
        }

        private void HideAdminBar()
        {
            if (_adminBar != null && _adminBar.Visible)
            {
                _adminBar.Visible = false;
                try { _content.PerformLayout(); } catch { }
            }
        }

        private static Color Darken(Color c)
        {
            return Color.FromArgb(Math.Max(0, (int)(c.R * 0.22)), Math.Max(0, (int)(c.G * 0.22)), Math.Max(0, (int)(c.B * 0.22)));
        }

        private void EventBannerPaint(object sender, PaintEventArgs e)
        {
            var p = sender as Panel; if (p == null) return;
            Color acc = p.Tag is Color ? (Color)p.Tag : Theme.Accent;
            if (_bannerImg != null)
            {
                try
                {
                    using (var bg = new SolidBrush(Darken(acc))) e.Graphics.FillRectangle(bg, 0, 0, p.Width, p.Height);
                    float sc = Math.Min((float)p.Width / _bannerImg.Width, (float)p.Height / _bannerImg.Height);
                    int dw = (int)(_bannerImg.Width * sc), dh = (int)(_bannerImg.Height * sc);
                    int dx = (p.Width - dw) / 2, dy = (p.Height - dh) / 2;
                    e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    e.Graphics.DrawImage(_bannerImg, dx, dy, dw, dh);
                    return;
                }
                catch { }
            }
            using (var b = new SolidBrush(acc)) e.Graphics.FillRectangle(b, 0, 0, 5, p.Height);
        }

        private void LoadBannerCover(string url)
        {
            new System.Threading.Thread(() =>
            {
                var bytes = NareLauncher.Core.NareEvents.DownloadImage(url);
                if (bytes == null) return;
                try
                {
                    var img = System.Drawing.Image.FromStream(new System.IO.MemoryStream(bytes));
                    try { if (_eventBanner.IsHandleCreated) _eventBanner.BeginInvoke((Action)(() => { _bannerImg = img; _eventBanner.Invalidate(); })); } catch { }
                }
                catch { }
            })
            { IsBackground = true }.Start();
        }

        private static void LoadEventImg(string url, PictureBox pb)
        {
            new System.Threading.Thread(() =>
            {
                var bytes = NareLauncher.Core.NareEvents.DownloadImage(url);
                if (bytes == null) return;
                try
                {
                    var img = System.Drawing.Image.FromStream(new System.IO.MemoryStream(bytes));
                    try { if (pb.IsHandleCreated) pb.BeginInvoke((Action)(() => { try { pb.Image = img; } catch { } })); } catch { }
                }
                catch { }
            })
            { IsBackground = true }.Start();
        }

        private static Color ParseHex(string hex, Color fallback)
        {
            try
            {
                if (string.IsNullOrEmpty(hex)) return fallback;
                hex = hex.TrimStart('#');
                if (hex.Length != 6) return fallback;
                return Color.FromArgb(Convert.ToInt32(hex.Substring(0, 2), 16), Convert.ToInt32(hex.Substring(2, 2), 16), Convert.ToInt32(hex.Substring(4, 2), 16));
            }
            catch { return fallback; }
        }

        public void Notify(string title, string body)
        {
            // Уведомления Windows (в систему, не внутри окна) — показываем всегда.
            if (_tray != null)
            {
                try { _tray.ShowBalloonTip(5000, title, body ?? "", ToolTipIcon.Info); } catch { }
            }
            try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
        }

        /// <summary>Пока игрок в «Чатах», новые сообщения не всплывают.</summary>
        public void MarkChatsSeen() { _chatSeen = -1; _adminChatSeen = -1; }

        /// <summary>
        /// Следит, чтобы был активный аккаунт. Когда ни одного нет, спрашивает
        /// ник коротким окном, а не гонит человека в список аккаунтов.
        /// Возвращает false, если окно просто закрыли.
        /// </summary>
        public bool EnsureAccount(string what)
        {
            if (AccountStore.Selected != null) return true;

            if (AccountStore.Accounts.Count == 0)
            {
                using (var welcome = new WelcomeForm(what))
                {
                    welcome.ShowDialog(this);
                    if (welcome.MadeAccount == null) return false;
                }

                RefreshAccountChip();
                RefreshPlayer();
                return true;
            }

            ManageAccounts();
            return AccountStore.Selected != null;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Settings.Save();
            AccountStore.Save();
            try { if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; } } catch { }
            base.OnFormClosing(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Theme.Border))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTCLIENT = 1;
            const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13,
                      HTTOPRIGHT = 14, HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);
                if ((int)m.Result == HTCLIENT)
                {
                    int lp = m.LParam.ToInt32();
                    var screen = new Point(unchecked((short)(lp & 0xFFFF)), unchecked((short)((lp >> 16) & 0xFFFF)));
                    var p = PointToClient(screen);

                    bool left = p.X <= ResizeBorder;
                    bool right = p.X >= Width - ResizeBorder;
                    bool top = p.Y <= ResizeBorder;
                    bool bottom = p.Y >= Height - ResizeBorder;

                    if (top && left) m.Result = (IntPtr)HTTOPLEFT;
                    else if (top && right) m.Result = (IntPtr)HTTOPRIGHT;
                    else if (bottom && left) m.Result = (IntPtr)HTBOTTOMLEFT;
                    else if (bottom && right) m.Result = (IntPtr)HTBOTTOMRIGHT;
                    else if (left) m.Result = (IntPtr)HTLEFT;
                    else if (right) m.Result = (IntPtr)HTRIGHT;
                    else if (top) m.Result = (IntPtr)HTTOP;
                    else if (bottom) m.Result = (IntPtr)HTBOTTOM;
                }
                return;
            }

            base.WndProc(ref m);
        }
    }

    // ====================================================================
    //  Окно управления аккаунтами: создание, переименование, удаление.
    //  Лежит здесь, а не в отдельном файле, чтобы не трогать .csproj.
    // ====================================================================

    /// <summary>
    /// Окно управления аккаунтами: создание, переименование, удаление,
    /// выбор активного. Открывается само, если аккаунтов ещё нет.
    /// </summary>
    public class AccountsForm : Form
    {
        private const int TitleHeight = 44;

        private readonly VirtualList _list = new VirtualList
        {
            ItemHeight = 58,
            EmptyText = "Пока нет ни одного аккаунта"
        };

        private readonly NareTextBox _nick = new NareTextBox
        {
            Placeholder = "Ник: 3–16 символов, латиница, цифры и _",
            Icon = Ico.Account
        };

        private readonly NareButton _add = new NareButton
        { Text = "Добавить", Style = BtnStyle.Accent, Icon = Ico.Plus, Size = new Size(166, 38) };

        private readonly NareButton _makeActive = new NareButton
        { Text = "Сделать активным", Style = BtnStyle.Success, Icon = Ico.Check, Size = new Size(196, 40) };

        private readonly NareButton _rename = new NareButton
        { Text = "Переименовать", Style = BtnStyle.Ghost, Size = new Size(160, 40) };

        private readonly NareButton _delete = new NareButton
        { Text = "Удалить", Style = BtnStyle.Danger, Icon = Ico.Trash, Size = new Size(138, 40) };

        private readonly NareButton _close = new NareButton
        { Text = "Готово", Style = BtnStyle.Ghost, Size = new Size(140, 38) };

        private readonly NareButton _closeX = new NareButton
        { Text = "", Style = BtnStyle.Ghost, Icon = Ico.Close, Size = new Size(40, 28), Radius = 8 };

        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);

        public AccountsForm()
        {
            Text = "Аккаунты";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(576, 506);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(9.5f);
            DoubleBuffered = true;
            KeyPreview = true;
            ShowInTaskbar = false;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            _list.Renderer = DrawAccount;
            _list.SelectionChanged += (s, e) =>
            {
                var acc = _list.SelectedItem as Account;
                if (acc != null) _nick.Text = acc.Name;
                UpdateButtons();
            };
            _list.ItemActivated += (s, e) => MakeActive();

            _add.Click += (s, e) => Add();
            _makeActive.Click += (s, e) => MakeActive();
            _rename.Click += (s, e) => Rename();
            _delete.Click += (s, e) => Delete();
            _close.Click += (s, e) => Close();
            _closeX.Click += (s, e) => Close();

            _nick.Inner.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                if (_list.SelectedItem != null && IsRenameOfSelected()) Rename();
                else Add();
            };
            _nick.Inner.TextChanged += (s, e) => UpdateButtons();

            Controls.Add(_list);
            Controls.Add(_nick);
            Controls.Add(_add);
            Controls.Add(_makeActive);
            Controls.Add(_rename);
            Controls.Add(_delete);
            Controls.Add(_close);
            Controls.Add(_closeX);
            Controls.Add(_status);

            Layout1();
            Reload();
        }

        // ------------------------------------------------------------ действия

        private string Nick { get { return (_nick.Text ?? "").Trim(); } }

        private bool IsRenameOfSelected()
        {
            var acc = _list.SelectedItem as Account;
            return acc != null && !string.Equals(acc.Name, Nick, StringComparison.Ordinal);
        }

        private void Add()
        {
            if (!AccountStore.IsValidNick(Nick))
            {
                Warn("Ник должен быть от 3 до 16 символов: латиница, цифры и _");
                return;
            }
            if (AccountStore.NickTaken(Nick))
            {
                Warn("Аккаунт с таким ником уже есть.");
                return;
            }

            AccountStore.AddOffline(Nick);
            _nick.Text = "";
            Info("Аккаунт добавлен и выбран активным.");
            Reload();
        }

        private void Rename()
        {
            int index = _list.SelectedIndex;
            var acc = _list.SelectedItem as Account;
            if (acc == null) { Warn("Сначала выберите аккаунт в списке."); return; }

            if (!AccountStore.IsValidNick(Nick))
            {
                Warn("Ник должен быть от 3 до 16 символов: латиница, цифры и _");
                return;
            }
            if (AccountStore.NickTaken(Nick, index))
            {
                Warn("Аккаунт с таким ником уже есть.");
                return;
            }
            if (string.Equals(acc.Name, Nick, StringComparison.Ordinal))
            {
                Warn("Ник не изменился.");
                return;
            }

            string old = acc.Name;
            AccountStore.Rename(index, Nick);
            Info("«" + old + "» теперь «" + Nick + "». UUID пересчитан под новый ник.");
            Reload();
            if (index < _list.Items.Count) _list.SelectedIndex = index;
        }

        private void Delete()
        {
            int index = _list.SelectedIndex;
            var acc = _list.SelectedItem as Account;
            if (acc == null) { Warn("Сначала выберите аккаунт в списке."); return; }

            if (MessageBox.Show(this, "Удалить аккаунт «" + acc.Name + "»?", "NareLauncher",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            AccountStore.Remove(index);
            _nick.Text = "";
            Info("Аккаунт «" + acc.Name + "» удалён.");
            Reload();
        }

        private void MakeActive()
        {
            int index = _list.SelectedIndex;
            if (index < 0 || index >= AccountStore.Accounts.Count)
            {
                Warn("Сначала выберите аккаунт в списке.");
                return;
            }

            AccountStore.SelectedIndex = index;
            AccountStore.Save();
            Info("Активный аккаунт: " + AccountStore.Accounts[index].Name);
            _list.Invalidate();
            UpdateButtons();
        }

        private void Reload()
        {
            var items = new List<object>();
            foreach (var a in AccountStore.Accounts) items.Add(a);
            _list.SetItems(items);

            if (AccountStore.SelectedIndex >= 0 && AccountStore.SelectedIndex < items.Count)
                _list.SelectedIndex = AccountStore.SelectedIndex;

            UpdateButtons();
            if (AccountStore.Accounts.Count == 0)
                Warn("Введите ник и нажмите «Добавить» — без аккаунта игра не запустится.");
        }

        private void UpdateButtons()
        {
            bool has = _list.SelectedItem != null;
            _makeActive.Enabled = has;
            _delete.Enabled = has;
            _rename.Enabled = has && IsRenameOfSelected();
            _add.Enabled = Nick.Length > 0;

            _makeActive.Invalidate();
            _delete.Invalidate();
            _rename.Invalidate();
            _add.Invalidate();
        }

        private void Warn(string text) { _status.Text = text; _status.ForeColor = Theme.Warn; }
        private void Info(string text) { _status.Text = text; _status.ForeColor = Theme.Muted; }

        // ------------------------------------------------------------ отрисовка

        private void DrawAccount(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var acc = item as Account;
            if (acc == null) return;

            var box = Rectangle.Inflate(r, -4, -3);
            if (box.Width <= 0 || box.Height <= 0) return;

            bool active = index == AccountStore.SelectedIndex;

            if (selected) Theme.FillRound(g, box, 10, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 10, Color.FromArgb(16, 255, 255, 255));
            if (active) Theme.DrawRound(g, box, 10, Color.FromArgb(150, Theme.Good), 1.6f);

            var av = new Rectangle(box.X + 14, box.Y + 10, 34, 34);
            Theme.FillRoundGradient(g, av, 8, Theme.Accent, Theme.Accent2, 30f);
            Theme.Text1(g, acc.Name.Substring(0, 1).ToUpperInvariant(), Theme.F(12f, FontStyle.Bold),
                Color.White, av, StringAlignment.Center, StringAlignment.Center);

            Theme.Text1(g, acc.Name, Theme.F(10.5f, FontStyle.Bold), Theme.Text,
                new Rectangle(av.Right + 14, box.Y + 9, box.Width - 210, 20));
            Theme.Text1(g, (acc.IsOffline ? "Оффлайн" : "Microsoft") + " · UUID " +
                           (acc.Uuid.Length > 8 ? acc.Uuid.Substring(0, 8) : acc.Uuid),
                Theme.F(8.6f), Theme.Muted,
                new Rectangle(av.Right + 14, box.Y + 30, box.Width - 210, 18));

            if (active)
            {
                var badge = new Rectangle(box.Right - 104, box.Y + (box.Height - 22) / 2, 90, 22);
                Theme.FillRound(g, badge, 11, Color.FromArgb(45, Theme.Good));
                Theme.Text1(g, "АКТИВНЫЙ", Theme.F(7.8f, FontStyle.Bold), Theme.Good, badge,
                    StringAlignment.Center, StringAlignment.Center);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.Smooth(g);

            var bar = new Rectangle(0, 0, Width, TitleHeight);
            using (var b = new SolidBrush(Theme.Sidebar))
                g.FillRectangle(b, bar);
            using (var pen = new Pen(Color.FromArgb(28, 255, 255, 255)))
                g.DrawLine(pen, 0, TitleHeight - 1, Width, TitleHeight - 1);

            var logo = new Rectangle(18, (TitleHeight - 20) / 2, 20, 20);
            Theme.FillRoundGradient(g, logo, 6, Theme.Accent, Theme.Accent2, 35f);
            Theme.Text1(g, "N", Theme.F(10f, FontStyle.Bold), Color.White, logo,
                StringAlignment.Center, StringAlignment.Center);

            Theme.Text1(g, "Аккаунты", Theme.F(11f, FontStyle.Bold), Theme.Text,
                new Rectangle(48, 0, 300, TitleHeight));

            Theme.Text1(g, "Ник сохраняется вместе с UUID — прогресс в мирах не потеряется",
                Theme.F(9f), Theme.Dim, new Rectangle(24, TitleHeight + 8, Width - 48, 20));

            using (var pen = new Pen(Theme.Border))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && e.Y < TitleHeight)
            {
                NativeMethods.ReleaseCapture();
                NativeMethods.SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Close();
        }

        private void Layout1()
        {
            int pad = 24;
            int w = ClientSize.Width - pad * 2;

            _closeX.Location = new Point(ClientSize.Width - 52, (TitleHeight - _closeX.Height) / 2);

            int y = TitleHeight + 36;
            _nick.SetBounds(pad, y, w - _add.Width - 12, 38);
            _add.Location = new Point(pad + w - _add.Width, y);

            int listY = y + 52;
            _list.SetBounds(pad, listY, w, 236);

            int by = listY + 252;
            _makeActive.Location = new Point(pad, by);
            _rename.Location = new Point(pad + 206, by);
            _delete.Location = new Point(pad + 376, by);

            _status.SetBounds(pad, by + 48, w - 164, 34);
            _close.Location = new Point(ClientSize.Width - pad - _close.Width, ClientSize.Height - 48);
        }
    }

    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
