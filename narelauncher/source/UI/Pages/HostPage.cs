using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// «Мои серверы» — выделенные серверы игрока на его ПК. Лаунчер запускает
    /// server.jar, держит тоннель до NareNet, и сервер доступен по адресу
    /// «ник-id.narelauncher.winnigames2024.ru».
    /// </summary>
    public class HostPage : PageBase
    {
        private const string DOMAIN = "narelauncher.winnigames2024.ru";

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
        private readonly NareButton _create = new NareButton();
        private readonly NareLabel _addr = new NareLabel();
        private readonly NareButton _start = new NareButton();
        private readonly NareButton _stop = new NareButton();
        private readonly NareButton _dl = new NareButton();
        private readonly TextBox _log = new TextBox();
        private readonly NareTextBox _cmd = new NareTextBox();
        private readonly NareButton _send = new NareButton();

        public HostPage()
        {
            BackColor = Theme.Bg;

            Controls.Add(new NareLabel
            {
                Text = "Мои серверы",
                Font = Theme.F(15f, FontStyle.Bold),
                ForeColor = Theme.Text,
                AutoSize = false,
                Bounds = new Rectangle(24, 18, 400, 28)
            });
            Controls.Add(new NareLabel
            {
                Text = "Запусти свой сервер на этом ПК — он будет доступен по адресу-поддомену.",
                Font = Theme.F(9.5f),
                ForeColor = Theme.Dim,
                AutoSize = false,
                Bounds = new Rectangle(24, 46, 700, 20)
            });

            _list.DropDownStyle = ComboBoxStyle.DropDownList;
            _list.FlatStyle = FlatStyle.Flat;
            _list.BackColor = Theme.Panel;
            _list.ForeColor = Theme.Text;
            _list.Bounds = new Rectangle(24, 76, 300, 26);
            _list.SelectedIndexChanged += (s, e) => SelectIndex(_list.SelectedIndex);
            Controls.Add(_list);

            _create.Style = BtnStyle.Accent;
            _create.Text = "Создать сервер";
            _create.Bounds = new Rectangle(332, 75, 150, 28);
            _create.Click += (s, e) => CreateServer();
            Controls.Add(_create);

            _addr.Font = Theme.F(10.5f, FontStyle.Bold);
            _addr.ForeColor = Theme.Accent2;
            _addr.AutoSize = false;
            _addr.Bounds = new Rectangle(24, 112, 720, 22);
            Controls.Add(_addr);

            _start.Style = BtnStyle.Accent;
            _start.Text = "Запустить";
            _start.Bounds = new Rectangle(24, 140, 120, 32);
            _start.Click += (s, e) => StartSel();
            Controls.Add(_start);

            _stop.Style = BtnStyle.Subtle;
            _stop.Text = "Остановить";
            _stop.Bounds = new Rectangle(152, 140, 120, 32);
            _stop.Click += (s, e) => StopSel();
            Controls.Add(_stop);

            _dl.Style = BtnStyle.Subtle;
            _dl.Text = "Скачать jar";
            _dl.Bounds = new Rectangle(280, 140, 130, 32);
            _dl.Click += (s, e) => DownloadJar();
            Controls.Add(_dl);

            _log.Multiline = true;
            _log.ReadOnly = true;
            _log.ScrollBars = ScrollBars.Vertical;
            _log.BackColor = Color.FromArgb(10, 12, 16);
            _log.ForeColor = Color.FromArgb(180, 190, 205);
            _log.BorderStyle = BorderStyle.FixedSingle;
            _log.Font = new Font("Consolas", 9f);
            _log.Bounds = new Rectangle(24, 184, 720, 300);
            Controls.Add(_log);

            _cmd.Bounds = new Rectangle(24, 492, 600, 34);
            _cmd.Inner.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SendCmd(); } };
            Controls.Add(_cmd);

            _send.Style = BtnStyle.Accent;
            _send.Text = "Ввод";
            _send.Bounds = new Rectangle(632, 492, 110, 34);
            _send.Click += (s, e) => SendCmd();
            Controls.Add(_send);

            LoadList();
            Resize += (s, e) => Relayout();
        }

        public override void PageShown() { Relayout(); }

        private void Relayout()
        {
            int w = Math.Max(500, Width);
            _log.Width = w - 48;
            _cmd.Width = w - 48 - 120;
            _send.Left = _cmd.Right + 8;
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
        }

        private void RefreshList()
        {
            _list.Items.Clear();
            foreach (var e in _servers) _list.Items.Add(e.Id);
            if (_servers.Count > 0) _list.SelectedIndex = 0;
            else { _sel = null; _addr.Text = "Серверов пока нет — создай первый."; }
        }

        private void SelectIndex(int i)
        {
            if (i < 0 || i >= _servers.Count) { _sel = null; return; }
            _sel = _servers[i];
            _addr.Text = "Адрес: " + Nick + "-" + _sel.Id + "." + DOMAIN + "   (локальный порт " + _sel.Port + ")";
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
            Append("Положи серверный jar как server.jar в папку:");
            Append("  " + folder);
            Append("Потом жми «Запустить».");
        }

        private void StartSel()
        {
            if (_sel == null) { Append("Сначала создай или выбери сервер."); return; }
            if (_sel.Mgr != null && _sel.Mgr.Running) { Append("Уже запущен."); return; }
            try
            {
                var entry = _sel;
                entry.Host = new NareHost(Nick + "-" + entry.Id, entry.Port);
                entry.Host.OnLog = s => Ui(() => Append("[тоннель] " + s));
                entry.Host.OnCommand = s => Ui(() =>
                {
                    // команда из веб-консоли — выполняем в stdin сервера
                    if (entry.Mgr != null && entry.Mgr.Running) { entry.Mgr.Send(s); Append("[web] > " + s); }
                });

                entry.Mgr = new ServerManager(entry.Folder, entry.Port);
                entry.Mgr.OnLine = s =>
                {
                    Ui(() => Append(s));
                    if (entry.Host != null) entry.Host.PushLog(s);   // в веб-консоль
                };
                entry.Mgr.OnExit = () => Ui(() => { Append("Сервер остановлен."); StopHost(entry); });
                entry.Mgr.Start();
                entry.Host.Start();

                Append("Запуск... адрес: " + Nick + "-" + entry.Id + "." + DOMAIN);
                Append("Веб-консоль: http://" + DOMAIN + "/server?name=" + Nick + "-" + entry.Id);
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

        private void StopSel() { if (_sel != null) StopHost(_sel); }

        private void StopHost(Entry e)
        {
            try { if (e.Host != null) { e.Host.Stop(); e.Host = null; } } catch { }
            try { if (e.Mgr != null) { e.Mgr.Stop(); } } catch { }
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

        private void Ui(Action a)
        {
            try { if (IsHandleCreated) BeginInvoke(a); else a(); } catch { }
        }

        private void Append(string s)
        {
            if (_log.TextLength > 60000) _log.Text = _log.Text.Substring(_log.TextLength - 40000);
            _log.AppendText(s + "\r\n");
        }
    }
}
