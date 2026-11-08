using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Всплывающие уведомления в правом нижнем углу, как в Steam. Карточки
    /// наезжают снизу, висят несколько секунд и исчезают. По клику — действие.
    /// </summary>
    public class ToastHost : Control
    {
        private class Item
        {
            public string Title = "";
            public string Body = "";
            public Action OnClick;
            public int Life;        // тиков до исчезновения
            public int In;          // тиков анимации появления
        }

        private const int CardW = 320;
        private const int CardH = 72;
        private const int Gap = 10;
        private const int Margin = 16;
        private const int ShowTicks = 55;   // ~5.5 c при 100 мс

        private readonly List<Item> _items = new List<Item>();
        private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer { Interval = 100 };

        public ToastHost()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Visible = false;
            _timer.Tick += (s, e) => Step();
        }

        public void Push(string title, string body, Action onClick)
        {
            // не копим бесконечно
            if (_items.Count >= 4) _items.RemoveAt(0);
            _items.Add(new Item { Title = title ?? "", Body = body ?? "", OnClick = onClick, Life = ShowTicks, In = 5 });
            Relayout();
            Visible = true;
            BringToFront();
            if (!_timer.Enabled) _timer.Start();
            Invalidate();
        }

        private void Step()
        {
            bool changed = false;
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (_items[i].In > 0) { _items[i].In--; changed = true; }
                _items[i].Life--;
                if (_items[i].Life <= 0) { _items.RemoveAt(i); changed = true; }
            }
            if (_items.Count == 0) { _timer.Stop(); Visible = false; return; }
            if (changed) Relayout();
            Invalidate();
        }

        private void Relayout()
        {
            int n = _items.Count;
            int h = n == 0 ? 0 : n * CardH + (n - 1) * Gap;
            Size = new Size(CardW, Math.Max(1, h));
            if (Parent != null)
                Location = new Point(Parent.ClientSize.Width - CardW - Margin,
                                     Parent.ClientSize.Height - Height - Margin);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int y = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                var r = new Rectangle(0, y, CardW, CardH);
                if (r.Contains(e.Location))
                {
                    var act = _items[i].OnClick;
                    _items.RemoveAt(i);
                    Relayout();
                    Invalidate();
                    if (_items.Count == 0) { _timer.Stop(); Visible = false; }
                    if (act != null) { try { act(); } catch { } }
                    return;
                }
                y += CardH + Gap;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            int y = 0;
            foreach (var it in _items)
            {
                int dx = it.In * 14;        // лёгкий наезд справа
                var card = new Rectangle(dx, y, CardW - dx, CardH);

                Theme.FillRound(g, card, 12, Color.FromArgb(242, 22, 26, 36));
                Theme.DrawRound(g, card, 12, Color.FromArgb(70, Theme.Accent));

                // иконка-кружок
                var dot = new Rectangle(card.X + 14, card.Y + (CardH - 34) / 2, 34, 34);
                Theme.FillRoundGradient(g, dot, 10, Theme.Accent, Theme.Accent2, 35f);
                Theme.Text1(g, "✉", Theme.F(13f, FontStyle.Bold), Color.White, dot,
                    StringAlignment.Center, StringAlignment.Center);

                var tx = new Rectangle(dot.Right + 12, card.Y + 12, card.Right - dot.Right - 24, 20);
                Theme.Text1(g, it.Title, Theme.F(10f, FontStyle.Bold), Theme.Text, tx);
                var bx = new Rectangle(dot.Right + 12, card.Y + 34, card.Right - dot.Right - 24, 26);
                Theme.Text1(g, it.Body, Theme.F(9f), Theme.Muted, bx);

                y += CardH + Gap;
            }
        }
    }
}
