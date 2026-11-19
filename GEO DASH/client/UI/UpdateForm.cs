using GEO_DASH.Update;

namespace GEO_DASH.UI
{
    /// <summary>Диалог автообновления: показывается, когда игра онлайн и на GitHub есть версия новее.</summary>
    public class UpdateForm : GdForm
    {
        readonly Updater.Info _info;
        string _status = "";
        double _bar;
        bool _busy;
        GdButton _bUpdate, _bLater, _bSkip;

        public UpdateForm(Updater.Info info)
        {
            _info = info;
            Text = "GEO DASH — Обновление";
            ClientSize = new Size(720, 460);
            BgA = Color.FromArgb(18, 46, 86);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;

            _bUpdate = Add("ОБНОВИТЬ СЕЙЧАС", GdIcon.Download, 40, 360, 300, 60,
                Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100), async (s, e) => await Go());
            _bLater = Add("ПОЗЖЕ", GdIcon.Back, 356, 360, 160, 60,
                Color.FromArgb(160, 160, 190), Color.FromArgb(70, 70, 110), (s, e) => Close());
            _bSkip = Add("НЕ ПРЕДЛАГАТЬ", GdIcon.Cross, 532, 360, 148, 60,
                Color.FromArgb(200, 120, 120), Color.FromArgb(120, 40, 50),
                (s, e) =>
                {
                    AppConfig.Settings.AutoUpdate = false;
                    AppConfig.Save();
                    Close();
                });
        }

        async Task Go()
        {
            if (_busy) return;
            _busy = true;
            _bUpdate.Visible = _bLater.Visible = _bSkip.Visible = false;
            Status("Скачиваю обновление с GitHub…");

            var prog = new Progress<string>(s => Status(s));
            var timer = new System.Windows.Forms.Timer { Interval = 60 };
            timer.Tick += (a, b) => { _bar = Math.Min(0.95, _bar + 0.012); Content.Invalidate(); };
            timer.Start();

            bool ok = await Updater.DownloadAndApplyAsync(_info, prog);
            timer.Stop();
            _bar = 1;

            if (ok)
            {
                Status("Готово. Игра перезапустится с новой версией…");
                await Task.Delay(900);
                Application.Exit();
            }
            else
            {
                Status("Не вышло обновиться. Попробуй позже или скачай с сайта.");
                _bUpdate.Visible = _bLater.Visible = _bSkip.Visible = true;
                _busy = false;
            }
        }

        void Status(string s)
        {
            if (InvokeRequired) { try { BeginInvoke(() => Status(s)); } catch { } return; }
            _status = s;
            Content.Invalidate();
        }

        protected override void OnRender(Graphics g)
        {
                        Header(g, "ЕСТЬ ОБНОВЛЕНИЕ", "сервер онлайн — можно обновиться прямо сейчас");

            var card = new RectangleF(40, 110, ClientSize.Width - 80, 220);
            Gd.Plate(g, card, Color.FromArgb(38, 48, 96), Color.FromArgb(18, 22, 54), 18f, 4f);

            using (var f = Gd.Title(18f))
                Gd.Outlined(g, $"{Updater.CurrentVersion}   →   {_info.version}", f,
                    new RectangleF(card.X + 20, card.Y + 14, card.Width - 40, 34), Gd.Lime, StringAlignment.Near, thick: 3f);

            using (var f = Gd.Bold(10.5f))
            {
                string notes = string.IsNullOrWhiteSpace(_info.notes) ? "Исправления и улучшения." : _info.notes;
                var r = new RectangleF(card.X + 20, card.Y + 58, card.Width - 40, 110);
                using var br = new SolidBrush(Color.FromArgb(230, 215, 225, 255));
                using var sf = new StringFormat { Trimming = StringTrimming.Word };
                g.DrawString(notes, f, br, r, sf);

                if (!string.IsNullOrEmpty(_info.date))
                    Gd.Outlined(g, "дата сборки: " + _info.date, f,
                        new RectangleF(card.X + 20, card.Bottom - 54, card.Width - 40, 22),
                        Color.FromArgb(200, 180, 200, 240), StringAlignment.Near, thick: 2f);
            }

            if (_busy || _bar > 0)
            {
                var bar = new RectangleF(card.X + 20, card.Bottom - 28, card.Width - 40, 18);
                using (var br = new SolidBrush(Color.FromArgb(150, 10, 8, 26))) g.FillRectangle(br, bar);
                using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(bar, Gd.Neon, Gd.Lime,
                           System.Drawing.Drawing2D.LinearGradientMode.Horizontal))
                    g.FillRectangle(br, new RectangleF(bar.X, bar.Y, (float)(bar.Width * _bar), bar.Height));
                using (var pen = new Pen(Gd.Ink, 2.5f)) g.DrawRectangle(pen, bar.X, bar.Y, bar.Width, bar.Height);
            }

            if (!string.IsNullOrEmpty(_status))
                using (var f = Gd.Bold(10f))
                    Gd.Outlined(g, _status, f, new RectangleF(40, 336, ClientSize.Width - 80, 22),
                        Gd.Gold, StringAlignment.Near, thick: 2.2f);
        }
    }
}
