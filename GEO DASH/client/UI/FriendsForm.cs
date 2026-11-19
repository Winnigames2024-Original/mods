using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    public class FriendsForm : GdForm
    {
        readonly ListView _friends, _requests;
        readonly TextBox _add;
        List<FriendDto> _f = new();
        List<FriendRequestDto> _r = new();
        string _msg = "";

        public FriendsForm()
        {
            Text = "GEO DASH — Друзья";
            ClientSize = new Size(1000, 640);
            BgA = Color.FromArgb(215, 60, 150); BgId = 18; GroundId = 6;

            _friends = MakeList(24, 140, 460, 380, new[] { "Друг", "★", "С" });
            _requests = MakeList(508, 140, 460, 250, new[] { "Запрос от / кому", "Когда", "Тип" });
            Host(_friends); Host(_requests);

            _add = new TextBox
            {
                Left = 508, Top = 420, Width = 290, Font = Gd.Bold(11f),
                BackColor = Color.FromArgb(20, 18, 48), ForeColor = Gd.Paper, BorderStyle = BorderStyle.FixedSingle
            };
            Host(_add);
            Add("ОТПРАВИТЬ ЗАПРОС", GdIcon.Plus, 806, 414, 162, 44, Color.FromArgb(120, 220, 255), Color.FromArgb(20, 110, 180),
                async (s, e) => await Send());

            Add("ПРИНЯТЬ", GdIcon.Check, 508, 470, 220, 48, Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100),
                async (s, e) => await Answer(true));
            Add("ОТКЛОНИТЬ", GdIcon.Cross, 748, 470, 220, 48, Color.FromArgb(240, 120, 120), Color.FromArgb(150, 40, 50),
                async (s, e) => await Answer(false));

            Add("ПРОФИЛЬ ДРУГА", GdIcon.Profile, 24, 532, 220, 48, Color.FromArgb(150, 180, 230), Color.FromArgb(60, 80, 140),
                (s, e) =>
                {
                    if (_friends.SelectedIndices.Count > 0)
                        using (var f = new ProfileForm(_f[_friends.SelectedIndices[0]].Name)) f.ShowDialog(this);
                });
            Add("УДАЛИТЬ ИЗ ДРУЗЕЙ", GdIcon.Trash, 264, 532, 220, 48, Color.FromArgb(230, 110, 110), Color.FromArgb(140, 40, 50),
                async (s, e) => await Remove());
            Add("ОБНОВИТЬ", GdIcon.Refresh, 508, 532, 220, 48, Color.FromArgb(160, 170, 210), Color.FromArgb(70, 80, 130),
                async (s, e) => await Reload());
            Add("НАЗАД", GdIcon.Back, 748, 532, 220, 48, Color.FromArgb(160, 160, 190), Color.FromArgb(70, 70, 110),
                (s, e) => Close());

            Load += async (s, e) => await Reload();
        }

        ListView MakeList(int x, int y, int w, int h, string[] cols)
        {
            var lv = new ListView
            {
                Left = x, Top = y, Width = w, Height = h, View = View.Details,
                FullRowSelect = true, HideSelection = false, Font = Gd.Reg(10f),
                BackColor = Color.FromArgb(20, 18, 48), ForeColor = Gd.Paper, BorderStyle = BorderStyle.FixedSingle
            };
            foreach (var c in cols) lv.Columns.Add(c, w / cols.Length - 8);
            return lv;
        }

        async Task Reload()
        {
            if (!AppConfig.LoggedIn) { _msg = "Войди в аккаунт, чтобы видеть друзей"; Content.Invalidate(); return; }
            _msg = "Загружаю…"; Content.Invalidate();

            var f = await ApiClient.FriendsAsync();
            var r = await ApiClient.FriendRequestsAsync();
            _f = f.Data ?? new();
            _r = r.Data ?? new();

            _friends.Items.Clear();
            foreach (var x in _f) _friends.Items.Add(new ListViewItem(new[] { x.Name, x.Stars.ToString(), x.Since }));

            _requests.Items.Clear();
            foreach (var x in _r)
            {
                bool incoming = string.Equals(x.To, AppConfig.Settings.UserName, StringComparison.OrdinalIgnoreCase);
                var it = new ListViewItem(new[] { incoming ? x.From : x.To, x.Created, incoming ? "входящий" : "исходящий" });
                it.ForeColor = incoming ? Gd.Lime : Gd.Gold;
                _requests.Items.Add(it);
            }

            _msg = (f.FromCache || r.FromCache) ? "Офлайн: показаны последние данные" :
                   $"друзей: {_f.Count}, запросов: {_r.Count}";
            Content.Invalidate();
        }

        async Task Send()
        {
            var n = _add.Text.Trim();
            if (n.Length < 3) { _msg = "Введи имя игрока"; Content.Invalidate(); return; }
            var r = await ApiClient.FriendRequestAsync(n);
            _msg = r.Ok ? "Запрос отправлен игроку " + n : r.Error;
            _add.Clear();
            await Reload();
        }

        async Task Answer(bool accept)
        {
            if (_requests.SelectedIndices.Count == 0) return;
            var req = _r[_requests.SelectedIndices[0]];
            bool incoming = string.Equals(req.To, AppConfig.Settings.UserName, StringComparison.OrdinalIgnoreCase);
            if (!incoming) { _msg = "Это твой исходящий запрос"; Content.Invalidate(); return; }
            var r = accept ? await ApiClient.FriendAcceptAsync(req.From) : await ApiClient.FriendDeclineAsync(req.From);
            _msg = r.Ok ? (accept ? "Теперь вы друзья" : "Запрос отклонён") : r.Error;
            await Reload();
        }

        async Task Remove()
        {
            if (_friends.SelectedIndices.Count == 0) return;
            var name = _f[_friends.SelectedIndices[0]].Name;
            if (!GdDialog.Confirm(this, "Друзья", "Удалить " + name + " из друзей?")) return;
            var r = await ApiClient.FriendRemoveAsync(name);
            _msg = r.Ok ? "Удалён" : r.Error;
            await Reload();
        }

        protected override void OnRender(Graphics g)
        {
                        Header(g, "ДРУЗЬЯ", "запросы принимаются и отклоняются здесь и на сайте");
            using var f = Gd.Bold(11f);
            Gd.Outlined(g, "Мои друзья", f, new RectangleF(24, 112, 300, 24), Gd.Paper, StringAlignment.Near, thick: 2.2f);
            Gd.Outlined(g, "Запросы", f, new RectangleF(508, 112, 300, 24), Gd.Paper, StringAlignment.Near, thick: 2.2f);
            if (!string.IsNullOrEmpty(_msg))
                Gd.Outlined(g, _msg, f, new RectangleF(24, ClientSize.Height - 46, 700, 24), Gd.Gold, StringAlignment.Near, thick: 2.2f);
        }
    }
}
