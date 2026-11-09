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
        private readonly NareButton _login = new NareButton
        { Text = "ВОЙТИ", Style = BtnStyle.Accent, Icon = Ico.Check, Size = new Size(200, 46), Radius = 12 };
        private readonly NareButton _register = new NareButton
        { Text = "РЕГИСТРАЦИЯ", Style = BtnStyle.Ghost, Size = new Size(200, 46), Radius = 12 };
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
                Go(false);
            };
            _password.Inner.TextChanged += (s, e) =>
            {
                _status.Text = "";
                bool on = _password.Text.Length > 0 && !_busy;
                _login.Enabled = on; _register.Enabled = on;
                _login.Invalidate(); _register.Invalidate();
            };

            _login.Click += (s, e) => Go(false);
            _register.Click += (s, e) => Go(true);
            _closeX.Click += (s, e) => Close();

            Controls.Add(_password);
            Controls.Add(_login);
            Controls.Add(_register);
            Controls.Add(_closeX);
            Controls.Add(_status);

            _login.Enabled = false; _register.Enabled = false;
            Layout1();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try { _password.Inner.Focus(); } catch { }
        }

        private async void Go(bool register)
        {
            string password = _password.Text;
            if (password.Length < 4) { Warn("Пароль — от 4 символов."); return; }

            _busy = true;
            _login.Enabled = false; _register.Enabled = false;
            _login.Invalidate(); _register.Invalidate();
            _status.ForeColor = Theme.Muted;
            _status.Text = register ? "Регистрирую…" : "Вхожу…";

            NareAuth.LoginResult result = null;
            string problem = null;
            try
            {
                result = register
                    ? await NareAuth.RegisterAsync(_account.Name, password, CancellationToken.None)
                    : await NareAuth.LoginAsync(_account.Name, password, CancellationToken.None);
            }
            catch (Exception ex) { problem = ex.Message; }

            _busy = false;

            if (result == null)
            {
                string msg = problem ?? "нет связи";
                if (register && msg.Contains("занят"))
                    Warn("Ник уже зарегистрирован — нажмите «Войти».");
                else if (!register && msg.Contains("не подходят"))
                    Warn("Ник или пароль не подходят. Новый аккаунт — «Регистрация».");
                else
                    Warn(msg);
                bool on = _password.Text.Length > 0;
                _login.Enabled = on; _register.Enabled = on;
                _login.Invalidate(); _register.Invalidate();
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
            Theme.Text1(g, "Войдите, если аккаунт уже есть, или зарегистрируйте его. "
                + "Пароль защитит аккаунт и откроет «Вместе», монеты и чаты.",
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
            int by = ClientSize.Height - _login.Height - 22;
            _register.Location = new Point(pad, by);
            _login.Location = new Point(ClientSize.Width - pad - _login.Width, by);
        }
    }
}
