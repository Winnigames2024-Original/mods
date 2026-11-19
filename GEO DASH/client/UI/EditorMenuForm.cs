using GEO_DASH.Core;
using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    /// <summary>
    /// EDITOR — меню редактора: создать уровень, свои уровни, копии, скачанные уровни и песни.
    /// </summary>
    public class EditorMenuForm : GdForm
    {
        int _mine, _copies, _saved, _songs, _published;

        public EditorMenuForm()
        {
            Text = "GEO DASH — Editor";
            ClientSize = new Size(1060, 700);
            BgA = Color.FromArgb(235, 130, 20); BgId = 5; GroundId = 4;

            int bw = 250, bh = 150, gap = 22;
            int x0 = ClientSize.Width / 2 - (bw * 3 + gap * 2) / 2;
            int y0 = 186;

            Big("СОЗДАТЬ", "новый уровень с нуля", GdIcon.Plus, x0, y0, bw, bh,
                Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100), (s, e) => Create());
            Big("МОИ УРОВНИ", "править и публиковать", GdIcon.Edit, x0 + bw + gap, y0, bw, bh,
                Color.FromArgb(255, 180, 70), Color.FromArgb(175, 95, 10),
                (s, e) => Open(new MyLevelsForm(false)));
            Big("СКОПИРОВАННЫЕ", "копии уровней", GdIcon.Copy, x0 + (bw + gap) * 2, y0, bw, bh,
                Color.FromArgb(200, 160, 255), Color.FromArgb(100, 55, 185),
                (s, e) => Open(new MyLevelsForm(true)));

            y0 += bh + gap;
            Big("СОХРАНЁННЫЕ", "скачанные уровни", GdIcon.Download, x0, y0, bw, bh,
                Color.FromArgb(120, 230, 160), Color.FromArgb(30, 130, 80),
                (s, e) => Open(new LevelListForm(LevelListMode.Saved)));
            Big("МОИ ПЕСНИ", "что уже скачано", GdIcon.Music, x0 + bw + gap, y0, bw, bh,
                Color.FromArgb(255, 150, 215), Color.FromArgb(160, 40, 120),
                (s, e) => Open(new SongsForm()));
            Big("ОБЩИЙ СПИСОК", "все уровни одним списком", GdIcon.Search, x0 + (bw + gap) * 2, y0, bw, bh,
                Color.FromArgb(140, 200, 255), Color.FromArgb(35, 95, 190),
                (s, e) => Open(new LevelListForm(LevelListMode.My)));

            Add("НАЗАД", GdIcon.Back, 24, ClientSize.Height - 86, 170, 52,
                Color.FromArgb(170, 170, 200), Color.FromArgb(70, 70, 110), (s, e) => Close());

            Load += (s, e) => Reload();
        }

        GdButton Big(string text, string sub, GdIcon ic, int x, int y, int w, int h, Color c1, Color c2, EventHandler click)
        {
            var b = new GdButton
            {
                Icon = ic, Caption = text, Sub = sub, Left = x, Top = y, Width = w, Height = h,
                Color1 = c1, Color2 = c2, Radius = 20f
            };
            b.Click += click;
            return Host(b);
        }

        void Reload()
        {
            var all = LocalStore.MyLevels();
            _mine = all.Count(l => !l.Settings.IsCopy);
            _copies = all.Count(l => l.Settings.IsCopy);
            _published = all.Count(l => l.Settings.ServerId > 0);
            _saved = LocalStore.SavedLevels().Count;
            try { _songs = Directory.GetFiles(AppConfig.MusicDir).Count(f => f.EndsWith(".mp3") || f.EndsWith(".wav")); }
            catch { _songs = 0; }
            Content.Invalidate();
        }

        void Create()
        {
            var lv = new LevelData();
            lv.Settings.Name = "Уровень " + (LocalStore.MyLevels(false).Count + 1);
            lv.Settings.Author = AppConfig.Settings.UserName;
            LocalStore.SaveMyLevel(lv);
            using (var f = new EditorForm(lv)) f.ShowDialog(this);
            Reload();
        }

        void Open(Form f)
        {
            using (f) f.ShowDialog(this);
            Reload();
        }

        protected override void OnRender(Graphics g)
        {
            Header(g, "EDITOR", "создать, изменить и опубликовать уровень");

            var card = new RectangleF(ClientSize.Width / 2f - 330, 104, 660, 62);
            Gd.Glass(g, card, 16f);

            string[] stat =
            {
                "своих: " + _mine,
                "копий: " + _copies,
                "опубликовано: " + _published,
                "скачано: " + _saved,
                "песен: " + _songs
            };
            using var f = Gd.Bold(11.5f);
            float w = card.Width / stat.Length;
            for (int i = 0; i < stat.Length; i++)
                Gd.Outlined(g, stat[i], f, new RectangleF(card.X + w * i, card.Y + 19, w, 24),
                    i == 2 ? Gd.Lime : Gd.Paper, thick: 2.4f);
        }
    }
}
