using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI.Pages
{
    public class VersionsPage : PageBase
    {
        private const int TabVanilla = 0, TabFabric = 1, TabQuilt = 2, TabForge = 3,
                          TabVisuals = 4, TabNeo = 5, TabInstalled = 6;

        /// <summary>Версия игры, под которую собран встроенный мод Forge Visuals.</summary>
        private const string VisualsVersion = "1.21.4";

        private readonly string[] _tabNames =
            { "Ваниль", "Fabric", "Quilt", "Forge", "Forge Visuals", "NeoForge", "Установленные" };
        private readonly List<NareButton> _tabs = new List<NareButton>();
        private int _tab = TabVanilla;

        private readonly NareTextBox _search = new NareTextBox { Placeholder = "Поиск версии…", Icon = Ico.Search };

        private readonly NareToggle _snapshots = new NareToggle();
        private readonly NareLabel _snapshotsLabel = new NareLabel("Снапшоты", 9.5f, FontStyle.Regular, Theme.Muted);
        private readonly NareToggle _old = new NareToggle();
        private readonly NareLabel _oldLabel = new NareLabel("Старые (альфа/бета)", 9.5f, FontStyle.Regular, Theme.Muted);

        /// <summary>Слева: версии Minecraft или установленные сборки.</summary>
        private readonly VirtualList _list = new VirtualList { ItemHeight = 56, EmptyText = "Список пуст" };

        /// <summary>Справа: сборки загрузчика для выбранной версии.</summary>
        private readonly VirtualList _builds = new VirtualList { ItemHeight = 56, EmptyText = "Выберите версию слева" };

        private readonly NareButton _install = new NareButton { Text = "Установить", Style = BtnStyle.Accent, Icon = Ico.Download, Size = new Size(190, 44) };
        private readonly NareButton _delete = new NareButton { Text = "Удалить", Style = BtnStyle.Danger, Icon = Ico.Trash, Size = new Size(150, 44) };
        private readonly NareButton _import = new NareButton { Text = "Импорт", Style = BtnStyle.Ghost, Icon = Ico.Plus, Size = new Size(140, 44) };
        private readonly NareButton _refresh = new NareButton { Text = "Обновить", Style = BtnStyle.Ghost, Icon = Ico.Refresh, Size = new Size(150, 44) };

        private readonly NareProgress _progress = new NareProgress();
        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);

        private List<RemoteVersion> _remote = new List<RemoteVersion>();
        private List<InstalledVersion> _installed = new List<InstalledVersion>();

        private CancellationTokenSource _cts;
        private CancellationTokenSource _buildsCts;
        private bool _busy;
        private bool _loadedOnce;

        private bool IsLoaderTab { get { return _tab != TabVanilla && _tab != TabInstalled; } }

        public VersionsPage()
        {
            Padding = new Padding(0);

            for (int i = 0; i < _tabNames.Length; i++)
            {
                int index = i;
                int w = 98;
                if (i == TabInstalled) w = 148;
                else if (i == TabVisuals) w = 132;
                else if (i == TabNeo) w = 104;

                var b = new NareButton
                {
                    Text = _tabNames[i],
                    Style = BtnStyle.Ghost,
                    Size = new Size(w, 36),
                    Radius = 9,
                    Font = Theme.F(9.5f, FontStyle.Bold)
                };
                b.Click += (s, e) => SwitchTab(index);
                _tabs.Add(b);
                Controls.Add(b);
            }

            _snapshots.Checked = Settings.Current.ShowSnapshots;
            _old.Checked = Settings.Current.ShowOldVersions;
            _snapshots.CheckedChanged += (s, e) =>
            {
                Settings.Current.ShowSnapshots = _snapshots.Checked;
                Settings.Save();
                RenderList();
            };
            _old.CheckedChanged += (s, e) =>
            {
                Settings.Current.ShowOldVersions = _old.Checked;
                Settings.Save();
                RenderList();
            };

            _search.Inner.TextChanged += (s, e) => RenderList();

            _list.Renderer = DrawLeftItem;
            _list.SelectionChanged += async (s, e) =>
            {
                UpdateButtons();
                if (IsLoaderTab) await LoadBuilds();
            };
            _list.ItemActivated += (s, e) => { if (!IsLoaderTab) InstallClick(null, EventArgs.Empty); };

            _builds.Renderer = DrawBuild;
            _builds.SelectionChanged += (s, e) => UpdateButtons();
            _builds.ItemActivated += (s, e) => InstallClick(null, EventArgs.Empty);

            _install.Click += InstallClick;
            _delete.Click += DeleteClick;
            _import.Click += ImportClick;
            _refresh.Click += async (s, e) => await LoadTab(true);

            Controls.Add(_list);
            Controls.Add(_builds);
            Controls.Add(_search);
            Controls.Add(_snapshots);
            Controls.Add(_snapshotsLabel);
            Controls.Add(_old);
            Controls.Add(_oldLabel);
            Controls.Add(_install);
            Controls.Add(_delete);
            Controls.Add(_import);
            Controls.Add(_refresh);
            Controls.Add(_progress);
            Controls.Add(_status);
        }

        public override async void PageShown()
        {
            UpdateTabs();
            Layout1();

            if (!_loadedOnce)
            {
                _loadedOnce = true;
                await LoadTab(false);
            }
            else if (_tab == TabInstalled)
            {
                _installed = VersionCatalog.Scan();
                RenderList();
            }
            else
            {
                _list.Invalidate();
            }
        }

        // ---------------------------------------------------------------- вкладки

        private async void SwitchTab(int index)
        {
            if (_busy || _tab == index) return;
            _tab = index;
            _search.Text = "";
            _builds.SetItems(new List<object>());
            UpdateTabs();
            Layout1();
            await LoadTab(false);
        }

        private void UpdateTabs()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                _tabs[i].Style = (i == _tab) ? BtnStyle.Accent : BtnStyle.Ghost;
                _tabs[i].Invalidate();
            }

            bool installed = _tab == TabInstalled;

            // снапшоты есть у ванили, Fabric и Quilt; Forge и NeoForge собираются только под релизы
            bool snapshotsUseful = _tab == TabVanilla || _tab == TabFabric || _tab == TabQuilt;
            _snapshots.Visible = _snapshotsLabel.Visible = snapshotsUseful;
            _old.Visible = _oldLabel.Visible = _tab == TabVanilla;

            _builds.Visible = IsLoaderTab;
            _delete.Visible = installed;
            _import.Visible = installed;
            _install.Text = installed ? "Проверить файлы" : "Установить";

            _builds.EmptyText = "Выберите версию слева";
        }

        private async Task LoadTab(bool force)
        {
            if (_busy) return;
            SetBusy(true);
            _cts = new CancellationTokenSource();

            try
            {
                if (_tab == TabInstalled)
                {
                    _installed = VersionCatalog.Scan();
                    SetStatus("Установлено версий: " + _installed.Count);
                    RenderList();
                }
                else
                {
                    SetStatus("Загружаю список версий Mojang…");
                    _progress.SetIndeterminate(true);
                    _remote = await MinecraftMeta.GetManifestAsync(force, _cts.Token);
                    _progress.SetIndeterminate(false);

                    RenderList();

                    if (IsLoaderTab)
                    {
                        SelectLatestRelease();
                        await LoadBuilds();
                    }
                    else
                    {
                        SetStatus("Доступно версий: " + _list.Items.Count);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                SetStatus("Ошибка: " + ex.Message);
                Log.Error("Не удалось загрузить список версий", ex);
            }
            finally
            {
                _progress.SetIndeterminate(false);
                SetBusy(false);
            }
        }

        private void SelectLatestRelease()
        {
            // для Forge Visuals встроенный мод собран под конкретную версию
            string preferred = _tab == TabVisuals ? VisualsVersion : MinecraftMeta.LatestRelease;

            for (int i = 0; i < _list.Items.Count; i++)
            {
                var rv = _list.Items[i] as RemoteVersion;
                if (rv != null && rv.Id == preferred)
                {
                    _list.SelectedIndex = i;
                    return;
                }
            }
            if (_list.Items.Count > 0) _list.SelectedIndex = 0;
        }

        private async Task LoadBuilds()
        {
            var selected = _list.SelectedItem as RemoteVersion;
            if (selected == null)
            {
                _builds.SetItems(new List<object>());
                return;
            }

            string gv = selected.Id;

            if (_buildsCts != null) { try { _buildsCts.Cancel(); } catch { } }
            _buildsCts = new CancellationTokenSource();
            var token = _buildsCts.Token;

            _builds.EmptyText = "Ищу сборки…";
            _builds.SetItems(new List<object>());
            SetStatus("Ищу сборки " + _tabNames[_tab] + " для " + gv + "…");
            _progress.SetIndeterminate(true);

            try
            {
                List<LoaderVersion> found = new List<LoaderVersion>();
                switch (_tab)
                {
                    case TabFabric: found = await LoaderInstaller.GetFabricLoadersAsync(gv, token); break;
                    case TabQuilt: found = await LoaderInstaller.GetQuiltLoadersAsync(gv, token); break;
                    case TabForge:
                    case TabVisuals: found = await LoaderInstaller.GetForgeVersionsAsync(gv, token); break;
                    case TabNeo: found = await LoaderInstaller.GetNeoForgeVersionsAsync(gv, token); break;
                }

                if (token.IsCancellationRequested) return;

                var items = new List<object>();
                foreach (var l in found) items.Add(l);
                _builds.SetItems(items);
                _builds.EmptyText = "Для " + gv + " сборок нет";

                if (items.Count > 0) _builds.SelectedIndex = 0;

                SetStatus(items.Count == 0
                    ? _tabNames[_tab] + " для " + gv + " не выпускался"
                    : "Сборок " + _tabNames[_tab] + " для " + gv + ": " + items.Count);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _builds.EmptyText = "Не удалось загрузить список";
                SetStatus("Ошибка: " + ex.Message);
            }
            finally
            {
                if (!token.IsCancellationRequested) _progress.SetIndeterminate(false);
                UpdateButtons();
            }
        }

        // ---------------------------------------------------------------- списки

        private void RenderList()
        {
            var items = new List<object>();
            string q = (_search.Text ?? "").Trim();

            if (_tab == TabInstalled)
            {
                foreach (var v in _installed)
                {
                    if (q.Length > 0 && v.Id.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    items.Add(v);
                }
            }
            else
            {
                foreach (var v in _remote)
                {
                    if (IsLoaderTab)
                    {
                        // загрузчики ставятся на релизы, Fabric и Quilt — ещё и на снапшоты
                        bool allowSnapshot = (_tab == TabFabric || _tab == TabQuilt) && _snapshots.Checked;
                        if (v.Type == "snapshot" && !allowSnapshot) continue;
                        if (v.Type != "release" && v.Type != "snapshot") continue;
                    }
                    else
                    {
                        if (v.Type == "snapshot" && !_snapshots.Checked) continue;
                        if ((v.Type == "old_beta" || v.Type == "old_alpha") && !_old.Checked) continue;
                    }

                    if (q.Length > 0 && v.Id.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    items.Add(v);
                }
            }

            _list.SetItems(items);
            _list.ScrollToTop();
            UpdateButtons();
        }

        private void DrawLeftItem(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var box = Rectangle.Inflate(r, -4, -3);
            if (box.Width <= 0 || box.Height <= 0) return;

            if (selected) Theme.FillRound(g, box, 10, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 10, Color.FromArgb(16, 255, 255, 255));
            if (selected) Theme.DrawRound(g, box, 10, Color.FromArgb(120, Theme.Accent));

            string main = "", sub = "", badge = "";
            Color badgeColor = Theme.Dim;

            var rv = item as RemoteVersion;
            var iv = item as InstalledVersion;

            if (rv != null)
            {
                main = rv.Id;
                sub = rv.TypeRu + " · " + rv.ReleaseTime.ToString("dd.MM.yyyy");
                if (!IsLoaderTab && VersionCatalog.Exists(rv.Id)) { badge = "УСТАНОВЛЕНА"; badgeColor = Theme.Good; }
                else if (rv.Id == MinecraftMeta.LatestRelease) { badge = "ПОСЛЕДНЯЯ"; badgeColor = Theme.Accent; }
            }
            else if (iv != null)
            {
                main = iv.Id;
                sub = iv.LoaderRu + " · Minecraft " + iv.BaseVersion +
                      (iv.SupportsMods ? " · моды поддерживаются" : "");
                badge = iv.LoaderRu.ToUpperInvariant();
                badgeColor = iv.SupportsMods ? Theme.Accent2 : Theme.Dim;
            }
            else main = item.ToString();

            DrawRow(g, box, main, sub, badge, badgeColor);
        }

        private void DrawBuild(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var lv = item as LoaderVersion;
            if (lv == null) return;

            var box = Rectangle.Inflate(r, -4, -3);
            if (box.Width <= 0 || box.Height <= 0) return;

            if (selected) Theme.FillRound(g, box, 10, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 10, Color.FromArgb(16, 255, 255, 255));
            if (selected) Theme.DrawRound(g, box, 10, Color.FromArgb(120, Theme.Accent));

            string badge = "";
            Color badgeColor = Theme.Dim;
            if (lv.Recommended) { badge = "РЕКОМЕНДУЕТСЯ"; badgeColor = Theme.Good; }
            else if (!lv.Stable) { badge = "BETA"; badgeColor = Theme.Warn; }

            string sub = lv.Recommended
                ? "проверенная сборка, берите её"
                : (lv.Stable ? "стабильная" : "нестабильная, может падать");

            DrawRow(g, box, lv.Version, sub, badge, badgeColor);
        }

        private static void DrawRow(Graphics g, Rectangle box, string main, string sub, string badge, Color badgeColor)
        {
            int badgeW = 0;
            if (!string.IsNullOrEmpty(badge))
            {
                var size = g.MeasureString(badge, Theme.F(8f, FontStyle.Bold));
                badgeW = (int)size.Width + 18;
            }

            int textW = Math.Max(40, box.Width - 30 - badgeW);

            Theme.Text1(g, main, Theme.F(10.5f, FontStyle.Bold), Theme.Text,
                new Rectangle(box.X + 16, box.Y + 8, textW, 20));
            Theme.Text1(g, sub, Theme.F(8.8f), Theme.Muted,
                new Rectangle(box.X + 16, box.Y + 27, textW, 18));

            if (badgeW > 0)
            {
                var br = new Rectangle(box.Right - badgeW - 12, box.Y + (box.Height - 22) / 2, badgeW, 22);
                Theme.FillRound(g, br, 11, Color.FromArgb(40, badgeColor));
                Theme.Text1(g, badge, Theme.F(8f, FontStyle.Bold), badgeColor, br,
                    StringAlignment.Center, StringAlignment.Center);
            }
        }

        private void UpdateButtons()
        {
            bool ready = IsLoaderTab
                ? (_list.SelectedItem != null && _builds.SelectedItem != null)
                : _list.SelectedItem != null;

            _install.Enabled = ready && !_busy;
            _delete.Enabled = _list.SelectedItem != null && !_busy && _tab == TabInstalled;
            _install.Invalidate();
            _delete.Invalidate();
        }

        // ---------------------------------------------------------------- действия

        private async void InstallClick(object sender, EventArgs e)
        {
            if (_busy) return;

            var rv = _list.SelectedItem as RemoteVersion;
            var iv = _list.SelectedItem as InstalledVersion;
            var lv = _builds.SelectedItem as LoaderVersion;

            if (IsLoaderTab && (rv == null || lv == null))
            {
                SetStatus("Выберите версию слева и сборку справа.");
                return;
            }
            if (!IsLoaderTab && rv == null && iv == null) return;

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
                string versionId = null;

                if (IsLoaderTab)
                {
                    string gv = rv.Id;
                    switch (_tab)
                    {
                        case TabFabric:
                            SetStatus("Ставлю Fabric " + lv.Version + " для " + gv + "…");
                            versionId = await LoaderInstaller.InstallFabricAsync(gv, lv.Version, _cts.Token);
                            break;
                        case TabQuilt:
                            SetStatus("Ставлю Quilt " + lv.Version + " для " + gv + "…");
                            versionId = await LoaderInstaller.InstallQuiltAsync(gv, lv.Version, _cts.Token);
                            break;
                        case TabForge:
                            SetStatus("Ставлю Forge " + lv.Version + "…");
                            versionId = await LoaderInstaller.InstallForgeAsync(lv.Version, progress, _cts.Token);
                            break;
                        case TabVisuals:
                            {
                                SetStatus("Ставлю Forge " + lv.Version + " под сборку Visuals…");
                                string forgeId = await LoaderInstaller.InstallForgeAsync(lv.Version, progress, _cts.Token);
                                await VersionInstaller.InstallAsync(forgeId, progress, _cts.Token);
                                versionId = LoaderInstaller.CreateVisualsProfile(forgeId, gv);
                                break;
                            }
                        case TabNeo:
                            SetStatus("Ставлю NeoForge " + lv.Version + "…");
                            versionId = await LoaderInstaller.InstallNeoForgeAsync(lv.Version, progress, _cts.Token);
                            break;
                    }
                }
                else versionId = rv != null ? rv.Id : iv.Id;

                if (string.IsNullOrEmpty(versionId)) return;

                await VersionInstaller.InstallAsync(versionId, progress, _cts.Token);

                string extra = "";
                if (versionId.EndsWith(LoaderInstaller.VisualsSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    int installedMods = BundledMods.InstallInto(versionId, SetStatus);
                    extra = installedMods > 0
                        ? " Встроенных модов добавлено: " + installedMods + "."
                        : " Внимание: встроенных модов не найдено — положите .jar в Resources\\Mods.";
                }

                Settings.Current.SelectedVersion = versionId;
                Settings.Save();

                SetStatus("Готово: " + versionId + "." + extra + " Версия выбрана на вкладке «Играть».");
                _installed = VersionCatalog.Scan();
                if (_tab == TabInstalled) RenderList(); else _list.Invalidate();

                Host.Home.ReloadVersions();
            }
            catch (OperationCanceledException)
            {
                SetStatus("Отменено");
            }
            catch (Exception ex)
            {
                Host.ShowError("Не удалось установить версию", ex);
                SetStatus("Ошибка: " + ex.Message);
            }
            finally
            {
                _progress.SetIndeterminate(false);
                SetBusy(false);
            }
        }

        private void DeleteClick(object sender, EventArgs e)
        {
            var iv = _list.SelectedItem as InstalledVersion;
            if (iv == null) return;

            if (!Host.Confirm("Удалить версию «" + iv.Id + "»?\n\nПапка версии будет удалена. " +
                              "Сохранения и моды этой сборки останутся на месте."))
                return;

            VersionCatalog.Delete(iv.Id, false);
            _installed = VersionCatalog.Scan();
            RenderList();
            Host.Home.ReloadVersions();
            SetStatus("Версия удалена: " + iv.Id);
        }

        private void ImportClick(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Выберите .json версии или ZIP-архив папки версии";
                dlg.Filter = "Версия Minecraft (*.json;*.zip)|*.json;*.zip|Все файлы (*.*)|*.*";
                if (dlg.ShowDialog(Host) != DialogResult.OK) return;

                try
                {
                    string id = VersionCatalog.Import(dlg.FileName);
                    _installed = VersionCatalog.Scan();
                    RenderList();
                    Host.Home.ReloadVersions();
                    SetStatus("Импортирована версия: " + id);
                }
                catch (Exception ex)
                {
                    Host.ShowError("Не удалось импортировать версию", ex);
                }
            }
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            foreach (var t in _tabs) t.Enabled = !busy;
            _refresh.Enabled = !busy;
            _import.Enabled = !busy;
            UpdateButtons();
            if (!busy) _progress.SetIndeterminate(false);
        }

        private void SetStatus(string text) { _status.Text = text ?? ""; }

        // ---------------------------------------------------------------- вёрстка

        private int _listTop = 190;

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Layout1();
        }

        private void Layout1()
        {
            int pad = 28;
            int right = Math.Max(pad + 220, Width - pad);
            int x = pad;
            int tabsY = 62;

            // вкладок стало много — переносим на вторую строку, если не влезают
            foreach (var t in _tabs)
            {
                if (x > pad && x + t.Width > right)
                {
                    x = pad;
                    tabsY += 44;
                }
                t.Location = new Point(x, tabsY);
                x += t.Width + 8;
            }

            int toolY = tabsY + 50;
            _listTop = toolY + 66;

            _search.SetBounds(pad, toolY, 300, 38);
            _snapshots.Location = new Point(pad + 314, toolY + 6);
            _snapshotsLabel.SetBounds(pad + 366, toolY + 6, 110, 26);
            _old.Location = new Point(pad + 484, toolY + 6);
            _oldLabel.SetBounds(pad + 536, toolY + 6, 180, 26);

            int listY = _listTop;
            int bottomH = 92;
            int listH = Math.Max(60, Height - listY - bottomH);
            int total = Math.Max(200, Width - pad * 2);

            if (IsLoaderTab)
            {
                int gap = 16;
                int leftW = (int)(total * 0.42);
                _list.SetBounds(pad, listY, leftW, listH);
                _builds.SetBounds(pad + leftW + gap, listY, Math.Max(100, total - leftW - gap), listH);
            }
            else
            {
                _list.SetBounds(pad, listY, total, listH);
            }

            int by = Height - bottomH + 18;
            _install.Location = new Point(pad, by);
            _delete.Location = new Point(pad + 200, by);
            _import.Location = new Point(pad + (_delete.Visible ? 360 : 200), by);
            _refresh.Location = new Point(Math.Max(pad, Width - pad - _refresh.Width), by);

            _progress.SetBounds(pad, Height - 22, Math.Max(50, Width - pad * 2), 6);
            _status.SetBounds(pad, by + 46, Math.Max(50, Width - pad * 2), 20);

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.Smooth(g);

            Theme.Text1(g, "Версии", Theme.F(17f, FontStyle.Bold), Theme.Text,
                new Rectangle(28, 16, 400, 28));
            Theme.Text1(g, "Выберите тип сверху, затем версию в списке",
                Theme.F(9.5f), Theme.Muted, new Rectangle(28, 40, 700, 20));

            // подписи над списками
            int capY = _listTop - 24;
            if (IsLoaderTab)
            {
                Theme.Text1(g, "ВЕРСИЯ MINECRAFT", Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                    new Rectangle(_list.Left, capY, 300, 20));
                Theme.Text1(g, "СБОРКА " + _tabNames[_tab].ToUpperInvariant(), Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                    new Rectangle(_builds.Left, capY, 300, 20));
            }
            else
            {
                Theme.Text1(g, _tab == TabInstalled ? "УСТАНОВЛЕННЫЕ СБОРКИ" : "ВЕРСИЯ MINECRAFT",
                    Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                    new Rectangle(_list.Left, capY, 400, 20));
            }
        }
    }
}
