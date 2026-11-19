namespace GEO_DASH.UI
{
    /// <summary>Настоящий полный экран: без рамки, во весь монитор, поверх панели задач.</summary>
    public static class FullScreen
    {
        static readonly Dictionary<Form, (FormBorderStyle b, Rectangle r, FormWindowState w)> _saved = new();

        public static bool IsOn(Form f) => f != null && _saved.ContainsKey(f);

        public static void Apply(Form f, bool on)
        {
            if (f == null || f.IsDisposed) return;
            if (on == IsOn(f)) return;

            if (on)
            {
                _saved[f] = (f.FormBorderStyle, f.Bounds, f.WindowState);
                var scr = Screen.FromControl(f) ?? Screen.PrimaryScreen;
                f.SuspendLayout();
                f.WindowState = FormWindowState.Normal;
                f.FormBorderStyle = FormBorderStyle.None;
                f.MaximizeBox = false;
                f.Bounds = scr.Bounds;
                f.ResumeLayout(true);
                f.Activate();
            }
            else
            {
                if (!_saved.TryGetValue(f, out var s)) return;
                _saved.Remove(f);
                f.SuspendLayout();
                f.FormBorderStyle = s.b;
                f.WindowState = s.w;
                if (s.w == FormWindowState.Normal) f.Bounds = s.r;
                f.ResumeLayout(true);
            }
        }

        public static void Toggle(Form f) => Apply(f, !IsOn(f));

        /// <summary>Повесить F11 на форму — переключает полный экран.</summary>
        public static void Hook(Form f)
        {
            if (f == null) return;
            f.KeyPreview = true;
            f.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.F11 && !(e.KeyCode == Keys.Enter && e.Alt)) return;
                Toggle(f);
                AppConfig.Settings.Fullscreen = IsOn(f);
                AppConfig.Save();
                e.Handled = true;
            };
            f.FormClosed += (s, e) => _saved.Remove(f);
        }
    }
}
