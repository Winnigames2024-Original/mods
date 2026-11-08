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
        public SkinPage SkinTab;
        public CosmeticsPage CosmeticsTab;
        public AccountsPage AccountsTab;
        public SettingsPage SettingsTab;
        public ConsolePage ConsoleTab;
        public TogetherPage TogetherTab;
        public AdminPage AdminTab;
        public AdServersPage AdTab;
        public ChatsPage ChatsTab;

        private NareLabel _accountChip;

        private BanOverlay _banOverlay;
        private ToastHost _toasts;
        private long _chatSeen = -1;       // игрок: время последнего увиденного ответа админа
        private long _adminChatSeen = -1;  // админ: время последнего увиденного сообщения игрока
        private readonly System.Windows.Forms.Timer _guard = new System.Windows.Forms.Timer { Interval = 15000 };
        private int _warnShown;          // сколько предупреждений уже показали за сессию
        private long _broadcastShown;    // время последнего показанного объявления
        private bool _updatePrompting;   // чтобы окно обновления не всплывало дважды
        private long _updateHandledAt = -1; // время последнего отработанного экстренного обновления
        private bool _loggingOut;        // идёт выход после отвязки устройства
        private string _guardNick = "";  // чей статус мы отслеживаем

        public MainForm()
        {
            Text = "NareLauncher";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1000, 640);
            Size = new Size(1180, 720);
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
            AdTab = new AdServersPage { Host = this };
            AdminTab = new AdminPage { Host = this };
            ChatsTab = new ChatsPage { Host = this };
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
            _pages.Add(SkinTab);
            _pages.Add(CosmeticsTab);
            _pages.Add(AccountsTab);
            _pages.Add(SettingsTab);
            _pages.Add(ConsoleTab);
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
            string[] titles = { "Играть", "Версии", "Моды и паки", "Сборка", "Серверы", "Вместе", "Скин", "Вещи", "Аккаунты", "Настройки", "Консоль", "Публичные", "Чаты", "Админ" };
            Ico[] icons = { Ico.Play, Ico.Versions, Ico.Mods, Ico.Folder, Ico.Arrow, Ico.Account, Ico.Skin, Ico.Skin, Ico.Account, Ico.Settings, Ico.Console, Ico.Arrow, Ico.Account, Ico.Settings };

            int y = 18;
            for (int i = 0; i < titles.Length; i++)
            {
                int index = i;
                var btn = new NavButton
                {
                    Text = titles[i],
                    Icon = icons[i],
                    Bounds = new Rectangle(8, y, SidebarWidth - 16, 44)
                };
                btn.Click += (s, e) => Navigate(index);
                _sidebar.Controls.Add(btn);
                _nav.Add(btn);
                y += 48;
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
            bool admin = acc != null && acc.Admin;

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

            // Первый запуск: без аккаунта играть нельзя — сразу открываем окно
            if (AccountStore.Accounts.Count == 0)
                ManageAccounts();

            // у активного аккаунта нет пароля на сервере — попросим сразу
            var active = AccountStore.Selected;
            if (active != null && string.IsNullOrEmpty(active.Token)) EnsureAuth();

            _guard.Tick += delegate { CheckPolicy(); };
            _guard.Start();
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
                _warnShown = 0;
                _broadcastShown = 0;
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

            // сервер может назначить игрока админом — подхватываем
            if (acc != null && acc.Admin != policy.IsAdmin)
            {
                acc.Admin = policy.IsAdmin;
                AccountStore.Save();
                RefreshAdminNav();
            }

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
            for (int i = _warnShown; i < policy.Warnings.Count; i++)
            {
                MessageBox.Show(this, policy.Warnings[i],
                    "Предупреждение от администратора", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            _warnShown = policy.Warnings.Count;

            // объявление для всех
            if (policy.HasBroadcast && policy.BroadcastAt > _broadcastShown)
            {
                _broadcastShown = policy.BroadcastAt;
                MessageBox.Show(this, policy.Broadcast,
                    "Объявление", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                    var chats = await NareAuth.AdminChatsAsync(acc.Token, System.Threading.CancellationToken.None);
                    long maxAt = 0; NareAuth.ChatSummary newest = null;
                    foreach (var c in chats)
                        if (!c.LastAdmin && c.At > maxAt) { maxAt = c.At; newest = c; }

                    if (_adminChatSeen < 0) _adminChatSeen = maxAt;      // базовая отметка — без уведомления
                    else if (maxAt > _adminChatSeen && newest != null)
                    {
                        _adminChatSeen = maxAt;
                        var who = newest.Nick;
                        _toasts.Push(who + " написал", newest.Last, () => OpenChats());
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
                        _toasts.Push("Ответ администратора", newest.Text, () => OpenChats());
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
