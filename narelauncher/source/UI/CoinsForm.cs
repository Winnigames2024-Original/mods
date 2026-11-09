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
    /// Монеты игрока: баланс, ежедневный бонус, перевод другому игроку и топ.
    /// </summary>
    public class CoinsForm : Form
    {
        private readonly Account _account;

        private readonly NareLabel _balance = new NareLabel("", 15f, FontStyle.Bold, Theme.Text);
        private readonly NareButton _daily = new NareButton { Text = "Ежедневный бонус", Style = BtnStyle.Success, Size = new Size(230, 42) };
        private readonly NareTextBox _toNick = new NareTextBox { Placeholder = "Кому (ник аккаунта)" };
        private readonly NareTextBox _amount = new NareTextBox { Placeholder = "Сколько" };
        private readonly NareButton _send = new NareButton { Text = "Перевести", Style = BtnStyle.Accent, Size = new Size(150, 40) };
        private readonly VirtualList _top = new VirtualList { ItemHeight = 34, EmptyText = "Загружаю топ…" };
        private readonly NareButton _close = new NareButton { Text = "Закрыть", Style = BtnStyle.Ghost, Size = new Size(120, 40) };
        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);

        public CoinsForm(Account account)
        {
            _account = account;
            Text = "Монеты";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = MinimizeBox = false; ShowInTaskbar = false;
            ClientSize = new Size(520, 470);
            BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.F(10f);

            _balance.SetBounds(24, 18, 472, 28);
            _daily.Location = new Point(24, 54);
            Cap("Перевести монеты игроку", 108);
            _toNick.SetBounds(24, 126, 250, 40);
            _amount.SetBounds(282, 126, 100, 40);
            _send.Location = new Point(392, 126);
            Cap("Топ игроков", 180);
            _top.SetBounds(24, 200, 472, 210);
            _close.Location = new Point(380, 420);
            _status.SetBounds(24, 426, 340, 20);

            _top.Renderer = DrawTop;
            _daily.Click += async (s, e) => await Daily();
            _send.Click += async (s, e) => await Send();
            _close.Click += (s, e) => Close();

            Controls.Add(_balance); Controls.Add(_daily); Controls.Add(_toNick);
            Controls.Add(_amount); Controls.Add(_send); Controls.Add(_top);
            Controls.Add(_close); Controls.Add(_status);

            foreach (var c in _caps) Controls.Add(c);

            Load += async delegate { ShowBalance(); await LoadTop(); };
        }

        private string Token { get { return _account.Token; } }

        private void ShowBalance()
        {
            _balance.Text = "Ваши монеты: " + (NareAuth.Last.Coins);
        }

        private async Task Daily()
        {
            try
            {
                var r = await NareAuth.CoinsDailyAsync(Token, CancellationToken.None);
                NareAuth.Last.Coins = r.Coins;
                ShowBalance();
                _status.Text = r.Awarded > 0 ? ("Получено " + r.Awarded + " монет!") : ("Бонус снова через ~" + r.NextIn + " ч.");
                _status.ForeColor = r.Awarded > 0 ? Theme.Good : Theme.Muted;
                await LoadTop();
            }
            catch (Exception ex) { _status.Text = ex.Message; _status.ForeColor = Theme.Bad; }
        }

        private async Task Send()
        {
            string to = _toNick.Text.Trim();
            int amt = ParseAmount(_amount.Text);
            if (to.Length == 0) { _status.Text = "Укажите, кому."; return; }
            if (amt <= 0) { _status.Text = "Сколько монет?"; return; }
            try
            {
                long bal = await NareAuth.CoinsSendAsync(Token, to, amt, CancellationToken.None);
                NareAuth.Last.Coins = bal;
                ShowBalance();
                _amount.Text = "";
                _status.Text = "Переведено " + amt + " монет игроку " + to + ".";
                _status.ForeColor = Theme.Good;
                await LoadTop();
            }
            catch (Exception ex) { _status.Text = "Не вышло: " + ex.Message; _status.ForeColor = Theme.Bad; }
        }

        private static int ParseAmount(string t)
        {
            int v; return int.TryParse((t ?? "").Trim(), out v) ? v : 0;
        }

        private async Task LoadTop()
        {
            try
            {
                var top = await NareAuth.TopAsync(CancellationToken.None);
                var items = new List<object>();
                foreach (var t in top) items.Add(t);
                _top.SetItems(items);
            }
            catch { }
        }

        private void DrawTop(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var t = item as NareAuth.TopPlayer;
            if (t == null) return;
            var box = Rectangle.Inflate(r, -4, -2);
            bool me = string.Equals(t.Nick, _account.Name, StringComparison.OrdinalIgnoreCase);
            if (me) Theme.FillRound(g, box, 8, Color.FromArgb(40, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 8, Color.FromArgb(14, 255, 255, 255));

            Theme.Text1(g, "#" + (index + 1), Theme.F(9f, FontStyle.Bold), Theme.Dim,
                new Rectangle(box.X + 8, box.Y, 36, box.Height), StringAlignment.Near, StringAlignment.Center);
            Theme.Text1(g, t.Nick, Theme.F(10f, me ? FontStyle.Bold : FontStyle.Regular), Theme.Text,
                new Rectangle(box.X + 48, box.Y, box.Width - 180, box.Height), StringAlignment.Near, StringAlignment.Center);
            Theme.Text1(g, t.Coins + " монет", Theme.F(9.5f, FontStyle.Bold), Theme.Good,
                new Rectangle(box.Right - 140, box.Y, 132, box.Height), StringAlignment.Far, StringAlignment.Center);
        }

        // ---- подписи над полями
        private readonly List<NareLabel> _caps = new List<NareLabel>();
        private void Cap(string text, int y)
        {
            _caps.Add(new NareLabel(text, 9f, FontStyle.Bold, Theme.Muted) { Bounds = new Rectangle(24, y, 460, 16) });
        }
    }
}
