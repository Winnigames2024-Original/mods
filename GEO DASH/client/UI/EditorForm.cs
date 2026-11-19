using GEO_DASH.Core;
using GEO_DASH.Net;
using GEO_DASH.Render;

namespace GEO_DASH.UI
{
    public class EditorForm : Form
    {
        LevelData _lv;
        readonly GameWorld _preview = new();
        readonly ViewTransform _v = new();
        Panel _canvas;
        ListBox _objList;
        ComboBox _catBox;
        Panel _props;
        Label _status;

        double _camX = 300, _camY = 240, _zoom = 1;
        int _grid = 30;
        bool _snap = true;
        int _selectedType = 1;
        readonly List<int> _sel = new();
        PointF _dragStart, _lastMouse;
        bool _panning, _dragging, _boxSelect;
        RectangleF _selBox;
        readonly List<string> _undo = new();
        readonly List<string> _redo = new();

        // текущие свойства для новых объектов
        PortalKind _portalKind = PortalKind.ModeShip;
        OrbKind _orbKind = OrbKind.Yellow;
        PadKind _padKind = PadKind.Yellow;
        TriggerData _trigTemplate = new();

        public EditorForm(LevelData lv)
        {
            _lv = lv;
            Theme.Apply(this, "Редактор — " + lv.Settings.Name);
            ClientSize = new Size(1420, 820);
            WindowState = FormWindowState.Maximized;
            BuildUi();
            _preview.Init(_lv);
            PushUndo();
        }

        void BuildUi()
        {
            // ---------- верхняя панель ----------
            var top = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.Panel };
            Controls.Add(top);

            int x = 8;
            Button T(string t, int w, EventHandler h, Color? c = null)
            {
                var b = Theme.Btn(t, x, 5, w, 34, c);
                b.Click += h;
                top.Controls.Add(b);
                x += w + 6;
                return b;
            }

            T("Сохранить", 100, (s, e) => Save());
            T("Тест ▶", 80, (s, e) => Playtest(false), Theme.Good);
            T("Тренировка", 95, (s, e) => Playtest(true));
            T("Настройки уровня", 140, (s, e) => OpenSettings(), Theme.Accent);
            T("Музыка по ID", 110, (s, e) => OpenMusic());
            T("Загрузить на сервер", 150, (s, e) => Upload(), Theme.Accent2);
            T("Копия", 70, (s, e) => { var c = LocalStore.CopyLevel(_lv); MessageBox.Show("Создана копия: " + c.Settings.Name); });
            T("Отмена", 70, (s, e) => Undo());
            T("Вернуть", 70, (s, e) => Redo());

            var gridLbl = Theme.Lbl("Сетка:", x, 14, 45); top.Controls.Add(gridLbl); x += 48;
            var gridCb = Theme.Cb(x, 8, 70);
            gridCb.Items.AddRange(new object[] { "30", "15", "10", "60" });
            gridCb.SelectedIndex = 0;
            gridCb.SelectedIndexChanged += (s, e) => { _grid = int.Parse(gridCb.Text); };
            top.Controls.Add(gridCb); x += 76;

            var snapChk = Theme.Chk("Привязка", x, 12, true, 90);
            snapChk.CheckedChanged += (s, e) => _snap = snapChk.Checked;
            top.Controls.Add(snapChk); x += 96;

            var hbChk = Theme.Chk("Хитбоксы", x, 12, false, 90);
            hbChk.CheckedChanged += (s, e) => { Renderer.ShowHitboxes = hbChk.Checked; _canvas.Invalidate(); };
            top.Controls.Add(hbChk);

            // ---------- левая палитра ----------
            var left = new Panel { Dock = DockStyle.Left, Width = 210, BackColor = Theme.Bg2 };
            Controls.Add(left);
            left.Controls.Add(Theme.Lbl("Объекты", 8, 8, 180, Theme.H2));
            _catBox = Theme.Cb(8, 34, 190);
            foreach (var c in ObjectCatalog.Categories()) _catBox.Items.Add(c);
            _catBox.SelectedIndexChanged += (s, e) => FillObjects();
            left.Controls.Add(_catBox);

            _objList = new ListBox
            {
                Left = 8, Top = 64, Width = 190, Height = 360,
                BackColor = Theme.Bg, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle
            };
            _objList.SelectedIndexChanged += (s, e) =>
            {
                if (_objList.SelectedItem is ObjectDef d) { _selectedType = d.Id; UpdateProps(); }
            };
            left.Controls.Add(_objList);

