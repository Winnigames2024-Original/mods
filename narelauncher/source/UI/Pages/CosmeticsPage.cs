using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI.Pages
{
    /// <summary>
    /// Вещи для персонажа: плащи, шляпы, рюкзаки. Слева список, справа живой
    /// персонаж — он сразу примеряет то, что выбрано, так что видно, как это
    /// смотрится в движении, а не на плоской картинке.
    /// </summary>
    public class CosmeticsPage : PageBase
    {
        private readonly VirtualList _list = new VirtualList
        {
            ItemHeight = 64,
            EmptyText = "Вещей пока нет"
        };

        private readonly PlayerStage _stage = new PlayerStage { ShowCaption = false };

        private readonly NareButton _wear = new NareButton
        { Text = "Надеть", Style = BtnStyle.Accent, Icon = Ico.Check, Size = new Size(160, 42) };

        private readonly NareButton _make = new NareButton
        { Text = "Создать свою", Style = BtnStyle.Ghost, Icon = Ico.Skin, Size = new Size(180, 42) };

        private readonly NareButton _import = new NareButton
        { Text = "Из файла", Style = BtnStyle.Ghost, Icon = Ico.Folder, Size = new Size(150, 42) };

        private readonly NareButton _remove = new NareButton
        { Text = "Удалить", Style = BtnStyle.Ghost, Size = new Size(140, 42) };

        private readonly NareButton _folder = new NareButton
        { Text = "Папка", Style = BtnStyle.Ghost, Icon = Ico.Folder, Size = new Size(120, 42) };

        private readonly NareLabel _status =
            new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);

        private List<Cosmetic> _items = new List<Cosmetic>();

        public CosmeticsPage()
        {
            _list.Renderer = Draw;
            _list.SelectionChanged += delegate { Preview(); };

            Controls.Add(_list);
            Controls.Add(_stage);
            Controls.Add(_wear);
            Controls.Add(_make);
            Controls.Add(_import);
            Controls.Add(_remove);
            Controls.Add(_folder);
            Controls.Add(_status);

            _wear.Click += delegate { WearOrTakeOff(); };
            _make.Click += delegate { MakeNew(); };
            _import.Click += delegate { ImportFile(); };
            _remove.Click += delegate { RemoveSelected(); };

            _folder.Click += delegate
            {
                try
                {
                    Paths.Ensure(CosmeticStore.Dir);
                    Process.Start("explorer.exe", "\"" + CosmeticStore.Dir + "\"");
                }
                catch (Exception ex) { Host.ShowError("Не удалось открыть папку", ex); }
            };
        }

        public override void PageShown()
        {
            _stage.LoadFromAccount();
            Reload();
            Layout1();
            Invalidate();
        }

        // ------------------------------------------------------------ список

        private void Reload()
        {
            int keep = _list.SelectedIndex;

            _items = CosmeticStore.All();

            var objects = new List<object>();
            foreach (var item in _items) objects.Add(item);
            _list.SetItems(objects);

            if (_items.Count > 0)
                _list.SelectedIndex = keep >= 0 && keep < _items.Count ? keep : 0;

            Preview();
            UpdateButtons();
        }

        private Cosmetic Current { get { return _list.SelectedItem as Cosmetic; } }

        private void Preview()
        {
            var item = Current;

            // показываем выбранное поверх всего, что надето: так видно примерку
            var show = CosmeticStore.Worn();
            if (item != null && !CosmeticStore.IsWorn(item))
            {
                var mixed = new List<Cosmetic>();
                foreach (var worn in show)
                    if (worn.Kind != item.Kind) mixed.Add(worn);
                mixed.Add(item);
                show = mixed;
            }

            _stage.ShowList(show);
            UpdateButtons();
            Invalidate();
        }

        private void UpdateButtons()
        {
            var item = Current;
            bool worn = item != null && CosmeticStore.IsWorn(item);

            _wear.Text = worn ? "Снять" : "Надеть";
            _wear.Enabled = item != null;
            _remove.Enabled = item != null;

            _status.Text = item == null
                ? "Создайте вещь или положите готовую картинку в папку."
                : item.KindRu + " «" + item.Name + "»" + (worn ? " — надета" : "");
        }

        // --------------------------------------------------------- действия

        private void WearOrTakeOff()
        {
            var item = Current;
            if (item == null) return;

            if (CosmeticStore.IsWorn(item)) CosmeticStore.TakeOff(item);
            else CosmeticStore.Wear(item);

            Preview();
            _list.Invalidate();
            Host.RefreshPlayer();
        }

        private void MakeNew()
        {
            using (var dlg = new CosmeticEditForm())
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

                try
                {
                    string path = CosmeticStore.Make(dlg.Kind, dlg.ItemName,
                        dlg.BaseColor, dlg.Accent, dlg.Pattern, dlg.Letter);

                    Reload();
                    SelectByPath(path);
                }
                catch (Exception ex) { Host.ShowError("Вещь не создалась", ex); }
            }
        }

        private void ImportFile()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Картинка вещи";
                dlg.Filter = "Картинка PNG (*.png)|*.png";

                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

                using (var ask = new CosmeticEditForm(Path.GetFileNameWithoutExtension(dlg.FileName)))
                {
                    ask.ImportMode = true;
                    if (ask.ShowDialog(FindForm()) != DialogResult.OK) return;

                    try
                    {
                        string path = CosmeticStore.Import(ask.Kind, ask.ItemName, dlg.FileName);
                        Reload();
                        SelectByPath(path);
                    }
                    catch (Exception ex) { Host.ShowError("Картинку не удалось взять", ex); }
                }
            }
        }

        private void RemoveSelected()
        {
            var item = Current;
            if (item == null) return;

            if (!Host.Confirm("Удалить «" + item.Name + "» насовсем?")) return;

            CosmeticStore.Remove(item);
            Reload();
            Host.RefreshPlayer();
        }

        private void SelectByPath(string path)
        {
            for (int i = 0; i < _items.Count; i++)
                if (string.Equals(_items[i].Path, path, StringComparison.OrdinalIgnoreCase))
                {
                    _list.SelectedIndex = i;
                    Preview();
                    return;
                }
        }

        // ------------------------------------------------------------ вёрстка

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Layout1();
        }

        private void Layout1()
        {
            int pad = 28;
            int top = 86;

            int stageW = Math.Min(320, Math.Max(200, Width / 3));
            int listW = Math.Max(240, Width - pad * 3 - stageW);

            int listH = Math.Max(120, Height - top - 118);

            _list.SetBounds(pad, top, listW, listH);
            _stage.SetBounds(pad * 2 + listW, top, stageW, Math.Max(180, Height - top - 56));

            int y = top + listH + 14;
            _wear.Location = new Point(pad, y);
            _make.Location = new Point(pad + _wear.Width + 10, y);
            _import.Location = new Point(pad + _wear.Width + _make.Width + 20, y);

            int y2 = y + 50;
            _remove.Location = new Point(pad, y2);
            _folder.Location = new Point(pad + _remove.Width + 10, y2);

            _status.SetBounds(pad + _remove.Width + _folder.Width + 24, y2,
                Math.Max(80, listW - _remove.Width - _folder.Width - 24), 42);

            Invalidate();
        }

        private void Draw(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var thing = item as Cosmetic;
            if (thing == null) return;

            var box = Rectangle.Inflate(r, -4, -3);
            if (box.Width <= 0 || box.Height <= 0) return;

            if (selected) Theme.FillRound(g, box, 10, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 10, Color.FromArgb(16, 255, 255, 255));
            if (selected) Theme.DrawRound(g, box, 10, Color.FromArgb(120, Theme.Accent));

            var icon = new Rectangle(box.X + 12, box.Y + 8, 42, 42);
            var texture = CosmeticStore.Texture(thing);

            if (texture != null)
            {
                var saved = g.InterpolationMode;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

                // у плаща показываем его лицевую часть, у остального — всю картинку
                var src = thing.Kind == CosmeticKind.Cape && texture.Width >= 22
                    ? new Rectangle(1, 1, 10, 16)
                    : new Rectangle(0, 0, texture.Width, texture.Height);

                try { g.DrawImage(texture, icon, src, GraphicsUnit.Pixel); }
                catch { }

                g.InterpolationMode = saved;
                Theme.DrawRound(g, icon, 4, Color.FromArgb(40, 255, 255, 255));
            }
            else Theme.FillRound(g, icon, 8, Color.FromArgb(44, Theme.Accent));

            bool worn = CosmeticStore.IsWorn(thing);

            Theme.Text1(g, thing.Name, Theme.F(10.5f, FontStyle.Bold), Theme.Text,
                new Rectangle(icon.Right + 14, box.Y + 10, Math.Max(60, box.Width - 180), 20));

            Theme.Text1(g, thing.KindRu, Theme.F(8.8f), Theme.Muted,
                new Rectangle(icon.Right + 14, box.Y + 32, Math.Max(60, box.Width - 180), 18));

            if (worn)
                Theme.Text1(g, "надето", Theme.F(9f, FontStyle.Bold), Theme.Good,
                    new Rectangle(box.Right - 96, box.Y, 84, box.Height),
                    StringAlignment.Far, StringAlignment.Center);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.Smooth(g);

            Theme.Text1(g, "Вещи", Theme.F(17f, FontStyle.Bold), Theme.Text,
                new Rectangle(28, 16, 400, 28));

            Theme.Text1(g, "Плащи, шляпы и рюкзаки для вашего персонажа в лаунчере",
                Theme.F(9.5f), Theme.Muted, new Rectangle(28, 42, 900, 20));

            Theme.Text1(g, "В игре их видно не будет: сервер о них не знает, это украшение лаунчера.",
                Theme.F(8.8f), Theme.Dim, new Rectangle(28, 60, 900, 18));
        }
    }
}
