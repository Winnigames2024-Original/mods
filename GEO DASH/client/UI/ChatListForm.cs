using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    /// <summary>Все переписки игрока: друзья, другие игроки и поддержка.</summary>
    public class ChatListForm : GdForm
    {
        readonly ListBox _list;
        List<ChatHeadDto> _heads = new();
        string _msg = "";

        public ChatListForm()
        {
            Text = "GEO DASH — Сообщения";
            ClientSize = new Size(900, 640);
            BgA = Color.FromArgb(40, 120, 230); BgId = 8; GroundId = 2;

            _list = new ListBox
            {
                Left = 40, Top = 118, Width = 520, Height = 430,
                BackColor = Color.FromArgb(18, 16, 44), ForeColor = Gd.Paper,
                BorderStyle = BorderStyle.FixedSingle, Font = Gd.Reg(10.5f), IntegralHeight = false
            };
            Host(_list);
            _list.DoubleClick += (s, e) => OpenSelected();

            int x = 588, y = 118;
            GdButton B(string t, GdIcon ic, Color c1, Color c2, EventHandler h)
            {
                var b = Add(t, ic, x, y, 272, 54, c1, c2, h);
                y += 62;
                return b;
            }

            B("ОТКРЫТЬ", GdIcon.Online, Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100),
                (s, e) => OpenSelected());
            B("ПОДДЕРЖКА", GdIcon.Info, Color.FromArgb(255, 200, 90), Color.FromArgb(170, 115, 10),
                (s, e) => Nav.Go(new ChatForm(ChatForm.Support), async () => await Reload()));
            B("НАПИСАТЬ ИГРОКУ", GdIcon.Plus, Color.FromArgb(150, 190, 255), Color.FromArgb(45, 80, 170),
                (s, e) => NewChat());
            B("ДРУЗЬЯ", GdIcon.Friends, Color.FromArgb(255, 140, 200), Color.FromArgb(150, 40, 110),
                (s, e) => Nav.Go(new FriendsForm()));

            if (AppConfig.IsAdminUser)
                B("ОБРАЩЕНИЯ В ПОДДЕРЖКУ", GdIcon.Admin, Color.FromArgb(255, 110, 110), Color.FromArgb(150, 30, 40),
                    (s, e) => Nav.Go(new SupportInboxForm(), async () => await Reload()));

            Add("НАЗАД", GdIcon.Back, 24, 24, 150, 50,
                Color.FromArgb(160, 160, 190), Color.FromArgb(70, 70, 110), (s, e) => CloseScreen());

            Shown += async (s, e) => await Reload();
        }

        async Task Reload()
        {
            var r = await ApiClient.ChatsAsync();
            _heads = r.Ok && r.Data != null ? r.Data : new List<ChatHeadDto>();

            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var h in _heads)
            {
                string who = h.Support ? "ПОДДЕРЖКА" : h.Peer;
                string mark = h.Unread > 0 ? $"  ({h.Unread} новых)" : "";
                _list.Items.Add($"{who}{mark} — {Short(h.Last)}");
            }
            _list.EndUpdate();
            if (_list.Items.Count > 0) _list.SelectedIndex = 0;

            _msg = _heads.Count == 0
                ? "Переписок пока нет — напиши игроку или в поддержку"
                : "";
            Content.Invalidate();
        }

        static string Short(string s) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length > 48 ? s.Substring(0, 46) + "…" : s);

        void OpenSelected()
        {
            int i = _list.SelectedIndex;
            if (i < 0 || i >= _heads.Count) { _msg = "Выбери переписку"; Content.Invalidate(); return; }
            var h = _heads[i];
            Nav.Go(new ChatForm(h.Support ? ChatForm.Support : h.Peer), async () => await Reload());
        }

        void NewChat()
        {
            string name = GdDialog.Prompt(this, "Написать игроку", "Имя игрока:");
            if (string.IsNullOrWhiteSpace(name)) return;
            Nav.Go(new ChatForm(name.Trim()), async () => await Reload());
        }

        protected override void OnRender(Graphics g)
        {
            Header(g, "СООБЩЕНИЯ", "чаты с игроками и поддержка");
            if (!string.IsNullOrEmpty(_msg))
                using (var f = Gd.Bold(11f))
                    Gd.Outlined(g, _msg, f, new RectangleF(40, ClientSize.Height - 62, ClientSize.Width - 80, 30),
                        Gd.Gold, thick: 2.4f);
        }
    }

    /// <summary>Обращения в поддержку — список для админа, ответ уходит от имени SUPPORT.</summary>
    public class SupportInboxForm : GdForm
    {
        readonly ListBox _list;
        List<SupportHeadDto> _items = new();
        string _msg = "";

        public SupportInboxForm()
        {
            Text = "GEO DASH — Обращения в поддержку";
            ClientSize = new Size(900, 620);
            BgA = Color.FromArgb(220, 80, 80); BgId = 15; GroundId = 6;

            _list = new ListBox
            {
                Left = 40, Top = 118, Width = 520, Height = 410,
                BackColor = Color.FromArgb(18, 16, 44), ForeColor = Gd.Paper,
                BorderStyle = BorderStyle.FixedSingle, Font = Gd.Reg(10.5f), IntegralHeight = false
            };
            Host(_list);
            _list.DoubleClick += (s, e) => Answer();

            Add("ОТВЕТИТЬ", GdIcon.Online, 588, 118, 272, 54,
                Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100), (s, e) => Answer());
            Add("ОБНОВИТЬ", GdIcon.Refresh, 588, 180, 272, 54,
                Color.FromArgb(120, 220, 255), Color.FromArgb(20, 110, 180), async (s, e) => await Reload());
            Add("НАЗАД", GdIcon.Back, 24, 24, 150, 50,
                Color.FromArgb(160, 160, 190), Color.FromArgb(70, 70, 110), (s, e) => CloseScreen());

            Shown += async (s, e) => await Reload();
        }

        async Task Reload()
        {
            var r = await ApiClient.SupportListAsync();
            _items = r.Ok && r.Data != null ? r.Data : new List<SupportHeadDto>();

            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var h in _items)
                _list.Items.Add((h.Waiting ? "● " : "  ") + h.User + " — " + h.Last);
            _list.EndUpdate();
            if (_list.Items.Count > 0) _list.SelectedIndex = 0;

            _msg = _items.Count == 0 ? "Обращений нет" : "● — ждут ответа";
            Content.Invalidate();
        }

        void Answer()
        {
            int i = _list.SelectedIndex;
            if (i < 0 || i >= _items.Count) { _msg = "Выбери обращение"; Content.Invalidate(); return; }
            Nav.Go(new ChatForm(_items[i].User, asSupport: true), async () => await Reload());
        }

        protected override void OnRender(Graphics g)
        {
            Header(g, "ОБРАЩЕНИЯ В ПОДДЕРЖКУ", "ответ уходит игроку от имени SUPPORT");
            if (!string.IsNullOrEmpty(_msg))
                using (var f = Gd.Bold(11f))
                    Gd.Outlined(g, _msg, f, new RectangleF(40, ClientSize.Height - 60, ClientSize.Width - 80, 30),
                        Gd.Gold, thick: 2.4f);
        }
    }
}
