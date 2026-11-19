using GEO_DASH.Audio;
using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    /// <summary>Музыка по ID: встроенные треки + любой трек Newgrounds по номеру.</summary>
    public class MusicForm : GdForm
    {
        public int SongId { get; private set; }
        public string SongName { get; private set; } = "";
        public string SongArtist { get; private set; } = "";

        readonly NumericUpDown _id;
        readonly ListBox _builtin;
        readonly ListBox _site;
        List<SongDto> _siteSongs = new();
        string _msg = "";

        public MusicForm(int current)
        {
            SongId = current;
            Text = "GEO DASH — Музыка";
            ClientSize = new Size(760, 560);
            BgA = Color.FromArgb(145, 50, 205); BgId = 12; GroundId = 6;

            _id = new NumericUpDown
            {
                Left = 40, Top = 150, Width = 220, Minimum = -99, Maximum = 99999999, Value = current,
                Font = Gd.Bold(14f), BackColor = Color.FromArgb(22, 20, 52), ForeColor = Gd.Paper,
                BorderStyle = BorderStyle.FixedSingle
            };
            Host(_id);

            _site = new ListBox
            {
                Left = 400, Top = 340, Width = 320, Height = 86, Font = Gd.Bold(10f),
                BackColor = Color.FromArgb(20, 18, 48), ForeColor = Gd.Paper, BorderStyle = BorderStyle.FixedSingle
            };
            _site.SelectedIndexChanged += (s, e) =>
            {
                if (_site.SelectedIndex < 0 || _site.SelectedIndex >= _siteSongs.Count) return;
                var t = _siteSongs[_site.SelectedIndex];
                _id.Value = Math.Clamp(t.Id, _id.Minimum, _id.Maximum);
                SongName = t.Name; SongArtist = t.Artist;
                _msg = $"С сайта: #{t.Id} {t.Name}";
                Content.Invalidate();
            };
            Host(_site);

            _builtin = new ListBox
            {
                Left = 400, Top = 150, Width = 320, Height = 180, Font = Gd.Bold(11f),
                BackColor = Color.FromArgb(20, 18, 48), ForeColor = Gd.Paper, BorderStyle = BorderStyle.FixedSingle
            };
            foreach (var t in Soundtrack.Tracks) _builtin.Items.Add($"{t.Id}  {t.Name} — {t.Artist}");
            _builtin.SelectedIndexChanged += (s, e) =>
            {
                if (_builtin.SelectedIndex >= 0)
                {
                    var t = Soundtrack.Tracks[_builtin.SelectedIndex];
                    _id.Value = t.Id;
                    SongName = t.Name; SongArtist = t.Artist;
                }
            };
            Host(_builtin);

            Add("ПРОСЛУШАТЬ", GdIcon.Play, 40, 200, 220, 50, Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100),
                async (s, e) => await Preview());
            Add("СКАЧАТЬ В ASSETS", GdIcon.Download, 40, 260, 220, 50, Color.FromArgb(120, 220, 255), Color.FromArgb(20, 110, 180),
                async (s, e) => await Download());
            Add("СТОП", GdIcon.Cross, 40, 320, 220, 46, Color.FromArgb(180, 150, 200), Color.FromArgb(90, 60, 140),
                (s, e) => MusicPlayer.Stop());
            Add("ЗАЛИТЬ СВОЙ MP3", GdIcon.Upload, 40, 372, 220, 46,
                Color.FromArgb(255, 150, 215), Color.FromArgb(160, 40, 120), async (s, e) => await UploadOwn());

            Add("ВЫБРАТЬ", GdIcon.Check, 400, 440, 180, 52, Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100),
                (s, e) => { SongId = (int)_id.Value; DialogResult = DialogResult.OK; Close(); });
            Add("ОТМЕНА", GdIcon.Back, 590, 440, 130, 52, Color.FromArgb(160, 160, 190), Color.FromArgb(70, 70, 110),
                (s, e) => { DialogResult = DialogResult.Cancel; Close(); });

            Load += async (s, e) => await LoadSite();
            FormClosed += (s, e) => MusicPlayer.Stop();
        }

        /// <summary>Треки, залитые на сайт игроками.</summary>
        async Task LoadSite()
        {
            var r = await ApiClient.SongsAsync();
            if (!r.Ok || r.Data == null) { _msg = "Список с сайта не загрузился"; Content.Invalidate(); return; }
            _siteSongs = r.Data;
            _site.Items.Clear();
            foreach (var t in _siteSongs) _site.Items.Add($"#{t.Id}  {t.Name}" + (string.IsNullOrEmpty(t.Artist) ? "" : " — " + t.Artist));
            Content.Invalidate();
        }

        /// <summary>Залить свой mp3 на сервер — он получит ID и станет доступен всем.</summary>
        async Task UploadOwn()
        {
            using var ofd = new OpenFileDialog { Filter = "Музыка (*.mp3)|*.mp3", Title = "Выбери свой трек" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;
            if (!AppConfig.LoggedIn) { _msg = "Нужен вход в аккаунт"; Content.Invalidate(); return; }

            _msg = "Заливаю трек…"; Content.Invalidate();
            var bytes = await File.ReadAllBytesAsync(ofd.FileName);
            var name = Path.GetFileNameWithoutExtension(ofd.FileName);
            var r = await ApiClient.UploadSongAsync(bytes, name);
            if (!r.Ok) { _msg = "Не вышло: " + r.Error; Content.Invalidate(); return; }

            int id = r.Data.TryGetProperty("id", out var v) ? v.GetInt32() : 0;
            if (id > 0)
            {
                _id.Value = Math.Clamp(id, _id.Minimum, _id.Maximum);
                SongName = name;
                SongLibrary.Remember(id, name, AppConfig.Settings.UserName);
                _msg = $"Готово! Твой трек #{id} — уже выбран для уровня";
            }
            await LoadSite();
        }

        async Task Preview()
        {
            int id = (int)_id.Value;
            _msg = "Готовлю трек…"; Content.Invalidate();
            bool ok = await MusicPlayer.PlayAsync(id, 0, 1, true);
            _msg = ok ? "Играет трек " + id : "Не получилось: " + MusicPlayer.LastError;
            Content.Invalidate();
        }

        async Task Download()
        {
            int id = (int)_id.Value;
            _msg = "Скачиваю…"; Content.Invalidate();
            var path = await MusicPlayer.EnsureAsync(id, new Progress<string>(s => { _msg = s; Content.Invalidate(); }));
            if (path != null)
            {
                var info = await ApiClient.SongInfoAsync(id);
                if (info.Ok && info.Data != null)
                {
                    SongName = info.Data.Name; SongArtist = info.Data.Artist;
                }
                SongLibrary.Remember(id, SongName, SongArtist);
                _msg = "Готово: " + path;
            }
            else _msg = "Не вышло: " + MusicPlayer.LastError;
            Content.Invalidate();
        }

        protected override void OnRender(Graphics g)
        {
                        Header(g, "МУЗЫКА ПО ID", "любой трек Newgrounds по номеру + встроенный саундтрек");
            using var f = Gd.Bold(11f);
            Gd.Outlined(g, "ID трека", f, new RectangleF(40, 124, 300, 22), Gd.Paper, StringAlignment.Near, thick: 2.2f);
            Gd.Outlined(g, "Встроенные треки (работают офлайн)", f, new RectangleF(400, 124, 340, 22), Gd.Paper, StringAlignment.Near, thick: 2.2f);
            Gd.Outlined(g, "Треки с сайта (их заливают игроки)", f, new RectangleF(400, 316, 340, 22), Gd.Paper, StringAlignment.Near, thick: 2.2f);
            if (!string.IsNullOrEmpty(SongName))
                Gd.Outlined(g, SongName + (string.IsNullOrEmpty(SongArtist) ? "" : " — " + SongArtist), f,
                    new RectangleF(400, 340, 340, 22), Gd.Neon, StringAlignment.Near, thick: 2.2f);
            Gd.Outlined(g, "песни сохраняются в " + AppConfig.MusicDir, f,
                new RectangleF(40, 382, 680, 22), Color.FromArgb(200, 190, 210, 255), StringAlignment.Near, thick: 2f);
            if (!string.IsNullOrEmpty(_msg))
                Gd.Outlined(g, _msg, f, new RectangleF(40, 410, 680, 22), Gd.Gold, StringAlignment.Near, thick: 2.2f);
        }
    }
}