            var hint = Theme.Lbl(
                "ЛКМ — поставить\nПКМ — удалить\nSHIFT+ЛКМ — выделить\nсредняя/пробел — двигать вид\nколесо — зум\nDel — удалить выделенное\nCtrl+C/V — копировать\nCtrl+D — дублировать\nCtrl+Z/Y — отмена",
                8, 436, 190, Theme.Body, Theme.Dim);
            hint.Height = 180;
            left.Controls.Add(hint);

            // ---------- правая панель свойств ----------
            _props = new Panel { Dock = DockStyle.Right, Width = 280, BackColor = Theme.Bg2, AutoScroll = true };
            Controls.Add(_props);

            // ---------- канва ----------
            _canvas = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            typeof(Panel).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(_canvas, true);
            Controls.Add(_canvas);
            _canvas.BringToFront();

            _canvas.Paint += CanvasPaint;
            _canvas.MouseDown += CanvasDown;
            _canvas.MouseMove += CanvasMove;
            _canvas.MouseUp += CanvasUp;
            _canvas.MouseWheel += (s, e) =>
            {
                double f = e.Delta > 0 ? 1.12 : 1 / 1.12;
                _zoom = Math.Clamp(_zoom * f, 0.2, 4);
                _canvas.Invalidate();
            };

            _status = new Label { Dock = DockStyle.Bottom, Height = 24, ForeColor = Theme.Dim, BackColor = Theme.Panel, TextAlign = ContentAlignment.MiddleLeft };
            Controls.Add(_status);

            KeyDown += EditorKeys;
            _catBox.SelectedIndex = 0;
            UpdateProps();
        }

        void FillObjects()
        {
            _objList.Items.Clear();
            string cat = _catBox.Text;
            foreach (var d in ObjectCatalog.All.Values)
                if (d.Category == cat) _objList.Items.Add(d);
            _objList.DisplayMember = "Name";
            if (_objList.Items.Count > 0) _objList.SelectedIndex = 0;
        }

        // ------------------------------------------------------------ свойства

