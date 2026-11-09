using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Профиль игрока (в духе соцсети): статус, статистика, галерея скриншотов.
    /// Сверху — поиск игроков, можно открыть чужой профиль.
    /// </summary>
    public class ProfilePage : PageBase
    {
        private readonly NareTextBox _search = new NareTextBox { Placeholder = "Поиск игроков по нику…" };
        private readonly VirtualList _results = new VirtualList { ItemHeight = 34, EmptyText = "" };
        private readonly NareButton _mine = new NareButton { Text = "Мой профиль", Style = BtnStyle.Ghost, Size = new Size(150, 34) };

        private readonly NareTextBox _status = new NareTextBox { Placeholder = "Ваш статус — что вы строите, зовёте играть…" };
        private readonly NareButton _saveStatus = new NareButton { Text = "Сохранить статус", Style = BtnStyle.Accent, Size = new Size(190, 36) };
        private readonly NareButton _addShot = new NareButton { Text = "Выложить скрин", Style = BtnStyle.Success, Size = new Size(180, 36) };
        private readonly NareLabel _info = new NareLabel("", 9.5f, FontStyle.Regular, Theme.Muted);

        private readonly System.Windows.Forms.Timer _searchTimer = new System.Windows.Forms.Timer { Interval = 350 };

        private string _viewNick = "";
        private NareAuth.Profile _profile;
        private readonly Dictionary<string, Image> _images = new Dictionary<string, Image>();
        private readonly List<Rectangle> _shotRects = new List<Rectangle>();

        public ProfilePage()
        {
            Controls.Add(_search); Controls.Add(_results); Controls.Add(_mine);
            Controls.Add(_status); Controls.Add(_saveStatus); Controls.Add(_addShot);
            Controls.Add(_info);

            _results.Renderer = DrawResult;
            _results.Visible = false;
            _results.ItemActivated += async (s, e) => await OpenResult();
            _results.SelectionChanged += async (s, e) => await OpenResult();

            _search.Inner.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };
            _searchTimer.Tick += async (s, e) => { _searchTimer.Stop(); await RunSearch(); };

            _mine.Click += async (s, e) => { _search.Text = ""; _results.Visible = false; await Open(MyNick); };
            _saveStatus.Click += async (s, e) => await SaveStatus();
            _addShot.Click += async (s, e) => await AddShot();
            MouseClick += OnShotClick;
        }

        private string Token { get { var a = AccountStore.Selected; return a == null ? "" : a.Token; } }
        private string MyNick { get { var a = AccountStore.Selected; return a == null ? "" : a.Name; } }
        private bool IsMine { get { return string.Equals(_viewNick, MyNick, StringComparison.OrdinalIgnoreCase); } }

        public override void PageShown()
        {
            var acc = AccountStore.Selected;
            if (acc != null && string.IsNullOrEmpty(acc.Token)) Host.EnsureAuth();
            Layout1();
            _ = Open(MyNick);
        }

        private async Task RunSearch()
        {
            string q = _search.Text.Trim();
            if (q.Length == 0) { _results.Visible = false; return; }
            try
            {
                var hits = await NareAuth.SearchAsync(q, CancellationToken.None);
                var items = new List<object>();
                foreach (var h in hits) items.Add(h);
                _results.SetItems(items);
                _results.Visible = items.Count > 0;
                _results.BringToFront();
            }
            catch { }
        }

        private async Task OpenResult()
        {
            var h = _results.SelectedItem as NareAuth.SearchHit;
            if (h == null) return;
            _results.Visible = false;
            _search.Text = "";
            await Open(h.Nick);
        }

        private async Task Open(string nick)
        {
            if (string.IsNullOrEmpty(nick)) return;
            _viewNick = nick;
            _info.Text = "Загружаю…";
            try
            {
                _profile = await NareAuth.ProfileGetAsync(nick, CancellationToken.None);
                _status.Text = _profile.Status;
                _status.Enabled = IsMine;
                _saveStatus.Visible = IsMine;
                _addShot.Visible = IsMine;
                _info.Text = "";
                LoadImages();
                Layout1();
                Invalidate();
            }
            catch (Exception ex) { _info.Text = "Не загрузилось: " + ex.Message; }
        }

        private async void LoadImages()
        {
            if (_profile == null) return;
            foreach (var id in _profile.Shots)
            {
                if (_images.ContainsKey(id)) continue;
                try
                {
                    var bytes = await Net.GetBytesAsync(NareAuth.ShotImageUrl(id), CancellationToken.None);
                    using (var ms = new MemoryStream(bytes))
                        _images[id] = Image.FromStream(ms);
                    Invalidate();
                }
                catch { }
            }
        }

        private async Task SaveStatus()
        {
            try
            {
                await NareAuth.ProfileStatusAsync(Token, _status.Text.Trim(), CancellationToken.None);
                _info.Text = "Статус сохранён.";
                _info.ForeColor = Theme.Good;
            }
            catch (Exception ex) { _info.Text = ex.Message; _info.ForeColor = Theme.Bad; }
        }

        private async Task AddShot()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "Картинки (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg";
                dlg.Title = "Выберите скриншот";
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

                try
                {
                    byte[] png = ToPng(dlg.FileName);
                    if (png.Length > 900 * 1024)
                    {
                        _info.Text = "Картинка больше ~1 МБ даже после сжатия — выберите поменьше.";
                        _info.ForeColor = Theme.Bad;
                        return;
                    }
                    string b64 = Convert.ToBase64String(png);
                    await NareAuth.ProfileShotAddAsync(Token, b64, CancellationToken.None);
                    _info.Text = "Скрин выложен!";
                    _info.ForeColor = Theme.Good;
                    await Open(MyNick);
                }
                catch (Exception ex) { _info.Text = "Не вышло: " + ex.Message; _info.ForeColor = Theme.Bad; }
            }
        }

        /// <summary>Перекодирует в PNG и ужимает слишком большие картинки по размеру.</summary>
        private static byte[] ToPng(string path)
        {
            using (var src = Image.FromFile(path))
            {
                int max = 1280;
                Image img = src;
                bool resized = false;
                if (src.Width > max || src.Height > max)
                {
                    double k = Math.Min((double)max / src.Width, (double)max / src.Height);
                    img = new Bitmap(src, new Size((int)(src.Width * k), (int)(src.Height * k)));
                    resized = true;
                }
                using (var ms = new MemoryStream())
                {
                    img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    if (resized) img.Dispose();
                    return ms.ToArray();
                }
            }
        }

        private void OnShotClick(object sender, MouseEventArgs e)
        {
            if (!IsMine || _profile == null) return;
            for (int i = 0; i < _shotRects.Count && i < _profile.Shots.Count; i++)
            {
                if (_shotRects[i].Contains(e.Location))
                {
                    string id = _profile.Shots[i];
                    if (MessageBox.Show(FindForm(), "Удалить этот скрин?", "Профиль",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        _ = DelShot(id);
                    return;
                }
            }
        }

        private async Task DelShot(string id)
        {
            try { await NareAuth.ProfileShotDelAsync(Token, id, CancellationToken.None); _images.Remove(id); await Open(MyNick); }
            catch (Exception ex) { _info.Text = ex.Message; }
        }

        private void DrawResult(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var h = item as NareAuth.SearchHit;
            if (h == null) return;
            var box = Rectangle.Inflate(r, -2, -1);
            if (selected || hover) Theme.FillRound(g, box, 7, Color.FromArgb(40, Theme.Accent));
            Theme.Text1(g, h.Nick, Theme.F(10f, FontStyle.Bold), Theme.Text,
                new Rectangle(box.X + 10, box.Y, box.Width - 120, box.Height), StringAlignment.Near, StringAlignment.Center);
            Theme.Text1(g, h.Coins + " монет", Theme.F(8.5f), Theme.Good,
                new Rectangle(box.Right - 120, box.Y, 112, box.Height), StringAlignment.Far, StringAlignment.Center);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Layout1(); }

        private void Layout1()
        {
            int pad = 28;
            _search.SetBounds(pad, 20, Math.Min(420, Width - pad * 2 - 170), 38);
            _mine.Location = new Point(_search.Right + 12, 21);
            _results.SetBounds(pad, 60, _search.Width, 180);

            int top = 132;
            _status.SetBounds(pad, top, Math.Min(520, Width - pad * 2 - 210), 38);
            _saveStatus.Location = new Point(_status.Right + 12, top + 1);
            _addShot.Location = new Point(pad, top + 52);
            _info.SetBounds(pad + 200, top + 58, Width - pad * 2 - 200, 20);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics; Theme.Smooth(g);

            if (_profile == null) return;

            string name = _profile.Deleted ? (_profile.Nick + " | Аккаунт Удалён") : _profile.Nick;
            string role = _profile.Admin ? "  · админ" : _profile.Moderator ? "  · модератор" : "";
            Theme.Text1(g, name + role, Theme.F(16f, FontStyle.Bold), Theme.Text, new Rectangle(28, 74, Width - 56, 26));

            string sub = _profile.Coins + " монет";
            if (!string.Equals(_profile.Username, _profile.Nick, StringComparison.Ordinal)) sub += "   ·   в игре: " + _profile.Username;
            if (_profile.Since > 0)
            {
                try { sub += "   ·   с " + DateTimeOffset.FromUnixTimeMilliseconds(_profile.Since).LocalDateTime.ToString("dd.MM.yyyy"); }
                catch { }
            }
            Theme.Text1(g, sub, Theme.F(9.5f), Theme.Muted, new Rectangle(28, 102, Width - 56, 18));

            if (!IsMine && !string.IsNullOrEmpty(_profile.Status))
                Theme.Text1(g, _profile.Status, Theme.F(11f), Theme.Text, new Rectangle(28, 132, Width - 56, 40));

            // галерея скриншотов
            int gy = IsMine ? 220 : 182;
            Theme.Text1(g, _profile.Shots.Count > 0 ? "Скриншоты" : (IsMine ? "Скриншотов пока нет — выложите первый!" : "Скриншотов нет"),
                Theme.F(11f, FontStyle.Bold), Theme.Muted, new Rectangle(28, gy, Width - 56, 20));

            _shotRects.Clear();
            int cw = 200, ch = 120, gap = 12, x = 28, y = gy + 28;
            int perRow = Math.Max(1, (Width - 56 + gap) / (cw + gap));
            for (int i = 0; i < _profile.Shots.Count; i++)
            {
                int col = i % perRow, row = i / perRow;
                var cell = new Rectangle(28 + col * (cw + gap), y + row * (ch + gap), cw, ch);
                _shotRects.Add(cell);
                Theme.FillRound(g, cell, 10, Theme.Panel);
                Image img;
                if (_images.TryGetValue(_profile.Shots[i], out img) && img != null)
                {
                    var fit = FitRect(img.Size, cell);
                    var clip = g.Clip; g.SetClip(Theme.Round(cell, 10));
                    g.DrawImage(img, fit);
                    g.Clip = clip;
                    if (IsMine)
                        Theme.Text1(g, "✕ удалить", Theme.F(8f, FontStyle.Bold), Color.White,
                            new Rectangle(cell.X + 8, cell.Bottom - 20, cell.Width - 16, 14), StringAlignment.Far, StringAlignment.Center);
                }
                else
                    Theme.Text1(g, "загрузка…", Theme.F(8.5f), Theme.Dim, cell, StringAlignment.Center, StringAlignment.Center);
            }
        }

        private static Rectangle FitRect(Size img, Rectangle box)
        {
            double k = Math.Max((double)box.Width / img.Width, (double)box.Height / img.Height);
            int w = (int)(img.Width * k), h = (int)(img.Height * k);
            return new Rectangle(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
        }
    }
}
