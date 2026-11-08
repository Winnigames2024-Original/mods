using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Экстренное обновление. Честно предупреждаем: через 30 секунд игра
    /// закроется и лаунчер обновится. Отмены нет — обновление обязательное,
    /// но человек видит отсчёт и может нажать «Обновить сейчас», не дожидаясь.
    /// </summary>
    public class EmergencyUpdateForm : Form
    {
        private const int TitleHeight = 44;
        private const int Seconds = 30;

        private readonly string _version;
        private readonly string _changelog;

        private readonly NareButton _now = new NareButton
        { Text = "ОБНОВИТЬ СЕЙЧАС", Style = BtnStyle.Accent, Size = new Size(240, 46), Radius = 12 };

        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);

        private readonly System.Windows.Forms.Timer _tick = new System.Windows.Forms.Timer { Interval = 1000 };
        private int _left = Seconds;
        private bool _started;

        public EmergencyUpdateForm(string version, string changelog)
        {
            _version = version ?? "";
            _changelog = string.IsNullOrEmpty(changelog) ? "Без описания." : changelog;

            Text = "Экстренное обновление";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 360);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(9.5f);
            DoubleBuffered = true;
            ShowInTaskbar = false;
            TopMost = true;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            _now.Click += (s, e) => Begin();
            Controls.Add(_now);
            Controls.Add(_status);

            _tick.Tick += (s, e) =>
            {
                _left--;
                if (_left <= 0) { _tick.Stop(); Begin(); }
                Invalidate();
            };

            Layout1();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _tick.Start();
        }

        private async void Begin()
        {
            if (_started) return;
            _started = true;
            _tick.Stop();

            _now.Enabled = false;
            _now.Invalidate();

            // игра закрывается первой, как и обещали
            if (GameLauncher.IsRunning)
            {
                Say("Закрываю Minecraft…");
                try { GameLauncher.Kill(); } catch { }
                await Task.Delay(800);
            }

            try
            {
                var info = await SelfUpdate.LatestAsync(CancellationToken.None);
                await SelfUpdate.ApplyAsync(info, Say, CancellationToken.None);

                Say("Готово, перезапускаюсь…");
                await Task.Delay(600);
                Application.Exit();
            }
            catch (Exception ex)
            {
                Say("Обновление не удалось: " + ex.Message);
                Log.Error("Экстренное обновление не применилось", ex);

                // не удалось — хотя бы откроем страницу загрузки и выйдем,
                // чтобы человек обновился руками
                try { System.Diagnostics.Process.Start("https://winnigames2024-original.github.io/NareLauncher/"); }
                catch { }

                _now.Text = "ЗАКРЫТЬ";
                _now.Enabled = true;
                _started = false;
                _now.Invalidate();
            }
        }

        private void Say(string text)
        {
            try
            {
                if (InvokeRequired) { BeginInvoke((Action)(() => Say(text))); return; }
                _status.Text = text;
            }
            catch { }
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
            Theme.FillRoundGradient(g, logo, 6, Theme.Warn, Theme.Bad, 35f);
            Theme.Text1(g, "!", Theme.F(11f, FontStyle.Bold), Color.White, logo,
                StringAlignment.Center, StringAlignment.Center);

            Theme.Text1(g, "Экстренное обновление " + _version, Theme.F(11f, FontStyle.Bold), Theme.Text,
                new Rectangle(48, 0, Width - 60, TitleHeight));

            int y = TitleHeight + 18;

            string warn = _started
                ? "Обновление началось…"
                : "Игра закроется через " + _left + " сек, затем лаунчер обновится.";
            Theme.Text1(g, warn, Theme.F(13f, FontStyle.Bold),
                _left <= 10 && !_started ? Theme.Bad : Theme.Text,
                new Rectangle(24, y, Width - 48, 26));

            Theme.Text1(g, "Что нового:", Theme.F(9.5f, FontStyle.Bold), Theme.Muted,
                new Rectangle(24, y + 36, Width - 48, 18));

            var box = new Rectangle(24, y + 58, Width - 48, 150);
            Theme.FillRound(g, box, 10, Theme.Panel);
            Theme.DrawRound(g, box, 10, Theme.Border);
            Theme.Text1(g, _changelog, Theme.F(9.2f), Theme.Text,
                Rectangle.Inflate(box, -12, -10), StringAlignment.Near, StringAlignment.Near);

            using (var pen = new Pen(Theme.Border))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Layout1();
        }

        private void Layout1()
        {
            int pad = 24;
            _status.SetBounds(pad, ClientSize.Height - 66, ClientSize.Width - pad * 2, 18);
            _now.Location = new Point((ClientSize.Width - _now.Width) / 2, ClientSize.Height - _now.Height - 20);
        }
    }
}
