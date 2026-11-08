using System;
using System.Drawing;
using System.Threading;
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

        private readonly NareButton _appeal = new NareButton
        { Text = "Подать апелляцию на разбан", Style = BtnStyle.Accent, Size = new Size(300, 46), Radius = 12 };

        public BanOverlay()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Bg;
            Visible = false;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            _appeal.Click += (s, e) => Appeal();
            Controls.Add(_appeal);

            Resize += (s, e) => Place();
        }

        public void Show(string nick, string reason, string left)
        {
            _nick = nick ?? "";
            _reason = string.IsNullOrEmpty(reason) ? "Причина не указана." : reason;
            _left = left ?? "";
            Visible = true;
            BringToFront();
            Place();
            Invalidate();
        }

        public void Hide1()
        {
            if (Visible) { Visible = false; SendToBack(); }
        }

        private void Place()
        {
            _appeal.Location = new Point((Width - _appeal.Width) / 2, Height / 2 + 40);
        }

        private async void Appeal()
        {
            string text = Prompt.Ask(FindForm(),
                "Апелляция на разбан",
                "Коротко объясните, почему вас стоит разбанить. Это увидит администратор.");

            if (string.IsNullOrWhiteSpace(text)) return;

            _appeal.Enabled = false;
            _appeal.Invalidate();

            try
            {
                await NareAuth.AppealAsync(_nick, text.Trim(), CancellationToken.None);
                MessageBox.Show(FindForm(), "Апелляция отправлена. Ждите ответа администратора.",
                    "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(FindForm(), "Не удалось отправить: " + ex.Message,
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            _appeal.Enabled = true;
            _appeal.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.Smooth(g);

            int cx = Width / 2;
            int top = Height / 2 - 150;

            var lockBox = new Rectangle(cx - 34, top, 68, 68);
            Theme.FillRoundGradient(g, lockBox, 16, Theme.Bad, Theme.Warn, 45f);
            Theme.Text1(g, "✖", Theme.F(26f, FontStyle.Bold), Color.White, lockBox,
                StringAlignment.Center, StringAlignment.Center);

            Theme.Text1(g, "Доступ заблокирован", Theme.F(20f, FontStyle.Bold), Theme.Text,
                new Rectangle(0, top + 84, Width, 32), StringAlignment.Center, StringAlignment.Center);

            string sub = _nick.Length > 0 ? "Аккаунт " + _nick : "";
            if (_left.Length > 0) sub += (sub.Length > 0 ? " · " : "") + _left;
            Theme.Text1(g, sub, Theme.F(10f), Theme.Muted,
                new Rectangle(0, top + 120, Width, 22), StringAlignment.Center, StringAlignment.Center);

            var reasonBox = new Rectangle(cx - 220, top + 150, 440, 56);
            Theme.FillRound(g, reasonBox, 10, Theme.Panel);
            Theme.DrawRound(g, reasonBox, 10, Theme.Border);
            Theme.Text1(g, _reason, Theme.F(9.5f), Theme.Text,
                Rectangle.Inflate(reasonBox, -14, -8), StringAlignment.Center, StringAlignment.Center);
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
    }
}
