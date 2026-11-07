using System;
using System.Drawing;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Окно создания вещи: вид, имя, два цвета и узор. Предпросмотр рисуется
    /// тут же, так что выбирать можно глазами, а не наугад.
    ///
    /// В режиме «из файла» остаются только вид и имя — рисунок берётся из
    /// выбранной картинки.
    /// </summary>
    public class CosmeticEditForm : Form
    {
        public CosmeticKind Kind = CosmeticKind.Cape;
        public string ItemName = "Новая вещь";
        public Color BaseColor = Color.FromArgb(24, 32, 56);
        public Color Accent = Color.FromArgb(96, 150, 255);
        public CosmeticStore.Pattern Pattern = CosmeticStore.Pattern.Градиент;
        public string Letter = "N";

        /// <summary>Рисунок берётся из файла — узор и цвета не нужны.</summary>
        public bool ImportMode
        {
            get { return _import; }
            set { _import = value; Relayout(); }
        }

        private bool _import;

        private readonly NareTextBox _name = new NareTextBox { Placeholder = "Название" };
        private readonly VirtualList _kinds = new VirtualList { ItemHeight = 34, EmptyText = "" };
        private readonly VirtualList _patterns = new VirtualList { ItemHeight = 30, EmptyText = "" };
        private readonly Panel _preview = new Panel { BackColor = Color.FromArgb(16, 20, 29) };

        private readonly NareButton _base = new NareButton
        { Text = "Основной", Style = BtnStyle.Ghost, Size = new Size(130, 38) };

        private readonly NareButton _accent = new NareButton
        { Text = "Узор", Style = BtnStyle.Ghost, Size = new Size(130, 38) };

        private readonly NareButton _ok = new NareButton
        { Text = "Создать", Style = BtnStyle.Accent, Size = new Size(160, 42) };

        private readonly NareButton _cancel = new NareButton
        { Text = "Отмена", Style = BtnStyle.Ghost, Size = new Size(130, 42) };

        private static readonly CosmeticKind[] AllKinds =
        { CosmeticKind.Cape, CosmeticKind.Hat, CosmeticKind.Backpack };

        private static readonly string[] KindNames = { "Плащ", "Шляпа", "Рюкзак" };

        public CosmeticEditForm() : this(null) { }

        public CosmeticEditForm(string suggestedName)
        {
            if (!string.IsNullOrWhiteSpace(suggestedName)) ItemName = suggestedName.Trim();

            Text = "Вещь для персонажа";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(10f);
            ClientSize = new Size(640, 470);

            _name.Text = ItemName;
            _name.Inner.TextChanged += delegate { ItemName = _name.Text; };

            var kinds = new System.Collections.Generic.List<object>();
            foreach (string k in KindNames) kinds.Add(k);
            _kinds.Renderer = DrawRow;
            _kinds.SetItems(kinds);
            _kinds.SelectedIndex = 0;
            _kinds.SelectionChanged += delegate
            {
                int i = _kinds.SelectedIndex;
                if (i >= 0 && i < AllKinds.Length) Kind = AllKinds[i];
                _preview.Invalidate();
            };

            var patterns = new System.Collections.Generic.List<object>();
            foreach (CosmeticStore.Pattern p in Enum.GetValues(typeof(CosmeticStore.Pattern)))
                patterns.Add(p.ToString());
            _patterns.Renderer = DrawRow;
            _patterns.SetItems(patterns);
            _patterns.SelectedIndex = (int)Pattern;
            _patterns.SelectionChanged += delegate
            {
                int i = _patterns.SelectedIndex;
                if (i >= 0) Pattern = (CosmeticStore.Pattern)i;
                _preview.Invalidate();
            };

            _base.Click += delegate { BaseColor = Pick(BaseColor); _preview.Invalidate(); Invalidate(); };
            _accent.Click += delegate { Accent = Pick(Accent); _preview.Invalidate(); Invalidate(); };

            _preview.Paint += PaintPreview;

            _ok.Click += delegate
            {
                if (string.IsNullOrWhiteSpace(_name.Text)) { _name.Inner.Focus(); return; }
                ItemName = _name.Text.Trim();
                DialogResult = DialogResult.OK;
                Close();
            };

            _cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(_name);
            Controls.Add(_kinds);
            Controls.Add(_patterns);
            Controls.Add(_preview);
            Controls.Add(_base);
            Controls.Add(_accent);
            Controls.Add(_ok);
            Controls.Add(_cancel);

            Relayout();
        }

        private void Relayout()
        {
            int pad = 24;

            _name.SetBounds(pad, 64, 300, 38);
            _kinds.SetBounds(pad, 134, 300, 108);

            _patterns.SetBounds(pad, 290, 300, 124);
            _base.Location = new Point(pad, 246);
            _accent.Location = new Point(pad + 166, 246);

            _preview.SetBounds(350, 64, 266, 350);

            _patterns.Visible = !_import;
            _base.Visible = !_import;
            _accent.Visible = !_import;

            _ok.Text = _import ? "Добавить" : "Создать";
            _ok.Location = new Point(ClientSize.Width - pad - _ok.Width, 418);
            _cancel.Location = new Point(ClientSize.Width - pad - _ok.Width - _cancel.Width - 10, 418);

            Invalidate();
        }

        private Color Pick(Color current)
        {
            using (var dlg = new ColorDialog { Color = current, FullOpen = true })
                return dlg.ShowDialog(this) == DialogResult.OK ? dlg.Color : current;
        }

        private void DrawRow(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var box = Rectangle.Inflate(r, -3, -2);
            if (box.Width <= 0 || box.Height <= 0) return;

            if (selected) Theme.FillRound(g, box, 7, Color.FromArgb(50, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 7, Color.FromArgb(16, 255, 255, 255));

            Theme.Text1(g, item == null ? "" : item.ToString(), Theme.F(9.8f),
                selected ? Theme.Text : Theme.Muted,
                new Rectangle(box.X + 12, box.Y, box.Width - 20, box.Height));
        }

        private void PaintPreview(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var area = _preview.ClientRectangle;

            Theme.Smooth(g);
            Theme.DrawRound(g, new Rectangle(0, 0, area.Width - 1, area.Height - 1), 10, Theme.Border);

            if (_import)
            {
                Theme.Text1(g, "Рисунок возьмётся из выбранного файла",
                    Theme.F(9.5f), Theme.Muted, area, StringAlignment.Center, StringAlignment.Center);
                return;
            }

            using (var image = CosmeticStore.Draw(Kind, BaseColor, Accent, Pattern, Letter))
            {
                Rectangle src = Kind == CosmeticKind.Cape
                    ? new Rectangle(1, 1, 10, 16)
                    : new Rectangle(0, 0, image.Width, image.Height);

                float k = Math.Min((area.Width - 60f) / src.Width, (area.Height - 80f) / src.Height);
                int w = Math.Max(8, (int)(src.Width * k));
                int h = Math.Max(8, (int)(src.Height * k));

                var box = new Rectangle((area.Width - w) / 2, (area.Height - h) / 2 - 10, w, h);

                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                try { g.DrawImage(image, box, src, GraphicsUnit.Pixel); }
                catch { }

                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                Theme.DrawRound(g, box, 4, Color.FromArgb(60, 255, 255, 255));
            }

            Theme.Text1(g, "Так будет выглядеть вещь", Theme.F(8.8f), Theme.Dim,
                new Rectangle(0, area.Height - 28, area.Width, 20), StringAlignment.Center);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.Smooth(g);

            int pad = 24;

            Theme.Text1(g, _import ? "Вещь из картинки" : "Новая вещь",
                Theme.F(14f, FontStyle.Bold), Theme.Text, new Rectangle(pad, 18, 400, 26));

            Theme.Text1(g, "Название", Theme.F(9f), Theme.Dim, new Rectangle(pad, 44, 300, 18));
            Theme.Text1(g, "Что это", Theme.F(9f), Theme.Dim, new Rectangle(pad, 114, 300, 18));

            if (_import) return;

            Theme.Text1(g, "Узор", Theme.F(9f), Theme.Dim, new Rectangle(pad, 270, 300, 18));

            // квадратики выбранных цветов рядом с кнопками
            var a = new Rectangle(_base.Right + 6, _base.Top + 9, 20, 20);
            var b = new Rectangle(_accent.Right + 6, _accent.Top + 9, 20, 20);

            using (var brush = new SolidBrush(BaseColor)) g.FillRectangle(brush, a);
            using (var brush = new SolidBrush(Accent)) g.FillRectangle(brush, b);

            Theme.DrawRound(g, a, 3, Theme.Border);
            Theme.DrawRound(g, b, 3, Theme.Border);
        }
    }
}
