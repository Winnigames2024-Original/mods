using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    /// <summary>Меню Creator — как в игре: Saved, Search, Daily, Weekly, Featured, Users, мои уровни.</summary>
    public class CreatorForm : GdForm
    {
        string _hint = "";

        public CreatorForm()
        {
            Text = "GEO DASH — Creator";
            ClientSize = new Size(1060, 680);
            BgA = Color.FromArgb(22, 36, 86);

            int bw = 230, bh = 120;
            int gapX = 24, gapY = 22;
            int x0 = ClientSize.Width / 2 - (bw * 4 + gapX * 3) / 2;
            int y0 = 150;

            GdButton Cell(int col, int row, string text, GdIcon ic, Color c1, Color c2, EventHandler h, string sub = "")
            {
                var b = Add(text, ic, x0 + col * (bw + gapX), y0 + row * (bh + gapY), bw, bh, c1, c2, h, sub);
                b.FontSize = 13f;
                return b;
            }

            Cell(0, 0, "SAVED", GdIcon.Download, Color.FromArgb(120, 230, 160), Color.FromArgb(30, 130, 80),
                (s, e) => Open(new LevelListForm(LevelListMode.Saved)), "скачанные уровни");
            Cell(1, 0, "SEARCH", GdIcon.Search, Color.FromArgb(90, 180, 255), Color.FromArgb(20, 80, 180),
                (s, e) => Open(new SearchForm(SearchMode.Levels)), "по имени и по ID");
            Cell(2, 0, "DAILY", GdIcon.Daily, Color.FromArgb(150, 200, 255), Color.FromArgb(40, 90, 190),
                (s, e) => Open(new DailyForm()), "уровень дня");
            Cell(3, 0, "WEEKLY", GdIcon.Weekly, Color.FromArgb(210, 140, 255), Color.FromArgb(110, 40, 180),
                (s, e) => Open(new DailyForm()), "уровень недели");

            Cell(0, 1, "FEATURED", GdIcon.Star, Color.FromArgb(255, 210, 90), Color.FromArgb(170, 110, 10),
                (s, e) => Open(new SearchForm(SearchMode.Featured)), "отобранные уровни");
            Cell(1, 1, "EPIC", GdIcon.Admin, Color.FromArgb(255, 130, 230), Color.FromArgb(160, 40, 140),
                (s, e) => Open(new SearchForm(SearchMode.Epic)), "лучшее из лучшего");
            Cell(2, 1, "USERS", GdIcon.Friends, Color.FromArgb(255, 150, 200), Color.FromArgb(160, 50, 110),
                (s, e) => Open(new SearchForm(SearchMode.Users)), "поиск игроков");
            Cell(3, 1, "TOP", GdIcon.Profile, Color.FromArgb(160, 180, 230), Color.FromArgb(60, 80, 150),
                (s, e) => Open(new SearchForm(SearchMode.Top)), "таблица лидеров");

            Cell(0, 2, "MY LEVELS", GdIcon.Edit, Color.FromArgb(255, 170, 60), Color.FromArgb(180, 90, 10),
                (s, e) => Open(new EditorMenuForm()), "редактор уровней");
            Cell(1, 2, "ONLINE", GdIcon.Online, Color.FromArgb(120, 220, 255), Color.FromArgb(20, 110, 180),
                (s, e) => Open(new OnlineForm()), "все уровни игроков");
            Cell(2, 2, "FRIENDS", GdIcon.Friends, Color.FromArgb(255, 170, 210), Color.FromArgb(150, 50, 110),
                (s, e) => Open(new FriendsForm()), "друзья и запросы");
            Cell(3, 2, "ACCOUNT", GdIcon.Profile, Color.FromArgb(120, 190, 255), Color.FromArgb(30, 90, 190),
                (s, e) => Open(new ProfileForm(AppConfig.Settings.UserName)), "профиль и статистика");

            Add("НАЗАД", GdIcon.Back, 24, ClientSize.Height - 78, 180, 54,
                Color.FromArgb(160, 160, 190), Color.FromArgb(70, 70, 110), (s, e) => Close());
        }

        void Open(Form f)
        {
            Audio.Sfx.Play("click");
            using (f) f.ShowDialog(this);
            Content.Invalidate();
        }

        protected override void OnRender(Graphics g)
        {
                        Header(g, "CREATOR", ApiClient.Online
                ? "сервер онлайн — уровни, поиск и игроки доступны"
                : "офлайн — работают SAVED и мои уровни");

            if (LocalStore.Queue.Count > 0)
                using (var f = Gd.Bold(10f))
                    Gd.Outlined(g, "в очереди на отправку: " + LocalStore.Queue.Count, f,
                        new RectangleF(24, ClientSize.Height - 36, 420, 22), Gd.Gold, StringAlignment.Near, thick: 2f);

            if (!string.IsNullOrEmpty(_hint))
                using (var f = Gd.Bold(10f))
                    Gd.Outlined(g, _hint, f, new RectangleF(240, ClientSize.Height - 60, 700, 22),
                        Gd.Paper, StringAlignment.Near, thick: 2f);
        }
    }
}
