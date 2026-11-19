using GEO_DASH.Audio;
using GEO_DASH.Net;
using GEO_DASH.Update;

namespace GEO_DASH.UI
{
    public class SettingsForm : GdForm
    {
        readonly TextBox _host, _port, _site;
        readonly TrackBar _music, _sfx;
        readonly CheckBox _fps, _hit, _auto, _offline, _full;
        readonly ComboBox _fpsCap;
        string _msg = "";

        public SettingsForm()
        {
            Text = "GEO DASH — Настройки";
            ClientSize = new Size(820, 640);
            BgA = Color.FromArgb(28, 30, 70);
            var s = AppConfig.Settings;

            TextBox Tb(int y, string val, int w = 240)
            {
                var t = new TextBox
                {
                    Left = 280, Top = y, Width = w, Text = val, Font = Gd.Bold(11f),
                    BackColor = Color.FromArgb(20, 18, 48), ForeColor = Gd.Paper, BorderStyle = BorderStyle.FixedSingle
                };
                Controls.Add(t);
                return t;
            }
            CheckBox Ck(int y, string t, bool v)
            {
                var c = new CheckBox
                {
                    Left = 280, Top = y, Width = 420, Text = t, Checked = v,
                    ForeColor = Gd.Paper, BackColor = Color.Transparent, Font = Gd.Bold(10f), FlatStyle = FlatStyle.Flat
                };
                Controls.Add(c);
                return c;
            }
            TrackBar Tr(int y, double v)
            {
                var t = new TrackBar
                {
                    Left = 276, Top = y, Width = 280, Minimum = 0, Maximum = 100,
                    Value = (int)Math.Clamp(v * 100, 0, 100), TickFrequency = 10, BackColor = Gd.Deep
                };
                Controls.Add(t);
                return t;
            }

            _host = Tb(110, s.ServerHost);
            _port = Tb(150, s.ServerPort.ToString(), 120);
            _site = Tb(190, s.SiteUrl, 380);
            _music = Tr(230, s.MusicVolume);
            _sfx = Tr(286, s.SfxVolume);

            _fpsCap = new ComboBox
            {
                Left = 280, Top = 344, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(20, 18, 48), ForeColor = Gd.Paper, FlatStyle = FlatStyle.Flat
            };
            _fpsCap.Items.AddRange(new object[] { "60", "75", "120", "144", "240" });
            _fpsCap.SelectedItem = s.Fps.ToString();
            if (_fpsCap.SelectedIndex < 0) _fpsCap.SelectedIndex = 0;
            Controls.Add(_fpsCap);

            _fps = Ck(380, "показывать FPS", s.ShowFps);
            _hit = Ck(408, "показывать хитбоксы", s.ShowHitboxes);
            _auto = Ck(436, "автообновление игры с GitHub", s.AutoUpdate);
            _offline = Ck(464, "автоматически качать офлайн-уровни и музыку в assets", s.OfflineAutoDownload);
            _full = Ck(492, "запускать игру в полноэкранном режиме", s.Fullscreen);

            Add("СОХРАНИТЬ", GdIcon.Save, 280, 530, 200, 50, Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100),
                (s2, e) => Save());
            Add("ПРОВЕРИТЬ ОБНОВЛЕНИЕ", GdIcon.Download, 492, 530, 240, 50, Color.FromArgb(120, 220, 255), Color.FromArgb(20, 110, 180),
                async (s2, e) => await CheckUpdate());
            Add("НАЗАД", GdIcon.Back, 40, 530, 200, 50, Color.FromArgb(160, 160, 190), Color.FromArgb(70, 70, 110),
                (s2, e) => Close());
        }

        void Save()
        {
            var s = AppConfig.Settings;
            s.ServerHost = _host.Text.Trim();
            s.ServerPort = int.TryParse(_port.Text.Trim(), out var p) ? p : 37534;
            s.SiteUrl = _site.Text.Trim();
            s.MusicVolume = _music.Value / 100.0;
            s.SfxVolume = _sfx.Value / 100.0;
            s.Fps = int.TryParse(_fpsCap.Text, out var f) ? f : 60;
            s.ShowFps = _fps.Checked;
            s.ShowHitboxes = _hit.Checked;
            s.AutoUpdate = _auto.Checked;
            s.OfflineAutoDownload = _offline.Checked;
            s.Fullscreen = _full.Checked;
            AppConfig.Save();
            MusicPlayer.SetVolume(s.MusicVolume);
            Sfx.Volume = (float)s.SfxVolume;
            _msg = "Сохранено. Папка данных: " + AppConfig.DataDir;
            Invalidate();
        }

        async Task CheckUpdate()
        {
            _msg = "Проверяю GitHub…"; Invalidate();
            var info = await Updater.CheckAsync();
            if (info == null) { _msg = "Не удалось получить update.json"; Invalidate(); return; }
            if (!Updater.IsNewer(info.version, Updater.CurrentVersion))
            {
                _msg = $"У тебя последняя версия ({Updater.CurrentVersion})";
                Invalidate(); return;
            }
            if (MessageBox.Show($"Есть версия {info.version}. Обновить?", "Обновление",
                MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                if (await Updater.DownloadAndApplyAsync(info, new Progress<string>(x => { _msg = x; Invalidate(); })))
                    Application.Exit();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Header(g, "НАСТРОЙКИ", "данные игры: " + AppConfig.DataDir);
            using var f = Gd.Bold(11f);
            void L(string t, int y) => Gd.Outlined(g, t, f, new RectangleF(40, y, 230, 24), Gd.Paper, StringAlignment.Near, thick: 2.2f);
            L("Сервер (IP)", 112);
            L("Порт API", 152);
            L("Сайт", 192);
            L("Громкость музыки", 236);
            L("Громкость звуков", 292);
            L("Лимит FPS", 346);
            if (!string.IsNullOrEmpty(_msg))
                Gd.Outlined(g, _msg, f, new RectangleF(40, 596, ClientSize.Width - 80, 24), Gd.Gold, StringAlignment.Near, thick: 2.2f);
        }
    }
}
