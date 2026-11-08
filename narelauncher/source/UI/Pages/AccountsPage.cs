using System;
using System.Collections.Generic;
using System.Threading;
using System.Drawing;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI.Pages
{
    public class AccountsPage : PageBase
    {
        private readonly VirtualList _list = new VirtualList { ItemHeight = 62, EmptyText = "Пока нет ни одного аккаунта" };

        private readonly NareButton _select = new NareButton
        { Text = "Сделать активным", Style = BtnStyle.Success, Icon = Ico.Check, Size = new Size(200, 42) };

        private readonly NareButton _manage = new NareButton
        { Text = "Управление аккаунтами", Style = BtnStyle.Accent, Icon = Ico.Account, Size = new Size(230, 42) };

        private readonly NareButton _password = new NareButton
        { Text = "Сменить пароль", Style = BtnStyle.Ghost, Size = new Size(170, 42) };

        private readonly NareButton _delete = new NareButton
        { Text = "Удалить аккаунт", Style = BtnStyle.Danger, Icon = Ico.Trash, Size = new Size(190, 42) };

        private readonly NareButton _rename = new NareButton
        { Text = "Ники (подаккаунты)", Style = BtnStyle.Ghost, Size = new Size(230, 42) };

        private readonly NareButton _devices = new NareButton
        { Text = "Устройства", Style = BtnStyle.Ghost, Size = new Size(190, 42) };

        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);

        public AccountsPage()
        {
            Padding = new Padding(0);

            _list.Renderer = DrawAccount;
            _list.ItemActivated += (s, e) => SelectClick(null, EventArgs.Empty);

            _select.Click += SelectClick;
            // создание, переименование и удаление — всё в отдельном окне AccountsForm
            _manage.Click += (s, e) => Host.ManageAccounts();

            Controls.Add(_list);
            _password.Click += async (s, e) => await ChangePassword();
            _delete.Click += async (s, e) => await DeleteAccount();
            _rename.Click += async (s, e) => await RenameUser();
            _devices.Click += (s, e) => OpenDevices();

            Controls.Add(_select);
            Controls.Add(_manage);
            Controls.Add(_password);
            Controls.Add(_delete);
            Controls.Add(_rename);
            Controls.Add(_devices);
            Controls.Add(_status);
        }

        public override void PageShown()
        {
            ReloadList();
            Layout1();
        }

        public void ReloadList()
        {
            var items = new List<object>();
            foreach (var a in AccountStore.Accounts) items.Add(a);
            _list.SetItems(items);

            if (AccountStore.SelectedIndex >= 0 && AccountStore.SelectedIndex < items.Count)
                _list.SelectedIndex = AccountStore.SelectedIndex;

            if (Host != null) Host.RefreshAccountChip();

            if (AccountStore.Accounts.Count == 0)
            {
                _status.Text = "Аккаунтов нет. Нажмите «Управление аккаунтами», чтобы добавить ник.";
                _status.ForeColor = Theme.Warn;
            }
            else
            {
                var active = AccountStore.Selected;
                _status.Text = "Аккаунтов: " + AccountStore.Accounts.Count +
                               (active == null ? "" : "   ·   активный: " + active.Name);
                _status.ForeColor = Theme.Muted;
            }
        }

        private void SelectClick(object sender, EventArgs e)
        {
            if (_list.SelectedIndex < 0 || _list.SelectedIndex >= AccountStore.Accounts.Count) return;

            AccountStore.SelectedIndex = _list.SelectedIndex;
            AccountStore.Save();
            ReloadList();

            // нет пароля на сервере — сразу попросим войти
            var acc = AccountStore.Selected;
            if (acc != null && string.IsNullOrEmpty(acc.Token)) Host.EnsureAuth();

            Host.Home.Invalidate();
        }

        private Account Chosen
        {
            get
            {
                int i = _list.SelectedIndex;
                return i >= 0 && i < AccountStore.Accounts.Count ? AccountStore.Accounts[i] : null;
            }
        }

        private async System.Threading.Tasks.Task ChangePassword()
        {
            var acc = Chosen;
            if (acc == null) { _status.Text = "Выберите аккаунт."; return; }

            var pair = Prompt.ChangePassword(FindForm());
            if (pair == null) return;

            if (pair.Item2.Length < 4)
            {
                _status.Text = "Новый пароль — от 4 символов.";
                _status.ForeColor = Theme.Warn;
                return;
            }

            try
            {
                await NareAuth.ChangePasswordAsync(acc.Name, pair.Item1, pair.Item2, CancellationToken.None);
                _status.Text = "Пароль для " + acc.Name + " сменён.";
                _status.ForeColor = Theme.Good;
            }
            catch (Exception ex)
            {
                _status.Text = "Не вышло: " + ex.Message;
                _status.ForeColor = Theme.Bad;
            }
        }

        private async System.Threading.Tasks.Task RenameUser()
        {
            var acc0 = Chosen;
            if (acc0 == null) { _status.Text = "Выберите аккаунт."; return; }
            if (string.IsNullOrEmpty(acc0.Token)) { Host.EnsureAuth(); if (string.IsNullOrEmpty(acc0.Token)) return; }

            using (var form = new SubAccountsForm(acc0)) form.ShowDialog(FindForm());
            ReloadList();
            await System.Threading.Tasks.Task.CompletedTask;
            return;
#pragma warning disable 0162
            var acc = Chosen;
            if (acc == null) { _status.Text = "Выберите аккаунт."; return; }
            if (string.IsNullOrEmpty(acc.Token)) { Host.EnsureAuth(); if (string.IsNullOrEmpty(acc.Token)) return; }

            string name = Prompt.OneField(FindForm(), "Смена игрового ника",
                "Новый игровой ник (3–16, латиница). Логин аккаунта " + acc.Name +
                " не меняется. Стоит 1000 монет.", false);
            if (string.IsNullOrWhiteSpace(name)) return;

            if (!AccountStore.IsValidNick(name))
            {
                _status.Text = "Ник: 3–16 символов, латиница, цифры и _";
                _status.ForeColor = Theme.Warn;
                return;
            }

            try
            {
                long coins = await NareAuth.ChangeUsernameAsync(acc.Token, name.Trim(), CancellationToken.None);
                acc.Username = name.Trim();
                AccountStore.Save();
                ReloadList();
                _status.Text = "Теперь вы играете как " + name.Trim() + ". Осталось монет: " + coins + ".";
                _status.ForeColor = Theme.Good;
            }
            catch (Exception ex)
            {
                _status.Text = "Не вышло: " + ex.Message;
                _status.ForeColor = Theme.Bad;
            }
        }

#pragma warning restore 0162
        private async System.Threading.Tasks.Task DeleteAccount()
        {
            var acc = Chosen;
            if (acc == null) { _status.Text = "Выберите аккаунт."; return; }

            if (!Host.Confirm("Удалить аккаунт " + acc.Name + " и на сервере, и из лаунчера? Это навсегда."))
                return;

            string password = Prompt.Password(FindForm(), "Удаление аккаунта",
                "Введите пароль аккаунта " + acc.Name + ", чтобы подтвердить удаление.");
            if (string.IsNullOrEmpty(password)) return;

            try
            {
                await NareAuth.DeleteAsync(acc.Name, password, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _status.Text = "Сервер не дал удалить: " + ex.Message;
                _status.ForeColor = Theme.Bad;
                return;
            }

            AccountStore.Remove(_list.SelectedIndex);
            ReloadList();
            _status.Text = "Аккаунт " + acc.Name + " удалён.";
            _status.ForeColor = Theme.Muted;
        }

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

            var av = new Rectangle(box.X + 14, box.Y + 10, 36, 36);
            Theme.FillRoundGradient(g, av, 8, Theme.Accent, Theme.Accent2, 30f);
            Theme.Text1(g, acc.Name.Substring(0, 1).ToUpperInvariant(), Theme.F(13f, FontStyle.Bold),
                Color.White, av, StringAlignment.Center, StringAlignment.Center);

            Theme.Text1(g, acc.Name, Theme.F(11f, FontStyle.Bold), Theme.Text,
                new Rectangle(av.Right + 14, box.Y + 10, box.Width - 220, 22));
            string sub = "NareLauncher онлайн";
            if (!string.Equals(acc.PlayName, acc.Name, StringComparison.Ordinal))
                sub += " · в игре: " + acc.PlayName;
            Theme.Text1(g, sub, Theme.F(8.8f), Theme.Muted,
                new Rectangle(av.Right + 14, box.Y + 32, box.Width - 220, 18));

            if (active)
            {
                var badge = new Rectangle(box.Right - 108, box.Y + (box.Height - 24) / 2, 94, 24);
                Theme.FillRound(g, badge, 12, Color.FromArgb(45, Theme.Good));
                Theme.Text1(g, "АКТИВНЫЙ", Theme.F(8f, FontStyle.Bold), Theme.Good, badge,
                    StringAlignment.Center, StringAlignment.Center);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Layout1();
        }

        private void OpenDevices()
        {
            var acc = Chosen;
            if (acc == null) { _status.Text = "Выберите аккаунт."; return; }
            if (string.IsNullOrEmpty(acc.Token)) { Host.EnsureAuth(); if (string.IsNullOrEmpty(acc.Token)) return; }

            using (var form = new DevicesForm(acc)) form.ShowDialog(FindForm());
        }

        private void Layout1()
        {
            int pad = 28;
            int listY = 76;
            int bottomH = 92;

            _list.SetBounds(pad, listY, Math.Max(100, Width - pad * 2),
                            Math.Max(60, Height - listY - bottomH));

            int by = Height - bottomH + 18;
            _select.Location = new Point(pad, by);
            _manage.Location = new Point(pad + 216, by);
            _password.Location = new Point(pad + 216 + 246, by);
            _delete.Location = new Point(pad + 216 + 246 + 186, by);
            _rename.Location = new Point(pad, by + 50);
            _devices.Location = new Point(pad + 246, by + 50);
            _status.SetBounds(pad + 246 + 206, by + 58, Math.Max(50, Width - pad * 2 - 246 - 206), 20);

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.Smooth(g);
            Theme.Text1(g, "Аккаунты", Theme.F(17f, FontStyle.Bold), Theme.Text,
                new Rectangle(28, 16, 400, 28));
            Theme.Text1(g, "Создание, переименование и удаление — в окне «Управление аккаунтами»",
                Theme.F(9.5f), Theme.Muted, new Rectangle(28, 40, 760, 20));
        }
    }
}
