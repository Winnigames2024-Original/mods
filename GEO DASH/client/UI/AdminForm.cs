using GEO_DASH.Core;
using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    /// <summary>Админка внутри игры — только для Winnigames2024.</summary>
    public class AdminForm : GdForm
    {
        readonly ListView _users, _levels;
        readonly ComboBox _diff;
        List<AccountDto> _u = new();
        List<LevelDto> _l = new();
        string _msg = "";

        public AdminForm()
        {
            Text = "GEO DASH — Админка";
            ClientSize = new Size(1180, 790);
            BgA = Color.FromArgb(210, 45, 60); BgId = 19; GroundId = 10;

            _users = Make(24, 130, 420, 300, new[] { "Игрок", "★", "Уровни", "Статус" });
            _levels = Make(460, 130, 690, 300, new[] { "ID", "Название", "Автор", "Сложность", "Флаги" });
            Host(_users); Host(_levels);

            _diff = new ComboBox
            {
                Left = 460, Top = 446, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(20, 18, 48), ForeColor = Gd.Paper, FlatStyle = FlatStyle.Flat
            };
            foreach (Difficulty d in Enum.GetValues<Difficulty>()) _diff.Items.Add(Difficulties.Label(d));
            _diff.SelectedIndex = 3;
            Host(_diff);

            // пользователи
            Add("БАН / РАЗБАН", GdIcon.Lock, 24, 440, 200, 46, Color.FromArgb(240, 140, 90), Color.FromArgb(150, 60, 20),
                async (s, e) => await BanToggle());
            Add("УДАЛИТЬ ИГРОКА", GdIcon.Trash, 236, 440, 200, 46, Color.FromArgb(230, 110, 110), Color.FromArgb(140, 40, 50),
                async (s, e) => await DeleteUser());

            // уровни
            Add("ОЦЕНИТЬ", GdIcon.Star, 672, 440, 150, 46, Color.FromArgb(255, 210, 90), Color.FromArgb(170, 120, 10),
                async (s, e) => await Rate(false, false));
            Add("FEATURED", GdIcon.Check, 830, 440, 150, 46, Color.FromArgb(120, 220, 255), Color.FromArgb(20, 110, 180),
                async (s, e) => await Rate(true, false));
            Add("EPIC", GdIcon.Admin, 988, 440, 150, 46, Color.FromArgb(255, 130, 230), Color.FromArgb(160, 40, 140),
                async (s, e) => await Rate(true, true));

            Add("СДЕЛАТЬ DAILY", GdIcon.Daily, 460, 496, 220, 46, Color.FromArgb(120, 190, 255), Color.FromArgb(30, 90, 190),
                async (s, e) => await SetDaily());
            Add("СДЕЛАТЬ WEEKLY", GdIcon.Weekly, 690, 496, 220, 46, Color.FromArgb(210, 140, 255), Color.FromArgb(110, 40, 180),
                async (s, e) => await SetWeekly());
            Add("DAILY → WEEKLY", GdIcon.Upload, 920, 496, 220, 46, Color.FromArgb(255, 180, 120), Color.FromArgb(170, 90, 30),
                async (s, e) => await PromoteDaily());

            Add("OFFLINE-ПАК ВКЛ/ВЫКЛ", GdIcon.Download, 460, 552, 290, 46, Color.FromArgb(120, 255, 180), Color.FromArgb(20, 140, 90),
                async (s, e) => await ToggleOffline());
            Add("УДАЛИТЬ УРОВЕНЬ", GdIcon.Trash, 760, 552, 220, 46, Color.FromArgb(230, 110, 110), Color.FromArgb(140, 40, 50),
                async (s, e) => await DeleteLevel());
            Add("ОБЪЯВЛЕНИЕ", GdIcon.Info, 990, 552, 150, 46, Color.FromArgb(160, 180, 230), Color.FromArgb(60, 80, 150),
                async (s, e) => await Broadcast());

            Add("АДМИН ВКЛ/ВЫКЛ", GdIcon.Admin, 24, 608, 200, 46, Color.FromArgb(255, 170, 90), Color.FromArgb(170, 85, 10),
                async (s, e) => await GrantToggle());
            Add("ВЫДАТЬ НАГРАДЫ", GdIcon.Star, 236, 608, 200, 46, Color.FromArgb(255, 215, 90), Color.FromArgb(170, 120, 10),
                async (s, e) => await Give());
            Add("СБРОС ПАРОЛЯ", GdIcon.Lock, 448, 608, 200, 46, Color.FromArgb(180, 190, 230), Color.FromArgb(80, 90, 160),
                async (s, e) => await ResetPassword());
            Add("ЗАЛИТЬ ПЕСНЮ MP3", GdIcon.Music, 660, 608, 230, 46, Color.FromArgb(255, 150, 215), Color.FromArgb(160, 40, 120),
                async (s, e) => await UploadSong());

            Add("ОБНОВИТЬ", GdIcon.Refresh, 24, 496, 200, 46, Color.FromArgb(160, 170, 210), Color.FromArgb(70, 80, 130),
                async (s, e) => await Reload());
            Add("НАЗАД", GdIcon.Back, 24, 552, 200, 46, Color.FromArgb(160, 160, 190), Color.FromArgb(70, 70, 110),
                (s, e) => Close());

            Load += async (s, e) => await Reload();
        }

        ListView Make(int x, int y, int w, int h, string[] cols)
        {
            var lv = new ListView
            {
                Left = x, Top = y, Width = w, Height = h, View = View.Details,
                FullRowSelect = true, HideSelection = false, Font = Gd.Reg(10f),
                BackColor = Color.FromArgb(20, 18, 48), ForeColor = Gd.Paper, BorderStyle = BorderStyle.FixedSingle
            };
            foreach (var c in cols) lv.Columns.Add(c, w / cols.Length - 6);
            return lv;
        }

        async Task Reload()
        {
            if (!AppConfig.IsAdminUser) { _msg = "Доступно только для " + AppConfig.AdminUser; Content.Invalidate(); return; }
            _msg = "Загружаю…"; Content.Invalidate();

            var u = await ApiClient.AdminUsersAsync();
            _u = u.Data ?? new();
            _users.Items.Clear();
            foreach (var a in _u)
            {
                var it = new ListViewItem(new[]
                {
                    a.Name, a.Stars.ToString(), a.Levels.ToString(),
                    a.Banned ? "ЗАБАНЕН" : (a.Admin ? "админ" : "ок")
                });
                it.ForeColor = a.Banned ? Gd.Red : (a.Admin ? Gd.Gold : Gd.Paper);
                _users.Items.Add(it);
            }

            var l = await ApiClient.LevelsAsync(sort: "recent");
            _l = l.Data ?? new();
            _levels.Items.Clear();
            foreach (var x in _l)
            {
                string flags = (x.Featured ? "F " : "") + (x.Epic ? "E " : "") +
                               (x.Daily ? "D " : "") + (x.Weekly ? "W " : "") + (x.OfflinePack ? "OFF" : "");
                _levels.Items.Add(new ListViewItem(new[]
                {
                    "#" + x.Id, x.Name, x.Author, Difficulties.Label((Difficulty)x.Difficulty), flags
                }));
            }

            _msg = $"игроков: {_u.Count}, уровней: {_l.Count}" + (u.FromCache ? " (из кэша)" : "");
            Content.Invalidate();
        }

        async Task GrantToggle()
        {
            var u = SelUser; if (u == null) { _msg = "Выбери игрока"; Content.Invalidate(); return; }
            bool make = !u.Admin;
            if (!GdDialog.Confirm(this, "Админка",
                    (make ? "Сделать админом: " : "Снять админку: ") + u.Name + "?")) return;
            var r = await ApiClient.AdminGrantAsync(u.Name, make);
            _msg = r.Ok ? (make ? u.Name + " теперь админ" : "Админка снята с " + u.Name) : r.Error;
            await Reload();
        }

        async Task Give()
        {
            var u = SelUser; if (u == null) { _msg = "Выбери игрока"; Content.Invalidate(); return; }
            string s1 = GdDialog.Prompt(this, "Награды", "Звёзды (можно со знаком −):", "10");
            if (s1 == null) return;
            string s2 = GdDialog.Prompt(this, "Награды", "Монеты:", "0");
            if (s2 == null) return;
            string s3 = GdDialog.Prompt(this, "Награды", "Демоны:", "0");
            if (s3 == null) return;
            int.TryParse(s1, out int st); int.TryParse(s2, out int co); int.TryParse(s3, out int de);
            var r = await ApiClient.AdminGiveAsync(u.Name, st, co, de);
            _msg = r.Ok ? $"{u.Name}: +{st}★ +{co} монет +{de} демонов" : r.Error;
            await Reload();
        }

        async Task ResetPassword()
        {
            var u = SelUser; if (u == null) { _msg = "Выбери игрока"; Content.Invalidate(); return; }
            string p = GdDialog.Prompt(this, "Сброс пароля", "Новый пароль для " + u.Name + ":", "", true);
            if (string.IsNullOrWhiteSpace(p)) return;
            var r = await ApiClient.AdminResetPasswordAsync(u.Name, p.Trim());
            _msg = r.Ok ? "Пароль сменён, старые входы сброшены" : r.Error;
            Content.Invalidate();
        }

        /// <summary>Залить свой mp3 как трек с нужным ID — Newgrounds не нужен.</summary>
        async Task UploadSong()
        {
            string idTxt = GdDialog.Prompt(this, "Песня", "ID трека (по нему он будет в игре):", "1000001");
            if (string.IsNullOrWhiteSpace(idTxt) || !int.TryParse(idTxt.Trim(), out int id) || id <= 0) return;

            using var ofd = new OpenFileDialog { Filter = "Музыка (*.mp3)|*.mp3", Title = "Выбери mp3" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            _msg = "Заливаю песню…"; Content.Invalidate();
            var bytes = await File.ReadAllBytesAsync(ofd.FileName);
            var r = await ApiClient.AdminUploadSongAsync(id, bytes,
                Path.GetFileNameWithoutExtension(ofd.FileName));
            _msg = r.Ok ? $"Трек #{id} залит на сервер" : r.Error;
            Content.Invalidate();
        }

        AccountDto SelUser => _users.SelectedIndices.Count > 0 ? _u[_users.SelectedIndices[0]] : null;
        LevelDto SelLevel => _levels.SelectedIndices.Count > 0 ? _l[_levels.SelectedIndices[0]] : null;

        async Task BanToggle()
        {
            var a = SelUser; if (a == null) return;
            var r = await ApiClient.AdminBanAsync(a.Name, !a.Banned);
            _msg = r.Ok ? (a.Banned ? "Разбанен " : "Забанен ") + a.Name : r.Error;
            await Reload();
        }

        async Task DeleteUser()
        {
            var a = SelUser; if (a == null) return;
            if (!GdDialog.Confirm(this, "Админка", $"Удалить игрока {a.Name} со всеми уровнями?")) return;
            var r = await ApiClient.AdminDeleteUserAsync(a.Name);
            _msg = r.Ok ? "Удалён " + a.Name : r.Error;
            await Reload();
        }

        async Task Rate(bool featured, bool epic)
        {
            var l = SelLevel; if (l == null) return;
            var r = await ApiClient.AdminRateAsync(l.Id, _diff.SelectedIndex, featured, epic);
            _msg = r.Ok ? $"Уровень #{l.Id}: {Difficulties.Label((Difficulty)_diff.SelectedIndex)}" +
                          (featured ? ", featured" : "") + (epic ? ", epic" : "") : r.Error;
            await Reload();
        }

        async Task SetDaily()
        {
            var l = SelLevel; if (l == null) return;
            var r = await ApiClient.AdminSetDailyAsync(l.Id);
            _msg = r.Ok ? $"#{l.Id} — уровень дня" : r.Error;
            await Reload();
        }

        async Task SetWeekly()
        {
            var l = SelLevel; if (l == null) return;
            var r = await ApiClient.AdminSetWeeklyAsync(l.Id);
            _msg = r.Ok ? $"#{l.Id} — уровень недели" : r.Error;
            await Reload();
        }

        async Task PromoteDaily()
        {
            var r = await ApiClient.AdminPromoteDailyToWeeklyAsync();
            _msg = r.Ok ? "Текущий daily переведён в weekly" : r.Error;
            await Reload();
        }

        async Task ToggleOffline()
        {
            var l = SelLevel; if (l == null) return;
            var r = await ApiClient.AdminSetOfflineAsync(l.Id, !l.OfflinePack);
            _msg = r.Ok
                ? (l.OfflinePack ? $"#{l.Id} убран из офлайн-паков" : $"#{l.Id} будет автоматически качаться всем в SAVED")
                : r.Error;
            await Reload();
        }

        async Task DeleteLevel()
        {
            var l = SelLevel; if (l == null) return;
            if (!GdDialog.Confirm(this, "Админка", $"Удалить уровень #{l.Id} «{l.Name}»?")) return;
            var r = await ApiClient.DeleteLevelAsync(l.Id);
            _msg = r.Ok ? "Удалён" : r.Error;
            await Reload();
        }

        async Task Broadcast()
        {
            var t = GdDialog.Prompt(this, "GEO DASH", "Текст объявления для всех игроков:");
            if (string.IsNullOrWhiteSpace(t)) return;
            var r = await ApiClient.AdminBroadcastAsync(t);
            _msg = r.Ok ? "Объявление отправлено" : r.Error;
            Content.Invalidate();
        }

        protected override void OnRender(Graphics g)
        {
                        Header(g, "АДМИНКА", "пользователь " + AppConfig.AdminUser);
            using var f = Gd.Bold(11f);
            Gd.Outlined(g, "Игроки", f, new RectangleF(24, 104, 300, 24), Gd.Paper, StringAlignment.Near, thick: 2.2f);
            Gd.Outlined(g, "Уровни", f, new RectangleF(460, 104, 300, 24), Gd.Paper, StringAlignment.Near, thick: 2.2f);
            Gd.Outlined(g, "Сложность для оценки:", f, new RectangleF(460, 420, 300, 24), Gd.Paper, StringAlignment.Near, thick: 2.2f);
            if (!string.IsNullOrEmpty(_msg))
                Gd.Outlined(g, _msg, f, new RectangleF(24, ClientSize.Height - 44, 1000, 24), Gd.Gold, StringAlignment.Near, thick: 2.2f);
        }
    }
}
