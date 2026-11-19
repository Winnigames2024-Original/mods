using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    /// <summary>
    /// Рекорды игроков: звёзды, демоны, монетки (только за чужие опубликованные уровни),
    /// шары, созданные и пройденные уровни.
    /// </summary>
    public class RecordsForm : GdForm
    {
        static readonly (string key, string title, GdIcon icon)[] Kinds =
        {
            ("stars",     "ЗВЁЗДЫ",            GdIcon.Star),
            ("demons",    "ДЕМОНЫ",            GdIcon.Admin),
            ("coins",     "МОНЕТКИ",           GdIcon.Coin),
            ("orbs",      "ШАРЫ",              GdIcon.Online),
            ("created",   "СОЗДАНО УРОВНЕЙ",   GdIcon.Edit),
            ("completed", "ПРОЙДЕНО УРОВНЕЙ",  GdIcon.Check)
        };

        readonly ListBox _list;
        readonly List<GdButton> _tabs = new();
        int _kind;
        List<AccountDto> _rows = new();
        string _msg = "";

        public RecordsForm()
        {
            Text = "GEO DASH — Рекорды";
            ClientSize = new Size(1000, 680);
            BgA = Color.FromArgb(210, 150, 30); BgId = 12; GroundId = 5;

            int x = 32;
            for (int i = 0; i < Kinds.Length; i++)
            {
                int idx = i;
                var b = Add("", Kinds[i].icon, x, 104, 150, 72,
                    Color.FromArgb(150, 190, 255), Color.FromArgb(45, 80, 170),
                    async (s, e) => await Pick(idx));
                b.Caption = Kinds[i].title;
                _tabs.Add(b);
                x += 156;
            }

            _list = new ListBox
            {
                Left = 32, Top = 194, Width = ClientSize.Width - 64, Height = 400,
                BackColor = Color.FromArgb(18, 16, 44), ForeColor = Gd.Paper,
                BorderStyle = BorderStyle.FixedSingle, Font = Gd.Reg(11f), IntegralHeight = false
            };
            Host(_list);
            _list.DoubleClick += (s, e) =>
            {
                int i = _list.SelectedIndex;
                if (i >= 0 && i < _rows.Count) Nav.Go(new ProfileForm(_rows[i].Name));
            };

            Add("ПРОФИЛЬ ИГРОКА", GdIcon.Profile, 32, ClientSize.Height - 68, 260, 50,
                Color.FromArgb(90, 190, 255), Color.FromArgb(20, 90, 170),
                (s, e) =>
                {
                    int i = _list.SelectedIndex;
                    if (i >= 0 && i < _rows.Count) Nav.Go(new ProfileForm(_rows[i].Name));
                });

            Add("НАЗАД", GdIcon.Back, ClientSize.Width - 212, ClientSize.Height - 68, 180, 50,
                Color.FromArgb(160, 160, 190), Color.FromArgb(70, 70, 110), (s, e) => CloseScreen());

            Shown += async (s, e) => await Pick(0);
        }

        async Task Pick(int i)
        {
            _kind = i;
            for (int k = 0; k < _tabs.Count; k++)
                _tabs[k].Color1 = k == i ? Color.FromArgb(255, 210, 90) : Color.FromArgb(150, 190, 255);
            foreach (var t in _tabs) t.Invalidate();

            _msg = "Загружаю…";
            Content.Invalidate();

            var r = await ApiClient.TopAsync(Kinds[i].key);
            _rows = r.Ok && r.Data != null ? r.Data : new List<AccountDto>();

            _list.BeginUpdate();
            _list.Items.Clear();
            for (int k = 0; k < _rows.Count; k++)
            {
                var a = _rows[k];
                int score = a.Score != 0 ? a.Score : Value(a);
                _list.Items.Add($"{k + 1,3}.  {a.Name,-22}  {score,8}    ★{a.Stars}  ◆{a.Demons}  ●{a.Coins}");
            }
            _list.EndUpdate();

            _msg = _rows.Count == 0
                ? (ApiClient.Online ? "Пока пусто" : "Нет сети — рекорды недоступны")
                : "";
            Content.Invalidate();
        }

        int Value(AccountDto a) => Kinds[_kind].key switch
        {
            "coins" => a.Coins,
            "demons" => a.Demons,
            "orbs" => a.Orbs,
            "created" => a.Levels,
            "completed" => a.Completed,
            _ => a.Stars
        };

        protected override void OnRender(Graphics g)
        {
            Header(g, "РЕКОРДЫ", Kinds[_kind].key == "coins"
                ? "монетки считаются только за чужие опубликованные уровни"
                : "лучшие игроки " + AppConfig.ServerLabel);

            if (!string.IsNullOrEmpty(_msg))
                using (var f = Gd.Bold(11f))
                    Gd.Outlined(g, _msg, f, new RectangleF(32, 610, ClientSize.Width - 64, 28), Gd.Gold, thick: 2.4f);
        }
    }
}
