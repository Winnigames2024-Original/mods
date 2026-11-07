using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Своё окно ошибки вместо системного «ОК». Сверху — что случилось
    /// человеческими словами, ниже — что с этим делать, и только потом
    /// подробности для журнала.
    ///
    /// Если сорвалось соединение, а на машине работает VPN, появляется кнопка
    /// «Выключить VPN и повторить»: чаще всего виноват именно туннель.
    /// </summary>
    public class ErrorForm : Form
    {
        private readonly string _title;
        private readonly string _plain;
        private readonly string _details;
        private readonly bool _network;

        private readonly Action _retry;
        private VpnControl.Found _vpn;

        private readonly NareButton _retryButton =
            new NareButton { Text = "Повторить", Style = BtnStyle.Accent, Size = new Size(150, 40) };

        private readonly NareButton _vpnButton =
            new NareButton { Text = "Выключить VPN и повторить", Style = BtnStyle.Accent, Size = new Size(250, 40) };

        private readonly NareButton _log =
            new NareButton { Text = "Журнал", Style = BtnStyle.Ghost, Size = new Size(120, 40) };

        private readonly NareButton _copy =
            new NareButton { Text = "Скопировать", Style = BtnStyle.Ghost, Size = new Size(150, 40) };

        private readonly NareButton _close =
            new NareButton { Text = "Закрыть", Style = BtnStyle.Ghost, Size = new Size(120, 40) };

        private readonly NareButton _more =
            new NareButton { Text = "Подробности", Style = BtnStyle.Ghost, Size = new Size(150, 32) };

        private readonly TextBox _text = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None
        };

        private bool _open;

        public ErrorForm(string title, Exception ex, Action retry)
        {
            _title = string.IsNullOrEmpty(title) ? "Что-то пошло не так" : title;
            _details = Describe(ex);
            _network = IsNetwork(ex);
            _plain = Explain(ex, _network);
            _retry = retry;

            Text = "NareLauncher — ошибка";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = true;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(10f);
            ClientSize = new Size(620, 300);

            _text.BackColor = Color.FromArgb(14, 17, 24);
            _text.ForeColor = Theme.Muted;
            _text.Font = new Font(FontFamily.GenericMonospace, 8.5f);
            _text.Text = _details;
            _text.Visible = false;
            Controls.Add(_text);

            if (_network) _vpn = VpnControl.Detect();

            _retryButton.Visible = retry != null;
            _vpnButton.Visible = retry != null && _vpn != null;

            _retryButton.Click += delegate { Again(); };

            _vpnButton.Click += delegate
            {
                _vpnButton.Enabled = false;
                string said = VpnControl.TurnOff(_vpn);
                Log.Info(said);
                _vpn = null;
                Again();
            };

            _log.Click += delegate
            {
                try { Process.Start("explorer.exe", "/select,\"" + Paths.LogFile + "\""); }
                catch (Exception open) { Log.Warn("Журнал не открылся: " + open.Message); }
            };

            _copy.Click += delegate
            {
                try { Clipboard.SetText(_title + Environment.NewLine + Environment.NewLine + _details); }
                catch { }
            };

            _more.Click += delegate
            {
                _open = !_open;
                _more.Text = _open ? "Свернуть подробности" : "Подробности";
                Layout1();
            };

            _close.Click += delegate { Close(); };

            Controls.Add(_retryButton);
            Controls.Add(_vpnButton);
            Controls.Add(_log);
            Controls.Add(_copy);
            Controls.Add(_close);
            Controls.Add(_more);

            Layout1();
        }

        private void Again()
        {
            var again = _retry;
            Close();
            if (again != null)
            {
                try { again(); }
                catch (Exception ex) { Log.Error("Повтор не удался", ex); }
            }
        }

        // ------------------------------------------------------------ вёрстка

        private void Layout1()
        {
            int pad = 24;
            int w = 620 - pad * 2;

            int y = 150;
            _more.Location = new Point(pad, y);

            y += 40;
            _text.SetBounds(pad, y, w, 150);
            _text.Visible = _open;

            int bottom = _open ? y + 162 : y;

            ClientSize = new Size(620, bottom + 58);

            int bx = 620 - pad;
            bx -= _close.Width; _close.Location = new Point(bx, bottom + 6);
            bx -= _copy.Width + 8; _copy.Location = new Point(bx, bottom + 6);
            bx -= _log.Width + 8; _log.Location = new Point(bx, bottom + 6);

            if (_vpnButton.Visible)
            {
                _vpnButton.Location = new Point(pad, bottom + 6);
                _retryButton.Visible = false;
            }
            else if (_retryButton.Visible)
                _retryButton.Location = new Point(pad, bottom + 6);

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.Smooth(g);

            int pad = 24;
            int w = ClientSize.Width - pad * 2;

            // красная полоса сверху — сразу видно, что это не обычное окно
            using (var brush = new SolidBrush(Color.FromArgb(70, Theme.Bad)))
                g.FillRectangle(brush, 0, 0, ClientSize.Width, 4);

            var mark = new Rectangle(pad, 22, 34, 34);
            Theme.FillRound(g, mark, 10, Color.FromArgb(55, Theme.Bad));
            Theme.Text1(g, "!", Theme.F(16f, FontStyle.Bold), Theme.Bad, mark,
                StringAlignment.Center, StringAlignment.Center);

            Theme.Text1(g, _title, Theme.F(14f, FontStyle.Bold), Theme.Text,
                new Rectangle(mark.Right + 14, 20, w - 48, 26));

            Theme.Text1(g, _plain, Theme.F(9.8f), Theme.Muted,
                new Rectangle(mark.Right + 14, 48, w - 48, 72), StringAlignment.Near, StringAlignment.Near);

            if (_vpnButton.Visible)
                Theme.Text1(g, "Похоже, работает " + (_vpn == null ? "VPN" : _vpn.Title)
                               + " — он часто и мешает достучаться до сервера.",
                    Theme.F(9f), Theme.Warn, new Rectangle(pad, 124, w, 20));
        }

        // -------------------------------------------------------- объяснение

        /// <summary>Сетевая ли это беда — от этого зависит, предлагать ли VPN.</summary>
        private static bool IsNetwork(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (e is SocketException) return true;
                if (e is WebException) return true;
                if (e is System.Net.Http.HttpRequestException) return true;
                if (e is TimeoutException) return true;

                string text = (e.Message ?? "").ToLowerInvariant();
                if (text.Contains("соединен") || text.Contains("подключ") ||
                    text.Contains("connection") || text.Contains("timed out") ||
                    text.Contains("timeout") || text.Contains("host") ||
                    text.Contains("network") || text.Contains("сеть") ||
                    text.Contains("не удалось разрешить") || text.Contains("ssl"))
                    return true;
            }

            return false;
        }

        /// <summary>Коротко и по-человечески, без слов из стека вызовов.</summary>
        private static string Explain(Exception ex, bool network)
        {
            if (ex == null) return "Подробностей нет — посмотрите журнал.";

            for (var e = ex; e != null; e = e.InnerException)
            {
                if (e is UnauthorizedAccessException)
                    return "Нет доступа к файлу или папке. Закройте игру, проверьте, "
                         + "не держит ли файл антивирус, и попробуйте снова.";

                if (e is FileNotFoundException || e is DirectoryNotFoundException)
                    return "Нужный файл не нашёлся. Чаще всего помогает переустановить версию "
                         + "на вкладке «Версии».";

                if (e is IOException && (e.Message ?? "").ToLowerInvariant().Contains("использ"))
                    return "Файл занят другой программой. Закройте игру и повторите.";
            }

            if (network)
                return "Не получилось связаться с сервером. Проверьте интернет; "
                     + "если включён VPN, попробуйте выключить его и повторить.";

            return ex.Message ?? "Подробностей нет — посмотрите журнал.";
        }

        private static string Describe(Exception ex)
        {
            if (ex == null) return "(без подробностей)";

            var text = new System.Text.StringBuilder();
            for (var e = ex; e != null; e = e.InnerException)
            {
                text.AppendLine(e.GetType().Name + ": " + e.Message);
                if (!string.IsNullOrEmpty(e.StackTrace)) text.AppendLine(e.StackTrace);
                text.AppendLine();
            }

            return text.ToString().TrimEnd();
        }
    }
}
