using GEO_DASH.Audio;
using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    /// <summary>МОИ ПЕСНИ — что уже скачано в assets/songs: слушаем, качаем новое, удаляем.</summary>
    public class SongsForm : GdForm
    {
        readonly ListView _list;
        List<SongLibrary.Entry> _data = new();
        string _msg = "";

        public SongsForm()
        {
            Text = "GEO DASH — Мои песни";
            ClientSize = new Size(960, 660);
            BgA = Color.FromArgb(200, 60, 170); BgId = 12; GroundId = 6;

            _list = new ListView
            {
                Left = 30, Top = 120, Width = ClientSize.Width - 60, Height = 380,
                View = View.Details, FullRowSelect = true, HideSelection = false,
                BackColor = Color.FromArgb(20, 16, 46), ForeColor = Gd.Paper,
                BorderStyle = BorderStyle.FixedSingle, Font = Gd.Bold(10.5f)
            };
            _list.Columns.Add("ID", 110);
            _list.Columns.Add("Название", 330);
            _list.Columns.Add("Исполнитель", 220);
            _list.Columns.Add("Размер", 110);
            _list.Columns.Add("Тип", 110);
            _list.DoubleClick += async (s, e) => await Play();
            Host(_list);

            int y = 520, bw = 170, gap = 12, x = 30;
            Add("ПРОСЛУШАТЬ", GdIcon.Play, x, y, bw, 52, Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100),
                async (s, e) => await Play());
            Add("СТОП", GdIcon.Cross, x += bw + gap, y, 120, 52, Color.FromArgb(180, 150, 210), Color.FromArgb(90, 60, 145),
                (s, e) => MusicPlayer.Stop());
            Add("СКАЧАТЬ ПО ID", GdIcon.Download, x += 120 + gap, y, bw + 20, 52,
                Color.FromArgb(120, 200, 255), Color.FromArgb(25, 95, 185), async (s, e) => await DownloadNew());
            Add("УДАЛИТЬ", GdIcon.Trash, x += bw + 20 + gap, y, bw - 20, 52,
                Color.FromArgb(240, 120, 120), Color.FromArgb(150, 40, 50), (s, e) => Remove());
            Add("ОБНОВИТЬ", GdIcon.Refresh, x += bw - 20 + gap, y, bw - 20, 52,
                Color.FromArgb(150, 170, 220), Color.FromArgb(60, 70, 140), (s, e) => Reload());

            Add("НАЗАД", GdIcon.Back, 30, ClientSize.Height - 70, 160, 50,
                Color.FromArgb(170, 170, 200), Color.FromArgb(70, 70, 110), (s, e) => Close());

            Load += (s, e) => Reload();
            FormClosed += (s, e) => MusicPlayer.Stop();
        }

        SongLibrary.Entry Sel => _list.SelectedIndices.Count > 0 && _list.SelectedIndices[0] < _data.Count
            ? _data[_list.SelectedIndices[0]] : null;

        void Reload()
        {
            _data = SongLibrary.Scan();
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var s in _data)
            {
                var it = new ListViewItem(s.BuiltIn ? s.Id.ToString() : "#" + s.Id);
                it.SubItems.Add(s.Name);
                it.SubItems.Add(string.IsNullOrEmpty(s.Artist) ? "—" : s.Artist);
                it.SubItems.Add(s.SizeText);
                it.SubItems.Add(s.BuiltIn ? "встроенная" : "Newgrounds");
                if (s.BuiltIn) it.ForeColor = Gd.Lime;
                _list.Items.Add(it);
            }
            _list.EndUpdate();
            if (_list.Items.Count > 0 && _list.SelectedIndices.Count == 0) _list.Items[0].Selected = true;
            Content.Invalidate();
        }

        async Task Play()
        {
            var s = Sel;
            if (s == null) return;
            _msg = "Играет: " + s.Name; Content.Invalidate();
            await MusicPlayer.PlayAsync(s.Id, 0, 1, true);
        }

        async Task DownloadNew()
        {
            string txt = GdDialog.Prompt(this, "Скачать песню", "ID трека на Newgrounds:", "");
            if (string.IsNullOrWhiteSpace(txt) || !int.TryParse(txt.Trim(), out int id) || id <= 0) return;

            _msg = "Скачиваю трек #" + id + "…"; Content.Invalidate();
            var path = await MusicPlayer.EnsureAsync(id, new Progress<string>(m => { _msg = m; Content.Invalidate(); }));
            if (path == null) { _msg = "Не вышло: " + MusicPlayer.LastError; Content.Invalidate(); return; }

            var info = await ApiClient.SongInfoAsync(id);
            if (info.Ok && info.Data != null) SongLibrary.Remember(id, info.Data.Name, info.Data.Artist);
            _msg = "Трек #" + id + " в папке songs";
            Reload();
        }

        void Remove()
        {
            var s = Sel;
            if (s == null) return;
            if (s.BuiltIn)
            {
                _msg = "Встроенные треки не удаляются — они нужны основным уровням";
                Content.Invalidate();
                return;
            }
            if (!GdDialog.Confirm(this, "Мои песни", $"Удалить «{s.Name}»?")) return;
            MusicPlayer.Stop();
            try { File.Delete(s.Path); } catch (Exception ex) { _msg = ex.Message; }
            Reload();
        }

        protected override void OnRender(Graphics g)
        {
            Header(g, "МОИ ПЕСНИ", "скачанное лежит в " + AppConfig.MusicDir);
            using var f = Gd.Bold(10.5f);
            long total = 0;
            foreach (var s in _data) total += s.Size;
            Gd.Outlined(g, $"всего: {_data.Count}   ·   {(total / 1024.0 / 1024.0):0.0} МБ", f,
                new RectangleF(30, 96, 500, 22), Gd.Paper, StringAlignment.Near, thick: 2.2f);
            if (!string.IsNullOrEmpty(_msg))
                Gd.Outlined(g, _msg, f, new RectangleF(210, ClientSize.Height - 64, ClientSize.Width - 240, 24),
                    Gd.Gold, StringAlignment.Near, thick: 2.2f);
        }
    }
}
