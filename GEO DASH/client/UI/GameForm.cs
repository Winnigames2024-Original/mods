using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using GEO_DASH.Audio;
using GEO_DASH.Core;
using GEO_DASH.Net;
using GEO_DASH.Render;

namespace GEO_DASH.UI
{
    public class GameForm : Form, IFillScreen
    {
        readonly GameWorld _w = new();
        readonly ViewTransform _v = new();
        readonly GameLoop _loop = new();
        readonly LevelData _level;

        bool _hold, _pressed, _hold2, _pressed2;
        bool _keyLeft, _keyRight;
        bool _paused;
        bool _practice;
        bool _testMode;
        double _deathDelay;
        int _attempts = 1;
        double _bestPercent;
        string _toast = "";
        double _toastT;

        public bool PlayFromEditor;
        public double StartAtProgress;

        // мультиплеер: все в одной карте уровня
        string _mpRoom;
        int _mpMax;
        List<MpPlayerDto> _mpPlayers = new();
        readonly System.Windows.Forms.Timer _mpTimer = new() { Interval = 110 };
        bool _mpBusy;

        // настраиваемый интерфейс: кнопка «=», полоса, кнопки платформера
        static GuiLayout Gui => AppConfig.Settings.Gui;
        bool _guiEdit;
        GuiItem _dragItem;
        PointF _dragOff;
        bool _btnLeft, _btnRight;
        readonly List<(RectangleF r, string id)> _hit = new();

        public GameForm(LevelData level, bool practice = false, bool testMode = false)
        {
            AppConfig.Trace("уровень открывается: " + (level?.Settings?.Name ?? "без названия")
                + ", объектов: " + (level?.Objects?.Count ?? 0));
            _level = level;
            _practice = practice;
            _testMode = testMode;

            Theme.Apply(this, level.Settings.Name);
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            ClientSize = new Size(1280, 720);
            // внутри оболочки игра занимает всю панель; отдельным окном — весь экран
            if (AppConfig.Settings.Fullscreen && !Nav.Ready) FullScreen.Hook(this);
            KeyPreview = true;

            Renderer.ShowHitboxes = AppConfig.Settings.ShowHitboxes;

            _w.Init(level, practice);
            _w.Sfx = Sfx.Play;
            _w.OnDeathEvent = OnDeath;
            _w.OnFinishEvent = OnFinish;
            _w.OnSongTrigger = (id, vol, start, loop) => _ = MusicPlayer.PlayAsync(id, start, vol, loop);

            _loop.TargetFps = AppConfig.Settings.Fps;
            _loop.Step += OnStep;
            _loop.Render += () => { try { Invalidate(); } catch { } };

            Shown += (s, e) => StartMusic();
            Shown += async (s, e) => await MpJoin();
            _mpTimer.Tick += async (s, e) => await MpTick();
            FormClosed += (s, e) =>
            {
                _loop.Dispose(); MusicPlayer.Stop(); SaveRun(); _buf?.Dispose(); _scene?.Dispose();
                _mpTimer.Stop();
                if (_mpRoom != null) _ = ApiClient.MpLeaveAsync(_mpRoom);
            };
            Shown += (s, e) => _loop.Start();
        }

        async void StartMusic()
        {
            MusicPlayer.SetVolume(AppConfig.Settings.MusicVolume);
            Sfx.Volume = (float)AppConfig.Settings.SfxVolume;
            int id = _level.Settings.SongId;
            if (id == 0) return;

            // id < 0 — встроенный саундтрек, он всегда рядом и играет без интернета
            bool ok = await MusicPlayer.PlayAsync(id, _level.Settings.SongOffsetSec, 1, true);
            if (ok) return;

            // не пошла — говорим прямо, что не так
            if (id > 0 && !MusicPlayer.IsCached(id))
                Toast(ApiClient.Online
                    ? "Трек #" + id + " не скачался с сервера — открой «Музыка» и нажми «Скачать»"
                    : "Трек #" + id + " не скачан, а сети нет — играем без музыки");
            else
                Toast("Музыка не пошла: " + (string.IsNullOrEmpty(MusicPlayer.LastError)
                                             ? "трек не готов" : MusicPlayer.LastError));
        }

        // ------------------------------------------------------------- цикл

