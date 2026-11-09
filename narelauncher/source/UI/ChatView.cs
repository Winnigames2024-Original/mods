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
            public bool Actions;             // есть ли inline-кнопки (заявка Publish)
            public Rectangle OkBtn;          // координаты в ленте (без прокрутки)
            public Rectangle NoBtn;
        }

        private readonly List<Row> _rows = new List<Row>();
        private int _scroll;
        private int _total;
        private bool _stick = true;         // держаться низа, пока не прокрутили вверх вручную

        /// <summary>id серверов, по которым ещё можно принять/отклонить.</summary>
        public readonly System.Collections.Generic.HashSet<string> PendingIds = new System.Collections.Generic.HashSet<string>();

        /// <summary>Нажата inline-кнопка: (serverId, approve).</summary>
        public event Action<string, bool> Action;

        public ChatView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Panel;
            Cursor = Cursors.Hand;
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
                    r.Actions = !string.IsNullOrEmpty(r.Msg.ServerId) && PendingIds.Contains(r.Msg.ServerId);
                    int bw = Math.Min(MaxBubble, Math.Max(200, sz.Width + 24));
                    int bh = sz.Height + 28;        // шапка (имя) + отступы
                    if (r.Actions) bh += 34;        // ряд кнопок
                    int x = r.Mine ? Width - bw - 12 : 12;
                    r.H = bh + 8;
                    r.Y = _total;
                    r.Bubble = new Rectangle(x, _total + 4, bw, bh);
                    if (r.Actions)
                    {
                        int by = r.Bubble.Bottom - 32;
                        int half = (r.Bubble.Width - 24 - 8) / 2;
                        r.OkBtn = new Rectangle(r.Bubble.X + 12, by, half, 26);
                        r.NoBtn = new Rectangle(r.OkBtn.Right + 8, by, half, 26);
                    }
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

        private int _copied = -1;   // индекс скопированного пузыря (для короткой подсветки)

        /// <summary>Клик по сообщению копирует его текст.</summary>
        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            for (int i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];

                if (r.Actions)
                {
                    var ok = new Rectangle(r.OkBtn.X, r.OkBtn.Y - _scroll, r.OkBtn.Width, r.OkBtn.Height);
                    var no = new Rectangle(r.NoBtn.X, r.NoBtn.Y - _scroll, r.NoBtn.Width, r.NoBtn.Height);
                    if (ok.Contains(e.Location)) { var h = Action; if (h != null) h(r.Msg.ServerId, true); return; }
                    if (no.Contains(e.Location)) { var h = Action; if (h != null) h(r.Msg.ServerId, false); return; }
                }

                var b = new Rectangle(r.Bubble.X, r.Bubble.Y - _scroll, r.Bubble.Width, r.Bubble.Height);
                if (b.Contains(e.Location))
                {
                    try { if (!string.IsNullOrEmpty(r.Msg.Text)) Clipboard.SetText(r.Msg.Text); } catch { }
                    _copied = i;
                    Invalidate();
                    var t = new System.Windows.Forms.Timer { Interval = 900 };
                    t.Tick += (s, ev) => { _copied = -1; t.Stop(); t.Dispose(); if (!IsDisposed) Invalidate(); };
                    t.Start();
                    return;
                }
            }
        }

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

                string who = string.IsNullOrEmpty(r.Msg.FromName) ? r.Msg.From : r.Msg.FromName;
                if (!string.IsNullOrEmpty(r.Msg.Tag)) who = r.Msg.Tag + " " + who;
                string name = r.Msg.Admin ? (who + " · админ") : who;
                Theme.Text1(g, name, Head, r.Mine ? Theme.Accent2 : Theme.Warn,
                    new Rectangle(bubble.X + 12, bubble.Y + 6, bubble.Width - 20, 13));

                var textR = new Rectangle(bubble.X + 12, bubble.Y + 20, bubble.Width - 24, bubble.Height - 26);
                TextRenderer.DrawText(g, r.Msg.Text ?? "", Body, textR, Theme.Text,
                    TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);

                if (r.Actions)
                {
                    var ok = new Rectangle(r.OkBtn.X, r.OkBtn.Y - _scroll, r.OkBtn.Width, r.OkBtn.Height);
                    var no = new Rectangle(r.NoBtn.X, r.NoBtn.Y - _scroll, r.NoBtn.Width, r.NoBtn.Height);
                    Theme.FillRound(g, ok, 8, Color.FromArgb(70, Theme.Good));
                    Theme.FillRound(g, no, 8, Color.FromArgb(70, Theme.Bad));
                    Theme.Text1(g, "✓ Принять", Theme.F(8.5f, FontStyle.Bold), Theme.Good, ok, StringAlignment.Center, StringAlignment.Center);
                    Theme.Text1(g, "✕ Отклонить", Theme.F(8.5f, FontStyle.Bold), Theme.Bad, no, StringAlignment.Center, StringAlignment.Center);
                }

                if (_rows.IndexOf(r) == _copied)
                {
                    var tag = new Rectangle(bubble.X, bubble.Y - 2, 96, 16);
                    Theme.Text1(g, "✓ скопировано", Theme.F(7.5f, FontStyle.Bold), Theme.Good, tag);
                }
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
