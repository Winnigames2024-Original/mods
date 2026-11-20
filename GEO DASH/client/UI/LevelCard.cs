using System.Drawing.Drawing2D;
using GEO_DASH.Core;
using GEO_DASH.Net;
using GEO_DASH.Audio;

namespace GEO_DASH.UI
{
    /// <summary>Мини-картинка уровня: начало уровня в маленьком окошке.</summary>
    public static class LevelThumb
    {
        public static void Draw(Graphics g, RectangleF r, LevelData lv, double fromX = 0)
        {
            if (lv == null || r.Width < 8 || r.Height < 8) return;
            var st = lv.Settings;
            var bg = Color.FromArgb(st.BgColor);
            var ground = Color.FromArgb(st.GroundColor);

            var old = g.Clip;
            using var path = Gd.Round(r, 10f);
            g.SetClip(path, CombineMode.Intersect);

            float scale = r.Height / 330f;      // ~11 клеток по высоте
            GdBackdrop.DrawBackground(g, r, st.BgId, bg, fromX * scale * 0.26, 0, Math.Max(0.25f, scale));

            float groundH = r.Height * 0.18f;
            var gr = new RectangleF(r.X, r.Bottom - groundH, r.Width, groundH);
            GdBackdrop.DrawGround(g, gr, st.GroundId, ground, Color.White, fromX * scale, Math.Max(0.25f, scale));

            // объекты
            float baseY = gr.Y;
            foreach (var o in lv.Objects)
            {
                var b = ObjectCatalog.Bounds(o);
                float x = r.X + (float)((b.X - fromX) * scale);
                if (x > r.Right + 20 || x + (float)(b.Width * scale) < r.X - 20) continue;
                float y = baseY - (float)((b.Y + b.Height) * scale);
                var cell = new RectangleF(x, y, Math.Max(2f, (float)(b.Width * scale)), Math.Max(2f, (float)(b.Height * scale)));
                if (cell.Bottom < r.Y - 10 || cell.Y > r.Bottom + 10) continue;
                ObjButton.DrawObject(g, cell, o);
            }

            g.Clip = old;
            using var pen = new Pen(Gd.Ink, 3f) { LineJoin = LineJoin.Round };
            g.DrawPath(pen, path);
        }
    }

    /// <summary>Карточка уровня для списков: картинка, лицо сложности, название и цифры.</summary>
    public class LevelCard : Control
    {
        public LevelData Level;
        public bool Selected;
        public string Line2 = "";
        bool _hover;

        /// <summary>Нажали кнопку трека (скачать или удалить).</summary>
        public event EventHandler SongClicked;

        /// <summary>Последний клик пришёлся на кнопку трека — карточку открывать не надо.</summary>
        public bool LastClickOnSong { get; private set; }

        RectangleF _songBtn;

        public LevelCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(640, 136);
            MouseEnter += (s, e) => { _hover = true; Invalidate(); };
            MouseLeave += (s, e) => { _hover = false; Invalidate(); };
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            LastClickOnSong = _songBtn.Width > 1 && _songBtn.Contains(e.Location);
            base.OnMouseDown(e);
            if (!LastClickOnSong) return;
            SongClicked?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(2, 2, Width - 4, Height - 4);

            Gd.Plate(g, r,
                Selected ? Color.FromArgb(70, 110, 215) : _hover ? Color.FromArgb(52, 50, 102) : Color.FromArgb(40, 38, 84),
                Selected ? Color.FromArgb(26, 56, 150) : Color.FromArgb(22, 20, 52), 16f, 3.2f);

            if (Level == null) return;
            var st = Level.Settings;

            // картинка уровня
            var thumb = new RectangleF(r.X + 10, r.Y + 9, r.Height * 1.72f, r.Height - 18);
            LevelThumb.Draw(g, thumb, Level);

            // лицо сложности
            float fs = r.Height * 0.46f;
            var face = new RectangleF(thumb.Right + 12, r.Y + (r.Height - fs) / 2 - 8, fs, fs);
            Gd.DrawFace(g, face, st.Difficulty);
            using (var f2 = Gd.Bold(7.5f))
                Gd.Outlined(g, Difficulties.Label(st.Difficulty), f2,
                    new RectangleF(face.X - 14, face.Bottom + 2, face.Width + 28, 16),
                    Difficulties.Tint(st.Difficulty), thick: 2f);

