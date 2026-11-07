using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace NareLauncher.UI
{
    public abstract class BaseControl : Control
    {
        protected bool Hover;
        protected bool Pressed;

        protected BaseControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = Theme.Text;
            Font = Theme.F(9.5f);
        }

        protected override void OnMouseEnter(EventArgs e) { Hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Hover = false; Pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { Pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { Pressed = false; Invalidate(); base.OnMouseUp(e); }
    }

    public enum BtnStyle { Accent, Ghost, Subtle, Danger, Success }

    public class NareButton : BaseControl
    {
        public BtnStyle Style = BtnStyle.Subtle;
        public Ico? Icon;
        public int Radius = 10;
        public bool Busy;

        public NareButton()
        {
            Size = new Size(140, 38);
            Cursor = Cursors.Hand;
            Font = Theme.F(9.5f, FontStyle.Bold);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (r.Width <= 0 || r.Height <= 0) return;

            Color fg = Theme.Text;
            float t = Pressed ? 0.25f : (Hover ? 0.12f : 0f);

            switch (Style)
            {
                case BtnStyle.Accent:
                    {
                        var a = Theme.Mix(Theme.Accent, Color.White, t);
                        var b = Theme.Mix(Theme.Accent2, Color.White, t);
                        Theme.FillRoundGradient(g, r, Radius, a, b, 20f);
                        fg = Color.White;
                        break;
                    }
                case BtnStyle.Ghost:
                    Theme.FillRound(g, r, Radius, Color.FromArgb(Hover ? 30 : 0, 255, 255, 255));
                    Theme.DrawRound(g, r, Radius, Hover ? Theme.Accent : Theme.Border, 1.4f);
                    fg = Hover ? Theme.Text : Theme.Muted;
                    break;
                case BtnStyle.Danger:
                    Theme.FillRound(g, r, Radius, Theme.Mix(Color.FromArgb(48, 24, 30), Theme.Bad, Hover ? 0.55f : 0.12f));
                    fg = Hover ? Color.White : Theme.Bad;
                    break;
                case BtnStyle.Success:
                    Theme.FillRound(g, r, Radius, Theme.Mix(Color.FromArgb(22, 44, 34), Theme.Good, Hover ? 0.5f : 0.12f));
                    fg = Hover ? Color.White : Theme.Good;
                    break;
                default:
                    Theme.FillRound(g, r, Radius, Theme.Mix(Theme.Card, Theme.CardHover, Hover ? 1f : 0f));
                    Theme.DrawRound(g, r, Radius, Theme.Border);
                    fg = Theme.Text;
                    break;
            }

            if (!Enabled) fg = Theme.Dim;

            string label = Busy ? "Подождите…" : Text;
            int pad = 14;

            if (Icon.HasValue && string.IsNullOrEmpty(label))
            {
                int s2 = Math.Min(18, Math.Min(Width, Height) - 10);
                Theme.DrawIcon(g, Icon.Value, new Rectangle((Width - s2) / 2, (Height - s2) / 2, s2, s2), fg);
                return;
            }

            if (Icon.HasValue)
            {
                int s = Math.Min(18, Height - 14);
                var ib = new Rectangle(pad, (Height - s) / 2, s, s);
                Theme.DrawIcon(g, Icon.Value, ib, fg);
                Theme.Text1(g, label, Font, fg,
                    new Rectangle(ib.Right + 8, 0, Width - ib.Right - 8 - pad, Height),
                    StringAlignment.Center);
            }
            else
            {
                Theme.Text1(g, label, Font, fg, new Rectangle(pad, 0, Width - pad * 2, Height),
                    StringAlignment.Center);
            }
        }
    }

    public class NavButton : BaseControl
    {
        public Ico Icon = Ico.Play;
        public bool Active;

        public NavButton()
        {
            Height = 44;
            Cursor = Cursors.Hand;
            Font = Theme.F(10f, FontStyle.Regular);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            var r = new Rectangle(8, 3, Width - 16, Height - 6);
            if (r.Width <= 0 || r.Height <= 0) return;

            if (Active)
                Theme.FillRound(g, r, 10, Color.FromArgb(38, Theme.Accent));
            else if (Hover)
                Theme.FillRound(g, r, 10, Color.FromArgb(18, 255, 255, 255));

            if (Active)
            {
                var bar = new Rectangle(0, Height / 2 - 9, 3, 18);
                Theme.FillRound(g, bar, 2, Theme.Accent);
            }

            Color fg = Active ? Theme.Text : (Hover ? Theme.Text : Theme.Muted);
            var ib = new Rectangle(r.X + 12, (Height - 18) / 2, 18, 18);
            Theme.DrawIcon(g, Icon, ib, Active ? Theme.Accent : fg);

            Theme.Text1(g, Text, Theme.F(10f, Active ? FontStyle.Bold : FontStyle.Regular), fg,
                new Rectangle(ib.Right + 12, 0, Width - ib.Right - 20, Height));
        }
    }

    public class CardPanel : Panel
    {
        public int Radius = 14;
        public Color Fill = Theme.Card;
        public Color Stroke = Theme.Border;
        public bool DrawBorder = true;

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (r.Width <= 0 || r.Height <= 0) return;
            Theme.FillRound(g, r, Radius, Fill);
            if (DrawBorder) Theme.DrawRound(g, r, Radius, Stroke);
            base.OnPaint(e);
        }
    }

    public class NareProgress : BaseControl
    {
        private int _value;
        private int _max = 100;
        public bool Indeterminate;
        private int _phase;
        private readonly Timer _timer;

        public NareProgress()
        {
            Height = 8;
            _timer = new Timer { Interval = 33 };
            _timer.Tick += (s, e) => { _phase = (_phase + 6) % 1000; Invalidate(); };
        }

        public int Maximum
        {
            get { return _max; }
            set { _max = Math.Max(1, value); Invalidate(); }
        }

        public int Value
        {
            get { return _value; }
            set { _value = Math.Max(0, Math.Min(_max, value)); Invalidate(); }
        }

        public void SetIndeterminate(bool on)
        {
            Indeterminate = on;
            if (on) _timer.Start(); else _timer.Stop();
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _timer != null) _timer.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            var r = new Rectangle(0, 0, Width, Height);
            if (r.Width <= 0 || r.Height <= 0) return;

            Theme.FillRound(g, r, Height / 2, Color.FromArgb(28, 34, 48));

            if (Indeterminate)
            {
                int w = Math.Max(40, Width / 4);
                int travel = Width + w;
                int x = (int)((_phase / 1000.0) * travel) - w;
                var bar = new Rectangle(Math.Max(0, x), 0, Math.Min(w, Width - Math.Max(0, x)), Height);
                if (bar.Width > 0)
                    Theme.FillRoundGradient(g, bar, Height / 2, Theme.Accent, Theme.Accent2);
            }
            else
            {
                int w = (int)(Width * (_value / (double)_max));
                if (w > 0)
                    Theme.FillRoundGradient(g, new Rectangle(0, 0, w, Height), Height / 2, Theme.Accent, Theme.Accent2);
            }
        }
    }

    public class NareTextBox : Panel
    {
        public readonly TextBox Inner = new TextBox();
        public string Placeholder = "";
        public Ico? Icon;

        public NareTextBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Height = 38;

            Inner.BorderStyle = BorderStyle.None;
            Inner.BackColor = Theme.Panel;
            Inner.ForeColor = Theme.Text;
            Inner.Font = Theme.F(10f);
            Inner.Enter += (s, e) => Invalidate();
            Inner.Leave += (s, e) => Invalidate();
            Inner.TextChanged += (s, e) => Invalidate();
            Controls.Add(Inner);
        }

        public override string Text
        {
            get { return Inner.Text; }
            set { Inner.Text = value; }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int left = Icon.HasValue ? 36 : 12;
            Inner.SetBounds(left, (Height - Inner.PreferredHeight) / 2 + 1, Math.Max(10, Width - left - 12), Inner.PreferredHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (r.Width <= 0 || r.Height <= 0) return;

            Theme.FillRound(g, r, 10, Theme.Panel);
            Theme.DrawRound(g, r, 10, Inner.Focused ? Theme.Accent : Theme.Border, 1.4f);

            if (Icon.HasValue)
                Theme.DrawIcon(g, Icon.Value, new Rectangle(11, (Height - 16) / 2, 16, 16), Theme.Dim, 1.6f);

            if (string.IsNullOrEmpty(Inner.Text) && !string.IsNullOrEmpty(Placeholder) && !Inner.Focused)
            {
                int left = Icon.HasValue ? 36 : 12;
                Theme.Text1(g, Placeholder, Theme.F(10f), Theme.Dim,
                    new Rectangle(left, 0, Width - left - 10, Height));
            }
        }
    }

    /// <summary>Выпадающий список в стиле лаунчера (без системного ComboBox).</summary>
    public class NareDropDown : BaseControl
    {
        public readonly List<object> Items = new List<object>();
        private int _index = -1;
        private Form _popup;

        public event EventHandler SelectedIndexChanged;

        public NareDropDown()
        {
            Height = 40;
            Cursor = Cursors.Hand;
            Font = Theme.F(10f);
        }

        public int SelectedIndex
        {
            get { return _index; }
            set
            {
                if (value < -1) value = -1;
                if (value >= Items.Count) value = Items.Count - 1;
                if (_index == value) return;
                _index = value;
                Invalidate();
                var h = SelectedIndexChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public object SelectedItem
        {
            get { return _index >= 0 && _index < Items.Count ? Items[_index] : null; }
        }

        public void SetItems(IEnumerable<object> items, object select = null)
        {
            Items.Clear();
            foreach (var i in items) Items.Add(i);

            _index = -1;
            if (select != null)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    if (string.Equals(Items[i].ToString(), select.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        _index = i;
                        break;
                    }
                }
            }
            if (_index < 0 && Items.Count > 0) _index = 0;
            Invalidate();

            var h = SelectedIndexChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (r.Width <= 0 || r.Height <= 0) return;

            Theme.FillRound(g, r, 10, Hover ? Theme.CardHover : Theme.Panel);
            Theme.DrawRound(g, r, 10, Hover ? Theme.Accent : Theme.Border, 1.4f);

            string text = SelectedItem != null ? SelectedItem.ToString() : "— не выбрано —";
            Theme.Text1(g, text, Font, SelectedItem != null ? Theme.Text : Theme.Dim,
                new Rectangle(12, 0, Width - 44, Height));

            Theme.DrawIcon(g, Ico.Arrow, new Rectangle(Width - 30, (Height - 16) / 2, 16, 16), Theme.Muted, 1.8f);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            ShowPopup();
        }

        private void ShowPopup()
        {
            if (_popup != null) { ClosePopup(); return; }
            if (Items.Count == 0) return;

            var list = new VirtualList
            {
                ItemHeight = 34,
                Dock = DockStyle.Fill,
                BackColorOverride = Theme.Panel
            };
            list.Items.AddRange(Items);
            list.SelectedIndex = _index;
            list.Renderer = (g, rect, item, idx, hover, sel) =>
            {
                if (sel) Theme.FillRound(g, Rectangle.Inflate(rect, -4, -2), 8, Color.FromArgb(50, Theme.Accent));
                else if (hover) Theme.FillRound(g, Rectangle.Inflate(rect, -4, -2), 8, Color.FromArgb(20, 255, 255, 255));
                Theme.Text1(g, item.ToString(), Theme.F(9.5f), sel ? Theme.Text : Theme.Muted,
                    new Rectangle(rect.X + 12, rect.Y, rect.Width - 20, rect.Height));
            };

            int height = Math.Min(320, Math.Max(40, Items.Count * 34 + 10));

            var form = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                BackColor = Theme.Panel,
                Size = new Size(Width, height),
                Location = PointToScreen(new Point(0, Height + 4)),
                TopMost = true
            };
            form.Controls.Add(list);
            form.Deactivate += (s, e) => ClosePopup();

            list.SelectionChanged += (s, e) =>
            {
                SelectedIndex = list.SelectedIndex;
                ClosePopup();
            };

            _popup = form;
            form.Show(FindForm());
            form.BringToFront();
        }

        private void ClosePopup()
        {
            if (_popup == null) return;
            var p = _popup;
            _popup = null;
            try { p.Close(); p.Dispose(); } catch { }
            Invalidate();
        }
    }

    /// <summary>Прокручиваемый список с собственной отрисовкой элементов.</summary>
    public class VirtualList : BaseControl
    {
        public readonly List<object> Items = new List<object>();
        public int ItemHeight = 56;
        public Action<Graphics, Rectangle, object, int, bool, bool> Renderer;
        public Color BackColorOverride = Color.Transparent;
        public string EmptyText = "Пусто";

        private int _scroll;
        private int _hoverIndex = -1;
        private int _selected = -1;
        private bool _draggingThumb;
        private int _dragOffset;

        public event EventHandler SelectionChanged;
        public event EventHandler ItemActivated;

        public VirtualList()
        {
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.Selectable, true);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            // чтобы работало колесо мыши без предварительного клика
            try { if (!ContainsFocus) Focus(); }
            catch { }
        }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                if (_selected == value) return;
                _selected = value;
                Invalidate();
            }
        }

        public object SelectedItem
        {
            get { return _selected >= 0 && _selected < Items.Count ? Items[_selected] : null; }
        }

        public void SetItems(IEnumerable<object> items)
        {
            Items.Clear();
            foreach (var i in items) Items.Add(i);
            _selected = -1;
            _scroll = 0;
            Invalidate();
        }

        private int ContentHeight { get { return Items.Count * ItemHeight; } }
        private int MaxScroll { get { return Math.Max(0, ContentHeight - Height); } }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            _scroll = Math.Max(0, Math.Min(MaxScroll, _scroll - e.Delta));
            Invalidate();
            base.OnMouseWheel(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_draggingThumb)
            {
                int track = Height;
                int thumb = ThumbHeight();
                int y = e.Y - _dragOffset;
                double ratio = (double)y / Math.Max(1, track - thumb);
                _scroll = (int)Math.Max(0, Math.Min(MaxScroll, ratio * MaxScroll));
                Invalidate();
                return;
            }

            int idx = IndexAt(e.Y);
            if (idx != _hoverIndex) { _hoverIndex = idx; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverIndex = -1;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (MaxScroll > 0 && e.X >= Width - 12)
            {
                int thumbTop = ThumbTop();
                int thumbH = ThumbHeight();
                if (e.Y >= thumbTop && e.Y <= thumbTop + thumbH)
                {
                    _draggingThumb = true;
                    _dragOffset = e.Y - thumbTop;
                    return;
                }
            }

            int idx = IndexAt(e.Y);
            if (idx >= 0 && idx < Items.Count)
            {
                _selected = idx;
                Invalidate();
                var h = SelectionChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _draggingThumb = false;
            base.OnMouseUp(e);
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            if (_selected >= 0)
            {
                var h = ItemActivated;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        private int IndexAt(int y)
        {
            int idx = (y + _scroll) / Math.Max(1, ItemHeight);
            return idx >= 0 && idx < Items.Count ? idx : -1;
        }

        private int ThumbHeight()
        {
            if (ContentHeight <= 0) return 0;
            return Math.Max(28, (int)(Height * (Height / (double)ContentHeight)));
        }

        private int ThumbTop()
        {
            if (MaxScroll <= 0) return 0;
            return (int)((Height - ThumbHeight()) * (_scroll / (double)MaxScroll));
        }

        public void ScrollToTop() { _scroll = 0; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);

            if (BackColorOverride.A > 0)
                using (var b = new SolidBrush(BackColorOverride))
                    g.FillRectangle(b, ClientRectangle);

            if (Items.Count == 0)
            {
                Theme.Text1(g, EmptyText, Theme.F(10f), Theme.Dim, ClientRectangle,
                    StringAlignment.Center, StringAlignment.Center);
                return;
            }

            if (_scroll > MaxScroll) _scroll = MaxScroll;

            int first = Math.Max(0, _scroll / ItemHeight);
            int last = Math.Min(Items.Count - 1, (_scroll + Height) / ItemHeight);

            for (int i = first; i <= last; i++)
            {
                var rect = new Rectangle(0, i * ItemHeight - _scroll, Width - (MaxScroll > 0 ? 12 : 0), ItemHeight);
                if (Renderer != null)
                    Renderer(g, rect, Items[i], i, i == _hoverIndex, i == _selected);
                else
                    Theme.Text1(g, Items[i].ToString(), Font, Theme.Text,
                        new Rectangle(rect.X + 10, rect.Y, rect.Width - 20, rect.Height));
            }

            // полоса прокрутки
            if (MaxScroll > 0)
            {
                var track = new Rectangle(Width - 9, 0, 6, Height);
                Theme.FillRound(g, track, 3, Color.FromArgb(24, 255, 255, 255));
                var thumb = new Rectangle(Width - 9, ThumbTop(), 6, ThumbHeight());
                Theme.FillRound(g, thumb, 3, Color.FromArgb(90, 255, 255, 255));
            }
        }
    }

    public class NareToggle : BaseControl
    {
        private bool _checked;
        public event EventHandler CheckedChanged;

        public NareToggle()
        {
            Size = new Size(46, 26);
            Cursor = Cursors.Hand;
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                var h = CheckedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            var r = new Rectangle(0, 2, Width - 1, Height - 5);
            if (r.Width <= 0 || r.Height <= 0) return;

            if (_checked) Theme.FillRoundGradient(g, r, r.Height / 2, Theme.Accent, Theme.Accent2);
            else Theme.FillRound(g, r, r.Height / 2, Theme.Mix(Theme.Panel, Theme.CardHover, Hover ? 1f : 0f));

            Theme.DrawRound(g, r, r.Height / 2, _checked ? Color.Transparent : Theme.Border);

            int d = r.Height - 6;
            int x = _checked ? r.Right - d - 3 : r.X + 3;
            using (var b = new SolidBrush(_checked ? Color.White : Theme.Muted))
                g.FillEllipse(b, x, r.Y + 3, d, d);
        }
    }

    public class NareSlider : BaseControl
    {
        private int _value = 50;
        public int Minimum = 0;
        public int Maximum = 100;
        public int Step = 1;
        public event EventHandler ValueChanged;
        private bool _drag;

        public NareSlider()
        {
            Height = 28;
            Cursor = Cursors.Hand;
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int v = Math.Max(Minimum, Math.Min(Maximum, value));
                if (Step > 1) v = Minimum + ((v - Minimum) / Step) * Step;
                if (_value == v) return;
                _value = v;
                Invalidate();
                var h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _drag = true;
            SetFromX(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_drag) SetFromX(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _drag = false;
            base.OnMouseUp(e);
        }

        private void SetFromX(int x)
        {
            double t = (x - 8) / (double)Math.Max(1, Width - 16);
            Value = Minimum + (int)Math.Round(t * (Maximum - Minimum));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            if (Width <= 20) return;

            int cy = Height / 2;
            var track = new Rectangle(8, cy - 3, Width - 16, 6);
            Theme.FillRound(g, track, 3, Color.FromArgb(30, 36, 50));

            double t = (Value - Minimum) / (double)Math.Max(1, Maximum - Minimum);
            int w = (int)(track.Width * t);
            if (w > 0)
                Theme.FillRoundGradient(g, new Rectangle(track.X, track.Y, w, track.Height), 3, Theme.Accent, Theme.Accent2);

            int kx = track.X + w;
            using (var b = new SolidBrush(Color.White))
                g.FillEllipse(b, kx - 8, cy - 8, 16, 16);
            using (var pen = new Pen(Theme.Accent, 2f))
                g.DrawEllipse(pen, kx - 8, cy - 8, 16, 16);
        }
    }

    /// <summary>Простая метка с темой лаунчера.</summary>
    public class NareLabel : Label
    {
        public NareLabel()
        {
            AutoSize = false;
            BackColor = Color.Transparent;
            ForeColor = Theme.Text;
            Font = Theme.F(10f);
            TextAlign = ContentAlignment.MiddleLeft;
        }

        public NareLabel(string text, float size = 10f, FontStyle style = FontStyle.Regular, Color? color = null)
            : this()
        {
            Text = text;
            Font = Theme.F(size, style);
            if (color.HasValue) ForeColor = color.Value;
        }
    }
}
