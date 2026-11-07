using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Полноценное окно редактора скина: развёртка, слои, фильтры и объёмная модель.
    /// Окно своё, тянется за края, помнит размер на время сессии.
    /// </summary>
    public class SkinEditorForm : Form
    {
        private const int TitleHeight = 44;
        private const int Grip = 6;
        private const int Pad = 22;
        private const int LeftWidth = 236;
        private const int RightWidth = 300;
        private const int PalCols = 9;
        private const int PalCell = 25;

        private static Size _lastSize = new Size(1300, 840);

        private readonly MainForm _host;

        private readonly SkinCanvas _canvas = new SkinCanvas();
        private readonly SkinView3D _view = new SkinView3D();
        private readonly SkinLayerList _layers = new SkinLayerList();

        private readonly List<NareButton> _tools = new List<NareButton>();
        private static readonly SkinTool[] ToolOrder =
        {
            SkinTool.Pencil,  SkinTool.Eraser,
            SkinTool.Fill,    SkinTool.Picker,
            SkinTool.Line,    SkinTool.Rect,
            SkinTool.Ellipse, SkinTool.Spray,
            SkinTool.Lighten, SkinTool.Darken,
            SkinTool.Replace
        };
        private static readonly string[] ToolNames =
        {
            "Кисть",  "Ластик",
            "Залить", "Пипетка",
            "Линия",  "Прям.",
            "Эллипс", "Спрей",
            "Светл.", "Темн.",
            "Замена"
        };
        private static readonly string[] ToolHints =
        {
            "B", "E", "G", "I", "L", "R", "O", "S", "+", "-", "H"
        };

        private readonly NareSlider _brush = new NareSlider { Minimum = 1, Maximum = 5, Value = 1 };
        private readonly NareToggle _mirror = new NareToggle();
        private readonly NareToggle _fillShapes = new NareToggle();

        private readonly NareToggle _overlay = new NareToggle { Checked = true };
        private readonly NareToggle _spin = new NareToggle { Checked = true };
        private readonly NareToggle _arms = new NareToggle();
        private readonly NareToggle _paint3d = new NareToggle();

        private readonly NareSlider _opacity = new NareSlider { Minimum = 0, Maximum = 100, Value = 100 };

        private readonly NareButton _layerAdd = new NareButton { Text = "+", Style = BtnStyle.Ghost, Size = new Size(34, 28), Radius = 7, Font = Theme.F(12f, FontStyle.Bold) };
        private readonly NareButton _layerCopy = new NareButton { Text = "⧉", Style = BtnStyle.Ghost, Size = new Size(34, 28), Radius = 7, Font = Theme.F(10f, FontStyle.Bold) };
        private readonly NareButton _layerDel = new NareButton { Text = "×", Style = BtnStyle.Ghost, Size = new Size(34, 28), Radius = 7, Font = Theme.F(12f, FontStyle.Bold) };
        private readonly NareButton _layerUp = new NareButton { Text = "↑", Style = BtnStyle.Ghost, Size = new Size(34, 28), Radius = 7, Font = Theme.F(11f, FontStyle.Bold) };
        private readonly NareButton _layerDown = new NareButton { Text = "↓", Style = BtnStyle.Ghost, Size = new Size(34, 28), Radius = 7, Font = Theme.F(11f, FontStyle.Bold) };
        private readonly NareButton _layerMerge = new NareButton { Text = "⇓", Style = BtnStyle.Ghost, Size = new Size(34, 28), Radius = 7, Font = Theme.F(11f, FontStyle.Bold) };

        private readonly List<NareButton> _filters = new List<NareButton>();

        private readonly NareButton _steve = new NareButton { Text = "Стив", Style = BtnStyle.Ghost, Size = new Size(84, 36) };
        private readonly NareButton _alex = new NareButton { Text = "Алекс", Style = BtnStyle.Ghost, Size = new Size(88, 36) };
        private readonly NareButton _web = new NareButton { Text = "С сайта", Style = BtnStyle.Ghost, Icon = Ico.Search, Size = new Size(118, 36) };
        private readonly NareButton _open = new NareButton { Text = "Импорт", Style = BtnStyle.Ghost, Icon = Ico.Folder, Size = new Size(112, 36) };
        private readonly NareButton _save = new NareButton { Text = "Экспорт", Style = BtnStyle.Ghost, Icon = Ico.Download, Size = new Size(118, 36) };
        private readonly NareButton _undo = new NareButton { Text = "Отмена", Style = BtnStyle.Ghost, Icon = Ico.Refresh, Size = new Size(112, 36) };
        private readonly NareButton _redo = new NareButton { Text = "Вернуть", Style = BtnStyle.Ghost, Size = new Size(106, 36) };
        private readonly NareButton _apply = new NareButton { Text = "Применить к аккаунту", Style = BtnStyle.Accent, Icon = Ico.Check, Size = new Size(214, 36) };

        private readonly NareButton _palFromSkin = new NareButton
        { Text = "Цвета из скина", Style = BtnStyle.Ghost, Size = new Size(LeftWidth - 56, 26), Radius = 8, Font = Theme.F(9f, FontStyle.Bold) };

        private readonly NareButton _closeX = new NareButton { Text = "", Style = BtnStyle.Ghost, Icon = Ico.Close, Size = new Size(40, 28), Radius = 8 };
        private readonly NareButton _maxBtn = new NareButton { Text = "□", Style = BtnStyle.Ghost, Size = new Size(36, 28), Radius = 8 };
        private readonly NareButton _minBtn = new NareButton { Text = "", Style = BtnStyle.Ghost, Icon = Ico.Minimize, Size = new Size(36, 28), Radius = 8 };

        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);

        private readonly List<Color> _fromSkin = new List<Color>();

        public SkinDocument Doc;

        private static readonly Color[] Palette =
        {
            Color.FromArgb(255, 255, 255, 255), Color.FromArgb(255, 200, 200, 205),
            Color.FromArgb(255, 150, 150, 158), Color.FromArgb(255, 100, 100, 110),
            Color.FromArgb(255, 60, 60, 68),    Color.FromArgb(255, 20, 20, 24),
            Color.FromArgb(255, 245, 222, 190), Color.FromArgb(255, 226, 186, 150),
            Color.FromArgb(255, 190, 140, 105), Color.FromArgb(255, 140, 96, 66),
            Color.FromArgb(255, 95, 62, 42),    Color.FromArgb(255, 60, 38, 26),
            Color.FromArgb(255, 255, 92, 92),   Color.FromArgb(255, 205, 50, 50),
            Color.FromArgb(255, 240, 150, 60),  Color.FromArgb(255, 240, 200, 70),
            Color.FromArgb(255, 150, 210, 80),  Color.FromArgb(255, 70, 170, 90),
            Color.FromArgb(255, 60, 150, 150),  Color.FromArgb(255, 70, 140, 235),
            Color.FromArgb(255, 60, 90, 190),   Color.FromArgb(255, 110, 80, 210),
            Color.FromArgb(255, 170, 80, 200),  Color.FromArgb(255, 230, 110, 170)
        };

        private Rectangle _paletteBox;
        private Rectangle _skinPalBox;
        private Rectangle _swatch;
        private int _brushLabelY, _layersLabelY, _filtersLabelY;

        public SkinEditorForm(MainForm host, SkinDocument doc)
        {
            _host = host;

            Text = "Редактор скина";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(960, 620);

            var work = Screen.PrimaryScreen.WorkingArea;
            ClientSize = new Size(Math.Min(_lastSize.Width, work.Width - 60),
                                  Math.Min(_lastSize.Height, work.Height - 60));
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(9.5f);
            DoubleBuffered = true;
            KeyPreview = true;
            ShowInTaskbar = false;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            BuildTools();
            BuildFilters();
            Wire();

            Controls.Add(_canvas);
            Controls.Add(_view);
            Controls.Add(_layers);
            Controls.Add(_brush);
            Controls.Add(_opacity);
            Controls.Add(_mirror);
            Controls.Add(_fillShapes);
            Controls.Add(_overlay);
            Controls.Add(_spin);
            Controls.Add(_arms);
            Controls.Add(_paint3d);
            Controls.Add(_palFromSkin);
            Controls.Add(_layerAdd);
            Controls.Add(_layerCopy);
            Controls.Add(_layerDel);
            Controls.Add(_layerUp);
            Controls.Add(_layerDown);
            Controls.Add(_layerMerge);
            Controls.Add(_steve);
            Controls.Add(_alex);
            Controls.Add(_web);
            Controls.Add(_open);
            Controls.Add(_save);
            Controls.Add(_undo);
            Controls.Add(_redo);
            Controls.Add(_apply);
            Controls.Add(_closeX);
            Controls.Add(_maxBtn);
            Controls.Add(_minBtn);
            Controls.Add(_status);

            SetDoc(doc ?? SkinDocument.FromSkin(SkinFile.Default(false, Settings.Current.SelectedVersion)));
            Layout1();
        }

        // ---------------------------------------------------------------- сборка

        private const int ToolCols = 3;

        private void BuildTools()
        {
            int cell = (LeftWidth - (ToolCols - 1) * 6) / ToolCols;

            for (int i = 0; i < ToolOrder.Length; i++)
            {
                SkinTool tool = ToolOrder[i];
                var b = new NareButton
                {
                    Text = ToolNames[i],
                    Style = i == 0 ? BtnStyle.Accent : BtnStyle.Ghost,
                    Size = new Size(cell, 30),
                    Radius = 8,
                    Font = Theme.F(8.5f, FontStyle.Bold)
                };
                b.Click += (s, e) => PickTool(tool);
                _tools.Add(b);
                Controls.Add(b);
            }
        }

        private class Filter
        {
            public string Name;
            public Action<Bitmap> Action;
        }

        private void BuildFilters()
        {
            var list = new List<Filter>
            {
                new Filter { Name = "Шум",      Action = b => SkinFilters.Noise(b, 14) },
                new Filter { Name = "Светлее",  Action = b => SkinFilters.Brightness(b, 12) },
                new Filter { Name = "Темнее",   Action = b => SkinFilters.Brightness(b, -12) },
                new Filter { Name = "Ярче",     Action = b => SkinFilters.Saturation(b, 1.25f) },
                new Filter { Name = "Бледнее",  Action = b => SkinFilters.Saturation(b, 0.8f) },
                new Filter { Name = "Контраст", Action = b => SkinFilters.Contrast(b, 1.2f) },
                new Filter { Name = "Оттенок +",Action = b => SkinFilters.HueShift(b, 18f) },
                new Filter { Name = "Оттенок −",Action = b => SkinFilters.HueShift(b, -18f) },
                new Filter { Name = "Отразить", Action = null },
                new Filter { Name = "Очистить", Action = SkinFilters.Clear }
            };

            foreach (var f in list)
            {
                Filter one = f;
                var b = new NareButton
                {
                    Text = one.Name,
                    Style = BtnStyle.Ghost,
                    Size = new Size(70, 28),
                    Radius = 7,
                    Font = Theme.F(8.5f, FontStyle.Bold)
                };
                b.Click += (s, e) =>
                {
                    if (one.Action == null) _canvas.ApplyToLayer(bmp => SkinFilters.FlipX(bmp, Doc.Arms));
                    else _canvas.ApplyToLayer(one.Action);

                    Say("Фильтр «" + one.Name + "» применён к слою «" + Doc.Current.Name + "».", Theme.Muted);
                };
                _filters.Add(b);
                Controls.Add(b);
            }
        }

        private void Wire()
        {
            _canvas.Changed += (s, e) => { _view.Invalidate(); UpdateStatus(); };
            _canvas.ColorPicked += (s, c) => { _canvas.Ink = c; Invalidate(); };

            _view.SurfacePaint += (s, a) =>
            {
                switch (a.Phase)
                {
                    case SurfacePhase.Begin: _canvas.BeginSurfaceStroke(); break;
                    case SurfacePhase.Move: _canvas.SurfaceStroke(a.Texel, a.Erase); break;
                    case SurfacePhase.End: _canvas.EndSurfaceStroke(); break;
                }
                _view.Invalidate();
            };

            _layers.Selected += (s, e) => { _canvas.ClearUndo(); SyncOpacity(); UpdateStatus(); };
            _layers.VisibilityChanged += (s, e) => { _canvas.Invalidate(); _view.Invalidate(); };
            _layers.RenameRequested += (s, e) => RenameLayer();

            _brush.ValueChanged += (s, e) => { _canvas.BrushSize = _brush.Value; Invalidate(); };
            _mirror.CheckedChanged += (s, e) => { _canvas.MirrorX = _mirror.Checked; _canvas.Invalidate(); };
            _fillShapes.CheckedChanged += (s, e) => { _canvas.FillShapes = _fillShapes.Checked; };

            _opacity.ValueChanged += (s, e) =>
            {
                if (Doc == null) return;
                Doc.Current.Opacity = _opacity.Value;
                Doc.Rebuild();
                _canvas.Invalidate();
                _view.Invalidate();
                _layers.Invalidate();
            };

            _overlay.CheckedChanged += (s, e) => { _view.ShowOverlay = _overlay.Checked; _view.Invalidate(); };
            _spin.CheckedChanged += (s, e) => { _view.Spin = _spin.Checked; };
            _paint3d.CheckedChanged += (s, e) =>
            {
                _view.SetPaintMode(_paint3d.Checked);
                if (_paint3d.Checked)
                {
                    _spin.Checked = false;
                    Say("Рисуем по модели: левая — цвет, правая — стереть, средняя — повернуть.", Theme.Muted);
                }
                Invalidate();
            };
            _arms.CheckedChanged += (s, e) =>
            {
                if (Doc == null) return;
                Doc.Arms = _arms.Checked ? SkinArms.Slim : SkinArms.Wide;
                _view.Invalidate();
                _canvas.Invalidate();
            };

            _layerAdd.Click += (s, e) => { Doc.AddLayer(); Say("Добавлен «" + Doc.Current.Name + "».", Theme.Muted); AfterLayers(); };
            _layerCopy.Click += (s, e) => { Doc.DuplicateLayer(); Say("Слой продублирован.", Theme.Muted); AfterLayers(); };
            _layerDel.Click += (s, e) =>
            {
                if (!Doc.RemoveLayer()) { Say("Последний слой удалить нельзя.", Theme.Warn); return; }
                Say("Слой удалён.", Theme.Muted);
                AfterLayers();
            };
            _layerUp.Click += (s, e) => { if (Doc.MoveLayer(1)) AfterLayers(); };
            _layerDown.Click += (s, e) => { if (Doc.MoveLayer(-1)) AfterLayers(); };
            _layerMerge.Click += (s, e) =>
            {
                if (!Doc.MergeDown()) { Say("Под этим слоем ничего нет.", Theme.Warn); return; }
                Say("Слои объединены.", Theme.Muted);
                AfterLayers();
            };

            _palFromSkin.Click += (s, e) => TakeColorsFromSkin();

            _steve.Click += (s, e) => LoadDefault(false);
            _alex.Click += (s, e) => LoadDefault(true);
            _web.Click += (s, e) => FromWeb();
            _open.Click += (s, e) => Import();
            _save.Click += (s, e) => Export();
            _undo.Click += (s, e) => { _canvas.Undo(); _view.Invalidate(); };
            _redo.Click += (s, e) => { _canvas.Redo(); _view.Invalidate(); };
            _apply.Click += (s, e) => ApplyToAccount();

            _closeX.Click += (s, e) => Close();
            _minBtn.Click += (s, e) => WindowState = FormWindowState.Minimized;
            _maxBtn.Click += (s, e) =>
                WindowState = WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal : FormWindowState.Maximized;
        }

        // ---------------------------------------------------------------- данные

        private void SetDoc(SkinDocument doc)
        {
            Doc = doc;

            _canvas.Doc = doc;
            _canvas.ClearUndo();
            _canvas.ResetView();

            _view.Doc = doc;
            _layers.Doc = doc;

            _arms.Checked = doc.Arms == SkinArms.Slim;

            SyncOpacity();
            _layers.ScrollToActive();
            _layers.Invalidate();
            _canvas.Invalidate();
            _view.Invalidate();
            UpdateStatus();
        }

        private void SyncOpacity()
        {
            if (Doc == null) return;
            _opacity.Value = Doc.Current.Opacity;
        }

        private void AfterLayers()
        {
            _canvas.ClearUndo();
            SyncOpacity();
            _layers.ScrollToActive();
            _layers.Invalidate();
            _canvas.Invalidate();
            _view.Invalidate();
            UpdateStatus();
        }

        private void PickTool(SkinTool tool)
        {
            _canvas.Tool = tool;
            for (int i = 0; i < _tools.Count; i++)
            {
                _tools[i].Style = ToolOrder[i] == tool ? BtnStyle.Accent : BtnStyle.Ghost;
                _tools[i].Invalidate();
            }
            Invalidate();
        }

        private void TakeColorsFromSkin()
        {
            if (Doc == null) return;

            _fromSkin.Clear();
            _fromSkin.AddRange(SkinFilters.TopColors(Doc.Flat.Image, PalCols));
            Say("Взято цветов из скина: " + _fromSkin.Count + ".", Theme.Muted);
            Invalidate();
        }

        private void RenameLayer()
        {
            if (Doc == null) return;

            string name = Prompt.Ask(this, "Имя слоя", Doc.Current.Name);
            if (string.IsNullOrEmpty(name)) return;

            Doc.Current.Name = name.Trim();
            _layers.Invalidate();
            Invalidate();
        }

        private void LoadDefault(bool slim)
        {
            try
            {
                SetDoc(SkinDocument.FromSkin(SkinFile.Default(slim, Settings.Current.SelectedVersion)));
                Say(slim ? "Загружен стандартный скин Алекс (тонкие руки)."
                         : "Загружен стандартный скин Стив (обычные руки).", Theme.Muted);
            }
            catch (Exception ex) { Error("Не удалось загрузить стандартный скин", ex); }
        }

        private void FromWeb()
        {
            var account = AccountStore.Selected;
            string nick = account == null ? "" : account.Name;

            using (var dlg = new SkinDownloadForm(nick))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Result == null) return;

                SetDoc(SkinDocument.FromSkin(dlg.Result));
                Say("Загружен скин «" + dlg.Result.Name + "» с сайта · " +
                    (dlg.Result.Arms == SkinArms.Slim ? "тонкие руки" : "обычные руки"), Theme.Good);
            }
        }

        private void Import()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Открыть скин";
                dlg.Filter = "Скин (*" + SkinDocument.Extension + ";*.png)|*" + SkinDocument.Extension + ";*.png|" +
                             "NareSkin со слоями (*" + SkinDocument.Extension + ")|*" + SkinDocument.Extension + "|" +
                             "PNG 64×64 (*.png)|*.png|Все файлы (*.*)|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    SetDoc(SkinDocument.Open(dlg.FileName));
                    Say("Открыт " + Path.GetFileName(dlg.FileName) + " · слоёв: " + Doc.Layers.Count, Theme.Muted);
                }
                catch (Exception ex) { Error("Не удалось открыть скин", ex); }
            }
        }

        private void Export()
        {
            if (Doc == null) return;

            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = "Сохранить скин";
                dlg.Filter = "NareSkin со слоями (*" + SkinDocument.Extension + ")|*" + SkinDocument.Extension +
                             "|PNG 64×64 для игры (*.png)|*.png";
                dlg.FileName = Paths.SafeName(Doc.Name) + SkinDocument.Extension;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    string file = dlg.FileName;
                    string ext = (Path.GetExtension(file) ?? "").ToLowerInvariant();

                    if (dlg.FilterIndex == 2 && ext != ".png")
                    {
                        file = Path.ChangeExtension(file, ".png");
                        ext = ".png";
                    }

                    if (ext == ".png")
                    {
                        Doc.SavePng(file);
                        Say("Сохранена готовая текстура: " + file, Theme.Good);
                    }
                    else
                    {
                        if (ext != SkinDocument.Extension) file = Path.ChangeExtension(file, SkinDocument.Extension);
                        Doc.SaveNare(file);
                        Say("Сохранено со слоями (" + Doc.Layers.Count + "): " + file, Theme.Good);
                    }
                }
                catch (Exception ex) { Error("Не удалось сохранить скин", ex); }
            }
        }

        private void ApplyToAccount()
        {
            if (Doc == null) return;

            var account = AccountStore.Selected;
            if (account == null)
            {
                if (_host != null) _host.ManageAccounts();
                account = AccountStore.Selected;
                if (account == null) return;
            }

            try
            {
                string dir = Path.Combine(Paths.DataDir, "skins");
                Paths.Ensure(dir);

                string png = Path.Combine(dir, Paths.SafeName(account.Name) + ".png");
                string nare = Path.ChangeExtension(png, SkinDocument.Extension);

                Doc.SavePng(png);
                Doc.SaveNare(nare);

                account.SkinPath = png;
                AccountStore.Save();

                Say("Скин применён к «" + account.Name + "». Слои сохранены рядом в " +
                    SkinDocument.Extension + ".", Theme.Good);
            }
            catch (Exception ex) { Error("Не удалось применить скин", ex); }
        }

        private void Error(string title, Exception ex)
        {
            Log.Error(title, ex);
            MessageBox.Show(this, title + "\n\n" + (ex == null ? "" : ex.Message),
                "NareLauncher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Say(title, Theme.Bad);
        }

        private void Say(string text, Color color)
        {
            _status.Text = text;
            _status.ForeColor = color;
        }

        private void UpdateStatus()
        {
            _undo.Enabled = _canvas.CanUndo;
            _redo.Enabled = _canvas.CanRedo;

            bool many = Doc != null && Doc.Layers.Count > 1;
            _layerDel.Enabled = many;
            _layerMerge.Enabled = many && Doc.Active > 0;
            _layerUp.Enabled = Doc != null && Doc.Active < Doc.Layers.Count - 1;
            _layerDown.Enabled = Doc != null && Doc.Active > 0;

            foreach (var b in new[] { _undo, _redo, _layerDel, _layerMerge, _layerUp, _layerDown })
                b.Invalidate();

            Invalidate();
        }

        // ---------------------------------------------------------------- клавиши

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Control)
            {
                if (e.KeyCode == Keys.Z) { _canvas.Undo(); _view.Invalidate(); e.Handled = true; return; }
                if (e.KeyCode == Keys.Y) { _canvas.Redo(); _view.Invalidate(); e.Handled = true; return; }
                if (e.KeyCode == Keys.S) { Export(); e.Handled = true; return; }
                if (e.KeyCode == Keys.O) { Import(); e.Handled = true; return; }
                return;
            }

            switch (e.KeyCode)
            {
                case Keys.Escape: Close(); break;
                case Keys.B: PickTool(SkinTool.Pencil); break;
                case Keys.E: PickTool(SkinTool.Eraser); break;
                case Keys.G: PickTool(SkinTool.Fill); break;
                case Keys.I: PickTool(SkinTool.Picker); break;
                case Keys.L: PickTool(SkinTool.Line); break;
                case Keys.R: PickTool(SkinTool.Rect); break;
                case Keys.O: PickTool(SkinTool.Ellipse); break;
                case Keys.S: PickTool(SkinTool.Spray); break;
                case Keys.H: PickTool(SkinTool.Replace); break;
                case Keys.M: _mirror.Checked = !_mirror.Checked; break;
                case Keys.F: _fillShapes.Checked = !_fillShapes.Checked; break;
                case Keys.Oemplus: case Keys.Add: _canvas.Zoom(1.25f); break;
                case Keys.OemMinus: case Keys.Subtract: _canvas.Zoom(0.8f); break;
                case Keys.D0: case Keys.NumPad0: _canvas.ResetView(); break;
                case Keys.D1: _brush.Value = 1; break;
                case Keys.D2: _brush.Value = 2; break;
                case Keys.D3: _brush.Value = 3; break;
                case Keys.D4: _brush.Value = 4; break;
                case Keys.D5: _brush.Value = 5; break;
                default: return;
            }

            e.Handled = true;
        }

        // ---------------------------------------------------------------- палитра мышью

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Left && e.Y < TitleHeight && e.X < Width - 140)
            {
                NativeMethods.ReleaseCapture();
                NativeMethods.SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
                return;
            }

            if (_paletteBox.Contains(e.Location))
            {
                int col = (e.X - _paletteBox.X) / PalCell;
                int line = (e.Y - _paletteBox.Y) / PalCell;
                if (col < 0 || col >= PalCols) return;

                int index = line * PalCols + col;

                if (index >= 0 && index < Palette.Length) { _canvas.Ink = Palette[index]; Invalidate(); return; }

                if (index == Palette.Length)
                {
                    using (var dlg = new ColorDialog { FullOpen = true, Color = _canvas.Ink })
                        if (dlg.ShowDialog(this) == DialogResult.OK)
                        {
                            _canvas.Ink = Color.FromArgb(255, dlg.Color);
                            Invalidate();
                        }
                }
                return;
            }

            if (_skinPalBox.Contains(e.Location) && _fromSkin.Count > 0)
            {
                int col = (e.X - _skinPalBox.X) / PalCell;
                if (col >= 0 && col < _fromSkin.Count) { _canvas.Ink = _fromSkin[col]; Invalidate(); }
            }
        }

        // ---------------------------------------------------------------- вёрстка

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Normal) _lastSize = ClientSize;
            Layout1();
        }

        private void Layout1()
        {
            if (_tools == null || _tools.Count == 0) return;

            int w = ClientSize.Width, h = ClientSize.Height;
            int top = TitleHeight + 38;

            _closeX.Location = new Point(w - _closeX.Width - 10, (TitleHeight - _closeX.Height) / 2);
            _maxBtn.Location = new Point(_closeX.Left - _maxBtn.Width - 4, _closeX.Top);
            _minBtn.Location = new Point(_maxBtn.Left - _minBtn.Width - 4, _closeX.Top);

            // ---- сколько строк займёт нижний ряд
            var row = new[] { _steve, _alex, _web, _open, _save, _undo, _redo };
            int applyX = Math.Max(Pad, w - Pad - _apply.Width);
            int limit = Math.Max(Pad + 240, applyX - 14);

            int lines = 1, probe = Pad;
            foreach (var b in row)
            {
                if (probe != Pad && probe + b.Width > limit) { lines++; probe = Pad; }
                probe += b.Width + 10;
            }

            int bottom = 40 + lines * 42;
            int height = Math.Max(200, h - top - bottom);

            // ---- инструменты в три колонки
            int cell = (LeftWidth - (ToolCols - 1) * 6) / ToolCols;
            int y = top;
            for (int i = 0; i < _tools.Count; i++)
            {
                int col = i % ToolCols, line = i / ToolCols;
                _tools[i].Size = new Size(cell, 28);
                _tools[i].Location = new Point(Pad + col * (cell + 6), y + line * 32);
            }
            y += ((_tools.Count + ToolCols - 1) / ToolCols) * 32 + 10;

            // ---- кисть
            _brushLabelY = y;
            y += 17;
            _brush.SetBounds(Pad, y, LeftWidth, 26);
            y += 32;

            // оба переключателя в одну строку
            _mirror.Location = new Point(Pad, y);
            _fillShapes.Location = new Point(Pad + 122, y);
            y += 34;

            // ---- цвет
            y += 16;
            _paletteBox = new Rectangle(Pad, y, PalCols * PalCell,
                                        ((Palette.Length / PalCols) + 1) * PalCell);
            y = _paletteBox.Bottom + 5;

            _skinPalBox = new Rectangle(Pad, y, PalCols * PalCell, PalCell);
            y = _skinPalBox.Bottom + 8;

            _swatch = new Rectangle(Pad, y, 46, 26);
            _palFromSkin.Location = new Point(Pad + 56, y);

            // ---- холст и полоса фильтров под ним
            int canvasX = Pad + LeftWidth + 18;
            int canvasW = Math.Max(220, w - canvasX - RightWidth - Pad - 18);

            int filterCols = 4;
            int filterRows = (_filters.Count + filterCols - 1) / filterCols;
            int filtersH = 18 + filterRows * 32;

            int canvasH = Math.Max(160, height - filtersH - 8);
            _canvas.SetBounds(canvasX, top, canvasW, canvasH);

            _filtersLabelY = top + canvasH + 10;
            int fw = (canvasW - (filterCols - 1) * 4) / filterCols;
            for (int i = 0; i < _filters.Count; i++)
            {
                int col = i % filterCols, line = i / filterCols;
                _filters[i].Size = new Size(fw, 28);
                _filters[i].Location = new Point(canvasX + col * (fw + 4),
                                                 _filtersLabelY + 18 + line * 32);
            }

            // ---- правая колонка: модель тянется, список слоёв не меньше трёх строк
            int rx = canvasX + canvasW + 18;
            int togglesH = 126;
            int belowList = 6 + 34 + 16 + 26;          // кнопки слоёв и прозрачность
            int labelH = 18;

            int viewH = Math.Max(120, Math.Min(340, (int)(height * 0.40f)));
            int listH = height - viewH - 6 - togglesH - labelH - belowList;

            if (listH < 82)
            {
                listH = 82;
                viewH = Math.Max(100, height - 6 - togglesH - labelH - belowList - listH);
            }

            _view.SetBounds(rx, top, RightWidth, viewH);

            int ty = top + viewH + 6;
            _overlay.Location = new Point(rx, ty);
            _spin.Location = new Point(rx, ty + 30);
            _arms.Location = new Point(rx, ty + 60);
            _paint3d.Location = new Point(rx, ty + 90);

            int ly = ty + togglesH;
            _layersLabelY = ly;
            ly += labelH;
            _layers.SetBounds(rx, ly, RightWidth, listH);
            ly += listH + 6;

            int bx = rx;
            foreach (var b in new[] { _layerAdd, _layerCopy, _layerDel, _layerUp, _layerDown, _layerMerge })
            {
                b.Location = new Point(bx, ly);
                bx += b.Width + 4;
            }
            ly += 34 + 16;
            _opacity.SetBounds(rx, ly, RightWidth, 26);

            // ---- нижний ряд
            int by = h - bottom + 14;
            _apply.Location = new Point(applyX, by);

            int rowX = Pad, lineY = by;
            foreach (var b in row)
            {
                if (rowX != Pad && rowX + b.Width > limit) { rowX = Pad; lineY += 42; }
                b.Location = new Point(rowX, lineY);
                rowX += b.Width + 10;
            }

            _status.SetBounds(Pad, h - 24, Math.Max(100, w - Pad * 2), 18);

            Invalidate();
        }

        // ---------------------------------------------------------------- отрисовка

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_tools == null || _tools.Count == 0) return;

            var g = e.Graphics;
            Theme.Smooth(g);
            Color ink = _canvas.Ink;

            // заголовок
            var bar = new Rectangle(0, 0, Width, TitleHeight);
            using (var b = new SolidBrush(Theme.Sidebar)) g.FillRectangle(b, bar);
            using (var pen = new Pen(Color.FromArgb(28, 255, 255, 255)))
                g.DrawLine(pen, 0, TitleHeight - 1, Width, TitleHeight - 1);

            var logo = new Rectangle(18, (TitleHeight - 20) / 2, 20, 20);
            Theme.FillRoundGradient(g, logo, 6, Theme.Accent, Theme.Accent2, 35f);
            Theme.Text1(g, "N", Theme.F(10f, FontStyle.Bold), Color.White, logo,
                StringAlignment.Center, StringAlignment.Center);

            Theme.Text1(g, "Редактор скина", Theme.F(11f, FontStyle.Bold), Theme.Text,
                new Rectangle(48, 0, 300, TitleHeight));

            if (Doc != null)
                Theme.Text1(g, Doc.Name + " · слоёв: " + Doc.Layers.Count + " · " +
                               (Doc.Arms == SkinArms.Slim ? "тонкие руки" : "обычные руки"),
                    Theme.F(9f), Theme.Dim, new Rectangle(190, 0, 460, TitleHeight));

            // подписи колонок
            Theme.Text1(g, "ИНСТРУМЕНТ", Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                new Rectangle(Pad, TitleHeight + 16, 200, 14));

            Theme.Text1(g, "КИСТЬ · " + _canvas.BrushSize + " px", Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                new Rectangle(Pad, _brushLabelY, 200, 14));

            Theme.Text1(g, "Симметрия", Theme.F(9f), _mirror.Checked ? Theme.Accent : Theme.Muted,
                new Rectangle(_mirror.Right + 8, _mirror.Top - 2, 80, 26));
            Theme.Text1(g, "Залив.", Theme.F(9f), _fillShapes.Checked ? Theme.Accent : Theme.Muted,
                new Rectangle(_fillShapes.Right + 8, _fillShapes.Top - 2, 80, 26));

            Theme.Text1(g, "ЦВЕТ", Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                new Rectangle(_paletteBox.X, _paletteBox.Y - 18, 200, 14));

            for (int i = 0; i <= Palette.Length; i++)
            {
                int col = i % PalCols, line = i / PalCols;
                var r = new Rectangle(_paletteBox.X + col * PalCell, _paletteBox.Y + line * PalCell,
                                      PalCell - 4, PalCell - 4);

                if (i == Palette.Length)
                {
                    Theme.FillRound(g, r, 5, Theme.Card);
                    Theme.DrawRound(g, r, 5, Theme.Border);
                    Theme.Text1(g, "+", Theme.F(10f, FontStyle.Bold), Theme.Muted, r,
                        StringAlignment.Center, StringAlignment.Center);
                    continue;
                }

                Theme.FillRound(g, r, 5, Palette[i]);
                if (Palette[i].ToArgb() == ink.ToArgb()) Theme.DrawRound(g, r, 5, Color.White, 2f);
            }

            // палитра, снятая со скина
            for (int i = 0; i < PalCols; i++)
            {
                var r = new Rectangle(_skinPalBox.X + i * PalCell, _skinPalBox.Y, PalCell - 4, PalCell - 4);
                if (i < _fromSkin.Count)
                {
                    Theme.FillRound(g, r, 5, _fromSkin[i]);
                    if (_fromSkin[i].ToArgb() == ink.ToArgb()) Theme.DrawRound(g, r, 5, Color.White, 2f);
                }
                else Theme.DrawRound(g, r, 5, Color.FromArgb(40, 255, 255, 255));
            }

            Theme.FillRound(g, _swatch, 6, ink);
            Theme.DrawRound(g, _swatch, 6, Theme.Border);
            Theme.Text1(g, "#" + ink.R.ToString("X2") + ink.G.ToString("X2") + ink.B.ToString("X2"),
                Theme.F(8f, FontStyle.Bold), Color.FromArgb(230, 10, 12, 18), _swatch,
                StringAlignment.Center, StringAlignment.Center);

            // правая колонка
            Theme.Text1(g, "Накладка (шляпа, куртка)", Theme.F(9.5f), Theme.Muted,
                new Rectangle(_overlay.Right + 12, _overlay.Top - 2, 240, 26));
            Theme.Text1(g, "Вращение", Theme.F(9.5f), Theme.Muted,
                new Rectangle(_spin.Right + 12, _spin.Top - 2, 200, 26));
            Theme.Text1(g, "Тонкие руки (Алекс)", Theme.F(9.5f), Theme.Muted,
                new Rectangle(_arms.Right + 12, _arms.Top - 2, 240, 26));
            Theme.Text1(g, "Рисовать по модели", Theme.F(9.5f),
                _paint3d.Checked ? Theme.Accent : Theme.Muted,
                new Rectangle(_paint3d.Right + 12, _paint3d.Top - 2, 240, 26));

            string layersTitle = "СЛОИ";
            if (Doc != null) layersTitle += " · " + Doc.Layers.Count + "   (двойной щелчок — переименовать)";
            Theme.Text1(g, layersTitle, Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                new Rectangle(_layers.Left, _layersLabelY, 320, 14));

            Theme.Text1(g, "ПРОЗРАЧНОСТЬ СЛОЯ · " + _opacity.Value + "%", Theme.F(8.5f, FontStyle.Bold),
                Theme.Dim, new Rectangle(_opacity.Left, _opacity.Top - 16, 260, 14));

            Theme.Text1(g, "ФИЛЬТРЫ АКТИВНОГО СЛОЯ", Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                new Rectangle(_canvas.Left, _filtersLabelY, 320, 14));

            // подсказки над холстом
            Theme.Text1(g, "Колесо — масштаб · средняя — сдвиг · правая — стереть · " +
                           "B E G I L R O S H — инструменты · M — симметрия · Ctrl+Z / Ctrl+Y",
                Theme.F(8.5f), Theme.Dim,
                new Rectangle(_canvas.Left, TitleHeight + 16, Math.Max(100, _canvas.Width + RightWidth), 14));

            using (var pen = new Pen(Theme.Border))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        // ---------------------------------------------------------------- тянем за края

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;

            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                int raw = m.LParam.ToInt32();
                var screen = new Point((short)(raw & 0xFFFF), (short)((raw >> 16) & 0xFFFF));
                var p = PointToClient(screen);

                bool left = p.X <= Grip, right = p.X >= ClientSize.Width - Grip;
                bool top = p.Y <= Grip, bottom = p.Y >= ClientSize.Height - Grip;

                if (left && top) { m.Result = (IntPtr)13; return; }
                if (right && top) { m.Result = (IntPtr)14; return; }
                if (left && bottom) { m.Result = (IntPtr)16; return; }
                if (right && bottom) { m.Result = (IntPtr)17; return; }
                if (left) { m.Result = (IntPtr)10; return; }
                if (right) { m.Result = (IntPtr)11; return; }
                if (top) { m.Result = (IntPtr)12; return; }
                if (bottom) { m.Result = (IntPtr)15; return; }
            }

            base.WndProc(ref m);
        }

        // ---------------------------------------------------------------- маленький ввод

        /// <summary>Однострочный запрос текста в стиле лаунчера.</summary>
        private class Prompt : Form
        {
            private readonly NareTextBox _box = new NareTextBox { Placeholder = "Введите текст" };

            private Prompt(string title, string value)
            {
                Text = title;
                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.CenterParent;
                ClientSize = new Size(380, 136);
                BackColor = Theme.Bg;
                ForeColor = Theme.Text;
                Font = Theme.F(9.5f);
                KeyPreview = true;
                ShowInTaskbar = false;
                DoubleBuffered = true;

                _box.Text = value ?? "";
                _box.SetBounds(20, 48, 340, 38);
                Controls.Add(_box);

                var ok = new NareButton { Text = "ОК", Style = BtnStyle.Accent, Size = new Size(120, 34) };
                ok.Location = new Point(240, 94);
                ok.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
                Controls.Add(ok);

                var cancel = new NareButton { Text = "Отмена", Style = BtnStyle.Ghost, Size = new Size(104, 34) };
                cancel.Location = new Point(20, 94);
                cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
                Controls.Add(cancel);

                _box.Inner.KeyDown += (s, e) =>
                {
                    if (e.KeyCode != Keys.Enter) return;
                    e.SuppressKeyPress = true;
                    DialogResult = DialogResult.OK;
                    Close();
                };
            }

            public static string Ask(IWin32Window owner, string title, string value)
            {
                using (var form = new Prompt(title, value))
                {
                    if (form.ShowDialog(owner) != DialogResult.OK) return null;
                    return form._box.Text;
                }
            }

            protected override void OnShown(EventArgs e)
            {
                base.OnShown(e);
                _box.Inner.Focus();
                _box.Inner.SelectAll();
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                base.OnKeyDown(e);
                if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Theme.Smooth(e.Graphics);
                Theme.Text1(e.Graphics, Text, Theme.F(11f, FontStyle.Bold), Theme.Text,
                    new Rectangle(20, 12, 340, 26));
                using (var pen = new Pen(Theme.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }
    }
}