        void OnStep(double dt)
        {
            if (_paused) return;

            if (_w.Dead)
            {
                _deathDelay += dt;
                if (_deathDelay > 0.55) Respawn();
                return;
            }
            if (_w.Finished) return;

            int mx = (_btnRight || _keyRight ? 1 : 0) - (_btnLeft || _keyLeft ? 1 : 0);
            var i1 = new PlayerInput { Hold = _hold, Pressed = _pressed, MoveX = mx };
            var i2 = new PlayerInput { Hold = _hold2, Pressed = _pressed2, MoveX = mx };
            _pressed = false; _pressed2 = false;

            _w.Update(dt, i1, i2);
            if (_toastT > 0) _toastT -= dt;
        }

        void OnDeath()
        {
            _deathDelay = 0;
            _bestPercent = Math.Max(_bestPercent, _w.Progress);
        }

        void Respawn()
        {
            _attempts++;
            _w.Attempts = _attempts;
            _w.Restart();
            _deathDelay = 0;
            // музыка не обрывается: просто перематывается к началу попытки
            if (_level.Settings.SongId == 0) return;
            double to = _level.Settings.SongOffsetSec;
            if (_practice && _w.Checkpoints.Count > 0) to += _w.Checkpoints[^1].Time;

            if (MusicPlayer.Playing) MusicPlayer.Seek(to);
            else _ = MusicPlayer.PlayAsync(_level.Settings.SongId, to, 1, true);
        }

        void OnFinish()
        {
            _bestPercent = 1;
            Toast("Уровень пройден!");
            Toasts.Show("Уровень пройден", _level.Settings.Name + " — попыток: " + _attempts,
                        Toasts.Kind.Star);
            SaveRun();
        }

        void SaveRun()
        {
            if (_testMode) return;
            double pct = Math.Max(_bestPercent, _w.Progress) * 100;
            LocalStore.RecordRun(_level, pct, _attempts, _w.CoinsGot, _w.Finished, _w.OrbsUsed);
        }

        void Toast(string s) { _toast = s; _toastT = 2.5; }

