using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI.Pages
{
    public class SettingsPage : PageBase
    {
        private readonly Panel _scroll = new Panel { AutoScroll = true, BackColor = Theme.Bg };

        private readonly NareTextBox _gameDir = new NareTextBox { Placeholder = "Папка .minecraft", Icon = Ico.Folder };
        private readonly NareButton _browseDir = new NareButton { Text = "Обзор", Style = BtnStyle.Ghost, Size = new Size(110, 38) };

        private readonly NareDropDown _java = new NareDropDown();
        private readonly NareButton _browseJava = new NareButton { Text = "Указать javaw.exe", Style = BtnStyle.Ghost, Size = new Size(190, 40) };
        private readonly NareButton _downloadJava = new NareButton { Text = "Скачать Java 21", Style = BtnStyle.Ghost, Icon = Ico.Download, Size = new Size(190, 40) };

        private readonly NareSlider _ram = new NareSlider { Minimum = 1024, Maximum = 16384, Step = 256 };
        private readonly NareLabel _ramLabel = new NareLabel("", 10f, FontStyle.Bold);

        private readonly NareTextBox _jvmArgs = new NareTextBox { Placeholder = "Дополнительные аргументы JVM" };

        private readonly NareTextBox _winW = new NareTextBox();
        private readonly NareTextBox _winH = new NareTextBox();
        private readonly NareToggle _fullscreen = new NareToggle();

        private readonly NareToggle _isolate = new NareToggle();
        private readonly NareToggle _closeOnLaunch = new NareToggle();
        private readonly NareToggle _verify = new NareToggle();
        private readonly NareToggle _demo = new NareToggle();
        private readonly NareToggle _autoVisuals = new NareToggle();
        private readonly NareToggle _gitVisuals = new NareToggle();
        private readonly NareButton _visualsVersion = new NareButton
        {
            Text = "Версия визуалов…", Style = BtnStyle.Ghost, Size = new Size(230, 38)
        };

        private readonly NareSlider _threads = new NareSlider { Minimum = 1, Maximum = 32, Step = 1 };
        private readonly NareLabel _threadsLabel = new NareLabel("", 10f, FontStyle.Bold);

        private readonly NareTextBox _cfKey = new NareTextBox { Placeholder = "Ключ API CurseForge (если нужен их каталог)" };
        private readonly NareTextBox _localFolder = new NareTextBox { Placeholder = "Папка со своими скачанными модами", Icon = Ico.Folder };

        private readonly NareTextBox _onJoin = new NareTextBox
        { Placeholder = "Команды при входе, через ; — например /login 1234; /spawn" };

        private readonly NareTextBox _netHost = new NareTextBox
        { Placeholder = "Сборные пункты «Вместе», через ; — 166.1.161.200:2445;151.245.217.104:2445" };
        private readonly NareButton _browseLocal = new NareButton { Text = "Обзор", Style = BtnStyle.Ghost, Size = new Size(110, 38) };

        private readonly NareButton _save = new NareButton { Text = "Сохранить", Style = BtnStyle.Accent, Icon = Ico.Check, Size = new Size(180, 42) };
        private readonly NareButton _openLauncher = new NareButton { Text = "Папка лаунчера", Style = BtnStyle.Ghost, Icon = Ico.Folder, Size = new Size(190, 42) };
        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);

        private List<JavaInstall> _javas = new List<JavaInstall>();
        private readonly List<NareLabel> _captions = new List<NareLabel>();

        public SettingsPage()
        {
            Padding = new Padding(0);

            Controls.Add(_scroll);

            AddCaption("Папка игры", "Здесь будут versions, libraries, assets и сборки");
            _scroll.Controls.Add(_gameDir);
            _scroll.Controls.Add(_browseDir);

            AddCaption("Java", "Пусто = лаунчер сам подберёт Java под версию");
            _scroll.Controls.Add(_java);
            _scroll.Controls.Add(_browseJava);
            _scroll.Controls.Add(_downloadJava);

            AddCaption("Оперативная память", "Сколько выделять игре");
            _scroll.Controls.Add(_ram);
            _scroll.Controls.Add(_ramLabel);

            AddCaption("Аргументы JVM", "Для опытных пользователей");
            _scroll.Controls.Add(_jvmArgs);

            AddCaption("Окно игры", "Разрешение при запуске");
            _scroll.Controls.Add(_winW);
            _scroll.Controls.Add(_winH);
            _scroll.Controls.Add(_fullscreen);

            AddCaption("Поведение", null);
            _scroll.Controls.Add(_isolate);
            _scroll.Controls.Add(_closeOnLaunch);
            _scroll.Controls.Add(_verify);
            _scroll.Controls.Add(_demo);
            _scroll.Controls.Add(_autoVisuals);
            _scroll.Controls.Add(_gitVisuals);
            _scroll.Controls.Add(_visualsVersion);

            _visualsVersion.Click += delegate
            {
                using (var dlg = new VisualsVersionForm(Settings.Current.VisualsVersion))
                {
                    if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

                    Settings.Current.VisualsVersion = dlg.Result;
                    Settings.Save();
                    UpdateVisualsVersionText();
                    Invalidate();
                }
            };

            UpdateVisualsVersionText();

            AddCaption("Загрузка", "Количество одновременных потоков");
            _scroll.Controls.Add(_threads);
            _scroll.Controls.Add(_threadsLabel);

            AddCaption("Источники модов", "Откуда брать моды на вкладке «Моды и паки»");
            _scroll.Controls.Add(_cfKey);
            _scroll.Controls.Add(_localFolder);
            _scroll.Controls.Add(_onJoin);
            _scroll.Controls.Add(_netHost);
            _scroll.Controls.Add(_browseLocal);

            _scroll.Controls.Add(_save);
            _scroll.Controls.Add(_openLauncher);
            _scroll.Controls.Add(_status);

            _ram.ValueChanged += (s, e) => _ramLabel.Text = _ram.Value + " МБ";
            _threads.ValueChanged += (s, e) => _threadsLabel.Text = _threads.Value.ToString();

            _browseDir.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "Выберите папку для файлов игры";
                    dlg.SelectedPath = Paths.GameRoot;
                    if (dlg.ShowDialog(Host) == DialogResult.OK) _gameDir.Text = dlg.SelectedPath;
                }
            };

            _browseLocal.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "Папка со своими модами";
                    if (dlg.ShowDialog(Host) == DialogResult.OK) _localFolder.Text = dlg.SelectedPath;
                }
            };

            _browseJava.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Title = "Выберите javaw.exe";
                    dlg.Filter = "javaw.exe|javaw.exe|java.exe|java.exe|Все файлы (*.*)|*.*";
                    if (dlg.ShowDialog(Host) != DialogResult.OK) return;

                    Settings.Current.JavaPath = dlg.FileName;
                    Settings.Save();
                    ReloadJava();
                    _status.Text = "Java задана вручную.";
                }
            };

            _downloadJava.Click += DownloadJavaClick;

            _java.SelectedIndexChanged += (s, e) =>
            {
                int i = _java.SelectedIndex;
                if (i <= 0) Settings.Current.JavaPath = "";
                else if (i - 1 < _javas.Count) Settings.Current.JavaPath = _javas[i - 1].Path;
            };

            _save.Click += (s, e) => { Apply(); _status.Text = "Настройки сохранены."; };
            _openLauncher.Click += (s, e) =>
            {
                try { Process.Start("explorer.exe", "\"" + Paths.LauncherDir + "\""); }
                catch (Exception ex) { Host.ShowError("Не удалось открыть папку", ex); }
            };

            _isolate.CheckedChanged += (s, e) =>
            {
                Settings.Current.IsolateVersions = _isolate.Checked;
                Settings.Save();
            };
        }

        private void AddCaption(string title, string hint)
        {
            var t = new NareLabel(title, 11.5f, FontStyle.Bold) { Tag = "caption" };
            _scroll.Controls.Add(t);
            _captions.Add(t);

            var h = new NareLabel(hint ?? "", 8.8f, FontStyle.Regular, Theme.Dim) { Tag = "hint" };
            _scroll.Controls.Add(h);
            _captions.Add(h);
        }

        public override void PageShown()
        {
            Load1();
            Layout1();
        }

        private void Load1()
        {
            var s = Settings.Current;
            _gameDir.Text = Paths.GameRoot;
            _ram.Maximum = Math.Max(4096, TotalRamMb());
            _ram.Value = s.RamMb;
            _ramLabel.Text = s.RamMb + " МБ";
            _jvmArgs.Text = s.JvmArgs;
            _winW.Text = s.WindowWidth.ToString();
            _winH.Text = s.WindowHeight.ToString();
            _fullscreen.Checked = s.Fullscreen;
            _isolate.Checked = s.IsolateVersions;
            _closeOnLaunch.Checked = s.CloseOnLaunch;
            _verify.Checked = s.VerifyHashes;
            _demo.Checked = s.DemoMode;
            _autoVisuals.Checked = s.AutoUpdateVisuals;
            _gitVisuals.Checked = s.UpdateFromGitHub;
            _threads.Value = s.DownloadThreads;
            _threadsLabel.Text = s.DownloadThreads.ToString();
            _cfKey.Text = s.CurseForgeKey;
            _localFolder.Text = s.LocalModsFolder;
            _onJoin.Text = s.JoinCommands;
            _netHost.Text = s.NareNetHosts;
            ReloadJava();
        }

        private void ReloadJava()
        {
            _javas = JavaLocator.FindAll(true);
            var items = new List<object> { "Автоматически (рекомендуется)" };
            foreach (var j in _javas) items.Add(j.ToString());

            object select = items[0];
            if (!string.IsNullOrEmpty(Settings.Current.JavaPath))
            {
                foreach (var j in _javas)
                {
                    if (string.Equals(j.Path, Settings.Current.JavaPath, StringComparison.OrdinalIgnoreCase))
                    {
                        select = j.ToString();
                        break;
                    }
                }
                if (ReferenceEquals(select, items[0]))
                {
                    items.Add(Settings.Current.JavaPath);
                    select = Settings.Current.JavaPath;
                }
            }

            _java.SetItems(items, select);
        }

        private async void DownloadJavaClick(object sender, EventArgs e)
        {
            _downloadJava.Enabled = false;
            try
            {
                _status.Text = "Скачиваю Java 21…";
                var progress = new Progress<InstallStatus>(s => { _status.Text = s.Text; });
                var java = await JavaLocator.DownloadJavaAsync(21, progress, CancellationToken.None);
                Settings.Current.JavaPath = java.Path;
                Settings.Save();
                ReloadJava();
                _status.Text = "Java установлена: " + java.Path;
            }
            catch (Exception ex)
            {
                Host.ShowError("Не удалось скачать Java", ex);
                _status.Text = "Ошибка: " + ex.Message;
            }
            finally { _downloadJava.Enabled = true; }
        }

        private void Apply()
        {
            var s = Settings.Current;

            string dir = (_gameDir.Text ?? "").Trim();
            if (dir.Length > 0) s.GameRoot = dir;

            s.RamMb = _ram.Value;
            s.JvmArgs = _jvmArgs.Text ?? "";
            s.Fullscreen = _fullscreen.Checked;
            s.IsolateVersions = _isolate.Checked;
            s.CloseOnLaunch = _closeOnLaunch.Checked;
            s.VerifyHashes = _verify.Checked;
            s.DemoMode = _demo.Checked;
            s.AutoUpdateVisuals = _autoVisuals.Checked;
            s.UpdateFromGitHub = _gitVisuals.Checked;
            s.DownloadThreads = _threads.Value;
            s.CurseForgeKey = (_cfKey.Text ?? "").Trim();
            s.LocalModsFolder = (_localFolder.Text ?? "").Trim();
            s.JoinCommands = (_onJoin.Text ?? "").Trim();

            string net = (_netHost.Text ?? "").Trim();
            s.NareNetHosts = net.Length == 0
                ? "166.1.161.200:2445;151.245.217.104:2445" : net;

            // выбранный адрес должен остаться в списке, иначе выбираем первый
            var known = NareNet.Known();
            if (known.Count > 0 && !known.Contains(s.NareNetHost)) s.NareNetHost = known[0];

            int w, h;
            if (int.TryParse((_winW.Text ?? "").Trim(), out w) && w >= 320) s.WindowWidth = w;
            if (int.TryParse((_winH.Text ?? "").Trim(), out h) && h >= 240) s.WindowHeight = h;

            Settings.Save();
            Paths.EnsureAll();
            Host.Home.ReloadVersions();
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MemoryStatusEx
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

        private static int TotalRamMb()
        {
            try
            {
                var st = new MemoryStatusEx();
                st.dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(MemoryStatusEx));
                if (GlobalMemoryStatusEx(ref st))
                {
                    long mb = (long)(st.ullTotalPhys / (1024UL * 1024UL));
                    return (int)Math.Max(2048, Math.Min(65536, mb));
                }
            }
            catch { }
            return 8192;
        }

        // ---------------------------------------------------------------- вёрстка

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Layout1();
        }

        private void Layout1()
        {
            _scroll.SetBounds(0, 64, Width, Math.Max(100, Height - 64));

            int pad = 28;
            int w = Math.Max(320, Width - pad * 2 - 24);
            int y = 6;
            int ci = 0;

            Action<int> caption = extra =>
            {
                if (ci + 1 >= _captions.Count) return;
                _captions[ci].SetBounds(pad, y, 420, 22);
                _captions[ci + 1].SetBounds(pad, y + 22, 620, 18);
                y += string.IsNullOrEmpty(_captions[ci + 1].Text) ? 28 : 46;
                ci += 2;
                y += extra;
            };

            // Папка игры
            caption(0);
            _gameDir.SetBounds(pad, y, Math.Max(200, w - 124), 38);
            _browseDir.Location = new Point(pad + Math.Max(200, w - 124) + 12, y);
            y += 60;

            // Java
            caption(0);
            _java.SetBounds(pad, y, Math.Max(240, w - 400), 40);
            _browseJava.Location = new Point(pad + Math.Max(240, w - 400) + 10, y);
            _downloadJava.Location = new Point(pad + Math.Max(240, w - 400) + 210, y);
            y += 62;

            // Память
            caption(0);
            _ram.SetBounds(pad, y, Math.Max(200, w - 160), 28);
            _ramLabel.SetBounds(pad + Math.Max(200, w - 160) + 16, y, 140, 28);
            y += 50;

            // JVM
            caption(0);
            _jvmArgs.SetBounds(pad, y, w, 38);
            y += 60;

            // Окно
            caption(0);
            _winW.SetBounds(pad, y, 110, 38);
            _winH.SetBounds(pad + 122, y, 110, 38);
            _fullscreen.Location = new Point(pad + 256, y + 6);
            y += 60;

            // Поведение
            caption(0);
            _isolate.Location = new Point(pad, y);
            _closeOnLaunch.Location = new Point(pad, y + 38);
            _verify.Location = new Point(pad, y + 76);
            _demo.Location = new Point(pad, y + 114);
            _autoVisuals.Location = new Point(pad, y + 152);
            _gitVisuals.Location = new Point(pad, y + 190);
            _visualsVersion.Location = new Point(pad, y + 226);
            y += 284;

            // Загрузка
            caption(0);
            _threads.SetBounds(pad, y, Math.Max(200, w - 160), 28);
            _threadsLabel.SetBounds(pad + Math.Max(200, w - 160) + 16, y, 80, 28);
            y += 56;

            // Источники модов
            caption(0);
            _cfKey.SetBounds(pad, y, w, 38);
            y += 48;
            _localFolder.SetBounds(pad, y, Math.Max(200, w - 124), 38);
            _browseLocal.Location = new Point(pad + Math.Max(200, w - 124) + 12, y);
            y += 48;

            _onJoin.SetBounds(pad, y, w, 38);
            y += 48;

            _netHost.SetBounds(pad, y, w, 38);
            y += 58;

            _save.Location = new Point(pad, y);
            _openLauncher.Location = new Point(pad + 196, y);
            _status.SetBounds(pad, y + 52, Math.Max(100, w), 20);

            Invalidate();
            _scroll.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.Smooth(g);
            Theme.Text1(g, "Настройки", Theme.F(17f, FontStyle.Bold), Theme.Text,
                new Rectangle(28, 16, 400, 28));
            Theme.Text1(g, "Папки, Java, память и поведение лаунчера",
                Theme.F(9.5f), Theme.Muted, new Rectangle(28, 40, 700, 20));
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();

            _scroll.Paint += (s, e) =>
            {
                var g = e.Graphics;
                Theme.Smooth(g);
                DrawToggleLabel(g, _fullscreen, "Полный экран");
                DrawToggleLabel(g, _isolate, "Отдельная папка и свои моды для каждой версии");
                DrawToggleLabel(g, _closeOnLaunch, "Закрывать лаунчер после запуска игры");
                DrawToggleLabel(g, _verify, "Проверять файлы по SHA-1 (медленнее, но надёжнее)");
                DrawToggleLabel(g, _demo, "Демо-режим");
                DrawToggleLabel(g, _autoVisuals, "Автообновление мода NareVisuals при запуске");
                DrawToggleLabel(g, _gitVisuals, "Брать свежий мод с GitHub, а не только встроенный");

                string pin = (Settings.Current.VisualsVersion ?? "").Trim();
                Theme.Text1(g, pin.Length == 0 ? "сейчас: всегда последняя" : "сейчас закреплена " + pin,
                    Theme.F(9f), Theme.Muted,
                    new Rectangle(_visualsVersion.Right + 14, _visualsVersion.Top, 420, _visualsVersion.Height));
            };
        }

        private void UpdateVisualsVersionText()
        {
            string pin = (Settings.Current.VisualsVersion ?? "").Trim();
            _visualsVersion.Text = pin.Length == 0 ? "Версия визуалов: последняя" : "Версия визуалов: " + pin;
        }

        private void DrawToggleLabel(Graphics g, NareToggle toggle, string text)
        {
            var r = new Rectangle(toggle.Right + 14, toggle.Top - 2, 620, toggle.Height + 4);
            Theme.Text1(g, text, Theme.F(9.8f), Theme.Muted, r);
        }
    }
}
