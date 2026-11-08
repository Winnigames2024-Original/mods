using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Окно пароля для уже существующего аккаунта, у которого ещё нет сессии
    /// на сервере (создан в старой версии лаунчера или заведён на другом
    /// компьютере). Ник известен — спрашиваем только пароль и жмём «Сохранить».
    ///
    /// Логика как при добавлении: сначала пробуем зарегистрировать ник этим
    /// паролем; занят — входим им же. Так аккаунт либо заводится на сервере,
    /// либо просто подхватывается существующий.
    /// </summary>
    public class AuthForm : Form
    {
        private const int TitleHeight = 44;

        private readonly Account _account;

        private readonly NareTextBox _password = new NareTextBox { Placeholder = "Пароль аккаунта (от 4 символов)" };
        private readonly NareButton _save = new NareButton
        { Text = "СОХРАНИТЬ", Style = BtnStyle.Accent, Icon = Ico.Check, Size = new Size(220, 46), Radius = 12 };
        private readonly NareButton _closeX = new NareButton
        { Text = "", Style = BtnStyle.Ghost, Icon = Ico.Close, Size = new Size(40, 28), Radius = 8 };
        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Warn);

        private bool _busy;

        /// <summary>Удалось ли войти.</summary>
        public bool Done;

        public AuthForm(Account account)
        {
            _account = account;

            Text = "Пароль аккаунта";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(500, 288);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(9.5f);
            DoubleBuffered = true;
            ShowInTaskbar = false;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            _password.Inner.UseSystemPasswordChar = true;
            _password.Inner.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                Accept();
            };
            _password.Inner.TextChanged += (s, e) =>
            {
                _status.Text = "";
                _save.Enabled = _password.Text.Length > 0 && !_busy;
                _save.Invalidate();
            };

            _save.Click += (s, e) => Accept();
            _closeX.Click += (s, e) => Close();

            Controls.Add(_password);
            Controls.Add(_save);
            Controls.Add(_closeX);
            Controls.Add(_status);

            _save.Enabled = false;
            Layout1();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try { _password.Inner.Focus(); } catch { }
        }

        private async void Accept()
        {
            string password = _password.Text;
            if (password.Length < 4)
            {
                Warn("Пароль — от 4 символов.");
                return;
            }

            _busy = true;
            _save.Enabled = false;
            _save.Invalidate();
            _status.ForeColor = Theme.Muted;
            _status.Text = "Проверяю на сервере…";

            NareAuth.LoginResult result = null;
            string problem = null;

            try
            {
                try { result = await NareAuth.RegisterAsync(_account.Name, password, CancellationToken.None); }
                catch { result = await NareAuth.LoginAsync(_account.Name, password, CancellationToken.None); }
            }
            catch (Exception ex) { problem = ex.Message; }

            _busy = false;

            if (result == null)
            {
                Warn(problem != null && problem.Contains("не подходят")
                    ? "Пароль не подходит к этому аккаунту."
                    : "Сервер недоступен: " + (problem ?? "нет связи"));
                _save.Enabled = _password.Text.Length > 0;
                _save.Invalidate();
                return;
            }

            _account.Token = result.Token;
            _account.Admin = result.Admin;
            AccountStore.Save();

            Done = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Warn(string text)
        {
            _status.Text = text;
            _status.ForeColor = Theme.Warn;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.Smooth(g);

            var bar = new Rectangle(0, 0, Width, TitleHeight);
            using (var b = new SolidBrush(Theme.Sidebar)) g.FillRectangle(b, bar);
            using (var pen = new Pen(Color.FromArgb(28, 255, 255, 255)))
                g.DrawLine(pen, 0, TitleHeight - 1, Width, TitleHeight - 1);

            var logo = new Rectangle(18, (TitleHeight - 20) / 2, 20, 20);
            Theme.FillRoundGradient(g, logo, 6, Theme.Accent, Theme.Accent2, 35f);
            Theme.Text1(g, "N", Theme.F(10f, FontStyle.Bold), Color.White, logo,
                StringAlignment.Center, StringAlignment.Center);
            Theme.Text1(g, "Вход в аккаунт", Theme.F(11f, FontStyle.Bold), Theme.Text,
                new Rectangle(48, 0, 300, TitleHeight));

            Theme.Text1(g, "Аккаунт " + _account.Name, Theme.F(15f, FontStyle.Bold), Theme.Text,
                new Rectangle(24, TitleHeight + 18, Width - 48, 26));
            Theme.Text1(g, "У этого аккаунта ещё нет пароля на сервере. Задайте его — "
                + "он защитит аккаунт и откроет функции «Вместе», монеты и баны.",
                Theme.F(9f), Theme.Muted, new Rectangle(24, TitleHeight + 46, Width - 48, 40));

            using (var pen = new Pen(Theme.Border))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && e.Y < TitleHeight)
            {
                NativeMethods.ReleaseCapture();
                NativeMethods.SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Close();
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Layout1(); }

        private void Layout1()
        {
            int pad = 24, w = ClientSize.Width - pad * 2;
            _closeX.Location = new Point(ClientSize.Width - 52, (TitleHeight - _closeX.Height) / 2);

            int y = TitleHeight + 100;
            _password.SetBounds(pad, y, w, 40);
            _status.SetBounds(pad, y + 46, w, 20);
            _save.Location = new Point(pad + (w - _save.Width) / 2, ClientSize.Height - _save.Height - 22);
        }
    }
}
