namespace GEO_DASH.UI
{
    /// <summary>
    /// Переходы между экранами, как activity на телефоне: все экраны живут
    /// в одной панели внутри <see cref="MainForm"/>, а не отдельными окнами.
    ///
    /// <code>
    /// Nav.Go(new SettingsForm());   // открыть экран в панели
    /// Nav.Back();                   // вернуться назад
    /// Nav.Home();                   // закрыть все экраны и вернуться в меню
    /// </code>
    /// Если оболочка ещё не готова (запуск, вход в аккаунт) — экран просто
    /// открывается обычным окном, так что код работает в обоих случаях.
    /// </summary>
    /// <summary>Экран, который должен занимать всю панель (игра, редактор).</summary>
    public interface IFillScreen { }

    public static class Nav
    {
        static Panel _host;
        static Form _shell;
        static readonly List<Form> _stack = new();
        static readonly Dictionary<Form, Size> _designed = new();

        /// <summary>Экран, который сейчас виден в панели (или null — видно меню).</summary>
        public static Form Current => _stack.Count > 0 ? _stack[^1] : null;

        /// <summary>Сколько экранов открыто поверх меню.</summary>
        public static int Depth => _stack.Count;

        /// <summary>Панель готова принимать экраны.</summary>
        public static bool Ready => _host != null && !_host.IsDisposed;

        /// <summary>Вызывается оболочкой один раз: сюда будут встраиваться все экраны.</summary>
        public static void Attach(Form shell, Panel host)
        {
            _shell = shell;
            _host = host;
            _host.Visible = false;
            _host.Resize += (s, e) => Fit(Current);
        }

        /// <summary>Этот экран сейчас встроен в панель (а не открыт окном).</summary>
        public static bool IsEmbedded(Form f) => f != null && _stack.Contains(f);

        /// <summary>Что-то поменялось в экране — пусть оболочка перерисуется.</summary>
        public static void Refresh() => _shell?.Invalidate(true);

        // ------------------------------------------------------------ переходы

        /// <summary>Открыть экран и выполнить действие, когда игрок с него уйдёт.</summary>
        public static void Go(Form f, Action after)
        {
            if (f == null) return;
            if (after != null) f.FormClosed += (s, e) => { try { after(); } catch { } };
            Go(f);
        }

        /// <summary>Открыть экран поверх текущего.</summary>
        public static void Go(Form f)
        {
            if (f == null) return;

            if (!Ready || _shell == null || _shell.IsDisposed)
            {
                using (f) f.ShowDialog();
                return;
            }

            try
            {
                var prev = Current;
                if (prev != null) prev.Visible = false;

                f.TopLevel = false;
                f.FormBorderStyle = FormBorderStyle.None;
                f.StartPosition = FormStartPosition.Manual;
                f.ControlBox = false;
                f.FormClosed += OnScreenClosed;

                _stack.Add(f);
                _host.Controls.Add(f);
                _host.Visible = true;
                _host.BringToFront();

                Fit(f);
                f.Show();
                f.BringToFront();
                try { f.Focus(); } catch { }
            }
            catch
            {
                // не получилось встроить — показываем окном, игра не ломается
                _stack.Remove(f);
                try { f.TopLevel = true; using (f) f.ShowDialog(_shell); } catch { }
                ShowTop();
            }
        }

        /// <summary>Закрыть верхний экран.</summary>
        public static void Back()
        {
            var f = Current;
            if (f == null) return;
            try { f.Close(); } catch { Drop(f); }
        }

        /// <summary>Закрыть все экраны — вернуться в главное меню.</summary>
        public static void Home()
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                var f = _stack[i];
                try { f.Close(); } catch { Drop(f); }
            }
            _stack.Clear();
            ShowTop();
        }

        // ------------------------------------------------------------ внутреннее

        static void OnScreenClosed(object sender, FormClosedEventArgs e) => Drop(sender as Form);

        static void Drop(Form f)
        {
            if (f == null) return;
            f.FormClosed -= OnScreenClosed;
            _stack.Remove(f);
            _designed.Remove(f);
            try { if (_host != null && _host.Controls.Contains(f)) _host.Controls.Remove(f); } catch { }
            try { if (!f.IsDisposed) f.Dispose(); } catch { }
            ShowTop();
        }

        static void ShowTop()
        {
            if (_host == null || _host.IsDisposed) return;
            var top = Current;
            if (top == null)
            {
                _host.Visible = false;
                _shell?.Invalidate(true);
                try { _shell?.Focus(); } catch { }
                return;
            }
            top.Visible = true;
            Fit(top);
            top.BringToFront();
            try { top.Focus(); } catch { }
        }

        /// <summary>
        /// Экран занимает всю панель; если он задуман меньше — ставим его по центру,
        /// чтобы вёрстка экрана осталась такой, какой её рисовали.
        /// </summary>
        static void Fit(Form f)
        {
            if (f == null || f.IsDisposed || _host == null || _host.IsDisposed) return;
            var host = _host.ClientSize;
            if (host.Width < 2 || host.Height < 2) return;

            if (!_designed.TryGetValue(f, out var want))
            {
                want = f.ClientSize;             // запоминаем задуманный размер один раз
                _designed[f] = want;
            }

            // Экран занимает всю панель всегда: раньше он оставался своим
            // задуманным размером по центру, и в полном экране меню выглядело
            // маленьким окошком посреди монитора. Начинку экран увеличивает
            // сам (GdForm.ScaleUi) — отсюда ему и сообщается разметка.
            if (f is GdForm gf && gf.DesignSize.Width == 0) gf.DesignSize = want;
            f.Bounds = new Rectangle(0, 0, host.Width, host.Height);

            // Окно игры слоёное (WS_EX_COMPOSITED): после разворачивания новая
            // площадь остаётся чёрной, пока её не попросить перерисоваться.
            try { f.PerformLayout(); f.Invalidate(true); _host.Invalidate(true); } catch { }
        }
    }
}
