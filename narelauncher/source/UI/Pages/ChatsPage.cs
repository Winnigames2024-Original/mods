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
        private readonly ChatView _msgs = new ChatView();
        private readonly NareTextBox _input = new NareTextBox { Placeholder = "Написать сообщение…" };
        private readonly NareButton _send = new NareButton { Text = "Отправить", Style = BtnStyle.Accent, Size = new Size(150, 40) };
        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);

        // --- админский список чатов + кнопки
        private readonly VirtualList _list = new VirtualList { ItemHeight = 56, EmptyText = "Никто ещё не писал." };
        private readonly NareButton _unban = new NareButton { Text = "Разбанить", Style = BtnStyle.Success, Size = new Size(150, 38) };
        private readonly NareDropDown _staffPick = new NareDropDown();
        private readonly NareButton _assign = new NareButton { Text = "Передать", Style = BtnStyle.Ghost, Size = new Size(130, 38) };
        private readonly NareLabel _who = new NareLabel("", 11f, FontStyle.Bold, Theme.Text);

        // --- бот Publish: приём/отклонение заявок
        private readonly NareDropDown _pubPick = new NareDropDown();
        private readonly NareButton _pubOk = new NareButton { Text = "Одобрить", Style = BtnStyle.Success, Size = new Size(150, 40) };
        private readonly NareButton _pubNo = new NareButton { Text = "Отклонить", Style = BtnStyle.Danger, Size = new Size(150, 40) };
        private List<NareAuth.PendingServer> _pending = new List<NareAuth.PendingServer>();
        private List<string> _staff = new List<string>();
        private List<string> _staffNicks = new List<string>();

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
            Controls.Add(_staffPick);
            Controls.Add(_assign);
            Controls.Add(_who);
            Controls.Add(_pubPick);
            Controls.Add(_pubOk);
            Controls.Add(_pubNo);
            Controls.Add(_status);

            _list.Renderer = DrawSummary;
            _list.ItemActivated += async (s, e) => await OpenSelected();
            _list.SelectionChanged += async (s, e) => await OpenSelected();

            _send.Click += async (s, e) => await Send();
            _input.Inner.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _ = Send(); } };
            _unban.Click += async (s, e) => await Unban();
            _assign.Click += async (s, e) => await Assign();
            _pubOk.Click += async (s, e) => await Decide(true);
            _pubNo.Click += async (s, e) => await Decide(false);

            _poll.Tick += async (s, e) => await Refresh();

            _msgs.EditRequested += async (m, nt) => await EditMsg(m, nt);
            _msgs.DeleteRequested += async (m) => await DeleteMsg(m);
            _msgs.TranslateRequested += async (m) => await TranslateMsg(m);
        }

        private string OwnerNick { get { return _admin ? _openNick : MyNick; } }

        private async System.Threading.Tasks.Task EditMsg(NareAuth.ChatMessage m, string nt)
        {
            if (string.IsNullOrEmpty(Token) || string.IsNullOrEmpty(m.Id)) return;
            var err = await NareAuth.ChatEditAsync(Token, OwnerNick, m.Id, nt, CancellationToken.None);
            if (!string.IsNullOrEmpty(err)) { _status.Text = err; return; }
            await Refresh();
        }

        private async System.Threading.Tasks.Task DeleteMsg(NareAuth.ChatMessage m)
        {
            if (string.IsNullOrEmpty(Token) || string.IsNullOrEmpty(m.Id)) return;
            var err = await NareAuth.ChatDeleteAsync(Token, OwnerNick, m.Id, CancellationToken.None);
            if (!string.IsNullOrEmpty(err)) { _status.Text = err; return; }
            await Refresh();
        }

        private async System.Threading.Tasks.Task TranslateMsg(NareAuth.ChatMessage m)
        {
            if (string.IsNullOrEmpty(m.Text)) return;
            _status.Text = "перевожу…";
            try
            {
                var tr = await Translate.TextAsync(m.Text, CancellationToken.None);
                _status.Text = "";
                InputPrompt.Info(FindForm(), "Перевод", tr);
            }
            catch { _status.Text = "перевод недоступен"; }
        }

        private string Token { get { var a = AccountStore.Selected; return a == null ? "" : a.Token; } }
        private string MyNick { get { var a = AccountStore.Selected; return a == null ? "" : a.Name; } }

        public override void PageShown()
        {
            var acc = AccountStore.Selected;
            if (acc != null && string.IsNullOrEmpty(acc.Token)) { Host.EnsureAuth(); }

            _admin = acc != null && (acc.Admin || NareAuth.Last.IsAdmin || NareAuth.Last.IsModerator);
            Layout1();
            _list.Visible = _admin;
            _unban.Visible = _admin;
            _staffPick.Visible = _admin;
            _assign.Visible = _admin;
            _who.Visible = _admin;
            _pubPick.Visible = false;
            _pubOk.Visible = false;
            _pubNo.Visible = false;

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

                    if (_staff.Count == 0)
                    {
                        try
                        {
                            var st = await NareAuth.AdminStaffAsync(Token, CancellationToken.None);
                            _staff = new List<string>();
                            foreach (var m in st) _staff.Add(m.Nick + "  (" + m.Role + ")");
                            _staffNicks = new List<string>();
                            foreach (var m in st) _staffNicks.Add(m.Nick);
                            _staffPick.SetItems(_staff, null);
                        }
                        catch { }
                    }

                    if (string.Equals(_openNick, "Publish", StringComparison.Ordinal))
                    {
                        _pending = await NareAuth.AdminPendingAsync(Token, CancellationToken.None);
                        var pl = new List<string>();
                        foreach (var ps in _pending) pl.Add(ps.Name + " — " + (ps.HideIp ? "IP скрыт" : ps.Ip) + "  [" + ps.Owner + "]");
                        _pubPick.SetItems(pl, null);
                    }

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
            _msgs.MeNick = MyNick;
            _msgs.IsStaff = _admin;
            _msgs.SetMessages(_chat, _admin, MyNick);
            if (_admin)
            {
                bool pub = string.Equals(_openNick, "Publish", StringComparison.Ordinal);
                bool bugs = string.Equals(_openNick, "Bugs", StringComparison.Ordinal);
                _who.Text = _openNick.Length == 0 ? "Выберите чат слева"
                    : pub ? "🤖 Publish — заявки на публикацию (кликните по тексту, чтобы скопировать)"
                    : bugs ? "🤖 Bugs | Players — отчёты о вылетах (кликните по тексту, чтобы скопировать)"
                    : "Чат с " + _openNick + (IsDeletedOpen() ? " | Аккаунт Удалён" : (_openBanned ? "  (в бане)" : ""));

                // обычные элементы прячем на ботах; на Publish — кнопки заявок
                bool chat = !pub && !bugs;
                _input.Visible = chat; _send.Visible = chat;
                _unban.Visible = chat; _staffPick.Visible = chat; _assign.Visible = chat;
                _pubPick.Visible = pub; _pubOk.Visible = pub; _pubNo.Visible = pub;

                _unban.Enabled = _openNick.Length > 0 && _openBanned;
                _assign.Enabled = _openNick.Length > 0;
                bool hasPending = _pending != null && _pending.Count > 0;
                _pubOk.Enabled = pub && hasPending;
                _pubNo.Enabled = pub && hasPending;
            }
        }

        private bool IsDeletedOpen()
        {
            foreach (var it in _list.Items)
            {
                var c = it as NareAuth.ChatSummary;
                if (c != null && string.Equals(c.Nick, _openNick, StringComparison.Ordinal)) return c.Deleted;
            }
            return false;
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
            int i = _staffPick.SelectedIndex;
            if (i < 0 || i >= _staffNicks.Count) { _status.Text = "Выберите, кому передать (список справа)."; return; }
            string to = _staffNicks[i];
            try
            {
                await NareAuth.AdminChatAssignAsync(Token, _openNick, to, CancellationToken.None);
                _status.Text = "Чат передан: " + to;
                _status.ForeColor = Theme.Good;
                await Refresh();
            }
            catch (Exception ex) { _status.Text = "Не вышло: " + ex.Message; _status.ForeColor = Theme.Bad; }
        }

        private async Task Decide(bool approve)
        {
            int i = _pubPick.SelectedIndex;
            if (i < 0 || i >= _pending.Count) { _status.Text = "Выберите заявку."; return; }
            var ps = _pending[i];
            try
            {
                if (approve) await NareAuth.ApproveServerAsync(Token, ps.Id, 0, CancellationToken.None);
                else
                {
                    string reason = Prompt.OneField(FindForm(), "Отклонить заявку",
                        "Причина отклонения «" + ps.Name + "» (можно пусто):", false);
                    await NareAuth.RejectServerAsync(Token, ps.Id, reason ?? "", CancellationToken.None);
                }
                _status.Text = (approve ? "Одобрено: " : "Отклонено: ") + ps.Name;
                _status.ForeColor = Theme.Good;
                await Refresh();
            }
            catch (Exception ex) { _status.Text = "Не вышло: " + ex.Message; _status.ForeColor = Theme.Bad; }
        }

        // --------------------------------------------------------- отрисовка

        private void DrawSummary(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var c = item as NareAuth.ChatSummary;
            if (c == null) return;
            var box = Rectangle.Inflate(r, -4, -3);
            if (selected) Theme.FillRound(g, box, 9, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 9, Color.FromArgb(16, 255, 255, 255));

            bool bot = c.Nick == "Publish" || c.Nick == "Bugs";
            string shown = !string.IsNullOrEmpty(c.Display) ? c.Display : c.Nick;
            if (!string.IsNullOrEmpty(c.Tag)) shown = c.Tag + " " + shown;
            string title = bot ? ("🤖 " + (c.Nick == "Bugs" ? "Bugs | Players" : "Publish"))
                : c.Deleted ? (c.Nick + " | Аккаунт Удалён") : shown;
            var titleColor = bot ? Theme.Accent : c.Deleted ? Theme.Dim : Theme.Text;
            // точка онлайна
            if (!bot && !c.Deleted && c.Online)
            {
                using (var br = new SolidBrush(Theme.Good))
                    g.FillEllipse(br, box.X + 12, box.Y + 13, 8, 8);
            }
            int tx = (!bot && !c.Deleted && c.Online) ? box.X + 26 : box.X + 12;
            Theme.Text1(g, title, Theme.F(10.5f, FontStyle.Bold), titleColor,
                new Rectangle(tx, box.Y + 7, box.Right - tx - 76, 20));

            string prev = (c.LastAdmin && !bot ? "вы: " : "") + c.Last;
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

                int ubX = w - pad - _unban.Width;
                int asX = ubX - 8 - _assign.Width;
                int spX = asX - 8 - 170;
                _unban.Location = new Point(ubX, top - 6);
                _assign.Location = new Point(asX, top - 6);
                _staffPick.SetBounds(spX, top - 4, 170, 34);
                _who.SetBounds(cx, top - 2, Math.Max(80, spX - cx - 10), 22);

                _msgs.SetBounds(cx, top + 30, cw, h - top - 30 - bottom);

                // обычный ввод
                _input.SetBounds(cx, h - bottom + 6, cw - 162, 40);
                _send.Location = new Point(cx + cw - 150, h - bottom + 6);

                // панель Publish (накладывается на ввод, видна только в чате Publish)
                _pubOk.Location = new Point(cx + cw - 150, h - bottom + 6);
                _pubNo.Location = new Point(cx + cw - 150 - 158, h - bottom + 6);
                _pubPick.SetBounds(cx, h - bottom + 9, cw - 320, 34);

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
