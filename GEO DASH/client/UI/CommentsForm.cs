using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    /// <summary>Комментарии к уровню: читаем и пишем прямо из игры.</summary>
    public class CommentsForm : GdForm
    {
        readonly int _levelId;
        readonly string _levelName;
        readonly ListView _list;
        readonly TextBox _text;
        List<CommentDto> _data = new();
        string _msg = "";

        public CommentsForm(int levelId, string levelName)
        {
            _levelId = levelId;
            _levelName = levelName ?? "";
            Text = "GEO DASH — Комментарии";
            ClientSize = new Size(880, 620);
            BgA = Color.FromArgb(60, 110, 230); BgId = 2; GroundId = 1;

            _list = new ListView
            {
                Left = 28, Top = 120, Width = ClientSize.Width - 56, Height = 340,
                View = View.Details, FullRowSelect = true, HideSelection = false,
                BackColor = Color.FromArgb(20, 16, 46), ForeColor = Gd.Paper,
                BorderStyle = BorderStyle.FixedSingle, Font = Gd.Bold(10.5f)
            };
            _list.Columns.Add("Игрок", 190);
            _list.Columns.Add("Комментарий", 490);
            _list.Columns.Add("Когда", 120);
            Host(_list);

            _text = new TextBox
            {
                Left = 28, Top = 476, Width = ClientSize.Width - 250, Font = Gd.Bold(12f),
                BackColor = Color.FromArgb(22, 18, 52), ForeColor = Gd.Paper,
                BorderStyle = BorderStyle.FixedSingle, MaxLength = 300
            };
            _text.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await Send(); } };
            Host(_text);

            Add("ОТПРАВИТЬ", GdIcon.Check, ClientSize.Width - 212, 468, 184, 46,
                Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100), async (s, e) => await Send());
            Add("ОБНОВИТЬ", GdIcon.Refresh, 28, 532, 180, 48,
                Color.FromArgb(150, 170, 220), Color.FromArgb(60, 70, 140), async (s, e) => await Reload());
            Add("УДАЛИТЬ", GdIcon.Trash, 218, 532, 180, 48,
                Color.FromArgb(240, 120, 120), Color.FromArgb(150, 40, 50), async (s, e) => await Delete());
            Add("НАЗАД", GdIcon.Back, ClientSize.Width - 212, 532, 184, 48,
                Color.FromArgb(170, 170, 200), Color.FromArgb(70, 70, 110), (s, e) => Close());

            Load += async (s, e) => await Reload();
        }

        async Task Reload()
        {
            _msg = "Загружаю…"; Content.Invalidate();
            var r = await ApiClient.CommentsAsync(_levelId);
            _data = r.Ok && r.Data != null ? r.Data : new List<CommentDto>();
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var c in _data)
            {
                var it = new ListViewItem(c.Author);
                it.SubItems.Add(c.Text);
                it.SubItems.Add(c.Created.Length >= 16 ? c.Created.Substring(0, 16).Replace('T', ' ') : c.Created);
                if (string.Equals(c.Author, AppConfig.Settings.UserName, StringComparison.OrdinalIgnoreCase))
                    it.ForeColor = Gd.Lime;
                _list.Items.Add(it);
            }
            _list.EndUpdate();
            _msg = r.Ok ? (_data.Count == 0 ? "Комментариев пока нет — будь первым" : "")
                        : "Не загрузилось: " + r.Error;
            Content.Invalidate();
        }

        async Task Send()
        {
            string t = _text.Text.Trim();
            if (t.Length == 0) return;
            if (!AppConfig.LoggedIn) { _msg = "Нужен вход в аккаунт"; Content.Invalidate(); return; }

            _msg = "Отправляю…"; Content.Invalidate();
            var r = await ApiClient.AddCommentAsync(_levelId, t);
            if (r.Ok) { _text.Clear(); await Reload(); }
            else { _msg = r.Error; Content.Invalidate(); }
        }

        async Task Delete()
        {
            if (_list.SelectedIndices.Count == 0) return;
            var c = _data[_list.SelectedIndices[0]];
            if (!GdDialog.Confirm(this, "Комментарии", $"Удалить комментарий от {c.Author}?")) return;
            var r = await ApiClient.DeleteCommentAsync(_levelId, c.Id);
            _msg = r.Ok ? "Удалено" : r.Error;
            await Reload();
        }

        protected override void OnRender(Graphics g)
        {
            Header(g, "КОММЕНТАРИИ", _levelName + "   ·   уровень #" + _levelId);
            if (!string.IsNullOrEmpty(_msg))
                using (var f = Gd.Bold(10.5f))
                    Gd.Outlined(g, _msg, f, new RectangleF(28, 92, ClientSize.Width - 56, 24),
                        Gd.Gold, StringAlignment.Near, thick: 2.2f);
        }
    }
}
