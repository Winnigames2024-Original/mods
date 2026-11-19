using GEO_DASH.Audio;
using GEO_DASH.Core;
using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    public enum SearchMode { Levels, Featured, Epic, Users, Top }

    /// <summary>Поиск уровней (по имени и по ID) и поиск игроков.</summary>
    public class SearchForm : GdForm
    {
        readonly SearchMode _mode;
        readonly TextBox _query;
        readonly ComboBox _diff, _sort;
        readonly ListView _list;
        List<LevelDto> _levels = new();
        List<AccountDto> _users = new();
        string _msg = "";

        public SearchForm(SearchMode mode)
        {
            _mode = mode;
            Text = "GEO DASH — " + Title();
            ClientSize = new Size(1100, 700);
            bool people = mode == SearchMode.Users || mode == SearchMode.Top;
            BgA = people ? Color.FromArgb(190, 60, 170) : Color.FromArgb(0, 145, 200);
            BgId = people ? 15 : 7; GroundId = people ? 9 : 2;

            _query = new TextBox
            {
                Left = 24, Top = 100, Width = 420, Font = Gd.Bold(14f),
                BackColor = Color.FromArgb(20, 18, 48), ForeColor = Gd.Paper, BorderStyle = BorderStyle.FixedSingle
            };
            _query.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await Run(); } };
            Host(_query);

            Add("ИСКАТЬ", GdIcon.Search, 456, 94, 160, 46, Color.FromArgb(120, 220, 255), Color.FromArgb(20, 110, 180),
                async (s, e) => await Run());

            bool levelish = mode != SearchMode.Users && mode != SearchMode.Top;

            _diff = new ComboBox
            {
                Left = 630, Top = 100, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(20, 18, 48), ForeColor = Gd.Paper, FlatStyle = FlatStyle.Flat,
                Visible = levelish
            };
            _diff.Items.Add("любая сложность");
            foreach (Difficulty d in Enum.GetValues<Difficulty>()) _diff.Items.Add(Difficulties.Label(d));
            _diff.SelectedIndex = 0;
            _diff.SelectedIndexChanged += async (s, e) => await Run();
            Host(_diff);

            _sort = new ComboBox
            {
                Left = 844, Top = 100, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(20, 18, 48), ForeColor = Gd.Paper, FlatStyle = FlatStyle.Flat,
                Visible = levelish
            };
            _sort.Items.AddRange(new object[] { "новые", "популярные", "лучшие" });
            _sort.SelectedIndex = 0;
            _sort.SelectedIndexChanged += async (s, e) => await Run();
            Host(_sort);

            _list = new ListView
            {
                Left = 24, Top = 156, Width = 760, Height = 480,
                View = View.Details, FullRowSelect = true, HideSelection = false,
                BackColor = Color.FromArgb(20, 18, 48), ForeColor = Gd.Paper,
                BorderStyle = BorderStyle.FixedSingle, Font = Gd.Reg(10f)
            };
            if (levelish)
            {
                _list.Columns.Add("ID", 70);
                _list.Columns.Add("Название", 250);
                _list.Columns.Add("Автор", 150);
                _list.Columns.Add("Сложность", 130);
                _list.Columns.Add("★", 50);
                _list.Columns.Add("♥", 55);
                _list.Columns.Add("↓", 55);
                _list.DoubleClick += async (s, e) => await PlaySelected();
            }
            else
            {
                _list.Columns.Add("Игрок", 260);
                _list.Columns.Add("★ звёзды", 110);
                _list.Columns.Add("Монеты", 100);
                _list.Columns.Add("Демоны", 100);
                _list.Columns.Add("Уровней", 100);
                _list.DoubleClick += (s, e) => OpenProfile();
            }
            Host(_list);

            int bx = 804, by = 156;
            void B(string t, GdIcon ic, Color c1, Color c2, EventHandler h)
            { Add(t, ic, bx, by, 272, 50, c1, c2, h); by += 56; }

            if (levelish)
            {
                B("ИГРАТЬ", GdIcon.Play, Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100),
                    async (s, e) => await PlaySelected());
                B("СОХРАНИТЬ (SAVED)", GdIcon.Download, Color.FromArgb(120, 220, 255), Color.FromArgb(20, 110, 180),
                    async (s, e) => await SaveSelected());
                B("КОПИЯ В РЕДАКТОР", GdIcon.Copy, Color.FromArgb(200, 170, 255), Color.FromArgb(100, 60, 180),
                    async (s, e) => await CopySelected());
                B("ЛАЙК", GdIcon.Check, Color.FromArgb(255, 150, 200), Color.FromArgb(160, 50, 110),
                    async (s, e) => await LikeSelected());
                B("ПРОФИЛЬ АВТОРА", GdIcon.Profile, Color.FromArgb(150, 180, 230), Color.FromArgb(60, 80, 140),
                    (s, e) => OpenProfile());
            }
            else
            {
                B("ПРОФИЛЬ", GdIcon.Profile, Color.FromArgb(120, 190, 255), Color.FromArgb(30, 90, 190),
                    (s, e) => OpenProfile());
                B("В ДРУЗЬЯ", GdIcon.Friends, Color.FromArgb(255, 150, 200), Color.FromArgb(160, 50, 110),
                    async (s, e) => await AddFriend());
                B("ЕГО УРОВНИ", GdIcon.Online, Color.FromArgb(120, 220, 255), Color.FromArgb(20, 110, 180),
                    async (s, e) => await LevelsOfUser());
            }

            B("ОБНОВИТЬ", GdIcon.Refresh, Color.FromArgb(160, 170, 210), Color.FromArgb(70, 80, 130),
                async (s, e) => await Run());
            Add("НАЗАД", GdIcon.Back, bx, ClientSize.Height - 74, 272, 50,
                Color.FromArgb(160, 160, 190), Color.FromArgb(70, 70, 110), (s, e) => Close());

            Load += async (s, e) => await Run();
        }

        string Title() => _mode switch
        {
            SearchMode.Featured => "Featured",
            SearchMode.Epic => "Epic",
            SearchMode.Users => "Поиск игроков",
            SearchMode.Top => "Топ игроков",
            _ => "Поиск уровней"
        };

        string Hint() => _mode switch
        {
            SearchMode.Users => "введи имя игрока (можно часть имени)",
            SearchMode.Top => "лучшие игроки по звёздам",
            _ => "введи название уровня или его ID (например 12 или #12)"
        };

        LevelDto SelLevel => _list.SelectedIndices.Count > 0 && _list.SelectedIndices[0] < _levels.Count
            ? _levels[_list.SelectedIndices[0]] : null;
        AccountDto SelUser => _list.SelectedIndices.Count > 0 && _list.SelectedIndices[0] < _users.Count
            ? _users[_list.SelectedIndices[0]] : null;

        async Task Run()
        {
            _msg = "Ищу…"; Content.Invalidate();
            _list.Items.Clear();

            if (_mode == SearchMode.Users)
            {
                var r = await ApiClient.SearchUsersAsync(_query.Text.Trim());
                _users = r.Data ?? new();
                FillUsers();
                _msg = r.Ok ? (r.FromCache ? "офлайн: последний список" : $"найдено игроков: {_users.Count}") : r.Error;
                Content.Invalidate();
                return;
            }
            if (_mode == SearchMode.Top)
            {
                var r = await ApiClient.TopAsync();
                _users = r.Data ?? new();
                FillUsers();
                _msg = r.Ok ? (r.FromCache ? "офлайн: последний топ" : "топ обновлён") : r.Error;
                Content.Invalidate();
                return;
            }

            string q = _query.Text.Trim().TrimStart('#');
            string sort = _mode switch
            {
                SearchMode.Featured => "featured",
                SearchMode.Epic => "epic",
                _ => _sort.SelectedIndex switch { 1 => "popular", 2 => "top", _ => "recent" }
            };
            int diff = _diff.SelectedIndex <= 0 ? -1 : _diff.SelectedIndex - 1;

            // поиск по ID: если ввели число — пробуем сначала точный уровень
            if (int.TryParse(q, out int id) && id > 0)
            {
                var one = await ApiClient.LevelAsync(id);
                if (one.Ok && one.Data != null)
                {
                    _levels = new List<LevelDto> { one.Data };
                    FillLevels();
                    _msg = $"уровень #{id} найден";
                    Content.Invalidate();
                    return;
                }
            }

            var res = await ApiClient.LevelsAsync(q, sort, diff);
            _levels = res.Data ?? new();
            FillLevels();
            _msg = res.Ok
                ? (res.FromCache ? "офлайн: показан кэш" : $"найдено уровней: {_levels.Count}")
                : res.Error;
            Content.Invalidate();
        }

        void FillLevels()
        {
            _list.Items.Clear();
            foreach (var l in _levels)
            {
                var it = new ListViewItem(new[]
                {
                    "#" + l.Id,
                    l.Name + (l.Daily ? "  [DAILY]" : "") + (l.Weekly ? "  [WEEKLY]" : "")
                           + (l.OfflinePack ? "  [OFFLINE]" : "") + (LocalStore.IsSaved(l.Id) ? "  ✔" : ""),
                    l.Author,
                    Difficulties.Label((Difficulty)l.Difficulty),
                    l.Stars.ToString(), l.Likes.ToString(), l.Downloads.ToString()
                });
                it.ForeColor = l.Epic ? Color.FromArgb(255, 150, 230)
                             : l.Featured ? Gd.Gold
                             : Difficulties.Tint((Difficulty)l.Difficulty);
                _list.Items.Add(it);
            }
            if (_list.Items.Count > 0) _list.Items[0].Selected = true;
        }

        void FillUsers()
        {
            _list.Items.Clear();
            foreach (var u in _users)
            {
                var it = new ListViewItem(new[]
                {
                    u.Name + (u.Admin ? "  [админ]" : "") + (u.Banned ? "  [бан]" : ""),
                    u.Stars.ToString(), u.Coins.ToString(), u.Demons.ToString(), u.Levels.ToString()
                });
                it.ForeColor = u.Admin ? Gd.Gold : (u.Banned ? Gd.Red : Gd.Paper);
                _list.Items.Add(it);
            }
            if (_list.Items.Count > 0) _list.Items[0].Selected = true;
        }

        async Task<LevelData> Fetch()
        {
            var d = SelLevel;
            if (d == null) return null;
            _msg = "Скачиваю уровень…"; Content.Invalidate();
            var lv = await LocalStore.DownloadAsync(d.Id, true, new Progress<string>(s => { _msg = s; Content.Invalidate(); }));
            if (lv == null) _msg = "Не удалось скачать (нет сети?)";
            Content.Invalidate();
            return lv;
        }

        async Task PlaySelected()
        {
            var lv = await Fetch();
            if (lv == null) return;
            MusicPlayer.Stop();
            Nav.Go(new GameForm(lv.DeepClone()));
            _msg = ""; Content.Invalidate();
        }

        async Task SaveSelected()
        {
            var lv = await Fetch();
            if (lv != null) _msg = "Сохранено в SAVED — работает без интернета";
            Content.Invalidate();
        }

        async Task CopySelected()
        {
            var lv = await Fetch();
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
            _msg = "Скопировано в MY LEVELS";
            Content.Invalidate();
        }

        async Task LikeSelected()
        {
            var d = SelLevel;
            if (d == null) return;
            var r = await ApiClient.LikeAsync(d.Id, true);
            _msg = r.Ok ? "Лайк отправлен" : r.Error;
            Content.Invalidate();
        }

        void OpenProfile()
        {
            string name = _mode is SearchMode.Users or SearchMode.Top ? SelUser?.Name : SelLevel?.Author;
            if (string.IsNullOrEmpty(name)) return;
            Nav.Go(new ProfileForm(name));
        }

        async Task AddFriend()
        {
            var u = SelUser;
            if (u == null) return;
            var r = await ApiClient.FriendRequestAsync(u.Name);
            _msg = r.Ok ? "Запрос отправлен игроку " + u.Name : r.Error;
            Content.Invalidate();
        }

        async Task LevelsOfUser()
        {
            var u = SelUser;
            if (u == null) return;
            _msg = "Загружаю уровни " + u.Name + "…"; Content.Invalidate();
            var r = await ApiClient.LevelsAsync(author: u.Name, sort: "recent");
            _levels = r.Data ?? new();
            _list.Columns.Clear();
            _list.Columns.Add("ID", 70);
            _list.Columns.Add("Название", 250);
            _list.Columns.Add("Автор", 150);
            _list.Columns.Add("Сложность", 130);
            _list.Columns.Add("★", 50);
            _list.Columns.Add("♥", 55);
            _list.Columns.Add("↓", 55);
            FillLevels();
            _msg = $"уровней у {u.Name}: {_levels.Count}";
            Content.Invalidate();
        }

        protected override void OnRender(Graphics g)
        {
                        Header(g, Title().ToUpperInvariant(), Hint());
            using var f = Gd.Bold(10f);
            if (!string.IsNullOrEmpty(_msg))
                Gd.Outlined(g, _msg, f, new RectangleF(24, ClientSize.Height - 44, 760, 24),
                    Gd.Gold, StringAlignment.Near, thick: 2.2f);
        }
    }
}
