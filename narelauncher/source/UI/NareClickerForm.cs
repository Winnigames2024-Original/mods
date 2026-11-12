using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Кликер 67. Тапаешь большую кнопку — монеты капают на аккаунт. Каждый тап
    /// стоит выносливости (как в Hamster Kombat): кончилась — ждёшь, пока
    /// восстановится. Есть пассивный доход и прокачки. Всё состояние — на
    /// сервере, поэтому переносится между компьютерами.
    /// </summary>
    public class NareClickerForm : Form
    {
        private readonly string _token;
        private readonly Color _accent;

        private long _coins, _energy, _energyMax = 1;
        private int _click = 67, _auto, _regen = 20;
        private long _pClick, _pAuto, _pEnergy, _pRegen;
        private int _pending;          // не отправленные тапы
        private bool _busyFlush;

        private readonly Label _coinsL = new Label();
        private readonly Label _rateL = new Label();
        private readonly Label _enL = new Label();
        private readonly Panel _enTrack = new Panel();
        private readonly Panel _enFill = new Panel();
        private readonly TapButton _tap = new TapButton();
        private readonly Button _upClick = new Button();
        private readonly Button _upAuto = new Button();
        private readonly Button _upEnergy = new Button();
        private readonly Button _upRegen = new Button();
        private readonly Label _flash = new Label();

        private readonly Timer _tick = new Timer { Interval = 1000 };    // локальный регЕН + авто
        private readonly Timer _flush = new Timer { Interval = 350 };    // отправка тапов
        private readonly Timer _sync = new Timer { Interval = 2500 };    // сверка с сервером

        public NareClickerForm(string token, Color accent)
        {
            _token = token; _accent = accent;

            Text = "Кликер 67";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(460, 660);
            MinimumSize = new Size(420, 600);
            BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.F(10f);
            try { Icon = SystemIcons.Application; } catch { }

            var hdr = new Label { Text = "КЛИКЕР 67", Font = Theme.F(20f, FontStyle.Bold), ForeColor = accent, AutoSize = false, Bounds = new Rectangle(0, 16, 460, 30), TextAlign = ContentAlignment.MiddleCenter, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(hdr);

            _coinsL.AutoSize = false; _coinsL.Bounds = new Rectangle(0, 50, 460, 40); _coinsL.TextAlign = ContentAlignment.MiddleCenter; _coinsL.Font = Theme.F(26f, FontStyle.Bold); _coinsL.ForeColor = Color.FromArgb(255, 210, 74); _coinsL.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; _coinsL.Text = "🪙 0";
            Controls.Add(_coinsL);

            _rateL.AutoSize = false; _rateL.Bounds = new Rectangle(0, 92, 460, 20); _rateL.TextAlign = ContentAlignment.MiddleCenter; _rateL.ForeColor = Theme.Dim; _rateL.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; _rateL.Text = "+67 за клик · +0/сек";
            Controls.Add(_rateL);

            // большая кнопка-тап
            _tap.Accent = accent; _tap.Bounds = new Rectangle(90, 130, 280, 280); _tap.Anchor = AnchorStyles.Top;
            _tap.Click += (s, e) => DoTap();
            Controls.Add(_tap);

            // выносливость
            _enL.AutoSize = false; _enL.Bounds = new Rectangle(30, 420, 400, 18); _enL.ForeColor = Theme.Text; _enL.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; _enL.Text = "Выносливость";
            Controls.Add(_enL);
            _enTrack.Bounds = new Rectangle(30, 440, 400, 20); _enTrack.BackColor = Color.FromArgb(28, 34, 50); _enTrack.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _enFill.Bounds = new Rectangle(0, 0, 0, 20); _enFill.BackColor = Color.FromArgb(86, 211, 100);
            _enTrack.Controls.Add(_enFill);
            Controls.Add(_enTrack);

            _flash.AutoSize = false; _flash.Bounds = new Rectangle(0, 464, 460, 18); _flash.TextAlign = ContentAlignment.MiddleCenter; _flash.ForeColor = Color.FromArgb(255, 107, 116); _flash.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_flash);

            // прокачки
            int uy = 492;
            StyleUp(_upClick, () => Upgrade("click")); _upClick.Bounds = new Rectangle(30, uy, 195, 48); Controls.Add(_upClick);
            StyleUp(_upAuto, () => Upgrade("auto")); _upAuto.Bounds = new Rectangle(235, uy, 195, 48); Controls.Add(_upAuto);
            StyleUp(_upEnergy, () => Upgrade("energy")); _upEnergy.Bounds = new Rectangle(30, uy + 56, 195, 48); Controls.Add(_upEnergy);
            StyleUp(_upRegen, () => Upgrade("regen")); _upRegen.Bounds = new Rectangle(235, uy + 56, 195, 48); Controls.Add(_upRegen);
            foreach (var b in new[] { _upClick, _upAuto, _upEnergy, _upRegen }) b.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            Refresh2();
            _tick.Tick += (s, e) => LocalTick();
            _flush.Tick += (s, e) => FlushTaps();
            _sync.Tick += (s, e) => Sync();
            Load += (s, e) => { _tick.Start(); _flush.Start(); _sync.Start(); Sync(); };
            FormClosing += (s, e) => { _tick.Stop(); _flush.Stop(); _sync.Stop(); FlushTaps(); };
        }

        private int _floats;
        private readonly Random _rnd = new Random();

        private void DoTap()
        {
            if (_energy < _click) { Flash("Нет выносливости — подожди восстановления"); return; }
            _coins += _click;
            _energy -= _click;
            _pending++;
            SpawnFloat(_click);
            Refresh2();
        }

        /// <summary>Вылетающее «+N» над кнопкой — эффект тапа.</summary>
        private void SpawnFloat(long amount)
        {
            if (_floats > 14) return;
            _floats++;
            var lbl = new Label
            {
                Text = "+" + amount.ToString("N0"),
                AutoSize = true,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(255, 210, 74),
                Font = Theme.F(15f, FontStyle.Bold)
            };
            int cx = _tap.Left + _tap.Width / 2 - 14 + _rnd.Next(-34, 34);
            lbl.Location = new Point(cx, _tap.Top - 4);
            Controls.Add(lbl); lbl.BringToFront();
            int step = 0;
            var t = new Timer { Interval = 40 };
            t.Tick += (s, e) =>
            {
                step++;
                lbl.Top -= 4;
                int k = Math.Min(255, step * 16);
                lbl.ForeColor = Color.FromArgb(
                    Lerp(255, Theme.Bg.R, k), Lerp(210, Theme.Bg.G, k), Lerp(74, Theme.Bg.B, k));
                if (step >= 16)
                {
                    t.Stop(); t.Dispose();
                    try { Controls.Remove(lbl); lbl.Dispose(); } catch { }
                    _floats--;
                }
            };
            t.Start();
        }

        private static int Lerp(int a, int b, int k) { return a + (b - a) * k / 255; }

        private void Flash(string t)
        {
            _flash.Text = t;
            var tmr = new Timer { Interval = 1200 }; tmr.Tick += (s, e) => { tmr.Stop(); tmr.Dispose(); _flash.Text = ""; }; tmr.Start();
        }

        private void FlushTaps()
        {
            if (_busyFlush || _pending <= 0) return;
            int n = _pending; _pending = 0; _busyFlush = true;
            new System.Threading.Thread(() =>
            {
                var r = NareEvents.ClickerClick(_token, n);
                Ui(() =>
                {
                    _busyFlush = false;
                    if (r != null && r.Ok) { _coins = r.Coins; if (r.Energy >= 0) _energy = r.Energy; Refresh2(); }
                });
            })
            { IsBackground = true }.Start();
        }

        private void Sync()
        {
            if (_pending > 0 || _busyFlush) return;   // не затираем неотправленные тапы
            new System.Threading.Thread(() =>
            {
                var r = NareEvents.ClickerGet(_token);
                Ui(() => { if (r != null && r.Ok) { Apply(r); } });
            })
            { IsBackground = true }.Start();
        }

        private void LocalTick()
        {
            // плавное восстановление и пассивный доход между сверками
            if (_energy < _energyMax) _energy = Math.Min(_energyMax, _energy + _regen);
            if (_auto > 0) _coins += _auto;
            Refresh2();
        }

        private void Upgrade(string what)
        {
            new System.Threading.Thread(() =>
            {
                var r = NareEvents.ClickerUpgrade(_token, what);
                Ui(() =>
                {
                    if (r == null) { Flash("Нет связи с сервером"); return; }
                    if (!r.Ok) { Flash(string.IsNullOrEmpty(r.Error) ? "Не вышло" : r.Error); return; }
                    Apply(r);
                });
            })
            { IsBackground = true }.Start();
        }

        private void Apply(ClickerState r)
        {
            _coins = r.Coins; _click = r.Click > 0 ? r.Click : _click; _auto = r.Auto;
            if (r.Energy >= 0) _energy = r.Energy;
            if (r.EnergyMax > 0) _energyMax = r.EnergyMax;
            if (r.Regen > 0) _regen = r.Regen;
            _pClick = r.PriceClick; _pAuto = r.PriceAuto; _pEnergy = r.PriceEnergy; _pRegen = r.PriceRegen;
            Refresh2();
        }

        private void Refresh2()
        {
            _coinsL.Text = "🪙 " + _coins.ToString("N0");
            _rateL.Text = "+" + _click + " за клик · +" + _auto + "/сек";
            if (_energyMax < 1) _energyMax = 1;
            _enL.Text = "Выносливость  " + Math.Max(0, _energy).ToString("N0") + " / " + _energyMax.ToString("N0") + "   (+" + _regen + "/сек)";
            int w = (int)Math.Round((double)_enTrack.ClientSize.Width * Math.Max(0, Math.Min(1.0, (double)_energy / _energyMax)));
            _enFill.Width = w; _enFill.Height = _enTrack.ClientSize.Height;
            _enFill.BackColor = _energy < _click ? Color.FromArgb(255, 107, 116) : Color.FromArgb(86, 211, 100);
            _tap.Enabled = true;
            long autoNext = _auto <= 0 ? 1 : (long)_auto * 2;
            _upClick.Text = "Клик ×2  →" + ((long)_click * 2).ToString("N0") + "\n" + _pClick.ToString("N0") + " 🪙";
            _upAuto.Text = "Доход ×2  →" + autoNext.ToString("N0") + "/с\n" + _pAuto.ToString("N0") + " 🪙";
            _upEnergy.Text = "Выносл. ×2  →" + ((long)_energyMax * 2).ToString("N0") + "\n" + _pEnergy.ToString("N0") + " 🪙";
            _upRegen.Text = "Восст. ×2  →" + ((long)_regen * 2).ToString("N0") + "/с\n" + _pRegen.ToString("N0") + " 🪙";
        }

        private void StyleUp(Button b, Action onClick)
        {
            b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0;
            b.BackColor = Color.FromArgb(22, 27, 40); b.ForeColor = Theme.Text; b.Font = Theme.F(9.5f, FontStyle.Bold);
            b.TextAlign = ContentAlignment.MiddleCenter; b.Cursor = Cursors.Hand;
            b.Click += (s, e) => onClick();
        }

        private void Ui(Action a) { try { if (IsHandleCreated) BeginInvoke(a); else a(); } catch { } }

        /// <summary>Большая круглая кнопка с «67».</summary>
        private class TapButton : Control
        {
            public Color Accent = Color.FromArgb(91, 140, 255);
            private bool _down;
            public TapButton()
            {
                DoubleBuffered = true; Cursor = Cursors.Hand;
                SetStyle(ControlStyles.SupportsTransparentBackColor, true); BackColor = Color.Transparent;
                MouseDown += (s, e) => { _down = true; Invalidate(); };
                MouseUp += (s, e) => { _down = false; Invalidate(); };
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                int pad = _down ? 10 : 4;
                var rc = new Rectangle(pad, pad, Width - pad * 2 - 1, Height - pad * 2 - 1);
                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(rc);
                    using (var br = new PathGradientBrush(path))
                    {
                        br.CenterColor = Lighten(Accent);
                        br.SurroundColors = new[] { Darken(Accent) };
                        g.FillPath(br, path);
                    }
                    using (var pen = new Pen(Lighten(Accent), 3)) g.DrawEllipse(pen, rc);
                }
                using (var f = new Font("Segoe UI", _down ? 72f : 76f, FontStyle.Bold))
                    TextRenderer.DrawText(g, "67", f, rc, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            private static Color Lighten(Color c) { return Color.FromArgb(Math.Min(255, c.R + 50), Math.Min(255, c.G + 50), Math.Min(255, c.B + 50)); }
            private static Color Darken(Color c) { return Color.FromArgb((int)(c.R * 0.55), (int)(c.G * 0.55), (int)(c.B * 0.55)); }
        }
    }
}