            float tx = face.Right + 14;
            using (var f = Gd.Title(13f))
                Gd.Outlined(g, st.Name, f, new RectangleF(tx, r.Y + 10, r.Right - tx - 12, 26),
                    Gd.Paper, StringAlignment.Near, thick: 3f);

            var pr = LocalStore.GetProgress(Level);
            string line1 = (string.IsNullOrEmpty(st.Author) ? "" : "автор: " + st.Author + "   ·   ")
                           + "объектов: " + Level.Objects.Count
                           + "   ·   " + (st.SongId == 0 ? "без музыки" : st.SongId < 0 ? "встроенная музыка" : "трек #" + st.SongId);
            using (var f = Gd.Bold(9f))
            {
                Gd.Outlined(g, line1, f, new RectangleF(tx, r.Y + 38, r.Right - tx - 12, 20),
                    Color.FromArgb(225, 205, 220, 255), StringAlignment.Near, thick: 2.2f);
                string line2 = string.IsNullOrEmpty(Line2)
                    ? $"лучший результат: {pr.Best:0}%   ·   попыток: {pr.Attempts}" +
                      (st.ServerId > 0 ? "   ·   на сервере #" + st.ServerId : "")
                    : Line2;
                Gd.Outlined(g, line2, f, new RectangleF(tx, r.Y + 58, r.Right - tx - 12, 20),
                    pr.Completed ? Gd.Lime : Gd.Gold, StringAlignment.Near, thick: 2.2f);
            }

            // ---- строка трека прямо под уровнем, как в игре ----
            DrawSongRow(g, new RectangleF(r.X + 10, r.Bottom - 34, r.Width - 20, 28), st);

            // полоса прогресса справа
            var bar = new RectangleF(tx, r.Bottom - 48, Math.Max(40, r.Right - tx - 16), 9);
            using (var br = new SolidBrush(Color.FromArgb(150, 10, 12, 26)))
            using (var p1 = Gd.Round(bar, 4.5f))
                g.FillPath(br, p1);
            var fill = new RectangleF(bar.X, bar.Y, (float)(bar.Width * Math.Clamp(pr.Best / 100.0, 0, 1)), bar.Height);
            if (fill.Width > 2)
                using (var p2 = Gd.Round(fill, 4.5f))
                using (var br = new SolidBrush(pr.Completed ? Gd.Lime : Gd.Gold))
                    g.FillPath(br, p2);
        }

        /// <summary>Трек уровня: номер, название и кнопка «скачать» или «удалить».</summary>
        void DrawSongRow(Graphics g, RectangleF row, LevelSettings st)
        {
            _songBtn = RectangleF.Empty;

            using var plate = Gd.Round(row, 9f);
            using (var br = new SolidBrush(Color.FromArgb(150, 12, 10, 32))) g.FillPath(br, plate);
            using (var pen = new Pen(Color.FromArgb(120, 80, 90, 160), 2f)) g.DrawPath(pen, plate);

            string label;
            if (st.SongId == 0) label = "♪  без музыки";
            else if (st.SongId < 0) label = "♪  встроенный трек · " + (st.SongName ?? "");
            else label = "♪  #" + st.SongId + "  ·  " + (string.IsNullOrWhiteSpace(st.SongName) ? "трек" : st.SongName);

            bool cached = st.SongId > 0 && MusicPlayer.IsCached(st.SongId);
            float bw = st.SongId > 0 ? 104f : 0f;

            using (var f = Gd.Bold(9f))
                Gd.Outlined(g, label, f,
                    new RectangleF(row.X + 10, row.Y + 4, row.Width - bw - 22, 20),
                    cached ? Gd.Lime : Color.FromArgb(230, 205, 220, 255), StringAlignment.Near, thick: 2.2f);

            if (st.SongId <= 0) return;

            _songBtn = new RectangleF(row.Right - bw - 6, row.Y + 3, bw, row.Height - 6);
            var c1 = cached ? Color.FromArgb(235, 130, 130) : Color.FromArgb(130, 220, 255);
            var c2 = cached ? Color.FromArgb(150, 40, 50) : Color.FromArgb(25, 95, 180);

            Gd.ButtonPlate(g, _songBtn, c1, c2, 9f, false, false);
            using (var f = Gd.Bold(8.5f))
                Gd.Outlined(g, cached ? "УДАЛИТЬ" : "СКАЧАТЬ", f,
                    new RectangleF(_songBtn.X, _songBtn.Y + 4, _songBtn.Width, 18), Gd.Paper, thick: 2.2f);
        }
    }
}
