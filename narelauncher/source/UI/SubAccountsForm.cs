using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Подаккаунты — игровые ники под одним аккаунтом NareLauncher. Логин не
    /// меняется, а под ним можно держать сколько угодно ников. Переключение
    /// бесплатно, новый ник стоит монеты. Чужой ник занять нельзя — это
    /// проверяет сервер.
    /// </summary>
    public class SubAccountsForm : Form
    {
        private readonly Account _account;

        private readonly VirtualList _list = new VirtualList { ItemHeight = 44, EmptyText = "Загружаю…" };
        private readonly NareButton _use = new NareButton { Text = "Играть под этим", Style = BtnStyle.Success, Size = new Size(190, 40) };
        private readonly NareButton _add = new NareButton { Text = "Добавить ник (1000 монет)", Style = BtnStyle.Accent, Size = new Size(240, 40) };
        private readonly NareButton _close = new NareButton { Text = "Закрыть", Style = BtnStyle.Ghost, Size = new Size(120, 40) };
        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);

        private NareAuth.Subs _subs = new NareAuth.Subs();

        public SubAccountsForm(Account account)
        {
            _account = account;

            Text = "Ники (подаккаунты)";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(480, 420);
            BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.F(10f);

            _list.Renderer = Draw;
            _list.SetBounds(20, 54, 440, 268);
            _use.Location = new Point(20, 332);
            _add.Location = new Point(220, 332);
            _close.Location = new Point(348, 376);
            _status.SetBounds(20, 384, 320, 20);

            _use.Click += async (s, e) => await Use();
            _add.Click += async (s, e) => await Add();
            _close.Click += (s, e) => Close();

            Controls.Add(_list); Controls.Add(_use); Controls.Add(_add); Controls.Add(_close); Controls.Add(_status);

            Load += async delegate { await Reload(); };
        }

        private string Token { get { return _account.Token; } }

        private async Task Reload()
        {
            try
            {
                _subs = await NareAuth.SubListAsync(Token, CancellationToken.None);
                var items = new List<object>();
                foreach (var n in _subs.List) items.Add(n);
                _list.SetItems(items);

                for (int i = 0; i < _subs.List.Count; i++)
                    if (string.Equals(_subs.List[i], _subs.Active, StringComparison.OrdinalIgnoreCase))
                        _list.SelectedIndex = i;

                _status.Text = "Монеты: " + _subs.Coins + "   ·   играете как " + _subs.Active;
            }
            catch (Exception ex) { _status.Text = "Не загрузилось: " + ex.Message; }
        }

        private async Task Use()
        {
            var nick = _list.SelectedItem as string;
            if (nick == null) return;
            try
            {
                string active = await NareAuth.SubUseAsync(Token, nick, CancellationToken.None);
                _account.Username = active;
                AccountStore.Save();
                await Reload();
                _status.Text = "Теперь вы играете как " + active + ".";
                _status.ForeColor = Theme.Good;
            }
            catch (Exception ex) { _status.Text = "Не вышло: " + ex.Message; _status.ForeColor = Theme.Bad; }
        }

        private async Task Add()
        {
            string nick = Prompt.OneField(this, "Новый ник",
                "Новый игровой ник (3–16, латиница). Стоит 1000 монет. Чужой занять нельзя.", false);
            if (string.IsNullOrWhiteSpace(nick)) return;

            if (!AccountStore.IsValidNick(nick))
            {
                _status.Text = "Ник: 3–16 символов, латиница, цифры и _";
                _status.ForeColor = Theme.Warn;
                return;
            }

            try
            {
                _subs = await NareAuth.SubAddAsync(Token, nick.Trim(), CancellationToken.None);
                _account.Username = _subs.Active;
                AccountStore.Save();
                await Reload();
                _status.Text = "Ник " + nick.Trim() + " добавлен. Осталось монет: " + _subs.Coins + ".";
                _status.ForeColor = Theme.Good;
            }
            catch (Exception ex) { _status.Text = "Не вышло: " + ex.Message; _status.ForeColor = Theme.Bad; }
        }

        private void Draw(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            string nick = item as string;
            if (nick == null) return;
            var box = Rectangle.Inflate(r, -4, -2);
            if (selected) Theme.FillRound(g, box, 9, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 9, Color.FromArgb(16, 255, 255, 255));

            bool active = string.Equals(nick, _subs.Active, StringComparison.OrdinalIgnoreCase);
            Theme.Text1(g, nick, Theme.F(10.5f, FontStyle.Bold), Theme.Text,
                new Rectangle(box.X + 14, box.Y, box.Width - 120, box.Height));

            if (active)
            {
                var badge = new Rectangle(box.Right - 108, box.Y + (box.Height - 24) / 2, 94, 24);
                Theme.FillRound(g, badge, 12, Color.FromArgb(45, Theme.Good));
                Theme.Text1(g, "ИГРАЕТЕ", Theme.F(8f, FontStyle.Bold), Theme.Good, badge,
                    StringAlignment.Center, StringAlignment.Center);
            }
            else if (string.Equals(nick, _account.Name, StringComparison.OrdinalIgnoreCase))
            {
                Theme.Text1(g, "основной", Theme.F(8f), Theme.Dim,
                    new Rectangle(box.Right - 108, box.Y, 94, box.Height),
                    StringAlignment.Far, StringAlignment.Center);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Theme.Text1(e.Graphics, "Ники под аккаунтом " + _account.Name, Theme.F(12f, FontStyle.Bold),
                Theme.Text, new Rectangle(20, 18, 440, 24));
        }
    }
}