        // ------------------------------------------------------------- ввод

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Space:
                case Keys.Up:
                case Keys.W:
                    if (!_hold) _pressed = true;
                    _hold = true;
                    e.Handled = true;
                    break;
                case Keys.ControlKey:
                case Keys.Down:
                case Keys.S:
                    if (!_hold2) _pressed2 = true;
                    _hold2 = true;
                    break;
                case Keys.Left:
                case Keys.A:
                    _keyLeft = true;
                    break;
                case Keys.Right:
                case Keys.D:
                    _keyRight = true;
                    break;
                case Keys.Escape:
                    TogglePause();
                    break;
                case Keys.R:
                    _attempts++; _w.Restart(); break;
                case Keys.Z:
                    if (_practice) { _w.AddCheckpoint(_w.P1); Toast("Чекпоинт поставлен"); }
                    break;
                case Keys.X:
                    if (_practice) { _w.RemoveCheckpoint(); Toast("Чекпоинт убран"); }
                    break;
                case Keys.F1:
                    Renderer.ShowHitboxes = !Renderer.ShowHitboxes;
                    break;
                case Keys.F2:
                    _practice = !_practice;
                    _w.Practice = _practice;
                    Toast(_practice ? "Режим тренировки включён" : "Обычный режим");
                    break;
                case Keys.F3:
                    _w.NoClipCheat = !_w.NoClipCheat;
                    Toast(_w.NoClipCheat ? "NoClip (тест)" : "NoClip выключен");
                    break;
                case Keys.F11:
                    WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
                    break;
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Space or Keys.Up or Keys.W) _hold = false;
            if (e.KeyCode is Keys.ControlKey or Keys.Down or Keys.S) _hold2 = false;
            if (e.KeyCode is Keys.Left or Keys.A) _keyLeft = false;
            if (e.KeyCode is Keys.Right or Keys.D) _keyRight = false;
            base.OnKeyUp(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                string id = HitId(Scene(e.Location));

                // режим настройки интерфейса: тащим кнопки по экрану
                if (_guiEdit)
                {
                    switch (id)
                    {
                        case "g:done": _guiEdit = false; AppConfig.Save(); Toast("Расположение сохранено"); Invalidate(); return;
                        case "g:reset": Gui.Reset(); AppConfig.Save(); Invalidate(); return;
                        case "g:plus": Resize(1.12); return;
                        case "g:minus": Resize(1 / 1.12); return;
                    }
                    foreach (var (r, nm) in _hit)
                    {
                        if (!nm.StartsWith("i:") || !r.Contains(Scene(e.Location))) continue;
                        _dragItem = ItemByName(nm.Substring(2));
                        var sp = Scene(e.Location);
                        _dragOff = new PointF(sp.X - r.X - r.Width / 2, sp.Y - r.Y - r.Height / 2);
                        return;
                    }
                    return;
                }

                if (_paused)
                {
                    switch (id)
                    {
                        case "p:resume": SetPaused(false); return;
                        case "p:restart": _attempts++; _w.Restart(); SetPaused(false); return;
                        case "p:gui": _guiEdit = true; Toast("Тащи кнопки куда удобно"); Invalidate(); return;
                        case "p:practice":
                            _practice = !_practice; _w.Practice = _practice;
                            Toast(_practice ? "Режим тренировки включён" : "Обычный режим"); Invalidate(); return;
                        case "p:exit": Close(); return;
                    }
                    return;
                }

                switch (id)
                {
                    case "menu": SetPaused(true); return;
                    case "left": _btnLeft = true; return;
                    case "right": _btnRight = true; return;
                    case "jump": if (!_hold) _pressed = true; _hold = true; return;
                }

                if (!_hold) _pressed = true;
                _hold = true;
            }
            if (e.Button == MouseButtons.Right) { if (!_hold2) _pressed2 = true; _hold2 = true; }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_guiEdit && _dragItem != null)
            {
                var mp = Scene(e.Location);
                _dragItem.X = Math.Clamp((mp.X - _dragOff.X) / Math.Max(1, View.Width), 0.02, 0.98);
                _dragItem.Y = Math.Clamp((mp.Y - _dragOff.Y) / Math.Max(1, View.Height), 0.02, 0.98);
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _hold = false; _btnLeft = false; _btnRight = false;
                if (_dragItem != null) { _dragItem = null; AppConfig.Save(); }
            }
            if (e.Button == MouseButtons.Right) _hold2 = false;
            base.OnMouseUp(e);
        }

        void Resize(double k)
        {
            foreach (var (_, it) in Gui.Items()) it.Size = Math.Clamp(it.Size * k, 0.03, 0.6);
            AppConfig.Save();
            Invalidate();
        }

        GuiItem ItemByName(string name)
        {
            foreach (var (n, it) in Gui.Items()) if (n == name) return it;
            return null;
        }

        string HitId(Point pt)
        {
            for (int i = _hit.Count - 1; i >= 0; i--)
                if (_hit[i].r.Contains(pt)) return _hit[i].id;
            return null;
        }

        /// <summary>Прямоугольник элемента интерфейса по его доле экрана.</summary>
        RectangleF RectOf(GuiItem it, float aspect = 1f)
        {
            float s = (float)(it.Size * View.Height);
            float w = s * aspect, h = s;
            return new RectangleF((float)(it.X * View.Width) - w / 2,
                                  (float)(it.Y * View.Height) - h / 2, w, h);
        }

        void TogglePause() => SetPaused(!_paused);

        /// <summary>Пауза останавливает и музыку — в том числе когда окно свернули или ушли в другое приложение.</summary>
        void SetPaused(bool value)
        {
            if (_paused == value) return;
            _paused = value;
            if (_paused) MusicPlayer.Pause(); else MusicPlayer.Resume();
            Invalidate();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            // другое приложение получило фокус — ставим игру и музыку на паузу
            BeginInvoke(() => { if (ActiveForm == null) SetPaused(true); });
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized) SetPaused(true);
        }

        // ------------------------------------------------------------- рендер

        Bitmap _buf;
        Bitmap _scene;
        double _sceneK = 1;

        /// <summary>
        /// Размер картинки, в которой считается кадр. На большом мониторе
        /// рисовать каждый пиксель градиентами и свечением дорого, поэтому
        /// кадр готовится в картинке заданной высоты (настройка качества) и
        /// одним куском растягивается на окно. Соотношение сторон берётся
        /// от окна — значит видно ровно тот же кусок уровня.
        /// </summary>
        Size View
        {
            get
            {
                int w = Math.Max(1, View.Width), h = Math.Max(1, View.Height);
                int qh = AppConfig.Settings.RenderHeight;
                if (qh <= 0 || h <= qh + 40) return new Size(w, h);
                return new Size(Math.Max(1, (int)Math.Round(qh * (w / (double)h))), qh);
            }
        }

        /// <summary>Точка окна — в координаты картинки кадра.</summary>
        Point Scene(Point p) =>
            _sceneK == 1 ? p : new Point((int)Math.Round(p.X * _sceneK), (int)Math.Round(p.Y * _sceneK));

        bool _firstFrame = true;

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_firstFrame) { _firstFrame = false; AppConfig.Trace("первый кадр уровня нарисован"); }
            var size = View;
            _sceneK = size.Height / (double)Math.Max(1, ClientSize.Height);

            if (size == ClientSize) { PaintScene(e.Graphics, size); return; }

            if (_scene == null || _scene.Width != size.Width || _scene.Height != size.Height)
            {
                _scene?.Dispose();
                _scene = new Bitmap(size.Width, size.Height);
            }
            using (var sg = Graphics.FromImage(_scene)) PaintScene(sg, size);

            var og = e.Graphics;
            og.InterpolationMode = InterpolationMode.Bilinear;
            og.PixelOffsetMode = PixelOffsetMode.Half;
            og.DrawImage(_scene, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height),
                         0, 0, _scene.Width, _scene.Height, GraphicsUnit.Pixel);
            og.InterpolationMode = InterpolationMode.Default;
            og.PixelOffsetMode = PixelOffsetMode.Default;
        }

        /// <summary>Нарисовать кадр целиком в заданную поверхность.</summary>
        void PaintScene(Graphics g, Size size)
        {
            _v.Client = size;
            _v.CamX = _w.Cam.CamX + _w.GameplayOffsetX;
            _v.CamY = _w.Cam.CamY + _w.GameplayOffsetY;
            _v.Zoom = _w.Cam.Zoom;

            if (_w.Fx.AnyPost) DrawWithFx(g);
            else Renderer.Draw(g, _w, _v, false);
            DrawOthers(g);
            DrawHud(g);
            if (_paused) DrawPause(g);
            if (_w.Dead) DrawDeath(g);
            if (_w.Finished) DrawWin(g);
        }

        /// <summary>Экранные эффекты 2.2: сначала рисуем мир в картинку, потом портим её как надо.</summary>
        void DrawWithFx(Graphics g)
        {
            int w = Math.Max(1, View.Width), h = Math.Max(1, View.Height);
            if (_buf == null || _buf.Width != w || _buf.Height != h)
            {
                _buf?.Dispose();
                _buf = new Bitmap(w, h);
            }

            using (var bg = Graphics.FromImage(_buf))
            {
                bg.Clear(Color.Black);
                Renderer.Draw(bg, _w, _v, false);
            }

            var fx = _w.Fx;
            Image img = _buf;

            // крупные пиксели
            if (fx.Pixelate.On)
            {
                int k = (int)Math.Clamp(2 + fx.Pixelate.K * 26, 2, 40);
                using var small = new Bitmap(Math.Max(1, w / k), Math.Max(1, h / k));
                using (var sg = Graphics.FromImage(small))
                {
                    sg.InterpolationMode = InterpolationMode.Bilinear;
                    sg.DrawImage(img, 0, 0, small.Width, small.Height);
                }
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                DrawColored(g, small, new Rectangle(0, 0, w, h), fx);
                g.InterpolationMode = InterpolationMode.Default;
                return;
            }

            // размытие — уменьшили и вернули обратно
            if (fx.Blur.On)
            {
                int k = (int)Math.Clamp(2 + fx.Blur.K * 10, 2, 14);
                using var small = new Bitmap(Math.Max(1, w / k), Math.Max(1, h / k));
                using (var sg = Graphics.FromImage(small))
                {
                    sg.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    sg.DrawImage(img, 0, 0, small.Width, small.Height);
                }
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                DrawColored(g, small, new Rectangle(0, 0, w, h), fx);
                g.InterpolationMode = InterpolationMode.Default;
                return;
            }

            // глюк — полосы со сдвигом
            if (fx.Glitch.On)
            {
                var rnd = new Random((int)(Environment.TickCount64 / 60));
                int slices = 14;
                for (int i = 0; i < slices; i++)
                {
                    int y = h * i / slices, hh = h / slices + 1;
                    int off = (int)((rnd.NextDouble() * 2 - 1) * 40 * fx.Glitch.K);
                    var dst = new Rectangle(off, y, w, hh);
                    var src = new Rectangle(0, y, w, hh);
                    g.DrawImage(img, dst, src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel);
                }
                return;
            }

            DrawColored(g, img, new Rectangle(0, 0, w, h), fx);
        }

        /// <summary>Инверсия, ч/б и хроматика — через цветовую матрицу.</summary>
        static void DrawColored(Graphics g, Image img, Rectangle dst, ScreenFx fx)
        {
            if (fx.Chromatic.On)
            {
                int off = (int)Math.Clamp(fx.Chromatic.K * 18, 1, 24);
                DrawChannel(g, img, new Rectangle(dst.X - off, dst.Y, dst.Width, dst.Height), 1, 0, 0);
                DrawChannel(g, img, dst, 0, 1, 0);
                DrawChannel(g, img, new Rectangle(dst.X + off, dst.Y, dst.Width, dst.Height), 0, 0, 1);
                return;
            }

            float inv = (float)fx.Invert.K, gray = (float)fx.Gray.K;
            if (inv < 0.001f && gray < 0.001f)
            {
                g.DrawImage(img, dst);
                return;
            }

            // смешиваем обычные цвета с инверсией и серым
            float r = 1 - inv * 2, gr = 0.299f * gray, gg = 0.587f * gray, gb = 0.114f * gray;
            var m = new ColorMatrix(new[]
            {
                new[] { r * (1 - gray) + gr, gr, gr, 0f, 0f },
                new[] { gg, r * (1 - gray) + gg, gg, 0f, 0f },
                new[] { gb, gb, r * (1 - gray) + gb, 0f, 0f },
                new[] { 0f, 0f, 0f, 1f, 0f },
                new[] { inv, inv, inv, 0f, 1f }
            });
            using var attr = new ImageAttributes();
            attr.SetColorMatrix(m);
            g.DrawImage(img, dst, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, attr);
        }

        static void DrawChannel(Graphics g, Image img, Rectangle dst, float r, float gg, float b)
        {
            var m = new ColorMatrix(new[]
            {
                new[] { r, 0f, 0f, 0f, 0f },
                new[] { 0f, gg, 0f, 0f, 0f },
                new[] { 0f, 0f, b, 0f, 0f },
                new[] { 0f, 0f, 0f, 1f, 0f },
                new[] { 0f, 0f, 0f, 0f, 1f }
            });
            using var attr = new ImageAttributes();
            attr.SetColorMatrix(m);
            var old = g.CompositingMode;
            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
            g.DrawImage(img, dst, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, attr);
            g.CompositingMode = old;
        }

        // ------------------------------------------------------------- мультиплеер

        async Task MpJoin()
        {
            if (!_level.Settings.Multiplayer || _testMode) return;
            if (!AppConfig.LoggedIn) { Toast("Мультиплеер — только с аккаунтом"); return; }

            int id = _level.Settings.ServerId;
            if (id <= 0) { Toast("Залей уровень на сервер, тогда будет мультиплеер"); return; }

            var r = await ApiClient.MpJoinAsync(id, Math.Clamp(_level.Settings.MaxPlayers, 2, 50));
            if (!r.Ok || r.Data == null || string.IsNullOrEmpty(r.Data.Room))
            {
                Toast("Мультиплеер недоступен: " + (r.Error ?? "нет связи"));
                return;
            }
            _mpRoom = r.Data.Room;
            _mpMax = r.Data.Max;
            Toast($"Мультиплеер: комната на {r.Data.Max} игроков");
            _mpTimer.Start();
        }

        async Task MpTick()
        {
            if (_mpRoom == null || _mpBusy) return;
            _mpBusy = true;
            try
            {
                var p = _w.P1;
                var r = await ApiClient.MpStateAsync(_mpRoom, p.X, p.Y, (int)p.Mode,
                    p.GravityDir, p.Mini, _w.Dead, _w.Progress * 100,
                    _w.Colors.Eff(ColorSystem.ChP1).ToArgb());
                if (r.Ok && r.Data != null)
                {
                    _mpPlayers = r.Data.Players ?? new List<MpPlayerDto>();
                    _mpMax = r.Data.Max;
                }
            }
            catch { }
            finally { _mpBusy = false; }
        }

        /// <summary>Другие игроки — полупрозрачные кубики с именами.</summary>
        void DrawOthers(Graphics g)
        {
            if (_mpRoom == null || _mpPlayers.Count == 0) return;
            foreach (var o in _mpPlayers)
            {
                double size = o.Mini ? 22 : 30;
                var r = _v.WorldRect(o.X - size / 2, o.Y - size / 2, size, size);
                if (r.Right < -60 || r.X > View.Width + 60) continue;

                var col = Color.FromArgb(o.Color == 0 ? unchecked((int)0xFF66CCFF) : o.Color);
                using (var br = new SolidBrush(Color.FromArgb(o.Dead ? 70 : 150, col)))
                    g.FillRectangle(br, r);
                using (var pen = new Pen(Color.FromArgb(o.Dead ? 90 : 200, 10, 10, 25), 2.5f))
                    g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                using var f = Gd.Bold(8.5f);
                Gd.Outlined(g, o.Name, f, new RectangleF(r.X - 50, r.Y - 22, r.Width + 100, 18),
                    Color.FromArgb(230, 235, 245, 255), thick: 2f);
            }
        }

        void DrawHud(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            _hit.Clear();
            int w = View.Width;
            int alpha = (int)Math.Clamp(Gui.Opacity * 255, 40, 255);

            // ---- полоса прогресса
            var pr = Gui.Progress;
            float pw = (float)(pr.Size * w), ph = Math.Max(14f, View.Height * 0.03f);
            var bar = new RectangleF((float)(pr.X * w) - pw / 2, (float)(pr.Y * View.Height) - ph / 2, pw, ph);
            using (var path = Gd.Round(bar, ph / 2))
            {
                using var br = new SolidBrush(Color.FromArgb(alpha, 10, 12, 26));
                g.FillPath(br, path);
            }
            var fill = new RectangleF(bar.X + 3, bar.Y + 3, Math.Max(0, (bar.Width - 6) * (float)_w.Progress), bar.Height - 6);
            if (fill.Width > 2)
                using (var path = Gd.Round(fill, (bar.Height - 6) / 2))
                using (var br = new LinearGradientBrush(new RectangleF(fill.X, fill.Y - 1, fill.Width, fill.Height + 2),
                           Color.FromArgb(140, 255, 170), Color.FromArgb(20, 170, 110), LinearGradientMode.Vertical))
                    g.FillPath(br, path);
            using (var path = Gd.Round(bar, ph / 2))
            using (var pen = new Pen(Gd.Ink, 3f))
                g.DrawPath(pen, path);
            using (var f = Gd.Title(Math.Max(8f, ph * 0.52f)))
                Gd.Outlined(g, $"{_w.Progress * 100:0}%", f, bar, Gd.Paper, thick: 2.6f);
            _hit.Add((bar, "i:Полоса прогресса"));

            // чекпоинты в тренировке
            if (_practice)
                foreach (var cp in _w.Checkpoints)
                {
                    float x = bar.X + (float)(bar.Width * Math.Clamp(cp.X / Math.Max(1, _w.RightEdge), 0, 1));
                    using var pen = new Pen(Color.FromArgb(220, 120, 255, 160), 2f);
                    g.DrawLine(pen, x, bar.Y, x, bar.Bottom);
                }

            // ---- кнопка «=» слева сверху
            var mr = RectOf(Gui.Menu);
            Gd.ButtonPlate(g, mr, Color.FromArgb(150, 160, 205), Color.FromArgb(60, 66, 110), mr.Height * 0.26f, false, false);
            using (var pen = new Pen(Gd.Paper, Math.Max(2.5f, mr.Height * 0.09f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float cx = mr.X + mr.Width / 2, cy = mr.Y + mr.Height / 2, half = mr.Width * 0.26f;
                g.DrawLine(pen, cx - half, cy - mr.Height * 0.12f, cx + half, cy - mr.Height * 0.12f);
                g.DrawLine(pen, cx - half, cy + mr.Height * 0.12f, cx + half, cy + mr.Height * 0.12f);
            }
            _hit.Add((mr, _guiEdit ? "i:Кнопка =" : "menu"));

            // ---- кнопки платформера
            if (_w.Platformer || _guiEdit)
            {
                PadButton(g, RectOf(Gui.Left), "◄", "left", _btnLeft);
                PadButton(g, RectOf(Gui.Right), "►", "right", _btnRight);
                PadButton(g, RectOf(Gui.Jump), "▲", "jump", _hold);
            }

            // ---- сведения
            using (var f = Gd.Title(12f))
                Gd.Outlined(g, "ПОПЫТКА " + _attempts, f,
                    new RectangleF(mr.Right + 12, mr.Y, 260, mr.Height), Gd.Paper, StringAlignment.Near, thick: 3f);
            using (var f = Gd.Bold(9.5f))
            {
                string info = $"{Modes.Label(_w.P1.Mode)} · {Speeds.Label(_w.P1.Speed)}" +
                              (_w.P1.Mini ? " · мини" : "") + (_w.DualMode ? " · дуал" : "") +
                              (_practice ? " · тренировка" : "");
                Gd.Outlined(g, info, f, new RectangleF(mr.Right + 12, mr.Bottom - 4, 420, 22),
                    Color.FromArgb(235, 215, 230, 255), StringAlignment.Near, thick: 2.2f);

                if (_w.CoinsGot > 0)
                {
                    for (int i = 0; i < _w.CoinsGot; i++)
                        Gd.DrawCoin(g, new RectangleF(mr.Right + 12 + i * 26, mr.Bottom + 20, 22, 22), true);
                }

                if (AppConfig.Settings.ShowFps)
                    Gd.Outlined(g, $"{_loop.Fps:0} FPS", f, new RectangleF(w - 120, 10, 110, 22),
                        Color.FromArgb(220, 200, 215, 255), StringAlignment.Far, thick: 2f);
                if (!ApiClient.Online)
                    Gd.Outlined(g, "ОФЛАЙН", f, new RectangleF(w - 120, 32, 110, 22), Gd.Gold, StringAlignment.Far, thick: 2f);
                if (_mpRoom != null)
                    Gd.Outlined(g, $"В КАРТЕ: {_mpPlayers.Count + 1} / {_mpMax}", f,
                        new RectangleF(w - 260, 54, 250, 22), Gd.Lime, StringAlignment.Far, thick: 2f);
            }

            // в лаве — обратный отсчёт до смерти
            if (_w.P1.LavaTime > 0.05)
            {
                float lw = Math.Min(420, w * 0.34f);
                var lr = new RectangleF(w / 2f - lw / 2, View.Height - 150, lw, 26);
                double left = Math.Max(0, Phys.LavaSeconds - _w.P1.LavaTime);
                using (var path = Gd.Round(lr, 13f))
                using (var br = new SolidBrush(Color.FromArgb(200, 40, 8, 4)))
                    g.FillPath(br, path);
                var lavaFill = new RectangleF(lr.X + 3, lr.Y + 3,
                    (float)((lr.Width - 6) * (left / Phys.LavaSeconds)), lr.Height - 6);
                if (lavaFill.Width > 2)
                    using (var path = Gd.Round(lavaFill, 10f))
                    using (var br = new LinearGradientBrush(new RectangleF(lavaFill.X, lavaFill.Y - 1, lavaFill.Width, lavaFill.Height + 2),
                               Color.FromArgb(255, 210, 90), Color.FromArgb(220, 60, 20), LinearGradientMode.Vertical))
                        g.FillPath(br, path);
                using (var path = Gd.Round(lr, 13f))
                using (var pen = new Pen(Gd.Ink, 3f))
                    g.DrawPath(pen, path);
                using (var f = Gd.Title(11f))
                    Gd.Outlined(g, $"ЛАВА — {left:0.0} с", f, lr, Gd.Paper, thick: 2.6f);
            }

            if (_toastT > 0)
                using (var f = Gd.Title(14f))
                    Gd.Outlined(g, _toast, f, new RectangleF(0, View.Height - 96, w, 32), Gd.Gold, thick: 3f);

            if (_guiEdit) DrawGuiEditor(g);
        }

        void PadButton(Graphics g, RectangleF r, string sign, string id, bool down)
        {
            Gd.ButtonPlate(g, r, Color.FromArgb(120, 190, 255), Color.FromArgb(25, 85, 180), r.Height * 0.26f, down, false);
            using (var f = Gd.Title(Math.Max(10f, r.Height * 0.34f)))
                Gd.Outlined(g, sign, f, r, Gd.Paper, thick: 3f);
            _hit.Add((r, _guiEdit ? "i:" + (id == "left" ? "Влево" : id == "right" ? "Вправо" : "Прыжок") : id));
        }

        /// <summary>Режим «настроить GUI»: подписи и кнопки управления.</summary>
        void DrawGuiEditor(Graphics g)
        {
            using (var br = new SolidBrush(Color.FromArgb(120, 8, 10, 24)))
                g.FillRectangle(br, ClientRectangle);

            foreach (var (name, it) in Gui.Items())
            {
                var r = RectOf(it, name == "Полоса прогресса" ? 1f : 1f);
                if (name == "Полоса прогресса")
                    r = new RectangleF((float)(it.X * View.Width) - (float)(it.Size * View.Width) / 2,
                                       (float)(it.Y * View.Height) - 14, (float)(it.Size * View.Width), 28);
                using var pen = new Pen(Gd.Neon, 2.5f) { DashStyle = DashStyle.Dash };
                g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                using var f = Gd.Bold(9f);
                Gd.Outlined(g, name, f, new RectangleF(r.X, r.Y - 20, r.Width, 18), Gd.Neon, thick: 2.2f);
            }

            float bw = 160, bh = 54, y = View.Height - bh - 18;
            float x = View.Width / 2f - (bw * 2 + 54 * 2 + 30) / 2f;
            Btn(g, new RectangleF(x, y, bw, bh), "ГОТОВО", "g:done", Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100));
            Btn(g, new RectangleF(x + bw + 10, y, bw, bh), "СБРОС", "g:reset", Color.FromArgb(255, 180, 90), Color.FromArgb(170, 95, 10));
            Btn(g, new RectangleF(x + bw * 2 + 20, y, 54, bh), "+", "g:plus", Color.FromArgb(150, 160, 205), Color.FromArgb(60, 66, 110));
            Btn(g, new RectangleF(x + bw * 2 + 80, y, 54, bh), "−", "g:minus", Color.FromArgb(150, 160, 205), Color.FromArgb(60, 66, 110));

            using var ft = Gd.Bold(11f);
            Gd.Outlined(g, "тащи кнопки мышью · + и − меняют размер · ГОТОВО — сохранить", ft,
                new RectangleF(0, y - 34, View.Width, 24), Gd.Paper, thick: 2.4f);
        }

        void Btn(Graphics g, RectangleF r, string text, string id, Color c1, Color c2)
        {
            Gd.ButtonPlate(g, r, c1, c2, r.Height * 0.26f, false, false);
            using var f = Gd.Title(Math.Max(8f, r.Height * 0.3f));
            Gd.Outlined(g, text, f, r, Gd.Paper, thick: 2.8f);
            _hit.Add((r, id));
        }

        void DrawPause(Graphics g)
        {
            using (var br = new SolidBrush(Color.FromArgb(185, 10, 12, 26)))
                g.FillRectangle(br, ClientRectangle);

            using (var f = Gd.Title(34f))
                Gd.Outlined(g, "ПАУЗА", f, new RectangleF(0, 70, View.Width, 50), Gd.Paper, thick: 5f);
            using (var f = Gd.Bold(11f))
                Gd.Outlined(g, $"{_level.Settings.Name}   ·   лучший результат {_bestPercent * 100:0}%   ·   попыток {_attempts}", f,
                    new RectangleF(0, 124, View.Width, 24), Color.FromArgb(235, 215, 230, 255), thick: 2.4f);

            float bw = 300, bh = 62, gap = 14;
            float x = View.Width / 2f - bw / 2, y = 190;
            Btn(g, new RectangleF(x, y, bw, bh), "ПРОДОЛЖИТЬ", "p:resume", Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100));
            y += bh + gap;
            Btn(g, new RectangleF(x, y, bw, bh), "ЗАНОВО", "p:restart", Color.FromArgb(255, 190, 90), Color.FromArgb(175, 100, 10));
            y += bh + gap;
            Btn(g, new RectangleF(x, y, bw, bh), "НАСТРОИТЬ GUI", "p:gui", Color.FromArgb(140, 200, 255), Color.FromArgb(30, 95, 190));
            y += bh + gap;
            Btn(g, new RectangleF(x, y, bw, bh), _practice ? "ТРЕНИРОВКА: ВКЛ" : "ТРЕНИРОВКА: ВЫКЛ", "p:practice",
                Color.FromArgb(200, 170, 255), Color.FromArgb(100, 60, 180));
            y += bh + gap;
            Btn(g, new RectangleF(x, y, bw, bh), "ВЫХОД В МЕНЮ", "p:exit", Color.FromArgb(240, 120, 120), Color.FromArgb(150, 40, 50));

            using var fh = Gd.Bold(9.5f);
            Gd.Outlined(g, "Esc — продолжить · R — заново · Z/X — чекпоинт · F1 хитбоксы · F11 на весь экран", fh,
                new RectangleF(0, View.Height - 40, View.Width, 22),
                Color.FromArgb(220, 200, 215, 255), thick: 2.2f);
        }

        void DrawDeath(Graphics g)
        {
            using var br = new SolidBrush(Color.FromArgb(70, 160, 20, 30));
            g.FillRectangle(br, ClientRectangle);
        }

        void DrawWin(Graphics g)
        {
            Overlay(g, "УРОВЕНЬ ПРОЙДЕН", new[]
            {
                $"{_level.Settings.Name}",
                $"Попыток: {_attempts}   Монет: {_w.CoinsGot}/3",
                $"Время: {_w.Time:0.0} c",
                "",
                ApiClient.Online ? "Результат отправлен на сервер" : "Офлайн — результат уедет при появлении сети",
                "Закрой окно, чтобы выйти"
            });
        }

        void Overlay(Graphics g, string title, string[] lines)
        {
            using (var br = new SolidBrush(Color.FromArgb(205, 12, 14, 26))) g.FillRectangle(br, ClientRectangle);
            using var tb = new SolidBrush(Theme.Text);
            var sz = g.MeasureString(title, Theme.H1);
            g.DrawString(title, Theme.H1, tb, View.Width / 2 - sz.Width / 2, 120);
            int y = 200;
            using var db = new SolidBrush(Theme.Dim);
            foreach (var l in lines)
            {
                var s = g.MeasureString(l, Theme.Body);
                g.DrawString(l, Theme.Body, db, View.Width / 2 - s.Width / 2, y);
                y += 24;
            }
        }
    }
}
