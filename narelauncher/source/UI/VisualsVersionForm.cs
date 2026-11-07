using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Выбор версии NareVisuals. Сверху — «всегда последняя»: лаунчер сам
    /// подтягивает свежую при каждом запуске. Ниже список выпусков: можно
    /// закрепить любой, в том числе вернуться на старый.
    /// </summary>
    public class VisualsVersionForm : Form
    {
        /// <summary>Что выбрали: пусто — всегда последняя.</summary>
        public string Result = "";

        private readonly VirtualList _list = new VirtualList
        {
            ItemHeight = 58,
            EmptyText = "Список версий не пришёл"
        };

        private readonly NareLabel _status =
            new NareLabel("Спрашиваю GitHub…", 9f, FontStyle.Regular, Theme.Muted);

        private readonly NareButton _always =
            new NareButton { Text = "Всегда последняя", Style = BtnStyle.Ghost, Size = new Size(200, 40) };

        private readonly NareButton _pin =
            new NareButton { Text = "Поставить выбранную", Style = BtnStyle.Accent, Size = new Size(220, 40) };

        private readonly NareButton _close =
            new NareButton { Text = "Закрыть", Style = BtnStyle.Ghost, Size = new Size(130, 40) };

        private readonly string _current;
        private List<ModUpdater.Release> _releases = new List<ModUpdater.Release>();

        public VisualsVersionForm(string pinned)
        {
            _current = (pinned ?? "").Trim();
            Result = _current;

            Text = "Версия NareVisuals";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(10f);
            ClientSize = new Size(560, 460);

            _list.Renderer = Draw;
            _list.SetBounds(20, 86, 520, 288);
            Controls.Add(_list);

            _status.SetBounds(22, 56, 520, 20);
            Controls.Add(_status);

            _always.Location = new Point(20, 396);
            _pin.Location = new Point(230, 396);
            _close.Location = new Point(460, 396);
            _close.Size = new Size(80, 40);

            _always.Click += delegate
            {
                Result = "";
                DialogResult = DialogResult.OK;
                Close();
            };

            _pin.Click += delegate
            {
                var release = _list.SelectedItem as ModUpdater.Release;
                if (release == null) return;

                Result = release.Version;
                DialogResult = DialogResult.OK;
                Close();
            };

            _close.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(_always);
            Controls.Add(_pin);
            Controls.Add(_close);

            Load += delegate { Fetch(); };
        }

        private async void Fetch()
        {
            try
            {
                var list = await ModUpdater.ListAsync(CancellationToken.None);
                _releases = list ?? new List<ModUpdater.Release>();

                var objects = new List<object>();
                foreach (var release in _releases) objects.Add(release);

                _list.SetItems(objects);

                for (int i = 0; i < _releases.Count; i++)
                    if (string.Equals(_releases[i].Version, _current, StringComparison.OrdinalIgnoreCase))
                        _list.SelectedIndex = i;

                if (_list.SelectedIndex < 0 && _releases.Count > 0) _list.SelectedIndex = 0;

                _status.Text = _releases.Count == 0
                    ? "GitHub не ответил — попробуйте позже."
                    : (_current.Length == 0
                        ? "Сейчас: всегда последняя. Выпусков в списке: " + _releases.Count
                        : "Сейчас закреплена " + _current + ". Выпусков в списке: " + _releases.Count);
            }
            catch (Exception ex)
            {
                _status.Text = "Список версий не пришёл: " + ex.Message;
            }
        }

        private void Draw(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var release = item as ModUpdater.Release;
            if (release == null) return;

            var box = Rectangle.Inflate(r, -4, -3);
            if (box.Width <= 0 || box.Height <= 0) return;

            if (selected) Theme.FillRound(g, box, 10, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 10, Color.FromArgb(16, 255, 255, 255));
            if (selected) Theme.DrawRound(g, box, 10, Color.FromArgb(120, Theme.Accent));

            bool newest = index == 0;
            bool pinned = string.Equals(release.Version, _current, StringComparison.OrdinalIgnoreCase);

            Theme.Text1(g, release.Version, Theme.F(11f, FontStyle.Bold), Theme.Text,
                new Rectangle(box.X + 14, box.Y + 7, 160, 20));

            string tag = pinned ? "закреплена" : (newest ? "последняя" : "");
            if (tag.Length > 0)
                Theme.Text1(g, tag, Theme.F(8.4f, FontStyle.Bold),
                    pinned ? Theme.Accent : Theme.Good,
                    new Rectangle(box.X + 100, box.Y + 9, 140, 18));

            string notes = string.IsNullOrEmpty(release.Notes) ? release.File : release.Notes;
            Theme.Text1(g, notes, Theme.F(8.8f), Theme.Muted,
                new Rectangle(box.X + 14, box.Y + 29, Math.Max(60, box.Width - 110), 20));

            if (release.Size > 0)
                Theme.Text1(g, (release.Size / 1024.0 / 1024.0).ToString("0.#") + " МБ",
                    Theme.F(8.4f), Theme.Dim,
                    new Rectangle(box.Right - 92, box.Y, 80, box.Height),
                    StringAlignment.Far, StringAlignment.Center);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.Smooth(g);

            Theme.Text1(g, "Какую версию визуалов держать", Theme.F(13f, FontStyle.Bold), Theme.Text,
                new Rectangle(22, 18, 520, 26));
        }
    }
}
