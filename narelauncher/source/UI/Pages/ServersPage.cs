using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI.Pages
{
    /// <summary>
    /// Список серверов — тот же самый, что видит игра. Лаунчер читает и пишет
    /// servers.dat сборки, опрашивает серверы и умеет сразу зайти на выбранный.
    /// </summary>
    public class ServersPage : PageBase
    {
        private readonly VirtualList _list = new VirtualList { ItemHeight = 64, EmptyText = "Серверов пока нет" };

        private readonly NareButton _play = new NareButton { Text = "Зайти", Style = BtnStyle.Accent, Icon = Ico.Play, Size = new Size(150, 42) };
        private readonly NareButton _ping = new NareButton { Text = "Проверить все", Style = BtnStyle.Ghost, Icon = Ico.Refresh, Size = new Size(185, 42) };
        private readonly NareButton _add = new NareButton { Text = "Добавить", Style = BtnStyle.Ghost, Icon = Ico.Plus, Size = new Size(150, 42) };
        private readonly NareButton _edit = new NareButton { Text = "Изменить", Style = BtnStyle.Ghost, Icon = Ico.Settings, Size = new Size(150, 42) };
        private readonly NareButton _remove = new NareButton { Text = "Удалить", Style = BtnStyle.Danger, Icon = Ico.Trash, Size = new Size(140, 42) };

        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);

        private readonly List<ServerEntry> _servers = new List<ServerEntry>();
        private readonly Dictionary<string, Image> _icons = new Dictionary<string, Image>();
        private bool _busy;

        public ServersPage()
        {
            Padding = new Padding(0);

            _list.Renderer = DrawItem;
            _list.SelectionChanged += (s, e) => UpdateButtons();
            _list.ItemActivated += (s, e) => Join();

            _play.Click += (s, e) => Join();
            _ping.Click += async (s, e) => await PingAllAsync();
            _add.Click += (s, e) => Edit(null);
            _edit.Click += (s, e) => Edit(Selected());
            _remove.Click += (s, e) => Remove();

            Controls.Add(_list);
            Controls.Add(_play);
            Controls.Add(_ping);
            Controls.Add(_add);
            Controls.Add(_edit);
            Controls.Add(_remove);
            Controls.Add(_status);
        }

        public override void PageShown()
        {
            Reload();
        }

        private ServerEntry Selected() { return _list.SelectedItem as ServerEntry; }

        private static string Version { get { return Settings.Current.SelectedVersion; } }

        // ---------------------------------------------------------------- данные

        private void Reload()
        {
            DropIcons();
            _servers.Clear();
            _servers.AddRange(ServerList.Load(Version));

            Show1();

            if (_servers.Count == 0)
                Say("Список пуст. «Добавить» положит сервер прямо в файл сборки — игра его увидит.", Theme.Muted);
            else
                Say(_servers.Count + " " + Word(_servers.Count) + " в сборке " + Where(), Theme.Muted);
        }

        private void Show1()
        {
            var items = new List<object>();
            foreach (var s in _servers) items.Add(s);

            int keep = _list.SelectedIndex;
            _list.SetItems(items);
            if (keep >= 0 && keep < items.Count) _list.SelectedIndex = keep;

            UpdateButtons();
            Invalidate();
        }

        private static string Where()
        {
            string id = Version;
            return string.IsNullOrEmpty(id) ? "(не выбрана)" : id;
        }

        private static string Word(int n)
        {
            int last = n % 10, two = n % 100;
            if (two >= 11 && two <= 14) return "серверов";
            if (last == 1) return "сервер";
            if (last >= 2 && last <= 4) return "сервера";
            return "серверов";
        }

        // ---------------------------------------------------------------- опрос

        private async Task PingAllAsync()
        {
            if (_busy || _servers.Count == 0) return;

            _busy = true;
            _ping.Enabled = false;
            Say("Опрашиваю серверы…", Theme.Muted);

            var jobs = new List<Task>();
            foreach (var server in _servers)
            {
                var one = server;
                jobs.Add(Task.Run(async () =>
                {
                    await ServerList.QueryAsync(one);
                    try { BeginInvoke((Action)(() => { AdoptIcon(one); _list.Invalidate(); })); } catch { }
                }));
            }

            try { await Task.WhenAll(jobs); }
            catch { }

            int alive = 0, people = 0;
            foreach (var s in _servers)
                if (s.Online >= 0) { alive++; people += s.Online; }

            _busy = false;
            _ping.Enabled = true;

            Say(alive + " из " + _servers.Count + " отвечают · игроков онлайн: " + people,
                alive > 0 ? Theme.Good : Theme.Warn);

            _list.Invalidate();
        }

        private void AdoptIcon(ServerEntry server)
        {
            if (string.IsNullOrEmpty(server.IconBase64)) return;
            if (_icons.ContainsKey(server.Address)) return;

            try
            {
                byte[] raw = Convert.FromBase64String(server.IconBase64);
                using (var stream = new MemoryStream(raw))
                    _icons[server.Address] = Image.FromStream(stream);
            }
            catch { }
        }

        private void DropIcons()
        {
            foreach (var image in _icons.Values)
                try { image.Dispose(); } catch { }
            _icons.Clear();
        }

        // ---------------------------------------------------------------- действия

        private void Join()
        {
            var server = Selected();
            if (server == null) { Say("Выберите сервер в списке.", Theme.Warn); return; }
            if (string.IsNullOrWhiteSpace(server.Address)) { Say("У сервера нет адреса.", Theme.Bad); return; }

            Say("Запускаю игру с заходом на " + server.Address + "…", Theme.Muted);
            Host.Home.LaunchOn(server.Address);
            Host.Navigate(0);
        }

        private void Edit(ServerEntry server)
        {
            bool fresh = server == null;
            using (var dialog = new ServerEditForm(fresh ? new ServerEntry() : server))
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;

                if (fresh) _servers.Add(dialog.Result);
                else
                {
                    server.Name = dialog.Result.Name;
                    server.Address = dialog.Result.Address;
                    server.Checked = false;
                    server.Online = server.Max = server.Ping = -1;
                }

                Store();
                Show1();
                Say(fresh ? "Сервер добавлен." : "Сервер изменён.", Theme.Good);
            }
        }

        private void Remove()
        {
            var server = Selected();
            if (server == null) return;
            if (!Host.Confirm("Убрать «" + server.Name + "» из списка серверов?")) return;

            _servers.Remove(server);
            Store();
            Show1();
            Say("Сервер убран.", Theme.Good);
        }

        private void Store()
        {
            try
            {
                ServerList.Save(Version, _servers);
            }
            catch (Exception ex)
            {
                Host.ShowError("Не удалось сохранить список серверов", ex);
            }
        }

        // ---------------------------------------------------------------- отрисовка

        private void DrawItem(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var server = item as ServerEntry;
            if (server == null) return;

            var box = Rectangle.Inflate(r, -4, -3);
            if (box.Width <= 0 || box.Height <= 0) return;

            if (selected) Theme.FillRound(g, box, 10, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 10, Color.FromArgb(16, 255, 255, 255));
            if (selected) Theme.DrawRound(g, box, 10, Color.FromArgb(120, Theme.Accent));

            // значок сервера
            var icon = new Rectangle(box.X + 10, box.Y + (box.Height - 38) / 2, 38, 38);
            Image picture;
            if (_icons.TryGetValue(server.Address ?? "", out picture) && picture != null)
            {
                g.DrawImage(picture, icon);
            }
            else
            {
                Theme.FillRound(g, icon, 8, Color.FromArgb(40, 255, 255, 255));
                string letter = string.IsNullOrEmpty(server.Name) ? "?" : server.Name.Substring(0, 1).ToUpperInvariant();
                Theme.Text1(g, letter, Theme.F(14f, FontStyle.Bold), Theme.Dim, icon,
                    StringAlignment.Center, StringAlignment.Center);
            }

            int textX = icon.Right + 12;
            int right = box.Right - 12;

            // справа: игроки и задержка
            string players = server.Online >= 0 ? server.Online + " / " + server.Max : "";
            if (players.Length > 0)
            {
                Theme.Text1(g, players, Theme.F(10f, FontStyle.Bold), Theme.Text,
                    new Rectangle(right - 110, box.Y + 9, 110, 18),
                    StringAlignment.Far, StringAlignment.Center);

                Theme.Text1(g, server.Ping + " мс", Theme.F(8.8f), PingColor(server.Ping),
                    new Rectangle(right - 110, box.Y + 28, 110, 16),
                    StringAlignment.Far, StringAlignment.Center);
            }
            else if (server.Checked)
            {
                Theme.Text1(g, server.Error.Length > 0 ? server.Error : "не отвечает",
                    Theme.F(8.8f), Theme.Bad,
                    new Rectangle(right - 160, box.Y + 18, 160, 18),
                    StringAlignment.Far, StringAlignment.Center);
            }

            int textW = Math.Max(60, (right - 130) - textX);

            Theme.Text1(g, server.Name, Theme.F(10.5f, FontStyle.Bold), Theme.Text,
                new Rectangle(textX, box.Y + 8, textW, 20));

            Theme.Text1(g, server.Address, Theme.F(8.8f), Theme.Dim,
                new Rectangle(textX, box.Y + 27, textW, 16));

            string line = server.Motd;
            if (line.Length == 0 && server.VersionName.Length > 0) line = server.VersionName;
            if (line.Length > 0)
                Theme.Text1(g, line, Theme.F(8.8f), Theme.Muted,
                    new Rectangle(textX, box.Y + 43, textW, 16));
        }

        private static Color PingColor(int ping)
        {
            if (ping < 0) return Theme.Dim;
            if (ping < 90) return Theme.Good;
            if (ping < 220) return Theme.Warn;
            return Theme.Bad;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.Smooth(g);

            Theme.Text1(g, "Серверы", Theme.F(17f, FontStyle.Bold), Theme.Text,
                new Rectangle(28, 16, 400, 28));

            Theme.Text1(g, "Тот же список, что и в игре · сборка " + Where(),
                Theme.F(9.5f), Theme.Muted, new Rectangle(28, 40, 900, 20));
        }

        // ---------------------------------------------------------------- вёрстка

        private void UpdateButtons()
        {
            bool any = Selected() != null;
            _play.Enabled = any;
            _edit.Enabled = any;
            _remove.Enabled = any;
            _ping.Enabled = !_busy && _servers.Count > 0;
            Layout1();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Layout1();
        }

        private void Layout1()
        {
            int pad = 28;
            int listTop = 78;
            int bottom = 56;

            _list.SetBounds(pad, listTop, Math.Max(200, Width - pad * 2),
                Math.Max(60, Height - listTop - bottom - 18));

            int by = Height - bottom - 2;
            int bx = pad;

            foreach (var button in new[] { _play, _ping, _add, _edit, _remove })
            {
                button.Location = new Point(bx, by);
                bx += button.Width + 8;
            }

            _status.SetBounds(pad, Height - 22, Math.Max(100, Width - pad * 2), 18);
            Invalidate();
        }

        private void Say(string text, Color color)
        {
            _status.Text = text ?? "";
            _status.ForeColor = color;
        }
    }

    /// <summary>Окошко «название + адрес» для добавления и правки сервера.</summary>
    public class ServerEditForm : Form
    {
        public ServerEntry Result = new ServerEntry();

        private readonly NareTextBox _name = new NareTextBox { Placeholder = "Название" };
        private readonly NareTextBox _address = new NareTextBox { Placeholder = "адрес или адрес:порт" };

        public ServerEditForm(ServerEntry source)
        {
            Text = "Сервер";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(10f);
            ClientSize = new Size(440, 230);

            _name.Text = source.Name;
            _address.Text = source.Address;

            Controls.Add(new NareLabel("Название", 9f, FontStyle.Regular, Theme.Muted) { Bounds = new Rectangle(24, 20, 200, 18) });
            _name.SetBounds(24, 42, 392, 38);
            Controls.Add(_name);

            Controls.Add(new NareLabel("Адрес", 9f, FontStyle.Regular, Theme.Muted) { Bounds = new Rectangle(24, 92, 200, 18) });
            _address.SetBounds(24, 114, 392, 38);
            Controls.Add(_address);

            var ok = new NareButton { Text = "Сохранить", Style = BtnStyle.Accent, Size = new Size(150, 40) };
            var cancel = new NareButton { Text = "Отмена", Style = BtnStyle.Ghost, Size = new Size(130, 40) };

            ok.Location = new Point(266, 172);
            cancel.Location = new Point(128, 172);

            ok.Click += (s, e) =>
            {
                string address = (_address.Text ?? "").Trim();
                if (address.Length == 0) { _address.Inner.Focus(); return; }

                Result = new ServerEntry
                {
                    Name = string.IsNullOrWhiteSpace(_name.Text) ? address : _name.Text.Trim(),
                    Address = address,
                    IconBase64 = source.IconBase64
                };

                DialogResult = DialogResult.OK;
                Close();
            };

            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(ok);
            Controls.Add(cancel);

            AcceptButton = null;
            Shown += (s, e) => _name.Inner.Focus();
        }
    }
}
