using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NareLauncher.UI
{
    /// <summary>
    /// Прокрутка колёсиком работает для панели под курсором, даже если фокус не на
    /// ней (стандартный WinForms отдаёт колесо только сфокусированному контролу).
    /// Регистрируется один раз: Application.AddMessageFilter(new WheelFilter()).
    /// </summary>
    public class WheelFilter : IMessageFilter
    {
        private const int WM_MOUSEWHEEL = 0x020A;

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(Point p);

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_MOUSEWHEEL) return false;
            try
            {
                IntPtr h = WindowFromPoint(Cursor.Position);
                if (h == IntPtr.Zero) return false;
                var c = Control.FromHandle(h);
                int delta = (short)(((long)m.WParam >> 16) & 0xFFFF);

                while (c != null)
                {
                    // контролы, которые сами умеют крутить колесо — не трогаем
                    if (c is TextBoxBase || c is ListBox || c is ComboBox) return false;

                    var sc = c as ScrollableControl;
                    if (sc != null && sc.AutoScroll && sc.VerticalScroll.Visible)
                    {
                        var ap = sc.AutoScrollPosition;
                        int step = (int)(delta * 0.8);
                        sc.AutoScrollPosition = new Point(-ap.X, -ap.Y - step);
                        return true;
                    }
                    c = c.Parent;
                }
            }
            catch { }
            return false;
        }
    }
}
