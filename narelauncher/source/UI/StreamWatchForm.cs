using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Просмотр эфира прямо в приложении по ID (как встраивание на сайте).
    /// Нативный плеер: тянет кадры с /stream/frame и показывает их; справа —
    /// живой чат (/stream/chat/get + /stream/chat/send) и счётчик зрителей.
    /// Для RTMP/HLS-эфиров кадры недоступны — предлагаем открыть видео в браузере.
    /// </summary>
    public class StreamWatchForm : Form
    {
        private readonly TextBox _id = new TextBox();
        private readonly Button _watch = new Button();
        private readonly Button _browser = new Button();
        private readonly PictureBox _video = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
        private readonly Label _status = new Label();
        private readonly Label _title = new Label();

        private readonly Panel _chatWrap = new Panel();
        private readonly Label _viewers = new Label();
        private readonly RichTextBox _chat = new RichTextBox();
        private readonly TextBox _msg = new TextBox();
        private readonly Button _send = new Button();

        private readonly Timer _frameTick = new Timer { Interval = 700 };
        private readonly Timer _chatTick = new Timer { Interval = 1500 };

        private string _cur = "";     // текущий ID эфира
        private int _chatSince = 0;
        private string _hls = "";
        private bool _busyFrame, _busyChat;

        private static readonly Color Bg = Color.FromArgb(14, 17, 24);
        private static readonly Color Panel = Color.FromArgb(21, 27, 38);
        private static readonly Color Blue = Color.FromArgb(0, 119, 255);   // VK blue
        private static readonly Color Txt = Color.FromArgb(232, 238, 248);
        private static readonly Color Dim = Color.FromArgb(140, 150, 170);

        public StreamWatchForm(string startId)
        {
            Text = "Смотреть эфир";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1040, 620);
            MinimumSize = new Size(760, 460);
            BackColor = Bg;
            Font = Theme.F(10f);

            // --- верхняя панель: поле ID + кнопки ---
            var bar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Panel };
            var lbl = new Label { Text = "ID эфира:", ForeColor = Dim, AutoSize = true, Location = new Point(16, 18), BackColor = Color.Transparent };
            bar.Controls.Add(lbl);

            _id.BorderStyle = BorderStyle.FixedSingle;
            _id.BackColor = Color.FromArgb(10, 13, 19);
            _id.ForeColor = Txt;
            _id.Font = Theme.F(11f);
            _id.SetBounds(96, 13, 240, 26);
            _id.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Open(_id.Text.Trim()); } };
            bar.Controls.Add(_id);

            Style(_watch, Blue, Color.White); _watch.Text = "▶ Смотреть"; _watch.SetBounds(346, 11, 120, 30);
            _watch.Click += (s, e) => Open(_id.Text.Trim());
            bar.Controls.Add(_watch);

            Style(_browser, Color.FromArgb(40, 47, 62), Txt); _browser.Text = "↗ В браузере"; _browser.SetBounds(474, 11, 130, 30);
            _browser.Click += (s, e) => OpenBrowser();
            bar.Controls.Add(_browser);

            Controls.Add(bar);

            // --- чат справа ---
            _chatWrap.Dock = DockStyle.Right;
            _chatWrap.Width = 300;
            _chatWrap.BackColor = Panel;
            _chatWrap.Padding = new Padding(10);

            _viewers.Dock = DockStyle.Top;
            _viewers.Height = 30;
            _viewers.ForeColor = Blue;
            _viewers.Font = Theme.F(10.5f, FontStyle.Bold);
            _viewers.Text = "Чат";
            _viewers.TextAlign = ContentAlignment.MiddleLeft;
            _viewers.BackColor = Color.Transparent;

            _chat.Dock = DockStyle.Fill;
            _chat.ReadOnly = true;
            _chat.BorderStyle = BorderStyle.None;
            _chat.BackColor = Color.FromArgb(17, 21, 30);
            _chat.ForeColor = Txt;
            _chat.Font = Theme.F(9.5f);
            _chat.ScrollBars = RichTextBoxScrollBars.Vertical;

            var sendRow = new Panel { Dock = DockStyle.Bottom, Height = 36, BackColor = Color.Transparent };
            _msg.BorderStyle = BorderStyle.FixedSingle;
            _msg.BackColor = Color.FromArgb(10, 13, 19);
            _msg.ForeColor = Txt;
            _msg.Dock = DockStyle.Fill;
            _msg.Font = Theme.F(10f);
            _msg.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SendMsg(); } };
            Style(_send, Blue, Color.White); _send.Text = "➤"; _send.Dock = DockStyle.Right; _send.Width = 44;
            _send.Click += (s, e) => SendMsg();
            sendRow.Controls.Add(_msg);
            sendRow.Controls.Add(_send);

            _chatWrap.Controls.Add(_chat);
            _chatWrap.Controls.Add(sendRow);
            _chatWrap.Controls.Add(_viewers);
            Controls.Add(_chatWrap);

            // --- видео + статус (центр) ---
            var center = new Panel { Dock = DockStyle.Fill, BackColor = Bg, Padding = new Padding(10) };
            _title.Dock = DockStyle.Top;
            _title.Height = 26;
            _title.ForeColor = Txt;
            _title.Font = Theme.F(12f, FontStyle.Bold);
            _title.BackColor = Color.Transparent;
            _title.Text = "";

            _status.Dock = DockStyle.Bottom;
            _status.Height = 24;
            _status.ForeColor = Dim;
            _status.BackColor = Color.Transparent;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.Text = "Введите ID эфира и нажмите «Смотреть».";

            _video.Dock = DockStyle.Fill;

            center.Controls.Add(_video);
            center.Controls.Add(_status);
            center.Controls.Add(_title);
            Controls.Add(center);
            _chatWrap.BringToFront();

            _frameTick.Tick += (s, e) => PollFrame();
            _chatTick.Tick += (s, e) => PollChat();

            if (!string.IsNullOrEmpty(startId)) { _id.Text = startId; Open(startId); }
        }

        private static void Style(Button b, Color bg, Color fg)
        {
            b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0;
            b.BackColor = bg; b.ForeColor = fg; b.Font = Theme.F(10f, FontStyle.Bold);
            b.Cursor = Cursors.Hand; b.UseVisualStyleBackColor = false;
        }

        private string Token { get { var a = AccountStore.Selected; return a == null ? "" : (a.Token ?? ""); } }

        private void Open(string id)
        {
            id = (id ?? "").Trim();
            if (id.Length == 0) { _status.Text = "Укажите ID эфира."; return; }
            if (Token.Length == 0) { _status.Text = "Войдите в аккаунт, чтобы смотреть эфиры."; return; }
            _cur = id; _chatSince = 0; _hls = "";
            _chat.Clear();
            _title.Text = "Эфир " + id;
            _status.Text = "Подключение…";
            _frameTick.Start();
            _chatTick.Start();
            PollFrame();
            PollChat();
        }

        private void OpenBrowser()
        {
            if (_cur.Length == 0) { _status.Text = "Сначала укажите ID эфира."; return; }
            try { System.Diagnostics.Process.Start("https://narelauncher.winnigames2024.ru/live/watch.php?s=" + Uri.EscapeDataString(_cur)); }
            catch { }
        }

        private void PollFrame()
        {
            if (_busyFrame || _cur.Length == 0) return;
            _busyFrame = true;
            string id = _cur;
            new System.Threading.Thread(() =>
            {
                string r = Post("/stream/frame", "{\"token\":\"" + Esc(Token) + "\",\"id\":\"" + Esc(id) + "\"}");
                Ui(() =>
                {
                    _busyFrame = false;
                    if (id != _cur || r == null) return;
                    var j = Json.Parse(r);
                    if (j["needLogin"].AsBool(false)) { _status.Text = "Войдите в аккаунт, чтобы смотреть."; Stop(); return; }
                    if (!j["found"].AsBool(false)) { _status.Text = "Эфир с таким ID не найден."; _title.Text = "СТРИМ НЕ НАЙДЕН"; return; }
                    bool online = j["online"].AsBool(false);
                    string ti = j["title"].AsString(""); string user = j["user"].AsString(j["nick"].AsString(""));
                    _title.Text = (ti.Length > 0 ? ti : "Эфир") + (user.Length > 0 ? "  ·  " + user : "");
                    int vc = j["viewers"].AsInt(0);
                    _viewers.Text = "Чат · 👁 " + vc;
                    _hls = j["hls"].AsString("");
                    string jpg = j["jpg"].AsString("");
                    if (jpg.Length > 0)
                    {
                        try
                        {
                            var bytes = Convert.FromBase64String(jpg);
                            using (var ms = new MemoryStream(bytes))
                            {
                                var img = Image.FromStream(ms);
                                var old = _video.Image; _video.Image = img; if (old != null) old.Dispose();
                            }
                        }
                        catch { }
                    }
                    if (online)
                        _status.Text = _hls.Length > 0
                            ? "● В ЭФИРЕ (RTMP/HLS). Для видео со звуком жми «В браузере»."
                            : "● В ЭФИРЕ · " + vc + " смотрят";
                    else
                        _status.Text = j["lost"].AsBool(false) ? "Эфир завершён." : "Ожидание эфира…";
                });
            }) { IsBackground = true }.Start();
        }

        private void PollChat()
        {
            if (_busyChat || _cur.Length == 0) return;
            _busyChat = true;
            string id = _cur; int since = _chatSince;
            new System.Threading.Thread(() =>
            {
                string r = Post("/stream/chat/get", "{\"id\":\"" + Esc(id) + "\",\"since\":" + since + "}");
                Ui(() =>
                {
                    _busyChat = false;
                    if (id != _cur || r == null) return;
                    var j = Json.Parse(r);
                    foreach (var m in j["msgs"].Items)
                    {
                        string from = m["from"].AsString("");
                        string text = m["text"].AsString("");
                        bool bot = m["bot"].AsBool(false);
                        AppendChat(from, text, bot);
                    }
                    _chatSince = j["total"].AsInt(_chatSince);
                });
            }) { IsBackground = true }.Start();
        }

        private void AppendChat(string from, string text, bool bot)
        {
            _chat.SelectionStart = _chat.TextLength;
            _chat.SelectionColor = bot ? Color.FromArgb(120, 200, 120) : Blue;
            _chat.SelectionFont = new Font(_chat.Font, FontStyle.Bold);
            _chat.AppendText(from + ": ");
            _chat.SelectionColor = Txt;
            _chat.SelectionFont = new Font(_chat.Font, FontStyle.Regular);
            _chat.AppendText(text + "\n");
            _chat.SelectionStart = _chat.TextLength;
            _chat.ScrollToCaret();
        }

        private void SendMsg()
        {
            string t = _msg.Text.Trim();
            if (t.Length == 0 || _cur.Length == 0) return;
            if (Token.Length == 0) { _status.Text = "Войдите, чтобы писать в чат."; return; }
            _msg.Clear();
            string id = _cur;
            new System.Threading.Thread(() =>
            {
                Post("/stream/chat/send", "{\"token\":\"" + Esc(Token) + "\",\"id\":\"" + Esc(id) + "\",\"text\":\"" + Esc(t) + "\"}");
                Ui(() => PollChat());
            }) { IsBackground = true }.Start();
        }

        private void Stop() { _frameTick.Stop(); _chatTick.Stop(); }

        protected override void OnFormClosing(FormClosingEventArgs e) { Stop(); base.OnFormClosing(e); }

        private void Ui(Action a) { if (IsDisposed) return; try { if (InvokeRequired) BeginInvoke(a); else a(); } catch { } }

        private static string Post(string route, string body)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("http://" + NareNet.Where + route);
                req.Method = "POST"; req.ContentType = "application/json"; req.Timeout = 15000;
                var data = Encoding.UTF8.GetBytes(body);
                req.ContentLength = data.Length;
                using (var s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) return rd.ReadToEnd();
            }
            catch (WebException we) { try { using (var rd = new StreamReader(we.Response.GetResponseStream(), Encoding.UTF8)) return rd.ReadToEnd(); } catch { return null; } }
            catch { return null; }
        }

        private static string Esc(string s)
        {
            s = s ?? ""; var b = new StringBuilder(s.Length + 8);
            foreach (char c in s) { if (c == '\\') b.Append("\\\\"); else if (c == '"') b.Append("\\\""); else if (c == '\n') b.Append("\\n"); else if (c == '\r') b.Append("\\r"); else if (c < 0x20) b.Append("\\u").Append(((int)c).ToString("x4")); else b.Append(c); }
            return b.ToString();
        }
    }
}
