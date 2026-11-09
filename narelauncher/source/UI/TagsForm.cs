using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Теги (значки) за монеты и покупка отображаемого имени в чатах.
    /// </summary>
    public class TagsForm : Form
    {
        private readonly Account _account;

        private readonly NareLabel _coins = new NareLabel("", 12f, FontStyle.Bold, Theme.Text);
        private readonly VirtualList _list = new VirtualList { ItemHeight = 46, EmptyText = "Загружаю…" };
        private readonly NareButton _buy = new NareButton { Text = "Купить / надеть", Style = BtnStyle.Accent, Size = new Size(190, 40) };
        private readonly NareButton _off = new NareButton { Text = "Снять тег", Style = BtnStyle.Ghost, Size = new Size(140, 40) };
        private readonly NareTextBox _name = new NareTextBox { Placeholder = "Имя в чатах" };
        private readonly NareButton _setName = new NareButton { Text = "Купить имя", Style = BtnStyle.Success, Size = new Size(150, 38) };
        private readonly NareButton _clrName = new NareButton { Text = "Убрать имя", Style = BtnStyle.Ghost, Size = new Size(140, 38) };
        private readonly NareButton _close = new NareButton { Text = "Закрыть", Style = BtnStyle.Ghost, Size = new Size(120, 38) };
        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);

        private NareAuth.TagsState _st = new NareAuth.TagsState();

        public TagsForm(Account account)
        {
            _account = account;
            Text = "Теги и имя";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = MinimizeBox = false; ShowInTaskbar = false;
            ClientSize = new Size(520, 520);
            BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.F(10f);

            _coins.SetBounds(24, 16, 472, 26);
            Cap("Теги (значок рядом с именем)", 50);
            _list.SetBounds(24, 74, 472, 230);
            _buy.Location = new Point(24, 312);
            _off.Location = new Point(224, 312);
            Cap("Имя в чатах за монеты", 362);
            _name.SetBounds(24, 386, 240, 38);
            _setName.Location = new Point(276, 386);
            _clrName.Location = new Point(436, 386);
            _close.Location = new Point(380, 468);
            _status.SetBounds(24, 436, 472, 20);

            _list.Renderer = DrawTag;
            _buy.Click += async (s, e) => await Buy();
            _off.Click += async (s, e) => await Equip("");
            _setName.Click += async (s, e) => await SetName();
            _clrName.Click += async (s, e) => await ClearName();
            _close.Click += (s, e) => Close();

            Controls.Add(_coins); Controls.Add(_list); Controls.Add(_buy); Controls.Add(_off);
            Controls.Add(_name); Controls.Add(_setName); Controls.Add(_clrName);
            Controls.Add(_close); Controls.Add(_status);
            foreach (var c in _caps) Controls.Add(c);

            Load += async delegate { await Reload(); };
        }

        private string Token { get { return _account.Token; } }

        private async Task Reload()
        {
            try
            {
                _st = await NareAuth.TagsListAsync(Token, CancellationToken.None);
                NareAuth.Last.Coins = _st.Coins;
                _coins.Text = "Монеты: " + _st.Coins;
                var items = new List<object>();
                foreach (var t in _st.Catalog) items.Add(t);
                _list.SetItems(items);
                if (_name.Text.Length == 0) _name.Text = _st.ChatName;
                _setName.Text = "Купить имя (" + _st.ChatNamePrice + ")";
            }
            catch (Exception ex) { _status.Text = ex.Message; }
        }

        private async Task Buy()
        {
            var t = _list.SelectedItem as NareAuth.TagInfo;
            if (t == null) { _status.Text = "Выберите тег."; return; }
            try
            {
                if (_st.Owned.Contains(t.Id)) await NareAuth.TagsEquipAsync(Token, t.Id, CancellationToken.None);
                else await NareAuth.TagsBuyAsync(Token, t.Id, CancellationToken.None);
                _status.Text = t.Label + " — надет.";
                _status.ForeColor = Theme.Good;
                await Reload();
            }
            catch (Exception ex) { _status.Text = "Не вышло: " + ex.Message; _status.ForeColor = Theme.Bad; }
        }

        private async Task Equip(string id)
        {
            try { await NareAuth.TagsEquipAsync(Token, id, CancellationToken.None); _status.Text = "Тег снят."; await Reload(); }
            catch (Exception ex) { _status.Text = ex.Message; }
        }

        private async Task SetName()
        {
            string n = _name.Text.Trim();
            if (n.Length < 2) { _status.Text = "Имя слишком короткое."; return; }
            try
            {
                await NareAuth.ChatNameSetAsync(Token, n, CancellationToken.None);
                _status.Text = "Имя в чатах теперь: " + n;
                _status.ForeColor = Theme.Good;
                await Reload();
            }
            catch (Exception ex) { _status.Text = "Не вышло: " + ex.Message; _status.ForeColor = Theme.Bad; }
        }

        private async Task ClearName()
        {
            try { await NareAuth.ChatNameClearAsync(Token, CancellationToken.None); _name.Text = ""; _status.Text = "Имя убрано."; await Reload(); }
            catch (Exception ex) { _status.Text = ex.Message; }
        }

        private void DrawTag(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var t = item as NareAuth.TagInfo;
            if (t == null) return;
            var box = Rectangle.Inflate(r, -4, -3);
            if (selected) Theme.FillRound(g, box, 9, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 9, Color.FromArgb(16, 255, 255, 255));

            Theme.Text1(g, t.Label, Theme.F(13f, FontStyle.Bold), Theme.Text,
                new Rectangle(box.X + 14, box.Y, 60, box.Height), StringAlignment.Near, StringAlignment.Center);

            bool owned = _st.Owned.Contains(t.Id);
            bool on = _st.Equipped == t.Id;
            string right = on ? "НАДЕТ" : owned ? "куплен" : (t.Price + " монет");
            var col = on ? Theme.Good : owned ? Theme.Muted : Theme.Accent2;
            Theme.Text1(g, right, Theme.F(9.5f, FontStyle.Bold), col,
                new Rectangle(box.Right - 160, box.Y, 150, box.Height), StringAlignment.Far, StringAlignment.Center);
        }

        private readonly List<NareLabel> _caps = new List<NareLabel>();
        private void Cap(string text, int y)
        {
            _caps.Add(new NareLabel(text, 9f, FontStyle.Bold, Theme.Muted) { Bounds = new Rectangle(24, y, 460, 16) });
        }
    }
}
