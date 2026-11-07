using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Персонаж в рамке — для вкладки «Вещи». Вся работа у PlayerRenderer,
    /// здесь только таймер, фон и то, что он провожает курсор взглядом.
    /// </summary>
    public class PlayerStage : BaseControl
    {
        private readonly PlayerRenderer _player = new PlayerRenderer();
        private readonly Timer _timer;

        public bool ShowCaption = true;

        public PlayerStage()
        {
            Cursor = Cursors.Default;

            _timer = new Timer { Interval = 33 };
            _timer.Tick += delegate
            {
                if (!Visible || Width < 8 || Height < 8) return;

                var mouse = PointToClient(MousePosition);
                if (ClientRectangle.Contains(mouse)) _player.LookAt(mouse);
                else _player.LookAway();

                _player.Tick(0.033f);
                Invalidate();
            };
            _timer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _timer != null) _timer.Dispose();
            base.Dispose(disposing);
        }

        public void LoadFromAccount() { _player.LoadFromAccount(); Invalidate(); }
        public void ReloadCosmetics() { _player.ReloadCosmetics(); Invalidate(); }
        public void ShowList(List<Cosmetic> items) { _player.ShowList(items); Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _player.Poke(e.Location);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);

            var box = new Rectangle(0, 0, Width - 1, Height - 1);
            if (box.Width <= 8 || box.Height <= 8) return;

            Theme.FillRound(g, box, 12, Color.FromArgb(16, 20, 29));
            Theme.DrawRound(g, box, 12, Theme.Border);

            if (!_player.Ready)
            {
                Theme.Text1(g, "Скин не загружен", Theme.F(9f), Theme.Dim, box,
                    StringAlignment.Center, StringAlignment.Center);
                return;
            }

            _player.Paint(g, new Rectangle(0, 0, Width, Height - (ShowCaption ? 22 : 0)), false);

            if (!ShowCaption) return;

            string line = _player.ActTitle;
            if (!string.IsNullOrEmpty(_player.Caption)) line = _player.Caption + " · " + line;

            Theme.Text1(g, line, Theme.F(8.5f), Theme.Dim,
                new Rectangle(8, Height - 22, Width - 16, 16), StringAlignment.Center);
        }
    }
}
