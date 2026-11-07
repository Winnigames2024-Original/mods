using System;
using System.Drawing;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Список слоёв скина. Верхняя строка — самый верхний слой,
    /// слева от имени — глазок видимости.
    /// </summary>
    public class SkinLayerList : BaseControl
    {
        public SkinDocument Doc;

        /// <summary>Сменился активный слой.</summary>
        public event EventHandler Selected;

        /// <summary>Изменилась видимость — надо пересобрать картинку.</summary>
        public event EventHandler VisibilityChanged;

        /// <summary>Двойной щелчок по строке — просят переименовать слой.</summary>
        public event EventHandler RenameRequested;

        private const int RowH = 26;
        private const int EyeW = 24;

        private int _scroll;
        private int _hoverRow = -1;

        public SkinLayerList()
        {
            Cursor = Cursors.Hand;
        }

        private int Count { get { return Doc == null ? 0 : Doc.Layers.Count; } }
        private int Rows { get { return Math.Max(1, (Height - 8) / RowH); } }

        /// <summary>Строка на экране -> номер слоя (сверху вниз от верхнего слоя).</summary>
        private int LayerAt(int row)
        {
            int index = Count - 1 - (row + _scroll);
            return index >= 0 && index < Count ? index : -1;
        }

        public void ScrollToActive()
        {
            if (Doc == null) return;
            int row = Count - 1 - Doc.Active - _scroll;
            if (row < 0) _scroll += row;
            else if (row >= Rows) _scroll += row - Rows + 1;
            ClampScroll();
            Invalidate();
        }

        private void ClampScroll()
        {
            int max = Math.Max(0, Count - Rows);
            if (_scroll > max) _scroll = max;
            if (_scroll < 0) _scroll = 0;
        }

        // ------------------------------------------------------------ мышь

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (Doc == null) return;

            int row = (e.Y - 4) / RowH;
            int index = LayerAt(row);
            if (index < 0) return;

            if (e.X < EyeW + 4)
            {
                var layer = Doc.Layers[index];
                layer.Visible = !layer.Visible;
                Doc.Rebuild();
                Invalidate();

                var vh = VisibilityChanged;
                if (vh != null) vh(this, EventArgs.Empty);
                return;
            }

            if (Doc.Active == index) return;

            Doc.Active = index;
            Invalidate();

            var h = Selected;
            if (h != null) h(this, EventArgs.Empty);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (Doc == null || e.X < EyeW + 4) return;

            int row = (e.Y - 4) / RowH;
            if (LayerAt(row) < 0) return;

            var h = RenameRequested;
            if (h != null) h(this, EventArgs.Empty);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int row = (e.Y - 4) / RowH;
            if (row != _hoverRow) { _hoverRow = row; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverRow = -1;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            _scroll += e.Delta > 0 ? -1 : 1;
            ClampScroll();
            Invalidate();
        }

        // ------------------------------------------------------------ отрисовка

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var box = new Rectangle(0, 0, Width - 1, Height - 1);
            if (box.Width <= 4 || box.Height <= 4) return;

            Theme.Smooth(g);
            Theme.FillRound(g, box, 10, Color.FromArgb(16, 20, 28));
            Theme.DrawRound(g, box, 10, Theme.Border);

            if (Doc == null || Count == 0)
            {
                Theme.Text1(g, "нет слоёв", Theme.F(9f), Theme.Dim, box,
                    StringAlignment.Center, StringAlignment.Center);
                return;
            }

            ClampScroll();
            int rows = Rows;

            for (int row = 0; row < rows; row++)
            {
                int index = LayerAt(row);
                if (index < 0) break;

                var layer = Doc.Layers[index];
                var r = new Rectangle(4, 4 + row * RowH, Width - 9, RowH - 2);

                bool active = index == Doc.Active;
                if (active) Theme.FillRound(g, r, 7, Color.FromArgb(52, Theme.Accent));
                else if (row == _hoverRow) Theme.FillRound(g, r, 7, Theme.CardHover);

                DrawEye(g, new Rectangle(r.X + 5, r.Y + (r.Height - 12) / 2, 16, 12), layer.Visible);

                string name = layer.Name;
                if (layer.Opacity < 100) name += "  " + layer.Opacity + "%";

                Theme.Text1(g, name, Theme.F(9f, active ? FontStyle.Bold : FontStyle.Regular),
                    layer.Visible ? (active ? Theme.Text : Theme.Muted) : Theme.Dim,
                    new Rectangle(r.X + EyeW + 4, r.Y, r.Width - EyeW - 10, r.Height),
                    StringAlignment.Near, StringAlignment.Center);
            }

            // полоска прокрутки, если слои не влезли
            if (Count > rows)
            {
                float part = (float)rows / Count;
                int trackH = Height - 10;
                int barH = Math.Max(18, (int)(trackH * part));
                int barY = 5 + (int)((trackH - barH) * (_scroll / (float)Math.Max(1, Count - rows)));
                using (var brush = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
                    g.FillRectangle(brush, Width - 5, barY, 3, barH);
            }
        }

        private static void DrawEye(Graphics g, Rectangle r, bool visible)
        {
            Color color = visible ? Theme.Accent : Theme.Dim;

            using (var pen = new Pen(color, 1.4f))
            {
                g.DrawArc(pen, r.X, r.Y, r.Width, r.Height * 2 - 2, 180, 180);
                g.DrawArc(pen, r.X, r.Y - r.Height + 2, r.Width, r.Height * 2 - 2, 0, 180);

                if (visible)
                {
                    using (var brush = new SolidBrush(color))
                        g.FillEllipse(brush, r.X + r.Width / 2f - 2.5f, r.Y + r.Height / 2f - 2.5f, 5, 5);
                }
                else g.DrawLine(pen, r.X + 1, r.Bottom - 1, r.Right - 1, r.Y + 1);
            }
        }
    }
}
