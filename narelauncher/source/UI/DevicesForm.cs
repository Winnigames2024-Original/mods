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
    /// Устройства аккаунта — как «Устройства» в Telegram/WhatsApp. Видно, где
    /// выполнен вход, и любое устройство можно отвязать. Отвязанное при ближайшей
    /// проверке само выйдет из аккаунта.
    /// </summary>
    public class DevicesForm : Form
    {
        private readonly Account _account;

        private readonly VirtualList _list = new VirtualList { ItemHeight = 58, EmptyText = "Загружаю…" };
        private readonly NareButton _unlink = new NareButton { Text = "Отвязать это устройство", Style = BtnStyle.Danger, Size = new Size(240, 40) };
        private readonly NareButton _refresh = new NareButton { Text = "Обновить", Style = BtnStyle.Ghost, Size = new Size(130, 40) };
        private readonly NareButton _others = new NareButton { Text = "Выйти на других устройствах", Style = BtnStyle.Danger, Size = new Size(270, 40) };
        private readonly NareButton _logout = new NareButton { Text = "Выйти из аккаунта", Style = BtnStyle.Ghost, Size = new Size(190, 40) };
        private readonly NareButton _close = new NareButton { Text = "Закрыть", Style = BtnStyle.Ghost, Size = new Size(120, 40) };
        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);

        private List<NareAuth.DeviceInfo> _devices = new List<NareAuth.DeviceInfo>();

        public DevicesForm(Account account)
        {
            _account = account;

            Text = "Устройства";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(520, 532);
            BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.F(10f);

            _list.Renderer = Draw;
            _list.SetBounds(20, 58, 480, 288);
            _unlink.Location = new Point(20, 356);
            _refresh.Location = new Point(270, 356);
            _others.Location = new Point(20, 404);
            _logout.Location = new Point(300, 404);
            _close.Location = new Point(380, 486);
            _status.SetBounds(20, 492, 340, 20);

            _unlink.Click += async (s, e) => await Unlink();
            _refresh.Click += async (s, e) => await Reload();
            _others.Click += async (s, e) => await LogoutOthers();
            _logout.Click += async (s, e) => await LogoutHere();
            _close.Click += (s, e) => Close();

            Controls.Add(_list); Controls.Add(_unlink); Controls.Add(_refresh);
            Controls.Add(_others); Controls.Add(_logout);
            Controls.Add(_close); Controls.Add(_status);

            Load += async delegate { await Reload(); };
        }

        private string Token { get { return _account.Token; } }

        private async Task Reload()
        {
            try
            {
                _devices = await NareAuth.DevicesAsync(Token, CancellationToken.None);
                var items = new List<object>();
                foreach (var d in _devices) items.Add(d);
                _list.SetItems(items);
                _status.Text = _devices.Count == 1 ? "1 устройство" : _devices.Count + " устройств(а)";
                _status.ForeColor = Theme.Muted;
            }
            catch (Exception ex) { _status.Text = "Не загрузилось: " + ex.Message; _status.ForeColor = Theme.Bad; }
        }

        private async Task Unlink()
        {
            var d = _list.SelectedItem as NareAuth.DeviceInfo;
            if (d == null) { _status.Text = "Выберите устройство."; return; }

            string q = d.Current
                ? "Отвязать ТЕКУЩЕЕ устройство? Вы выйдете из аккаунта на этом компьютере."
                : "Отвязать устройство «" + d.Name + "»? Там выполнят выход из аккаунта.";
            if (!Confirm(q)) return;

            try
            {
                await NareAuth.DeviceUnlinkAsync(Token, d.Id, CancellationToken.None);

                if (d.Current)
                {
                    // сами себя отвязали — выходим из аккаунта здесь
                    _account.Token = "";
                    AccountStore.Save();
                    _status.Text = "Устройство отвязано. Выход…";
                    await Task.Delay(500);
                    Close();
                    return;
                }

                await Reload();
                _status.Text = "Устройство отвязано.";
                _status.ForeColor = Theme.Good;
            }
            catch (Exception ex) { _status.Text = "Не вышло: " + ex.Message; _status.ForeColor = Theme.Bad; }
        }

        private async Task LogoutOthers()
        {
            if (!Confirm("Выйти из аккаунта на всех ДРУГИХ устройствах? Это устройство останется. Используйте, если аккаунт взломали.")) return;
            try
            {
                int kicked = await NareAuth.LogoutOthersAsync(Token, CancellationToken.None);
                _status.Text = kicked > 0 ? ("Отключено других устройств: " + kicked) : "Других устройств не было.";
                _status.ForeColor = Theme.Good;
                await Reload();
            }
            catch (Exception ex) { _status.Text = "Не вышло: " + ex.Message; _status.ForeColor = Theme.Bad; }
        }

        private async Task LogoutHere()
        {
            if (!Confirm("Выйти из аккаунта на этом устройстве? Чтобы вернуться, нужно будет ввести пароль.")) return;
            await NareAuth.LogoutAsync(Token, CancellationToken.None);
            _account.Token = "";
            AccountStore.Save();
            _status.Text = "Вы вышли из аккаунта.";
            await Task.Delay(400);
            Close();
        }

        private bool Confirm(string text)
        {
            return MessageBox.Show(this, text, "Устройства",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        private void Draw(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var d = item as NareAuth.DeviceInfo;
            if (d == null) return;
            var box = Rectangle.Inflate(r, -4, -3);
            if (selected) Theme.FillRound(g, box, 10, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 10, Color.FromArgb(16, 255, 255, 255));

            Theme.Text1(g, d.Name, Theme.F(11f, FontStyle.Bold), Theme.Text,
                new Rectangle(box.X + 14, box.Y + 8, box.Width - 120, 22));

            string when = d.At > 0 ? "вход " + Ago(d.At) : "";
            Theme.Text1(g, when, Theme.F(8.5f), Theme.Dim,
                new Rectangle(box.X + 14, box.Y + 30, box.Width - 120, 18));

            if (d.Current)
            {
                var badge = new Rectangle(box.Right - 132, box.Y + (box.Height - 24) / 2, 118, 24);
                Theme.FillRound(g, badge, 12, Color.FromArgb(45, Theme.Good));
                Theme.Text1(g, "ЭТО УСТРОЙСТВО", Theme.F(7.5f, FontStyle.Bold), Theme.Good, badge,
                    StringAlignment.Center, StringAlignment.Center);
            }
        }

        private static string Ago(long atMs)
        {
            try
            {
                var when = DateTimeOffset.FromUnixTimeMilliseconds(atMs).LocalDateTime;
                var span = DateTime.Now - when;
                if (span.TotalMinutes < 2) return "только что";
                if (span.TotalHours < 1) return (int)span.TotalMinutes + " мин назад";
                if (span.TotalDays < 1) return (int)span.TotalHours + " ч назад";
                if (span.TotalDays < 30) return (int)span.TotalDays + " дн назад";
                return when.ToString("dd.MM.yyyy");
            }
            catch { return ""; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Theme.Text1(e.Graphics, "Устройства аккаунта " + _account.Name, Theme.F(12f, FontStyle.Bold),
                Theme.Text, new Rectangle(20, 18, 480, 24));
            Theme.Text1(e.Graphics, "Здесь выполнен вход. Любое можно отвязать — там выйдут из аккаунта.",
                Theme.F(8.5f), Theme.Dim, new Rectangle(20, 40, 480, 16));
        }
    }
}
