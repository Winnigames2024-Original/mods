using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Экран бана поверх всего лаунчера. Пока он виден, работает только одна
    /// кнопка — «Подать апелляцию». Ничего не прячется: человек видит причину
    /// и срок.
    /// </summary>
    public class BanOverlay : Panel
    {
        private string _reason = "";
        private string _left = "";
        private string _nick = "";

        private readonly ChatView _chat = new ChatView();
        private readonly NareTextBox _input = new NareTextBox { Placeholder = "Сообщение администратору…" };
        private readonly NareButton _send = new NareButton { Text = "Отправить", Style = BtnStyle.Accent, Size = new Size(140, 38) };

        private readonly System.Windows.Forms.Timer _poll = new System.Windows.Forms.Timer { Interval = 5000 };

        public BanOverlay()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Bg;
            Visible = false;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            Controls.Add(_chat);
            Controls.Add(_input);
            Controls.Add(_send);

            _send.Click += (s, e) => Send();
            _input.Inner.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Send(); } };

            _chat.EditRequested += async (m, nt) =>
            {
                if (string.IsNullOrEmpty(Token) || string.IsNullOrEmpty(m.Id)) return;
                await NareAuth.ChatEditAsync(Token, _nick, m.Id, nt, CancellationToken.None);
                await Refresh();
            };
            _chat.DeleteRequested += async (m) =>
            {
                if (string.IsNullOrEmpty(Token) || string.IsNullOrEmpty(m.Id)) return;
                await NareAuth.ChatDeleteAsync(Token, _nick, m.Id, CancellationToken.None);
                await Refresh();
            };
            _chat.TranslateRequested += async (m) =>
            {
                if (string.IsNullOrEmpty(m.Text)) return;
                try { var tr = await Translate.TextAsync(m.Text, CancellationToken.None); InputPrompt.Info(FindForm(), "Перевод", tr); }
                catch { }
            };

            _poll.Tick += async delegate { if (Visible) await Refresh(); };

            Resize += (s, e) => Place();
        }

        public async void Show(string nick, string reason, string left)
        {
            _nick = nick ?? "";
            _reason = string.IsNullOrEmpty(reason) ? "Причина не указана." : reason;
            _left = left ?? "";
            Visible = true;
            BringToFront();
            Place();
            Invalidate();
            _poll.Start();
            await Refresh();
        }

        public void Hide1()
        {
            if (Visible) { Visible = false; SendToBack(); _poll.Stop(); }
        }

        private string Token { get { var a = AccountStore.Selected; return a == null ? "" : a.Token; } }

        private async System.Threading.Tasks.Task Refresh()
        {
            if (string.IsNullOrEmpty(Token)) return;
            try
            {
                var msgs = await NareAuth.BanChatAsync(Token, CancellationToken.None);
                Fill(msgs);
            }
            catch { }
        }

        private void Fill(System.Collections.Generic.List<NareAuth.ChatMessage> msgs)
        {
            _chat.MeNick = _nick;
            _chat.IsStaff = false;
            _chat.SetMessages(msgs, false, _nick);
        }

        private async void Send()
        {
            string text = (_input.Text ?? "").Trim();
            if (text.Length == 0 || string.IsNullOrEmpty(Token)) return;

            _input.Text = "";
            try
            {
                var msgs = await NareAuth.BanSendAsync(Token, text, CancellationToken.None);
                Fill(msgs);
            }
            catch (Exception ex)
            {
                MessageBox.Show(FindForm(), "Не отправилось: " + ex.Message, "Чат",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Place()
        {
            int cx = Width / 2;
            int top = 150;
            int chatW = Math.Min(620, Width - 80);
            int chatX = cx - chatW / 2;

            _chat.SetBounds(chatX, top, chatW, Math.Max(80, Height - top - 90));
            _input.SetBounds(chatX, _chat.Bottom + 10, chatW - 152, 38);
            _send.SetBounds(_input.Right + 12, _chat.Bottom + 10, 140, 38);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.Smooth(g);

            int cx = Width / 2;

            var lockBox = new Rectangle(cx - 22, 20, 44, 44);
            Theme.FillRoundGradient(g, lockBox, 12, Theme.Bad, Theme.Warn, 45f);
            Theme.Text1(g, "✖", Theme.F(18f, FontStyle.Bold), Color.White, lockBox,
                StringAlignment.Center, StringAlignment.Center);

            Theme.Text1(g, "Вы забанены", Theme.F(16f, FontStyle.Bold), Theme.Text,
                new Rectangle(0, 68, Width, 26), StringAlignment.Center, StringAlignment.Center);

            string sub = _nick.Length > 0 ? _nick : "";
            if (_left.Length > 0) sub += (sub.Length > 0 ? " · " : "") + _left;
            Theme.Text1(g, sub, Theme.F(9.5f), Theme.Muted,
                new Rectangle(0, 96, Width, 18), StringAlignment.Center, StringAlignment.Center);

            Theme.Text1(g, "Причина: " + _reason, Theme.F(9.5f), Theme.Warn,
                new Rectangle(0, 118, Width, 20), StringAlignment.Center, StringAlignment.Center);
        }
    }

    /// <summary>Крошечное окно ввода одной строки — своё, без системных диалогов.</summary>
    public static class Prompt
    {
        public static string Ask(IWin32Window owner, string title, string hint)
        {
            using (var form = new Form())
            {
                form.Text = title;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MaximizeBox = form.MinimizeBox = false;
                form.ShowInTaskbar = false;
                form.ClientSize = new Size(460, 220);
                form.BackColor = Theme.Bg;
                form.ForeColor = Theme.Text;
                form.Font = Theme.F(10f);

                var label = new NareLabel(hint, 9.5f, FontStyle.Regular, Theme.Muted)
                { Bounds = new Rectangle(18, 14, 424, 44) };

                var box = new TextBox
                {
                    Multiline = true,
                    Bounds = new Rectangle(18, 64, 424, 86),
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = Theme.Panel,
                    ForeColor = Theme.Text,
                    Font = Theme.F(10f)
                };

                var ok = new NareButton { Text = "Отправить", Style = BtnStyle.Accent, Size = new Size(150, 38) };
                var cancel = new NareButton { Text = "Отмена", Style = BtnStyle.Ghost, Size = new Size(120, 38) };
                ok.Location = new Point(18, 166);
                cancel.Location = new Point(322, 166);

                string result = null;
                ok.Click += (s, e) => { result = box.Text; form.DialogResult = DialogResult.OK; form.Close(); };
                cancel.Click += (s, e) => { form.DialogResult = DialogResult.Cancel; form.Close(); };

                form.Controls.Add(label);
                form.Controls.Add(box);
                form.Controls.Add(ok);
                form.Controls.Add(cancel);

                return form.ShowDialog(owner) == DialogResult.OK ? result : null;
            }
        }

        /// <summary>Один скрытый ввод пароля.</summary>
        public static string Password(IWin32Window owner, string title, string hint)
        {
            return OneField(owner, title, hint, true);
        }

        /// <summary>Одна строка ввода; mask — скрывать ли символы.</summary>
        public static string OneField(IWin32Window owner, string title, string hint, bool mask)
        {
            using (var form = Shell(title, 180))
            {
                var label = new NareLabel(hint, 9.5f, FontStyle.Regular, Theme.Muted)
                { Bounds = new Rectangle(18, 14, 424, 40) };
                var box = new NareTextBox { Bounds = new Rectangle(18, 60, 424, 40) };
                box.Inner.UseSystemPasswordChar = mask;

                var ok = new NareButton { Text = "ОК", Style = BtnStyle.Accent, Size = new Size(150, 38) };
                var cancel = new NareButton { Text = "Отмена", Style = BtnStyle.Ghost, Size = new Size(120, 38) };
                ok.Location = new Point(18, 124);
                cancel.Location = new Point(322, 124);

                string result = null;
                Action accept = () => { result = box.Text; form.DialogResult = DialogResult.OK; form.Close(); };
                ok.Click += (s, e) => accept();
                cancel.Click += (s, e) => { form.DialogResult = DialogResult.Cancel; form.Close(); };
                box.Inner.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; accept(); } };

                form.Controls.Add(label); form.Controls.Add(box);
                form.Controls.Add(ok); form.Controls.Add(cancel);

                return form.ShowDialog(owner) == DialogResult.OK ? result : null;
            }
        }

        /// <summary>Старый и новый пароль разом.</summary>
        public static Tuple<string, string> ChangePassword(IWin32Window owner)
        {
            using (var form = Shell("Смена пароля", 240))
            {
                var l1 = new NareLabel("Текущий пароль", 9.5f, FontStyle.Regular, Theme.Muted)
                { Bounds = new Rectangle(18, 12, 424, 20) };
                var old1 = new NareTextBox { Bounds = new Rectangle(18, 36, 424, 38) };
                old1.Inner.UseSystemPasswordChar = true;

                var l2 = new NareLabel("Новый пароль (от 4 символов)", 9.5f, FontStyle.Regular, Theme.Muted)
                { Bounds = new Rectangle(18, 84, 424, 20) };
                var new1 = new NareTextBox { Bounds = new Rectangle(18, 108, 424, 38) };
                new1.Inner.UseSystemPasswordChar = true;

                var ok = new NareButton { Text = "Сменить", Style = BtnStyle.Accent, Size = new Size(150, 38) };
                var cancel = new NareButton { Text = "Отмена", Style = BtnStyle.Ghost, Size = new Size(120, 38) };
                ok.Location = new Point(18, 182);
                cancel.Location = new Point(322, 182);

                Tuple<string, string> result = null;
                ok.Click += (s, e) => { result = Tuple.Create(old1.Text, new1.Text); form.DialogResult = DialogResult.OK; form.Close(); };
                cancel.Click += (s, e) => { form.DialogResult = DialogResult.Cancel; form.Close(); };

                form.Controls.Add(l1); form.Controls.Add(old1);
                form.Controls.Add(l2); form.Controls.Add(new1);
                form.Controls.Add(ok); form.Controls.Add(cancel);

                return form.ShowDialog(owner) == DialogResult.OK ? result : null;
            }
        }

        private static Form Shell(string title, int height)
        {
            return new Form
            {
                Text = title,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                ClientSize = new Size(460, height),
                BackColor = Theme.Bg,
                ForeColor = Theme.Text,
                Font = Theme.F(10f)
            };
        }
    }
}
