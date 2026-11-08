using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Лента чата с пузырями переменной высоты: длинные сообщения переносятся
    /// и показываются целиком, есть прокрутка колесом и автоскролл вниз.
    /// </summary>
    public class ChatView : Control
    {
        private class Row
        {
            public NareAuth.ChatMessage Msg;
            public bool Mine;
            public int Y;
            public int H;
            public Rectangle Bubble;
        }

        private readonly List<Row> _rows = new List<Row>();
        private int _scroll;
        private int _total;
        private bool _stick = true;         // держаться низа, пока не прокрутили вверх вручную

        public ChatView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Panel;
        }

        private static readonly Font Body = Theme.F(9.5f);
        private static readonly Font Head = Theme.F(7.6f, FontStyle.Bold);

        public void SetMessages(IEnumerable<NareAuth.ChatMessage> msgs, bool adminView, string youName)
        {
            _rows.Clear();
            foreach (var m in msgs)
                _rows.Add(new Row { Msg = m, Mine = adminView ? m.Admin : !m.Admin });
            Measure();
            if (_stick) ScrollBottom();
            Invalidate();
        }

        private int MaxBubble { get { return Math.Max(140, Math.Min(400, Width - 90)); } }

        private void Measure()
        {
            _total = 8;
            using (var g = CreateGraphics())
            {
                foreach (var r in _rows)
                {
                    int inner = MaxBubble - 24;
                    var sz = TextRenderer.MeasureText(g, r.Msg.Text ?? "", Body,
                        new Size(inner, 0), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                    int bw = Math.Min(MaxBubble, Math.Max(90, sz.Width + 24));
                    int bh = sz.Height + 28;        // шапка (имя) + отступы
                    int x = r.Mine ? Width - bw - 12 : 12;
                    r.H = bh + 8;
                    r.Y = _total;
                    r.Bubble = new Rectangle(x, _total + 4, bw, bh);
                    _total += r.H;
                }
            }
            _total += 8;
            ClampScroll();
        }

        private int MaxScroll { get { return Math.Max(0, _total - Height); } }

        private void ClampScroll()
        {
            if (_scroll < 0) _scroll = 0;
            if (_scroll > MaxScroll) _scroll = MaxScroll;
        }

        public void ScrollBottom() { _scroll = MaxScroll; _stick = true; ClampScroll(); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            _scroll -= Math.Sign(e.Delta) * 60;
            ClampScroll();
            _stick = _scroll >= MaxScroll - 2;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Measure();
            if (_stick) ScrollBottom();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            Theme.FillRound(g, ClientRectangle, 10, Theme.Panel);

            if (_rows.Count == 0)
            {
                Theme.Text1(g, "Сообщений пока нет.", Theme.F(10f), Theme.Dim, ClientRectangle,
                    StringAlignment.Center, StringAlignment.Center);
                return;
            }

            foreach (var r in _rows)
            {
                int y = r.Bubble.Y - _scroll;
                if (y + r.Bubble.Height < 0 || y > Height) continue;

                var bubble = new Rectangle(r.Bubble.X, y, r.Bubble.Width, r.Bubble.Height);
                Theme.FillRound(g, bubble, 12, r.Mine ? Color.FromArgb(70, Theme.Accent) : Color.FromArgb(255, 30, 36, 48));

                string name = r.Msg.Admin ? (r.Msg.From + " · админ") : r.Msg.From;
                Theme.Text1(g, name, Head, r.Mine ? Theme.Accent2 : Theme.Warn,
                    new Rectangle(bubble.X + 12, bubble.Y + 6, bubble.Width - 20, 13));

                var textR = new Rectangle(bubble.X + 12, bubble.Y + 20, bubble.Width - 24, bubble.Height - 26);
                TextRenderer.DrawText(g, r.Msg.Text ?? "", Body, textR, Theme.Text,
                    TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            }

            // тонкий скроллбар
            if (MaxScroll > 0)
            {
                int trackH = Height - 8;
                int thumbH = Math.Max(30, (int)((float)Height / _total * trackH));
                int thumbY = 4 + (int)((float)_scroll / MaxScroll * (trackH - thumbH));
                var thumb = new Rectangle(Width - 7, thumbY, 4, thumbH);
                Theme.FillRound(g, thumb, 2, Color.FromArgb(90, 255, 255, 255));
            }
        }
    }
}