        void UpdateProps()
        {
            _props.Controls.Clear();
            int y = 8;
            void Add(Control c) { _props.Controls.Add(c); y = c.Bottom + 6; }

            Add(Theme.Lbl("Свойства", 8, y, 240, Theme.H2));

            var def = ObjectCatalog.Get(_selectedType);
            Add(Theme.Lbl("Объект: " + def.Name, 8, y, 250, Theme.Body, Theme.Accent));

            if (def.Kind == ObjKind.Portal)
            {
                Add(Theme.Lbl("Тип портала", 8, y, 240));
                var cb = Theme.Cb(8, y, 250);
                foreach (PortalKind k in Enum.GetValues<PortalKind>())
                    if (k != PortalKind.None) cb.Items.Add(k);
                cb.SelectedItem = _portalKind;
                cb.SelectedIndexChanged += (s, e) => { _portalKind = (PortalKind)cb.SelectedItem; ApplyToSelection(o => o.Portal = _portalKind); };
                Add(cb);
            }
            if (def.Kind == ObjKind.Orb)
            {
                Add(Theme.Lbl("Тип шара", 8, y, 240));
                var cb = Theme.Cb(8, y, 250);
                foreach (OrbKind k in Enum.GetValues<OrbKind>()) cb.Items.Add(k);
                cb.SelectedItem = _orbKind;
                cb.SelectedIndexChanged += (s, e) => { _orbKind = (OrbKind)cb.SelectedItem; ApplyToSelection(o => o.Orb = _orbKind); };
                Add(cb);
            }
            if (def.Kind == ObjKind.Pad)
            {
                Add(Theme.Lbl("Тип пада", 8, y, 240));
                var cb = Theme.Cb(8, y, 250);
                foreach (PadKind k in Enum.GetValues<PadKind>()) cb.Items.Add(k);
                cb.SelectedItem = _padKind;
                cb.SelectedIndexChanged += (s, e) => { _padKind = (PadKind)cb.SelectedItem; ApplyToSelection(o => o.Pad = _padKind); };
                Add(cb);
            }
            if (def.Kind == ObjKind.Trigger)
            {
                var b = Theme.Btn("Настроить триггер…", 8, y, 250, 34, Theme.Accent);
                b.Click += (s, e) =>
                {
                    using var f = new TriggerEditForm(_trigTemplate);
                    if (f.ShowDialog(this) == DialogResult.OK)
                    {
                        _trigTemplate = f.Data;
                        ApplyToSelection(o => o.Trigger = _trigTemplate.Clone());
                    }
                };
                Add(b);
                Add(Theme.Lbl("Текущий: " + TriggerEngine.Label(_trigTemplate.Type), 8, y, 250, Theme.Body, Theme.Dim));
            }

            Add(Theme.Lbl("— Выделение (" + _sel.Count + ") —", 8, y + 6, 250, Theme.H2));

            if (_sel.Count > 0)
            {
                var first = _lv.Objects[_sel[0]];

                Add(Theme.Lbl("Группы (через запятую)", 8, y, 250));
                var tbG = Theme.Tb(8, y, 250, string.Join(",", first.Groups));
                tbG.TextChanged += (s, e) =>
                {
                    var gs = tbG.Text.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                     .Select(t => int.TryParse(t.Trim(), out var g) ? g : 0)
                                     .Where(g => g > 0).Distinct().ToList();
                    ApplyToSelection(o => o.Groups = new List<int>(gs));
                };
                Add(tbG);

                Add(Theme.Lbl("Поворот", 8, y, 120));
                var nRot = Theme.Num(8, y, -360, 360, first.Rotation, 0, 110);
                nRot.ValueChanged += (s, e) => ApplyToSelection(o => o.Rotation = (double)nRot.Value);
                Add(nRot);

                Add(Theme.Lbl("Масштаб X / Y", 8, y, 150));
                var nSx = Theme.Num(8, y, 0.1, 10, first.ScaleX, 1, 110);
                var nSy = Theme.Num(130, y, 0.1, 10, first.ScaleY, 1, 110);
                nSx.ValueChanged += (s, e) => ApplyToSelection(o => o.ScaleX = (double)nSx.Value);
                nSy.ValueChanged += (s, e) => ApplyToSelection(o => o.ScaleY = (double)nSy.Value);
                _props.Controls.Add(nSy);
                Add(nSx);

                Add(Theme.Lbl("Канал цвета / деталей", 8, y, 200));
                var nC1 = Theme.Num(8, y, 1, 999, first.BaseColorChannel, 0, 110);
                var nC2 = Theme.Num(130, y, 1, 999, first.DetailColorChannel, 0, 110);
                nC1.ValueChanged += (s, e) => ApplyToSelection(o => o.BaseColorChannel = (int)nC1.Value);
                nC2.ValueChanged += (s, e) => ApplyToSelection(o => o.DetailColorChannel = (int)nC2.Value);
                _props.Controls.Add(nC2);
                Add(nC1);

                Add(Theme.Lbl("Прозрачность", 8, y, 150));
                var nA = Theme.Num(8, y, 0, 1, first.Opacity, 2, 110);
                nA.ValueChanged += (s, e) => ApplyToSelection(o => o.Opacity = (double)nA.Value);
                Add(nA);

                Add(Theme.Lbl("Z-порядок", 8, y, 150));
                var nZ = Theme.Num(8, y, -20, 20, first.ZOrder, 0, 110);
                nZ.ValueChanged += (s, e) => ApplyToSelection(o => o.ZOrder = (int)nZ.Value);
                Add(nZ);

                if (ObjectCatalog.Get(first.TypeId).Kind == ObjKind.Text)
                {
                    Add(Theme.Lbl("Текст", 8, y, 250));
                    var tbT = Theme.Tb(8, y, 250, first.Text);
                    tbT.TextChanged += (s, e) => ApplyToSelection(o => o.Text = tbT.Text);
                    Add(tbT);
                }

                if (first.Trigger != null)
                {
                    var b = Theme.Btn("Править триггер выделенного…", 8, y, 250, 34, Theme.Accent);
                    b.Click += (s, e) =>
                    {
                        using var f = new TriggerEditForm(first.Trigger);
                        if (f.ShowDialog(this) == DialogResult.OK)
                            ApplyToSelection(o => { if (o.Trigger != null) o.Trigger = f.Data.Clone(); });
                    };
                    Add(b);
                }

                var bd = Theme.Btn("Удалить выделенное", 8, y, 250, 32, Theme.Bad);
                bd.Click += (s, e) => DeleteSelection();
                Add(bd);
            }
            else
            {
                Add(Theme.Lbl("Ничего не выделено.\nSHIFT+ЛКМ — выделить объект\nSHIFT+рамка — выделить область",
                    8, y, 250, Theme.Body, Theme.Dim));
            }
        }

