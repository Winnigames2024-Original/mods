using System.Diagnostics;
using System.ServiceProcess;
using Microsoft.Win32;

namespace GeoDashSetup
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // --service <папка> — запуск службой Windows, окон не показываем
            if (args.Length > 0 && args[0] == "--service")
            {
                string dir = args.Length > 1 ? args[1] : Cfg.DefaultDir;
                ServiceBase.Run(new UpdateService(dir));
                return;
            }

            // --update <папка> — разовая проверка обновления (её зовёт планировщик или человек)
            if (args.Length > 0 && args[0] == "--update")
            {
                string dir = args.Length > 1 ? args[1] : Cfg.DefaultDir;
                UpdateService.TickAsync(dir).GetAwaiter().GetResult();
                return;
            }

            // autoupdate.exe — отдельная качалка: сверяет version.txt с update.json
            string self = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "");
            if (self.Equals("autoupdate", StringComparison.OrdinalIgnoreCase))
            {
                string dir = args.Length > 0 && Directory.Exists(args[0])
                    ? args[0]
                    : Path.GetDirectoryName(Environment.ProcessPath!)!;
                ApplicationConfiguration.Initialize();
                Application.Run(new UpdateWindow(dir));
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new SetupForm(args.Contains("--uninstall")));
        }
    }

    /// <summary>Окно установщика: куда ставить, ярлыки, служба автообновления.</summary>
    public class SetupForm : Form
    {
        static readonly Color Deep = Color.FromArgb(14, 12, 34);
        static readonly Color Paper = Color.FromArgb(240, 244, 255);
        static readonly Color Blue = Color.FromArgb(40, 125, 255);

        readonly TextBox _dir, _key, _login, _pass;
        readonly CheckBox _byAccount;
        readonly Button _pick;
        readonly CheckBox _desktop, _menu, _service, _run, _assoc, _offline;
        readonly ProgressBar _bar;
        readonly Label _status;
        readonly Button _go, _remove;
        bool _busy;

        public SetupForm(bool uninstallMode)
        {
            Text = Cfg.AppName + " — установка";
            ClientSize = new Size(640, 584);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Deep;
            ForeColor = Paper;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            Label L(string t, int x, int y, int w, float size = 9.5f, bool bold = false)
            {
                var l = new Label
                {
                    Text = t, Left = x, Top = y, Width = w, AutoSize = false, Height = 22,
                    ForeColor = Paper, BackColor = Color.Transparent,
                    Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular)
                };
                Controls.Add(l);
                return l;
            }

            L("GEO DASH", 28, 24, 400, 22f, true).Height = 40;
            L("игра и автообновление · " + Cfg.Brand, 30, 66, 400).ForeColor = Color.FromArgb(150, 190, 255);

            L("Папка установки", 28, 112, 300, 10f, true);
            _dir = new TextBox
            {
                Left = 28, Top = 136, Width = 480, Text = Installed() ?? Cfg.DefaultDir,
                BackColor = Color.FromArgb(22, 20, 52), ForeColor = Paper, BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_dir);

            var browse = new Button { Text = "Обзор…", Left = 516, Top = 134, Width = 96, Height = 26, FlatStyle = FlatStyle.Flat };
            browse.Click += (s, e) =>
            {
                using var d = new FolderBrowserDialog { SelectedPath = _dir.Text };
                if (d.ShowDialog() == DialogResult.OK) _dir.Text = Path.Combine(d.SelectedPath, Cfg.AppName);
            };
            Controls.Add(browse);

            CheckBox C(string t, int y, bool on)
            {
                var c = new GdCheck
                {
                    Text = t, Left = 28, Top = y, Width = 560, Height = 24, Checked = on,
                    ForeColor = Paper
                };
                Controls.Add(c);
                return c;
            }

            _desktop = C("Ярлык на рабочем столе", 176, true);
            _menu = C("Ярлык в меню «Пуск»", 200, true);
            _assoc = C("Открывать файлы уровней .gdl этой игрой", 224, true);
            _service = C("Служба автообновления: сама скачивает новые версии", 248, true);
            _run = C("Запустить игру после установки", 272, true);

            // по умолчанию всегда качаем свежую версию с сервера
            _offline = C("Ставить из файла рядом с установщиком (если нет интернета)", 296, false);
            _offline.ForeColor = Color.FromArgb(255, 215, 120);
            _offline.Enabled = HasLocalGame();

            L("Лицензионный ключ", 28, 330, 300, 10f, true);
            L("нужен, чтобы скачать и запустить игру — проверяется на сервере",
              200, 332, 412).ForeColor = Color.FromArgb(150, 190, 255);

            _key = new TextBox
            {
                Left = 28, Top = 354, Width = 584, Text = Cfg.ReadMarkKey(Installed() ?? Cfg.DefaultDir),
                BackColor = Color.FromArgb(22, 20, 52), ForeColor = Paper,
                BorderStyle = BorderStyle.FixedSingle, CharacterCasing = CharacterCasing.Upper,
                TextAlign = HorizontalAlignment.Center, MaxLength = 24,
                Font = new Font("Consolas", 13f, FontStyle.Bold)
            };
            _key.TextChanged += (s, e) =>
            {
                string pretty = Lic.Pretty(_key.Text);
                if (pretty != _key.Text)
                {
                    int tail = _key.Text.Length - _key.SelectionStart;
                    _key.Text = pretty;
                    _key.SelectionStart = Math.Max(0, pretty.Length - Math.Max(0, tail));
                }
                _key.ForeColor = _key.Text.Length == 0 || Lic.WellFormed(_key.Text)
                    ? Paper : Color.FromArgb(255, 150, 150);
            };
            Controls.Add(_key);

            _byAccount = C("Войти в аккаунт вместо ключа — подберу свободный ключ сам", 386, false);
            _byAccount.ForeColor = Color.FromArgb(150, 220, 255);

            TextBox F(int x, int w, string hint, bool secret)
            {
                var t = new TextBox
                {
                    Left = x, Top = 412, Width = w, Visible = false,
                    BackColor = Color.FromArgb(22, 20, 52), ForeColor = Paper,
                    BorderStyle = BorderStyle.FixedSingle, PlaceholderText = hint,
                    UseSystemPasswordChar = secret, Font = new Font("Segoe UI", 10f)
                };
                Controls.Add(t);
                return t;
            }

            _login = F(28, 214, "логин на сайте", false);
            _pass = F(250, 184, "пароль", true);

            _pick = new Button
            {
                Text = "НАЙТИ КЛЮЧ", Left = 444, Top = 411, Width = 168, Height = 25,
                FlatStyle = FlatStyle.Flat, Visible = false,
                BackColor = Color.FromArgb(40, 90, 170), ForeColor = Paper,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            _pick.Click += async (s, e) => await PickKeyAsync();
            Controls.Add(_pick);

            _byAccount.CheckedChanged += (s, e) =>
            {
                bool on = _byAccount.Checked;
                _login.Visible = _pass.Visible = _pick.Visible = on;
                _key.Enabled = !on || _key.Text.Length > 0;
                if (on) _login.Focus();
            };

            _bar = new ProgressBar { Left = 28, Top = 448, Width = 584, Height = 16, Style = ProgressBarStyle.Continuous };
            Controls.Add(_bar);

            _status = L("Готов к установке", 28, 470, 584);
            _status.ForeColor = Color.FromArgb(200, 215, 255);

            _go = new Button
            {
                Text = Installed() == null ? "УСТАНОВИТЬ" : "ОБНОВИТЬ",
                Left = 360, Top = 502, Width = 250, Height = 44, FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(32, 150, 100), ForeColor = Paper,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold)
            };
            _go.Click += async (s, e) => await InstallAsync();
            Controls.Add(_go);

            _remove = new Button
            {
                Text = "УДАЛИТЬ", Left = 28, Top = 502, Width = 170, Height = 44, FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(120, 40, 48), ForeColor = Paper,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Visible = Installed() != null
            };
            _remove.Click += (s, e) => Remove();
            Controls.Add(_remove);

            if (uninstallMode) BeginInvoke(() => Remove());
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // шапка в цветах игры
            using var br = new System.Drawing.Drawing2D.LinearGradientBrush(
                new Rectangle(0, 0, ClientSize.Width, 96), Blue, Color.FromArgb(18, 60, 170),
                System.Drawing.Drawing2D.LinearGradientMode.Vertical);
            e.Graphics.FillRectangle(br, new Rectangle(0, 0, ClientSize.Width, 96));
            using var pen = new Pen(Color.FromArgb(10, 8, 26), 3);
            e.Graphics.DrawLine(pen, 0, 96, ClientSize.Width, 96);
        }

        /// <summary>Файл игры рядом с установщиком — для установки без интернета.</summary>
        static string LocalGame()
        {
            string here = Path.GetDirectoryName(Environment.ProcessPath!)!;
            foreach (var n in new[] { "GEO_DASH.exe", "GEO DASH.exe", "game.exe" })
            {
                string p = Path.Combine(here, n);
                if (File.Exists(p) && new FileInfo(p).Length > 1024 * 1024) return p;
            }
            return null;
        }

        static bool HasLocalGame() => LocalGame() != null;

        /// <summary>Файлы .gdl открываются игрой по двойному клику.</summary>
        static void AssociateGdl(string dir)
        {
            try
            {
                string exe = Cfg.GameExe(dir);
                using (var k = Registry.LocalMachine.CreateSubKey(@"Software\Classes\.gdl"))
                    k.SetValue("", "GeoDash.Level");

                using (var k = Registry.LocalMachine.CreateSubKey(@"Software\Classes\GeoDash.Level"))
                    k.SetValue("", "Уровень GEO DASH");
                using (var k = Registry.LocalMachine.CreateSubKey(@"Software\Classes\GeoDash.Level\DefaultIcon"))
                    k.SetValue("", exe + ",0");
                using (var k = Registry.LocalMachine.CreateSubKey(@"Software\Classes\GeoDash.Level\shell\open\command"))
                    k.SetValue("", "\"" + exe + "\" \"%1\"");
            }
            catch { }
        }

        /// <summary>Куда игра уже поставлена (из реестра), либо null.</summary>
        static string Installed()
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(Cfg.UninstallKey);
                return k?.GetValue("InstallLocation") as string;
            }
            catch { return null; }
        }

        void Status(string s)
        {
            if (InvokeRequired) { BeginInvoke(() => Status(s)); return; }
            _status.Text = s;
        }

        /// <summary>Спросить у сервера ключ по логину и паролю.</summary>
        async Task PickKeyAsync()
        {
            string name = _login.Text.Trim(), pass = _pass.Text;
            if (name.Length == 0 || pass.Length == 0)
            {
                Status("Введите логин и пароль от аккаунта на сайте");
                return;
            }

            _pick.Enabled = false;
            Status("Ищу ключ на вашем аккаунте…");
            var (good, key, text) = await Net.PickKeyAsync(name, pass);
            _pick.Enabled = true;

            if (!good) { Status(text); return; }

            _key.Enabled = true;
            _key.Text = Lic.Pretty(key);
            Status(text + " — можно устанавливать");
        }

        async Task InstallAsync()
        {
            if (_busy) return;
            _busy = true;
            _go.Enabled = _remove.Enabled = false;

            string dir = _dir.Text.Trim();
            try
            {
                Directory.CreateDirectory(dir);

                // сам установщик кладём рядом — он же служба и деинсталлятор
                string me = Environment.ProcessPath!;
                string target = Cfg.SetupExe(dir);
                if (!string.Equals(me, target, StringComparison.OrdinalIgnoreCase))
                {
                    UpdateService.Uninstall();              // старая служба держит файл
                    await Task.Delay(400);
                    File.Copy(me, target, true);
                }

                var prog = new Progress<double>(p => BeginInvoke(() => _bar.Value = (int)Math.Clamp(p, 0, 100)));
                UpdateInfo info = null;
                bool offline = _offline.Checked;

                // ключ проверяем первым делом: без годного ключа не ставим
                string lic = Lic.WellFormed(_key.Text) ? Lic.Pretty(_key.Text) : Cfg.ReadMarkKey(dir);
                if (!Lic.WellFormed(lic))
                {
                    Status("Введите лицензионный ключ — он есть в вашем аккаунте на сайте");
                    return;
                }

                Status("Проверяю ключ на сервере…");
                var (keyOk, keySay) = await Net.CheckKeyAsync(lic);
                if (!keyOk)
                {
                    Status(keySay.Length > 0 ? keySay : "Ключ не принят сервером");
                    return;
                }
                Status(keySay.Length > 0 ? keySay : "Ключ годен");

                if (!offline)
                {
                    Status("Проверяю интернет…");
                    if (!await Net.OnlineAsync())
                    {
                        offline = true;
                        Status("Интернета нет — ставлю из файла рядом");
                    }
                }

                if (!offline)
                {
                    // всегда ставим самую свежую сборку с сервера
                    info = await Net.CheckAsync();
                    Status(info != null && !string.IsNullOrEmpty(info.version)
                        ? "Скачиваю GEO DASH " + info.version + "…"
                        : "Скачиваю последнюю версию игры…");

                    if (!await Net.DownloadFirstAsync(Net.GameUrls(info), Cfg.GameExe(dir),
                                                      new Progress<string>(Status), prog))
                    {
                        if (LocalGame() != null)
                        {
                            Status("Скачать не вышло — ставлю из файла рядом");
                            offline = true;
                        }
                        else
                        {
                            Status("Не получилось скачать игру. Проверь интернет.");
                            return;
                        }
                    }
                    else _bar.Value = 100;
                }

                if (offline)
                {
                    string local = LocalGame();
                    if (local == null)
                    {
                        Status("Рядом с установщиком нет файла GEO_DASH.exe — положи его сюда или включи интернет");
                        return;
                    }
                    Status("Ставлю из файла рядом…");
                    File.Copy(local, Cfg.GameExe(dir), true);
                    _bar.Value = 100;
                }

                // отдельная качалка обновлений рядом с игрой
                try { File.Copy(Cfg.SetupExe(dir), Cfg.AutoUpdateExe(dir), true); } catch { }
                Cfg.WriteVersion(dir, info?.version ?? Net.LocalVersion(dir));

                // метка установки плюс сам ключ в licence.txt, зашифрованным:
                // по метке игра понимает, что её поставили установщиком, а ключ
                // активирует сама при первом запуске
                Cfg.WriteMark(dir, info?.version ?? Net.LocalVersion(dir), lic);

                Status("Создаю ярлыки…");
                if (_desktop.Checked)
                    Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                                          Cfg.AppName + ".lnk"), Cfg.GameExe(dir), dir);
                if (_menu.Checked)
                {
                    string sm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), Cfg.Brand);
                    Directory.CreateDirectory(sm);
                    Shortcut(Path.Combine(sm, Cfg.AppName + ".lnk"), Cfg.GameExe(dir), dir);
                }

                if (_assoc.Checked) AssociateGdl(dir);
                RegisterUninstall(dir, info?.version ?? Net.LocalVersion(dir));

                if (_service.Checked)
                {
                    Status("Ставлю службу автообновления…");
                    Status(UpdateService.Install(dir));
                }
                else UpdateService.Uninstall();

                Status("Готово. GEO DASH установлен в " + dir);
                _go.Text = "ОБНОВИТЬ";
                _remove.Visible = true;

                if (_run.Checked)
                {
                    try { Process.Start(new ProcessStartInfo(Cfg.GameExe(dir)) { UseShellExecute = true, WorkingDirectory = dir }); } catch { }
                    await Task.Delay(1200);
                    Close();
                }
            }
            catch (Exception ex)
            {
                Status("Ошибка: " + ex.Message);
            }
            finally
            {
                _busy = false;
                _go.Enabled = _remove.Enabled = true;
            }
        }

        void Remove()
        {
            string dir = Installed() ?? _dir.Text.Trim();
            if (MessageBox.Show(this, "Удалить GEO DASH из " + dir + "?\nСохранения и уровни останутся.",
                                "Удаление", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            UpdateService.Uninstall();
            try { File.Delete(Cfg.GameExe(dir)); } catch { }
            try { File.Delete(Cfg.InstallMark(dir)); } catch { }
            try
            {
                File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                                         Cfg.AppName + ".lnk"));
            }
            catch { }
            try
            {
                Directory.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                                              Cfg.Brand), true);
            }
            catch { }
            try { Registry.LocalMachine.DeleteSubKeyTree(Cfg.UninstallKey, false); } catch { }

            Status("GEO DASH удалён. Файл установщика можно стереть вручную.");
            _remove.Visible = false;
            _go.Text = "УСТАНОВИТЬ";
        }

        /// <summary>Запись в «Установка и удаление программ».</summary>
        static void RegisterUninstall(string dir, string version)
        {
            try
            {
                using var k = Registry.LocalMachine.CreateSubKey(Cfg.UninstallKey);
                k.SetValue("DisplayName", Cfg.AppName);
                k.SetValue("DisplayVersion", version ?? "1.0.0");
                k.SetValue("Publisher", Cfg.Brand);
                k.SetValue("InstallLocation", dir);
                k.SetValue("DisplayIcon", Cfg.GameExe(dir));
                k.SetValue("UninstallString", "\"" + Cfg.SetupExe(dir) + "\" --uninstall");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
            catch { }
        }

        /// <summary>Ярлык делаем через PowerShell — без лишних библиотек.</summary>
        static void Shortcut(string lnk, string target, string workDir)
        {
            try
            {
                string ps =
                    "$s=(New-Object -ComObject WScript.Shell).CreateShortcut('" + lnk.Replace("'", "''") + "');" +
                    "$s.TargetPath='" + target.Replace("'", "''") + "';" +
                    "$s.WorkingDirectory='" + workDir.Replace("'", "''") + "';" +
                    "$s.IconLocation='" + target.Replace("'", "''") + ",0';" +
                    "$s.Description='GEO DASH';$s.Save()";
                var psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + ps + "\"")
                { UseShellExecute = false, CreateNoWindow = true };
                using var p = Process.Start(psi);
                p.WaitForExit(20000);
            }
            catch { }
        }
    }

    /// <summary>
    /// Окно autoupdate.exe: сверяет version.txt рядом с игрой и update.json на GitHub.
    /// Версия в txt меньше — качает новую игру и переписывает txt.
    /// </summary>
    public class UpdateWindow : Form
    {
        readonly string _dir;
        readonly Label _text;
        readonly ProgressBar _bar;
        readonly Button _go, _close;

        public UpdateWindow(string dir)
        {
            _dir = dir;
            Text = Cfg.AppName + " — обновление";
            ClientSize = new Size(520, 210);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(14, 12, 34);
            ForeColor = Color.FromArgb(240, 244, 255);
            Font = new Font("Segoe UI", 9.5f);

            var title = new Label
            {
                Text = "GEO DASH", Left = 24, Top = 18, Width = 460, Height = 34,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold), BackColor = Color.Transparent
            };
            Controls.Add(title);

            _text = new Label
            {
                Text = "Проверяю обновление…", Left = 24, Top = 64, Width = 470, Height = 46,
                BackColor = Color.Transparent, ForeColor = Color.FromArgb(205, 220, 255)
            };
            Controls.Add(_text);

            _bar = new ProgressBar { Left = 24, Top = 116, Width = 470, Height = 16, Style = ProgressBarStyle.Marquee };
            Controls.Add(_bar);

            _go = new Button
            {
                Text = "ОБНОВИТЬ", Left = 268, Top = 150, Width = 226, Height = 40, FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(32, 150, 100), ForeColor = Color.FromArgb(240, 244, 255),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold), Visible = false
            };
            _go.Click += async (s, e) => await RunAsync();
            Controls.Add(_go);

            _close = new Button
            {
                Text = "ЗАКРЫТЬ", Left = 24, Top = 150, Width = 150, Height = 40, FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 52, 86), ForeColor = Color.FromArgb(240, 244, 255)
            };
            _close.Click += (s, e) => Close();
            Controls.Add(_close);

            Shown += async (s, e) => await RunAsync();
        }

        async Task RunAsync()
        {
            _go.Visible = false;
            _bar.Style = ProgressBarStyle.Marquee;

            string local = Net.LocalVersion(_dir);
            var info = await Net.CheckAsync();
            if (info == null)
            {
                Done("Сервер обновлений недоступен. Версия: " + local);
                return;
            }
            if (!Net.IsNewer(info.version, local))
            {
                Done($"Установлена последняя версия {local}");
                return;
            }

            _text.Text = $"Есть версия {info.version} (у тебя {local}). Качаю…";
            if (UpdateService.GameRunning())
            {
                Done("Закрой игру — файл занят, обновление применится после выхода");
                _go.Visible = true;
                return;
            }

            string res = await UpdateService.TickAsync(_dir);
            Done(res);
        }

        void Done(string text)
        {
            _bar.Style = ProgressBarStyle.Continuous;
            _bar.Value = _bar.Maximum;
            _text.Text = text;
        }
    }

    /// <summary>
    /// Галочка в стиле игры: тёмный квадрат с обводкой и яркой зелёной птичкой.
    /// Своя отрисовка нужна потому, что обычная галочка Windows на тёмном фоне
    /// получается белым квадратом, в котором ничего не видно.
    /// </summary>
    public class GdCheck : CheckBox
    {
        public GdCheck()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                     | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? Color.FromArgb(14, 12, 34));

            int side = 18;
            var box = new Rectangle(0, (Height - side) / 2, side, side);

            // подложка квадрата
            using (var br = new SolidBrush(Checked
                       ? Color.FromArgb(32, 150, 100)
                       : Color.FromArgb(28, 26, 60)))
                g.FillRectangle(br, box);

            using (var pen = new Pen(Checked ? Color.FromArgb(120, 255, 180)
                                             : Color.FromArgb(90, 95, 150), 2f))
                g.DrawRectangle(pen, box);

            // птичка
            if (Checked)
            {
                using var pen = new Pen(Color.FromArgb(235, 255, 240), 2.6f)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round
                };
                g.DrawLines(pen, new[]
                {
                    new PointF(box.X + 4f,  box.Y + side * 0.52f),
                    new PointF(box.X + 7.5f, box.Y + side * 0.74f),
                    new PointF(box.X + 14f, box.Y + side * 0.28f)
                });
            }

            // подпись
            var text = new Rectangle(box.Right + 10, 0, Width - box.Right - 12, Height);
            using (var br = new SolidBrush(Enabled ? ForeColor : Color.Gray))
            using (var sf = new StringFormat { LineAlignment = StringAlignment.Center,
                                               Trimming = StringTrimming.EllipsisCharacter })
                g.DrawString(Text, Font, br, text, sf);

            if (Focused)
                using (var pen = new Pen(Color.FromArgb(120, 160, 220, 255), 1f))
                    g.DrawRectangle(pen, box.X - 3, box.Y - 3, side + 6, side + 6);
        }
    }
}
