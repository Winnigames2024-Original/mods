using GEO_DASH.Audio;
using GEO_DASH.Core;
using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    /// <summary>Меню PLAY — основные уровни игры (Easy / Hard / Insane).</summary>
    public class PlayMenuForm : GdForm
    {
        readonly List<LevelData> _levels;
        int _index;
        GdButton _bPlay, _bPractice, _bPrev, _bNext, _bBack;

        public PlayMenuForm()
        {
            Text = "GEO DASH — Основные уровни";
            ClientSize = new Size(1040, 640);
            BgA = Color.FromArgb(120, 60, 230); BgId = 11; GroundId = 2;
            _levels = OfficialLevels.EnsureFiles();

            _bPrev = Add("", GdIcon.Back, 24, 300, 60, 60, Color.FromArgb(120, 130, 170), Color.FromArgb(50, 56, 90),
                (s, e) => Move(-1));
            _bNext = Add("", GdIcon.Play, ClientSize.Width - 84, 300, 60, 60, Color.FromArgb(120, 130, 170), Color.FromArgb(50, 56, 90),
                (s, e) => Move(1));
            _bPlay = Add("ИГРАТЬ", GdIcon.Play, 0, 0, 240, 68, Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100),
                (s, e) => Play(false));
            _bPractice = Add("ТРЕНИРОВКА", GdIcon.Check, 0, 0, 240, 56, Color.FromArgb(120, 190, 255), Color.FromArgb(30, 90, 190),
                (s, e) => Play(true));
            _bBack = Add("НАЗАД", GdIcon.Back, 20, 20, 150, 50, Color.FromArgb(160, 160, 190), Color.FromArgb(70, 70, 110),
                (s, e) => Close());

            Resize += (s, e) => Layout2();
            Layout2();
            Shown += (s, e) => PlayPreview();
        }

        void Layout2()
        {
            if (_bPlay == null) return;
            int cx = ClientSize.Width / 2;
            _bPlay.Location = new Point(cx - 120, ClientSize.Height - 150);
            _bPractice.Location = new Point(cx - 120, ClientSize.Height - 78);
            _bPrev.Location = new Point(24, ClientSize.Height / 2 - 30);
            _bNext.Location = new Point(ClientSize.Width - 84, ClientSize.Height / 2 - 30);
            Content.Invalidate();
        }

        void Move(int d)
        {
            Sfx.Play("click");
            _index = (_index + d + _levels.Count) % _levels.Count;
            PlayPreview();
            Content.Invalidate();
        }

        void PlayPreview()
        {
            var lv = _levels[_index];
            _ = MusicPlayer.PlayAsync(lv.Settings.SongId, 0, 0.55, true);
        }

        void Play(bool practice)
        {
            var lv = _levels[_index];
            MusicPlayer.Stop();
            using (var f = new GameForm(lv.DeepClone(), practice))
                f.ShowDialog(this);
            PlayPreview();
            Content.Invalidate();
        }

        protected override void OnRender(Graphics g)
        {
                        Header(g, "ОСНОВНЫЕ УРОВНИ", "музыка встроена — работает без интернета");

            var lv = _levels[_index];
            var st = lv.Settings;
            var prog = LocalStore.GetProgress(lv);

            var card = new RectangleF(ClientSize.Width / 2f - 340, 120, 680, 320);
            Gd.Plate(g, card, Color.FromArgb(44, 38, 92), Color.FromArgb(22, 18, 54), 22f, 4f);

            // лицо сложности
            var face = new RectangleF(card.X + 34, card.Y + 60, 120, 120);
            Gd.DrawFace(g, face, st.Difficulty);
            using (var f = Gd.Bold(12f))
                Gd.Outlined(g, Difficulties.Label(st.Difficulty), f,
                    new RectangleF(face.X - 20, face.Bottom + 8, face.Width + 40, 24), Difficulties.Tint(st.Difficulty), thick: 2.6f);

            // звёзды
            int stars = Difficulties.Stars(st.Difficulty);
            using (var f = Gd.Bold(12f))
                Gd.Outlined(g, stars.ToString(), f, new RectangleF(face.X + 46, face.Y - 36, 40, 26), Gd.Gold, thick: 2.4f);
            Gd.DrawStar(g, new RectangleF(face.X + 20, face.Y - 36, 26, 26), Gd.Gold);

            // название и данные
            using (var f = Gd.Title(28f))
                Gd.Outlined(g, st.Name, f, new RectangleF(card.X + 190, card.Y + 30, 460, 44), Gd.Paper, StringAlignment.Near, thick: 4f);
            using (var f = Gd.Bold(11f))
            {
                Gd.Outlined(g, "автор: " + st.Author, f, new RectangleF(card.X + 192, card.Y + 78, 460, 22), Gd.Neon, StringAlignment.Near, thick: 2.2f);
                Gd.Outlined(g, "музыка: " + st.SongName + "  (встроенный трек)", f,
                    new RectangleF(card.X + 192, card.Y + 102, 460, 22), Color.FromArgb(220, 200, 220, 255), StringAlignment.Near, thick: 2.2f);
                Gd.Outlined(g, $"длина: {st.LengthUnits / 30:0} клеток   объектов: {lv.Objects.Count}", f,
                    new RectangleF(card.X + 192, card.Y + 126, 460, 22), Color.FromArgb(200, 180, 200, 240), StringAlignment.Near, thick: 2.2f);
            }

            // прогресс
            var bar = new RectangleF(card.X + 192, card.Y + 170, 440, 26);
            using (var br = new SolidBrush(Color.FromArgb(160, 10, 8, 26))) g.FillRectangle(br, bar);
            using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(bar, Gd.Neon, Gd.Lime,
                       System.Drawing.Drawing2D.LinearGradientMode.Horizontal))
                g.FillRectangle(br, new RectangleF(bar.X, bar.Y, (float)(bar.Width * Math.Clamp(prog.Best / 100.0, 0, 1)), bar.Height));
            using (var pen = new Pen(Gd.Ink, 3f)) g.DrawRectangle(pen, bar.X, bar.Y, bar.Width, bar.Height);
            using (var f = Gd.Bold(11f))
                Gd.Outlined(g, $"{prog.Best:0}%" + (prog.Completed ? "  ПРОЙДЕН" : ""), f, bar, Gd.Paper, thick: 2.4f);

            // монеты
            float coinX = card.X + 192;
            for (int i = 1; i <= 3; i++)
            {
                Gd.DrawCoin(g, new RectangleF(coinX, card.Y + 210, 30, 30), prog.Coins >= i);
                coinX += 40;
            }
            using (var f = Gd.Bold(10f))
                Gd.Outlined(g, $"монеты: {prog.Coins}/3   попыток: {prog.Attempts}", f,
                    new RectangleF(card.X + 320, card.Y + 212, 320, 24), Color.FromArgb(220, 210, 220, 255), StringAlignment.Near, thick: 2.2f);

            // точки-индикаторы
            float dx = ClientSize.Width / 2f - (_levels.Count * 18) / 2f;
            for (int i = 0; i < _levels.Count; i++)
            {
                var r = new RectangleF(dx + i * 18, card.Bottom + 16, 12, 12);
                using var br = new SolidBrush(i == _index ? Gd.Neon : Color.FromArgb(90, 255, 255, 255));
                g.FillEllipse(br, r);
            }
        }
    }
}
