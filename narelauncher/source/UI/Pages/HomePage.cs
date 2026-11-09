using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI.Pages
{
    public class HomePage : PageBase
    {
        private readonly NareDropDown _version = new NareDropDown();
        private readonly NareButton _play = new NareButton();
        private readonly NareButton _folder = new NareButton();
        private readonly NareButton _visuals = new NareButton();
        private readonly NareButton _nick = new NareButton();
        private readonly NareButton _wmine = new NareButton();

        /// <summary>
        /// Наш сервер. Адрес держим здесь, а не в настройках: кнопка нужна,
        /// чтобы зайти одним нажатием, а не чтобы его переписывали.
        /// </summary>
        private const string WMineAddress = "213.171.18.140:32846";

        /// <summary>
        /// Версия, на которой WMine работает. 1.21.4 — та, под которую собран
        /// NareVisuals: на ней всё проверено, остальные 1.21.x мод терпит, но
        /// часть рисования там может не примениться.
        /// </summary>
        private const string WMineVersion = "1.21.4";
        private readonly NareProgress _progress = new NareProgress();
        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);
        private readonly NareLabel _hint = new NareLabel("", 9f, FontStyle.Regular, Theme.Dim);
        private readonly PlayerRenderer _player = new PlayerRenderer();
        private readonly System.Windows.Forms.Timer _playerTimer =
            new System.Windows.Forms.Timer { Interval = 33 };

        private CancellationTokenSource _cts;
        private bool _busy;

        public HomePage()
        {
            Padding = new Padding(0);

            _play.Text = "ИГРАТЬ";
            _play.Style = BtnStyle.Accent;
            _play.Icon = Ico.Play;
            _play.Font = Theme.F(12f, FontStyle.Bold);
            _play.Size = new Size(210, 56);
            _play.Radius = 14;
            _play.Click += PlayClick;

            _folder.Text = "Папка версии";
            _folder.Style = BtnStyle.Ghost;
            _folder.Icon = Ico.Folder;
            _folder.Size = new Size(170, 40);
            _folder.Click += (s, e) => OpenInstanceFolder();

            _visuals.Text = "Обновить визуалы";
            _visuals.Style = BtnStyle.Ghost;
            _visuals.Icon = Ico.Refresh;
            _visuals.Size = new Size(190, 40);
            _visuals.Click += (s, e) => RefreshVisuals();

            _nick.Text = "Игровой ник";
            _nick.Style = BtnStyle.Ghost;
            _nick.Icon = Ico.Account;
            _nick.Size = new Size(190, 40);
            _nick.Click += (s, e) => OpenSubAccounts();

            _wmine.Text = "ИГРАТЬ НА WMINE";
            _wmine.Style = BtnStyle.Success;
            _wmine.Icon = Ico.Play;
            _wmine.Font = Theme.F(10f, FontStyle.Bold);
            _wmine.Size = new Size(210, 44);
            _wmine.Radius = 12;
            _wmine.Click += (s, e) => PlayWMine();

            _progress.Height = 8;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _hint.TextAlign = ContentAlignment.MiddleLeft;

            // персонаж — фон всей страницы, поэтому он не контрол:
            // кнопки и надписи остаются обычными контролами поверх него
            _playerTimer.Tick += PlayerTick;
            _playerTimer.Start();
            Controls.Add(_version);
            Controls.Add(_play);
            Controls.Add(_wmine);
            Controls.Add(_folder);
            Controls.Add(_visuals);
            Controls.Add(_nick);
            Controls.Add(_progress);
            Controls.Add(_status);
            Controls.Add(_hint);

            _version.SelectedIndexChanged += (s, e) =>
            {
                var v = _version.SelectedItem as InstalledVersion;
                if (v != null)
                {
                    Settings.Current.SelectedVersion = v.Id;
                    Settings.Save();
                    UpdateHint();
                }
            };

            GameLauncher.Exited += code =>
            {
                try
                {
                    if (IsHandleCreated)
                        BeginInvoke((Action)(() => { SetBusy(false); SetStatus("Игра закрыта (код " + code + ")"); }));
                }
                catch { }
            };
        }

        /// <summary>Перечитать персонажа — после правок скина или вещей.</summary>
        public void RefreshPlayer()
        {
            _player.LoadFromAccount();
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _player.Poke(e.Location);
        }

        private void PlayerTick(object sender, EventArgs e)
        {
            if (!Visible || Width < 8 || Height < 8) return;

            var mouse = PointToClient(MousePosition);
            if (ClientRectangle.Contains(mouse)) _player.LookAt(mouse);
            else _player.LookAway();

            _player.Tick(0.033f);

            // перерисовываем только верхнюю часть: нижняя панель не меняется
            Invalidate(new Rectangle(0, 0, Width, Math.Max(0, Height - PanelHeight)));
        }

        public override void PageShown()
        {
            ReloadVersions();
            _player.LoadFromAccount();
            Host.RefreshAccountChip();
            UpdateNickButton();
            Layout1();
            Invalidate();
        }

        public void ReloadVersions()
        {
            var versions = VersionCatalog.Scan();
            var objects = new System.Collections.Generic.List<object>();
            foreach (var v in versions) objects.Add(v);

            object select = null;
            foreach (var v in versions)
                if (string.Equals(v.Id, Settings.Current.SelectedVersion, StringComparison.OrdinalIgnoreCase))
                    select = v;

            _version.SetItems(objects, select);
            UpdateHint();
        }

        private void UpdateHint()
        {
            var v = _version.SelectedItem as InstalledVersion;
            if (v == null)
            {
                _hint.Text = "Версии пока не установлены — откройте вкладку «Версии».";
                _hint.ForeColor = Theme.Warn;
                return;
            }

            string mods = v.SupportsMods ? "моды: " + v.LoaderRu : "без модов";
            string isolated = Settings.Current.IsolateVersions ? "своя папка и свои моды" : "общая папка игры";
            _hint.Text = v.LoaderRu + " · Minecraft " + v.BaseVersion + " · " + mods + " · " + isolated;
            _hint.ForeColor = Theme.Dim;
        }

        // --------------------------------------------------------------- запуск

        /// <summary>
        /// Кнопка нашего сервера: сразу заходим туда, и выйти обратно в меню
        /// из игры нельзя — только закрыть Minecraft.
        /// </summary>
        private void PlayWMine()
        {
            if (_busy) { SetStatus("Подождите, запуск уже идёт."); return; }

            // на WMine заходят только со сборкой Visuals, поэтому не просим
            // человека выбирать её руками: нет — поставим сами
            if (!Host.EnsureAccount("WMine")) return;

            GameLauncher.QuickServer = WMineAddress;
            GameLauncher.LockToServer = true;

            SetStatus("Захожу на WMine…");

            PlayFromLink(new PlayLink.Request
            {
                Version = WMineVersion,
                Loader = "visuals",
                Server = WMineAddress
            });
        }

        /// <summary>Запуск с немедленным заходом на сервер — зовётся со вкладки «Серверы».</summary>
        /// <summary>
        /// Что сделать со сборкой перед самым запуском. Вкладка «Вместе»
        /// доносит сюда моды хозяина, когда сборка уже готова, но игра ещё
        /// не пошла. Срабатывает один раз.
        /// </summary>
        public Func<string, IProgress<InstallStatus>, CancellationToken, Task> BeforeLaunch;

        public void LaunchOn(string address)
        {
            if (_busy) { SetStatus("Подождите, запуск уже идёт."); return; }

            GameLauncher.QuickServer = address;
            PlayClick(this, EventArgs.Empty);
        }

        /// <summary>
        /// Запуск по ссылке с сайта: версии может ещё не быть, поэтому сначала
        /// ставим её, а потом запускаем. Аккаунт к этому моменту уже есть —
        /// о нём позаботился MainForm.
        /// </summary>
        public async void PlayFromLink(PlayLink.Request request)
        {
            if (request == null || !request.HasVersion) return;

            if (_busy) { SetStatus("Подождите, запуск уже идёт."); return; }

            var account = AccountStore.Selected;
            if (account == null) return;

            SetBusy(true);
            _cts = new CancellationTokenSource();

            var progress = new Progress<InstallStatus>(s =>
            {
                SetStatus(s.Text);
                if (s.Indeterminate || s.Total <= 0) _progress.SetIndeterminate(true);
                else
                {
                    _progress.SetIndeterminate(false);
                    _progress.Maximum = s.Total;
                    _progress.Value = s.Done;
                }
            });

            try
            {
                string id = await Resolve(request, progress, _cts.Token);
                if (string.IsNullOrEmpty(id)) return;

                Settings.Current.SelectedVersion = id;
                Settings.Save();
                ReloadVersions();

                if (BeforeLaunch != null)
                {
                    var hook = BeforeLaunch;
                    BeforeLaunch = null;
                    await hook(id, progress, _cts.Token);
                }

                // вход одним щелчком — значит без вопросов: мод, который
                // этой сборке не подходит, просто отключаем, иначе падение
                try
                {
                    int off = 0;
                    foreach (var build in VersionCatalog.Scan())
                    {
                        if (build.Id != id) continue;
                        off += DisableBlockedMods(build);
                        foreach (var problem in ModCheck.Scan("mod", build))
                            if (problem.Bad) { ModCheck.TurnOff(problem); off++; }
                        break;
                    }
                    if (off > 0) SetStatus("Отключено несовместимых модов: " + off + "…");
                }
                catch (Exception ex) { Log.Warn("Проверка модов перед входом: " + ex.Message); }

                if (!string.IsNullOrEmpty(request.Server))
                    GameLauncher.QuickServer = request.Server;

                SetStatus("Запускаю " + id + "…");
                await GameLauncher.LaunchAsync(id, account, progress, _cts.Token);

                SetStatus("Minecraft запущен");
                _progress.SetIndeterminate(false);
                _progress.Maximum = 1;
                _progress.Value = 1;

                if (Settings.Current.CloseOnLaunch)
                {
                    await Task.Delay(1200);
                    Host.Close();
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                SetStatus("Отменено");
            }
            catch (Exception ex)
            {
                Host.ShowError("Не удалось запустить " + request.Version, ex,
                    delegate { PlayFromLink(request); });
                SetStatus("Ошибка: " + ex.Message);
            }
            finally
            {
                SetBusy(false);
                _progress.SetIndeterminate(false);
                if (_cts != null) { _cts.Dispose(); _cts = null; }
            }
        }

        /// <summary>Находит уже поставленную версию или ставит нужную. Возвращает её id.</summary>
        private async Task<string> Resolve(PlayLink.Request request,
            IProgress<InstallStatus> progress, CancellationToken ct)
        {
            var ready = FindInstalled(request);
            if (ready != null)
            {
                SetStatus("Версия " + ready.Id + " уже стоит");
                return ready.Id;
            }

            string game = request.Version;
            string id = null;

            switch (request.Loader)
            {
                case "fabric":
                    {
                        var build = Best(await LoaderInstaller.GetFabricLoadersAsync(game, ct));
                        if (build == null) throw new Exception("Для " + game + " нет сборок Fabric.");
                        SetStatus("Ставлю Fabric " + build.Version + " для " + game + "…");
                        id = await LoaderInstaller.InstallFabricAsync(game, build.Version, ct);
                        break;
                    }

                case "quilt":
                    {
                        var build = Best(await LoaderInstaller.GetQuiltLoadersAsync(game, ct));
                        if (build == null) throw new Exception("Для " + game + " нет сборок Quilt.");
                        SetStatus("Ставлю Quilt " + build.Version + " для " + game + "…");
                        id = await LoaderInstaller.InstallQuiltAsync(game, build.Version, ct);
                        break;
                    }

                case "forge":
                    {
                        var build = Best(await LoaderInstaller.GetForgeVersionsAsync(game, ct));
                        if (build == null) throw new Exception("Для " + game + " нет сборок Forge.");
                        SetStatus("Ставлю Forge " + build.Version + "…");
                        id = await LoaderInstaller.InstallForgeAsync(build.Version, progress, ct);
                        break;
                    }

                case "neoforge":
                    {
                        var build = Best(await LoaderInstaller.GetNeoForgeVersionsAsync(game, ct));
                        if (build == null) throw new Exception("Для " + game + " нет сборок NeoForge.");
                        SetStatus("Ставлю NeoForge " + build.Version + "…");
                        id = await LoaderInstaller.InstallNeoForgeAsync(build.Version, progress, ct);
                        break;
                    }

                case "visuals":
                    {
                        var build = Best(await LoaderInstaller.GetForgeVersionsAsync(game, ct));
                        if (build == null) throw new Exception("Для " + game + " нет сборок Forge.");
                        SetStatus("Ставлю Forge " + build.Version + " под сборку Visuals…");

                        string forgeId = await LoaderInstaller.InstallForgeAsync(build.Version, progress, ct);
                        await VersionInstaller.InstallAsync(forgeId, progress, ct);
                        id = LoaderInstaller.CreateVisualsProfile(forgeId, game);
                        break;
                    }

                default:
                    id = game;
                    SetStatus("Качаю Minecraft " + game + "…");
                    break;
            }

            if (string.IsNullOrEmpty(id)) throw new Exception("Не вышло подготовить версию " + game + ".");

            await VersionInstaller.InstallAsync(id, progress, ct);

            if (id.EndsWith(LoaderInstaller.VisualsSuffix, StringComparison.OrdinalIgnoreCase))
                BundledMods.InstallInto(id, SetStatus);

            return id;
        }

        /// <summary>Уже поставленная версия с той же основой и тем же загрузчиком.</summary>
        public static InstalledVersion FindInstalled(PlayLink.Request request)
        {
            bool wantVisuals = request.Loader == "visuals";
            string loader = wantVisuals ? "forge" : request.Loader;

            foreach (var v in VersionCatalog.Scan())
            {
                if (!v.HasJar) continue;
                if (!string.Equals(v.BaseVersion, request.Version, StringComparison.OrdinalIgnoreCase)) continue;

                bool isVisuals = v.Id.EndsWith(LoaderInstaller.VisualsSuffix, StringComparison.OrdinalIgnoreCase);
                if (isVisuals != wantVisuals) continue;

                if (!string.Equals(v.Loader, loader, StringComparison.OrdinalIgnoreCase)) continue;

                return v;
            }

            return null;
        }

        /// <summary>Рекомендованная сборка загрузчика, иначе первая стабильная.</summary>
        private static LoaderVersion Best(System.Collections.Generic.List<LoaderVersion> list)
        {
            if (list == null || list.Count == 0) return null;

            foreach (var v in list) if (v.Recommended) return v;
            foreach (var v in list) if (v.Stable) return v;

            return list[0];
        }

        private async void PlayClick(object sender, EventArgs e)
        {
            if (_busy)
            {
                if (_cts != null) _cts.Cancel();
                return;
            }

            // режим техработ — вход закрыт (для персонала сервер его не ставит)
            if (!string.IsNullOrEmpty(NareAuth.Last.Maintenance))
            {
                GameLauncher.QuickServer = null;
                Host.ShowInfo("Технические работы\n\n" + NareAuth.Last.Maintenance);
                return;
            }

            var version = _version.SelectedItem as InstalledVersion;
            if (version == null)
            {
                Host.ShowInfo("Сначала установите хотя бы одну версию на вкладке «Версии».");
                Host.Navigate(1);
                return;
            }

            var account = AccountStore.Selected;
            if (account == null)
            {
                Host.ManageAccounts();
                account = AccountStore.Selected;
                if (account == null) return;
                Invalidate();
            }

            // у аккаунта должен быть пароль на сервере — иначе просим войти
            if (string.IsNullOrEmpty(account.Token) && !Host.EnsureAuth()) return;

            if (GameLauncher.IsRunning)
            {
                if (!Host.Confirm("Игра уже запущена. Запустить ещё одну копию?")) return;
            }

            // с несовместимым модом Minecraft падает на старте и пишет об этом
            // стеной текста в логе. Проще сказать заранее.
            if (!CheckMods(version)) return;

            SetBusy(true);
            _cts = new CancellationTokenSource();

            var progress = new Progress<InstallStatus>(s =>
            {
                SetStatus(s.Text);
                if (s.Indeterminate || s.Total <= 0) _progress.SetIndeterminate(true);
                else
                {
                    _progress.SetIndeterminate(false);
                    _progress.Maximum = s.Total;
                    _progress.Value = s.Done;
                }
            });

            try
            {
                await GameLauncher.LaunchAsync(version.Id, account, progress, _cts.Token);
                SetStatus("Minecraft запущен");
                _progress.SetIndeterminate(false);
                _progress.Maximum = 1;
                _progress.Value = 1;

                if (Settings.Current.CloseOnLaunch)
                {
                    await Task.Delay(1200);
                    Host.Close();
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                SetStatus("Отменено");
            }
            catch (Exception ex)
            {
                Host.ShowError("Не удалось запустить игру", ex, delegate { PlayClick(this, EventArgs.Empty); });
                SetStatus("Ошибка: " + ex.Message);
            }
            finally
            {
                SetBusy(false);
                _progress.SetIndeterminate(false);
                if (_cts != null) { _cts.Dispose(); _cts = null; }
            }
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _play.Text = busy ? "ОТМЕНА" : "ИГРАТЬ";
            _play.Icon = busy ? Ico.Stop : Ico.Play;
            _play.Style = busy ? BtnStyle.Danger : BtnStyle.Accent;
            _version.Enabled = !busy;
            _play.Invalidate();
        }

        public void SetStatus(string text)
        {
            _status.Text = text ?? "";
        }

        /// <summary>Кладёт свежий NareVisuals в выбранную сборку, если наш файл новее.</summary>
        private async void RefreshVisuals()
        {
            var version = _version.SelectedItem as InstalledVersion;
            string id = version != null ? version.Id : Settings.Current.SelectedVersion;

            if (string.IsNullOrEmpty(id))
            {
                _status.Text = "Сначала выберите сборку в списке.";
                _status.ForeColor = Theme.Warn;
                return;
            }

            try
            {
                if (Settings.Current.UpdateFromGitHub)
                {
                    _status.Text = "Смотрю GitHub…";
                    _status.ForeColor = Theme.Muted;

                    string fromWeb = await ModUpdater.UpdateAsync(id, null, CancellationToken.None);
                    if (!string.IsNullOrEmpty(fromWeb))
                    {
                        _status.Text = fromWeb;
                        _status.ForeColor = Theme.Good;
                    }
                }

                string report = BundledMods.Refresh(id);
                if (string.IsNullOrEmpty(_status.Text) || !Settings.Current.UpdateFromGitHub)
                {
                    _status.Text = report;
                    _status.ForeColor = Theme.Good;
                }
            }
            catch (Exception ex)
            {
                Host.ShowError("Не удалось обновить визуалы", ex, delegate { RefreshVisuals(); });
                _status.Text = "Обновить визуалы не вышло: " + ex.Message;
                _status.ForeColor = Theme.Bad;
            }
        }

        /// <summary>
        /// Смотрим jar-ы сборки перед запуском. Вернёт false, только если
        /// игрок сам решил ничего не запускать.
        /// </summary>
        private bool CheckMods(InstalledVersion version)
        {
            if (version == null || !version.SupportsMods) return true;

            // моды, запрещённые администратором для этой версии, отключаем сразу
            int adminOff = DisableBlockedMods(version);
            if (adminOff > 0) SetStatus("Отключено модов по запрету администратора: " + adminOff + "…");

            List<ModCheck.Problem> bad;
            try
            {
                bad = new List<ModCheck.Problem>();
                foreach (var p in ModCheck.Scan("mod", version))
                    if (p.Bad) bad.Add(p);
            }
            catch (Exception ex)
            {
                Log.Warn("Проверка модов не прошла: " + ex.Message);
                return true;                     // не её дело мешать запуску
            }

            if (bad.Count == 0) return true;

            var text = new System.Text.StringBuilder();
            text.Append(bad.Count == 1
                ? "Один мод не подходит этой сборке:"
                : "Этим модам сборка не подходит (" + bad.Count + "):");

            int shown = 0;
            foreach (var p in bad)
            {
                if (shown++ >= 6) { text.Append("\n…и ещё " + (bad.Count - 6)); break; }
                text.Append("\n\n• " + p.Mod.FileName + "\n  " + p.Text);
            }

            text.Append("\n\nОтключить их и запустить игру?");
            text.Append("\nФайлы останутся на месте — только переименуются.");

            var answer = MessageBox.Show(FindForm(), text.ToString(),
                "Моды и сборка не совпадают",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

            if (answer == DialogResult.Cancel) return false;

            if (answer == DialogResult.Yes)
            {
                foreach (var p in bad) ModCheck.TurnOff(p);
                SetStatus("Отключено модов: " + bad.Count + ". Запускаю…");
            }

            return true;
        }

        /// <summary>Отключает моды, которые админ заблокировал для этой сборки.</summary>
        private int DisableBlockedMods(InstalledVersion version)
        {
            var policy = NareAuth.Last;
            if (policy == null || policy.BlockedMods.Count == 0) return 0;

            int off = 0;
            try
            {
                foreach (var mod in ModManager.List("mod", version.Id))
                {
                    if (!mod.Enabled) continue;

                    var about = ModJar.Read(mod.Path);
                    string id = about.Id;
                    if (string.IsNullOrEmpty(id)) continue;

                    if (policy.ModBlocked(id, version.BaseVersion))
                    {
                        ModManager.Toggle(mod);
                        off++;
                        Log.Info("Мод " + mod.FileName + " отключён: запрещён администратором");
                    }
                }
            }
            catch (Exception ex) { Log.Warn("Проверка запрещённых модов: " + ex.Message); }

            return off;
        }

        private void OpenInstanceFolder()
        {
            var v = _version.SelectedItem as InstalledVersion;
            string dir = v == null ? Paths.GameRoot : Paths.InstanceDir(v.Id);
            Paths.Ensure(dir);
            try { Process.Start("explorer.exe", "\"" + dir + "\""); }
            catch (Exception ex) { Host.ShowError("Не удалось открыть папку", ex); }
        }

        // --------------------------------------------------------------- вёрстка

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Layout1();
        }

        private const int PanelHeight = 212;

        /// <summary>Персонаж во весь верх страницы — под ним и рисуем.</summary>
        private Rectangle PlayerArea
        {
            get { return new Rectangle(0, 0, Width, Math.Max(0, Height - PanelHeight)); }
        }

        /// <summary>
        /// Нижняя панель в две строки: сверху выбор версии и кнопки запуска,
        /// под ними — вспомогательные.
        ///
        /// Раньше «Папка версии» стояла по жёсткой координате и в узком окне
        /// оказывалась под кнопкой сервера. Теперь правая группа считается
        /// первой, а левая кладётся в то, что от неё осталось.
        /// </summary>
        private void UpdateNickButton()
        {
            var acc = AccountStore.Selected;
            _nick.Text = acc == null ? "Игровой ник" : "Ник: " + acc.PlayName;
            _nick.Invalidate();
        }

        private void OpenSubAccounts()
        {
            var acc = AccountStore.Selected;
            if (acc == null) { Host.ManageAccounts(); acc = AccountStore.Selected; if (acc == null) return; }
            if (string.IsNullOrEmpty(acc.Token)) { Host.EnsureAuth(); if (string.IsNullOrEmpty(acc.Token)) return; }

            using (var form = new SubAccountsForm(acc)) form.ShowDialog(FindForm());
            Host.RefreshAccountChip();
            UpdateNickButton();
            Invalidate();
        }

        private void Layout1()
        {
            int top = Height - PanelHeight;
            int pad = 34;

            // правая группа: «Играть» у края, кнопка сервера слева от неё
            _play.SetBounds(Math.Max(pad, Width - pad - _play.Width), top + 34,
                _play.Width, _play.Height);
            _wmine.SetBounds(Math.Max(pad, _play.Left - 12 - _wmine.Width), top + 40,
                _wmine.Width, _wmine.Height);

            int free = _wmine.Left - 12;        // докуда можно класть левую группу

            _version.SetBounds(pad, top + 40, Math.Max(180, Math.Min(330, free - pad)), 44);

            // вспомогательные кнопки: в первую строку, если влезают обе
            int row = top + 40;
            int fx = _version.Right + 12;

            if (fx + _folder.Width + 12 + _visuals.Width + 12 + _nick.Width > free)
            {
                fx = pad;
                row = top + 96;
            }

            _folder.SetBounds(fx, row + 2, _folder.Width, _folder.Height);
            _visuals.SetBounds(_folder.Right + 12, row + 2, _visuals.Width, _visuals.Height);
            _nick.SetBounds(_visuals.Right + 12, row + 2, _nick.Width, _nick.Height);

            _hint.SetBounds(pad, top + 144, Math.Max(100, Width - pad * 2), 20);
            _progress.SetBounds(pad, top + 170, Math.Max(50, Width - pad * 2), 8);
            _status.SetBounds(pad, top + 182, Math.Max(50, Width - pad * 2 - 20), 24);

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);

            var hero = new Rectangle(0, 0, Width, Math.Max(0, Height - PanelHeight));
            Theme.PaintHeroBackground(g, hero);

            // заголовок
            // персонаж во весь экран, прямо под надписями
            _player.Paint(g, PlayerArea, true);

            // текст поверх него читается плохо, поэтому слева мягкая подложка
            int shade = Math.Min(Width, Math.Max(320, Width / 2));
            using (var veil = new System.Drawing.Drawing2D.LinearGradientBrush(
                       new Rectangle(0, 0, shade, Math.Max(1, hero.Height)),
                       Color.FromArgb(205, 8, 10, 16), Color.FromArgb(0, 8, 10, 16), 0f))
                g.FillRectangle(veil, 0, 0, shade, hero.Height);

            var titleRect = new Rectangle(40, hero.Height / 2 - 70, Math.Max(100, Width - 80), 60);
            Theme.Text1(g, "NARE LAUNCHER", Theme.F(30f, FontStyle.Bold), Color.White, titleRect,
                StringAlignment.Near, StringAlignment.Center, false);

            Theme.Text1(g, "Любая версия Minecraft · Fabric, Quilt, Forge, NeoForge · свои моды для каждой сборки",
                Theme.F(11f), Color.FromArgb(190, 210, 235),
                new Rectangle(42, hero.Height / 2 - 8, Math.Max(100, Width - 80), 28));

            var acc = AccountStore.Selected;
            Theme.Text1(g, acc == null ? "Аккаунт не выбран" : "Игрок: " + acc.Name,
                Theme.F(10f, FontStyle.Bold), acc == null ? Theme.Warn : Color.FromArgb(150, 180, 230),
                new Rectangle(42, hero.Height / 2 + 28, 400, 24));

            // нижняя панель
            var bar = new Rectangle(0, hero.Height, Width, PanelHeight);
            using (var b = new SolidBrush(Theme.Panel))
                g.FillRectangle(b, bar);
            using (var pen = new Pen(Color.FromArgb(34, 255, 255, 255)))
                g.DrawLine(pen, 0, bar.Y, Width, bar.Y);

            Theme.Text1(g, "ВЕРСИЯ ИГРЫ", Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                new Rectangle(34, bar.Y + 16, 300, 20));

            base.OnPaint(e);
        }
    }
}
