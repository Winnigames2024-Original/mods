using GEO_DASH.Audio;
using GEO_DASH.Core;
using GEO_DASH.Net;
using GEO_DASH.Render;

namespace GEO_DASH.UI
{
    /// <summary>
    /// Редактор уровней по образцу игры: режимы BUILD / EDIT / DELETE,
    /// палитра объектов по вкладкам внизу, панель правки со стрелками.
    /// Объекты ставятся по одному клику — постановки свайпом нет.
    /// </summary>
    public class EditorForm : Form
    {
        enum Mode { Build, Edit, Delete }

        LevelData _lv;
        readonly GameWorld _preview = new();
        readonly ViewTransform _v = new();

        GdPanel _canvas;
        Panel _bottom, _modeBar, _content;
        Label _status;

        double _camX = 300, _camY = 240, _zoom = 1;
        int _grid = 30;
        bool _snap = true;
        Mode _mode = Mode.Build;

        readonly List<int> _sel = new();
        List<LevelObject> _clipboard = new();
        readonly List<string> _undo = new();
        readonly List<string> _redo = new();

        LevelObject _brush = new() { TypeId = 1 };
        /// <summary>Картинки с сайта, доступные в палитре.</summary>
        readonly List<Net.ImageInfo> _images = new();
        string _category = "Блоки";
        double _step = 30;            // шаг стрелок: 15 / 30 / 150

        PointF _dragStart, _lastMouse;
        bool _panning, _movingSel, _boxSelect;
        RectangleF _selBox;

        /// <summary>Свайп-выделение: ведёшь мышью/пальцем — всё под курсором выделяется.</summary>
        bool _swipe = true;
        bool _swiping;
        /// <summary>Выбранная в палитре конструкция (ставится целиком).</summary>
        BuildPiece _piece;

        public EditorForm(LevelData lv)
        {
            _lv = lv;
            Text = AppConfig.AppName + " — Редактор — " + lv.Settings.Name;
            BackColor = Gd.Deep;
            ForeColor = Gd.Paper;
            Font = Gd.Reg(9.5f);
            ClientSize = new Size(1440, 860);
            WindowState = FormWindowState.Maximized;
            KeyPreview = true;

            BuildUi();
            _preview.Init(_lv);
            PushUndo();
            FormClosing += (s, e) => Save();
        }

        // ------------------------------------------------------------ интерфейс

        void BuildUi()
        {
            _status = new Label
            {
                Dock = DockStyle.Bottom, Height = 22, ForeColor = Theme.Dim,
                BackColor = Color.FromArgb(14, 12, 34), TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(_status);

            _bottom = new Panel { Dock = DockStyle.Bottom, Height = 236, BackColor = Color.FromArgb(18, 16, 44) };
            Controls.Add(_bottom);

            var top = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Color.FromArgb(18, 16, 44) };
            Controls.Add(top);

            _canvas = new GdPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(16, 14, 40) };
            Controls.Add(_canvas);
            _canvas.BringToFront();

            // ---------- верхняя панель ----------
            int x = 8;
            GdButton T(string text, GdIcon ic, int w, Color c1, Color c2, EventHandler h)
            {
                var b = new GdButton
                {
                    Text = text, Icon = ic, Left = x, Top = 4, Width = w, Height = 44,
                    Color1 = c1, Color2 = c2, FontSize = 9.5f
                };
                b.Click += h;
                top.Controls.Add(b);
                x += w + 6;
                return b;
            }

            T("ВЫХОД", GdIcon.Back, 110, Color.FromArgb(160, 160, 190), Color.FromArgb(70, 70, 110),
                (s, e) => { Save(); Close(); });
            T("СОХР.", GdIcon.Save, 100, Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100),
                (s, e) => { Save(); Status("Сохранено"); });
            T("ОТМЕНА", GdIcon.Back, 110, Color.FromArgb(170, 175, 210), Color.FromArgb(70, 76, 125), (s, e) => Undo());
            T("ВЕРНУТЬ", GdIcon.Refresh, 112, Color.FromArgb(170, 175, 210), Color.FromArgb(70, 76, 125), (s, e) => Redo());
            T("ТЕСТ", GdIcon.Play, 100, Color.FromArgb(120, 220, 255), Color.FromArgb(20, 110, 180), (s, e) => Playtest(false));
            T("ТРЕНИР.", GdIcon.Check, 110, Color.FromArgb(150, 185, 255), Color.FromArgb(50, 80, 170), (s, e) => Playtest(true));
            T("НАСТРОЙКИ", GdIcon.Settings, 130, Color.FromArgb(200, 170, 255), Color.FromArgb(100, 60, 180), (s, e) => OpenSettings());
            T("МУЗЫКА", GdIcon.Music, 115, Color.FromArgb(255, 180, 230), Color.FromArgb(150, 60, 130), (s, e) => OpenMusic());
            T("НА СЕРВЕР", GdIcon.Upload, 135, Color.FromArgb(255, 210, 90), Color.FromArgb(170, 120, 10), async (s, e) => await Upload());

            x += 14;
            T("", GdIcon.Plus, 48, Color.FromArgb(140, 150, 190), Color.FromArgb(60, 66, 105),
                (s, e) => { _zoom = Math.Min(4, _zoom * 1.25); _canvas.Invalidate(); });
            T("", GdIcon.Cross, 48, Color.FromArgb(140, 150, 190), Color.FromArgb(60, 66, 105),
                (s, e) => { _zoom = Math.Max(0.2, _zoom / 1.25); _canvas.Invalidate(); });