        void ApplyToSelection(Action<LevelObject> f)
        {
            if (_sel.Count == 0) return;
            PushUndo();
            foreach (var i in _sel) if (i >= 0 && i < _lv.Objects.Count) f(_lv.Objects[i]);
            RebuildPreview();
        }

        // ------------------------------------------------------------ канва

        void CanvasPaint(object s, PaintEventArgs e)
        {
            var g = e.Graphics;
            _v.Client = _canvas.ClientSize;
            _v.CamX = _camX; _v.CamY = _camY; _v.Zoom = _zoom;

            Renderer.Draw(g, _preview, _v, true);
            DrawGrid(g);

            // выделение
            foreach (var i in _sel)
            {
                if (i < 0 || i >= _lv.Objects.Count) continue;
                var b = ObjectCatalog.Bounds(_lv.Objects[i]);
                var r = _v.WorldRect(b.X, b.Y, b.Width, b.Height);
                using var pen = new Pen(Theme.Accent2, 2f);
                g.DrawRectangle(pen, r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2);
            }

            if (_boxSelect)
            {
                var a = _v.W2S(_selBox.Left, _selBox.Top);
                var b2 = _v.W2S(_selBox.Right, _selBox.Bottom);
                var r = RectangleF.FromLTRB(Math.Min(a.X, b2.X), Math.Min(a.Y, b2.Y), Math.Max(a.X, b2.X), Math.Max(a.Y, b2.Y));
                using var pen = new Pen(Theme.Accent, 1.5f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
            }

            _status.Text = $"объектов: {_lv.Objects.Count}   выделено: {_sel.Count}   зум: {_zoom:0.00}   " +
                           $"камера: {_camX:0}/{_camY:0}   длина уровня: {_lv.RightEdge() / 30:0} клеток   " +
                           $"музыка: {(_lv.Settings.SongId > 0 ? "#" + _lv.Settings.SongId : "нет")}   " +
                           $"сложность: {Difficulties.Label(_lv.Settings.Difficulty)}";
        }

        void DrawGrid(Graphics g)
        {
            float sc = _v.Scale;
            if (sc * _grid < 5) return;
            var vis = _v.VisibleWorld();
            using var pen = new Pen(Color.FromArgb(40, 255, 255, 255), 1);
            using var pen2 = new Pen(Color.FromArgb(80, 255, 255, 255), 1);
            double x0 = Math.Floor(vis.Left / _grid) * _grid;
            for (double x = x0; x < vis.Right; x += _grid)
            {
                var p1 = _v.W2S(x, vis.Top); var p2 = _v.W2S(x, vis.Bottom);
                g.DrawLine(Math.Abs(x % (_grid * 8)) < 0.01 ? pen2 : pen, p1.X, p1.Y, p2.X, p2.Y);
            }
            double y0 = Math.Floor(vis.Top / _grid) * _grid;
            for (double y = y0; y < vis.Bottom; y += _grid)
            {
                var p1 = _v.W2S(vis.Left, y); var p2 = _v.W2S(vis.Right, y);
                g.DrawLine(Math.Abs(y % (_grid * 8)) < 0.01 ? pen2 : pen, p1.X, p1.Y, p2.X, p2.Y);
            }
        }

        PointF WorldOf(Point p)
        {
            var w = _v.S2W(p.X, p.Y);
            if (_snap)
                w = new PointF((float)(Math.Floor(w.X / _grid) * _grid), (float)(Math.Floor(w.Y / _grid) * _grid));
            return w;
        }

        void CanvasDown(object s, MouseEventArgs e)
        {
            _canvas.Focus();
            _lastMouse = e.Location;
            bool shift = (ModifierKeys & Keys.Shift) != 0;

            if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && (ModifierKeys & Keys.Alt) != 0))
            {
                _panning = true;
                return;
            }

            var w = _v.S2W(e.X, e.Y);

            if (e.Button == MouseButtons.Left)
            {
                if (shift)
                {
                    int hit = HitTest(w);
                    if (hit >= 0)
                    {
                        if (_sel.Contains(hit)) _sel.Remove(hit); else _sel.Add(hit);
                        UpdateProps();
                    }
                    else
                    {
                        _boxSelect = true;
                        _dragStart = w;
                        _selBox = new RectangleF(w.X, w.Y, 0, 0);
                    }
                    _canvas.Invalidate();
                    return;
                }

                int under = HitTest(w);
                if (under >= 0 && _sel.Contains(under))
                {
                    _dragging = true;
                    _dragStart = w;
                    return;
                }

                PlaceObject(WorldOf(e.Location));
                return;
            }

