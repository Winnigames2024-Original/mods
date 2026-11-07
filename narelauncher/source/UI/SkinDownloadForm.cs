using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Загрузка скина игрока с сайта: по нику или по прямой ссылке на PNG.
    /// Перед тем как забрать скин, его видно на модели.
    /// </summary>
    public class SkinDownloadForm : Form
    {
        private const int TitleHeight = 44;

        private readonly NareTextBox _query = new NareTextBox
        {
            Placeholder = "Ник игрока или ссылка на PNG",
            Icon = Ico.Account
        };

        private readonly NareButton _find = new NareButton
        { Text = "Найти", Style = BtnStyle.Accent, Icon = Ico.Search, Size = new Size(132, 38) };

        private readonly NareButton _take = new NareButton
        { Text = "Взять этот скин", Style = BtnStyle.Success, Icon = Ico.Check, Size = new Size(190, 38) };

        private readonly NareButton _cancel = new NareButton
        { Text = "Отмена", Style = BtnStyle.Ghost, Size = new Size(120, 38) };

        private readonly NareButton _closeX = new NareButton
        { Text = "", Style = BtnStyle.Ghost, Icon = Ico.Close, Size = new Size(40, 28), Radius = 8 };

        private readonly NareButton[] _sources;
        private readonly SkinView3D _preview = new SkinView3D { Spin = true };
        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);

        private SkinDownloader.Source _source = SkinDownloader.Source.Auto;
        private CancellationTokenSource _cts;
        private bool _busy;

        /// <summary>Что забрали. Заполняется, когда окно закрылось с OK.</summary>
        public SkinFile Result;

        public SkinDownloadForm(string suggestedNick)
        {
            Text = "Скин с сайта";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(620, 562);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(9.5f);
            DoubleBuffered = true;
            KeyPreview = true;
            ShowInTaskbar = false;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            _sources = new NareButton[SkinDownloader.All.Length];
            for (int i = 0; i < SkinDownloader.All.Length; i++)
            {
                SkinDownloader.Source one = SkinDownloader.All[i];
                var b = new NareButton
                {
                    Text = SkinDownloader.Title(one),
                    Style = one == _source ? BtnStyle.Accent : BtnStyle.Ghost,
                    Size = new Size(130, 32),
                    Radius = 8,
                    Font = Theme.F(9f, FontStyle.Bold)
                };
                b.Click += (s, e) => { _source = one; UpdateSources(); };
                _sources[i] = b;
                Controls.Add(b);
            }

            _find.Click += (s, e) => Find();
            _take.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            _cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            _closeX.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            _query.Inner.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                Find();
            };

            if (!string.IsNullOrEmpty(suggestedNick)) _query.Text = suggestedNick;

            _take.Enabled = false;

            Controls.Add(_query);
            Controls.Add(_find);
            Controls.Add(_preview);
            Controls.Add(_take);
            Controls.Add(_cancel);
            Controls.Add(_closeX);
            Controls.Add(_status);

            Say("Ник берётся с Mojang вместе с типом модели. Ссылку на PNG тоже можно вставить.", Theme.Dim);
            Layout1();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _query.Inner.Focus();
            _query.Inner.SelectAll();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_cts != null) { _cts.Cancel(); _cts.Dispose(); _cts = null; }
            base.OnFormClosed(e);
        }

        // ---------------------------------------------------------------- загрузка

        private async void Find()
        {
            if (_busy) return;

            string query = _query.Text;
            _busy = true;
            _find.Busy = true;
            _find.Enabled = false;
            _take.Enabled = false;
            Result = null;
            _preview.Doc = null;
            _preview.Invalidate();
            Say("Ищем «" + query.Trim() + "»…", Theme.Muted);

            if (_cts != null) { _cts.Cancel(); _cts.Dispose(); }
            _cts = new CancellationTokenSource();

            try
            {
                var progress = new Progress<string>(line => Say(line, Theme.Muted));
                var found = await SkinDownloader.FetchAsync(query, _source, _cts.Token, progress);

                Result = found.Skin;
                _preview.Doc = SkinDocument.FromSkin(found.Skin);
                _preview.Invalidate();
                _take.Enabled = true;

                string note = SkinDownloader.Title(found.From);
                if (!string.IsNullOrEmpty(found.Note)) note = found.Note;

                Say("Нашли: " + found.Skin.Name + " · " +
                    (found.Skin.Arms == SkinArms.Slim ? "тонкие руки" : "обычные руки") +
                    " · " + note, Theme.Good);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Say(ex.Message, Theme.Bad);
            }
            finally
            {
                _busy = false;
                _find.Busy = false;
                _find.Enabled = true;
                _find.Invalidate();
                _take.Invalidate();
            }
        }

        private void Say(string text, Color color)
        {
            _status.Text = text;
            _status.ForeColor = color;
        }

        private void UpdateSources()
        {
            for (int i = 0; i < _sources.Length; i++)
            {
                _sources[i].Style = SkinDownloader.All[i] == _source ? BtnStyle.Accent : BtnStyle.Ghost;
                _sources[i].Invalidate();
            }
            Invalidate();
        }

        // ---------------------------------------------------------------- вёрстка

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Layout1();
        }

        private void Layout1()
        {
            // ClientSize в конструкторе дёргает OnResize раньше, чем создан ряд источников
            if (_sources == null) return;

            int pad = 24;
            int w = ClientSize.Width;
            int h = ClientSize.Height;

            _closeX.Location = new Point(w - _closeX.Width - 10, (TitleHeight - _closeX.Height) / 2);

            int y = TitleHeight + 46;
            _query.SetBounds(pad, y, Math.Max(140, w - pad * 2 - _find.Width - 10), 38);
            _find.Location = new Point(_query.Right + 10, y);

            y += 38 + 32;
            int sx = pad;
            foreach (var b in _sources)
            {
                b.Location = new Point(sx, y);
                sx += b.Width + 8;
            }

            y += 32 + 22;
            int previewH = Math.Max(120, h - y - 76);
            _preview.SetBounds(pad, y, 200, previewH);

            _take.Location = new Point(w - pad - _take.Width, h - 58);
            _cancel.Location = new Point(_take.Left - _cancel.Width - 10, h - 58);

            _status.SetBounds(pad, h - 24, Math.Max(100, w - pad * 2), 18);

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_sources == null) return;

            var g = e.Graphics;
            Theme.Smooth(g);

            var bar = new Rectangle(0, 0, Width, TitleHeight);
            using (var b = new SolidBrush(Theme.Sidebar))
                g.FillRectangle(b, bar);
            using (var pen = new Pen(Color.FromArgb(28, 255, 255, 255)))
                g.DrawLine(pen, 0, TitleHeight - 1, Width, TitleHeight - 1);

            var logo = new Rectangle(18, (TitleHeight - 20) / 2, 20, 20);
            Theme.FillRoundGradient(g, logo, 6, Theme.Accent, Theme.Accent2, 35f);
            Theme.Text1(g, "N", Theme.F(10f, FontStyle.Bold), Color.White, logo,
                StringAlignment.Center, StringAlignment.Center);

            Theme.Text1(g, "Скин игрока с сайта", Theme.F(11f, FontStyle.Bold), Theme.Text,
                new Rectangle(48, 0, 360, TitleHeight));

            Theme.Text1(g, "Скин загрузится в редактор как отдельный слой-основа — его можно дорисовать",
                Theme.F(9f), Theme.Dim, new Rectangle(24, TitleHeight + 10, Width - 72, 20));

            Theme.Text1(g, "ИСТОЧНИК", Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                new Rectangle(24, _sources[0].Top - 20, 200, 14));

            // пояснение справа от модели
            int tx = _preview.Right + 22;
            int tw = Math.Max(60, Width - tx - 24);

            Theme.Text1(g, "КАК ЭТО РАБОТАЕТ", Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                new Rectangle(tx, _preview.Top, tw, 14));

            string[] lines =
            {
                "Авто — сначала Mojang, потом зеркала.",
                "Mojang — официальный профиль: оттуда",
                "берётся и тип модели (обычные или тонкие руки).",
                "",
                "Minotar и MC-Heads — зеркала. Они быстрее,",
                "но отвечают даже на несуществующий ник,",
                "подставляя стандартный скин.",
                "",
                "Ссылка на PNG подойдёт любая прямая —",
                "64×64 или старый формат 64×32.",
                "",
                "На источник даётся " + SkinDownloader.AttemptSeconds + " с, потом",
                "берётся следующий."
            };

            int ly = _preview.Top + 22;
            foreach (string line in lines)
            {
                Theme.Text1(g, line, Theme.F(9f), line.Length == 0 ? Theme.Dim : Theme.Muted,
                    new Rectangle(tx, ly, tw, 18));
                ly += 18;
            }

            using (var pen = new Pen(Theme.Border))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && e.Y < TitleHeight)
            {
                NativeMethods.ReleaseCapture();
                NativeMethods.SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
        }
    }
}
