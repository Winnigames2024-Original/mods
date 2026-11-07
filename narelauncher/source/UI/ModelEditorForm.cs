using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Редактор моделей Minecraft: кубы, их размеры, повороты и текстуры по
    /// граням, с просмотром в объёме. Сохраняет обычный .json из ресурспака,
    /// поэтому модель можно открыть и дальше в чём угодно.
    ///
    /// Чего здесь нет: анимаций и формата .bbmodel. Это редактор моделей для
    /// игры, а не весь Blockbench.
    /// </summary>
    public class ModelEditorForm : Form
    {
        private BlockModel _model = BlockModel.Empty();
        private string _file = "";
        private bool _loading;

        // ---- левая колонка
        private readonly VirtualList _cubes = new VirtualList { ItemHeight = 44, EmptyText = "Кубов нет" };
        private readonly VirtualList _slots = new VirtualList { ItemHeight = 40, EmptyText = "Текстур нет" };

        private readonly NareButton _add = new NareButton { Text = "Добавить куб", Style = BtnStyle.Accent, Size = new Size(140, 34) };
        private readonly NareButton _copy = new NareButton { Text = "Копия", Style = BtnStyle.Ghost, Size = new Size(92, 34) };
        private readonly NareButton _drop = new NareButton { Text = "Удалить", Style = BtnStyle.Danger, Size = new Size(140, 34) };
        private readonly NareButton _up = new NareButton { Text = "Выше", Style = BtnStyle.Ghost, Size = new Size(92, 34) };

        private readonly NareButton _addTexture = new NareButton { Text = "Добавить png", Style = BtnStyle.Ghost, Size = new Size(140, 34) };
        private readonly NareButton _dropTexture = new NareButton { Text = "Убрать", Style = BtnStyle.Ghost, Size = new Size(92, 34) };
        private readonly NareTextBox _texturePath = new NareTextBox { Placeholder = "minecraft:block/моя_текстура" };

        // ---- середина
        private readonly ModelView3D _view = new ModelView3D();

        // ---- правая колонка
        private readonly NareTextBox _name = new NareTextBox { Placeholder = "Название куба" };
        private readonly NareTextBox[] _from = { Box(), Box(), Box() };
        private readonly NareTextBox[] _to = { Box(), Box(), Box() };
        private readonly NareTextBox[] _origin = { Box(), Box(), Box() };

        private readonly NareButton _axis = new NareButton { Text = "ось: нет", Style = BtnStyle.Ghost, Size = new Size(118, 32) };
        private readonly NareButton _angle = new NareButton { Text = "угол: 0°", Style = BtnStyle.Ghost, Size = new Size(118, 32) };

        private readonly VirtualList _faces = new VirtualList { ItemHeight = 38, EmptyText = "" };
        private readonly NareButton _faceTexture = new NareButton { Text = "Текстура ▸", Style = BtnStyle.Ghost, Size = new Size(118, 32) };
        private readonly NareButton _faceTurn = new NareButton { Text = "Повернуть ▸", Style = BtnStyle.Ghost, Size = new Size(118, 32) };
        private readonly NareButton _faceHide = new NareButton { Text = "Скрыть грань", Style = BtnStyle.Ghost, Size = new Size(118, 32) };
        private readonly NareButton _faceAuto = new NareButton { Text = "UV сам", Style = BtnStyle.Ghost, Size = new Size(118, 32) };
        private readonly NareTextBox[] _uv = { Box(), Box(), Box(), Box() };

        // ---- верх и низ
        private readonly NareButton _new = new NareButton { Text = "Новая", Style = BtnStyle.Ghost, Size = new Size(104, 34) };
        private readonly NareButton _open = new NareButton { Text = "Открыть .json", Style = BtnStyle.Ghost, Size = new Size(150, 34) };
        private readonly NareButton _save = new NareButton { Text = "Сохранить .json", Style = BtnStyle.Ghost, Size = new Size(166, 34) };
        private readonly NareButton _pack = new NareButton { Text = "В ресурспак…", Style = BtnStyle.Accent, Size = new Size(160, 34) };
        private readonly NareButton _close = new NareButton { Text = "Закрыть", Style = BtnStyle.Ghost, Size = new Size(110, 34) };
        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);

        private static NareTextBox Box() { return new NareTextBox { Placeholder = "0" }; }

        private ModelCube Cube { get { return _cubes.SelectedItem as ModelCube; } }
        private string Side { get { return _faces.SelectedItem as string; } }

        public ModelEditorForm()
        {
            Text = "Редактор моделей";
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(10f);
            MinimumSize = new Size(1040, 660);
            ClientSize = new Size(1160, 720);

            _view.Model = _model;

            Build();
            FillCubes();
            FillSlots();
            FillFaces();
            Status("Новая модель. Куб 8x8x8 в середине блока.");
        }

        // ------------------------------------------------------------ сборка

        private void Build()
        {
            Controls.Add(_new); Controls.Add(_open); Controls.Add(_save);
            Controls.Add(_pack); Controls.Add(_close);
            Controls.Add(_status);

            _cubes.Renderer = DrawCube;
            _cubes.SelectionChanged += delegate
            {
                _view.Selected = _cubes.SelectedIndex;
                Show(Cube);
                _view.Invalidate();
            };
            Controls.Add(_cubes);

            _slots.Renderer = DrawSlot;
            _slots.SelectionChanged += delegate
            {
                var slot = _slots.SelectedItem as ModelTexture;
                _loading = true;
                _texturePath.Text = slot == null ? "" : slot.Path;
                _loading = false;
            };
            Controls.Add(_slots);

            Controls.Add(_add); Controls.Add(_copy); Controls.Add(_drop); Controls.Add(_up);
            Controls.Add(_addTexture); Controls.Add(_dropTexture); Controls.Add(_texturePath);

            _view.Picked += delegate
            {
                if (_view.Selected >= 0 && _view.Selected < _cubes.Items.Count)
                    _cubes.SelectedIndex = _view.Selected;
            };
            Controls.Add(_view);

            Controls.Add(_name);
            foreach (var b in _from) Controls.Add(b);
            foreach (var b in _to) Controls.Add(b);
            foreach (var b in _origin) Controls.Add(b);
            foreach (var b in _uv) Controls.Add(b);

            Controls.Add(_axis); Controls.Add(_angle);

            _faces.Renderer = DrawFace;
            _faces.SelectionChanged += delegate { ShowFace(); };
            Controls.Add(_faces);

            Controls.Add(_faceTexture); Controls.Add(_faceTurn);
            Controls.Add(_faceHide); Controls.Add(_faceAuto);

            Wire();
            Layout1();
        }

        private void Wire()
        {
            _name.Inner.TextChanged += delegate
            {
                if (_loading || Cube == null) return;
                Cube.Name = _name.Text;
                _cubes.Invalidate();
            };

            for (int i = 0; i < 3; i++)
            {
                int axis = i;
                _from[i].Inner.TextChanged += delegate { Number(_from[axis], v => { if (Cube != null) Cube.From[axis] = v; }); };
                _to[i].Inner.TextChanged += delegate { Number(_to[axis], v => { if (Cube != null) Cube.To[axis] = v; }); };
                _origin[i].Inner.TextChanged += delegate { Number(_origin[axis], v => { if (Cube != null) Cube.Origin[axis] = v; }); };
            }

            for (int i = 0; i < 4; i++)
            {
                int which = i;
                _uv[i].Inner.TextChanged += delegate
                {
                    Number(_uv[which], v =>
                    {
                        var cube = Cube;
                        string side = Side;
                        if (cube == null || side == null) return;

                        var face = cube.Face(side);
                        if (face.Uv == null) face.Uv = cube.AutoUv(side);
                        face.Uv[which] = v;
                        _faces.Invalidate();
                    });
                };
            }

            _axis.Click += delegate
            {
                if (Cube == null) return;
                string[] order = { "", "x", "y", "z" };
                int at = Array.IndexOf(order, Cube.Axis ?? "");
                Cube.Axis = order[(Math.Max(0, at) + 1) % order.Length];
                Show(Cube);
                _view.Invalidate();
            };

            _angle.Click += delegate
            {
                if (Cube == null) return;
                float[] order = { 0f, 22.5f, 45f, -22.5f, -45f };
                int at = 0;
                for (int i = 0; i < order.Length; i++)
                    if (Math.Abs(order[i] - Cube.Angle) < 0.01f) { at = i; break; }

                Cube.Angle = order[(at + 1) % order.Length];
                if (Cube.Angle != 0f && string.IsNullOrEmpty(Cube.Axis)) Cube.Axis = "y";

                Show(Cube);
                _view.Invalidate();
            };

            _add.Click += delegate
            {
                var cube = new ModelCube { Name = "Куб " + (_model.Cubes.Count + 1) };
                cube.From = new[] { 6f, 0f, 6f };
                cube.To = new[] { 10f, 4f, 10f };
                foreach (var side in ModelCube.Sides) cube.Face(side).Texture = First();

                _model.Cubes.Add(cube);
                FillCubes();
                _cubes.SelectedIndex = _model.Cubes.Count - 1;
                Status("Куб добавлен. Размеры — справа, в клетках блока от 0 до 16.");
            };

            _copy.Click += delegate
            {
                if (Cube == null) return;
                var copy = Cube.Copy();
                copy.Name = Cube.Name + " (копия)";
                _model.Cubes.Insert(_cubes.SelectedIndex + 1, copy);
                FillCubes();
                _cubes.SelectedIndex = _model.Cubes.IndexOf(copy);
            };

            _drop.Click += delegate
            {
                if (Cube == null) return;
                int at = _cubes.SelectedIndex;
                _model.Cubes.RemoveAt(at);
                FillCubes();
                _cubes.SelectedIndex = Math.Min(at, _model.Cubes.Count - 1);
            };

            _up.Click += delegate
            {
                int at = _cubes.SelectedIndex;
                if (at <= 0) return;

                var cube = _model.Cubes[at];
                _model.Cubes.RemoveAt(at);
                _model.Cubes.Insert(at - 1, cube);
                FillCubes();
                _cubes.SelectedIndex = at - 1;
            };

            _addTexture.Click += delegate { AddTexture(); };

            _dropTexture.Click += delegate
            {
                var slot = _slots.SelectedItem as ModelTexture;
                if (slot == null || _model.Textures.Count <= 1) return;

                _model.Textures.Remove(slot);
                if (slot.Image != null) slot.Image.Dispose();

                FillSlots();
                _view.Invalidate();
            };

            _texturePath.Inner.TextChanged += delegate
            {
                if (_loading) return;
                var slot = _slots.SelectedItem as ModelTexture;
                if (slot == null) return;

                slot.Path = _texturePath.Text.Trim();
                _slots.Invalidate();
            };

            _faceTexture.Click += delegate
            {
                var cube = Cube; string side = Side;
                if (cube == null || side == null || _model.Textures.Count == 0) return;

                var face = cube.Face(side);
                string key = face.Texture.StartsWith("#") ? face.Texture.Substring(1) : face.Texture;

                int at = -1;
                for (int i = 0; i < _model.Textures.Count; i++)
                    if (_model.Textures[i].Key == key) { at = i; break; }

                face.Texture = "#" + _model.Textures[(at + 1) % _model.Textures.Count].Key;
                _faces.Invalidate();
                _view.Invalidate();
            };

            _faceTurn.Click += delegate
            {
                var cube = Cube; string side = Side;
                if (cube == null || side == null) return;

                var face = cube.Face(side);
                face.Rotation = (face.Rotation + 90) % 360;
                ShowFace();
                _view.Invalidate();
            };

            _faceHide.Click += delegate
            {
                var cube = Cube; string side = Side;
                if (cube == null || side == null) return;

                var face = cube.Face(side);
                face.Hidden = !face.Hidden;
                ShowFace();
                _faces.Invalidate();
                _view.Invalidate();
            };

            _faceAuto.Click += delegate
            {
                var cube = Cube; string side = Side;
                if (cube == null || side == null) return;

                cube.Face(side).Uv = null;
                ShowFace();
                _faces.Invalidate();
                _view.Invalidate();
                Status("Развёртка этой грани снова считается сама.");
            };

            _new.Click += delegate
            {
                _model = BlockModel.Empty();
                _file = "";
                _view.Model = _model;
                FillCubes(); FillSlots(); FillFaces();
                Status("Новая модель.");
            };

            _open.Click += delegate { Open(); };
            _save.Click += delegate { Save(); };
            _pack.Click += delegate { ToPack(); };
            _close.Click += delegate { DialogResult = DialogResult.OK; Close(); };
        }

        private string First()
        {
            return _model.Textures.Count > 0 ? "#" + _model.Textures[0].Key : "#0";
        }

        /// <summary>Разбор числа: запятая с цифровой клавиатуры тоже годится.</summary>
        private void Number(NareTextBox box, Action<float> set)
        {
            if (_loading) return;

            string text = (box.Text ?? "").Trim().Replace(',', '.');
            float value;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return;

            set(value);
            _view.Invalidate();
        }

        // ------------------------------------------------------------ списки

        private void FillCubes()
        {
            var items = new List<object>();
            foreach (var cube in _model.Cubes) items.Add(cube);

            int keep = _cubes.SelectedIndex;
            _cubes.SetItems(items);

            if (items.Count > 0)
                _cubes.SelectedIndex = keep >= 0 && keep < items.Count ? keep : 0;

            _view.Selected = _cubes.SelectedIndex;
            Show(Cube);
            _view.Invalidate();
        }

        private void FillSlots()
        {
            var items = new List<object>();
            foreach (var slot in _model.Textures) items.Add(slot);

            _slots.SetItems(items);
            if (items.Count > 0 && _slots.SelectedIndex < 0) _slots.SelectedIndex = 0;

            var one = _slots.SelectedItem as ModelTexture;
            _loading = true;
            _texturePath.Text = one == null ? "" : one.Path;
            _loading = false;
        }

        private void FillFaces()
        {
            var items = new List<object>();
            foreach (var side in ModelCube.Sides) items.Add(side);

            _faces.SetItems(items);
            if (_faces.SelectedIndex < 0) _faces.SelectedIndex = 0;
            ShowFace();
        }

        private void Show(ModelCube cube)
        {
            _loading = true;
            try
            {
                bool has = cube != null;

                _name.Text = has ? cube.Name : "";
                for (int i = 0; i < 3; i++)
                {
                    _from[i].Text = has ? Text1(cube.From[i]) : "";
                    _to[i].Text = has ? Text1(cube.To[i]) : "";
                    _origin[i].Text = has ? Text1(cube.Origin[i]) : "";
                }

                _axis.Text = "ось: " + (!has || string.IsNullOrEmpty(cube.Axis) ? "нет" : cube.Axis.ToUpperInvariant());
                _angle.Text = "угол: " + (has ? Text1(cube.Angle) : "0") + "°";

                _name.Enabled = has;
                foreach (var b in _from) b.Enabled = has;
                foreach (var b in _to) b.Enabled = has;
                foreach (var b in _origin) b.Enabled = has;
                _axis.Enabled = _angle.Enabled = has;
                _copy.Enabled = _drop.Enabled = has;
            }
            finally { _loading = false; }

            ShowFace();
        }

        private void ShowFace()
        {
            var cube = Cube;
            string side = Side;
            bool has = cube != null && side != null;

            _loading = true;
            try
            {
                if (!has)
                {
                    foreach (var b in _uv) { b.Text = ""; b.Enabled = false; }
                    _faceTexture.Enabled = _faceTurn.Enabled = _faceHide.Enabled = _faceAuto.Enabled = false;
                    return;
                }

                var face = cube.Face(side);
                var uv = cube.UvOf(side);

                for (int i = 0; i < 4; i++)
                {
                    _uv[i].Text = Text1(uv[i]);
                    _uv[i].Enabled = !face.Hidden;
                }

                _faceTexture.Enabled = _faceTurn.Enabled = _faceAuto.Enabled = !face.Hidden;
                _faceHide.Enabled = true;
                _faceHide.Text = face.Hidden ? "Показать грань" : "Скрыть грань";
                _faceTurn.Text = "Поворот " + face.Rotation + "°";
            }
            finally { _loading = false; }
        }

        private static string Text1(float value)
        {
            return Math.Abs(value - (float)Math.Round(value)) < 0.001f
                ? ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------ файлы

        private void AddTexture()
        {
            using (var dialog = new OpenFileDialog
            {
                Title = "Текстура для модели",
                Filter = "Картинка PNG|*.png",
                Multiselect = true
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                foreach (var file in dialog.FileNames)
                {
                    string plain = Path.GetFileNameWithoutExtension(file).ToLowerInvariant().Replace(' ', '_');

                    var slot = new ModelTexture
                    {
                        Key = _model.FreeKey(),
                        File = file,
                        Path = "minecraft:block/" + plain
                    };
                    slot.Reload();

                    if (slot.Image == null)
                    {
                        Status("Не открыл картинку " + Path.GetFileName(file) + ".");
                        continue;
                    }

                    _model.Textures.Add(slot);
                }
            }

            FillSlots();
            _view.Invalidate();
            Status("Теперь выберите грань справа и нажмите «Текстура», чтобы её назначить.");
        }

        private void Open()
        {
            using (var dialog = new OpenFileDialog
            {
                Title = "Модель Minecraft",
                Filter = "Модель|*.json"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    var loaded = BlockModel.Load(dialog.FileName);
                    _model = loaded;
                    _file = dialog.FileName;
                    _view.Model = _model;

                    FindTextures(Path.GetDirectoryName(dialog.FileName));

                    FillCubes(); FillSlots(); FillFaces();

                    string note = "Открыто: кубов " + _model.Cubes.Count +
                                  ", текстур " + _model.Textures.Count + ".";

                    if (_model.Dropped.Count > 0)
                        note += " Не сохранятся при записи: " + string.Join(", ", _model.Dropped.ToArray()) + ".";

                    Status(note);
                }
                catch (Exception ex)
                {
                    Oops("Модель не открылась", ex);
                }
            }
        }

        /// <summary>
        /// Ищем png для текстур модели рядом с ней: обычно модель лежит в
        /// assets/.../models, а картинки в assets/.../textures.
        /// </summary>
        private void FindTextures(string modelDir)
        {
            string root = PackRoot(modelDir);

            foreach (var slot in _model.Textures)
            {
                if (string.IsNullOrWhiteSpace(slot.Path) || slot.Path.StartsWith("#")) continue;

                if (root != null)
                {
                    string guess = BlockModel.TexturePath(root, slot.Path);
                    if (guess.Length > 0 && File.Exists(guess))
                    {
                        slot.File = guess;
                        slot.Reload();
                        continue;
                    }
                }

                // иначе хотя бы по имени файла в той же папке
                string name = slot.Path;
                int slash = name.LastIndexOf('/');
                if (slash >= 0) name = name.Substring(slash + 1);

                string beside = Path.Combine(modelDir ?? "", name + ".png");
                if (File.Exists(beside)) { slot.File = beside; slot.Reload(); }
            }
        }

        /// <summary>Поднимаемся от папки модели до корня ресурспака.</summary>
        private static string PackRoot(string dir)
        {
            var at = string.IsNullOrEmpty(dir) ? null : new DirectoryInfo(dir);

            for (int hop = 0; hop < 8 && at != null; hop++)
            {
                if (File.Exists(Path.Combine(at.FullName, "pack.mcmeta"))) return at.FullName;
                if (Directory.Exists(Path.Combine(at.FullName, "assets"))) return at.FullName;
                at = at.Parent;
            }
            return null;
        }

        private void Save()
        {
            using (var dialog = new SaveFileDialog
            {
                Title = "Куда сохранить модель",
                Filter = "Модель|*.json",
                FileName = _file.Length > 0 ? Path.GetFileName(_file) : "model.json"
            })
            {
                if (_file.Length > 0) dialog.InitialDirectory = Path.GetDirectoryName(_file);
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    _model.Save(dialog.FileName);
                    _file = dialog.FileName;
                    Status("Сохранено: " + dialog.FileName);
                }
                catch (Exception ex) { Oops("Модель не сохранилась", ex); }
            }
        }

        private void ToPack()
        {
            var builds = new List<InstalledVersion>();
            foreach (var v in VersionCatalog.Scan()) builds.Add(v);

            if (builds.Count == 0)
            {
                Status("Сначала поставьте хотя бы одну версию игры.");
                return;
            }

            using (var pick = new ModelPackForm(builds, _model))
            {
                if (pick.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    var written = _model.SaveToPack(pick.PackRoot, pick.ModelName);
                    Status("Записано файлов: " + written.Count + ". Пак: " + pick.PackRoot);
                }
                catch (Exception ex) { Oops("В пак не записалось", ex); }
            }
        }

        /// <summary>Окно ошибки: редактор открыт как отдельное окно, Host ему не положен.</summary>
        private void Oops(string title, Exception ex)
        {
            Log.Error(title, ex);
            using (var form = new ErrorForm(title, ex, null)) form.ShowDialog(this);
            Status(title + ": " + ex.Message);
        }

        private void Status(string text)
        {
            _status.Text = text ?? "";
        }

        // ---------------------------------------------------------- рисование

        private void DrawCube(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var cube = item as ModelCube;
            if (cube == null) return;

            var box = Rectangle.Inflate(r, -4, -2);
            if (box.Width <= 0 || box.Height <= 0) return;

            if (selected) Theme.FillRound(g, box, 8, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 8, Color.FromArgb(16, 255, 255, 255));

            Theme.Text1(g, cube.Name, Theme.F(9.6f, FontStyle.Bold), Theme.Text,
                new Rectangle(box.X + 10, box.Y + 4, box.Width - 20, 18));

            string size = Text1(cube.High(0) - cube.Low(0)) + "×" +
                          Text1(cube.High(1) - cube.Low(1)) + "×" +
                          Text1(cube.High(2) - cube.Low(2));

            if (!string.IsNullOrEmpty(cube.Axis) && Math.Abs(cube.Angle) > 0.001f)
                size += " · поворот " + Text1(cube.Angle) + "° по " + cube.Axis.ToUpperInvariant();

            Theme.Text1(g, size, Theme.F(8.4f), Theme.Dim,
                new Rectangle(box.X + 10, box.Y + 22, box.Width - 20, 16));
        }

        private void DrawSlot(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var slot = item as ModelTexture;
            if (slot == null) return;

            var box = Rectangle.Inflate(r, -4, -2);
            if (box.Width <= 0 || box.Height <= 0) return;

            if (selected) Theme.FillRound(g, box, 8, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 8, Color.FromArgb(16, 255, 255, 255));

            var icon = new Rectangle(box.X + 8, box.Y + 4, 26, 26);

            if (slot.Image != null)
            {
                var saved = g.InterpolationMode;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                try { g.DrawImage(slot.Image, icon); } catch { }
                g.InterpolationMode = saved;
            }
            else Theme.FillRound(g, icon, 6, Color.FromArgb(40, 255, 255, 255));

            Theme.Text1(g, "#" + slot.Key, Theme.F(9.4f, FontStyle.Bold), Theme.Text,
                new Rectangle(icon.Right + 8, box.Y + 2, 40, 16));

            string path = slot.Path.Length > 0 ? slot.Path : "путь не задан";
            Theme.Text1(g, path, Theme.F(8.2f), slot.Path.Length > 0 ? Theme.Dim : Theme.Warn,
                new Rectangle(icon.Right + 8, box.Y + 18, Math.Max(40, box.Right - icon.Right - 16), 16));
        }

        private void DrawFace(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            string side = item as string;
            if (side == null) return;

            var box = Rectangle.Inflate(r, -4, -2);
            if (box.Width <= 0 || box.Height <= 0) return;

            if (selected) Theme.FillRound(g, box, 8, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 8, Color.FromArgb(16, 255, 255, 255));

            var cube = Cube;
            var face = cube == null ? null : cube.Face(side);

            Theme.Text1(g, ModelCube.SideRu(side), Theme.F(9.2f, FontStyle.Bold),
                face != null && face.Hidden ? Theme.Dim : Theme.Text,
                new Rectangle(box.X + 10, box.Y + 2, 150, 16));

            if (face == null) return;

            string note = face.Hidden
                ? "скрыта"
                : face.Texture + (face.Uv == null ? " · uv сам" : " · uv вручную") +
                  (face.Rotation != 0 ? " · " + face.Rotation + "°" : "");

            Theme.Text1(g, note, Theme.F(8.2f), face.Hidden ? Theme.Warn : Theme.Dim,
                new Rectangle(box.X + 10, box.Y + 18, Math.Max(40, box.Width - 20), 16));
        }

        // ------------------------------------------------------------ вёрстка

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Layout1();
        }

        private void Layout1()
        {
            if (_cubes == null) return;

            int pad = 18;
            int top = 16;

            _new.Location = new Point(pad, top);
            _open.Location = new Point(_new.Right + 8, top);
            _save.Location = new Point(_open.Right + 8, top);
            _pack.Location = new Point(_save.Right + 8, top);
            _close.Location = new Point(Math.Max(_pack.Right + 8, ClientSize.Width - pad - _close.Width), top);

            int left = pad;
            int leftW = 240;
            int rightW = 300;
            int rightX = Math.Max(left + leftW + 20, ClientSize.Width - pad - rightW);

            int listTop = top + 52;
            int bottom = ClientSize.Height - 40;

            // левая колонка: кубы сверху, текстуры снизу
            int cubesH = Math.Max(120, (bottom - listTop) * 55 / 100 - 90);

            _cubes.SetBounds(left, listTop, leftW, cubesH);
            _add.Location = new Point(left, _cubes.Bottom + 8);
            _copy.Location = new Point(left + 148, _cubes.Bottom + 8);
            _drop.Location = new Point(left, _add.Bottom + 6);
            _up.Location = new Point(left + 148, _add.Bottom + 6);

            int slotsTop = _drop.Bottom + 14;
            int slotsH = Math.Max(80, bottom - slotsTop - 84);

            _slots.SetBounds(left, slotsTop, leftW, slotsH);
            _texturePath.SetBounds(left, _slots.Bottom + 8, leftW, 34);
            _addTexture.Location = new Point(left, _texturePath.Bottom + 6);
            _dropTexture.Location = new Point(left + 148, _texturePath.Bottom + 6);

            // середина
            int viewX = left + leftW + 20;
            _view.SetBounds(viewX, listTop, Math.Max(180, rightX - viewX - 20), bottom - listTop);

            // правая колонка
            int y = listTop;
            _name.SetBounds(rightX, y, rightW, 34); y += 42;

            int cell = (rightW - 16) / 3;
            for (int i = 0; i < 3; i++) _from[i].SetBounds(rightX + i * (cell + 8), y, cell, 32);
            y += 38;
            for (int i = 0; i < 3; i++) _to[i].SetBounds(rightX + i * (cell + 8), y, cell, 32);
            y += 46;

            _axis.Location = new Point(rightX, y);
            _angle.Location = new Point(rightX + 128, y);
            y += 40;

            for (int i = 0; i < 3; i++) _origin[i].SetBounds(rightX + i * (cell + 8), y, cell, 32);
            y += 46;

            int facesH = Math.Max(90, bottom - y - 130);
            _faces.SetBounds(rightX, y, rightW, facesH);
            y = _faces.Bottom + 8;

            _faceTexture.Location = new Point(rightX, y);
            _faceTurn.Location = new Point(rightX + 128, y);
            y += 38;
            _faceHide.Location = new Point(rightX, y);
            _faceAuto.Location = new Point(rightX + 128, y);
            y += 40;

            int quarter = (rightW - 24) / 4;
            for (int i = 0; i < 4; i++) _uv[i].SetBounds(rightX + i * (quarter + 8), y, quarter, 30);

            _status.SetBounds(pad, ClientSize.Height - 30, Math.Max(100, ClientSize.Width - pad * 2), 22);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.Smooth(g);

            int pad = 18;
            int listTop = 16 + 52;

            Theme.Text1(g, "КУБЫ", Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                new Rectangle(pad, listTop - 20, 200, 16));

            Theme.Text1(g, "ТЕКСТУРЫ", Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                new Rectangle(pad, _slots.Top - 20, 200, 16));

            int rightX = _name.Left;

            Theme.Text1(g, "КУБ: НАЗВАНИЕ, ОТ И ДО (0…16)", Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                new Rectangle(rightX, listTop - 20, 300, 16));

            Theme.Text1(g, "ПОВОРОТ И ЕГО ЦЕНТР", Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                new Rectangle(rightX, _axis.Top - 18, 300, 16));

            Theme.Text1(g, "ГРАНИ", Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                new Rectangle(rightX, _faces.Top - 18, 300, 16));

            Theme.Text1(g, "UV ГРАНИ: U1 V1 U2 V2", Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                new Rectangle(rightX, _uv[0].Top - 18, 300, 16));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _model != null)
                foreach (var slot in _model.Textures)
                    if (slot.Image != null) { slot.Image.Dispose(); slot.Image = null; }

            base.Dispose(disposing);
        }
    }
}
