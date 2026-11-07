using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Куда положить готовую модель: в ресурспак какой сборки, под каким
    /// именем. Пак создаётся сам, вместе с pack.mcmeta.
    /// </summary>
    public class ModelPackForm : Form
    {
        /// <summary>Папка пака на диске.</summary>
        public string PackRoot = "";

        /// <summary>Имя модели внутри пака: minecraft:block/моя_модель.</summary>
        public string ModelName = "";

        private readonly VirtualList _builds = new VirtualList { ItemHeight = 50, EmptyText = "Версий нет" };
        private readonly NareTextBox _pack = new NareTextBox { Placeholder = "naremodels" };
        private readonly NareTextBox _name = new NareTextBox { Placeholder = "minecraft:block/моя_модель" };
        private readonly NareLabel _where = new NareLabel("", 8.6f, FontStyle.Regular, Theme.Dim);

        private readonly NareButton _ok = new NareButton { Text = "Записать", Style = BtnStyle.Accent, Size = new Size(150, 40) };
        private readonly NareButton _cancel = new NareButton { Text = "Отмена", Style = BtnStyle.Ghost, Size = new Size(110, 40) };

        private readonly BlockModel _model;

        public ModelPackForm(List<InstalledVersion> builds, BlockModel model)
        {
            _model = model;

            Text = "Записать модель в ресурспак";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(10f);
            ClientSize = new Size(560, 480);

            _builds.Renderer = Draw;
            _builds.SetBounds(20, 46, 520, 230);
            _builds.SelectionChanged += delegate { Refresh1(); };
            Controls.Add(_builds);

            _pack.SetBounds(20, 312, 520, 34);
            _pack.Text = "naremodels";
            _pack.Inner.TextChanged += delegate { Refresh1(); };
            Controls.Add(_pack);

            _name.SetBounds(20, 376, 520, 34);
            _name.Text = "minecraft:block/моя_модель";
            _name.Inner.TextChanged += delegate { Refresh1(); };
            Controls.Add(_name);

            _where.SetBounds(22, 414, 520, 18);
            Controls.Add(_where);

            _ok.Location = new Point(20, 436);
            _cancel.Location = new Point(430, 436);

            _ok.Click += delegate
            {
                if (!Ready()) return;
                DialogResult = DialogResult.OK;
                Close();
            };
            _cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(_ok);
            Controls.Add(_cancel);

            var items = new List<object>();
            foreach (var v in builds) items.Add(v);
            _builds.SetItems(items);

            if (items.Count > 0)
            {
                _builds.SelectedIndex = 0;

                // по умолчанию выбираем ту сборку, что выбрана в лаунчере
                for (int i = 0; i < builds.Count; i++)
                    if (builds[i].Id == Settings.Current.SelectedVersion) { _builds.SelectedIndex = i; break; }
            }

            Refresh1();
        }

        private bool Ready()
        {
            var build = _builds.SelectedItem as InstalledVersion;
            string pack = (_pack.Text ?? "").Trim();
            string name = (_name.Text ?? "").Trim();

            if (build == null || pack.Length == 0 || name.Length == 0) return false;
            if (pack.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;

            PackRoot = Path.Combine(Paths.ResourcePacksDir(build.Id), pack);
            ModelName = name;
            return true;
        }

        private void Refresh1()
        {
            bool ready = Ready();
            _ok.Enabled = ready;
            _ok.Invalidate();

            if (!ready)
            {
                _where.Text = "Выберите сборку и впишите имя пака и модели.";
                _where.ForeColor = Theme.Warn;
                return;
            }

            string file = BlockModel.ModelPath(PackRoot, ModelName);

            int pictures = 0;
            foreach (var t in _model.Textures)
                if (!string.IsNullOrWhiteSpace(t.Path) && !t.Path.StartsWith("#")
                    && !string.IsNullOrEmpty(t.File) && File.Exists(t.File)) pictures++;

            _where.Text = file + (pictures > 0 ? "   + картинок: " + pictures : "   текстуры не приложены");
            _where.ForeColor = Theme.Dim;
        }

        private void Draw(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var build = item as InstalledVersion;
            if (build == null) return;

            var box = Rectangle.Inflate(r, -4, -2);
            if (box.Width <= 0 || box.Height <= 0) return;

            if (selected) Theme.FillRound(g, box, 9, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 9, Color.FromArgb(16, 255, 255, 255));

            Theme.Text1(g, build.Id, Theme.F(10f, FontStyle.Bold), Theme.Text,
                new Rectangle(box.X + 12, box.Y + 6, box.Width - 24, 18));

            Theme.Text1(g, build.LoaderRu + " · Minecraft " + build.BaseVersion, Theme.F(8.4f), Theme.Dim,
                new Rectangle(box.X + 12, box.Y + 24, box.Width - 24, 16));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.Smooth(g);

            Theme.Text1(g, "СБОРКА", Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                new Rectangle(22, 24, 300, 16));

            Theme.Text1(g, "ПАПКА ПАКА В resourcepacks", Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                new Rectangle(22, 290, 400, 16));

            Theme.Text1(g, "ИМЯ МОДЕЛИ ВНУТРИ ПАКА", Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                new Rectangle(22, 354, 400, 16));
        }
    }
}
