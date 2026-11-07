using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Отклик на щелчки мыши: от места клика расходится кольцо и разлетаются
    /// искры. Живёт в отдельном прозрачном окне поверх лаунчера — так эффект
    /// виден и над кнопками, и над списками, и ничего не перерисовывает.
    ///
    /// Окно сквозное для мыши (WS_EX_TRANSPARENT), не берёт фокус и не
    /// показывается на панели задач, так что работать оно не мешает.
    /// Клики ловятся фильтром сообщений — значит, считается любой щелчок в
    /// лаунчере, по какому бы контролу он ни пришёлся.
    /// </summary>
    public class ClickFxLayer : Form, IMessageFilter
    {
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private const int ULW_ALPHA = 0x00000002;
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;

        private class Splash
        {
            public PointF At;
            public float Life;        // 0..1, сколько прошло
            public float Speed;
            public Color Tint;
            public float[] Angles;
            public float[] Reach;
        }

        private readonly List<Splash> _splashes = new List<Splash>();
        private readonly Random _random = new Random();
        private readonly Timer _timer;
        private readonly Form _owner;

        private Bitmap _canvas;

        public ClickFxLayer(Form owner)
        {
            _owner = owner;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = false;
            Enabled = false;

            _timer = new Timer { Interval = 16 };
            _timer.Tick += delegate { Step(); };

            Application.AddMessageFilter(this);

            owner.Move += delegate { Follow(); };
            owner.Resize += delegate { Follow(); };
            owner.VisibleChanged += delegate { Follow(); };

            owner.FormClosed += delegate
            {
                try { Application.RemoveMessageFilter(this); } catch { }
                try { Close(); } catch { }
            };
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        // ------------------------------------------------------------ клики

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_LBUTTONDOWN && m.Msg != WM_RBUTTONDOWN && m.Msg != WM_MBUTTONDOWN)
                return false;

            try
            {
                if (_owner == null || _owner.IsDisposed || !_owner.Visible) return false;
                if (_owner.WindowState == FormWindowState.Minimized) return false;

                var screen = Control.MousePosition;
                var local = _owner.PointToClient(screen);

                if (local.X < 0 || local.Y < 0 ||
                    local.X > _owner.ClientSize.Width || local.Y > _owner.ClientSize.Height)
                    return false;

                Add(local, m.Msg == WM_RBUTTONDOWN);
            }
            catch { }

            return false;   // щелчок идём дальше, мы только подсматриваем
        }

        private void Add(Point at, bool second)
        {
            int rays = 6 + _random.Next(4);
            var angles = new float[rays];
            var reach = new float[rays];

            for (int i = 0; i < rays; i++)
            {
                angles[i] = (float)(_random.NextDouble() * Math.PI * 2);
                reach[i] = 18f + (float)_random.NextDouble() * 26f;
            }

            _splashes.Add(new Splash
            {
                At = at,
                Life = 0f,
                Speed = second ? 2.6f : 2.2f,
                Tint = second ? Theme.Accent2 : Theme.Accent,
                Angles = angles,
                Reach = reach
            });

            // одновременно держим немного, иначе частый клик превращается в кашу
            while (_splashes.Count > 12) _splashes.RemoveAt(0);

            Follow();
            if (!_timer.Enabled) _timer.Start();
        }

        // ------------------------------------------------------------- ход

        private void Follow()
        {
            try
            {
                if (_owner == null || _owner.IsDisposed) return;

                bool show = _owner.Visible && _owner.WindowState != FormWindowState.Minimized;

                if (!show)
                {
                    if (Visible) Hide();
                    return;
                }

                var origin = _owner.PointToScreen(Point.Empty);
                var size = _owner.ClientSize;

                if (Bounds.Location != origin || Size != size)
                    Bounds = new Rectangle(origin, size);

                if (!Visible && _splashes.Count > 0) Show();
            }
            catch { }
        }

        private void Step()
        {
            for (int i = _splashes.Count - 1; i >= 0; i--)
            {
                _splashes[i].Life += 0.016f * _splashes[i].Speed;
                if (_splashes[i].Life >= 1f) _splashes.RemoveAt(i);
            }

            if (_splashes.Count == 0)
            {
                _timer.Stop();
                if (Visible) Hide();
                return;
            }

            Follow();
            Render();
        }

        // -------------------------------------------------------- отрисовка

        private void Render()
        {
            int w = Math.Max(1, ClientSize.Width);
            int h = Math.Max(1, ClientSize.Height);

            if (_canvas == null || _canvas.Width != w || _canvas.Height != h)
            {
                if (_canvas != null) _canvas.Dispose();
                _canvas = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            }

            using (var g = Graphics.FromImage(_canvas))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                foreach (var splash in _splashes) Draw(g, splash);
            }

            Push();
        }

        private static void Draw(Graphics g, Splash splash)
        {
            float t = splash.Life;
            float ease = 1f - (1f - t) * (1f - t);     // быстро начинает, мягко гаснет
            int alpha = (int)(230 * (1f - t));
            if (alpha <= 0) return;

            // внешнее кольцо
            float r1 = 6f + ease * 34f;
            using (var pen = new Pen(Color.FromArgb(alpha, splash.Tint), 2.2f * (1f - t) + 0.6f))
                g.DrawEllipse(pen, splash.At.X - r1, splash.At.Y - r1, r1 * 2, r1 * 2);

            // внутреннее, чуть отстаёт
            float r2 = 2f + ease * 18f;
            using (var pen = new Pen(Color.FromArgb((int)(alpha * 0.55f), Color.White), 1.4f))
                g.DrawEllipse(pen, splash.At.X - r2, splash.At.Y - r2, r2 * 2, r2 * 2);

            // вспышка в самом начале
            if (t < 0.35f)
            {
                int flash = (int)(170 * (1f - t / 0.35f));
                float r3 = 10f * (1f - t / 0.35f) + 2f;
                using (var brush = new SolidBrush(Color.FromArgb(flash, Color.White)))
                    g.FillEllipse(brush, splash.At.X - r3, splash.At.Y - r3, r3 * 2, r3 * 2);
            }

            // искры
            for (int i = 0; i < splash.Angles.Length; i++)
            {
                float d = splash.Reach[i] * ease;
                float x = splash.At.X + (float)Math.Cos(splash.Angles[i]) * d;
                float y = splash.At.Y + (float)Math.Sin(splash.Angles[i]) * d;
                float size = 3.2f * (1f - t) + 0.6f;

                using (var brush = new SolidBrush(Color.FromArgb((int)(alpha * 0.85f), splash.Tint)))
                    g.FillEllipse(brush, x - size / 2, y - size / 2, size, size);
            }
        }

        /// <summary>Отдаёт картинку окну: именно так рисуют полупрозрачные слои.</summary>
        private void Push()
        {
            if (_canvas == null) return;

            IntPtr screen = GetDC(IntPtr.Zero);
            IntPtr memory = CreateCompatibleDC(screen);
            IntPtr bitmap = IntPtr.Zero;
            IntPtr old = IntPtr.Zero;

            try
            {
                bitmap = _canvas.GetHbitmap(Color.FromArgb(0));
                old = SelectObject(memory, bitmap);

                var size = new Size(_canvas.Width, _canvas.Height);
                var from = new Point(0, 0);
                var at = new Point(Left, Top);

                var blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = AC_SRC_ALPHA
                };

                UpdateLayeredWindow(Handle, screen, ref at, ref size,
                                    memory, ref from, 0, ref blend, ULW_ALPHA);
            }
            catch (Exception ex) { Log.Warn("Слой эффектов не обновился: " + ex.Message); }
            finally
            {
                if (old != IntPtr.Zero) SelectObject(memory, old);
                if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                DeleteDC(memory);
                ReleaseDC(IntPtr.Zero, screen);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { Application.RemoveMessageFilter(this); } catch { }
                if (_timer != null) _timer.Dispose();
                if (_canvas != null) _canvas.Dispose();
            }
            base.Dispose(disposing);
        }

        // --------------------------------------------------------- windows

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
            ref Point pptDst, ref Size psize, IntPtr hdcSrc, ref Point pprSrc,
            int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
