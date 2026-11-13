using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Студия трансляции (как в OBS, но проще): захват всего экрана или
    /// отдельного окна/приложения и отправка кадров на NareNet. Эфир виден
    /// на сайте в разделе «Эфиры».
    /// </summary>
    public class StreamForm : Form
    {
        private readonly ComboBox _source = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _category = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _fps = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox _title = new TextBox();
        private readonly TextBox _sub = new TextBox();
        private readonly TextBox _sid = new TextBox();    // постоянная ссылка (свой ID)
        private readonly TextBox _desc = new TextBox();   // описание эфира
        private readonly Button _saveMeta = new Button { Text = "💾" };
        private readonly Button _refresh = new Button { Text = "⟳ Окна" };
        private readonly Button _go = new Button { Text = "Выйти в эфир" };
        private readonly Button _overlayBtn = new Button { Text = "Чат-оверлей" };
        private readonly PictureBox _preview = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
        private readonly Label _status = new Label { Text = "Готово", AutoSize = false };
        private readonly LinkLabel _link = new LinkLabel { Text = "", AutoSize = false };
        private ChatOverlay _overlay;

        private readonly System.Windows.Forms.Timer _tick = new System.Windows.Forms.Timer { Interval = 800 };
        private readonly System.Windows.Forms.Timer _srcTick = new System.Windows.Forms.Timer { Interval = 3000 };
        private readonly List<IntPtr> _windows = new List<IntPtr>();
        private readonly List<string> _titles = new List<string>();   // полные заголовки окон (для ffmpeg)
        private bool _live;
        private string _streamId, _streamLink;
        private int _viewerTick;
        private StreamEncoder _enc;      // настоящее видео через ffmpeg
        private bool _videoMode;         // true = идёт видео, false = запасной режим кадров
        // параметры для авто-переподключения, если эфир оборвался не по кнопке
        private string _rtmpUrl, _rcWinTitle;
        private bool _rcFullScreen;
        private int _rcBitrate = 3000, _rcFps = 60;
        private bool _reconnecting;
        private int _reconnectTicks, _reconnectTries;
        private const int ReconnectMaxTries = 20;   // ~1 минута попыток

        public StreamForm()
        {
            Text = "NareLauncher — Студия трансляции";
            ClientSize = new Size(760, 620);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(14, 17, 23);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);
            MinimumSize = new Size(620, 520);

            var lblSrc = Lbl("Источник", 16, 14);
            _source.SetBounds(16, 36, 420, 26); Style(_source);
            _refresh.SetBounds(446, 35, 90, 28); Btn(_refresh, false);
            _category.SetBounds(546, 36, 198, 26); Style(_category);
            _category.Items.AddRange(new object[] { "Игры", "Разговоры", "Музыка", "Творчество", "Другое" });
            _category.SelectedIndex = 0;

            var lblTitle = Lbl("Название эфира", 16, 72);
            _title.SetBounds(16, 94, 470, 26); Style(_title);
            var lblSub = Lbl("Подкатегория", 500, 72);
            _sub.SetBounds(500, 94, 150, 26); Style(_sub);
            var lblFps = Lbl("Кадры/с", 660, 72);
            _fps.SetBounds(660, 94, 84, 26); Style(_fps);
            _fps.Items.AddRange(new object[] { "60", "120", "240", "360" });
            _fps.SelectedIndex = 0;

            // постоянная ссылка (свой ID) + описание
            var lblSid = Lbl("Постоянная ссылка (ID)", 16, 126);
            _sid.SetBounds(16, 148, 360, 26); Style(_sid);
            var lblDesc = Lbl("Описание эфира", 390, 126);
            _desc.SetBounds(390, 148, 284, 26); Style(_desc);
            _saveMeta.SetBounds(680, 147, 64, 28); Btn(_saveMeta, false);
            _saveMeta.Click += (s, e) => SaveMeta();

            _preview.SetBounds(16, 190, 728, 286);
            _preview.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            _go.SetBounds(16, 488, 200, 40); Btn(_go, true);
            _go.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _status.SetBounds(228, 496, 300, 24); _status.ForeColor = Color.Silver;
            _status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _link.SetBounds(16, 532, 728, 22); _link.LinkColor = Color.FromArgb(120, 160, 255);
            _link.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _link.LinkClicked += (s, e) => { try { if (!string.IsNullOrEmpty(_streamLink)) System.Diagnostics.Process.Start(_streamLink); } catch { } };

            _overlayBtn.SetBounds(556, 488, 188, 40); Btn(_overlayBtn, false);
            _overlayBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _overlayBtn.Click += (s, e) => ToggleOverlay();

            Controls.AddRange(new Control[] { lblSrc, _source, _refresh, _category, lblTitle, _title, lblSub, _sub, lblFps, _fps,
                                              lblSid, _sid, lblDesc, _desc, _saveMeta, _preview, _go, _overlayBtn, _status, _link });
            LoadMeta();

            _refresh.Click += (s, e) => LoadSources();
            _go.Click += (s, e) => { if (_live) Stop(); else Start(); };
            _tick.Tick += (s, e) => Capture();

            // авто-обновление списка запущенных приложений/окон (когда не в эфире и список не раскрыт)
            _srcTick.Tick += (s, e) => { if (!_live && !_source.DroppedDown) LoadSources(); };
            _srcTick.Start();

            LoadSources();
            var acc = AccountStore.Selected;
            _title.Text = "Эфир " + (acc != null ? acc.PlayName : "");
        }

        private Label Lbl(string t, int x, int y) { return new Label { Text = t, ForeColor = Color.Silver, AutoSize = true, Location = new Point(x, y) }; }
        private void Style(Control c) { c.BackColor = Color.FromArgb(10, 13, 20); c.ForeColor = Color.White; if (c is TextBox) ((TextBox)c).BorderStyle = BorderStyle.FixedSingle; }
        private void Btn(Button b, bool accent)
        {
            b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = accent ? 0 : 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(40, 46, 58);
            b.BackColor = accent ? Color.FromArgb(91, 140, 255) : Color.FromArgb(22, 26, 34);
            b.ForeColor = Color.White; b.Cursor = Cursors.Hand;
        }

        // ------------------------------------------------------------ источники
        private void LoadSources()
        {
            string prev = _source.SelectedItem as string;
            _windows.Clear();
            _titles.Clear();
            _source.Items.Clear();
            _source.Items.Add("🖥 Весь экран");
            _windows.Add(IntPtr.Zero);                     // 0 = весь экран
            _titles.Add("");
            EnumWindows((h, l) =>
            {
                if (!IsWindowVisible(h) || IsIconic(h)) return true;
                int len = GetWindowTextLength(h);
                if (len == 0) return true;
                var sb = new StringBuilder(len + 1);
                GetWindowText(h, sb, sb.Capacity);
                string t = sb.ToString().Trim();
                if (t.Length == 0 || t == "NareLauncher — Студия трансляции") return true;
                RECT r; if (!GetWindowRect(h, out r)) return true;
                if (r.Right - r.Left < 80 || r.Bottom - r.Top < 60) return true;
                _windows.Add(h);
                _titles.Add(t);
                _source.Items.Add("🪟 " + (t.Length > 60 ? t.Substring(0, 60) : t));
                return true;
            }, IntPtr.Zero);
            int idx = prev != null ? _source.Items.IndexOf(prev) : 0;
            _source.SelectedIndex = idx >= 0 ? idx : 0;
        }

        // ------------------------------------------------------------ эфир
        private void Start()
        {
            var acc = AccountStore.Selected;
            if (acc == null || string.IsNullOrEmpty(acc.Token)) { _status.Text = "Сначала войдите в аккаунт."; return; }
            string title = (_title.Text ?? "").Trim(); if (title.Length == 0) title = "Эфир";
            string cat = CatSlug(_category.SelectedItem as string);
            string sub = (_sub.Text ?? "").Trim();
            string desc = (_desc.Text ?? "").Trim();
            string customKey = (_sid.Text ?? "").Trim();
            // берём личный ключ RTMP и сразу задаём название/описание/категорию/свой ID будущего эфира
            string resp = Post("/stream/key", "{\"token\":\"" + Esc(acc.Token) + "\",\"title\":\"" + Esc(title) + "\",\"category\":\"" + Esc(cat) +
                "\",\"subcategory\":\"" + Esc(sub) + "\",\"desc\":\"" + Esc(desc) + "\",\"customKey\":\"" + Esc(customKey) + "\"}");
            var j = resp != null ? Json.Parse(resp) : null;
            if (j == null || !j["ok"].AsBool(false)) { _status.Text = "Не удалось начать: " + (j != null ? j["error"].AsString("нет связи") : "нет связи"); return; }
            string ke = j["keyError"].AsString(""); if (ke.Length > 0) { _status.Text = "ID: " + ke; return; }
            string key = j["key"].AsString("");
            string server = j["server"].AsString("rtmp://151.245.217.104/live");
            string rtmpUrl = server + "/" + key;
            _streamId = key;
            _streamLink = "https://narelauncher.winnigames2024.ru" + j["watch"].AsString("/live/watch.php?s=" + key);

            int si = _source.SelectedIndex;
            bool fullScreen = !(si > 0 && si < _titles.Count);
            string winTitle = fullScreen ? "" : _titles[si];
            int fps; if (!int.TryParse(_fps.SelectedItem as string, out fps)) fps = 60;
            if (fps > 60) fps = 60;   // по RTMP держим до 60

            _enc = new StreamEncoder(SafeStatus);
            bool ok = false;
            try { ok = _enc.StartRtmp(rtmpUrl, fullScreen, winTitle, 3000, fps); } catch { ok = false; }
            if (!ok)
            {
                // ffmpeg ещё не скачан — качаем и просим повторить
                _enc = null;
                _status.Text = "Готовлю кодек видео… запусти эфир ещё раз через минуту.";
                StreamEncoder.EnsureFfmpegAsync(SafeStatus);
                return;
            }

            // запоминаем параметры для авто-переподключения
            _rtmpUrl = rtmpUrl; _rcFullScreen = fullScreen; _rcWinTitle = winTitle; _rcBitrate = 3000; _rcFps = fps;
            _reconnecting = false; _reconnectTicks = 0; _reconnectTries = 0;

            _live = true; _videoMode = true; _go.Text = "Завершить эфир"; _go.BackColor = Color.FromArgb(210, 70, 72);
            _status.Text = "● В ЭФИРЕ · RTMP (как OBS)";
            _link.Text = "Ссылка на эфир: " + _streamLink;
            _tick.Interval = 1000;   // таймер только для превью и счётчика зрителей
            _tick.Start();
        }

        private void SafeStatus(string s)
        {
            try { if (IsHandleCreated) BeginInvoke((Action)(() => { _status.Text = s; })); } catch { }
        }

        // эфир оборвался не по кнопке — ждём немного и переподключаемся (тот же ключ = та же ссылка)
        private void HandleDrop()
        {
            if (!_reconnecting)
            {
                _reconnecting = true; _reconnectTicks = 0; _reconnectTries = 0;
                try { if (_enc != null) _enc.Dispose(); } catch { }
                _enc = null;
                _status.Text = "⚠ Связь прервалась — переподключаюсь…";
            }
            _reconnectTicks++;
            if (_reconnectTicks < 3) return;   // пауза ~3 сек между попытками (тик = 1 сек)
            _reconnectTicks = 0; _reconnectTries++;
            if (_reconnectTries > ReconnectMaxTries)
            {
                _status.Text = "Не удалось переподключиться. Нажми «Завершить» и попробуй снова.";
                _reconnecting = false; _tick.Stop(); _live = false;
                _go.Text = "Выйти в эфир"; _go.BackColor = Color.FromArgb(91, 140, 255);
                return;
            }
            _status.Text = "Переподключение… попытка " + _reconnectTries + "/" + ReconnectMaxTries;
            var enc = new StreamEncoder(SafeStatus);
            bool ok = false;
            try { ok = enc.StartRtmp(_rtmpUrl, _rcFullScreen, _rcWinTitle, _rcBitrate, _rcFps); } catch { ok = false; }
            if (ok)
            {
                _enc = enc; _reconnecting = false; _reconnectTries = 0;
                _status.Text = "● Снова в эфире · RTMP";
            }
            // если не вышло — на следующих тиках попробуем ещё раз
        }

        private void Stop()
        {
            _tick.Stop(); _live = false; _reconnecting = false;
            try { if (_enc != null) _enc.Dispose(); } catch { }
            _enc = null; _videoMode = false;
            var acc = AccountStore.Selected;
            if (acc != null && _streamId != null) Post("/stream/stop", "{\"token\":\"" + Esc(acc.Token) + "\",\"id\":\"" + Esc(_streamId) + "\"}");
            _go.Text = "Выйти в эфир"; _go.BackColor = Color.FromArgb(91, 140, 255);
            _status.Text = "Эфир завершён";
        }

        private void Capture()
        {
            try
            {
                int i = _source.SelectedIndex;
                IntPtr hwnd = (i >= 0 && i < _windows.Count) ? _windows[i] : IntPtr.Zero;

                // локальное превью (и в режиме видео, и в режиме кадров)
                Bitmap bmp = hwnd == IntPtr.Zero ? GrabScreen() : GrabWindow(hwnd);
                if (bmp != null)
                {
                    using (bmp)
                    {
                        _preview.Image = (Image)bmp.Clone();   // локальное превью; само видео уходит по RTMP через ffmpeg
                    }
                }

                // если ffmpeg (RTMP) оборвался не по кнопке — пытаемся переподключиться сами
                if (_live && _enc != null && !_enc.Running) { HandleDrop(); return; }

                if (!_reconnecting && ++_viewerTick % 5 == 0 && _streamId != null)
                {
                    string r = Post("/stream/viewers", "{\"id\":\"" + Esc(_streamId) + "\"}");
                    if (r != null) { int v = Json.Parse(r)["count"].AsInt(0); _status.Text = "● В ЭФИРЕ · RTMP · " + v + " смотрят"; }
                }
            }
            catch { }
        }

        private static Bitmap GrabScreen()
        {
            var b = Screen.PrimaryScreen.Bounds;
            var bmp = new Bitmap(b.Width, b.Height);
            using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(b.Location, Point.Empty, b.Size);
            return bmp;
        }

        private static Bitmap GrabWindow(IntPtr hwnd)
        {
            RECT r; if (!GetWindowRect(hwnd, out r)) return null;
            int w = r.Right - r.Left, h = r.Bottom - r.Top;
            if (w < 10 || h < 10) return null;
            var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                IntPtr hdc = g.GetHdc();
                bool ok = PrintWindow(hwnd, hdc, 2);        // 2 = PW_RENDERFULLCONTENT
                g.ReleaseHdc(hdc);
                if (!ok) g.CopyFromScreen(r.Left, r.Top, 0, 0, new Size(w, h));
            }
            return bmp;
        }

        private static ImageCodecInfo _jpeg;
        private static string Encode(Bitmap src, int maxW, long q)
        {
            double scale = src.Width > maxW ? (double)maxW / src.Width : 1.0;
            int ow = Math.Max(1, (int)(src.Width * scale)), oh = Math.Max(1, (int)(src.Height * scale));
            using (var small = new Bitmap(ow, oh))
            {
                using (var g = Graphics.FromImage(small)) { g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear; g.DrawImage(src, 0, 0, ow, oh); }
                if (_jpeg == null) foreach (var c in ImageCodecInfo.GetImageEncoders()) if (c.FormatID == ImageFormat.Jpeg.Guid) { _jpeg = c; break; }
                var pars = new EncoderParameters(1); pars.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, q);
                using (var ms = new MemoryStream()) { small.Save(ms, _jpeg, pars); return Convert.ToBase64String(ms.ToArray()); }
            }
        }

        private static string CatSlug(string ru)
        {
            switch (ru) { case "Игры": return "games"; case "Разговоры": return "just-chatting";
                case "Музыка": return "music"; case "Творчество": return "creative"; default: return "stream"; }
        }

        private void ToggleOverlay()
        {
            if (_overlay != null && !_overlay.IsDisposed) { try { _overlay.Close(); } catch { } _overlay = null; _overlayBtn.Text = "Чат-оверлей"; return; }
            if (string.IsNullOrEmpty(_streamId)) { _status.Text = "Сначала выйдите в эфир."; return; }
            _overlay = new ChatOverlay(_streamId, true);   // справа
            _overlay.Show();
            _overlayBtn.Text = "Убрать оверлей";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_overlay != null && !_overlay.IsDisposed) { try { _overlay.Close(); } catch { } }
            _srcTick.Stop();
            if (_live) Stop();
            base.OnFormClosing(e);
        }

        // загрузить текущий постоянный ключ/ID и сохранённые название/описание
        private void LoadMeta()
        {
            var acc = AccountStore.Selected;
            if (acc == null || string.IsNullOrEmpty(acc.Token)) return;
            new System.Threading.Thread(() =>
            {
                string r = Post("/stream/key", "{\"token\":\"" + Esc(acc.Token) + "\"}");
                if (r == null) return;
                var j = Json.Parse(r);
                string key = j["key"].AsString("");
                string watch = j["watch"].AsString("/live/watch.php?s=" + key);
                UiInvoke(() =>
                {
                    if (_sid.Text.Trim().Length == 0) _sid.Text = key;
                    _streamLink = "https://narelauncher.winnigames2024.ru" + watch;
                    _link.Text = "Постоянная ссылка: " + _streamLink;
                });
            }) { IsBackground = true }.Start();
        }

        // сохранить свой ID (постоянную ссылку) + название/описание без выхода в эфир
        private void SaveMeta()
        {
            var acc = AccountStore.Selected;
            if (acc == null || string.IsNullOrEmpty(acc.Token)) { _status.Text = "Сначала войдите."; return; }
            string title = (_title.Text ?? "").Trim();
            string desc = (_desc.Text ?? "").Trim();
            string customKey = (_sid.Text ?? "").Trim();
            string cat = CatSlug(_category.SelectedItem as string);
            string sub = (_sub.Text ?? "").Trim();
            new System.Threading.Thread(() =>
            {
                string r = Post("/stream/key", "{\"token\":\"" + Esc(acc.Token) + "\",\"title\":\"" + Esc(title) + "\",\"category\":\"" + Esc(cat) +
                    "\",\"subcategory\":\"" + Esc(sub) + "\",\"desc\":\"" + Esc(desc) + "\",\"customKey\":\"" + Esc(customKey) + "\"}");
                var j = r != null ? Json.Parse(r) : null;
                UiInvoke(() =>
                {
                    if (j == null) { _status.Text = "Нет связи."; return; }
                    string ke = j["keyError"].AsString("");
                    if (ke.Length > 0) { _status.Text = "ID: " + ke; return; }
                    string key = j["key"].AsString("");
                    _sid.Text = key;
                    _streamLink = "https://narelauncher.winnigames2024.ru" + j["watch"].AsString("/live/watch.php?s=" + key);
                    _link.Text = "Постоянная ссылка: " + _streamLink;
                    _status.Text = "Сохранено ✓";
                });
            }) { IsBackground = true }.Start();
        }

        private void UiInvoke(Action a) { if (IsDisposed) return; try { if (InvokeRequired) BeginInvoke(a); else a(); } catch { } }

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

        // ------------------------------------------------------------ WinAPI
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr p);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr h, StringBuilder s, int max);
        [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    }
}