            var snap = new CheckBox
            {
                Text = "привязка", Left = x + 6, Top = 16, Width = 90, Checked = true,
                ForeColor = Gd.Paper, BackColor = Color.Transparent, FlatStyle = FlatStyle.Flat
            };
            snap.CheckedChanged += (s, e) => _snap = snap.Checked;
            top.Controls.Add(snap);

            var hb = new CheckBox
            {
                Text = "хитбоксы", Left = x + 102, Top = 16, Width = 92,
                ForeColor = Gd.Paper, BackColor = Color.Transparent, FlatStyle = FlatStyle.Flat
            };
            hb.CheckedChanged += (s, e) => { Renderer.ShowHitboxes = hb.Checked; _canvas.Invalidate(); };
            top.Controls.Add(hb);

            // ---------- режимы ----------
            _modeBar = new Panel { Dock = DockStyle.Left, Width = 132, BackColor = Color.FromArgb(22, 20, 52) };
            _bottom.Controls.Add(_modeBar);

            void M(string text, GdIcon ic, int top2, Mode m, Color c1, Color c2)
            {
                var b = new GdButton
                {
                    Text = text, Icon = ic, Left = 8, Top = top2, Width = 116, Height = 68,
                    Color1 = c1, Color2 = c2, FontSize = 10f
                };
                b.Click += (s, e) => SetMode(m);
                _modeBar.Controls.Add(b);
            }
            M("BUILD", GdIcon.Plus, 8, Mode.Build, Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100));
            M("EDIT", GdIcon.Edit, 82, Mode.Edit, Color.FromArgb(255, 190, 80), Color.FromArgb(170, 100, 10));
            M("DELETE", GdIcon.Trash, 156, Mode.Delete, Color.FromArgb(240, 120, 120), Color.FromArgb(150, 40, 50));

            _content = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(18, 16, 44) };
            _bottom.Controls.Add(_content);
            _content.BringToFront();

            // ---------- канва ----------
            _canvas.Paint += CanvasPaint;
            _canvas.MouseDown += CanvasDown;
            _canvas.MouseMove += CanvasMove;
            _canvas.MouseUp += CanvasUp;
            _canvas.MouseWheel += (s, e) =>
            {
                _zoom = Math.Clamp(_zoom * (e.Delta > 0 ? 1.12 : 1 / 1.12), 0.2, 4);
                _canvas.Invalidate();
            };
            _canvas.MouseEnter += (s, e) => _canvas.Focus();

            KeyDown += Keys_;
            SetMode(Mode.Build);
        }

        void SetMode(Mode m)
        {
            _mode = m;
            Sfx.Play("click");
            _content.Controls.Clear();
            if (m == Mode.Build) BuildPalette();
            else if (m == Mode.Edit) BuildEditPanel();
            else BuildDeletePanel();
            _canvas.Invalidate();
            UpdateStatus();
        }

        // ---------------------------------------------------- палитра (BUILD)

        FlowLayoutPanel _palette;

        void BuildPalette()
        {
            var tabs = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 42, BackColor = Color.FromArgb(24, 22, 56),
                WrapContents = false, AutoScroll = false, Padding = new Padding(6, 4, 6, 0)
            };
            _content.Controls.Add(tabs);

            _palette = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, BackColor = Color.FromArgb(18, 16, 44),
                AutoScroll = true, Padding = new Padding(8, 6, 8, 6)
            };
            _content.Controls.Add(_palette);
            _palette.BringToFront();

            foreach (var cat in new[] { "Блоки", "Шипы", "Порталы", "Шары и пады", "Декор", "Жидкости", "Картинки", "Сбор", "Триггеры", "Служебное", "Конструкции" })
            {
                string c = cat;
                var b = new GdButton
                {
                    Text = cat, Width = 112, Height = 34, FontSize = 8f,
                    Color1 = cat == _category ? Color.FromArgb(120, 220, 255) : Color.FromArgb(70, 76, 125),
                    Color2 = cat == _category ? Color.FromArgb(20, 110, 180) : Color.FromArgb(34, 38, 70),
                    Margin = new Padding(0, 0, 6, 0)
                };
                b.Click += (s, e) => { _category = c; SetMode(Mode.Build); };
                tabs.Controls.Add(b);
            }

            ShowCategory(_category);
        }

        void ShowCategory(string cat)
        {
            if (_palette == null) return;
            _palette.Controls.Clear();

            void AddItem(LevelObject model, string caption)
            {
                var b = new ObjButton
                {
                    Model = model, Caption = caption, Size = new Size(64, 68),
                    Margin = new Padding(4), Selected = SameBrush(model)
                };
                b.Click += (s, e) =>
                {
                    _brush = model.Clone();
                    foreach (var c in _palette.Controls) if (c is ObjButton ob) ob.Selected = ob == b;
                    _palette.Invalidate(true);
                    UpdateStatus();
                };
                _palette.Controls.Add(b);
            }

            switch (cat)
            {
                case "Порталы":
                    foreach (PortalKind k in Enum.GetValues<PortalKind>())
                    {
                        if (k == PortalKind.None) continue;
                        AddItem(new LevelObject { TypeId = 60, Portal = k }, PortalName(k));
                    }
                    break;

                case "Шары и пады":
                    foreach (OrbKind k in Enum.GetValues<OrbKind>())
                        AddItem(new LevelObject { TypeId = 40, Orb = k }, OrbName(k));
                    foreach (PadKind k in Enum.GetValues<PadKind>())
                        AddItem(new LevelObject { TypeId = 50, Pad = k }, "пад " + PadName(k));
                    break;

                case "Триггеры":
                    foreach (TriggerType t in Enum.GetValues<TriggerType>())
                        AddItem(new LevelObject { TypeId = 100, Trigger = new TriggerData { Type = t } },
                            ObjButton.ShortTrigger(t));
                    break;

                case "Картинки":
                    {
                        var addBtn = new GdButton
                        {
                            Text = "КАРТИНКА С САЙТА", Icon = GdIcon.Download, Width = 230, Height = 46,
                            FontSize = 8.5f, Margin = new Padding(4),
                            Color1 = Color.FromArgb(255, 150, 215), Color2 = Color.FromArgb(160, 40, 120)
                        };
                        addBtn.Click += async (s2, e2) => await PickImage();
                        _palette.Controls.Add(addBtn);

                        foreach (var img in _images)
                        {
                            var model = new LevelObject { TypeId = 98, ImageId = img.Id };
                            AddItem(model, "#" + img.Id);
                        }
                        if (_images.Count == 0)
                            _palette.Controls.Add(new Label
                            {
                                Text = "Залей картинку на сайте в разделе «Картинки»,\nпотом нажми «КАРТИНКА С САЙТА».",
                                Width = 420, Height = 44, ForeColor = Theme.Dim, BackColor = Color.Transparent
                            });
                        break;
                    }

                case "Конструкции":
                    {
                        var pieces = BuildLibrary.All();
                        if (pieces.Count == 0)
                        {
                            _palette.Controls.Add(new Label
                            {
                                Text = "Пусто. Выдели кусок уровня в EDIT свайпом\nи нажми «СОХРАНИТЬ КАК КОНСТРУКЦИЮ».",
                                Width = 420, Height = 44, ForeColor = Theme.Dim, BackColor = Color.Transparent
                            });
                            break;
                        }
                        foreach (var piece in pieces)
                        {
                            var pc = piece;
                            var b = new ObjButton
                            {
                                Piece = pc, Caption = pc.Name, Size = new Size(84, 84),
                                Margin = new Padding(4), Selected = _piece != null && _piece.Name == pc.Name
                            };
                            b.Click += (s2, e2) =>
                            {
                                _piece = pc;
                                foreach (var c in _palette.Controls) if (c is ObjButton ob) ob.Selected = ob == b;
                                _palette.Invalidate(true);
                                Status("Конструкция «" + pc.Name + "»: клик по полю — поставить целиком");
                            };
                            b.MouseUp += (s2, e2) =>
                            {
                                if (e2.Button != MouseButtons.Right) return;
                                if (!GdDialog.Confirm(this, "Конструкции", $"Удалить «{pc.Name}»?")) return;
                                BuildLibrary.Delete(pc.Name);
                                if (_piece != null && _piece.Name == pc.Name) _piece = null;
                                ShowCategory("Конструкции");
                            };
                            _palette.Controls.Add(b);
                        }
                        break;
                    }

                default:
                    foreach (var d in ObjectCatalog.All.Values)
                        if (d.Category == cat && d.Kind is not (ObjKind.Portal or ObjKind.Orb or ObjKind.Pad or ObjKind.Trigger))
                            AddItem(new LevelObject { TypeId = d.Id }, d.Name);
                    break;
            }
        }

        bool SameBrush(LevelObject m) =>
            m.TypeId == _brush.TypeId && m.Portal == _brush.Portal && m.Orb == _brush.Orb && m.Pad == _brush.Pad
            && (m.Trigger?.Type ?? TriggerType.Move) == (_brush.Trigger?.Type ?? TriggerType.Move);

        static string PortalName(PortalKind k) => k switch
        {
            PortalKind.ModeCube => "куб",
            PortalKind.ModeShip => "корабль",
            PortalKind.ModeBall => "шар",
            PortalKind.ModeUfo => "НЛО",
            PortalKind.ModeWave => "волна",
            PortalKind.ModeRobot => "робот",
            PortalKind.ModeSpider => "паук",
            PortalKind.ModeWorm => "червяк",
            PortalKind.GravityUp => "грав ↑",
            PortalKind.GravityDown => "грав ↓",
            PortalKind.GravityToggle => "грав ⇅",
            PortalKind.Mini => "мини",
            PortalKind.Big => "обычный",
            PortalKind.Dual => "дуал",
            PortalKind.Single => "один",
            PortalKind.Mirror => "зеркало",
            PortalKind.Unmirror => "норм",
            PortalKind.SpeedSlow => "0.5x",
            PortalKind.SpeedNormal => "1x",
            PortalKind.SpeedFast => "2x",
            PortalKind.SpeedFaster => "3x",
            PortalKind.SpeedFastest => "4x",
            PortalKind.SpeedHyper => "5x",
            PortalKind.SpeedUltra => "6x",
            PortalKind.Teleport => "телепорт",
            _ => k.ToString()
        };

        static string OrbName(OrbKind k) => k switch
        {
            OrbKind.Yellow => "жёлтый",
            OrbKind.Pink => "розовый",
            OrbKind.Blue => "синий",
            OrbKind.Green => "зелёный",
            OrbKind.Red => "красный",
            OrbKind.Black => "чёрный",
            OrbKind.Dash => "рывок",
            OrbKind.GravityDash => "гр-рывок",
            OrbKind.Spider => "паук",
            _ => "перекл."
        };

        static string PadName(PadKind k) => k switch
        {
            PadKind.Yellow => "жёлт",
            PadKind.Pink => "роз",
            PadKind.Blue => "син",
            PadKind.Red => "красн",
            _ => "паук"
        };

        // ---------------------------------------------------- панель правки (EDIT)

        void BuildEditPanel()
        {
            var p = _content;

            void B(string text, GdIcon ic, int x, int y, int w, int h, Color c1, Color c2, Action act)
            {
                var b = new GdButton
                {
                    Text = text, Icon = ic, Left = x, Top = y, Width = w, Height = h,
                    Color1 = c1, Color2 = c2, FontSize = 9f
                };
                b.Click += (s, e) => { act(); _canvas.Invalidate(); UpdateStatus(); };
                p.Controls.Add(b);
            }

            var gray1 = Color.FromArgb(150, 158, 200);
            var gray2 = Color.FromArgb(60, 66, 110);

            // стрелки
            B("", GdIcon.Back, 70, 70, 56, 56, gray1, gray2, () => MoveSel(-_step, 0));
            B("", GdIcon.Play, 198, 70, 56, 56, gray1, gray2, () => MoveSel(_step, 0));
            B("", GdIcon.Upload, 134, 12, 56, 56, gray1, gray2, () => MoveSel(0, _step));
            B("", GdIcon.Download, 134, 128, 56, 56, gray1, gray2, () => MoveSel(0, -_step));

            // шаг
            int sx = 272;
            foreach (var pair in new[] { ("0.5", 15.0), ("1", 30.0), ("5", 150.0) })
            {
                double v = pair.Item2;
                bool on = Math.Abs(_step - v) < 0.01;
                B(pair.Item1, GdIcon.None, sx, 12, 56, 46,
                    on ? Color.FromArgb(120, 220, 255) : gray1,
                    on ? Color.FromArgb(20, 110, 180) : gray2,
                    () => { _step = v; SetMode(Mode.Edit); });
                sx += 62;
            }
            p.Controls.Add(new Label
            {
                Text = "шаг, клеток", Left = 272, Top = 62, Width = 180, Height = 18,
                ForeColor = Theme.Dim, BackColor = Color.Transparent
            });

            // поворот и отражение
            B("↺", GdIcon.None, 272, 86, 56, 56, gray1, gray2, () => RotateSel(-90));
            B("↻", GdIcon.None, 334, 86, 56, 56, gray1, gray2, () => RotateSel(90));
            B("⇄", GdIcon.None, 396, 86, 56, 56, gray1, gray2, () => FlipSel(true));
            B("⇅", GdIcon.None, 458, 86, 56, 56, gray1, gray2, () => FlipSel(false));

            // операции
            B("КОПИЯ", GdIcon.Copy, 540, 12, 118, 50, Color.FromArgb(200, 170, 255), Color.FromArgb(100, 60, 180), CopySel);
            B("ВСТАВИТЬ", GdIcon.Plus, 540, 68, 118, 50, Color.FromArgb(160, 200, 255), Color.FromArgb(50, 90, 180), PasteSel);
            B("УДАЛИТЬ", GdIcon.Trash, 540, 124, 118, 50, Color.FromArgb(240, 120, 120), Color.FromArgb(150, 40, 50), DeleteSel);

            B("ПРАВИТЬ ОБЪЕКТ", GdIcon.Settings, 668, 12, 190, 50,
                Color.FromArgb(255, 200, 90), Color.FromArgb(170, 110, 10), EditObject);
            B("ТРИГГЕР", GdIcon.Admin, 668, 68, 190, 50,
                Color.FromArgb(120, 190, 255), Color.FromArgb(30, 90, 190), EditTrigger);
            B("СНЯТЬ ВЫДЕЛЕНИЕ", GdIcon.Cross, 668, 124, 190, 50, gray1, gray2, () => _sel.Clear());

            B("СОХРАНИТЬ КАК КОНСТРУКЦИЮ", GdIcon.Save, 868, 124, 260, 50,
                Color.FromArgb(120, 255, 180), Color.FromArgb(20, 150, 100), SaveSelectionAsPiece);

            var swipeBtn = new GdButton
            {
                Text = _swipe ? "СВАЙП: ВКЛ" : "СВАЙП: ВЫКЛ", Icon = GdIcon.Edit,
                Left = 1056, Top = 12, Width = 180, Height = 50, FontSize = 8.5f,
                Color1 = _swipe ? Color.FromArgb(120, 255, 170) : Color.FromArgb(150, 150, 185),
                Color2 = _swipe ? Color.FromArgb(20, 150, 100) : Color.FromArgb(60, 62, 100)
            };
            swipeBtn.Click += (s2, e2) =>
            {
                _swipe = !_swipe;
                swipeBtn.Text = _swipe ? "СВАЙП: ВКЛ" : "СВАЙП: ВЫКЛ";
                swipeBtn.Color1 = _swipe ? Color.FromArgb(120, 255, 170) : Color.FromArgb(150, 150, 185);
                swipeBtn.Color2 = _swipe ? Color.FromArgb(20, 150, 100) : Color.FromArgb(60, 62, 100);
                swipeBtn.Invalidate();
                Status(_swipe ? "Свайп: веди мышью — выделяется всё под курсором"
                              : "Свайп выключен: ЛКМ по пустому — рамка выделения");
            };
            p.Controls.Add(swipeBtn);

            B("ВЫДЕЛИТЬ ВСЁ", GdIcon.Check, 868, 12, 180, 50, gray1, gray2, () =>
            {
                _sel.Clear();
                for (int i = 0; i < _lv.Objects.Count; i++) _sel.Add(i);
            });
            B("ПО СЕТКЕ", GdIcon.Check, 868, 68, 180, 50, gray1, gray2, () =>
            {
                PushUndo();
                foreach (var i in _sel)
                {
                    _lv.Objects[i].X = Math.Round(_lv.Objects[i].X / 30) * 30;
                    _lv.Objects[i].Y = Math.Round(_lv.Objects[i].Y / 30) * 30;
                }
                RebuildPreview();
            });

            p.Controls.Add(new Label
            {
                Text = "ЛКМ — выделить, ЛКМ по выделенному — тащить, свайп по пустому — выделение росчерком (Shift — рамка),\n" +
                       "СКМ или Alt — двигать вид, стрелки — двигать выделенное, «СОХРАНИТЬ КАК КОНСТРУКЦИЮ» — свой блок в палитру",
                Left = 70, Top = 192, Width = 980, Height = 36,
                ForeColor = Theme.Dim, BackColor = Color.Transparent
            });
        }

        void BuildDeletePanel()
        {
            var p = _content;
            void B(string text, GdIcon ic, int x, int y, int w, Action act)
            {
                var b = new GdButton
                {
                    Text = text, Icon = ic, Left = x, Top = y, Width = w, Height = 54,
                    Color1 = Color.FromArgb(240, 130, 130), Color2 = Color.FromArgb(150, 45, 55), FontSize = 9.5f
                };
                b.Click += (s, e) => { act(); _canvas.Invalidate(); UpdateStatus(); };
                p.Controls.Add(b);
            }

            B("УДАЛИТЬ ВЫДЕЛЕННОЕ", GdIcon.Trash, 40, 20, 250, DeleteSel);
            B("УДАЛИТЬ ТАКИЕ ЖЕ", GdIcon.Trash, 40, 86, 250, () =>
            {
                if (_sel.Count == 0) return;
                int type = _lv.Objects[_sel[0]].TypeId;
                PushUndo();
                _lv.Objects.RemoveAll(o => o.TypeId == type);
                _sel.Clear();
                RebuildPreview();
            });
            B("ОЧИСТИТЬ УРОВЕНЬ", GdIcon.Cross, 40, 152, 250, () =>
            {
                if (!GdDialog.Confirm(this, "Очистка", "Удалить все объекты уровня?")) return;
                PushUndo();
                _lv.Objects.Clear();
                _sel.Clear();
                RebuildPreview();
            });

            p.Controls.Add(new Label
            {
                Text = "В режиме DELETE клик по объекту удаляет его.\n" +
                       "Стирания свайпом нет — объекты удаляются по одному.",
                Left = 310, Top = 28, Width = 560, Height = 44,
                ForeColor = Theme.Dim, BackColor = Color.Transparent
            });
        }

        // ---------------------------------------------------- действия правки

        void MoveSel(double dx, double dy)
        {
            if (_sel.Count == 0) return;
            PushUndo();
            foreach (var i in _sel) { _lv.Objects[i].X += dx; _lv.Objects[i].Y += dy; }
            RebuildPreview();
        }

        void RotateSel(double deg)
        {
            if (_sel.Count == 0) return;
            PushUndo();
            foreach (var i in _sel) _lv.Objects[i].Rotation = (_lv.Objects[i].Rotation + deg) % 360;
            RebuildPreview();
        }

        void FlipSel(bool horizontal)
        {
            if (_sel.Count == 0) return;
            PushUndo();
            foreach (var i in _sel)
            {
                var o = _lv.Objects[i];
                if (horizontal) { o.FlipX = !o.FlipX; o.ScaleX = -o.ScaleX; }
                else { o.FlipY = !o.FlipY; o.ScaleY = -o.ScaleY; }
            }
            RebuildPreview();
        }

        void CopySel()
        {
            _clipboard = _sel.Where(i => i < _lv.Objects.Count).Select(i => _lv.Objects[i].Clone()).ToList();
            Status("Скопировано: " + _clipboard.Count);
        }

        void PasteSel()
        {
            if (_clipboard.Count == 0) return;
            PushUndo();
            _sel.Clear();
            foreach (var o in _clipboard)
            {
                var c = o.Clone();
                c.Id = 0; c.X += 30;
                _lv.Add(c);
                _sel.Add(_lv.Objects.Count - 1);
            }
            RebuildPreview();
        }

        void DeleteSel()
        {
            if (_sel.Count == 0) return;
            PushUndo();
            foreach (var i in _sel.OrderByDescending(v => v))
                if (i >= 0 && i < _lv.Objects.Count) _lv.Objects.RemoveAt(i);
            _sel.Clear();
            RebuildPreview();
        }

        void EditObject()
        {
            if (_sel.Count == 0) { Status("Сначала выдели объект"); return; }
            var first = _lv.Objects[_sel[0]];
            using var f = new ObjectEditForm(first);
            if (f.ShowDialog(this) == DialogResult.OK)
            {
                PushUndo();
                foreach (var i in _sel) f.ApplyTo(_lv.Objects[i]);
                RebuildPreview();
            }
        }

        void EditTrigger()
        {
            if (_sel.Count == 0) { Status("Сначала выдели триггер"); return; }
            var first = _lv.Objects[_sel[0]];
            var data = first.Trigger ?? new TriggerData();
            using var f = new TriggerEditForm(data);
            if (f.ShowDialog(this) == DialogResult.OK)
            {
                PushUndo();
                foreach (var i in _sel)
                {
                    var o = _lv.Objects[i];
                    if (ObjectCatalog.Get(o.TypeId).Kind == ObjKind.Trigger) o.Trigger = f.Data.Clone();
                }
                RebuildPreview();
            }
        }

        // ---------------------------------------------------- канва

        void CanvasPaint(object s, PaintEventArgs e)
        {
            var g = e.Graphics;
            _v.Client = _canvas.ClientSize;
            _v.CamX = _camX; _v.CamY = _camY; _v.Zoom = _zoom;

            Renderer.Draw(g, _preview, _v, true);
            DrawGrid(g);

            foreach (var i in _sel)
            {
                if (i < 0 || i >= _lv.Objects.Count) continue;
                var b = ObjectCatalog.Bounds(_lv.Objects[i]);
                var r = _v.WorldRect(b.X, b.Y, b.Width, b.Height);
                using var pen = new Pen(Gd.Gold, 2.5f);
                g.DrawRectangle(pen, r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2);
            }

            if (_boxSelect)
            {
                var a = _v.W2S(_selBox.Left, _selBox.Top);
                var b2 = _v.W2S(_selBox.Right, _selBox.Bottom);
                var r = RectangleF.FromLTRB(Math.Min(a.X, b2.X), Math.Min(a.Y, b2.Y), Math.Max(a.X, b2.X), Math.Max(a.Y, b2.Y));
                using var pen = new Pen(Gd.Neon, 1.5f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
            }

            var sr = _v.WorldRect(0, 0, 30, 30);
            using (var pen = new Pen(Gd.Lime, 2f)) g.DrawRectangle(pen, sr.X, sr.Y, sr.Width, sr.Height);
            using (var f = Gd.Bold(8f))
                Gd.Outlined(g, "СТАРТ", f, new RectangleF(sr.X - 20, sr.Y - 20, sr.Width + 40, 18), Gd.Lime, thick: 2f);

            if (_mode == Mode.Build)
            {
                var p = _canvas.PointToClient(Cursor.Position);
                if (_canvas.ClientRectangle.Contains(p))
                {
                    var w = WorldOf(p);
                    var d = ObjectCatalog.Get(_brush.TypeId);
                    var r = _v.WorldRect(w.X, w.Y, d.W, d.H);
                    using var pen = new Pen(Color.FromArgb(160, Gd.Neon), 2f);
                    g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                }
            }
        }

        void DrawGrid(Graphics g)
        {
            float sc = _v.Scale;
            if (sc * _grid < 5) return;
            var vis = _v.VisibleWorld();
            using var pen = new Pen(Color.FromArgb(38, 255, 255, 255), 1);
            using var pen2 = new Pen(Color.FromArgb(80, 255, 255, 255), 1);
            for (double x = Math.Floor(vis.Left / _grid) * _grid; x < vis.Right; x += _grid)
            {
                var p1 = _v.W2S(x, vis.Top); var p2 = _v.W2S(x, vis.Bottom);
                g.DrawLine(Math.Abs(x % (_grid * 8)) < 0.01 ? pen2 : pen, p1.X, p1.Y, p2.X, p2.Y);
            }
            for (double y = Math.Floor(vis.Top / _grid) * _grid; y < vis.Bottom; y += _grid)
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
            var w = _v.S2W(e.X, e.Y);

            if (e.Button == MouseButtons.Middle || (ModifierKeys & Keys.Alt) != 0)
            {
                _panning = true;
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
                }
                return;
            }

            if (e.Button != MouseButtons.Left) return;

            switch (_mode)
            {
                case Mode.Build:
                    PlaceObject(WorldOf(e.Location));   // один клик — один объект
                    break;

                case Mode.Edit:
                    {
                        int hit = HitTest(w);
                        if (hit >= 0)
                        {
                            if ((ModifierKeys & Keys.Shift) != 0)
                            {
                                if (_sel.Contains(hit)) _sel.Remove(hit); else _sel.Add(hit);
                            }
                            else if (!_sel.Contains(hit))
                            {
                                _sel.Clear();
                                _sel.Add(hit);
                            }
                            _movingSel = true;
                            _dragStart = w;
                        }
                        else if (_swipe && (ModifierKeys & Keys.Shift) == 0)
                        {
                            // свайп: ведёшь — выделяется всё, по чему провёл
                            _swiping = true;
                            _sel.Clear();
                            _dragStart = w;
                        }
                        else
                        {
                            _boxSelect = true;
                            _dragStart = w;
                            _selBox = new RectangleF(w.X, w.Y, 0, 0);
                            if ((ModifierKeys & Keys.Shift) == 0) _sel.Clear();
                        }
                        break;
                    }

                case Mode.Delete:
                    {
                        int hit = HitTest(w);
                        if (hit >= 0)
                        {
                            PushUndo();
                            _lv.Objects.RemoveAt(hit);
                            _sel.Clear();
                            RebuildPreview();
                        }
                        else _panning = true;
                        break;
                    }
            }
            UpdateStatus();
            _canvas.Invalidate();
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

            if (_swiping)
            {
                int under = HitTest(w);
                if (under >= 0 && !_sel.Contains(under)) { _sel.Add(under); UpdateStatus(); }
                _canvas.Invalidate();
                return;
            }

            if (_boxSelect)
            {
                _selBox = RectangleF.FromLTRB(
                    Math.Min(_dragStart.X, w.X), Math.Min(_dragStart.Y, w.Y),
                    Math.Max(_dragStart.X, w.X), Math.Max(_dragStart.Y, w.Y));
                _canvas.Invalidate();
                return;
            }

            if (_movingSel && e.Button == MouseButtons.Left && _sel.Count > 0)
            {
                double dx = w.X - _dragStart.X, dy = w.Y - _dragStart.Y;
                if (_snap) { dx = Math.Round(dx / _grid) * _grid; dy = Math.Round(dy / _grid) * _grid; }
                if (Math.Abs(dx) >= (_snap ? _grid : 1) || Math.Abs(dy) >= (_snap ? _grid : 1))
                {
                    foreach (var i in _sel) { _lv.Objects[i].X += dx; _lv.Objects[i].Y += dy; }
                    _dragStart = new PointF((float)(_dragStart.X + dx), (float)(_dragStart.Y + dy));
                    RebuildPreview();
                }
                return;
            }

            // в режиме BUILD перетаскивание двигает вид, а не ставит объекты
            if (_mode == Mode.Build && e.Button == MouseButtons.Left)
            {
                float sc = _v.Scale;
                _camX -= (e.X - _lastMouse.X) / sc;
                _camY += (e.Y - _lastMouse.Y) / sc;
                _lastMouse = e.Location;
            }
            _canvas.Invalidate();
        }

        void CanvasUp(object s, MouseEventArgs e)
        {
            if (_swiping)
            {
                _swiping = false;
                if (_sel.Count > 0) Status($"Выделено свайпом: {_sel.Count}");
            }

            if (_boxSelect)
            {
                _boxSelect = false;
                for (int i = 0; i < _lv.Objects.Count; i++)
                {
                    var b = ObjectCatalog.Bounds(_lv.Objects[i]);
                    if (_selBox.IntersectsWith(new RectangleF(b.X, b.Y, Math.Max(1, b.Width), Math.Max(1, b.Height)))
                        && !_sel.Contains(i)) _sel.Add(i);
                }
            }
            if (_movingSel) PushUndo();
            _panning = false;
            _movingSel = false;
            UpdateStatus();
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

        /// <summary>Список картинок с сайта и выбор нужной по ID.</summary>
        async Task PickImage()
        {
            Status("Беру список картинок с сайта…");
            var r = await Net.ApiClient.ImagesAsync();
            if (r.Ok && r.Data != null)
            {
                _images.Clear();
                _images.AddRange(r.Data);
            }
            if (_images.Count == 0)
            {
                string idTxt = GdDialog.Prompt(this, "Картинка",
                    "Список не загрузился. Введи ID картинки с сайта:", "1");
                if (!int.TryParse((idTxt ?? "").Trim(), out int manual) || manual <= 0) return;
                _images.Add(new Net.ImageInfo { Id = manual, Name = "Картинка " + manual });
            }

            var names = _images.Select(i => $"#{i.Id}  {i.Name}  ({i.Author})").ToArray();
            int pick = GdDialog.Choose(this, "Картинки с сайта", names);
            if (pick < 0 || pick >= _images.Count) { ShowCategory("Картинки"); return; }

            var img = _images[pick];
            await Net.ImageLibrary.DownloadAsync(img.Id);
            _brush = new LevelObject { TypeId = 98, ImageId = img.Id };
            _category = "Картинки";
            SetMode(Mode.Build);
            Status($"Картинка #{img.Id} выбрана — ставь кликом по полю");
        }

        void PlaceObject(PointF p)
        {
            // выбрана своя конструкция — ставим её целиком
            if (_category == "Конструкции" && _piece != null)
            {
                PushUndo();
                var stamp = BuildLibrary.Stamp(_piece, p.X, p.Y);
                _sel.Clear();
                foreach (var o in stamp) { _lv.Add(o); _sel.Add(_lv.Objects.Count - 1); }
                Sfx.Play("click");
                RebuildPreview();
                Status($"Поставлена конструкция «{_piece.Name}» — {stamp.Count} объектов");
                return;
            }

            var def = ObjectCatalog.Get(_brush.TypeId);
            foreach (var o in _lv.Objects)
                if (Math.Abs(o.X - p.X) < 1 && Math.Abs(o.Y - p.Y) < 1 && o.TypeId == _brush.TypeId) return;

            PushUndo();
            var n = _brush.Clone();
            n.Id = 0;
            n.X = p.X; n.Y = p.Y;
            if (def.Kind == ObjKind.Text) n.Text = "Текст";
            _lv.Add(n);
            Sfx.Play("click");
            RebuildPreview();
        }

        // ---------------------------------------------------- прочее

        void Keys_(object s, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Z) { Undo(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.Y) { Redo(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.S) { Save(); Status("Сохранено"); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.C) { CopySel(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.V) { PasteSel(); e.Handled = true; }
            else if (e.KeyCode == Keys.Delete) { DeleteSel(); e.Handled = true; }
            else if (e.KeyCode == Keys.F5) { Playtest(false); e.Handled = true; }
            else if (e.KeyCode == Keys.D1) SetMode(Mode.Build);
            else if (e.KeyCode == Keys.D2) SetMode(Mode.Edit);
            else if (e.KeyCode == Keys.D3) SetMode(Mode.Delete);
            else if (e.KeyCode == Keys.Left) { MoveSel(-_step, 0); e.Handled = true; }
            else if (e.KeyCode == Keys.Right) { MoveSel(_step, 0); e.Handled = true; }
            else if (e.KeyCode == Keys.Up) { MoveSel(0, _step); e.Handled = true; }
            else if (e.KeyCode == Keys.Down) { MoveSel(0, -_step); e.Handled = true; }
        }

        /// <summary>Сохранить выделенное как свою конструкцию — потом ставится одним кликом.</summary>
        void SaveSelectionAsPiece()
        {
            if (_sel.Count == 0) { Status("Сначала выдели объекты — свайпом или рамкой"); return; }
            string name = GdDialog.Prompt(this, "Своя конструкция",
                $"Название для {_sel.Count} объектов:", "Конструкция " + (BuildLibrary.All().Count + 1));
            if (string.IsNullOrWhiteSpace(name)) return;

            var objs = _sel.Where(i => i >= 0 && i < _lv.Objects.Count).Select(i => _lv.Objects[i]);
            var piece = BuildLibrary.Save(name.Trim(), objs);
            if (piece == null) { Status("Не вышло сохранить"); return; }
            _piece = piece;
            Status($"Конструкция «{piece.Name}» сохранена — ищи её в BUILD, вкладка «Конструкции»");
        }

        void RebuildPreview()
        {
            _preview.Init(_lv);
            _canvas.Invalidate();
            UpdateStatus();
        }

        void PushUndo()
        {
            _undo.Add(_lv.ToJson());
            if (_undo.Count > 80) _undo.RemoveAt(0);
            _redo.Clear();
        }

        void Undo()
        {
            if (_undo.Count < 2) return;
            _redo.Add(_lv.ToJson());
            _undo.RemoveAt(_undo.Count - 1);
            _lv = LevelData.FromJson(_undo[^1]);
            _sel.Clear();
            RebuildPreview();
        }

        void Redo()
        {
            if (_redo.Count == 0) return;
            _undo.Add(_lv.ToJson());
            _lv = LevelData.FromJson(_redo[^1]);
            _redo.RemoveAt(_redo.Count - 1);
            _sel.Clear();
            RebuildPreview();
        }

        void Save() => LocalStore.SaveMyLevel(_lv);

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
                UpdateStatus();
            }
        }

        async Task Upload()
        {
            Save();
            if (!AppConfig.LoggedIn)
            {
                GdDialog.Info(this, "Загрузка", "Сначала войди в аккаунт (меню → ПРОФИЛЬ).");
                return;
            }
            Status("Загружаю на сервер…");
            var r = await ApiClient.UploadLevelAsync(_lv, _lv.Settings.ServerId, _lv.Settings.Password);
            if (r.Ok && r.Data != null)
            {
                _lv.Settings.ServerId = r.Data.Id;
                _lv.Settings.Version++;
                Save();
                GdDialog.Info(this, "Готово", $"Уровень на сервере под ID {r.Data.Id}.");
            }
            else GdDialog.Info(this, "Загрузка", r.Error + "\n\nЕсли нет сети — уровень уедет сам, когда связь появится.");
            UpdateStatus();
        }

        void Status(string s) { _status.Text = s; }

        void UpdateStatus()
        {
            string brush = ObjectCatalog.Get(_brush.TypeId).Name;
            if (_brush.TypeId == 60) brush = "портал " + PortalName(_brush.Portal);
            if (_brush.TypeId == 40) brush = "орб " + OrbName(_brush.Orb);
            if (_brush.TypeId == 50) brush = "пад " + PadName(_brush.Pad);
            if (_brush.TypeId == 100 && _brush.Trigger != null) brush = "триггер " + _brush.Trigger.Type;

            _status.Text = $"режим: {_mode}   объект: {brush}   выделено: {_sel.Count}   " +
                           $"объектов: {_lv.Objects.Count}   длина: {_lv.RightEdge() / 30:0} клеток   " +
                           $"зум: {_zoom:0.00}   шаг: {_step / 30:0.##}   " +
                           $"музыка: {(_lv.Settings.SongId == 0 ? "нет" : _lv.Settings.SongId < 0 ? "встроенная" : "#" + _lv.Settings.SongId)}";
        }
    }
}
