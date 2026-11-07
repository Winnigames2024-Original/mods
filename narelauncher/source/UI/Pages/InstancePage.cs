using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI.Pages
{
    /// <summary>
    /// Всё содержимое выбранной сборки в одном месте: миры, скриншоты,
    /// ресурспаки, шейдеры и логи. Плюс экспорт всей сборки одним файлом
    /// и импорт обратно — чтобы переносить её между компьютерами.
    /// </summary>
    public class InstancePage : PageBase
    {
        private const int TabWorlds = 0, TabShots = 1, TabPacks = 2, TabShaders = 3, TabLogs = 4;

        /// <summary>Строка списка: файл или папка внутри сборки.</summary>
        private sealed class Entry
        {
            public string Name = "";
            public string Path = "";
            public long Size;
            public DateTime Time;
            public bool Directory;
            public bool Crash;
        }

        private readonly string[] _tabNames = { "Миры", "Скриншоты", "Ресурспаки", "Шейдеры", "Логи" };
        private readonly List<NareButton> _tabs = new List<NareButton>();
        private int _tab = TabWorlds;

        private readonly NareTextBox _search = new NareTextBox { Placeholder = "Поиск…", Icon = Ico.Search };
        private readonly VirtualList _list = new VirtualList { ItemHeight = 52, EmptyText = "Пусто" };

        private readonly NareButton _open = new NareButton { Text = "Папка", Style = BtnStyle.Ghost, Icon = Ico.Folder, Size = new Size(120, 42) };
        private readonly NareButton _add = new NareButton { Text = "Добавить", Style = BtnStyle.Ghost, Icon = Ico.Plus, Size = new Size(140, 42) };
        private readonly NareButton _backup = new NareButton { Text = "Копия в архив", Style = BtnStyle.Ghost, Icon = Ico.Download, Size = new Size(176, 42) };
        private readonly NareButton _delete = new NareButton { Text = "Удалить", Style = BtnStyle.Danger, Icon = Ico.Trash, Size = new Size(140, 42) };
        private readonly NareButton _export = new NareButton { Text = "Экспорт сборки", Style = BtnStyle.Ghost, Icon = Ico.Download, Size = new Size(184, 42) };
        private readonly NareButton _import = new NareButton { Text = "Импорт", Style = BtnStyle.Ghost, Icon = Ico.Plus, Size = new Size(132, 42) };
        private readonly NareButton _refresh = new NareButton { Text = "Обновить", Style = BtnStyle.Ghost, Icon = Ico.Refresh, Size = new Size(140, 42) };

        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);

        private readonly List<Entry> _all = new List<Entry>();

        /// <summary>Картинка выбранного скриншота — показывается справа.</summary>
        private Image _preview;
        private string _previewPath = "";

        /// <summary>Хвост выбранного лога — тоже показывается справа.</summary>
        private string _text = "";

        public InstancePage()
        {
            Padding = new Padding(0);

            for (int i = 0; i < _tabNames.Length; i++)
            {
                int index = i;
                var tab = new NareButton
                {
                    Text = _tabNames[i],
                    Style = BtnStyle.Subtle,
                    Size = new Size(128, 38),
                    Radius = 10
                };
                tab.Click += (s, e) => { _tab = index; Reload(); };
                _tabs.Add(tab);
                Controls.Add(tab);
            }

            _list.Renderer = DrawItem;

            ModIcons.Loaded += delegate
            {
                try
                {
                    if (IsHandleCreated) BeginInvoke((Action)(() => _list.Invalidate()));
                }
                catch { }
            };
            _list.SelectionChanged += (s, e) => { LoadPreview(); UpdateButtons(); };
            _list.ItemActivated += (s, e) => OpenSelected();

            _search.Inner.TextChanged += (s, e) => Filter();

            _open.Click += (s, e) => OpenFolder(CurrentDir());
            _add.Click += (s, e) => AddFiles();
            _backup.Click += (s, e) => Backup();
            _delete.Click += (s, e) => DeleteSelected();
            _export.Click += (s, e) => ExportPack();
            _import.Click += (s, e) => ImportPack();
            _refresh.Click += (s, e) => Reload();

            Controls.Add(_list);
            Controls.Add(_search);
            Controls.Add(_open);
            Controls.Add(_add);
            Controls.Add(_backup);
            Controls.Add(_delete);
            Controls.Add(_export);
            Controls.Add(_import);
            Controls.Add(_refresh);
            Controls.Add(_status);
        }

        public override void PageShown()
        {
            Reload();
        }

        // ---------------------------------------------------------------- данные

        private string InstanceDir()
        {
            string id = Settings.Current.SelectedVersion;
            return string.IsNullOrEmpty(id) ? Paths.GameRoot : Paths.InstanceDir(id);
        }

        private string CurrentDir()
        {
            string root = InstanceDir();
            switch (_tab)
            {
                case TabShots: return Path.Combine(root, "screenshots");
                case TabPacks: return Path.Combine(root, "resourcepacks");
                case TabShaders: return Path.Combine(root, "shaderpacks");
                case TabLogs: return Path.Combine(root, "logs");
                default: return Path.Combine(root, "saves");
            }
        }

        private void Reload()
        {
            UpdateTabs();
            DropPreview();

            _all.Clear();

            string dir = CurrentDir();
            try
            {
                Directory.CreateDirectory(dir);

                if (_tab == TabWorlds)
                {
                    foreach (string path in Directory.GetDirectories(dir))
                        _all.Add(Describe(path, true));
                }
                else if (_tab == TabLogs)
                {
                    foreach (string path in Directory.GetFiles(dir))
                        if (IsText(path)) _all.Add(Describe(path, false));

                    // отчёты о падениях лежат отдельно, но смотреть их надо здесь же
                    string crashes = Path.Combine(InstanceDir(), "crash-reports");
                    if (Directory.Exists(crashes))
                        foreach (string path in Directory.GetFiles(crashes))
                            if (IsText(path))
                            {
                                var entry = Describe(path, false);
                                entry.Crash = true;
                                _all.Add(entry);
                            }
                }
                else
                {
                    foreach (string path in Directory.GetFiles(dir))
                    {
                        if (_tab == TabShots && !IsImage(path)) continue;
                        _all.Add(Describe(path, false));
                    }

                    // ресурспаки и шейдеры бывают и распакованными папками
                    if (_tab != TabShots)
                        foreach (string path in Directory.GetDirectories(dir))
                            _all.Add(Describe(path, true));
                }
            }
            catch (Exception ex)
            {
                Say("Папку не прочитать: " + ex.Message, Theme.Bad);
            }

            _all.Sort((a, b) => b.Time.CompareTo(a.Time));

            Filter();
            UpdateButtons();
            Layout1();
        }

        private static Entry Describe(string path, bool directory)
        {
            var e = new Entry
            {
                Name = Path.GetFileName(path),
                Path = path,
                Directory = directory
            };

            try
            {
                e.Time = directory ? System.IO.Directory.GetLastWriteTime(path) : File.GetLastWriteTime(path);
                e.Size = directory ? FolderSize(path) : new FileInfo(path).Length;
            }
            catch { }

            return e;
        }

        /// <summary>Размер папки. С ограничением по времени — мир бывает огромным.</summary>
        private static long FolderSize(string path)
        {
            long total = 0;
            var clock = Stopwatch.StartNew();

            try
            {
                foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { total += new FileInfo(file).Length; } catch { }
                    if (clock.ElapsedMilliseconds > 400) break;
                }
            }
            catch { }

            return total;
        }

        private static bool IsImage(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg";
        }

        private static bool IsText(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".log" || ext == ".txt";
        }

        private void Filter()
        {
            string query = (_search.Inner.Text ?? "").Trim().ToLowerInvariant();

            var shown = new List<object>();
            foreach (var e in _all)
                if (query.Length == 0 || e.Name.ToLowerInvariant().Contains(query))
                    shown.Add(e);

            _list.SetItems(shown);
            DropPreview();
            UpdateButtons();

            if (_all.Count == 0) Say(Empty(), Theme.Muted);
            else Say(_all.Count + " " + Counted(_all.Count) + " · " + Human(Total()), Theme.Muted);
        }

        private long Total()
        {
            long sum = 0;
            foreach (var e in _all) sum += e.Size;
            return sum;
        }

        private string Empty()
        {
            switch (_tab)
            {
                case TabShots: return "Скриншотов нет. В игре их делает F2.";
                case TabPacks: return "Ресурспаков нет. Кнопка «Добавить» положит сюда .zip.";
                case TabShaders: return "Шейдеров нет. Нужен Iris или OptiFine в сборке.";
                case TabLogs: return "Логов нет — игра ещё ни разу не запускалась в этой сборке.";
                default: return "Миров нет — создайте мир в игре.";
            }
        }

        private string Counted(int n)
        {
            switch (_tab)
            {
                case TabShots: return n == 1 ? "скриншот" : "скриншотов";
                case TabPacks: return n == 1 ? "ресурспак" : "ресурспаков";
                case TabShaders: return n == 1 ? "шейдер" : "шейдеров";
                case TabLogs: return n == 1 ? "файл" : "файлов";
                default: return n == 1 ? "мир" : "миров";
            }
        }

        private Entry Selected()
        {
            return _list.SelectedItem as Entry;
        }

        // ---------------------------------------------------------------- действия

        private void OpenFolder(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch (Exception ex) { Host.ShowError("Не удалось открыть папку", ex); }
        }

        private void OpenSelected()
        {
            var e = Selected();
            if (e == null) return;

            try
            {
                if (e.Directory) Process.Start("explorer.exe", "\"" + e.Path + "\"");
                else Process.Start("explorer.exe", "/select,\"" + e.Path + "\"");
            }
            catch (Exception ex) { Host.ShowError("Не удалось открыть", ex); }
        }

        private void AddFiles()
        {
            string dir = CurrentDir();

            using (var dialog = new OpenFileDialog())
            {
                dialog.Multiselect = true;
                dialog.Title = _tab == TabShaders ? "Выберите шейдеры" : "Выберите ресурспаки";
                dialog.Filter = "Архивы (*.zip)|*.zip|Все файлы|*.*";

                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                int copied = 0;
                try
                {
                    Directory.CreateDirectory(dir);
                    foreach (string file in dialog.FileNames)
                    {
                        string target = Path.Combine(dir, Path.GetFileName(file));
                        if (string.Equals(target, file, StringComparison.OrdinalIgnoreCase)) continue;
                        File.Copy(file, target, true);
                        copied++;
                    }
                }
                catch (Exception ex)
                {
                    Host.ShowError("Не удалось скопировать", ex);
                }

                Reload();
                Say("Добавлено файлов: " + copied, Theme.Good);
            }
        }

        private void Backup()
        {
            var e = Selected();
            if (e == null) { Say("Сначала выберите мир в списке.", Theme.Warn); return; }

            string backups = Path.Combine(InstanceDir(), "backups");
            string name = e.Name + "-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".zip";
            string target = Path.Combine(backups, name);

            try
            {
                Directory.CreateDirectory(backups);
                if (File.Exists(target)) File.Delete(target);

                ZipFile.CreateFromDirectory(e.Path, target, CompressionLevel.Fastest, false);

                Say("Копия готова: " + name + " (" + Human(new FileInfo(target).Length) + ")", Theme.Good);
                Log.Info("Копия мира: " + target);
            }
            catch (Exception ex)
            {
                Host.ShowError("Не удалось сделать копию", ex);
                Say("Копия не получилась: " + ex.Message, Theme.Bad);
            }
        }

        private void DeleteSelected()
        {
            var e = Selected();
            if (e == null) return;

            // мир не стираем насовсем: уносим в отдельную папку, откуда его можно вернуть
            if (_tab == TabWorlds)
            {
                if (!Host.Confirm("Убрать мир «" + e.Name + "» из сборки?\n\n"
                                  + "Он переедет в папку deleted внутри сборки — оттуда его можно вернуть."))
                    return;

                try
                {
                    string graveyard = Path.Combine(InstanceDir(), "deleted");
                    Directory.CreateDirectory(graveyard);

                    string target = Path.Combine(graveyard, e.Name + "-" + DateTime.Now.ToString("yyyyMMdd-HHmm"));
                    Directory.Move(e.Path, target);

                    Say("Мир убран в папку deleted.", Theme.Good);
                }
                catch (Exception ex)
                {
                    Host.ShowError("Не удалось убрать мир", ex);
                }

                Reload();
                return;
            }

            if (!Host.Confirm("Удалить «" + e.Name + "» насовсем?")) return;

            try
            {
                DropPreview();
                if (e.Directory) Directory.Delete(e.Path, true);
                else File.Delete(e.Path);

                Say("Удалено: " + e.Name, Theme.Good);
            }
            catch (Exception ex)
            {
                Host.ShowError("Не удалось удалить", ex);
            }

            Reload();
        }

        // ------------------------------------------------------- перенос сборки

        /// <summary>Что в экспорт не кладём никогда — это либо мусор, либо гигабайты.</summary>
        private static readonly string[] Skip =
            { "logs", "crash-reports", "backups", "deleted", "natives", "assets", "libraries" };

        private void ExportPack()
        {
            string root = InstanceDir();
            if (!Directory.Exists(root)) { Say("Сборки ещё нет.", Theme.Warn); return; }

            bool withWorlds = Host.Confirm("Положить в файл и миры тоже?\n\n"
                                           + "Да — переедет всё вместе с мирами (файл будет большим).\n"
                                           + "Нет — только моды, настройки, ресурспаки и шейдеры.");

            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Куда сохранить сборку";
                dialog.Filter = "Сборка NareLauncher (*.narepack)|*.narepack|Архив (*.zip)|*.zip";
                dialog.FileName = Paths.SafeName(Settings.Current.SelectedVersion) + "-"
                                  + DateTime.Now.ToString("yyyyMMdd") + ".narepack";

                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    if (File.Exists(dialog.FileName)) File.Delete(dialog.FileName);

                    int files = 0;
                    using (var zip = ZipFile.Open(dialog.FileName, ZipArchiveMode.Create))
                    {
                        // опись: по ней при импорте понятно, что это за сборка
                        var card = Json.NewObject();
                        card.Set("launcher", GameLauncher.LauncherName);
                        card.Set("version", Settings.Current.SelectedVersion ?? "");
                        card.Set("worlds", withWorlds);
                        card.Set("made", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));

                        var note = zip.CreateEntry("narepack.json");
                        using (var writer = new StreamWriter(note.Open(), new UTF8Encoding(false)))
                            writer.Write(card.ToJson(true));

                        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                        {
                            string rel = file.Substring(root.Length).TrimStart('\\', '/');
                            string top = rel.Replace('\\', '/').Split('/')[0];

                            if (Array.IndexOf(Skip, top) >= 0) continue;
                            if (!withWorlds && top == "saves") continue;

                            try
                            {
                                zip.CreateEntryFromFile(file, rel.Replace('\\', '/'), CompressionLevel.Fastest);
                                files++;
                            }
                            catch { }
                        }
                    }

                    long size = new FileInfo(dialog.FileName).Length;
                    Say("Сборка собрана: " + files + " файлов, " + Human(size), Theme.Good);
                    Log.Info("Экспорт сборки: " + dialog.FileName);
                }
                catch (Exception ex)
                {
                    Host.ShowError("Не удалось собрать файл сборки", ex);
                }
            }
        }

        private void ImportPack()
        {
            string root = InstanceDir();

            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Файл сборки";
                dialog.Filter = "Сборка NareLauncher (*.narepack;*.zip)|*.narepack;*.zip|Все файлы|*.*";

                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                if (!Host.Confirm("Распаковать «" + Path.GetFileName(dialog.FileName) + "» в сборку "
                                  + (Settings.Current.SelectedVersion ?? "") + "?\n\n"
                                  + "Файлы с одинаковыми именами будут заменены."))
                    return;

                try
                {
                    Directory.CreateDirectory(root);

                    int files = 0;
                    using (var zip = ZipFile.OpenRead(dialog.FileName))
                    {
                        foreach (var item in zip.Entries)
                        {
                            if (item.Length == 0 && item.FullName.EndsWith("/")) continue;
                            if (item.FullName == "narepack.json") continue;

                            string target = Path.GetFullPath(Path.Combine(root, item.FullName.Replace('/', '\\')));

                            // архив не должен уметь писать мимо папки сборки
                            if (!target.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase)) continue;

                            Paths.EnsureForFile(target);
                            item.ExtractToFile(target, true);
                            files++;
                        }
                    }

                    Say("Распаковано файлов: " + files, Theme.Good);
                    Log.Info("Импорт сборки: " + dialog.FileName);
                }
                catch (Exception ex)
                {
                    Host.ShowError("Не удалось распаковать сборку", ex);
                }

                Reload();
            }
        }

        // ---------------------------------------------------------------- превью

        private void LoadPreview()
        {
            var e = Selected();

            if (_tab == TabLogs)
            {
                DropPreview();
                if (e == null) return;

                _text = Tail(e.Path, 220);
                Invalidate();
                return;
            }

            if (_tab != TabShots || e == null || e.Directory) { DropPreview(); return; }
            if (e.Path == _previewPath) return;

            DropPreview();

            try
            {
                // читаем через память, иначе файл остаётся занятым и его не удалить
                byte[] bytes = File.ReadAllBytes(e.Path);
                using (var stream = new MemoryStream(bytes))
                    _preview = Image.FromStream(stream);
                _previewPath = e.Path;
            }
            catch { _preview = null; _previewPath = ""; }

            Invalidate();
        }

        /// <summary>Последние строки файла — именно в них обычно и лежит причина.</summary>
        private static string Tail(string path, int lines)
        {
            try
            {
                var kept = new Queue<string>();
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        kept.Enqueue(line);
                        if (kept.Count > lines) kept.Dequeue();
                    }
                }
                return string.Join("\n", kept.ToArray());
            }
            catch (Exception ex) { return "Файл не прочитать: " + ex.Message; }
        }

        private void DropPreview()
        {
            if (_preview != null) { _preview.Dispose(); _preview = null; }
            _previewPath = "";
            _text = "";
            Invalidate();
        }

        // ---------------------------------------------------------------- отрисовка

        private void DrawItem(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var e = item as Entry;
            if (e == null) return;

            var box = Rectangle.Inflate(r, -4, -3);
            if (box.Width <= 0 || box.Height <= 0) return;

            if (selected) Theme.FillRound(g, box, 10, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 10, Color.FromArgb(16, 255, 255, 255));
            if (selected) Theme.DrawRound(g, box, 10, Color.FromArgb(120, Theme.Accent));

            var icon = new Rectangle(box.X + 12, box.Y + (box.Height - 18) / 2, 18, 18);
            Color tint = e.Crash ? Theme.Bad : (selected ? Theme.Accent : Theme.Dim);

            // у ресурспаков и шейдеров показываем их pack.png
            Bitmap picture = (_tab == TabPacks || _tab == TabShaders) ? ModIcons.For(e.Path) : null;

            if (picture != null)
            {
                icon = new Rectangle(box.X + 10, box.Y + (box.Height - 34) / 2, 34, 34);
                var saved = g.InterpolationMode;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                try { g.DrawImage(picture, icon); } catch { }
                g.InterpolationMode = saved;
            }
            else Theme.DrawIcon(g, e.Directory ? Ico.Folder : Ico.Check, icon, tint);

            int textX = icon.Right + 12;
            int textW = Math.Max(40, box.Width - (textX - box.X) - 16);

            Theme.Text1(g, e.Name, Theme.F(10.5f, FontStyle.Bold), e.Crash ? Theme.Bad : Theme.Text,
                new Rectangle(textX, box.Y + 7, textW, 20));

            string sub = Human(e.Size) + " · " + e.Time.ToString("dd.MM.yyyy HH:mm");
            if (e.Crash) sub = "отчёт о падении · " + sub;

            Theme.Text1(g, sub, Theme.F(8.8f), Theme.Muted,
                new Rectangle(textX, box.Y + 26, textW, 18));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.Smooth(g);

            Theme.Text1(g, "Сборка", Theme.F(17f, FontStyle.Bold), Theme.Text,
                new Rectangle(28, 16, 400, 28));

            string id = Settings.Current.SelectedVersion;
            string where = string.IsNullOrEmpty(id) ? "сборка не выбрана" : id;
            Theme.Text1(g, "Миры, скриншоты, ресурспаки, шейдеры и логи · " + where,
                Theme.F(9.5f), Theme.Muted, new Rectangle(28, 40, 900, 20));

            if (_tab == TabShots) DrawPreview(g);
            else if (_tab == TabLogs) DrawText(g);
        }

        private void DrawPreview(Graphics g)
        {
            var area = PreviewArea();
            if (area.Width < 80 || area.Height < 80) return;

            Theme.FillRound(g, area, 12, Color.FromArgb(120, 10, 13, 19));
            Theme.DrawRound(g, area, 12, Color.FromArgb(26, 255, 255, 255));

            if (_preview == null)
            {
                Theme.Text1(g, "Выберите скриншот", Theme.F(9.5f), Theme.Dim, area,
                    StringAlignment.Center, StringAlignment.Center);
                return;
            }

            var inner = Rectangle.Inflate(area, -10, -10);
            float scale = Math.Min((float)inner.Width / _preview.Width,
                                   (float)inner.Height / _preview.Height);
            int w = Math.Max(1, (int)(_preview.Width * scale));
            int h = Math.Max(1, (int)(_preview.Height * scale));

            var target = new Rectangle(inner.X + (inner.Width - w) / 2,
                                       inner.Y + (inner.Height - h) / 2, w, h);
            g.DrawImage(_preview, target);

            Theme.Text1(g, _preview.Width + " × " + _preview.Height, Theme.F(8.5f), Theme.Dim,
                new Rectangle(area.X, area.Bottom - 20, area.Width, 16),
                StringAlignment.Center, StringAlignment.Center);
        }

        private void DrawText(Graphics g)
        {
            var area = PreviewArea();
            if (area.Width < 80 || area.Height < 80) return;

            Theme.FillRound(g, area, 12, Color.FromArgb(120, 10, 13, 19));
            Theme.DrawRound(g, area, 12, Color.FromArgb(26, 255, 255, 255));

            if (string.IsNullOrEmpty(_text))
            {
                Theme.Text1(g, "Выберите файл — покажу его хвост", Theme.F(9.5f), Theme.Dim, area,
                    StringAlignment.Center, StringAlignment.Center);
                return;
            }

            var inner = Rectangle.Inflate(area, -12, -10);
            var font = new Font(FontFamily.GenericMonospace, 7.5f);

            // рисуем снизу вверх: последние строки важнее всего
            var rows = _text.Split('\n');
            int lineHeight = 13;
            int shown = Math.Min(rows.Length, Math.Max(1, inner.Height / lineHeight));

            for (int i = 0; i < shown; i++)
            {
                string row = rows[rows.Length - shown + i];
                if (row.Length > 220) row = row.Substring(0, 220);

                Color color = Theme.Muted;
                if (row.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    row.IndexOf("Exception", StringComparison.Ordinal) >= 0) color = Theme.Bad;
                else if (row.IndexOf("WARN", StringComparison.OrdinalIgnoreCase) >= 0) color = Theme.Warn;

                using (var brush = new SolidBrush(color))
                    g.DrawString(row, font, brush,
                        new RectangleF(inner.X, inner.Y + i * lineHeight, inner.Width, lineHeight),
                        new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter });
            }

            font.Dispose();
        }

        private Rectangle PreviewArea()
        {
            int pad = 28;
            int width = _tab == TabLogs
                ? Math.Min(560, Math.Max(0, Width / 2))
                : Math.Min(360, Math.Max(0, Width / 3));
            int top = 150;
            return new Rectangle(Width - pad - width, top, width, Math.Max(0, Height - top - 80));
        }

        // ---------------------------------------------------------------- вёрстка

        private void UpdateTabs()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                _tabs[i].Style = i == _tab ? BtnStyle.Accent : BtnStyle.Subtle;
                _tabs[i].Invalidate();
            }
        }

        private void UpdateButtons()
        {
            bool any = Selected() != null;

            _add.Visible = _tab == TabPacks || _tab == TabShaders;
            _backup.Visible = _tab == TabWorlds;

            _backup.Enabled = any;
            _delete.Enabled = any;
            _delete.Text = _tab == TabWorlds ? "Убрать мир" : "Удалить";

            Layout1();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Layout1();
        }

        private void Layout1()
        {
            int pad = 28;
            int y = 78;

            int x = pad;
            foreach (var tab in _tabs)
            {
                tab.Location = new Point(x, y);
                x += tab.Width + 8;
            }

            int searchW = Math.Max(150, Math.Min(260, Width - x - pad - 12));
            _search.SetBounds(Math.Max(x + 12, Width - pad - searchW), y, searchW, 38);

            int listTop = y + 52;
            int bottom = 56;

            int listW = Width - pad * 2;
            if (_tab == TabShots || _tab == TabLogs)
                listW = Math.Max(200, Width - pad * 3 - PreviewArea().Width);

            _list.SetBounds(pad, listTop, listW, Math.Max(60, Height - listTop - bottom - 18));

            int by = Height - bottom - 2;
            int bx = pad;

            foreach (var button in new[] { _open, _add, _backup, _delete, _export, _import, _refresh })
            {
                if (!button.Visible) continue;
                button.Location = new Point(bx, by);
                bx += button.Width + 7;
            }

            _status.SetBounds(pad, Height - 22, Math.Max(100, Width - pad * 2), 18);

            Invalidate();
        }

        private void Say(string text, Color color)
        {
            _status.Text = text ?? "";
            _status.ForeColor = color;
        }

        private static string Human(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024) return (bytes / 1024.0 / 1024 / 1024).ToString("0.0") + " ГБ";
            if (bytes >= 1024L * 1024) return (bytes / 1024.0 / 1024).ToString("0.0") + " МБ";
            if (bytes >= 1024) return (bytes / 1024.0).ToString("0") + " КБ";
            return bytes + " Б";
        }
    }
}
