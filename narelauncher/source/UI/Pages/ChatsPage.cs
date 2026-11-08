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
    /// «Чаты» — есть у всех. Обычный игрок пишет администрации (хоть забанен,
    /// хоть нет). Администратор видит список всех переписок, отвечает, а в чате
    /// с забаненным может разбанить или передать чат другому администратору.
    /// </summary>
    public class ChatsPage : PageBase
    {
        // --- общий ввод/лента
        private readonly VirtualList _msgs = new VirtualList { ItemHeight = 46, EmptyText = "Сообщений пока нет." };
        private readonly NareTextBox _input = new NareTextBox { Placeholder = "Написать сообщение…" };
        private readonly NareButton _send = new NareButton { Text = "Отправить", Style = BtnStyle.Accent, Size = new Size(150, 40) };
        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);

        // --- админский список чатов + кнопки
        private readonly VirtualList _list = new VirtualList { ItemHeight = 56, EmptyText = "Никто ещё не писал." };
        private readonly NareButton _unban = new NareButton { Text = "Разбанить", Style = BtnStyle.Success, Size = new Size(150, 38) };
        private readonly NareButton _assign = new NareButton { Text = "Передать админу", Style = BtnStyle.Ghost, Size = new Size(190, 38) };
        private readonly NareLabel _who = new NareLabel("", 11f, FontStyle.Bold, Theme.Text);

        private readonly System.Windows.Forms.Timer _poll = new System.Windows.Forms.Timer { Interval = 5000 };

        private bool _admin;
        private string _openNick = "";          // с кем открыт чат (у админа)
        private bool _openBanned;
        private List<NareAuth.ChatMessage> _chat = new List<NareAuth.ChatMessage>();

        public ChatsPage()
        {
            Controls.Add(_msgs);
            Controls.Add(_list);
            Controls.Add(_input);
            Controls.Add(_send);
            Controls.Add(_unban);
            Controls.Add(_assign);
            Controls.Add(_who);
            Controls.Add(_status);

            _msgs.Renderer = DrawMsg;
            _list.Renderer = DrawSummary;
            _list.ItemActivated += async (s, e) => await OpenSelected();
            _list.SelectionChanged += async (s, e) => await OpenSelected();

            _send.Click += async (s, e) => await Send();
            _input.Inner.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _ = Send(); } };
            _unban.Click += async (s, e) => await Unban();
            _assign.Click += async (s, e) => await Assign();

            _poll.Tick += async (s, e) => await Refresh();
        }

        private string Token { get { var a = AccountStore.Selected; return a == null ? "" : a.Token; } }
        private string MyNick { get { var a = AccountStore.Selected; return a == null ? "" : a.Name; } }

        public override void PageShown()
        {
            var acc = AccountStore.Selected;
            if (acc != null && string.IsNullOrEmpty(acc.Token)) { Host.EnsureAuth(); }

            _admin = acc != null && (acc.Admin || NareAuth.Last.IsAdmin);
            Layout1();
            _list.Visible = _admin;
            _unban.Visible = _admin;
            _assign.Visible = _admin;
            _who.Visible = _admin;

            if (!_admin) { _openNick = MyNick; _openBanned = false; }

            _poll.Start();
            _ = Refresh();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible) _poll.Stop();
        }

        private async Task Refresh()
        {
            if (string.IsNullOrEmpty(Token)) return;
            if (Host != null) Host.MarkChatsSeen();   // пока открыт чат — без всплывашек
            try
            {
                if (_admin)
                {
                    var chats = await NareAuth.AdminChatsAsync(Token, CancellationToken.None);
                    var items = new List<object>();
                    foreach (var c in chats) items.Add(c);
                    int keep = _list.SelectedIndex;
                    _list.SetItems(items);
                    if (keep >= 0 && keep < items.Count) _list.SelectedIndex = keep;

                    if (_openNick.Length > 0)
                        _chat = await NareAuth.AdminChatAsync(Token, _openNick, CancellationToken.None);
                }
                else
                {
                    _chat = await NareAuth.BanChatAsync(Token, CancellationToken.None);
                }
                ShowChat();
            }
            catch (Exception ex) { _status.Text = ex.Message; }
        }

        private void ShowChat()
        {
            var items = new List<object>();
            foreach (var m in _chat) items.Add(m);
            _msgs.SetItems(items);
            if (items.Count > 0) _msgs.SelectedIndex = items.Count - 1;
            if (_admin)
            {
                _who.Text = _openNick.Length > 0
                    ? "Чат с " + _openNick + (_openBanned ? "  (в бане)" : "")
                    : "Выберите чат слева";
                _unban.Enabled = _openNick.Length > 0 && _openBanned;
                _assign.Enabled = _openNick.Length > 0;
            }
        }

        private async Task OpenSelected()
        {
            var c = _list.SelectedItem as NareAuth.ChatSummary;
            if (c == null) return;
            _openNick = c.Nick;
            _openBanned = c.Banned;
            await Refresh();
        }

        private async Task Send()
        {
            string text = _input.Text.Trim();
            if (text.Length == 0) return;
            if (string.IsNullOrEmpty(Token)) { Host.EnsureAuth(); if (string.IsNullOrEmpty(Token)) return; }

            try
            {
                if (_admin)
                {
                    if (_openNick.Length == 0) { _status.Text = "Выберите чат."; return; }
                    _chat = await NareAuth.AdminChatSendAsync(Token, _openNick, text, CancellationToken.None);
                }
                else
                {
                    _chat = await NareAuth.BanSendAsync(Token, text, CancellationToken.None);
                }
                _input.Text = "";
                ShowChat();
            }
            catch (Exception ex) { _status.Text = "Не отправилось: " + ex.Message; }
        }

        private async Task Unban()
        {
            if (_openNick.Length == 0) return;
            try
            {
                await NareAuth.UnbanAsync(Token, _openNick, CancellationToken.None);
                _openBanned = false;
                _status.Text = _openNick + " разбанен.";
                await Refresh();
            }
            catch (Exception ex) { _status.Text = "Не вышло: " + ex.Message; }
        }

        private async Task Assign()
        {
            if (_openNick.Length == 0) return;
            string to = Prompt.OneField(FindForm(), "Передать чат",
                "Ник администратора, которому передать чат с " + _openNick + ":", false);
            if (string.IsNullOrWhiteSpace(to)) return;
            try
            {
                await NareAuth.AdminChatAssignAsync(Token, _openNick, to.Trim(), CancellationToken.None);
                _status.Text = "Чат передан администратору " + to.Trim() + ".";
                await Refresh();
            }
            catch (Exception ex) { _status.Text = "Не вышло: " + ex.Message; }
        }

        // --------------------------------------------------------- отрисовка

        private void DrawSummary(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var c = item as NareAuth.ChatSummary;
            if (c == null) return;
            var box = Rectangle.Inflate(r, -4, -3);
            if (selected) Theme.FillRound(g, box, 9, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 9, Color.FromArgb(16, 255, 255, 255));

            Theme.Text1(g, c.Nick, Theme.F(10.5f, FontStyle.Bold), Theme.Text,
                new Rectangle(box.X + 12, box.Y + 7, box.Width - 90, 20));

            string prev = (c.LastAdmin ? "вы: " : "") + c.Last;
            Theme.Text1(g, prev, Theme.F(8.5f), Theme.Dim,
                new Rectangle(box.X + 12, box.Y + 28, box.Width - 24, 18));

            if (c.Banned)
            {
                var badge = new Rectangle(box.Right - 70, box.Y + 8, 58, 20);
                Theme.FillRound(g, badge, 10, Color.FromArgb(50, Theme.Bad));
                Theme.Text1(g, "БАН", Theme.F(7.5f, FontStyle.Bold), Theme.Bad, badge,
                    StringAlignment.Center, StringAlignment.Center);
            }
            if (!string.IsNullOrEmpty(c.Assigned))
                Theme.Text1(g, "→ " + c.Assigned, Theme.F(7.5f), Theme.Warn,
                    new Rectangle(box.Right - 90, box.Y + 30, 82, 16), StringAlignment.Far, StringAlignment.Center);
        }

        private void DrawMsg(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var m = item as NareAuth.ChatMessage;
            if (m == null) return;

            // своё справа: у игрока — его сообщения, у админа — админские
            bool mine = _admin ? m.Admin : !m.Admin;
            int w = Math.Min(r.Width - 80, 380);
            var bubble = mine
                ? new Rectangle(r.Right - w - 10, r.Y + 4, w, r.Height - 10)
                : new Rectangle(r.X + 10, r.Y + 4, w, r.Height - 10);

            Theme.FillRound(g, bubble, 10, mine ? Color.FromArgb(60, Theme.Accent) : Theme.Panel);
            string name = m.Admin ? (m.From + " · админ") : m.From;
            Theme.Text1(g, name, Theme.F(7.6f, FontStyle.Bold), m.Admin ? Theme.Accent : Theme.Warn,
                new Rectangle(bubble.X + 10, bubble.Y + 4, bubble.Width - 20, 12));
            Theme.Text1(g, m.Text, Theme.F(9f), Theme.Text,
                new Rectangle(bubble.X + 10, bubble.Y + 18, bubble.Width - 20, bubble.Height - 22));
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Layout1(); }

        private void Layout1()
        {
            int pad = 28, top = 70, bottom = 60;
            int w = Width, h = Height;

            if (_admin)
            {
                int listW = 300;
                _list.SetBounds(pad, top, listW, h - top - 20);
                int cx = pad + listW + 20;
                int cw = w - cx - pad;
                _who.SetBounds(cx, top - 2, cw, 22);
                _unban.Location = new Point(w - pad - _assign.Width - _unban.Width - 12, top - 6);
                _assign.Location = new Point(w - pad - _assign.Width, top - 6);
                _msgs.SetBounds(cx, top + 30, cw, h - top - 30 - bottom);
                _input.SetBounds(cx, h - bottom + 6, cw - 162, 40);
                _send.Location = new Point(cx + cw - 150, h - bottom + 6);
                _status.SetBounds(cx, h - 24, cw, 18);
            }
            else
            {
                int cx = pad, cw = w - pad * 2;
                _msgs.SetBounds(cx, top, cw, h - top - bottom);
                _input.SetBounds(cx, h - bottom + 6, cw - 162, 40);
                _send.Location = new Point(cx + cw - 150, h - bottom + 6);
                _status.SetBounds(cx, h - 24, cw, 18);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Theme.Text1(e.Graphics, "Чаты", Theme.F(17f, FontStyle.Bold), Theme.Text,
                new Rectangle(28, 18, 500, 30));
            if (!_admin)
                Theme.Text1(e.Graphics, "Напишите администрации — ответят здесь же.",
                    Theme.F(9.5f), Theme.Muted, new Rectangle(28, 46, 600, 20));
        }
    }
}
