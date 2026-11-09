using System;
using System.Drawing;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>Небольшое модальное окно ввода текста в стиле лаунчера.</summary>
    public class InputPrompt : Form
    {
        private readonly NareTextBox _box = new NareTextBox();

        private InputPrompt(string title, string initial)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg;
            Size = new Size(420, 170);
            ShowInTaskbar = false;

            var head = new NareLabel { Text = title, Font = Theme.F(11f, FontStyle.Bold),
                ForeColor = Theme.Text, AutoSize = false, Bounds = new Rectangle(20, 16, 380, 24) };
            Controls.Add(head);

            _box.Bounds = new Rectangle(20, 52, 380, 42);
            _box.Inner.Font = Theme.F(10.5f);
            _box.Text = initial ?? "";
            Controls.Add(_box);

            var ok = new NareButton { Style = BtnStyle.Accent, Text = "Сохранить",
                Bounds = new Rectangle(214, 112, 186, 40) };
            var no = new NareButton { Style = BtnStyle.Subtle, Text = "Отмена",
                Bounds = new Rectangle(20, 112, 180, 40) };
            ok.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            no.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(ok);
            Controls.Add(no);

            _box.Inner.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; DialogResult = DialogResult.OK; Close(); }
                else if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            };
            Shown += (s, e) => { _box.Inner.Focus(); _box.Inner.SelectAll(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Theme.Smooth(e.Graphics);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.DrawRound(e.Graphics, r, 14, Theme.Border, 1.4f);
        }

        /// <summary>Показать текст только для чтения (например, перевод) с кнопкой «Закрыть».</summary>
        public static void Info(IWin32Window owner, string title, string text)
        {
            using (var f = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Theme.Bg,
                Size = new Size(440, 260),
                ShowInTaskbar = false
            })
            {
                var head = new NareLabel { Text = title, Font = Theme.F(11f, FontStyle.Bold),
                    ForeColor = Theme.Text, AutoSize = false, Bounds = new Rectangle(20, 16, 400, 24) };
                var body = new TextBox
                {
                    Text = text ?? "",
                    ReadOnly = true,
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    BorderStyle = BorderStyle.None,
                    BackColor = Theme.Panel,
                    ForeColor = Theme.Text,
                    Font = Theme.F(10.5f),
                    Bounds = new Rectangle(20, 50, 400, 140)
                };
                var ok = new NareButton { Style = BtnStyle.Accent, Text = "Закрыть",
                    Bounds = new Rectangle(20, 202, 400, 40) };
                ok.Click += (s, e) => f.Close();
                f.Controls.Add(head);
                f.Controls.Add(body);
                f.Controls.Add(ok);
                f.Paint += (s, e) =>
                {
                    Theme.Smooth(e.Graphics);
                    Theme.DrawRound(e.Graphics, new Rectangle(0, 0, f.Width - 1, f.Height - 1), 14, Theme.Border, 1.4f);
                };
                f.ShowDialog(owner);
            }
        }

        /// <summary>Показать окно; вернуть введённый текст или null, если отменили.</summary>
        public static string Ask(IWin32Window owner, string title, string initial)
        {
            using (var f = new InputPrompt(title, initial))
            {
                if (f.ShowDialog(owner) == DialogResult.OK)
                {
                    string t = (f._box.Text ?? "").Trim();
                    return t.Length == 0 ? null : t;
                }
                return null;
            }
        }
    }
}
