using System.Drawing.Drawing2D;
using GEO_DASH.Audio;
using GEO_DASH.Core;
using GEO_DASH.Net;
using GEO_DASH.Render;

namespace GEO_DASH.UI
{
    public class GameForm : Form
    {
        readonly GameWorld _w = new();
        readonly ViewTransform _v = new();
        readonly GameLoop _loop = new();
        readonly LevelData _level;

        bool _hold, _pressed, _hold2, _pressed2;
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

        public GameForm(LevelData level, bool practice = false, bool testMode = false)
        {
            _level = level;
            _practice = practice;
            _testMode = testMode;

            Theme.Apply(this, level.Settings.Name);
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            ClientSize = new Size(1280, 720);
            WindowState = AppConfig.Settings.Fullscreen ? FormWindowState.Maximized : FormWindowState.Normal;
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

            Load += (s, e) => StartMusic();
            FormClosed += (s, e) => { _loop.Dispose(); MusicPlayer.Stop(); SaveRun(); };
            Shown += (s, e) => _loop.Start();
        }

        async void StartMusic()
        {
            MusicPlayer.SetVolume(AppConfig.Settings.MusicVolume);
            Sfx.Volume = (float)AppConfig.Settings.SfxVolume;
            int id = _level.Settings.SongId;
            if (id > 0)
            {
                if (!MusicPlayer.IsCached(id) && !ApiClient.Online)
                    Toast("Трек #" + id + " не скачан — играем без музыки");
                else
                    await MusicPlayer.PlayAsync(id, _level.Settings.SongOffsetSec, 1, true);
            }
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

            var i1 = new PlayerInput { Hold = _hold, Pressed = _pressed };
            var i2 = new PlayerInput { Hold = _hold2, Pressed = _pressed2 };
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
            if (_level.Settings.SongId > 0 && !_practice)
                MusicPlayer.Seek(_level.Settings.SongOffsetSec);
            else if (_practice && _w.Checkpoints.Count > 0)
                MusicPlayer.Seek(_level.Settings.SongOffsetSec + _w.Checkpoints[^1].Time);
        }

        void OnFinish()
        {
            _bestPercent = 1;
            Toast("Уровень пройден!");
            SaveRun();
        }

        void SaveRun()
        {
            if (_testMode) return;
            double pct = Math.Max(_bestPercent, _w.Progress) * 100;
            LocalStore.RecordRun(_level, pct, _attempts, _w.CoinsGot, _w.Finished);
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
            base.OnKeyUp(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { if (!_hold) _pressed = true; _hold = true; }
            if (e.Button == MouseButtons.Right) { if (!_hold2) _pressed2 = true; _hold2 = true; }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) _hold = false;
            if (e.Button == MouseButtons.Right) _hold2 = false;
            base.OnMouseUp(e);
        }

        void TogglePause()
        {
            _paused = !_paused;
            if (_paused) MusicPlayer.Pause(); else MusicPlayer.Resume();
            Invalidate();
        }

        // ------------------------------------------------------------- рендер

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            _v.Client = ClientSize;
            _v.CamX = _w.Cam.CamX;
            _v.CamY = _w.Cam.CamY;
            _v.Zoom = _w.Cam.Zoom;

            Renderer.Draw(g, _w, _v, false);
            DrawHud(g);
            if (_paused) DrawPause(g);
            if (_w.Dead) DrawDeath(g);
            if (_w.Finished) DrawWin(g);
        }

        void DrawHud(Graphics g)
        {
            int w = ClientSize.Width;
            // прогресс
            var bar = new Rectangle(w / 2 - 300, 14, 600, 18);
            using (var br = new SolidBrush(Color.FromArgb(120, 10, 12, 22))) g.FillRectangle(br, bar);
            using (var br = new LinearGradientBrush(bar, Theme.Accent, Theme.Good, LinearGradientMode.Horizontal))
                g.FillRectangle(br, new Rectangle(bar.X, bar.Y, (int)(bar.Width * _w.Progress), bar.Height));
            using (var pen = new Pen(Color.FromArgb(200, 255, 255, 255), 1.5f)) g.DrawRectangle(pen, bar);
            using (var br = new SolidBrush(Color.White))
                g.DrawString($"{_w.Progress * 100:0}%", Theme.Body, br, bar.Right + 8, bar.Y);

            // чекпоинты в тренировке
            if (_practice)
                foreach (var cp in _w.Checkpoints)
                {
                    float x = bar.X + (float)(bar.Width * Math.Clamp(cp.X / Math.Max(1, _w.RightEdge), 0, 1));
                    using var pen = new Pen(Color.FromArgb(220, 120, 255, 160), 2f);
                    g.DrawLine(pen, x, bar.Y, x, bar.Bottom);
                }

            using var tb = new SolidBrush(Color.FromArgb(230, 255, 255, 255));
            g.DrawString($"Попытка {_attempts}", Theme.H2, tb, 14, 12);
            string info = $"{Modes.Label(_w.P1.Mode)} · {Speeds.Label(_w.P1.Speed)}" +
                          (_w.P1.Mini ? " · мини" : "") + (_w.DualMode ? " · дуал" : "") +
                          (_practice ? " · тренировка" : "");
            using var db = new SolidBrush(Theme.Dim);
            g.DrawString(info, Theme.Body, db, 16, 44);

            if (_w.CoinsGot > 0)
                g.DrawString("Монеты: " + _w.CoinsGot, Theme.Body, tb, 16, 64);

            if (AppConfig.Settings.ShowFps)
                g.DrawString($"{_loop.Fps:0} FPS", Theme.Mono, db, w - 80, 14);

            if (!ApiClient.Online)
                g.DrawString("ОФЛАЙН", Theme.Mono, new SolidBrush(Theme.Accent2), w - 80, 32);

            if (_toastT > 0)
            {
                using var br = new SolidBrush(Color.FromArgb((int)Math.Clamp(_toastT * 120, 0, 220), 10, 12, 22));
                var sz = g.MeasureString(_toast, Theme.H2);
                var r = new RectangleF(w / 2 - sz.Width / 2 - 14, ClientSize.Height - 90, sz.Width + 28, sz.Height + 14);
                g.FillRectangle(br, r);
                g.DrawString(_toast, Theme.H2, tb, r.X + 14, r.Y + 7);
            }

            if (_w.Items.Count > 0)
            {
                int y = 86;
                foreach (var kv in _w.Items)
                {
                    g.DrawString($"item {kv.Key}: {kv.Value}", Theme.Mono, db, 16, y);
                    y += 16;
                }
            }
        }

        void DrawPause(Graphics g)
        {
            Overlay(g, "ПАУЗА", new[]
            {
                "Esc — продолжить",
                "R — заново",
                "F2 — режим тренировки (" + (_practice ? "вкл" : "выкл") + ")",
                "Z / X — поставить / убрать чекпоинт",
                "F1 — хитбоксы, F3 — noclip, F11 — на весь экран",
                "",
                $"Лучший результат: {_bestPercent * 100:0}%   Попыток: {_attempts}",
                "Закрой окно, чтобы выйти в меню"
            });
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
            g.DrawString(title, Theme.H1, tb, ClientSize.Width / 2 - sz.Width / 2, 120);
            int y = 200;
            using var db = new SolidBrush(Theme.Dim);
            foreach (var l in lines)
            {
                var s = g.MeasureString(l, Theme.Body);
                g.DrawString(l, Theme.Body, db, ClientSize.Width / 2 - s.Width / 2, y);
                y += 24;
            }
        }
    }
}
