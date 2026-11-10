using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// «Видео» — NareVideo, система как YouTube: лента роликов, поиск, загрузка,
    /// переход в свой канал (NareProfileForm). Просмотр ролика открывается в
    /// браузере (там HTML5-плеер со стримингом).
    /// </summary>
    public class VideosPage : PageBase
    {
        internal static readonly Color CBg = Color.FromArgb(15, 15, 15);
        internal static readonly Color CCard = Color.FromArgb(28, 28, 28);
        internal static readonly Color CText = Color.FromArgb(241, 241, 241);
        internal static readonly Color CDim = Color.FromArgb(170, 170, 170);
        internal static readonly Color CRed = Color.FromArgb(255, 0, 51);

        private readonly TextBox _search = new TextBox();
        private readonly Button _upload = new Button();
        private readonly Button _mine = new Button();
        private readonly Button _refresh = new Button();
        private readonly Label _status = new Label();
        private readonly FlowLayoutPanel _feed = new FlowLayoutPanel();

        public VideosPage()
        {
            BackColor = CBg;

            var logo = new Label
            {
                Text = "► NareVideo",
                Font = Theme.F(16f, FontStyle.Bold),
                ForeColor = CRed,
                BackColor = Color.Transparent,
                AutoSize = false,
                Bounds = new Rectangle(24, 16, 220, 30)
            };
            Controls.Add(logo);

            _search.BorderStyle = BorderStyle.FixedSingle;
            _search.BackColor = Color.FromArgb(18, 18, 18);
            _search.ForeColor = CText;
            _search.Font = Theme.F(10.5f);
            _search.Bounds = new Rectangle(250, 18, 320, 26);
            _search.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Load(_search.Text.Trim(), null); } };
            Controls.Add(_search);

            _upload.Text = "↑ Загрузить"; VideosPage.StyleBtn(_upload, CRed, Color.White);
            _upload.Bounds = new Rectangle(584, 16, 130, 30);
            _upload.Click += (s, e) => DoUpload();
            Controls.Add(_upload);

            _mine.Text = "Мой канал"; VideosPage.StyleBtn(_mine, CCard, CText);
            _mine.Bounds = new Rectangle(724, 16, 120, 30);
            _mine.Click += (s, e) => OpenChannel(Nick, true);
            Controls.Add(_mine);

            _refresh.Text = "⟳"; VideosPage.StyleBtn(_refresh, CCard, CText);
            _refresh.Bounds = new Rectangle(852, 16, 44, 30);
            _refresh.Click += (s, e) => Load(null, null);
            Controls.Add(_refresh);

            _status.AutoSize = false; _status.BackColor = Color.Transparent; _status.ForeColor = CDim;
            _status.Font = Theme.F(9.5f); _status.Bounds = new Rectangle(24, 52, 860, 20);
            Controls.Add(_status);

            _feed.AutoScroll = true;
            _feed.WrapContents = true;
            _feed.BackColor = CBg;
            _feed.Padding = new Padding(16, 6, 16, 16);
            _feed.Bounds = new Rectangle(8, 76, 900, 460);
            Controls.Add(_feed);

            Resize += (s, e) => Relayout();
        }

        public override void PageShown() { Relayout(); if (_feed.Controls.Count == 0) Load(null, null); }

        private void Relayout()
        {
            int w = Math.Max(640, Width);
            _refresh.Left = w - 68; _mine.Left = _refresh.Left - 128; _upload.Left = _mine.Left - 138;
            _search.Width = Math.Max(180, _upload.Left - 250 - 12);
            _status.Width = w - 48;
            _feed.Bounds = new Rectangle(8, 76, w - 16, Math.Max(200, Height - 84));
        }

        private string Nick
        {
            get { var a = AccountStore.Selected; return (a == null ? "" : a.Name).ToLowerInvariant(); }
        }
        private string Token
        {
            get { var a = AccountStore.Selected; return a == null ? "" : (a.Token ?? ""); }
        }

        private void Load(string q, string channel)
        {
            _status.Text = "Загрузка…";
            _feed.Controls.Clear();
            new System.Threading.Thread(() =>
            {
                List<NareVideo> list;
                try { list = NareVideo.List(channel, q); }
                catch { list = new List<NareVideo>(); }
                Ui(() =>
                {
                    _feed.Controls.Clear();
                    if (list.Count == 0) { _status.Text = "Пока нет видео. Загрузи первое — кнопка «Загрузить»."; return; }
                    _status.Text = list.Count + " видео";
                    foreach (var v in list) _feed.Controls.Add(BuildCard(v, OpenChannel));
                });
            })
            { IsBackground = true }.Start();
        }

        private void DoUpload()
        {
            if (Token.Length == 0) { MessageBox.Show("Сначала войди в аккаунт."); return; }
            string path;
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Видео (*.mp4;*.webm;*.ogg)|*.mp4;*.webm;*.ogg|Все файлы|*.*";
                ofd.Title = "Выбери видео для загрузки";
                if (ofd.ShowDialog(FindForm()) != DialogResult.OK) return;
                path = ofd.FileName;
            }
            string title = InputPrompt.Ask(FindForm(), "Название видео:", Path.GetFileNameWithoutExtension(path));
            if (string.IsNullOrEmpty(title)) return;
            string desc = InputPrompt.Ask(FindForm(), "Описание (можно пусто):", "") ?? "";

            _status.Text = "Загрузка видео… 0%";
            _upload.Enabled = false;
            string tok = Token;
            new System.Threading.Thread(() =>
            {
                try
                {
                    string id = NareVideo.Upload(tok, path, title, desc, pct => Ui(() => _status.Text = "Загрузка видео… " + pct + "%"));
                    Ui(() =>
                    {
                        _status.Text = "Готово! Видео опубликовано.";
                        _upload.Enabled = true;
                        Load(null, null);
                        try { Process.Start(NareVideo.WatchUrl(id)); } catch { }
                    });
                }
                catch (Exception ex)
                {
                    Ui(() => { _status.Text = "Не загрузилось: " + ex.Message; _upload.Enabled = true; });
                }
            })
            { IsBackground = true }.Start();
        }

        private void OpenChannel(string nick, bool mine)
        {
            try { using (var f = new NareProfileForm(nick, mine ? Token : "")) f.ShowDialog(FindForm()); }
            catch (Exception ex) { MessageBox.Show("Не открылся канал: " + ex.Message); }
        }

        private void Ui(Action a) { try { if (IsHandleCreated) BeginInvoke(a); else a(); } catch { } }

        // ---------- общий конструктор карточки (используется и в профиле) ----------

        internal static Panel BuildCard(NareVideo v, Action<string, bool> openChannel)
        {
            var card = new Panel { Width = 264, Height = 234, BackColor = CBg, Margin = new Padding(8) };

            var thumb = new Panel { Bounds = new Rectangle(0, 0, 264, 148), BackColor = CCard };
            var pic = new PictureBox { Bounds = new Rectangle(0, 0, 264, 148), SizeMode = PictureBoxSizeMode.StretchImage, BackColor = CCard };
            var img = ThumbImage(v.Thumb);
            if (img != null) pic.Image = img;
            else { pic.SizeMode = PictureBoxSizeMode.CenterImage; pic.Image = PlaceholderThumb(); }
            thumb.Controls.Add(pic);
            if (v.Dur > 0)
            {
                var dur = new Label
                {
                    Text = FmtDur(v.Dur), AutoSize = false, Bounds = new Rectangle(264 - 46, 148 - 22, 40, 16),
                    BackColor = Color.FromArgb(0, 0, 0), ForeColor = Color.White, Font = Theme.F(8f, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                pic.Controls.Add(dur);
            }
            card.Controls.Add(thumb);

            var title = new Label
            {
                Text = v.Title, AutoSize = false, Bounds = new Rectangle(2, 156, 260, 38),
                ForeColor = CText, BackColor = Color.Transparent, Font = Theme.F(10f, FontStyle.Bold)
            };
            card.Controls.Add(title);
            var chan = new Label
            {
                Text = v.User, AutoSize = false, Bounds = new Rectangle(2, 196, 260, 16),
                ForeColor = CDim, BackColor = Color.Transparent, Font = Theme.F(9f)
            };
            card.Controls.Add(chan);
            var meta = new Label
            {
                Text = Nfmt(v.Views) + " просмотров · " + Ago(v.At), AutoSize = false, Bounds = new Rectangle(2, 212, 260, 16),
                ForeColor = CDim, BackColor = Color.Transparent, Font = Theme.F(8.5f)
            };
            card.Controls.Add(meta);

            string id = v.Id, who = v.Nick;
            EventHandler watch = (s, e) => { try { Process.Start(NareVideo.WatchUrl(id)); } catch { } };
            EventHandler chanClick = (s, e) => { if (openChannel != null) openChannel(who, false); };
            foreach (Control c in new Control[] { card, thumb, pic, title, meta })
            { c.Cursor = Cursors.Hand; c.Click += watch; }
            chan.Cursor = Cursors.Hand; chan.Click += chanClick;

            return card;
        }

        internal static Image ThumbImage(string dataUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(dataUrl)) return null;
                int comma = dataUrl.IndexOf(',');
                string b64 = comma >= 0 ? dataUrl.Substring(comma + 1) : dataUrl;
                var bytes = Convert.FromBase64String(b64);
                using (var ms = new MemoryStream(bytes)) return Image.FromStream(ms);
            }
            catch { return null; }
        }

        private static Image _ph;
        internal static Image PlaceholderThumb()
        {
            if (_ph != null) return _ph;
            var bmp = new Bitmap(48, 48);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(28, 28, 28));
                using (var b = new SolidBrush(Color.FromArgb(90, 90, 90)))
                    g.FillPolygon(b, new[] { new Point(18, 14), new Point(18, 34), new Point(34, 24) });
            }
            _ph = bmp;
            return _ph;
        }

        internal static void StyleBtn(Button b, Color bg, Color fg)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = bg; b.ForeColor = fg;
            b.Font = Theme.F(9.5f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
        }

        internal static string FmtDur(int s)
        {
            if (s <= 0) return "";
            int m = s / 60, x = s % 60;
            return m + ":" + (x < 10 ? "0" : "") + x;
        }
        internal static string Nfmt(int n)
        {
            if (n >= 1000000) return (n / 1000000.0).ToString("0.0") + " млн";
            if (n >= 1000) return (n / 1000.0).ToString("0.0") + " тыс";
            return n.ToString();
        }
        internal static string Ago(long t)
        {
            double d = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - t) / 1000.0;
            if (d < 60) return "только что";
            if (d < 3600) return (int)(d / 60) + " мин назад";
            if (d < 86400) return (int)(d / 3600) + " ч назад";
            if (d < 2592000) return (int)(d / 86400) + " дн назад";
            return (int)(d / 2592000) + " мес назад";
        }
    }
}
