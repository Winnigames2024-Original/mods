using GEO_DASH.Audio;
using GEO_DASH.Core;
using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    public class DailyForm : GdForm
    {
        DailyDto _daily, _weekly;
        bool _weeklySelected;
        string _msg = "";

        public DailyForm()
        {
            Text = "GEO DASH — Daily / Weekly";
            ClientSize = new Size(900, 600);
            BgA = Color.FromArgb(56, 24, 86);

            Add("DAILY", GdIcon.Daily, 60, 110, 220, 56, Color.FromArgb(120, 200, 255), Color.FromArgb(30, 90, 190),
                (s, e) => { _weeklySelected = false; Content.Invalidate(); });
            Add("WEEKLY", GdIcon.Weekly, 300, 110, 220, 56, Color.FromArgb(220, 150, 255), Color.FromArgb(120, 50, 190),
                (s, e) => { _weeklySelected = true; Content.Invalidate(); });

            Add("ИГРАТЬ", GdIcon.Play, 60, ClientSize.Height - 90, 250, 60, Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100),
                async (s, e) => await Play());
            Add("СОХРАНИТЬ ОФЛАЙН", GdIcon.Download, 326, ClientSize.Height - 90, 250, 60,
                Color.FromArgb(120, 220, 255), Color.FromArgb(20, 110, 180), async (s, e) => await Save());
            Add("НАЗАД", GdIcon.Back, 600, ClientSize.Height - 90, 230, 60,
                Color.FromArgb(160, 160, 190), Color.FromArgb(70, 70, 110), (s, e) => Close());

            Load += async (s, e) => await Reload();
        }

        async Task Reload()
        {
            _msg = "Загружаю…"; Content.Invalidate();
            var d = await ApiClient.DailyAsync();
            var w = await ApiClient.WeeklyAsync();
            _daily = d.Data; _weekly = w.Data;
            _msg = d.FromCache || w.FromCache ? "Офлайн: последние скачанные" : "";
            Content.Invalidate();
        }

        LevelDto Cur => (_weeklySelected ? _weekly : _daily)?.Level;

        async Task Play()
        {
            var l = Cur;
            if (l == null) return;
            var lv = await LocalStore.DownloadAsync(l.Id, true, new Progress<string>(s => { _msg = s; Content.Invalidate(); }));
            if (lv == null) { _msg = "Нет сети и уровень не сохранён офлайн"; Content.Invalidate(); return; }
            MusicPlayer.Stop();
            using (var f = new GameForm(lv.DeepClone())) f.ShowDialog(this);
            _msg = ""; Content.Invalidate();
        }

        async Task Save()
        {
            var l = Cur;
            if (l == null) return;
            await LocalStore.DownloadAsync(l.Id, true, new Progress<string>(s => { _msg = s; Content.Invalidate(); }));
            _msg = "Сохранено в SAVED";
            Content.Invalidate();
        }

        protected override void OnRender(Graphics g)
        {
                        Header(g, _weeklySelected ? "УРОВЕНЬ НЕДЕЛИ" : "УРОВЕНЬ ДНЯ",
                "скачиваются автоматически и играются без интернета");

            var dto = _weeklySelected ? _weekly : _daily;
            var card = new RectangleF(60, 190, ClientSize.Width - 120, 280);
            Gd.Plate(g, card, Color.FromArgb(46, 34, 92), Color.FromArgb(22, 16, 52), 20f, 4f);

            if (dto?.Level == null)
            {
                using var f0 = Gd.Bold(13f);
                Gd.Outlined(g, "Уровень не назначен (или нет связи с сервером)", f0, card, Gd.Gold, thick: 2.6f);
                return;
            }

            var l = dto.Level;
            var face = new RectangleF(card.X + 30, card.Y + 60, 110, 110);
            Gd.DrawFace(g, face, (Difficulty)l.Difficulty);

            using (var f = Gd.Title(24f))
                Gd.Outlined(g, l.Name, f, new RectangleF(card.X + 170, card.Y + 26, card.Width - 200, 40), Gd.Paper, StringAlignment.Near, thick: 3.6f);
            using (var f = Gd.Bold(11f))
            {
                Gd.Outlined(g, "автор: " + l.Author, f, new RectangleF(card.X + 172, card.Y + 72, 400, 22), Gd.Neon, StringAlignment.Near, thick: 2.2f);
                Gd.Outlined(g, $"сложность: {Difficulties.Label((Difficulty)l.Difficulty)}   ★ {l.Stars}   ♥ {l.Likes}   ↓ {l.Downloads}", f,
                    new RectangleF(card.X + 172, card.Y + 98, 600, 22), Color.FromArgb(220, 210, 225, 255), StringAlignment.Near, thick: 2.2f);
                Gd.Outlined(g, "музыка: " + (l.SongId < 0 ? "встроенная" : "#" + l.SongId + " " + l.SongName), f,
                    new RectangleF(card.X + 172, card.Y + 124, 600, 22), Color.FromArgb(220, 210, 225, 255), StringAlignment.Near, thick: 2.2f);

                var left = TimeSpan.FromSeconds(Math.Max(0, dto.SecondsLeft));
                Gd.Outlined(g, $"сменится через {left.Days}д {left.Hours}ч {left.Minutes}м", f,
                    new RectangleF(card.X + 172, card.Y + 156, 600, 22), Gd.Gold, StringAlignment.Near, thick: 2.2f);
                Gd.Outlined(g, LocalStore.IsSaved(l.Id) ? "✔ сохранён офлайн" : "не сохранён офлайн", f,
                    new RectangleF(card.X + 172, card.Y + 184, 600, 22),
                    LocalStore.IsSaved(l.Id) ? Gd.Lime : Color.FromArgb(220, 180, 180, 200), StringAlignment.Near, thick: 2.2f);

                if (!string.IsNullOrEmpty(_msg))
                    Gd.Outlined(g, _msg, f, new RectangleF(60, card.Bottom + 10, 700, 24), Gd.Gold, StringAlignment.Near, thick: 2.2f);
            }
        }
    }
}
