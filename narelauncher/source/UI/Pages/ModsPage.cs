using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI.Pages
{
    public class ModsPage : PageBase
    {
        /// <summary>Слева: установленные сборки. Никаких выпадающих списков.</summary>
        private readonly VirtualList _versions = new VirtualList { ItemHeight = 54, EmptyText = "Нет установленных версий" };

        private readonly NareLabel _loaderInfo = new NareLabel("", 8.8f, FontStyle.Regular, Theme.Dim);

        private readonly List<NareButton> _sourceTabs = new List<NareButton>();
        private IModSource _source = ModSources.ById(Settings.Current.ModSourceId);

        private readonly List<NareButton> _typeTabs = new List<NareButton>();
        private string _type = "mod";

        private readonly NareTextBox _search = new NareTextBox { Placeholder = "Поиск на Modrinth…", Icon = Ico.Search };
        private readonly NareButton _find = new NareButton { Text = "Найти", Style = BtnStyle.Ghost, Size = new Size(104, 38) };
        private readonly NareButton _more = new NareButton { Text = "Ещё", Style = BtnStyle.Ghost, Size = new Size(84, 38) };

        private readonly VirtualList _catalog = new VirtualList { ItemHeight = 66, EmptyText = "Ничего не найдено" };
        private readonly VirtualList _local = new VirtualList { ItemHeight = 54, EmptyText = "Папка пуста" };

        private readonly NareButton _install = new NareButton { Text = "Установить", Style = BtnStyle.Accent, Icon = Ico.Download, Size = new Size(172, 42) };
        private readonly NareButton _addFile = new NareButton { Text = "Добавить файл", Style = BtnStyle.Ghost, Icon = Ico.Plus, Size = new Size(168, 42) };
        private readonly NareButton _openFolder = new NareButton { Text = "Папка", Style = BtnStyle.Ghost, Icon = Ico.Folder, Size = new Size(116, 42) };
        private readonly NareButton _toggle = new NareButton { Text = "Вкл / Выкл", Style = BtnStyle.Ghost, Size = new Size(134, 42) };
        private readonly NareButton _delete = new NareButton { Text = "Удалить", Style = BtnStyle.Danger, Icon = Ico.Trash, Size = new Size(134, 42) };
        private readonly NareButton _check = new NareButton { Text = "Проверить", Style = BtnStyle.Ghost, Size = new Size(126, 42) };

        private readonly NareProgress _progress = new NareProgress();
        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);

        private readonly List<ModHit> _hits = new List<ModHit>();
        private CancellationTokenSource _cts;
        private bool _busy;
        private int _offset;

        private InstalledVersion Current { get { return _versions.SelectedItem as InstalledVersion; } }

        public ModsPage()
        {
            Padding = new Padding(0);

            foreach (var src in ModSources.All)
            {
                IModSource source = src;
                var b = new NareButton
                {
                    Text = src.Title,
                    Style = src.Id == _source.Id ? BtnStyle.Accent : BtnStyle.Ghost,
                    Size = new Size(Math.Max(104, 26 + src.Title.Length * 8), 30),
                    Radius = 8,
                    Font = Theme.F(9f, FontStyle.Bold)
                };
                b.Click += async (s2, e2) =>
                {
                    if (_source.Id == source.Id) return;
                    _source = source;
                    Settings.Current.ModSourceId = source.Id;
                    Settings.Save();
                    UpdateSourceTabs();
                    UpdateHint();
                    await Search(true);          // каталог подтягивается сам
                };
                _sourceTabs.Add(b);
                Controls.Add(b);
            }

            foreach (var t in ModrinthApi.ProjectTypes)
            {
                string type = t;
                var b = new NareButton
                {
                    Text = ModrinthApi.ProjectTypeRu(t),
                    Style = t == _type ? BtnStyle.Accent : BtnStyle.Ghost,
                    Size = new Size(122, 34),
                    Radius = 9,
                    Font = Theme.F(9.5f, FontStyle.Bold)
                };
                b.Click += async (s, e) =>
                {
                    if (_type == type) return;
                    _type = type;
                    UpdateTypeTabs();
                    RefreshLocal();
                    await Search(true);
                };
                _typeTabs.Add(b);
                Controls.Add(b);
            }

            _versions.Renderer = DrawVersion;
            _versions.SelectionChanged += async (s, e) =>
            {
                UpdateLoaderInfo();
                RefreshLocal();
                await Search(true);
            };

            _catalog.Renderer = DrawHit;
            _catalog.SelectionChanged += (s, e) => UpdateButtons();
            _catalog.ItemActivated += (s, e) => InstallClick(null, EventArgs.Empty);

            _local.Renderer = DrawLocal;

            // картинки читаются в фоне: как догрузились — перерисовываем список
            ModIcons.Loaded += delegate
            {
                try
                {
                    if (IsHandleCreated) BeginInvoke((Action)(() => _local.Invalidate()));
                }
                catch { }
            };
            WebIcons.Loaded += delegate
            {
                try
                {
                    if (IsHandleCreated) BeginInvoke((Action)(() => _catalog.Invalidate()));
                }
                catch { }
            };

            _local.SelectionChanged += (s, e) => UpdateButtons();

            _find.Click += async (s, e) => await Search(true);
            _more.Click += async (s, e) => await Search(false);
            _search.Inner.KeyDown += async (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                await Search(true);
            };

            _install.Click += InstallClick;
            _toggle.Click += (s, e) =>
            {
                var m = _local.SelectedItem as LocalMod;
                if (m == null) return;
                ModManager.Toggle(m);
                RefreshLocal();
            };
            _delete.Click += (s, e) =>
            {
                var m = _local.SelectedItem as LocalMod;
                if (m == null) return;
                if (!Host.Confirm("Удалить «" + m.FileName + "»?")) return;
                ModManager.Delete(m);
                RefreshLocal();
            };
            _addFile.Click += AddFileClick;
            _openFolder.Click += (s, e) =>
            {
                if (Current != null) ModManager.OpenFolder(_type, Current.Id);
            };

            Controls.Add(_versions);
            Controls.Add(_catalog);
            Controls.Add(_local);
            Controls.Add(_loaderInfo);
            Controls.Add(_search);
            Controls.Add(_find);
            Controls.Add(_more);
            Controls.Add(_install);
            // мод можно просто перетащить в окно: лаунчер сам прочитает jar
            // и положит его в ту сборку, для которой он собран
            AllowDrop = true;
            DragEnter += (s, e) =>
            {
                e.Effect = Dropped(e).Length > 0 ? DragDropEffects.Copy : DragDropEffects.None;
            };
            DragOver += (s, e) =>
            {
                e.Effect = Dropped(e).Length > 0 ? DragDropEffects.Copy : DragDropEffects.None;
            };
            DragDrop += (s, e) => TakeDrop(Dropped(e));

            _check.Click += delegate
            {
                var v = Current;
                if (v == null) { SetStatus("Сначала выберите сборку."); return; }

                using (var form = new ModCheckForm(_type, v))
                {
                    form.ShowDialog(FindForm());
                    if (form.Changed) RefreshLocal();
                }
            };

            Controls.Add(_addFile);
            Controls.Add(_openFolder);
            Controls.Add(_toggle);
            Controls.Add(_delete);
            Controls.Add(_check);
            Controls.Add(_progress);
            Controls.Add(_status);
        }

        public override async void PageShown()
        {
            Layout1();
            bool first = _versions.Items.Count == 0;
            ReloadVersions();
            UpdateLoaderInfo();
            UpdateHint();
            RefreshLocal();
            if (first || _hits.Count == 0) await Search(true);
        }

        private void ReloadVersions()
        {
            var all = VersionCatalog.Scan();
            var items = new List<object>();
            foreach (var v in all) items.Add(v);

            int keep = -1;
            var previous = Current;
            string wanted = previous != null ? previous.Id : Settings.Current.SelectedVersion;

            _versions.SetItems(items);

            for (int i = 0; i < items.Count; i++)
            {
                var v = items[i] as InstalledVersion;
                if (v != null && string.Equals(v.Id, wanted, StringComparison.OrdinalIgnoreCase)) { keep = i; break; }
            }
            if (keep < 0 && items.Count > 0) keep = 0;
            if (keep >= 0) _versions.SelectedIndex = keep;
        }

        private void UpdateSourceTabs()
        {
            foreach (var b in _sourceTabs)
            {
                bool active = b.Text == _source.Title;
                b.Style = active ? BtnStyle.Accent : BtnStyle.Ghost;
                b.Invalidate();
            }
            Invalidate();
        }

        private void UpdateTypeTabs()
        {
            for (int i = 0; i < _typeTabs.Count; i++)
            {
                _typeTabs[i].Style = ModrinthApi.ProjectTypes[i] == _type ? BtnStyle.Accent : BtnStyle.Ghost;
                _typeTabs[i].Invalidate();
            }
        }

        private void UpdateLoaderInfo()
        {
            var v = Current;
            if (v == null)
            {
                _loaderInfo.Text = "Слева нет ни одной сборки — установите версию на вкладке «Версии».";
                _loaderInfo.ForeColor = Theme.Warn;
                return;
            }

            if (_type == "mod" && !v.SupportsMods)
            {
                _loaderInfo.Text = "В «" + v.Id + "» нет модлоадера — поставьте Fabric или Forge.";
                _loaderInfo.ForeColor = Theme.Warn;
                return;
            }

            string note = "";
            if (_type == "shader") note = "   ·   нужен Iris или OptiFine";
            else if (_type == "datapack") note = "   ·   потом скопируйте в saves\\<мир>\\datapacks";

            _loaderInfo.Text = ModrinthApi.TargetDir(_type, v.Id) + note;
            _loaderInfo.ForeColor = Theme.Dim;
        }

        // ---------------------------------------------------------------- поиск

        private async Task Search(bool reset)
        {
            if (_busy) return;
            var v = Current;
            if (v == null) { SetStatus("Сначала установите хотя бы одну версию."); return; }

            if (_type == "mod" && !v.SupportsMods)
            {
                _hits.Clear();
                _catalog.SetItems(new List<object>());
                SetStatus("Для этой сборки нет модлоадера.");
                return;
            }

            string problem;
            if (!_source.Ready(out problem))
            {
                _hits.Clear();
                _catalog.SetItems(new List<object>());
                _catalog.EmptyText = problem;
                SetStatus(problem);
                return;
            }

            if (!_source.Supports(_type))
            {
                _hits.Clear();
                _catalog.SetItems(new List<object>());
                _catalog.EmptyText = _source.Title + " не отдаёт этот тип содержимого";
                SetStatus(_catalog.EmptyText);
                return;
            }

            _catalog.EmptyText = "Ничего не найдено";

            SetBusy(true);
            _cts = new CancellationTokenSource();
            _progress.SetIndeterminate(true);

            if (reset) { _offset = 0; _hits.Clear(); }

            try
            {
                SetStatus("Загружаю из источника «" + _source.Title + "»…");
                var found = await _source.SearchAsync(_search.Text, _type, v.BaseVersion,
                                                      v.ModrinthLoader, _offset, 20, _cts.Token);
                _hits.AddRange(found);
                _offset += found.Count;

                var items = new List<object>();
                foreach (var h in _hits) items.Add(h);
                _catalog.SetItems(items);

                if (_hits.Count == 0)
                {
                    string what = ModrinthApi.ProjectTypeRu(_type).ToLowerInvariant();
                    string where = "Minecraft " + v.BaseVersion
                                 + (v.SupportsMods ? " (" + v.LoaderRu + ")" : "");

                    string miss = ModJar.LooksLikeGameVersion(v.BaseVersion)
                        ? "Выберите другую версию — " + what + " на " + where + " тут нет."
                        : "Не понял, какая версия игры у сборки «" + v.Id +
                          "» — выберите другую версию.";

                    _catalog.EmptyText = miss;
                    SetStatus(_source.Title + ": " + miss);
                }
                else SetStatus(_source.Title + " — найдено: " + _hits.Count);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                SetStatus("Ошибка источника: " + ex.Message);
                Log.Error("Поиск в источнике " + _source.Title + " не удался", ex);
            }
            finally
            {
                _progress.SetIndeterminate(false);
                SetBusy(false);
            }
        }

        private async void InstallClick(object sender, EventArgs e)
        {
            if (_busy) return;
            var hit = _catalog.SelectedItem as ModHit;
            var v = Current;
            if (hit == null || v == null) return;

            SetBusy(true);
            _cts = new CancellationTokenSource();
            _progress.SetIndeterminate(true);

            try
            {
                SetStatus("Ищу подходящий файл для " + hit.Title + "…");
                var files = await _source.GetFilesAsync(hit, v.BaseVersion, v.ModrinthLoader, _cts.Token);
                if (files.Count == 0)
                {
                    SetStatus("В источнике нет файла «" + hit.Title + "» под Minecraft " + v.BaseVersion + ".");
                    return;
                }

                var installed = await _source.InstallAsync(
                    files[0], _type, v.Id, v.BaseVersion, v.ModrinthLoader,
                    text => SetStatus(text), _cts.Token);

                RefreshLocal();
                SetStatus("Установлено: " + string.Join(", ", installed.ToArray()));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Host.ShowError("Не удалось установить", ex);
                SetStatus("Ошибка: " + ex.Message);
            }
            finally
            {
                _progress.SetIndeterminate(false);
                SetBusy(false);
            }
        }

        private void AddFileClick(object sender, EventArgs e)
        {
            var v = Current;
            if (v == null) return;

            using (var dlg = new OpenFileDialog())
            {
                dlg.Multiselect = true;
                dlg.Title = "Выберите файлы";
                dlg.Filter = _type == "mod"
                    ? "Моды (*.jar)|*.jar|Все файлы (*.*)|*.*"
                    : "Архивы (*.zip)|*.zip|Все файлы (*.*)|*.*";

                if (dlg.ShowDialog(Host) != DialogResult.OK) return;

                try
                {
                    foreach (var f in dlg.FileNames)
                        ModManager.AddLocalFile(f, _type, v.Id);
                    RefreshLocal();
                    SetStatus("Добавлено файлов: " + dlg.FileNames.Length);
                }
                catch (Exception ex) { Host.ShowError("Не удалось скопировать файлы", ex); }
            }
        }

        // ----------------------------------------------------- перетаскивание

        private string[] Dropped(DragEventArgs e)
        {
            var good = new List<string>();
            try
            {
                if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return new string[0];

                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files == null) return new string[0];

                foreach (var f in files)
                {
                    if (f == null) continue;
                    string ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
                    if (ext == ".jar" || ext == ".zip") good.Add(f);
                }
            }
            catch { return new string[0]; }

            return good.ToArray();
        }

        /// <summary>Куда положить этот файл: ищем сборку, для которой он собран.</summary>
        private InstalledVersion Target(ModJar about)
        {
            var here = Current;
            if (here != null && about.Fits(here.BaseVersion, here.ModrinthLoader) != false) return here;

            InstalledVersion guess = null;
            foreach (var v in VersionCatalog.Scan())
            {
                if (!v.SupportsMods) continue;
                if (about.Fits(v.BaseVersion, v.ModrinthLoader) == true) { guess = v; break; }
            }

            return guess;
        }

        private void TakeDrop(string[] files)
        {
            if (files == null || files.Length == 0) return;

            int done = 0;
            string last = "";

            foreach (var file in files)
            {
                string ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                string type = ext == ".jar" ? "mod" : _type;

                var about = type == "mod" ? ModJar.Read(file) : new ModJar();
                var target = type == "mod" ? Target(about) : Current;

                if (target == null)
                {
                    var here = Current;
                    string what = about.Known ? "Мод для " + about.Short : "Мод о себе ничего не пишет";

                    if (here == null)
                    {
                        SetStatus(what + ", а сборка не выбрана.");
                        return;
                    }

                    var answer = MessageBox.Show(FindForm(),
                        what + ", а подходящей сборки нет.\n\nВсё равно положить в «" + here.Id + "»?",
                        "Мод не для этой сборки", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                    if (answer != DialogResult.Yes) continue;
                    target = here;
                }

                try
                {
                    ModManager.AddLocalFile(file, type, target.Id);
                    done++;
                    last = System.IO.Path.GetFileName(file) + " → " + target.Id;
                    Log.Info("Перетащили: " + last);
                }
                catch (Exception ex)
                {
                    Host.ShowError("Не удалось положить " + System.IO.Path.GetFileName(file), ex);
                }
            }

            if (done > 0)
            {
                RefreshLocal();
                SetStatus(done == 1 ? "Готово: " + last : "Положено файлов: " + done + ".");
            }
        }

        /// <summary>Подсказка в поиске: у каждого источника она своя.</summary>
        private void UpdateHint()
        {
            switch (_source.Id)
            {
                case "url": _search.Placeholder = "Вставьте ссылку на .jar или .zip…"; break;
                case "github": _search.Placeholder = "Название мода или автор на GitHub…"; break;
                case "nare": _search.Placeholder = "Поиск в нашем списке…"; break;
                case "local": _search.Placeholder = "Имя файла в папке…"; break;
                default: _search.Placeholder = "Поиск на " + _source.Title + "…"; break;
            }
            _search.Invalidate();
        }

        private void RefreshLocal()
        {
            var v = Current;
            var items = new List<object>();
            if (v != null)
                foreach (var m in ModManager.List(_type, v.Id)) items.Add(m);
            _local.SetItems(items);
            UpdateLoaderInfo();
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            bool hasLocal = _local.SelectedItem != null;
            _toggle.Enabled = hasLocal;
            _delete.Enabled = hasLocal;
            _check.Enabled = Current != null && _type == "mod";
            _install.Enabled = !_busy && _catalog.SelectedItem != null;
            _toggle.Invalidate();
            _delete.Invalidate();
            _install.Invalidate();
        }

        // ---------------------------------------------------------------- отрисовка

        private void DrawVersion(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var v = item as InstalledVersion;
            if (v == null) return;

            var box = Rectangle.Inflate(r, -4, -3);
            if (box.Width <= 0 || box.Height <= 0) return;

            if (selected) Theme.FillRound(g, box, 10, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 10, Color.FromArgb(16, 255, 255, 255));
            if (selected) Theme.DrawRound(g, box, 10, Color.FromArgb(120, Theme.Accent));

            Theme.Text1(g, v.Id, Theme.F(9.8f, FontStyle.Bold), Theme.Text,
                new Rectangle(box.X + 14, box.Y + 8, box.Width - 24, 19));
            Theme.Text1(g, v.LoaderRu + " · " + v.BaseVersion,
                Theme.F(8.5f), v.SupportsMods ? Theme.Accent2 : Theme.Muted,
                new Rectangle(box.X + 14, box.Y + 27, box.Width - 24, 17));
        }

        private void DrawHit(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var hit = item as ModHit;
            if (hit == null) return;

            var box = Rectangle.Inflate(r, -4, -3);
            if (box.Width <= 0 || box.Height <= 0) return;

            if (selected) Theme.FillRound(g, box, 10, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 10, Color.FromArgb(16, 255, 255, 255));
            if (selected) Theme.DrawRound(g, box, 10, Color.FromArgb(120, Theme.Accent));

            var icon = new Rectangle(box.X + 12, box.Y + 10, 38, 38);
            var picture = WebIcons.For(hit.IconUrl);

            if (picture != null)
            {
                var saved = g.InterpolationMode;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                try { g.DrawImage(picture, icon); } catch { }
                g.InterpolationMode = saved;
            }
            else
            {
                // пока картинка не приехала — квадратик с первой буквой
                Theme.FillRoundGradient(g, icon, 9, Color.FromArgb(60, Theme.Accent), Color.FromArgb(60, Theme.Accent2));
                string letter = string.IsNullOrEmpty(hit.Title) ? "?" : hit.Title.Substring(0, 1).ToUpperInvariant();
                Theme.Text1(g, letter, Theme.F(13f, FontStyle.Bold), Color.White, icon,
                    StringAlignment.Center, StringAlignment.Center);
            }

            int textW = Math.Max(60, box.Width - icon.Width - 118);
            Theme.Text1(g, hit.Title, Theme.F(10.3f, FontStyle.Bold), Theme.Text,
                new Rectangle(icon.Right + 12, box.Y + 9, textW, 20));
            Theme.Text1(g, hit.Description, Theme.F(8.6f), Theme.Muted,
                new Rectangle(icon.Right + 12, box.Y + 29, textW, 18));
            Theme.Text1(g, hit.Author, Theme.F(8.3f), Theme.Dim,
                new Rectangle(icon.Right + 12, box.Y + 46, textW, 16));

            Theme.Text1(g, "↓ " + hit.DownloadsText, Theme.F(9f, FontStyle.Bold), Theme.Accent,
                new Rectangle(box.Right - 104, box.Y, 92, box.Height),
                StringAlignment.Far, StringAlignment.Center);
        }

        private void DrawLocal(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var m = item as LocalMod;
            if (m == null) return;

            var box = Rectangle.Inflate(r, -4, -2);
            if (box.Width <= 0 || box.Height <= 0) return;

            if (selected) Theme.FillRound(g, box, 8, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 8, Color.FromArgb(16, 255, 255, 255));

            string name = m.FileName;
            if (name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 9);

            // картинка мода, а пока её нет — квадратик с первой буквой
            var icon = new Rectangle(box.X + 10, box.Y + (box.Height - 34) / 2, 34, 34);
            var picture = ModIcons.For(m.Path);

            if (picture != null)
            {
                var saved = g.InterpolationMode;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                try { g.DrawImage(picture, icon); } catch { }
                g.InterpolationMode = saved;
                if (!m.Enabled)
                    using (var veil = new SolidBrush(Color.FromArgb(150, 12, 15, 22)))
                        g.FillRectangle(veil, icon);
            }
            else
            {
                Theme.FillRound(g, icon, 8, Color.FromArgb(44, Theme.Accent));
                string letter = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
                Theme.Text1(g, letter, Theme.F(12f, FontStyle.Bold),
                    m.Enabled ? Theme.Text : Theme.Dim, icon,
                    StringAlignment.Center, StringAlignment.Center);
            }

            var dot = new Rectangle(icon.Right + 8, box.Y + box.Height / 2 - 4, 8, 8);
            using (var b = new SolidBrush(m.Enabled ? Theme.Good : Theme.Dim))
                g.FillEllipse(b, dot);

            int textWidth = Math.Max(40, box.Right - dot.Right - 82);

            // мод сам пишет внутри jar, для какой версии игры он собран —
            // показываем это прямо в списке, чтобы не ставить лишнего
            var about = _type == "mod" ? ModJar.Read(m.Path) : new ModJar();
            var build = Current;
            bool? fits = build == null ? null : about.Fits(build.BaseVersion, build.ModrinthLoader);

            string sub = about.Known ? about.Short : "";
            var subColor = Theme.Dim;

            if (fits == false)
            {
                sub = "не для этой сборки · " + about.Short;
                subColor = Theme.Bad;
            }

            bool two = sub != "";

            Theme.Text1(g, name, Theme.F(9.2f, m.Enabled ? FontStyle.Regular : FontStyle.Italic),
                m.Enabled ? Theme.Text : Theme.Dim,
                two ? new Rectangle(dot.Right + 10, box.Y + 7, textWidth, 20)
                    : new Rectangle(dot.Right + 10, box.Y, textWidth, box.Height));

            if (two)
                Theme.Text1(g, sub, Theme.F(8.2f), subColor,
                    new Rectangle(dot.Right + 10, box.Y + 27, textWidth, 18));

            Theme.Text1(g, m.SizeText, Theme.F(8.4f), Theme.Dim,
                new Rectangle(box.Right - 72, box.Y, 60, box.Height),
                StringAlignment.Far, StringAlignment.Center);
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _find.Enabled = !busy;
            _more.Enabled = !busy;
            UpdateButtons();
        }

        private void SetStatus(string text) { _status.Text = text ?? ""; }

        // ---------------------------------------------------------------- вёрстка

        private const int ListTop = 226;
        private const int BottomH = 100;

        private int _catalogLeft, _localLeft;

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Layout1();
        }

        private void Layout1()
        {
            int pad = 28;

            int wide = 0;
            foreach (var b in _sourceTabs) wide += b.Width;

            int gapS = 8;
            if (wide + gapS * (_sourceTabs.Count - 1) > Width - pad * 2) gapS = 4;

            int sx = pad;
            foreach (var b in _sourceTabs)
            {
                b.Location = new Point(sx, 78);
                sx += b.Width + gapS;
            }

            int tx = pad;
            foreach (var t in _typeTabs)
            {
                t.Location = new Point(tx, 118);
                tx += t.Width + 8;
            }

            int searchY = 160;
            _search.SetBounds(pad, searchY, 300, 38);
            _find.Location = new Point(pad + 312, searchY);
            _more.Location = new Point(pad + 424, searchY);
            _loaderInfo.SetBounds(pad + 520, searchY + 10, Math.Max(80, Width - pad - 520 - pad), 18);

            int total = Math.Max(240, Width - pad * 2);
            int gap = 14;
            int avail = total - gap * 2;
            int vw = Math.Max(150, (int)(avail * 0.24));
            int cw = Math.Max(200, (int)(avail * 0.44));
            int iw = Math.Max(140, avail - vw - cw);

            int listH = Math.Max(60, Height - ListTop - BottomH);
            _catalogLeft = pad + vw + gap;
            _localLeft = _catalogLeft + cw + gap;

            _versions.SetBounds(pad, ListTop, vw, listH);
            _catalog.SetBounds(_catalogLeft, ListTop, cw, listH);
            _local.SetBounds(_localLeft, ListTop, iw, listH);

            int by = Height - BottomH + 12;
            _install.Location = new Point(_catalogLeft, by);
            _addFile.Location = new Point(_catalogLeft + 182, by);
            _openFolder.Location = new Point(_catalogLeft + 360, by);
            _toggle.Location = new Point(_localLeft, by);
            _delete.Location = new Point(_localLeft + 144, by);
            _check.Location = new Point(_localLeft + 288, by);

            _progress.SetBounds(pad, Height - 38, Math.Max(50, Width - pad * 2), 6);
            _status.SetBounds(pad, Height - 28, Math.Max(50, Width - pad * 2), 20);

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.Smooth(g);

            Theme.Text1(g, "Моды, ресурспаки и шейдеры", Theme.F(17f, FontStyle.Bold), Theme.Text,
                new Rectangle(28, 12, Math.Max(100, Width - 56), 30));
            Theme.Text1(g, "Выберите сборку слева — моды ставятся в её собственную папку",
                Theme.F(9.5f), Theme.Muted, new Rectangle(28, 42, Math.Max(100, Width - 56), 16));

            Theme.Text1(g, "ИСТОЧНИК", Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                new Rectangle(28, 60, 200, 13));

            int capY = ListTop - 22;
            Theme.Text1(g, "СБОРКА", Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                new Rectangle(28, capY, 200, 18));
            Theme.Text1(g, "КАТАЛОГ MODRINTH", Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                new Rectangle(_catalogLeft, capY, 260, 18));
            Theme.Text1(g, "УСТАНОВЛЕНО", Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                new Rectangle(_localLeft, capY, 220, 18));
        }
    }
}
