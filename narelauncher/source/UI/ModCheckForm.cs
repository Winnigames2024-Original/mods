using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Что в папке модов не подойдёт этой сборке. Отключаем переименованием
    /// файла, а не удалением: мод может пригодиться для другой сборки.
    /// </summary>
    public class ModCheckForm : Form
    {
        private readonly VirtualList _list = new VirtualList
        {
            ItemHeight = 58,
            EmptyText = "Всё подходит — ни одного спорного мода"
        };

        private readonly NareLabel _status =
            new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);

        private readonly NareButton _off =
            new NareButton { Text = "Отключить выбранный", Style = BtnStyle.Ghost, Size = new Size(210, 40) };

        private readonly NareButton _offAll =
            new NareButton { Text = "Отключить все лишние", Style = BtnStyle.Accent, Size = new Size(210, 40) };

        private readonly NareButton _folder =
            new NareButton { Text = "Папка", Style = BtnStyle.Ghost, Size = new Size(90, 40) };

        private readonly NareButton _close =
            new NareButton { Text = "Закрыть", Style = BtnStyle.Ghost, Size = new Size(100, 40) };

        private readonly string _type;
        private readonly InstalledVersion _build;
        private List<ModCheck.Problem> _problems = new List<ModCheck.Problem>();

        /// <summary>Что-то поменяли — странице модов стоит перечитать список.</summary>
        public bool Changed;

        public ModCheckForm(string projectType, InstalledVersion build)
        {
            _type = projectType;
            _build = build;

            Text = "Проверка модов";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(10f);
            ClientSize = new Size(620, 470);

            _list.Renderer = Draw;
            _list.SetBounds(20, 80, 580, 300);
            _list.SelectionChanged += delegate { Buttons(); };
            Controls.Add(_list);

            _status.SetBounds(22, 50, 580, 20);
            Controls.Add(_status);

            _off.Location = new Point(20, 406);
            _offAll.Location = new Point(238, 406);
            _folder.Location = new Point(456, 406);
            _close.Location = new Point(552, 406);
            _close.Size = new Size(48, 40);

            _off.Click += delegate
            {
                var one = _list.SelectedItem as ModCheck.Problem;
                if (one == null || !one.Bad) return;

                ModCheck.TurnOff(one);
                Changed = true;
                Scan();
            };

            _offAll.Click += delegate
            {
                int done = 0;
                foreach (var p in _problems)
                    if (p.Bad) { ModCheck.TurnOff(p); done++; }

                if (done > 0) Changed = true;
                Scan();
                _status.Text = done == 0 ? _status.Text : "Отключено модов: " + done + ". Файлы остались на месте.";
            };

            _folder.Click += delegate
            {
                if (_build != null) ModManager.OpenFolder(_type, _build.Id);
            };

            _close.Click += delegate { DialogResult = DialogResult.OK; Close(); };

            Controls.Add(_off);
            Controls.Add(_offAll);
            Controls.Add(_folder);
            Controls.Add(_close);

            Load += delegate { Scan(); };
        }

        private void Scan()
        {
            _problems = ModCheck.Scan(_type, _build);

            var items = new List<object>();
            foreach (var p in _problems) items.Add(p);
            _list.SetItems(items);

            int total = _build == null ? 0 : ModManager.List(_type, _build.Id).Count;
            _status.Text = ModCheck.Summary(_problems, total);

            if (_problems.Count > 0 && _list.SelectedIndex < 0) _list.SelectedIndex = 0;
            Buttons();
        }

        private void Buttons()
        {
            var one = _list.SelectedItem as ModCheck.Problem;
            _off.Enabled = one != null && one.Bad;

            bool any = false;
            foreach (var p in _problems) if (p.Bad) { any = true; break; }
            _offAll.Enabled = any;
        }

        private static Color Tint(ModCheck.Kind kind)
        {
            switch (kind)
            {
                case ModCheck.Kind.Game: return Theme.Bad;
                case ModCheck.Kind.Loader: return Theme.Bad;
                case ModCheck.Kind.Twice: return Theme.Warn;
                case ModCheck.Kind.Broken: return Theme.Bad;
                default: return Theme.Muted;
            }
        }

        private static string TagOf(ModCheck.Kind kind)
        {
            switch (kind)
            {
                case ModCheck.Kind.Game: return "другая версия";
                case ModCheck.Kind.Loader: return "другой загрузчик";
                case ModCheck.Kind.Twice: return "дважды";
                case ModCheck.Kind.Broken: return "битый файл";
                default: return "не пишет о себе";
            }
        }

        private void Draw(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var p = item as ModCheck.Problem;
            if (p == null) return;

            var box = Rectangle.Inflate(r, -4, -3);
            if (box.Width <= 0 || box.Height <= 0) return;

            if (selected) Theme.FillRound(g, box, 10, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 10, Color.FromArgb(16, 255, 255, 255));
            if (selected) Theme.DrawRound(g, box, 10, Color.FromArgb(120, Theme.Accent));

            // полоска слева цветом беды
            using (var b = new SolidBrush(Tint(p.Kind)))
                g.FillRectangle(b, box.X + 6, box.Y + 10, 3, box.Height - 20);

            string name = p.About != null && p.About.Label.Length > 0
                ? p.About.Label : p.Mod.FileName;

            Theme.Text1(g, name, Theme.F(10.4f, FontStyle.Bold), Theme.Text,
                new Rectangle(box.X + 18, box.Y + 6, Math.Max(60, box.Width - 160), 20));

            string tag = TagOf(p.Kind);
            Theme.Text1(g, tag, Theme.F(8.4f, FontStyle.Bold), Tint(p.Kind),
                new Rectangle(box.Right - 140, box.Y + 8, 130, 18),
                StringAlignment.Far, StringAlignment.Center);

            Theme.Text1(g, p.Text, Theme.F(8.8f), Theme.Muted,
                new Rectangle(box.X + 18, box.Y + 28, Math.Max(60, box.Width - 30), 18));

            Theme.Text1(g, p.Mod.FileName, Theme.F(8.2f), Theme.Dim,
                new Rectangle(box.X + 18, box.Y + 42, Math.Max(60, box.Width - 30), 16));
        }
    }
}
