using GEO_DASH.Audio;
using GEO_DASH.Core;
using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    public class OnlineForm : GdForm
    {
        readonly ListView _list;
        readonly TextBox _search;
        readonly ComboBox _sort, _diff;
        List<LevelDto> _data = new();
        string _msg = "";
        bool _cached;

        public OnlineForm()
        {
            Text = "GEO DASH — Онлайн";
            ClientSize = new Size(1100, 680);
            BgA = Color.FromArgb(30, 95, 225); BgId = 2; GroundId = 1;

            _search = new TextBox
            {
                Left = 24, Top = 96, Width = 300, Font = Gd.Bold(11f),
                BackColor = Color.FromArgb(20, 18, 48), ForeColor = Gd.Paper, BorderStyle = BorderStyle.FixedSingle
            };
            _search.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) await Reload(); };
            Host(_search);

            _sort = new ComboBox
            {
                Left = 332, Top = 96, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(20, 18, 48), ForeColor = Gd.Paper, FlatStyle = FlatStyle.Flat
            };
            _sort.Items.AddRange(new object[] { "Новые", "Популярные", "Лучшие", "Featured", "Epic" });
            _sort.SelectedIndex = 0;
            _sort.SelectedIndexChanged += async (s, e) => await Reload();
            Host(_sort);

            _diff = new ComboBox
            {
                Left = 490, Top = 96, Width = 170, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(20, 18, 48), ForeColor = Gd.Paper, FlatStyle = FlatStyle.Flat
            };
            _diff.Items.Add("Любая сложность");
            foreach (Difficulty d in Enum.GetValues<Difficulty>()) _diff.Items.Add(Difficulties.Label(d));
            _diff.SelectedIndex = 0;
            _diff.SelectedIndexChanged += async (s, e) => await Reload();
            Host(_diff);

            _list = new ListView
            {
                Left = 24, Top = 134, Width = 760, Height = 500,
                View = View.Details, FullRowSelect = true, HideSelection = false,
                BackColor = Color.FromArgb(20, 18, 48), ForeColor = Gd.Paper,
                BorderStyle = BorderStyle.FixedSingle, Font = Gd.Reg(10f)
            };
            _list.Columns.Add("ID", 60);
            _list.Columns.Add("Название", 230);
            _list.Columns.Add("Автор", 130);
            _list.Columns.Add("Сложность", 110);
            _list.Columns.Add("★", 45);
            _list.Columns.Add("♥", 55);
            _list.Columns.Add("↓", 60);
            _list.DoubleClick += async (s, e) => await Play();
            Host(_list);

            int x = 804, y = 134;
            void B(string t, GdIcon ic, Color c1, Color c2, EventHandler h)
            {
                Add(t, ic, x, y, 272, 50, c1, c2, h);
                y += 56;
            }

            B("ИГРАТЬ", GdIcon.Play, Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100), async (s, e) => await Play());
            B("СОХРАНИТЬ (SAVED)", GdIcon.Download, Color.FromArgb(120, 220, 255), Color.FromArgb(20, 110, 180), async (s, e) => await Save());
            B("КОПИРОВАТЬ В РЕДАКТОР", GdIcon.Copy, Color.FromArgb(200, 170, 255), Color.FromArgb(100, 60, 180), async (s, e) => await CopyToEditor());
            B("ЛАЙК", GdIcon.Check, Color.FromArgb(255, 150, 200), Color.FromArgb(160, 50, 110), async (s, e) => await Like());
            B("ПРОФИЛЬ АВТОРА", GdIcon.Profile, Color.FromArgb(150, 180, 230), Color.FromArgb(60, 80, 140), (s, e) => OpenAuthor());
            B("ОБНОВИТЬ", GdIcon.Refresh, Color.FromArgb(160, 170, 210), Color.FromArgb(70, 80, 130), async (s, e) => await Reload());

            Add("НАЗАД", GdIcon.Back, x, ClientSize.Height - 74, 272, 48,
                Color.FromArgb(160, 160, 190), Color.FromArgb(70, 70, 110), (s, e) => Close());

            Load += async (s, e) => await Reload();
        }

        LevelDto Sel => _list.SelectedIndices.Count > 0 && _list.SelectedIndices[0] < _data.Count
            ? _data[_list.SelectedIndices[0]] : null;

        async Task Reload()
        {
            _msg = "Загружаю…"; Content.Invalidate();
            string sort = _sort.SelectedIndex switch
            {
                1 => "popular", 2 => "top", 3 => "featured", 4 => "epic", _ => "recent"
            };
            int diff = _diff.SelectedIndex <= 0 ? -1 : _diff.SelectedIndex - 1;
            var r = await ApiClient.LevelsAsync(_search.Text.Trim(), sort, diff);
            _data = r.Data ?? new List<LevelDto>();
            _cached = r.FromCache;
            _msg = r.Ok
                ? (r.FromCache ? "Офлайн: показан последний скачанный список" : $"Найдено: {_data.Count}")
                : r.Error;

            _list.Items.Clear();
            foreach (var l in _data)
            {
                var it = new ListViewItem(new[]
                {
                    "#" + l.Id, l.Name + (l.Daily ? "  [DAILY]" : "") + (l.Weekly ? "  [WEEKLY]" : "") +
                        (l.OfflinePack ? "  [OFFLINE]" : ""),
                    l.Author, Difficulties.Label((Difficulty)l.Difficulty),
                    l.Stars.ToString(), l.Likes.ToString(), l.Downloads.ToString()
                });
                it.ForeColor = l.Epic ? Color.FromArgb(255, 150, 230)
                             : l.Featured ? Gd.Gold
                             : Difficulties.Tint((Difficulty)l.Difficulty);
                _list.Items.Add(it);
            }
            if (_list.Items.Count > 0) _list.Items[0].Selected = true;
            Content.Invalidate();
        }

        async Task<LevelData> Fetch(LevelDto d)
        {
            if (d == null) return null;
            _msg = "Скачиваю уровень…"; Content.Invalidate();
            var lv = await LocalStore.DownloadAsync(d.Id, true, new Progress<string>(s => { _msg = s; Content.Invalidate(); }));
            if (lv == null) _msg = "Не удалось скачать (нет сети?)";
            Content.Invalidate();
            return lv;
        }

        async Task Play()
        {
            var lv = await Fetch(Sel);
            if (lv == null) return;
            MusicPlayer.Stop();
            Nav.Go(new GameForm(lv.DeepClone()));
            _msg = "";
            Content.Invalidate();
        }

        async Task Save()
        {
            var lv = await Fetch(Sel);
            if (lv != null) _msg = "Сохранено в SAVED — доступно без интернета";
            Content.Invalidate();
        }

        async Task CopyToEditor()
        {
            var d = Sel;
            if (d == null) return;
            var lv = await Fetch(d);
            if (lv == null) return;

            var st = lv.Settings;
            if (st.CopyMode == 0)
            {
                GdDialog.Info(this, "Копирование запрещено", "Автор закрыл копирование этого уровня.");
                return;
            }
            if (st.CopyMode == 1)
            {
                var pass = GdDialog.Prompt(this, "GEO DASH", "Копирование по паролю. Введи пароль автора:");
                if (pass == null) return;
                if (!string.Equals(pass.Trim(), st.CopyPassword, StringComparison.Ordinal))
                {
                    GdDialog.Info(this, "Копирование", "Неверный пароль.");
                    return;
                }
            }

            var c = lv.DeepClone();
            c.Settings.LocalId = Guid.NewGuid().ToString("N");
            c.Settings.ServerId = 0;
            c.Settings.Name = st.Name + " (копия)";
            c.Settings.Author = AppConfig.Settings.UserName;
            c.Settings.CopyMode = 0;
            c.Settings.CopyPassword = "";
            LocalStore.SaveMyLevel(c);
            _msg = "Скопировано в «Мои уровни»";
            Content.Invalidate();
        }

        async Task Like()
        {
            var d = Sel;
            if (d == null) return;
            var r = await ApiClient.LikeAsync(d.Id, true);
            _msg = r.Ok ? "Лайк отправлен" : r.Error;
            Content.Invalidate();
        }

        void OpenAuthor()
        {
            var d = Sel;
            if (d == null) return;
            Nav.Go(new ProfileForm(d.Author));
        }

        protected override void OnRender(Graphics g)
        {
                        Header(g, "ОНЛАЙН-УРОВНИ", ApiClient.Online ? AppConfig.ServerLabel : "офлайн — только кэш");
            using var f = Gd.Bold(10f);
            if (!string.IsNullOrEmpty(_msg))
                Gd.Outlined(g, _msg, f, new RectangleF(24, ClientSize.Height - 40, 760, 24),
                    _cached ? Gd.Gold : Gd.Paper, StringAlignment.Near, thick: 2.2f);
        }
    }
}