            if (e.Button == MouseButtons.Right)
            {
                int hit = HitTest(w);
                if (hit >= 0)
                {
                    PushUndo();
                    _lv.Objects.RemoveAt(hit);
                    _sel.Clear();
                    RebuildPreview();
                    UpdateProps();
                }
            }
        }

        void CanvasMove(object s, MouseEventArgs e)
        {
            if (_panning)
            {
                float sc = _v.Scale;
                _camX -= (e.X - _lastMouse.X) / sc;
                _camY += (e.Y - _lastMouse.Y) / sc;
                _lastMouse = e.Location;
                _canvas.Invalidate();
                return;
            }

            var w = _v.S2W(e.X, e.Y);

            if (_boxSelect)
            {
                _selBox = RectangleF.FromLTRB(
                    Math.Min(_dragStart.X, w.X), Math.Min(_dragStart.Y, w.Y),
                    Math.Max(_dragStart.X, w.X), Math.Max(_dragStart.Y, w.Y));
                _canvas.Invalidate();
                return;
            }

            if (_dragging && _sel.Count > 0 && e.Button == MouseButtons.Left)
            {
                double dx = w.X - _dragStart.X, dy = w.Y - _dragStart.Y;
                if (_snap) { dx = Math.Round(dx / _grid) * _grid; dy = Math.Round(dy / _grid) * _grid; }
                if (Math.Abs(dx) >= _grid || Math.Abs(dy) >= _grid || !_snap)
                {
                    foreach (var i in _sel) { _lv.Objects[i].X += dx; _lv.Objects[i].Y += dy; }
                    _dragStart = new PointF((float)(_dragStart.X + dx), (float)(_dragStart.Y + dy));
                    RebuildPreview();
                }
            }

            // рисование «кистью» при зажатой ЛКМ без выделения
            if (e.Button == MouseButtons.Left && !_dragging && !_boxSelect && (ModifierKeys & Keys.Shift) == 0)
            {
                var gp = WorldOf(e.Location);
                if (!_lv.Objects.Any(o => Math.Abs(o.X - gp.X) < 1 && Math.Abs(o.Y - gp.Y) < 1 && o.TypeId == _selectedType))
                    PlaceObject(gp, false);
            }
        }

        void CanvasUp(object s, MouseEventArgs e)
        {
            if (_boxSelect)
            {
                _boxSelect = false;
                _sel.Clear();
                for (int i = 0; i < _lv.Objects.Count; i++)
                {
                    var b = ObjectCatalog.Bounds(_lv.Objects[i]);
                    if (_selBox.IntersectsWith(new RectangleF(b.X, b.Y, Math.Max(1, b.Width), Math.Max(1, b.Height))))
                        _sel.Add(i);
                }
                UpdateProps();
            }
            _panning = false;
            _dragging = false;
            _canvas.Invalidate();
        }

        int HitTest(PointF w)
        {
            for (int i = _lv.Objects.Count - 1; i >= 0; i--)
            {
                var b = ObjectCatalog.Bounds(_lv.Objects[i]);
                if (w.X >= b.X && w.X <= b.Right && w.Y >= b.Y && w.Y <= b.Bottom) return i;
            }
            return -1;
        }

        void PlaceObject(PointF p, bool undo = true)
        {
            if (undo) PushUndo();
            var def = ObjectCatalog.Get(_selectedType);
            var o = new LevelObject { TypeId = _selectedType, X = p.X, Y = p.Y };
            if (def.Kind == ObjKind.Portal) o.Portal = _portalKind;
            if (def.Kind == ObjKind.Orb) o.Orb = _orbKind;
            if (def.Kind == ObjKind.Pad) o.Pad = _padKind;
            if (def.Kind == ObjKind.Trigger) o.Trigger = _trigTemplate.Clone();
            if (def.Kind == ObjKind.Text) o.Text = "Текст";
            _lv.Add(o);
            RebuildPreview();
        }

        void DeleteSelection()
        {
            if (_sel.Count == 0) return;
            PushUndo();
            foreach (var i in _sel.OrderByDescending(v => v))
                if (i >= 0 && i < _lv.Objects.Count) _lv.Objects.RemoveAt(i);
            _sel.Clear();
            RebuildPreview();
            UpdateProps();
        }

        List<LevelObject> _clipboard = new();

        void EditorKeys(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete) { DeleteSelection(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.Z) { Undo(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.Y) { Redo(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.S) { Save(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.C)
            {
                _clipboard = _sel.Where(i => i < _lv.Objects.Count).Select(i => _lv.Objects[i].Clone()).ToList();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.V)
            {
                if (_clipboard.Count > 0)
                {
                    PushUndo();
                    _sel.Clear();
                    foreach (var o in _clipboard)
                    {
                        var c = o.Clone(); c.Id = 0; c.X += 30;
                        _lv.Add(c);
                        _sel.Add(_lv.Objects.Count - 1);
                    }
                    RebuildPreview(); UpdateProps();
                }
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.D)
            {
                if (_sel.Count > 0)
                {
                    PushUndo();
                    var copies = _sel.Select(i => _lv.Objects[i].Clone()).ToList();
                    _sel.Clear();
                    foreach (var c in copies) { c.Id = 0; c.X += 30; _lv.Add(c); _sel.Add(_lv.Objects.Count - 1); }
                    RebuildPreview(); UpdateProps();
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5) { Playtest(false); e.Handled = true; }
            else if (e.KeyCode == Keys.Space) { _panning = false; }
        }

        void RebuildPreview()
        {
            _preview.Init(_lv);
            _canvas.Invalidate();
        }

        void PushUndo()
        {
            _undo.Add(_lv.ToJson());
            if (_undo.Count > 60) _undo.RemoveAt(0);
            _redo.Clear();
        }

        void Undo()
        {
            if (_undo.Count < 2) return;
            _redo.Add(_lv.ToJson());
            _undo.RemoveAt(_undo.Count - 1);
            _lv = LevelData.FromJson(_undo[^1]);
            _sel.Clear();
            RebuildPreview(); UpdateProps();
        }

        void Redo()
        {
            if (_redo.Count == 0) return;
            _undo.Add(_lv.ToJson());
            _lv = LevelData.FromJson(_redo[^1]);
            _redo.RemoveAt(_redo.Count - 1);
            _sel.Clear();
            RebuildPreview(); UpdateProps();
        }

        // ------------------------------------------------------------ действия

        void Save()
        {
            LocalStore.SaveMyLevel(_lv);
            _status.Text = "Сохранено: " + LocalStore.MyLevelPath(_lv);
        }

        void Playtest(bool practice)
        {
            Save();
            using var f = new GameForm(_lv.DeepClone(), practice, true);
            f.ShowDialog(this);
        }

        void OpenSettings()
        {
            using var f = new LevelSettingsForm(_lv.Settings);
            if (f.ShowDialog(this) == DialogResult.OK)
            {
                RebuildPreview();
                Text = AppConfig.AppName + " — Редактор — " + _lv.Settings.Name;
            }
        }

        void OpenMusic()
        {
            using var f = new MusicForm(_lv.Settings.SongId);
            if (f.ShowDialog(this) == DialogResult.OK)
            {
                _lv.Settings.SongId = f.SongId;
                _lv.Settings.SongName = f.SongName;
                _lv.Settings.SongArtist = f.SongArtist;
                _canvas.Invalidate();
            }
        }

        async void Upload()
        {
            Save();
            if (!AppConfig.LoggedIn)
            {
                MessageBox.Show("Сначала войди в аккаунт (меню → Аккаунт).");
                return;
            }
            _status.Text = "Загружаю на сервер…";
            var r = await ApiClient.UploadLevelAsync(_lv, _lv.Settings.ServerId, _lv.Settings.Password);
            if (r.Ok && r.Data != null)
            {
                _lv.Settings.ServerId = r.Data.Id;
                _lv.Settings.Version++;
                Save();
                _status.Text = $"Загружено. ID уровня на сервере: {r.Data.Id}";
                MessageBox.Show($"Уровень на сервере под ID {r.Data.Id}.\nСсылка: {AppConfig.Settings.SiteUrl}levels/view.php?id={r.Data.Id}");
            }
            else
            {
                _status.Text = "Не вышло: " + r.Error;
                MessageBox.Show(r.Error + "\n\nЕсли нет сети — уровень уедет на сервер сам, когда сеть появится.");
            }
        }
    }
}
