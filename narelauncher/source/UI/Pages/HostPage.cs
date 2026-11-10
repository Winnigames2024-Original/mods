using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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

            LoadList();
            SetOnline(false);

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

            int ty = by + 52, cmdY = Height - 54;
            if (cmdY < ty + 120) cmdY = ty + 120;
            _termCard.Bounds = new Rectangle(m, ty, right - m, cmdY - ty - 12);
            _termBar.Bounds = new Rectangle(14, 8, _termCard.Width - 28, 20);
            _log.Bounds = new Rectangle(12, 32, _termCard.Width - 24, _termCard.Height - 42);

            _send.Bounds = new Rectangle(right - 110, cmdY, 110, 40);
            _cmdCard.Bounds = new Rectangle(m, cmdY, _send.Left - m - 10, 40);
            _cmd.Bounds = new Rectangle(14, 11, _cmdCard.Width - 28, 20);
        }

        // ---------- данные ----------

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

        private void DownloadJar()
        {
            if (_sel == null) { Append("Сначала создай или выбери сервер."); return; }
            string ver = InputPrompt.Ask(FindForm(), "Версия Minecraft (напр. 1.21.4):", "1.21.4");
            if (string.IsNullOrEmpty(ver)) return;
            var r = MessageBox.Show("Да — Paper (плагины, быстрее).\nНет — ванильный сервер Mojang.",
                "Тип сервера", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            string type = r == DialogResult.Yes ? "paper" : "vanilla";
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
            public PillLabel() { DoubleBuffered = true; BackColor = Color.Transparent; }
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
