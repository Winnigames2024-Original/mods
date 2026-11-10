using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI.Pages
{
    /// <summary>
    /// Публичные (рекламные) серверы. Их выкладывают игроки, одобряет
    /// администратор. За заход через «Играть» начисляются монеты.
    /// </summary>
    public class AdServersPage : PageBase
    {
        private readonly VirtualList _list = new VirtualList { ItemHeight = 76, EmptyText = "Пока нет одобренных серверов" };

        private readonly NareButton _play = new NareButton
        { Text = "Играть (+монеты)", Style = BtnStyle.Accent, Icon = Ico.Play, Size = new Size(200, 42) };
        private readonly NareButton _refresh = new NareButton
        { Text = "Обновить", Style = BtnStyle.Ghost, Size = new Size(130, 42) };
        private readonly NareButton _submit = new NareButton
        { Text = "Добавить свой сервер", Style = BtnStyle.Ghost, Size = new Size(230, 42) };
        private readonly NareButton _mine = new NareButton
        { Text = "Мои заявки", Style = BtnStyle.Ghost, Size = new Size(150, 42) };

        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);
        private readonly NareLabel _coins = new NareLabel("", 11f, FontStyle.Bold, Theme.Warn);

        private readonly List<NareAuth.AdServer> _servers = new List<NareAuth.AdServer>();

        private NareAuth.AdServer Current { get { return _list.SelectedItem as NareAuth.AdServer; } }
        private string Token { get { var a = AccountStore.Selected; return a == null ? "" : a.Token; } }

        public AdServersPage()
        {
            _list.Renderer = Draw;
            _list.ItemActivated += (s, e) => Play();
            Controls.Add(_list);

            _play.Click += (s, e) => Play();
            _refresh.Click += async (s, e) => await Load();
            _submit.Click += (s, e) => Submit();
            _mine.Click += async (s, e) => await ShowMine();

            Controls.Add(_play); Controls.Add(_refresh); Controls.Add(_submit); Controls.Add(_mine);
            Controls.Add(_status); Controls.Add(_coins);
            Layout1();
        }

        public override async void PageShown()
        {
            Layout1();
            _coins.Text = "Монеты: " + NareAuth.Last.Coins;
            await Load();
        }

        private async System.Threading.Tasks.Task Load()
        {
            _status.Text = "Загружаю список…";
            try
            {
                var list = await NareAuth.AdServersAsync(CancellationToken.None);
                _servers.Clear();
                _servers.AddRange(list);

                var items = new List<object>();
                foreach (var s in _servers) items.Add(s);
                _list.SetItems(items);
                if (_servers.Count > 0 && _list.SelectedIndex < 0) _list.SelectedIndex = 0;

                _status.Text = _servers.Count == 0 ? "Серверов пока нет." : "Серверов: " + _servers.Count;
                _coins.Text = "Монеты: " + NareAuth.Last.Coins;
            }
            catch (Exception ex)
            {
                _status.Text = "Не загрузилось: " + ex.Message;
            }
        }

        private async void Play()
        {
            var server = Current;
            if (server == null) return;

            if (string.IsNullOrEmpty(Token) && !Host.EnsureAuth()) return;

            // сначала заход на сервере: начисляет монеты и отдаёт реальный адрес
            // (для серверов со скрытым IP адреса нет в списке — только отсюда)
            string ip = server.Ip;
            try
            {
                var earn = await NareAuth.EarnCoinsAsync(Token, server.Id, CancellationToken.None);
                if (!string.IsNullOrEmpty(earn.Ip)) ip = earn.Ip;
                _coins.Text = "Монеты: " + earn.Coins;
                _status.Text = earn.Awarded > 0
                    ? "Начислено " + earn.Awarded + " монет за заход!"
                    : "Монеты за этот сервер снова через " + earn.CooldownLeft + " мин.";
            }
            catch (Exception ex) { Log.Warn("Монеты не начислились: " + ex.Message); }

            if (string.IsNullOrEmpty(ip))
            {
                _status.Text = "Не удалось получить адрес сервера. Попробуйте ещё раз.";
                _status.ForeColor = Theme.Bad;
                return;
            }

            // версия сервера — это версия СЕРВЕРА, не клиента: "server"/"vanilla"/пусто
            // не годятся как версия для запуска игры — тогда играем на выбранной версии
            string sv = server.Version ?? "";
            if (sv == "server" || sv == "vanilla") sv = "";
            var request = new PlayLink.Request
            {
                Version = sv,
                Loader = string.IsNullOrEmpty(server.Loader) ? "vanilla" : server.Loader,
                Server = ip
            };

            if (request.HasVersion) Host.Home.PlayFromLink(request);
            else Host.Home.LaunchOn(ip);
            Host.Navigate(0);
        }

        private async void Submit()
        {
            if (string.IsNullOrEmpty(Token) && !Host.EnsureAuth()) return;

            using (var form = new AdSubmitForm())
            {
                if (form.ShowDialog(FindForm()) != DialogResult.OK) return;

                try
                {
                    await NareAuth.SubmitServerAsync(Token, form.Result, CancellationToken.None);
                    _status.Text = "Заявка отправлена на модерацию. Появится после одобрения.";
                    _status.ForeColor = Theme.Good;
                }
                catch (Exception ex)
                {
                    _status.Text = "Не отправилось: " + ex.Message;
                    _status.ForeColor = Theme.Bad;
                }
            }
        }

        private async System.Threading.Tasks.Task ShowMine()
        {
            if (string.IsNullOrEmpty(Token) && !Host.EnsureAuth()) return;

            try
            {
                var mine = await NareAuth.MyServersAsync(Token, CancellationToken.None);
                if (mine.Count == 0) { _status.Text = "У вас нет заявок."; return; }

                var text = new System.Text.StringBuilder("Ваши серверы:\n");
                foreach (var s in mine)
                {
                    string st = s.Status == "approved" ? "одобрен"
                              : s.Status == "rejected" ? "отклонён" + (s.Reason.Length > 0 ? " (" + s.Reason + ")" : "")
                              : "на модерации";
                    text.Append("\n• " + s.Name + " — " + st);
                }
                MessageBox.Show(FindForm(), text.ToString(), "Мои заявки",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { _status.Text = "Не вышло: " + ex.Message; }
        }

        private void Draw(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var s = item as NareAuth.AdServer;
            if (s == null) return;

            var box = Rectangle.Inflate(r, -4, -3);
            if (box.Width <= 0 || box.Height <= 0) return;

            if (selected) Theme.FillRound(g, box, 10, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 10, Color.FromArgb(16, 255, 255, 255));

            Theme.Text1(g, s.Name, Theme.F(12f, FontStyle.Bold), Theme.Text,
                new Rectangle(box.X + 14, box.Y + 8, box.Width - 160, 22));

            string sub = s.HideIp ? "🔒 IP скрыт" : s.Ip;
            if (!string.IsNullOrEmpty(s.Version)) sub += "  ·  " + s.Version;
            if (!string.IsNullOrEmpty(s.Loader)) sub += " " + s.Loader;
            Theme.Text1(g, sub, Theme.F(9f), Theme.Muted,
                new Rectangle(box.X + 14, box.Y + 32, box.Width - 160, 18));

            if (!string.IsNullOrEmpty(s.Desc))
                Theme.Text1(g, s.Desc, Theme.F(8.6f), Theme.Dim,
                    new Rectangle(box.X + 14, box.Y + 52, box.Width - 160, 18));

            var coin = new Rectangle(box.Right - 120, box.Y + box.Height / 2 - 14, 104, 28);
            Theme.FillRound(g, coin, 14, Color.FromArgb(45, Theme.Warn));
            Theme.Text1(g, "+" + s.Reward + " монет", Theme.F(8.6f, FontStyle.Bold), Theme.Warn, coin,
                StringAlignment.Center, StringAlignment.Center);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Layout1(); }

        private void Layout1()
        {
            if (_list == null) return;
            int pad = 28, top = 80, bottom = 72;
            _list.SetBounds(pad, top, Math.Max(100, Width - pad * 2), Math.Max(60, Height - top - bottom));

            int by = Height - bottom + 14;
            _play.Location = new Point(pad, by);
            _refresh.Location = new Point(pad + 214, by);
            _submit.Location = new Point(pad + 358, by);
            _mine.Location = new Point(pad + 602, by);
            _status.SetBounds(pad, by + 46, Math.Max(50, Width - pad * 2 - 200), 20);
            _coins.SetBounds(Width - pad - 200, 30, 200, 24);
            _coins.TextAlign = ContentAlignment.MiddleRight;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.Smooth(g);
            Theme.Text1(g, "Публичные сервера", Theme.F(17f, FontStyle.Bold), Theme.Text,
                new Rectangle(28, 24, 500, 28));
            Theme.Text1(g, "Заходи через «Играть» — за это начисляются монеты",
                Theme.F(9.5f), Theme.Muted, new Rectangle(28, 50, 600, 20));
        }
    }

    /// <summary>Форма заявки на добавление своего сервера в рекламу.</summary>
    public class AdSubmitForm : Form
    {
        private readonly NareTextBox _name = new NareTextBox { Placeholder = "Название сервера" };
        private readonly NareTextBox _ip = new NareTextBox { Placeholder = "Адрес: ip или ip:порт" };
        private readonly NareTextBox _version = new NareTextBox { Placeholder = "Версия (например 1.21.4), необязательно" };
        private readonly NareTextBox _desc = new NareTextBox { Placeholder = "Короткое описание" };
        private readonly NareButton _ok = new NareButton { Text = "Отправить на модерацию", Style = BtnStyle.Accent, Size = new Size(240, 40) };
        private readonly NareButton _cancel = new NareButton { Text = "Отмена", Style = BtnStyle.Ghost, Size = new Size(120, 40) };
        private readonly NareButton _hideIp = new NareButton { Text = "☐  Скрыть IP (адрес не виден в списке)", Style = BtnStyle.Ghost, Size = new Size(440, 38) };
        private bool _hide;

        public NareAuth.AdServer Result = new NareAuth.AdServer();

        public AdSubmitForm()
        {
            Text = "Добавить сервер в рекламу";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(480, 404);
            BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.F(10f);

            void Cap(string text, int y)
            {
                var l = new NareLabel(text, 8f, FontStyle.Regular, Theme.Dim)
                { Bounds = new Rectangle(20, y, 440, 14) };
                Controls.Add(l);
            }

            Cap("Название сервера", 16);
            _name.SetBounds(20, 32, 440, 38);
            Cap("Адрес: ip или ip:порт", 78);
            _ip.SetBounds(20, 94, 440, 38);
            Cap("Версия игры (необязательно)", 140);
            _version.SetBounds(20, 156, 440, 38);
            Cap("Короткое описание", 202);
            _desc.SetBounds(20, 218, 440, 38);
            Cap("Приватность", 264);
            _hideIp.Location = new Point(20, 280);
            _ok.Location = new Point(20, 340);
            _cancel.Location = new Point(340, 340);

            _hideIp.Click += (s, e) =>
            {
                _hide = !_hide;
                _hideIp.Text = (_hide ? "☑" : "☐") + "  Скрыть IP (адрес не виден в списке)";
                _hideIp.Style = _hide ? BtnStyle.Accent : BtnStyle.Ghost;
                _hideIp.Invalidate();
            };

            _ok.Click += (s, e) =>
            {
                string name = _name.Text.Trim(), ip = _ip.Text.Trim();
                if (name.Length == 0 || ip.Length == 0) return;
                Result = new NareAuth.AdServer
                {
                    Name = name, Ip = ip,
                    Version = _version.Text.Trim(),
                    Desc = _desc.Text.Trim(),
                    HideIp = _hide
                };
                DialogResult = DialogResult.OK; Close();
            };
            _cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(_name); Controls.Add(_ip); Controls.Add(_version); Controls.Add(_desc);
            Controls.Add(_hideIp); Controls.Add(_ok); Controls.Add(_cancel);
        }
    }
}
