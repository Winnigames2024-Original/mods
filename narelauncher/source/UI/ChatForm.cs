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

        private readonly ChatView _chat = new ChatView();
        private readonly NareTextBox _input = new NareTextBox { Placeholder = "Ответ игроку…" };
        private readonly NareButton _send = new NareButton { Text = "Отправить", Style = BtnStyle.Accent, Size = new Size(140, 38) };
        private readonly NareButton _unban = new NareButton { Text = "Разбанить", Style = BtnStyle.Success, Size = new Size(130, 34) };

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

            _chat.SetBounds(16, 56, 488, 348);
            _input.SetBounds(16, 416, 336, 40);
            _send.SetBounds(360, 417, 140, 38);
            _unban.Location = new Point(374, 16);

            Controls.Add(_chat); Controls.Add(_input); Controls.Add(_send); Controls.Add(_unban);

            _send.Click += (s, e) => Send();
            _unban.Click += async (s, e) => await Unban();
            _input.Inner.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Send(); } };

            _chat.IsStaff = true;
            var me = AccountStore.Selected;
            _chat.MeNick = me == null ? "" : me.Name;
            _chat.EditRequested += async (m, nt) =>
            {
                if (string.IsNullOrEmpty(m.Id)) return;
                await NareAuth.ChatEditAsync(_token, _nick, m.Id, nt, CancellationToken.None);
                await Refresh();
            };
            _chat.DeleteRequested += async (m) =>
            {
                if (string.IsNullOrEmpty(m.Id)) return;
                await NareAuth.ChatDeleteAsync(_token, _nick, m.Id, CancellationToken.None);
                await Refresh();
            };
            _chat.TranslateRequested += async (m) =>
            {
                if (string.IsNullOrEmpty(m.Text)) return;
                try { var tr = await Translate.TextAsync(m.Text, CancellationToken.None); InputPrompt.Info(this, "Перевод", tr); }
                catch { }
            };

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
            _chat.SetMessages(msgs, true, _nick);
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

        private async System.Threading.Tasks.Task Unban()
        {
            try
            {
                await NareAuth.UnbanAsync(_token, _nick, CancellationToken.None);
                _unban.Enabled = false;
                _unban.Text = "Разбанен";
                _unban.Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не вышло: " + ex.Message, "Чат",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnPaintTitle(PaintEventArgs e) { }
    }
}
