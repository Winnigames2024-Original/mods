using GEO_DASH.Audio;
using GEO_DASH.Core;
using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    public enum LevelListMode { My, Saved }

    public class LevelListForm : GdForm
    {
        readonly LevelListMode _mode;
        readonly ListView _list;
        List<LevelData> _data = new();
        string _msg = "";

        public LevelListForm(LevelListMode mode)
        {
            _mode = mode;
            Text = mode == LevelListMode.My ? "GEO DASH — Мои уровни" : "GEO DASH — Сохранённые уровни";
            ClientSize = new Size(1040, 660);
            BgA = mode == LevelListMode.My ? Color.FromArgb(60, 36, 16) : Color.FromArgb(16, 50, 40);

            _list = new ListView
            {
                Left = 24, Top = 110, Width = 700, Height = 500,
                View = View.Details, FullRowSelect = true, HideSelection = false,
                BackColor = Color.FromArgb(20, 18, 48), ForeColor = Gd.Paper,
                BorderStyle = BorderStyle.FixedSingle, Font = Gd.Reg(10f)
            };
            _list.Columns.Add("Название", 250);
            _list.Columns.Add("Сложность", 110);
            _list.Columns.Add("Объектов", 80);
            _list.Columns.Add("Музыка", 90);
            _list.Columns.Add("Прогресс", 90);
            _list.Columns.Add(mode == LevelListMode.My ? "Сервер" : "ID", 70);
            _list.DoubleClick += (s, e) => Play(false);
            Host(_list);

            int x = 744, y = 110;
            GdButton B(string t, GdIcon ic, Color c1, Color c2, EventHandler h)
            {
                var b = Add(t, ic, x, y, 260, 52, c1, c2, h);
                y += 58;
                return b;
            }

            B("ИГРАТЬ", GdIcon.Play, Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100), (s, e) => Play(false));
            B("ТРЕНИРОВКА", GdIcon.Check, Color.FromArgb(120, 190, 255), Color.FromArgb(30, 90, 190), (s, e) => Play(true));

            if (mode == LevelListMode.My)
            {
                B("НОВЫЙ УРОВЕНЬ", GdIcon.Plus, Color.FromArgb(255, 190, 80), Color.FromArgb(170, 100, 10), (s, e) => NewLevel());
                B("РЕДАКТИРОВАТЬ", GdIcon.Edit, Color.FromArgb(255, 170, 60), Color.FromArgb(180, 90, 10), (s, e) => Edit());
                B("КОПИЯ", GdIcon.Copy, Color.FromArgb(160, 170, 210), Color.FromArgb(70, 80, 130), (s, e) => Copy());
                B("НА СЕРВЕР", GdIcon.Upload, Color.FromArgb(120, 220, 255), Color.FromArgb(20, 110, 180), async (s, e) => await Upload());
                B("УДАЛИТЬ", GdIcon.Trash, Color.FromArgb(230, 110, 110), Color.FromArgb(140, 40, 50), (s, e) => Delete());
            }
            else
            {
                B("КОПИЯ В МОИ", GdIcon.Copy, Color.FromArgb(160, 170, 210), Color.FromArgb(70, 80, 130), (s, e) => CopyToMine());
                B("ОБНОВИТЬ С СЕРВЕРА", GdIcon.Refresh, Color.FromArgb(120, 220, 255), Color.FromArgb(20, 110, 180), async (s, e) => await Redownload());
                B("УДАЛИТЬ ИЗ SAVED", GdIcon.Trash, Color.FromArgb(230, 110, 110), Color.FromArgb(140, 40, 50), (s, e) => Delete());
            }

            Add("НАЗАД", GdIcon.Back, x, ClientSize.Height - 72, 260, 48,
                Color.FromArgb(160, 160, 190), Color.FromArgb(70, 70, 110), (s, e) => Close());

            Load += (s, e) => Reload();
        }

        void Reload()
        {
            _data = _mode == LevelListMode.My ? LocalStore.MyLevels() : LocalStore.SavedLevels();
            _list.Items.Clear();
            foreach (var lv in _data)
            {
                var p = LocalStore.GetProgress(lv);
                var it = new ListViewItem(new[]
                {
                    lv.Settings.Name,
                    Difficulties.Label(lv.Settings.Difficulty),
                    lv.Objects.Count.ToString(),
                    lv.Settings.SongId == 0 ? "—" : (lv.Settings.SongId < 0 ? "встроенная" : "#" + lv.Settings.SongId),
                    $"{p.Best:0}%" + (p.Completed ? " ✓" : ""),
                    lv.Settings.ServerId > 0 ? "#" + lv.Settings.ServerId : "—"
                });
                it.ForeColor = Difficulties.Tint(lv.Settings.Difficulty);
                _list.Items.Add(it);
            }
            if (_list.Items.Count > 0) _list.Items[0].Selected = true;
            Content.Invalidate();
        }

        LevelData Sel => _list.SelectedIndices.Count > 0 && _list.SelectedIndices[0] < _data.Count
            ? _data[_list.SelectedIndices[0]] : null;

        void Play(bool practice)
        {
            var lv = Sel;
            if (lv == null) return;
            MusicPlayer.Stop();
            using (var f = new GameForm(lv.DeepClone(), practice)) f.ShowDialog(this);
            Reload();
        }

        void NewLevel()
        {
            var lv = new LevelData();
            lv.Settings.Name = "Уровень " + (LocalStore.MyLevels().Count + 1);
            lv.Settings.Author = AppConfig.Settings.UserName;
            LocalStore.SaveMyLevel(lv);
            using (var f = new EditorForm(lv)) f.ShowDialog(this);
            Reload();
        }

        void Edit()
        {
            var lv = Sel;
            if (lv == null) return;
            using (var f = new EditorForm(lv)) f.ShowDialog(this);
            Reload();
        }

        void Copy()
        {
            var lv = Sel;
            if (lv == null) return;
            LocalStore.CopyLevel(lv);
            _msg = "Копия создана";
            Reload();
        }

        /// <summary>Копирование скачанного уровня в свои — с учётом защиты от копирования.</summary>
        void CopyToMine()
        {
            var lv = Sel;
            if (lv == null) return;
            var st = lv.Settings;

            if (st.CopyMode == 0)
            {
                MessageBox.Show("Автор закрыл копирование этого уровня.", "Копирование запрещено",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (st.CopyMode == 1)
            {
                string pass = Prompt("Уровень копируется по паролю. Введи пароль автора:");
                if (pass == null) return;
                if (!string.Equals(pass.Trim(), st.CopyPassword, StringComparison.Ordinal))
                {
                    MessageBox.Show("Неверный пароль.", "Копирование", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            var c = lv.DeepClone();
            c.Settings.LocalId = Guid.NewGuid().ToString("N");
            c.Settings.ServerId = 0;
            c.Settings.Name = st.Name + " (копия)";
            c.Settings.Author = AppConfig.Settings.UserName;
            c.Settings.CopyMode = 0;
            c.Settings.CopyPassword = "";
            LocalStore.SaveMyLevel(c);
            MessageBox.Show("Скопировано в «Мои уровни».", "Готово");
        }

        async Task Redownload()
        {
            var lv = Sel;
            if (lv?.Settings.ServerId > 0)
            {
                _msg = "Скачиваю…"; Content.Invalidate();
                await LocalStore.DownloadAsync(lv.Settings.ServerId, true, new Progress<string>(s => { _msg = s; Content.Invalidate(); }));
                Reload();
            }
        }

        async Task Upload()
        {
            var lv = Sel;
            if (lv == null) return;
            if (!AppConfig.LoggedIn) { MessageBox.Show("Сначала войди в аккаунт."); return; }
            _msg = "Загружаю…"; Content.Invalidate();
            var r = await ApiClient.UploadLevelAsync(lv, lv.Settings.ServerId, lv.Settings.Password);
            if (r.Ok && r.Data != null)
            {
                lv.Settings.ServerId = r.Data.Id;
                LocalStore.SaveMyLevel(lv);
                _msg = "Загружено, ID #" + r.Data.Id;
            }
            else _msg = r.Error;
            Reload();
        }

        void Delete()
        {
            var lv = Sel;
            if (lv == null) return;
            if (MessageBox.Show("Удалить «" + lv.Settings.Name + "»?", "Удаление",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            if (_mode == LevelListMode.My) LocalStore.DeleteMyLevel(lv);
            else LocalStore.DeleteSaved(lv.Settings.ServerId);
            Reload();
        }

        public static string Prompt(string text, string def = "")
        {
            using var f = new Form
            {
                Text = "GEO DASH", Width = 420, Height = 180, FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent, BackColor = Gd.Deep, ForeColor = Gd.Paper,
                MaximizeBox = false, MinimizeBox = false
            };
            var l = new Label { Left = 16, Top = 14, Width = 370, Height = 40, Text = text, ForeColor = Gd.Paper };
            var tb = new TextBox { Left = 16, Top = 58, Width = 370, Text = def, BackColor = Color.FromArgb(24, 20, 54), ForeColor = Gd.Paper };
            var ok = new Button { Left = 206, Top = 94, Width = 85, Text = "OK", DialogResult = DialogResult.OK };
            var ca = new Button { Left = 301, Top = 94, Width = 85, Text = "Отмена", DialogResult = DialogResult.Cancel };
            f.Controls.AddRange(new Control[] { l, tb, ok, ca });
            f.AcceptButton = ok; f.CancelButton = ca;
            return f.ShowDialog() == DialogResult.OK ? tb.Text : null;
        }

        protected override void OnRender(Graphics g)
        {
            Header(g, _mode == LevelListMode.My ? "МОИ УРОВНИ" : "SAVED — СОХРАНЁННЫЕ",
                _mode == LevelListMode.My
                    ? "хранятся в assets/levels рядом с игрой"
                    : "скачанные и офлайн-уровни: assets/saved (играются без интернета)");
            if (!string.IsNullOrEmpty(_msg))
                using (var f = Gd.Bold(10f))
                    Gd.Outlined(g, _msg, f, new RectangleF(24, ClientSize.Height - 40, 700, 24), Gd.Gold,
                        StringAlignment.Near, thick: 2.2f);
        }
    }
}
