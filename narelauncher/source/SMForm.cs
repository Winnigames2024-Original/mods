using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareServerManager
{
    /// <summary>
    /// ServerManager.exe — отдельное приложение: запускает сервер, держит тоннель до
    /// NareNet (онлайн + веб-панель), показывает консоль и управление. Запускается
    /// с папкой сервера как аргументом; имя/порт берёт из nare_run.json.
    /// </summary>
    public class SMForm : Form
    {
        private static readonly Color CBg = Color.FromArgb(10, 13, 20);
        private static readonly Color CCard = Color.FromArgb(16, 20, 31);
        private static readonly Color CBorder = Color.FromArgb(27, 34, 50);
        private static readonly Color CText = Color.FromArgb(231, 234, 242);
        private static readonly Color CDim = Color.FromArgb(139, 147, 167);
        private static readonly Color CGreen = Color.FromArgb(86, 211, 100);
        private static readonly Color CRed = Color.FromArgb(255, 107, 116);
        private static readonly Color CAmber = Color.FromArgb(240, 200, 90);
        private static readonly Color CTerm = Color.FromArgb(7, 10, 17);

        private readonly string _folder;
        private readonly string _name;
        private readonly int _port;
        private ServerManager _mgr;
        private NareHost _host;

        private readonly Label _title = new Label();
        private readonly PillLabel _pill = new PillLabel();
        private readonly Label _addr = new Label();
        private readonly Label _stats = new Label();
        private readonly Button _start = new Button();
        private readonly Button _restart = new Button();
        private readonly Button _stop = new Button();
        private readonly TextBox _log = new TextBox();
        private readonly TextBox _cmd = new TextBox();
        private readonly Button _send = new Button();
        private readonly Timer _tick = new Timer { Interval = 1500 };

        public SMForm(string folder)
        {
            _folder = folder;
            string name = Path.GetFileName(folder.TrimEnd('\\', '/'));
            int port = 25600;
            try
            {
                string rf = Path.Combine(folder, "nare_run.json");
                if (File.Exists(rf))
                {
                    var j = Json.Parse(File.ReadAllText(rf));
                    name = j["name"].AsString(name);
                    port = j["port"].AsInt(port);
                }
            }
            catch { }
            _name = name; _port = port;

            Text = "ServerManager — " + _name;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1000, 680);
            MinimumSize = new Size(760, 520);
            BackColor = CBg; ForeColor = CText; Font = new Font("Segoe UI", 9.5f);
            try { Icon = SystemIcons.Application; } catch { }

            _title.AutoSize = false; _title.Bounds = new Rectangle(20, 16, 560, 30); _title.Font = new Font("Segoe UI", 16f, FontStyle.Bold); _title.ForeColor = CText; _title.BackColor = Color.Transparent; _title.Text = _name;
            Controls.Add(_title);
            _pill.Bounds = new Rectangle(20, 50, 120, 26); _pill.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold); Controls.Add(_pill);
            _addr.AutoSize = false; _addr.Bounds = new Rectangle(150, 52, 820, 22); _addr.ForeColor = CDim; _addr.BackColor = Color.Transparent;
            _addr.Text = "🌐 " + _name + ".narelauncher.winnigames2024.ru   ·   локальный порт " + _port;
            Controls.Add(_addr);
            _stats.AutoSize = false; _stats.Bounds = new Rectangle(20, 82, 940, 22); _stats.ForeColor = CDim; _stats.BackColor = Color.Transparent; Controls.Add(_stats);

            StyleBtn(_start, "▶ Запустить", Color.FromArgb(17, 54, 32), CGreen); _start.Bounds = new Rectangle(20, 114, 150, 38); _start.Click += (s, e) => StartServer(); Controls.Add(_start);
            StyleBtn(_restart, "↻ Рестарт", Color.FromArgb(54, 45, 17), CAmber); _restart.Bounds = new Rectangle(178, 114, 140, 38); _restart.Click += (s, e) => { StopServer(); var t = new Timer { Interval = 6000 }; t.Tick += (a, b) => { t.Stop(); t.Dispose(); StartServer(); }; t.Start(); }; Controls.Add(_restart);
            StyleBtn(_stop, "■ Остановить", Color.FromArgb(54, 20, 23), CRed); _stop.Bounds = new Rectangle(326, 114, 150, 38); _stop.Click += (s, e) => StopServer(); Controls.Add(_stop);

            _log.Multiline = true; _log.ReadOnly = true; _log.ScrollBars = ScrollBars.Vertical; _log.BackColor = CTerm; _log.ForeColor = Color.FromArgb(194, 204, 221); _log.BorderStyle = BorderStyle.FixedSingle; _log.Font = new Font("Consolas", 9.5f);
            _log.Bounds = new Rectangle(20, 166, 950, 420); _log.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom; Controls.Add(_log);

            _cmd.BorderStyle = BorderStyle.FixedSingle; _cmd.BackColor = CCard; _cmd.ForeColor = CText; _cmd.Font = new Font("Consolas", 10f);
            _cmd.Bounds = new Rectangle(20, 598, 830, 30); _cmd.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _cmd.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SendCmd(); } };
            Controls.Add(_cmd);
            StyleBtn(_send, "Ввод", Color.FromArgb(27, 44, 82), Color.FromArgb(157, 180, 255)); _send.Bounds = new Rectangle(860, 597, 110, 32); _send.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; _send.Click += (s, e) => SendCmd(); Controls.Add(_send);

            SetOnline(false);
            _tick.Tick += (s, e) => UpdateStats();
            _tick.Start();
            Load += (s, e) => StartServer();
            FormClosing += (s, e) => { try { StopServer(); } catch { } };
        }

        private void StartServer()
        {
            if (_mgr != null && _mgr.Running) { Append("Уже запущен."); return; }
            try
            {
                _host = new NareHost(_name, _port);
                _host.OnLog = s => Ui(() => Append("[тоннель] " + s));
                _host.OnCommand = s => Ui(() => { if (_mgr != null && _mgr.Running) { _mgr.Send(s); Append("[web] > " + s); } });
                _host.StatsJson = () => (_mgr != null ? _mgr.StatsJson() : null);
                _host.OnFs = (action, pth, content) => HandleFs(action, pth, content);
                var meta = ReadMeta();
                _host.Loader = meta[0]; _host.McVersion = meta[1];

                _mgr = new ServerManager(_folder, _port);
                _mgr.OnLine = s => { Ui(() => Append(s)); if (_host != null) _host.PushLog(s); };
                _mgr.OnExit = () => Ui(() => { Append("Сервер остановлен."); SetOnline(false); });
                _mgr.Start();
                _host.Start();
                Append("Запуск... веб-панель: http://narelauncher.winnigames2024.ru/server?name=" + _name);
                SetOnline(true);
            }
            catch (Exception ex) { Append("Ошибка запуска: " + ex.Message); }
        }

        private void StopServer()
        {
            try { if (_host != null) { _host.Stop(); _host = null; } } catch { }
            var mgr = _mgr;
            if (mgr != null) new System.Threading.Thread(() => { try { mgr.Stop(); } catch { } }) { IsBackground = true }.Start();
            SetOnline(false);
        }

        private void SendCmd()
        {
            if (_mgr == null || !_mgr.Running) return;
            string c = (_cmd.Text ?? "").Trim();
            if (c.Length == 0) return;
            _mgr.Send(c); Append("> " + c); _cmd.Text = "";
        }

        private void SetOnline(bool on)
        {
            _pill.On = on; _pill.Invalidate();
            _start.Enabled = !on; _stop.Enabled = on; _restart.Enabled = on;
        }

        private void UpdateStats()
        {
            if (_mgr == null || !_mgr.Running) { _stats.Text = ""; return; }
            try { _mgr.SampleCpu(); } catch { }
            _stats.Text = "CPU " + _mgr.CpuPct.ToString("0.0") + "%   ·   Память " + (_mgr.RamMb / 1024.0).ToString("0.00") + " ГиБ   ·   Аптайм " + FmtUp(_mgr.UptimeSec) + "   ·   Игроков " + _mgr.Players;
        }

        private static string FmtUp(int s)
        {
            if (s < 0) s = 0; int d = s / 86400, h = (s % 86400) / 3600, m = (s % 3600) / 60;
            if (d > 0) return d + "д " + h + "ч"; if (h > 0) return h + "ч " + m + "м"; return m + "м";
        }

        // ---------- файловый мост (как в лаунчере) ----------
        private string[] ReadMeta()
        {
            try { string f = Path.Combine(_folder, "nare_core.txt"); if (File.Exists(f)) { var p = File.ReadAllText(f).Split('|'); return new[] { p.Length > 0 ? p[0].Trim() : "", p.Length > 1 ? p[1].Trim() : "" }; } }
            catch { }
            return new[] { "", "" };
        }
        private string SubFolder()
        {
            string core = ReadMeta()[0];
            if (core == "paper") return "plugins";
            if (core == "forge" || core == "fabric") return "mods";
            if (Directory.Exists(Path.Combine(_folder, "plugins"))) return "plugins";
            return "mods";
        }
        private static string JEsc(string v)
        {
            v = v ?? ""; var b = new StringBuilder(v.Length + 8);
            foreach (char c in v) { if (c == '\\') b.Append("\\\\"); else if (c == '"') b.Append("\\\""); else if (c == '\n') b.Append("\\n"); else if (c == '\r') b.Append("\\r"); else if (c == '\t') b.Append("\\t"); else if (c < 0x20) b.Append(' '); else b.Append(c); }
            return b.ToString();
        }
        private static string ErrJ(string m) { return "{\"ok\":false,\"error\":\"" + JEsc(m) + "\"}"; }

        private string HandleFs(string action, string rel, string content)
        {
            try
            {
                string root = Path.GetFullPath(_folder);
                Directory.CreateDirectory(root);
                rel = (rel ?? "").Replace('\\', '/').TrimStart('/');
                string full = Path.GetFullPath(Path.Combine(root, rel));
                if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return ErrJ("путь вне папки");

                if (action == "list")
                {
                    string dir = rel.Length == 0 ? root : full;
                    if (!Directory.Exists(dir)) return ErrJ("нет папки");
                    var sb = new StringBuilder("{\"ok\":true,\"path\":\"" + JEsc(rel) + "\",\"entries\":[");
                    bool f = true;
                    foreach (var d in Directory.GetDirectories(dir)) { if (!f) sb.Append(','); f = false; sb.Append("{\"name\":\"").Append(JEsc(Path.GetFileName(d))).Append("\",\"dir\":true,\"size\":0}"); }
                    foreach (var fl in Directory.GetFiles(dir)) { if (!f) sb.Append(','); f = false; long sz = 0; try { sz = new FileInfo(fl).Length; } catch { } sb.Append("{\"name\":\"").Append(JEsc(Path.GetFileName(fl))).Append("\",\"dir\":false,\"size\":").Append(sz).Append('}'); }
                    sb.Append("]}"); return sb.ToString();
                }
                if (action == "read")
                {
                    if (!File.Exists(full)) return ErrJ("нет файла");
                    if (new FileInfo(full).Length > 1024 * 1024) return ErrJ("файл больше 1 МБ");
                    return "{\"ok\":true,\"content\":\"" + JEsc(File.ReadAllText(full)) + "\"}";
                }
                if (action == "write") { Directory.CreateDirectory(Path.GetDirectoryName(full)); File.WriteAllText(full, content ?? ""); return "{\"ok\":true}"; }
                if (action == "delete") { if (Directory.Exists(full)) Directory.Delete(full, true); else if (File.Exists(full)) File.Delete(full); return "{\"ok\":true}"; }
                if (action == "plugins")
                {
                    string sub = SubFolder(); string dir = Path.Combine(root, sub); Directory.CreateDirectory(dir);
                    var sb = new StringBuilder("{\"ok\":true,\"sub\":\"" + sub + "\",\"items\":["); bool f = true;
                    foreach (var fl in Directory.GetFiles(dir, "*.jar")) { if (!f) sb.Append(','); f = false; sb.Append('"').Append(JEsc(Path.GetFileName(fl))).Append('"'); }
                    sb.Append("]}"); return sb.ToString();
                }
                if (action == "players")
                {
                    var sb = new StringBuilder("{\"ok\":true,\"players\":["); if (_mgr != null) { var ns = _mgr.OnlineNames; for (int i = 0; i < ns.Length; i++) { if (i > 0) sb.Append(','); sb.Append('"').Append(JEsc(ns[i])).Append('"'); } } sb.Append("]}"); return sb.ToString();
                }
                if (action == "launchget")
                {
                    var c = ServerManager.ReadLaunchCfg(_folder);
                    return "{\"ok\":true,\"jar\":\"" + JEsc(c.Jar) + "\",\"ramMax\":" + c.RamMax + ",\"ramMin\":" + c.RamMin + ",\"java\":" + c.JavaMajor + ",\"gc\":\"" + JEsc(c.Gc) + "\",\"args\":\"" + JEsc(c.Args) + "\"}";
                }
                if (action == "launchset")
                {
                    var j = Json.Parse(content ?? "{}");
                    var c = new ServerManager.LaunchCfg { Jar = j["jar"].AsString("server.jar"), RamMax = j["ramMax"].AsInt(2048), RamMin = j["ramMin"].AsInt(1024), JavaMajor = j["java"].AsInt(0), Gc = j["gc"].AsString("g1"), Args = j["args"].AsString("") };
                    ServerManager.WriteLaunchCfg(_folder, c); return "{\"ok\":true}";
                }
                if (action == "psearch")
                {
                    var meta = ReadMeta(); bool plugins = SubFolder() == "plugins";
                    string type = plugins ? "plugin" : "mod"; string cat = plugins ? "paper" : (meta[0] == "fabric" ? "fabric" : (meta[0] == "forge" ? "forge" : ""));
                    var res = ModrinthClient.Search(rel, type, cat, meta[1]);
                    var sb = new StringBuilder("{\"ok\":true,\"items\":["); for (int i = 0; i < res.Count; i++) { if (i > 0) sb.Append(','); sb.Append("{\"slug\":\"").Append(JEsc(res[i].Slug)).Append("\",\"title\":\"").Append(JEsc(res[i].Title)).Append("\",\"downloads\":").Append(res[i].Downloads).Append(",\"icon\":\"").Append(JEsc(res[i].IconUrl)).Append("\"}"); } sb.Append("]}"); return sb.ToString();
                }
                if (action == "pinstall")
                {
                    var meta = ReadMeta(); bool plugins = SubFolder() == "plugins";
                    string[] loaders = plugins ? new[] { "paper", "spigot", "bukkit", "purpur", "folia" } : (meta[0] == "fabric" ? new[] { "fabric", "quilt" } : new[] { "forge", "neoforge" });
                    var dl = ModrinthClient.Resolve(rel, loaders, meta[1]); if (dl == null || string.IsNullOrEmpty(dl[0])) return ErrJ("нет совместимой версии");
                    string sub = Path.Combine(root, SubFolder()); Directory.CreateDirectory(sub);
                    string fn = dl[1].Length > 0 ? dl[1] : rel + ".jar"; ModrinthClient.Download(dl[0], Path.Combine(sub, fn)); return "{\"ok\":true,\"file\":\"" + JEsc(fn) + "\"}";
                }
                return ErrJ("неизвестное действие");
            }
            catch (Exception ex) { return ErrJ(ex.Message); }
        }

        private void Ui(Action a) { try { if (IsHandleCreated) BeginInvoke(a); else a(); } catch { } }
        private void Append(string s)
        {
            if (_log.TextLength > 80000) _log.Text = _log.Text.Substring(_log.TextLength - 50000);
            _log.AppendText(s + "\r\n");
        }
        private static void StyleBtn(Button b, string text, Color bg, Color fg)
        {
            b.Text = text; b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0; b.BackColor = bg; b.ForeColor = fg; b.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold); b.Cursor = Cursors.Hand;
        }

        private class PillLabel : Control
        {
            public bool On;
            public PillLabel() { DoubleBuffered = true; SetStyle(ControlStyles.SupportsTransparentBackColor, true); BackColor = Color.Transparent; }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                Color bg = On ? Color.FromArgb(16, 53, 31) : Color.FromArgb(53, 20, 22);
                Color fg = On ? Color.FromArgb(86, 211, 100) : Color.FromArgb(255, 107, 116);
                var gp = new GraphicsPath(); int r = Height; var rc = new Rectangle(0, 0, Width - 1, Height - 1);
                gp.AddArc(rc.X, rc.Y, r, r, 90, 180); gp.AddArc(rc.Right - r, rc.Y, r, r, 270, 180); gp.CloseFigure();
                using (var b = new SolidBrush(bg)) g.FillPath(b, gp);
                using (var b = new SolidBrush(fg)) g.FillEllipse(b, 11, Height / 2 - 4, 8, 8);
                TextRenderer.DrawText(g, On ? "Онлайн" : "Оффлайн", Font, new Rectangle(25, 0, Width - 25, Height), fg, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
        }
    }
}
