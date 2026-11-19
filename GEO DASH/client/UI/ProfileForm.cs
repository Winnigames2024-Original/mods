using GEO_DASH.Core;
using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    public class ProfileForm : GdForm
    {
        readonly string _user;
        AccountDto _acc;
        List<LevelDto> _levels = new();
        readonly ListView _list;
        string _msg = "";

        public ProfileForm(string user)
        {
            _user = string.IsNullOrWhiteSpace(user) ? AppConfig.Settings.UserName : user;
            Text = "GEO DASH — Профиль";
            ClientSize = new Size(940, 640);
            BgA = Color.FromArgb(20, 40, 80);

            _list = new ListView
            {
                Left = 24, Top = 330, Width = 600, Height = 240, View = View.Details,
                FullRowSelect = true, Font = Gd.Reg(10f), HideSelection = false,
                BackColor = Color.FromArgb(20, 18, 48), ForeColor = Gd.Paper, BorderStyle = BorderStyle.FixedSingle
            };
            _list.Columns.Add("ID", 60);
            _list.Columns.Add("Уровень автора", 260);
            _list.Columns.Add("Сложность", 120);
            _list.Columns.Add("♥", 60);
            _list.Columns.Add("↓", 70);
            Controls.Add(_list);

            int x = 648, y = 330;
            void B(string t, GdIcon ic, Color c1, Color c2, EventHandler h)
            { Add(t, ic, x, y, 264, 50, c1, c2, h); y += 56; }

            if (string.IsNullOrEmpty(_user) || _user == AppConfig.Settings.UserName)
                B("ВОЙТИ / СМЕНИТЬ АККАУНТ", GdIcon.Profile, Color.FromArgb(120, 190, 255), Color.FromArgb(30, 90, 190),
                    async (s, e) => { using (var f = new LoginForm()) f.ShowDialog(this); await Reload(); });
            else
                B("ДОБАВИТЬ В ДРУЗЬЯ", GdIcon.Friends, Color.FromArgb(255, 150, 200), Color.FromArgb(160, 50, 110),
                    async (s, e) => { var r = await ApiClient.FriendRequestAsync(_user); _msg = r.Ok ? "Запрос отправлен" : r.Error; Invalidate(); });

            B("ОБНОВИТЬ", GdIcon.Refresh, Color.FromArgb(160, 170, 210), Color.FromArgb(70, 80, 130), async (s, e) => await Reload());
            B("НАЗАД", GdIcon.Back, Color.FromArgb(160, 160, 190), Color.FromArgb(70, 70, 110), (s, e) => Close());

            Load += async (s, e) => await Reload();
        }

        async Task Reload()
        {
            if (string.IsNullOrEmpty(_user)) { _msg = "Ты не вошёл в аккаунт"; Invalidate(); return; }
            _msg = "Загружаю…"; Invalidate();

            var a = _user == AppConfig.Settings.UserName ? await ApiClient.MeAsync() : await ApiClient.UserAsync(_user);
            _acc = a.Data ?? (_user == AppConfig.Settings.UserName ? LocalStore.CachedMe() : null);

            var l = await ApiClient.LevelsAsync(author: _user, sort: "recent");
            _levels = l.Data ?? new();
            _list.Items.Clear();
            foreach (var x in _levels)
                _list.Items.Add(new ListViewItem(new[]
                {
                    "#" + x.Id, x.Name, Difficulties.Label((Difficulty)x.Difficulty),
                    x.Likes.ToString(), x.Downloads.ToString()
                }));

            _msg = a.FromCache || l.FromCache ? "Офлайн: последние данные" : "";
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Header(g, (_acc?.Name ?? _user ?? "ГОСТЬ").ToUpperInvariant(),
                _acc?.Banned == true ? "аккаунт заблокирован" : (_acc?.Admin == true ? "администратор" : ""));

            var card = new RectangleF(24, 110, ClientSize.Width - 48, 196);
            Gd.Plate(g, card, Color.FromArgb(40, 44, 96), Color.FromArgb(20, 22, 54), 18f, 4f);

            // локальная статистика (считается даже офлайн)
            int localCoins = 0, localDemons = 0, completed = 0;
            foreach (var p in LocalStore.Progress.Values)
            {
                localCoins += p.Coins;
                if (p.Completed) completed++;
            }
            foreach (var lv in LocalStore.SavedLevels().Concat(LocalStore.MyLevels()).Concat(OfficialLevels.EnsureFiles()))
            {
                var pr = LocalStore.GetProgress(lv);
                if (pr.Completed && Difficulties.IsDemon(lv.Settings.Difficulty)) localDemons++;
            }

            int stars = _acc?.Stars ?? 0, coins = Math.Max(_acc?.Coins ?? 0, localCoins);
            int demons = Math.Max(_acc?.Demons ?? 0, localDemons);

            float x = card.X + 30, y = card.Y + 34;
            void Stat(GdIcon ic, string val, string label, Color c)
            {
                Gd.Icon(g, ic, new RectangleF(x, y, 42, 42), c);
                using var f1 = Gd.Title(20f);
                Gd.Outlined(g, val, f1, new RectangleF(x, y + 46, 100, 30), Gd.Paper, StringAlignment.Near, thick: 3f);
                using var f2 = Gd.Bold(9.5f);
                Gd.Outlined(g, label, f2, new RectangleF(x, y + 78, 130, 20), Color.FromArgb(220, 200, 215, 255), StringAlignment.Near, thick: 2f);
                x += 150;
            }

            Stat(GdIcon.Star, stars.ToString(), "звёзды", Gd.Gold);
            Stat(GdIcon.Coin, coins.ToString(), "монеты", Gd.Gold);
            Stat(GdIcon.Admin, demons.ToString(), "демоны пройдены", Color.FromArgb(220, 110, 255));
            Stat(GdIcon.Check, completed.ToString(), "уровней пройдено", Gd.Lime);
            Stat(GdIcon.Edit, (_acc?.Levels ?? _levels.Count).ToString(), "своих уровней", Gd.Neon);
            Stat(GdIcon.Friends, (_acc?.FriendCount ?? 0).ToString(), "друзей", Color.FromArgb(255, 150, 200));

            using var f = Gd.Bold(10f);
            if (!string.IsNullOrEmpty(_msg))
                Gd.Outlined(g, _msg, f, new RectangleF(24, ClientSize.Height - 44, 600, 22), Gd.Gold, StringAlignment.Near, thick: 2.2f);
        }
    }
}
