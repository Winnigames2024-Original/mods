using System.Drawing;
using System.Windows.Forms;

namespace NareLauncher.UI
{
    /// <summary>
    /// Базовая страница. Важно: контролы с Dock = Fill добавляются ПЕРВЫМИ,
    /// затем Top/Bottom — WinForms раскладывает их в обратном порядке добавления.
    /// </summary>
    public class PageBase : Panel
    {
        public MainForm Host;

        public PageBase()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Dock = DockStyle.Fill;
            BackColor = Theme.Bg;
        }

        /// <summary>Вызывается каждый раз при открытии страницы.</summary>
        public virtual void PageShown() { }

        protected static Panel Header(string title, string subtitle)
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = string.IsNullOrEmpty(subtitle) ? 54 : 72,
                BackColor = Color.Transparent
            };

            var t = new NareLabel(title, 17f, FontStyle.Bold)
            {
                Bounds = new Rectangle(0, 4, 700, 28)
            };
            header.Controls.Add(t);

            if (!string.IsNullOrEmpty(subtitle))
            {
                var s = new NareLabel(subtitle, 9.5f, FontStyle.Regular, Theme.Muted)
                {
                    Bounds = new Rectangle(0, 34, 900, 22)
                };
                header.Controls.Add(s);
            }

            return header;
        }
    }
}
