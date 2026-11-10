using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Net;
using System.IO;
using System.Text;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// «Мои серверы» — выделенные серверы игрока на его ПК в стиле игрового хостинга:
    /// карточка сервера со статусом, карточки ресурсов (CPU/память/аптайм/игроки),
    /// кнопки питания и консоль. Лаунчер запускает server.jar и держит тоннель до
    /// NareNet; сервер доступен по «ник-id.narelauncher.winnigames2024.ru».
    /// </summary>
    public class HostPage : PageBase
    {
        private const string DOMAIN = "narelauncher.winnigames2024.ru";

        // палитра как на веб-панели
        private static readonly Color CBg = Color.FromArgb(10, 13, 20);
        private static readonly Color CCard = Color.FromArgb(16, 20, 31);
        private static readonly Color CBorder = Color.FromArgb(27, 34, 50);
        private static readonly Color CText = Color.FromArgb(231, 234, 242);
        private static readonly Color CDim = Color.FromArgb(139, 147, 167);
        private static readonly Color CAccent = Color.FromArgb(91, 140, 255);
        private static readonly Color CGreen = Color.FromArgb(86, 211, 100);
        private static readonly Color CAmber = Color.FromArgb(240, 200, 90);
        private static readonly Color CRed = Color.FromArgb(255, 107, 116);
        private static readonly Color CTermBg = Color.FromArgb(7, 10, 17);

        private class Entry
        {
            public string Id;
            public string Folder;
            public int Port;
            public ServerManager Mgr;
            public NareHost Host;
        }

        private readonly List<Entry> _servers = new List<Entry>();
        private Entry _sel;

        private readonly ComboBox _list = new ComboBox();
        private readonly Button _create = new Button();
        private readonly Button _dl = new Button();

        private readonly CardPanel _hdr = new CardPanel();
        private readonly Label _srvName = new Label();
        private readonly PillLabel _pill = new PillLabel();
        private readonly Label _addr = new Label();

        private readonly CardPanel _cpuCard = new CardPanel();
        private readonly CardPanel _memCard = new CardPanel();
        private readonly CardPanel _upCard = new CardPanel();
        private readonly CardPanel _plCard = new CardPanel();
        private Label _cpuVal, _memVal, _upVal, _plVal;

        private readonly Button _start = new Button();
        private readonly Button _restart = new Button();
        private readonly Button _stop = new Button();

        private readonly CardPanel _termCard = new CardPanel();
        private readonly Label _termBar = new Label();
        private readonly TextBox _log = new TextBox();

        private readonly CardPanel _cmdCard = new CardPanel();
        private readonly TextBox _cmd = new TextBox();
        private readonly Button _send = new Button();

        private readonly Timer _stats = new Timer();

        // под-вкладки управления (как в хостинг-панели)
        private readonly Button _tabConsole = new Button();
        private readonly Button _tabFiles = new Button();
        private readonly Button _tabJars = new Button();
        private readonly Button _tabProps = new Button();
        private string _view = "console";
        private readonly CardPanel _filesPanel = new CardPanel();
        private readonly ListBox _filesList = new ListBox();
        private Button[] _filesBtns;
        private readonly CardPanel _jarsPanel = new CardPanel();
        private readonly FlowLayoutPanel _jarsFlow = new FlowLayoutPanel();
        private readonly Label _jarsTitle = new Label();
        private readonly TextBox _jarsSearch = new TextBox();
        private List<ModProject> _catalog = new List<ModProject>();
        private Button[] _jarsBtns;
        private readonly CardPanel _propsPanel = new CardPanel();
        private readonly TextBox _propsText = new TextBox();
        private Button[] _propsBtns;

        public HostPage()
        {
            BackColor = CBg;

            Controls.Add(Title("Мои серверы", 15f, FontStyle.Bold, CText, new Rectangle(24, 16, 400, 28)));
            Controls.Add(Title("Запусти свой сервер на этом ПК — он будет доступен по адресу-поддомену.",
                9.5f, FontStyle.Regular, CDim, new Rectangle(24, 44, 760, 20)));

            // выбор сервера + создать + скачать
            _list.DropDownStyle = ComboBoxStyle.DropDownList;
            _list.FlatStyle = FlatStyle.Flat;
            _list.BackColor = CCard;
            _list.ForeColor = CText;
            _list.Font = Theme.F(10f);
            _list.Bounds = new Rectangle(24, 74, 300, 28);
            _list.SelectedIndexChanged += (s, e) => SelectIndex(_list.SelectedIndex);
            Controls.Add(_list);

            StyleBtn(_create, "Создать сервер", CCard, CAccent, CAccent);
            _create.Bounds = new Rectangle(336, 73, 160, 30);
            _create.Click += (s, e) => CreateServer();
            Controls.Add(_create);

            StyleBtn(_dl, "Скачать jar", CCard, CDim, CBorder);
            _dl.Bounds = new Rectangle(508, 73, 150, 30);
            _dl.Click += (s, e) => DownloadJar();
            Controls.Add(_dl);

            // карточка сервера
            _hdr.Bounds = new Rectangle(24, 114, 760, 92);
            _srvName.AutoSize = false; _srvName.BackColor = Color.Transparent; _srvName.ForeColor = CText;
            _srvName.Font = Theme.F(16f, FontStyle.Bold); _srvName.Bounds = new Rectangle(18, 14, 460, 30);
            _srvName.Text = "Сервер не выбран";
            _hdr.Controls.Add(_srvName);
            _pill.Bounds = new Rectangle(486, 18, 118, 26); _pill.Font = Theme.F(9.5f, FontStyle.Bold);
            _hdr.Controls.Add(_pill);
            _addr.AutoSize = false; _addr.BackColor = Color.Transparent; _addr.ForeColor = CDim;
            _addr.Font = Theme.F(9.5f); _addr.Bounds = new Rectangle(18, 52, 720, 22);
            _addr.Text = "Создай или выбери сервер слева.";
            _hdr.Controls.Add(_addr);
            Controls.Add(_hdr);

            // карточки ресурсов
            _cpuVal = StatCard(_cpuCard, "Загрузка CPU");
            _memVal = StatCard(_memCard, "Память");
            _upVal = StatCard(_upCard, "Аптайм");
            _plVal = StatCard(_plCard, "Игроки");
            Controls.Add(_cpuCard); Controls.Add(_memCard); Controls.Add(_upCard); Controls.Add(_plCard);

            // кнопки питания
            StyleBtn(_start, "▶  Запустить", CCard, CGreen, CBorder);
            _start.Click += (s, e) => StartSel();
            Controls.Add(_start);
            StyleBtn(_restart, "↻  Рестарт", CCard, CAmber, CBorder);
            _restart.Click += (s, e) => RestartSel();
            Controls.Add(_restart);
            StyleBtn(_stop, "⏻  Остановить", CCard, CRed, Color.FromArgb(92, 31, 36));
            _stop.Click += (s, e) => StopSel();
            Controls.Add(_stop);

            // консоль
            _termBar.AutoSize = false; _termBar.BackColor = Color.Transparent; _termBar.ForeColor = CDim;
            _termBar.Font = Theme.F(9f); _termBar.Text = "Консоль сервера";
            _termBar.TextAlign = ContentAlignment.MiddleLeft;
            _termCard.BackColor = CTermBg;
            _termCard.Controls.Add(_termBar);
            _log.Multiline = true; _log.ReadOnly = true; _log.ScrollBars = ScrollBars.Vertical;
            _log.BackColor = CTermBg; _log.ForeColor = Color.FromArgb(194, 204, 221);
            _log.BorderStyle = BorderStyle.None; _log.Font = new Font("Consolas", 9f);
            _termCard.Controls.Add(_log);
            Controls.Add(_termCard);

            // строка команд
            _cmdCard.Bounds = new Rectangle(24, 500, 640, 40);
            _cmd.BorderStyle = BorderStyle.None; _cmd.BackColor = CCard; _cmd.ForeColor = CText;
            _cmd.Font = new Font("Consolas", 10f);
            _cmd.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SendCmd(); } };
            _cmdCard.Controls.Add(_cmd);
            Controls.Add(_cmdCard);
            StyleBtn(_send, "Ввод", Color.FromArgb(27, 44, 82), Color.FromArgb(157, 180, 255), Color.FromArgb(27, 44, 82));
            _send.Click += (s, e) => SendCmd();
            Controls.Add(_send);

            BuildManage();
            LoadList();
            LoadRemote();
            SetOnline(false);
            ShowView("console");

            _stats.Interval = 1500;
            _stats.Tick += (s, e) => UpdateStats();
            _stats.Start();

            NareAgent.OnControl = (name, action) => Ui(() => WebPower(name, action));
            NareAgent.Start();

            Resize += (s, e) => Relayout();
        }

        public override void PageShown() { Relayout(); }

        private void Relayout()
        {
            int W = Math.Max(640, Width);
            int m = 24, right = W - m;
            _hdr.Bounds = new Rectangle(m, 114, right - m, 92);
            _pill.Left = _hdr.Width - 136; _addr.Width = _hdr.Width - 36; _srvName.Width = _hdr.Width - 160;

            int gap = 14, cw = (right - m - gap * 3) / 4, cy = 218, ch = 76;
            _cpuCard.Bounds = new Rectangle(m, cy, cw, ch);
            _memCard.Bounds = new Rectangle(m + (cw + gap), cy, cw, ch);
            _upCard.Bounds = new Rectangle(m + (cw + gap) * 2, cy, cw, ch);
            _plCard.Bounds = new Rectangle(m + (cw + gap) * 3, cy, right - (m + (cw + gap) * 3), ch);

            int by = cy + ch + 14;
            _start.Bounds = new Rectangle(m, by, 150, 40);
            _restart.Bounds = new Rectangle(m + 158, by, 140, 40);
            _stop.Bounds = new Rectangle(m + 306, by, 150, 40);

            int tabY = by + 52;
            _tabConsole.Bounds = new Rectangle(m, tabY, 104, 30);
            _tabFiles.Bounds = new Rectangle(m + 108, tabY, 96, 30);
            _tabJars.Bounds = new Rectangle(m + 208, tabY, 108, 30);
            _tabProps.Bounds = new Rectangle(m + 320, tabY, 172, 30);

            int cTop = tabY + 38, cBottom = Math.Max(cTop + 120, Height - 14);
            int cmdY = cBottom - 40;
            _termCard.Bounds = new Rectangle(m, cTop, right - m, cmdY - cTop - 10);
            _termBar.Bounds = new Rectangle(14, 8, _termCard.Width - 28, 20);
            _log.Bounds = new Rectangle(12, 32, _termCard.Width - 24, _termCard.Height - 42);
            _send.Bounds = new Rectangle(right - 110, cmdY, 110, 40);
            _cmdCard.Bounds = new Rectangle(m, cmdY, _send.Left - m - 10, 40);
            _cmd.Bounds = new Rectangle(14, 11, _cmdCard.Width - 28, 20);

            var full = new Rectangle(m, cTop, right - m, cBottom - cTop);
            _filesPanel.Bounds = full; _jarsPanel.Bounds = full; _propsPanel.Bounds = full;
            LayoutFiles(); LayoutJars(); LayoutProps();
        }

        // ---------- данные ----------

        private string Token
        {
            get { var a = AccountStore.Selected; return a == null ? "" : (a.Token ?? ""); }
        }

        private string Nick
        {
            get { var a = AccountStore.Selected; return (a == null ? "player" : a.Name).ToLowerInvariant(); }
        }

        private void LoadList()
        {
            _servers.Clear();
            foreach (var line in (Settings.Current.HostServers ?? "").Split('\n'))
            {
                var t = line.Trim();
                if (t.Length == 0) continue;
                var p = t.Split('|');
                if (p.Length < 3) continue;
                int port;
                int.TryParse(p[2], out port);
                _servers.Add(new Entry { Id = p[0], Folder = p[1], Port = port });
            }
            RefreshList();
        }

        private void SaveList()
        {
            var sb = new StringBuilder();
            foreach (var e in _servers)
                sb.Append(e.Id).Append('|').Append(e.Folder).Append('|').Append(e.Port).Append('\n');
            Settings.Current.HostServers = sb.ToString();
            Settings.Save();
            SaveRemote();
        }

        // --- синхронизация списка серверов с нашим сервером (по аккаунту) ---
        private void SaveRemote()
        {
            string tok = Token;
            if (tok.Length == 0) return;
            var sb = new StringBuilder("{\"token\":\"" + Esc(tok) + "\",\"servers\":[");
            for (int i = 0; i < _servers.Count; i++)
            {
                var e = _servers[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"id\":\"").Append(Esc(e.Id)).Append("\",\"folder\":\"")
                  .Append(Esc(e.Folder)).Append("\",\"port\":").Append(e.Port).Append('}');
            }
            sb.Append("]}");
            string body = sb.ToString();
            new System.Threading.Thread(() => { try { HttpPost("/host/save", body); } catch { } }) { IsBackground = true }.Start();
        }

        private void LoadRemote()
        {
            string tok = Token;
            if (tok.Length == 0) return;
            new System.Threading.Thread(() =>
            {
                string resp = null;
                try { resp = HttpPost("/host/list", "{\"token\":\"" + Esc(tok) + "\"}"); } catch { }
                if (resp == null) return;
                try
                {
                    var j = Json.Parse(resp);
                    var remote = new List<Entry>();
                    foreach (var v in j["servers"].Items)
                        remote.Add(new Entry { Id = v["id"].AsString(""), Folder = v["folder"].AsString(""), Port = v["port"].AsInt(25600) });
                    Ui(() => MergeRemote(remote));
                }
                catch { }
            }) { IsBackground = true }.Start();
        }

        private void MergeRemote(List<Entry> remote)
        {
            bool changed = false;
            foreach (var r in remote)
            {
                if (string.IsNullOrEmpty(r.Id)) continue;
                bool found = false;
                foreach (var e in _servers) if (e.Id == r.Id) { found = true; break; }
                if (!found) { _servers.Add(r); changed = true; }
            }
            if (changed)
            {
                SaveList();
                RefreshList();
            }
        }

        private static string Esc(string s)
        {
            s = s ?? "";
            var b = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                if (c == '\\') b.Append("\\\\");
                else if (c == '"') b.Append("\\\"");
                else if (c == '\n') b.Append("\\n");
                else if (c == '\r') b.Append("\\r");
                else if (c == '\t') b.Append("\\t");
                else if (c < 0x20) b.Append(' ');
                else b.Append(c);
            }
            return b.ToString();
        }

        private static string HttpPost(string route, string body)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("http://" + NareNet.Where + route);
                req.Method = "POST"; req.ContentType = "application/json"; req.Timeout = 12000;
                var data = Encoding.UTF8.GetBytes(body);
                req.ContentLength = data.Length;
                using (var st = req.GetRequestStream()) st.Write(data, 0, data.Length);
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    return rd.ReadToEnd();
            }
            catch (WebException we)
            {
                try { using (var rd = new StreamReader(we.Response.GetResponseStream(), Encoding.UTF8)) return rd.ReadToEnd(); }
                catch { return null; }
            }
            catch { return null; }
        }

        private void RefreshList()
        {
            _list.Items.Clear();
            foreach (var e in _servers) _list.Items.Add(e.Id);
            if (_servers.Count > 0) _list.SelectedIndex = 0;
            else { _sel = null; _srvName.Text = "Серверов пока нет"; _addr.Text = "Нажми «Создать сервер», чтобы начать."; }
        }

        private void SelectIndex(int i)
        {
            if (i < 0 || i >= _servers.Count) { _sel = null; return; }
            _sel = _servers[i];
            _srvName.Text = Nick + "-" + _sel.Id;
            _addr.Text = "🌐 " + Nick + "-" + _sel.Id + "." + DOMAIN + "   ·   локальный порт " + _sel.Port;
            UpdateStats();
            UpdateJarsLabel();
            RefreshActiveView();
        }

        private void CreateServer()
        {
            string raw = InputPrompt.Ask(FindForm(), "ID сервера (латиница/цифры, напр. 1 или survival):", "");
            if (string.IsNullOrEmpty(raw)) return;
            var clean = new StringBuilder();
            foreach (char c in raw.ToLowerInvariant())
                if (char.IsLetterOrDigit(c)) clean.Append(c);
            string id = clean.ToString();
            if (id.Length == 0) return;
            foreach (var e in _servers)
                if (e.Id == id) { MessageBox.Show("Сервер с таким id уже есть"); return; }

            string folder = Path.Combine(Paths.DataDir, "servers", id);
            Directory.CreateDirectory(folder);
            int port = 25600 + _servers.Count;
            _servers.Add(new Entry { Id = id, Folder = folder, Port = port });
            SaveList();
            RefreshList();
            _list.SelectedIndex = _servers.Count - 1;
            Append("Создан сервер «" + id + "».");
            Append("Положи серверный jar как server.jar в папку (или жми «Скачать jar»):");
            Append("  " + folder);
        }

        private void StartSel()
        {
            if (_sel == null) { Append("Сначала создай или выбери сервер."); return; }
            StartEntry(_sel);
        }

        private void StartEntry(Entry entry)
        {
            if (entry == null) return;
            if (entry.Mgr != null && entry.Mgr.Running) { Append("Уже запущен."); return; }
            try
            {
                entry.Host = new NareHost(Nick + "-" + entry.Id, entry.Port);
                entry.Host.OnLog = s => Ui(() => Append("[тоннель] " + s));
                entry.Host.OnCommand = s => Ui(() =>
                {
                    if (entry.Mgr != null && entry.Mgr.Running) { entry.Mgr.Send(s); Append("[web] > " + s); }
                });
                entry.Host.StatsJson = () => (entry.Mgr != null ? entry.Mgr.StatsJson() : null);
                var meta = ReadMeta(entry.Folder);
                entry.Host.Loader = meta[0]; entry.Host.McVersion = meta[1];

                entry.Mgr = new ServerManager(entry.Folder, entry.Port);
                entry.Mgr.OnLine = s =>
                {
                    Ui(() => Append(s));
                    if (entry.Host != null) entry.Host.PushLog(s);
                };
                entry.Mgr.OnExit = () => Ui(() => { Append("Сервер остановлен."); StopHost(entry); if (_sel == entry) SetOnline(false); });
                entry.Mgr.Start();
                entry.Host.Start();

                Append("Запуск... адрес: " + Nick + "-" + entry.Id + "." + DOMAIN);
                Append("Веб-панель: http://" + DOMAIN + "/server?name=" + Nick + "-" + entry.Id);
                if (_sel == entry) SetOnline(true);
            }
            catch (Exception ex) { Append("Ошибка запуска: " + ex.Message); }
        }

        /// <summary>Диалог выбора ядра: forge|fabric|vanilla|paper или null (отмена).</summary>
        private string ChooseCore()
        {
            using (var dlg = new Form())
            {
                dlg.Text = "Тип сервера";
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MaximizeBox = false; dlg.MinimizeBox = false;
                dlg.ClientSize = new Size(340, 236);
                dlg.BackColor = CBg; dlg.ForeColor = CText; dlg.Font = Theme.F(10f);

                dlg.Controls.Add(new Label
                {
                    Text = "Выбери ядро сервера:", AutoSize = false, Bounds = new Rectangle(18, 14, 300, 22),
                    ForeColor = CDim, BackColor = Color.Transparent
                });

                string result = null;
                var defs = new[] {
                    new[]{"forge","Forge","моды Forge (ставится установщиком)"},
                    new[]{"fabric","Fabric","моды Fabric (лёгкий, быстрый)"},
                    new[]{"vanilla","Vanilla","чистый сервер Mojang"},
                    new[]{"paper","Paper","плагины, оптимизирован"}
                };
                int y = 44;
                foreach (var d in defs)
                {
                    var b = new Button();
                    StyleBtn(b, d[1] + "  —  " + d[2], CCard, CText, CBorder);
                    b.TextAlign = ContentAlignment.MiddleLeft;
                    b.Font = Theme.F(9.5f, FontStyle.Bold);
                    b.Bounds = new Rectangle(18, y, 304, 40);
                    string code = d[0];
                    b.Click += (s, e) => { result = code; dlg.DialogResult = DialogResult.OK; dlg.Close(); };
                    dlg.Controls.Add(b);
                    y += 46;
                }
                dlg.ShowDialog(FindForm());
                return result;
            }
        }

        private void DownloadJar()
        {
            if (_sel == null) { Append("Сначала создай или выбери сервер."); return; }
            string type = ChooseCore();
            if (type == null) return;
            string ver = InputPrompt.Ask(FindForm(), "Версия Minecraft (напр. 1.21.4):", "1.21.4");
            if (string.IsNullOrEmpty(ver)) return;
            var entry = _sel;
            Append("Скачивание " + type + " " + ver + "...");
            new System.Threading.Thread(() =>
            {
                try
                {
                    ServerJar.Download(type, ver, entry.Folder, s => Ui(() => Append(s)));
                    Ui(() => Append("Jar скачан. Теперь жми «Запустить»."));
                }
                catch (Exception ex) { Ui(() => Append("Не удалось скачать jar: " + ex.Message)); }
            }) { IsBackground = true }.Start();
        }

        private void StopSel() { if (_sel != null) { StopHost(_sel); SetOnline(false); } }

        private void RestartSel()
        {
            if (_sel == null) return;
            var e = _sel;
            Append("Перезапуск...");
            StopHost(e);
            var t = new Timer { Interval = 6000 };
            t.Tick += (s, ev) => { t.Stop(); t.Dispose(); StartEntry(e); };
            t.Start();
        }

        private void StopHost(Entry e)
        {
            try { if (e.Host != null) { e.Host.Stop(); e.Host = null; } } catch { }
            var mgr = e.Mgr;
            if (mgr != null)
                new System.Threading.Thread(() => { try { mgr.Stop(); } catch { } }) { IsBackground = true }.Start();
        }

        // Кнопки питания из веб-панели (через NareAgent).
        private void WebPower(string name, string action)
        {
            Entry e = null;
            foreach (var x in _servers)
                if (string.Equals((Nick + "-" + x.Id), name, StringComparison.OrdinalIgnoreCase)) { e = x; break; }
            if (e == null) { Append("[web] неизвестный сервер: " + name); return; }
            if (action == "start") { Append("[web] запрос запуска «" + e.Id + "»"); StartEntry(e); }
            else if (action == "stop") { Append("[web] запрос остановки «" + e.Id + "»"); StopHost(e); if (_sel == e) SetOnline(false); }
            else if (action == "restart")
            {
                Append("[web] перезапуск «" + e.Id + "»");
                StopHost(e);
                var ee = e;
                var t = new Timer { Interval = 6000 };
                t.Tick += (s, ev) => { t.Stop(); t.Dispose(); StartEntry(ee); };
                t.Start();
            }
        }

        private void SendCmd()
        {
            if (_sel == null || _sel.Mgr == null || !_sel.Mgr.Running) return;
            string c = (_cmd.Text ?? "").Trim();
            if (c.Length == 0) return;
            _sel.Mgr.Send(c);
            Append("> " + c);
            _cmd.Text = "";
        }

        // ---------- статус/статистика ----------

        private void SetOnline(bool on)
        {
            _pill.On = on; _pill.Invalidate();
            _start.Enabled = !on; _stop.Enabled = on; _restart.Enabled = on;
            _start.ForeColor = _start.Enabled ? CGreen : CDim;
            _stop.ForeColor = _stop.Enabled ? CRed : CDim;
            _restart.ForeColor = _restart.Enabled ? CAmber : CDim;
            if (!on) { _cpuVal.Text = "—"; _memVal.Text = "—"; _upVal.Text = "—"; _plVal.Text = "—"; }
        }

        private void UpdateStats()
        {
            var m = (_sel != null) ? _sel.Mgr : null;
            bool on = m != null && m.Running;
            if (_pill.On != on) SetOnline(on);
            if (!on) return;
            try { m.SampleCpu(); } catch { }
            _cpuVal.Text = m.CpuPct.ToString("0.0") + " %";
            _memVal.Text = (m.RamMb / 1024.0).ToString("0.00") + " / 2 GiB";
            _upVal.Text = FmtUp(m.UptimeSec);
            _plVal.Text = m.Players.ToString();
        }

        private static string FmtUp(int sec)
        {
            if (sec < 0) sec = 0;
            int d = sec / 86400, h = (sec % 86400) / 3600, mn = (sec % 3600) / 60;
            if (d > 0) return d + "д " + h + "ч";
            if (h > 0) return h + "ч " + mn + "м";
            return mn + "м";
        }

        // ---------- мелочь ----------

        private void Ui(Action a)
        {
            try { if (IsHandleCreated) BeginInvoke(a); else a(); } catch { }
        }

        private void Append(string s)
        {
            if (_log.TextLength > 60000) _log.Text = _log.Text.Substring(_log.TextLength - 40000);
            _log.AppendText(s + "\r\n");
        }

        private static Label Title(string text, float size, FontStyle st, Color fg, Rectangle b)
        {
            return new Label { Text = text, Font = Theme.F(size, st), ForeColor = fg, BackColor = Color.Transparent, AutoSize = false, Bounds = b };
        }

        private static void StyleBtn(Button b, string text, Color bg, Color fg, Color border)
        {
            b.Text = text; b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = border; b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(bg, 0.15f);
            b.BackColor = bg; b.ForeColor = fg;
            b.Font = Theme.F(10f, FontStyle.Bold); b.Cursor = Cursors.Hand;
        }

        private Label StatCard(CardPanel card, string caption)
        {
            var cap = new Label
            {
                Text = caption, Font = Theme.F(8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(123, 132, 152),
                BackColor = Color.Transparent, AutoSize = false, Bounds = new Rectangle(15, 13, 220, 16)
            };
            var val = new Label
            {
                Text = "—", Font = Theme.F(15f, FontStyle.Bold), ForeColor = CText,
                BackColor = Color.Transparent, AutoSize = false, Bounds = new Rectangle(15, 36, 220, 26)
            };
            card.Controls.Add(cap); card.Controls.Add(val);
            return val;
        }

        // ---------- управление сервером (файлы/моды/плагины/настройки) ----------

        private void BuildManage()
        {
            foreach (var t in new[] { _tabConsole, _tabFiles, _tabJars, _tabProps })
            {
                t.FlatStyle = FlatStyle.Flat; t.FlatAppearance.BorderSize = 0;
                t.BackColor = CBg; t.ForeColor = CDim; t.Font = Theme.F(9.5f, FontStyle.Bold);
                t.Cursor = Cursors.Hand;
                Controls.Add(t);
            }
            _tabConsole.Text = "Консоль"; _tabFiles.Text = "Файлы"; _tabJars.Text = "Моды"; _tabProps.Text = "server.properties";
            _tabConsole.Click += (s, e) => ShowView("console");
            _tabFiles.Click += (s, e) => { ShowView("files"); RefreshFiles(); };
            _tabJars.Click += (s, e) => { ShowView("jars"); RefreshJars(); };
            _tabProps.Click += (s, e) => { ShowView("props"); LoadProps(); };

            _filesPanel.Visible = false; Controls.Add(_filesPanel);
            _filesList.BorderStyle = BorderStyle.None; _filesList.BackColor = CTermBg; _filesList.ForeColor = CText; _filesList.Font = new Font("Consolas", 9.5f);
            _filesPanel.Controls.Add(_filesList);
            _filesBtns = new[] { Mk("Открыть папку", OpenServerFolder), Mk("Обновить", RefreshFiles), Mk("Удалить", DeleteSelectedFile) };
            foreach (var b in _filesBtns) _filesPanel.Controls.Add(b);

            _jarsPanel.Visible = false; Controls.Add(_jarsPanel);
            _jarsTitle.AutoSize = false; _jarsTitle.BackColor = Color.Transparent; _jarsTitle.ForeColor = CDim; _jarsTitle.Font = Theme.F(9f);
            _jarsPanel.Controls.Add(_jarsTitle);
            _jarsSearch.BorderStyle = BorderStyle.FixedSingle; _jarsSearch.BackColor = Color.FromArgb(18, 18, 18); _jarsSearch.ForeColor = CText; _jarsSearch.Font = Theme.F(10f);
            _jarsSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SearchCatalog(); } };
            _jarsPanel.Controls.Add(_jarsSearch);
            _jarsFlow.AutoScroll = true; _jarsFlow.WrapContents = false; _jarsFlow.FlowDirection = FlowDirection.TopDown;
            _jarsFlow.BackColor = CTermBg; _jarsFlow.Padding = new Padding(6);
            _jarsPanel.Controls.Add(_jarsFlow);
            _jarsBtns = new[] { Mk("Искать", SearchCatalog), Mk("Папка", OpenJarsFolder), Mk("На сайте", OpenOnSite) };
            foreach (var b in _jarsBtns) _jarsPanel.Controls.Add(b);

            _propsPanel.Visible = false; Controls.Add(_propsPanel);
            _propsText.Multiline = true; _propsText.ScrollBars = ScrollBars.Both; _propsText.WordWrap = false;
            _propsText.BorderStyle = BorderStyle.None; _propsText.BackColor = CTermBg; _propsText.ForeColor = CText; _propsText.Font = new Font("Consolas", 9.5f);
            _propsPanel.Controls.Add(_propsText);
            _propsBtns = new[] { Mk("Сохранить", SaveProps), Mk("Перечитать", LoadProps) };
            foreach (var b in _propsBtns) _propsPanel.Controls.Add(b);
        }

        private Button Mk(string text, Action act)
        {
            var b = new Button();
            StyleBtn(b, text, CCard, CText, CBorder);
            b.Font = Theme.F(9f, FontStyle.Bold);
            b.Click += (s, e) => { try { act(); } catch (Exception ex) { MessageBox.Show(ex.Message); } };
            return b;
        }

        private void ShowView(string v)
        {
            _view = v;
            bool con = v == "console", fil = v == "files", jar = v == "jars", pr = v == "props";
            _termCard.Visible = con; _cmdCard.Visible = con; _send.Visible = con;
            _filesPanel.Visible = fil; _jarsPanel.Visible = jar; _propsPanel.Visible = pr;
            _tabConsole.ForeColor = con ? CText : CDim;
            _tabFiles.ForeColor = fil ? CText : CDim;
            _tabJars.ForeColor = jar ? CText : CDim;
            _tabProps.ForeColor = pr ? CText : CDim;
            Relayout();
        }

        private void LayoutFiles()
        {
            int w = _filesPanel.Width, x = 12;
            if (_filesBtns != null) foreach (var b in _filesBtns) { b.Bounds = new Rectangle(x, 10, 120, 28); x += 128; }
            _filesList.Bounds = new Rectangle(12, 48, w - 24, Math.Max(40, _filesPanel.Height - 60));
        }
        private void LayoutJars()
        {
            int w = _jarsPanel.Width, x = 12;
            _jarsTitle.Bounds = new Rectangle(12, 10, w - 24, 18);
            _jarsSearch.Bounds = new Rectangle(12, 32, w - 24, 28);
            if (_jarsBtns != null) foreach (var b in _jarsBtns) { b.Bounds = new Rectangle(x, 66, 118, 28); x += 126; }
            _jarsFlow.Bounds = new Rectangle(12, 102, w - 24, Math.Max(40, _jarsPanel.Height - 114));
            SizeCards();
        }
        private void SizeCards()
        {
            int cw = _jarsFlow.ClientSize.Width - 10;
            if (cw < 120) cw = 120;
            foreach (Control c in _jarsFlow.Controls) if (c is Panel) c.Width = cw;
        }
        private void LayoutProps()
        {
            int w = _propsPanel.Width, x = 12;
            if (_propsBtns != null) foreach (var b in _propsBtns) { b.Bounds = new Rectangle(x, 10, 120, 28); x += 128; }
            _propsText.Bounds = new Rectangle(12, 48, w - 24, Math.Max(40, _propsPanel.Height - 60));
        }

        private string[] ReadMeta(string folder)
        {
            try
            {
                string f = Path.Combine(folder, "nare_core.txt");
                if (File.Exists(f))
                {
                    var p = File.ReadAllText(f).Split('|');
                    return new[] { p.Length > 0 ? p[0].Trim() : "", p.Length > 1 ? p[1].Trim() : "" };
                }
            }
            catch { }
            return new[] { "", "" };
        }
        private string CoreOf(Entry e) { return e == null ? "" : ReadMeta(e.Folder)[0]; }
        private string JarsSub()
        {
            string core = CoreOf(_sel);
            if (core == "paper") return "plugins";
            if (core == "forge" || core == "fabric") return "mods";
            if (_sel != null)
            {
                if (Directory.Exists(Path.Combine(_sel.Folder, "plugins"))) return "plugins";
                if (Directory.Exists(Path.Combine(_sel.Folder, "mods"))) return "mods";
            }
            return "mods";
        }
        private void UpdateJarsLabel()
        {
            bool plugins = JarsSub() == "plugins";
            _tabJars.Text = plugins ? "Плагины" : "Моды";
            _jarsTitle.Text = plugins ? "Плагины (папка plugins) — для Paper" : "Моды (папка mods) — для Forge/Fabric";
        }
        private void RefreshActiveView()
        {
            if (_view == "files") RefreshFiles();
            else if (_view == "jars") RefreshJars();
            else if (_view == "props") LoadProps();
        }

        private void RefreshFiles()
        {
            _filesList.Items.Clear();
            if (_sel == null) { _filesList.Items.Add("(выбери сервер)"); return; }
            try
            {
                Directory.CreateDirectory(_sel.Folder);
                foreach (var d in Directory.GetDirectories(_sel.Folder)) _filesList.Items.Add("[папка] " + Path.GetFileName(d));
                foreach (var f in Directory.GetFiles(_sel.Folder)) _filesList.Items.Add(Path.GetFileName(f));
                if (_filesList.Items.Count == 0) _filesList.Items.Add("(папка пуста)");
            }
            catch (Exception ex) { _filesList.Items.Add("ошибка: " + ex.Message); }
        }
        private void OpenServerFolder()
        {
            if (_sel == null) return;
            Directory.CreateDirectory(_sel.Folder);
            Process.Start("explorer.exe", "\"" + _sel.Folder + "\"");
        }
        private void DeleteSelectedFile()
        {
            if (_sel == null || _filesList.SelectedItem == null) return;
            string name = _filesList.SelectedItem.ToString();
            if (name.StartsWith("(")) return;
            bool dir = name.StartsWith("[папка] ");
            if (dir) name = name.Substring("[папка] ".Length);
            string full = Path.Combine(_sel.Folder, name);
            if (MessageBox.Show("Удалить «" + name + "»?", "Удаление", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            if (dir) Directory.Delete(full, true); else File.Delete(full);
            RefreshFiles();
        }

        private void RefreshJars()
        {
            UpdateJarsLabel();
            SearchCatalog();
        }

        private void SearchCatalog()
        {
            if (_sel == null) { _jarsFlow.Controls.Clear(); return; }
            bool plugins = JarsSub() == "plugins";
            string core = CoreOf(_sel);
            string type = plugins ? "plugin" : "mod";
            string cat = plugins ? "paper" : (core == "fabric" ? "fabric" : (core == "forge" ? "forge" : ""));
            string ver = ReadMeta(_sel.Folder)[1];
            string q = (_jarsSearch.Text ?? "").Trim();
            _jarsTitle.Text = (plugins ? "Плагины для Paper" : "Моды для " + (cat == "" ? "сервера" : cat))
                            + (ver.Length > 0 ? "  ·  версия " + ver : "  ·  любая версия");
            _jarsFlow.Controls.Clear();
            _jarsFlow.Controls.Add(new Label { Text = "поиск в каталоге…", AutoSize = true, ForeColor = CDim, BackColor = Color.Transparent, Margin = new Padding(8) });
            new System.Threading.Thread(() =>
            {
                var res = ModrinthClient.Search(q, type, cat, ver);
                Ui(() =>
                {
                    _catalog = res;
                    _jarsFlow.Controls.Clear();
                    if (res.Count == 0)
                    {
                        _jarsFlow.Controls.Add(new Label { Text = "(ничего не найдено для этой версии)", AutoSize = true, ForeColor = CDim, BackColor = Color.Transparent, Margin = new Padding(8) });
                        return;
                    }
                    foreach (var pr in res) _jarsFlow.Controls.Add(BuildPluginCard(pr));
                    SizeCards();
                });
            })
            { IsBackground = true }.Start();
        }

        private Panel BuildPluginCard(ModProject pr)
        {
            var card = new Panel { Height = 80, BackColor = CCard, Margin = new Padding(0, 0, 0, 8) };

            var pic = new PictureBox { Dock = DockStyle.Left, Width = 80, SizeMode = PictureBoxSizeMode.Zoom, BackColor = CTermBg };
            card.Controls.Add(pic);

            var install = new Button { Dock = DockStyle.Right, Width = 120 };
            StyleBtn(install, "Установить", Color.FromArgb(18, 58, 34), CGreen, Color.FromArgb(28, 92, 53));
            install.Font = Theme.F(9.5f, FontStyle.Bold);
            var proj = pr;
            install.Click += (s, e) => InstallProject(proj);
            card.Controls.Add(install);

            var mid = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            var title = new Label { Text = pr.Title, Bounds = new Rectangle(12, 8, 400, 22), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, ForeColor = CText, BackColor = Color.Transparent, Font = Theme.F(11f, FontStyle.Bold) };
            var meta = new Label { Text = VideosPage.Nfmt(pr.Downloads) + " загрузок", Bounds = new Rectangle(12, 32, 400, 16), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, ForeColor = CAccent, BackColor = Color.Transparent, Font = Theme.F(8.5f, FontStyle.Bold) };
            var desc = new Label { Text = pr.Desc, Bounds = new Rectangle(12, 50, 400, 24), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, ForeColor = CDim, BackColor = Color.Transparent, Font = Theme.F(8.5f) };
            mid.Controls.Add(desc); mid.Controls.Add(meta); mid.Controls.Add(title);
            card.Controls.Add(mid);

            if (!string.IsNullOrEmpty(pr.IconUrl)) LoadIcon(pr.IconUrl, pic);
            return card;
        }

        private void LoadIcon(string url, PictureBox pic)
        {
            new System.Threading.Thread(() =>
            {
                var bytes = ModrinthClient.DownloadData(url);
                if (bytes == null) return;
                try
                {
                    var img = Image.FromStream(new MemoryStream(bytes));
                    Ui(() => { try { pic.Image = img; } catch { } });
                }
                catch { }
            })
            { IsBackground = true }.Start();
        }

        private void InstallProject(ModProject proj)
        {
            if (_sel == null || proj == null) return;
            bool plugins = JarsSub() == "plugins";
            string core = CoreOf(_sel);
            string[] loaders = plugins
                ? new[] { "paper", "spigot", "bukkit", "purpur", "folia" }
                : (core == "fabric" ? new[] { "fabric", "quilt" } : new[] { "forge", "neoforge" });
            string ver = ReadMeta(_sel.Folder)[1];
            string sub = Path.Combine(_sel.Folder, JarsSub());
            Append("Устанавливаю «" + proj.Title + "»...");
            ShowView("console");
            new System.Threading.Thread(() =>
            {
                try
                {
                    var dl = ModrinthClient.Resolve(proj.Slug, loaders, ver);
                    if (dl == null || string.IsNullOrEmpty(dl[0])) { Ui(() => Append("Нет совместимой версии «" + proj.Title + "».")); return; }
                    Directory.CreateDirectory(sub);
                    string fname = dl[1].Length > 0 ? dl[1] : proj.Slug + ".jar";
                    ModrinthClient.Download(dl[0], Path.Combine(sub, fname));
                    Ui(() => Append("Установлено: " + fname + "  →  " + JarsSub()));
                }
                catch (Exception ex) { Ui(() => Append("Не установилось: " + ex.Message)); }
            })
            { IsBackground = true }.Start();
        }


        private void OpenOnSite()
        {
            bool plugins = JarsSub() == "plugins";
            string q = (_jarsSearch.Text ?? "").Trim();
            string url = "https://modrinth.com/" + (plugins ? "plugins" : "mods");
            if (q.Length > 0) url += "?q=" + Uri.EscapeDataString(q);
            try { Process.Start(url); } catch { }
        }
        private void OpenJarsFolder()
        {
            if (_sel == null) return;
            string sub = Path.Combine(_sel.Folder, JarsSub());
            Directory.CreateDirectory(sub);
            Process.Start("explorer.exe", "\"" + sub + "\"");
        }

        private void LoadProps()
        {
            if (_sel == null) { _propsText.Text = ""; return; }
            string f = Path.Combine(_sel.Folder, "server.properties");
            try
            {
                _propsText.Text = File.Exists(f)
                    ? File.ReadAllText(f).Replace("\n", "\r\n").Replace("\r\r\n", "\r\n")
                    : "# server.properties создастся при первом запуске сервера.\r\n# Можно задать настройки заранее и сохранить — лаунчер их применит\r\n# (кроме server-port и online-mode, они ставятся автоматически).\r\n";
            }
            catch (Exception ex) { _propsText.Text = "ошибка чтения: " + ex.Message; }
        }
        private void SaveProps()
        {
            if (_sel == null) return;
            string f = Path.Combine(_sel.Folder, "server.properties");
            Directory.CreateDirectory(_sel.Folder);
            File.WriteAllText(f, _propsText.Text.Replace("\r\n", "\n"));
            Append("server.properties сохранён.");
        }

        // ---------- кастомные контролы ----------

        private static GraphicsPath Round(Rectangle r, int rad)
        {
            var gp = new GraphicsPath();
            int d = rad * 2;
            if (d > r.Height) d = r.Height;
            if (d > r.Width) d = r.Width;
            gp.AddArc(r.X, r.Y, d, d, 180, 90);
            gp.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            gp.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            gp.CloseFigure();
            return gp;
        }

        private class CardPanel : Panel
        {
            public int Radius = 14;
            public Color Border = CBorder;
            public CardPanel() { DoubleBuffered = true; BackColor = CCard; }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, Width - 1, Height - 1);
                using (var gp = Round(r, Radius))
                using (var b = new SolidBrush(BackColor))
                using (var p = new Pen(Border))
                { e.Graphics.FillPath(b, gp); e.Graphics.DrawPath(p, gp); }
            }
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
                var r = new Rectangle(0, 0, Width - 1, Height - 1);
                using (var gp = Round(r, Height / 2))
                using (var b = new SolidBrush(bg)) g.FillPath(b, gp);
                using (var b = new SolidBrush(fg)) g.FillEllipse(b, 11, Height / 2 - 4, 8, 8);
                TextRenderer.DrawText(g, On ? "Онлайн" : "Оффлайн", Font,
                    new Rectangle(25, 0, Width - 25, Height), fg,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
        }
    }
}
