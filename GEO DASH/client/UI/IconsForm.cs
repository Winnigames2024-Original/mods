using GEO_DASH.Render;

namespace GEO_DASH.UI
{
    /// <summary>
    /// Внешний вид кубика: встроенные рисунки, цвета и своя картинка.
    /// Выбранное сразу видно на большом предпросмотре.
    /// </summary>
    public class IconsForm : GdForm
    {
        readonly List<RectangleF> _cells = new();
        string _msg = "";

        public IconsForm()
        {
            Text = "GEO DASH — Иконки";
            ClientSize = new Size(1000, 680);
            BgA = Color.FromArgb(60, 170, 230); BgId = 9; GroundId = 7;

            LoadCustom();

            Add("ЦВЕТ КУБИКА", GdIcon.Settings, 600, 150, 340, 54,
                Color.FromArgb(120, 220, 255), Color.FromArgb(20, 110, 180),
                (s, e) => PickColor(true));
            Add("ЦВЕТ РИСУНКА", GdIcon.Settings, 600, 212, 340, 54,
                Color.FromArgb(200, 170, 255), Color.FromArgb(100, 60, 180),
                (s, e) => PickColor(false));
            Add("СВОЯ КАРТИНКА", GdIcon.Upload, 600, 274, 340, 54,
                Color.FromArgb(255, 210, 90), Color.FromArgb(170, 120, 10),
                (s, e) => PickImage());
            Add("УБРАТЬ КАРТИНКУ", GdIcon.Cross, 600, 336, 340, 54,
                Color.FromArgb(230, 130, 130), Color.FromArgb(150, 40, 50),
                (s, e) =>
                {
                    AppConfig.Settings.IconImage = "";
                    AppConfig.Save();
                    IconKit.ClearCustom();
                    Render.PlayerIcons.Clear();
                    _msg = "Картинка убрана — снова встроенный рисунок";
                    Content.Invalidate();
                });

            Add("НАЗАД", GdIcon.Back, 24, 24, 150, 50,
                Color.FromArgb(160, 160, 190), Color.FromArgb(70, 70, 110), (s, e) => CloseScreen());

            Content.MouseDown += (s, e) =>
            {
                for (int i = 0; i < _cells.Count; i++)
                {
                    if (!_cells[i].Contains(e.Location)) continue;
                    AppConfig.Settings.IconShape = i + 1;
                    AppConfig.Save();
                    Render.PlayerIcons.Clear();
                    _msg = "Рисунок: " + IconKit.Name(i + 1);
                    Content.Invalidate();
                    return;
                }
            };
        }

        /// <summary>Подтянуть свою картинку из настроек.</summary>
        static void LoadCustom()
        {
            var p = AppConfig.Settings.IconImage;
            if (string.IsNullOrWhiteSpace(p) || !File.Exists(p)) { IconKit.ClearCustom(); return; }
            try { IconKit.SetCustom(p, Image.FromFile(p)); }
            catch { IconKit.ClearCustom(); }
        }

        void PickColor(bool main)
        {
            using var d = new ColorDialog
            {
                FullOpen = true,
                Color = Color.FromArgb(main ? AppConfig.Settings.IconColor1 : AppConfig.Settings.IconColor2)
            };
            if (d.ShowDialog(this) != DialogResult.OK) return;
            if (main) AppConfig.Settings.IconColor1 = d.Color.ToArgb();
            else AppConfig.Settings.IconColor2 = d.Color.ToArgb();
            AppConfig.Save();
            Render.PlayerIcons.Clear();
            Content.Invalidate();
        }

        void PickImage()
        {
            using var d = new OpenFileDialog
            {
                Title = "Картинка для кубика",
                Filter = "Картинки (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif"
            };
            if (d.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                // копию кладём в папку игры, чтобы картинка не пропала
                string dst = Path.Combine(AppConfig.Ensure(Path.Combine(AppConfig.DataDir, "icons")),
                                          "icon" + Path.GetExtension(d.FileName));
                File.Copy(d.FileName, dst, true);
                AppConfig.Settings.IconImage = dst;
                AppConfig.Save();
                IconKit.ClearCustom();
                IconKit.SetCustom(dst, Image.FromFile(dst));
                Render.PlayerIcons.Clear();
                _msg = "Картинка поставлена на кубик";
            }
            catch (Exception ex) { _msg = "Не получилось: " + ex.Message; }
            Content.Invalidate();
        }

        protected override void OnRender(Graphics g)
        {
            Header(g, "ИКОНКИ", "выбери рисунок, цвета или поставь свою картинку");

            var c1 = Color.FromArgb(AppConfig.Settings.IconColor1);
            var c2 = Color.FromArgb(AppConfig.Settings.IconColor2);

            // сетка рисунков
            _cells.Clear();
            int cols = 6, size = 78, gap = 12, x0 = 40, y0 = 150;
            for (int i = 0; i < IconKit.Count; i++)
            {
                int cx = x0 + (i % cols) * (size + gap);
                int cy = y0 + (i / cols) * (size + gap);
                var r = new RectangleF(cx, cy, size, size);
                _cells.Add(r);

                if (AppConfig.Settings.IconShape == i + 1)
                {
                    using var sel = new Pen(Gd.Gold, 4f);
                    g.DrawRectangle(sel, r.X - 5, r.Y - 5, r.Width + 10, r.Height + 10);
                }
                IconKit.Draw(g, r, i + 1, c1, c2);
            }

            // большой предпросмотр
            var prev = new RectangleF(600, 400, 180, 180);
            using (var f = Gd.Bold(11f))
                Gd.Outlined(g, "как выглядит в игре", f, new RectangleF(600, 376, 340, 22),
                    Color.FromArgb(225, 210, 230, 255), StringAlignment.Near, thick: 2.2f);
            IconKit.Draw(g, prev, AppConfig.Settings.IconShape, c1, c2);
            IconKit.Draw(g, new RectangleF(800, 400, 180, 180), AppConfig.Settings.IconShape, c1, c2, round: true);

            if (!string.IsNullOrEmpty(_msg))
                using (var f = Gd.Bold(11f))
                    Gd.Outlined(g, _msg, f, new RectangleF(40, ClientSize.Height - 58, ClientSize.Width - 80, 28),
                        Gd.Gold, thick: 2.4f);
        }
    }
}
