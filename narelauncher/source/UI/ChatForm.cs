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
    /// Переписка администратора с забаненным игроком. Тот же чат, что игрок
    /// видит на экране бана — здесь администратор отвечает.
    /// </summary>
    public class ChatForm : Form
    {
        private readonly string _token;
        private readonly string _nick;

        private readonly VirtualList _chat = new VirtualList { ItemHeight = 46, EmptyText = "Сообщений нет" };
        private readonly NareTextBox _input = new NareTextBox { Placeholder = "Ответ игроку…" };
        private readonly NareButton _send = new NareButton { Text = "Отправить", Style = BtnStyle.Accent, Size = new Size(140, 38) };

        private readonly System.Windows.Forms.Timer _poll = new System.Windows.Forms.Timer { Interval = 5000 };

        public ChatForm(string token, string nick)
        {
            _token = token;
            _nick = nick;

            Text = "Чат с " + nick;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(520, 480);
            BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.F(10f);

            _chat.Renderer = Draw;
            _chat.SetBounds(16, 16, 488, 388);
            _input.SetBounds(16, 416, 336, 40);
            _send.SetBounds(360, 417, 140, 38);

            Controls.Add(_chat); Controls.Add(_input); Controls.Add(_send);

            _send.Click += (s, e) => Send();
            _input.Inner.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Send(); } };

            _poll.Tick += async delegate { await Refresh(); };
            Load += async delegate { await Refresh(); _poll.Start(); };
            FormClosed += delegate { _poll.Stop(); };
        }

        private async Task Refresh()
        {
            try { Fill(await NareAuth.AdminChatAsync(_token, _nick, CancellationToken.None)); }
            catch { }
        }

        private void Fill(List<NareAuth.ChatMessage> msgs)
        {
            var items = new List<object>();
            foreach (var m in msgs) items.Add(m);
            _chat.SetItems(items);
            if (items.Count > 0) _chat.SelectedIndex = items.Count - 1;
        }

        private async void Send()
        {
            string text = (_input.Text ?? "").Trim();
            if (text.Length == 0) return;
            _input.Text = "";
            try { Fill(await NareAuth.AdminChatSendAsync(_token, _nick, text, CancellationToken.None)); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не отправилось: " + ex.Message, "Чат",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Draw(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var m = item as NareAuth.ChatMessage;
            if (m == null) return;

            bool mine = m.Admin;     // в админском окне «мои» — это сообщения админа
            int w = Math.Min(r.Width - 80, 380);
            var bubble = mine
                ? new Rectangle(r.Right - w - 10, r.Y + 4, w, r.Height - 10)
                : new Rectangle(r.X + 10, r.Y + 4, w, r.Height - 10);

            Theme.FillRound(g, bubble, 10, mine ? Color.FromArgb(60, Theme.Accent) : Theme.Panel);
            Theme.Text1(g, m.Admin ? "Вы (админ)" : _nick, Theme.F(7.6f, FontStyle.Bold),
                m.Admin ? Theme.Accent : Theme.Warn,
                new Rectangle(bubble.X + 10, bubble.Y + 4, bubble.Width - 20, 12));
            Theme.Text1(g, m.Text, Theme.F(9f), Theme.Text,
                new Rectangle(bubble.X + 10, bubble.Y + 18, bubble.Width - 20, bubble.Height - 22));
        }
    }
}
