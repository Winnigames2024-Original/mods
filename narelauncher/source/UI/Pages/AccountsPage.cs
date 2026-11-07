using System;
using System.Collections.Generic;
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
            Controls.Add(_select);
            Controls.Add(_manage);
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
            Host.Home.Invalidate();
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
            Theme.Text1(g, (acc.IsOffline ? "Оффлайн-аккаунт" : "Microsoft") + " · UUID " +
                           (acc.Uuid.Length > 8 ? acc.Uuid.Substring(0, 8) : acc.Uuid),
                Theme.F(8.8f), Theme.Muted,
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
            _status.SetBounds(pad, by + 48, Math.Max(50, Width - pad * 2), 20);

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
