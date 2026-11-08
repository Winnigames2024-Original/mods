using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Первое окно, которое видит человек, пришедший с сайта: ник и кнопка.
    /// Больше ничего спрашивать не надо — всё остальное лаунчер сделает сам.
    /// </summary>
    public class WelcomeForm : Form
    {
        private const int TitleHeight = 44;

        private readonly NareTextBox _nick = new NareTextBox
        {
            Placeholder = "Ник: 3–16 символов, латиница, цифры и _",
            Icon = Ico.Account
        };

        private readonly NareTextBox _password = new NareTextBox
        {
            Placeholder = "Пароль аккаунта (от 4 символов)"
        };

        private readonly NareButton _go = new NareButton
        {
            Text = "НАЧАТЬ ИГРАТЬ",
            Style = BtnStyle.Accent,
            Icon = Ico.Play,
            Size = new Size(240, 46),
            Radius = 12
        };

        private readonly NareButton _closeX = new NareButton
        { Text = "", Style = BtnStyle.Ghost, Icon = Ico.Close, Size = new Size(40, 28), Radius = 8 };

        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Warn);

        private readonly string _what;

        /// <summary>Созданный аккаунт или null, если окно закрыли.</summary>
        public Account MadeAccount;

        /// <param name="what">Что собираемся запускать — показываем строкой, чтобы было понятно, ради чего ник.</param>
        public WelcomeForm(string what)
        {
            _what = what ?? "";

            Text = "Добро пожаловать";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 338);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(9.5f);
            DoubleBuffered = true;
            KeyPreview = true;
            ShowInTaskbar = false;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            _go.Click += (s, e) => Accept();
            _closeX.Click += (s, e) => Close();

            _nick.Inner.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                Accept();
            };
            _nick.Inner.TextChanged += (s, e) =>
            {
                _status.Text = "";
                _go.Enabled = Ready();
                _go.Invalidate();
            };

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
                _go.Enabled = Ready();
                _go.Invalidate();
            };

            Controls.Add(_nick);
            Controls.Add(_password);
            Controls.Add(_go);
            Controls.Add(_closeX);
            Controls.Add(_status);

            _go.Enabled = false;
            Layout1();
        }

        private bool Ready()
        {
            return _nick.Text.Trim().Length > 0 && _password.Text.Length > 0 && !_busy;
        }

        private bool _busy;

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try { _nick.Inner.Focus(); } catch { }
        }

        // ------------------------------------------------------------ создание

        private async void Accept()
        {
            string nick = _nick.Text.Trim();
            string password = _password.Text;

            if (!AccountStore.IsValidNick(nick))
            {
                Warn("Ник должен быть 3–16 символов: латиница, цифры и _");
                return;
            }
            if (password.Length < 4)
            {
                Warn("Пароль — от 4 символов. Он защищает аккаунт на сервере.");
                return;
            }
            if (AccountStore.NickTaken(nick))
            {
                Warn("Такой ник уже есть в списке аккаунтов на этом компьютере.");
                return;
            }

            // правило: несколько аккаунтов NareLauncher разрешены только если
            // среди них есть Winnigames2024
            if (!AllowMore(nick))
            {
                Warn("На этом компьютере может быть только один аккаунт. "
                   + "Несколько — только если есть аккаунт Winnigames2024.");
                return;
            }

            _busy = true;
            _go.Enabled = false;
            _go.Invalidate();
            Warn("Проверяю на сервере…");
            _status.ForeColor = Theme.Muted;

            NareAuth.LoginResult result = null;
            string problem = null;

            try
            {
                // новый ник — регистрируем; занят — пробуем войти этим паролем
                try
                {
                    result = await NareAuth.RegisterAsync(nick, password, CancellationToken.None);
                }
                catch
                {
                    result = await NareAuth.LoginAsync(nick, password, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                problem = ex.Message;
            }

            _busy = false;

            if (result == null)
            {
                Warn(problem != null && problem.Contains("не подходят")
                    ? "Этот ник занят, а пароль не подходит. Выберите другой ник или вспомните пароль."
                    : "Сервер недоступен: " + (problem ?? "нет связи") + ". Попробуйте позже.");
                _go.Enabled = Ready();
                _go.Invalidate();
                return;
            }

            try
            {
                MadeAccount = AccountStore.AddOffline(nick);
                MadeAccount.Token = result.Token;
                MadeAccount.Admin = result.Admin;
                AccountStore.Save();
            }
            catch (Exception ex)
            {
                Warn("Аккаунт на сервере есть, но локально не сохранился: " + ex.Message);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>
        /// Можно ли завести ещё один аккаунт NareLauncher. Разрешаем, если
        /// аккаунтов ещё нет, либо среди них есть Winnigames2024, либо как раз
        /// его и добавляют.
        /// </summary>
        public static bool AllowMore(string adding)
        {
            if (AccountStore.Accounts.Count == 0) return true;
            if (string.Equals(adding, "Winnigames2024", StringComparison.OrdinalIgnoreCase)) return true;
            foreach (var a in AccountStore.Accounts)
                if (string.Equals(a.Name, "Winnigames2024", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void Warn(string text)
        {
            _status.Text = text;
            _status.ForeColor = Theme.Warn;
        }

        // -------------------------------------------------------------- вид

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.Smooth(g);

            var bar = new Rectangle(0, 0, Width, TitleHeight);
            using (var b = new SolidBrush(Theme.Sidebar))
                g.FillRectangle(b, bar);
            using (var pen = new Pen(Color.FromArgb(28, 255, 255, 255)))
                g.DrawLine(pen, 0, TitleHeight - 1, Width, TitleHeight - 1);

            var logo = new Rectangle(18, (TitleHeight - 20) / 2, 20, 20);
            Theme.FillRoundGradient(g, logo, 6, Theme.Accent, Theme.Accent2, 35f);
            Theme.Text1(g, "N", Theme.F(10f, FontStyle.Bold), Color.White, logo,
                StringAlignment.Center, StringAlignment.Center);

            Theme.Text1(g, "NareLauncher", Theme.F(11f, FontStyle.Bold), Theme.Text,
                new Rectangle(48, 0, 300, TitleHeight));

            Theme.Text1(g, "Как вас зовут в игре?", Theme.F(15f, FontStyle.Bold), Theme.Text,
                new Rectangle(24, TitleHeight + 18, Width - 48, 26));

            string line = string.IsNullOrEmpty(_what)
                ? "Ник нужен, чтобы запустить игру. Потом его можно поменять во вкладке «Аккаунты»."
                : "Ник нужен, чтобы запустить " + _what + ". Потом его можно поменять во вкладке «Аккаунты».";

            Theme.Text1(g, line, Theme.F(9f), Theme.Muted,
                new Rectangle(24, TitleHeight + 46, Width - 48, 36));

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

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Layout1();
        }

        private void Layout1()
        {
            int pad = 24;
            int w = ClientSize.Width - pad * 2;

            _closeX.Location = new Point(ClientSize.Width - 52, (TitleHeight - _closeX.Height) / 2);

            int y = TitleHeight + 86;
            _nick.SetBounds(pad, y, w, 40);
            _password.SetBounds(pad, y + 48, w, 40);

            _status.SetBounds(pad, y + 94, w, 20);

            _go.Location = new Point(pad + (w - _go.Width) / 2, ClientSize.Height - _go.Height - 22);
        }
    }
}
