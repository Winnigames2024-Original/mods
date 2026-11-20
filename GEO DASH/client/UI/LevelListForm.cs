using GEO_DASH.Audio;
using GEO_DASH.Core;
using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    public enum LevelListMode { My, Saved }

    public class LevelListForm : GdForm
    {
        readonly LevelListMode _mode;
        readonly FlowLayoutPanel _list;
        readonly List<LevelCard> _cards = new();
        List<LevelData> _data = new();
        int _index;
        string _msg = "";

        public LevelListForm(LevelListMode mode)
        {
            _mode = mode;
            Text = mode == LevelListMode.My ? "GEO DASH — Мои уровни" : "GEO DASH — Сохранённые уровни";
            ClientSize = new Size(1040, 660);
            BgA = mode == LevelListMode.My ? Color.FromArgb(225, 125, 25) : Color.FromArgb(25, 170, 110);
            BgId = mode == LevelListMode.My ? 5 : 10; GroundId = mode == LevelListMode.My ? 4 : 3;

            // список карточек с картинкой уровня
            _list = new FlowLayoutPanel
            {
                Left = 24, Top = 110, Width = 700, Height = 500,
                BackColor = Color.FromArgb(150, 14, 12, 38),
                AutoScroll = true, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, Padding = new Padding(6)
            };
            Host(_list);

            int x = 744, y = 110;
            GdButton B(string t, GdIcon ic, Color c1, Color c2, EventHandler h)
            {
                var b = Add(t, ic, x, y, 260, 52, c1, c2, h);
                y += 58;
                return b;
            }

            B("ОБ УРОВНЕ", GdIcon.Info, Color.FromArgb(150, 200, 255), Color.FromArgb(35, 95, 185), (s, e) => Info());
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
                B("КОММЕНТАРИИ", GdIcon.Info, Color.FromArgb(140, 200, 255), Color.FromArgb(30, 95, 190),
                    (s, e) =>
                    {
                        var lv = Sel;
                        if (lv == null || lv.Settings.ServerId <= 0) { _msg = "Уровень не с сервера"; Content.Invalidate(); return; }
                        Nav.Go(new CommentsForm(lv.Settings.ServerId, lv.Settings.Name));
                    });
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
            _list.Controls.Clear();
            _cards.Clear();
            int w = _list.ClientSize.Width - 28;

            for (int i = 0; i < _data.Count; i++)
            {
                int idx = i;
                var card = new LevelCard
                {
                    Level = _data[i], Width = Math.Max(400, w), Height = 140,
                    Margin = new Padding(2, 2, 2, 6), Selected = i == _index
                };
                // клик по уровню открывает его карточку — как в игре
                card.Click += (s, e) => { if (card.LastClickOnSong) return; Select(idx); Info(); };
                card.SongClicked += async (s, e) => await SongAction(card);
                card.DoubleClick += (s, e) => { Select(idx); Play(false); };
                _cards.Add(card);
                _list.Controls.Add(card);
            }
            if (_index >= _data.Count) _index = Math.Max(0, _data.Count - 1);
            Content.Invalidate();
        }

        void Select(int i)
        {
            _index = i;
            for (int k = 0; k < _cards.Count; k++)
            {
                _cards[k].Selected = k == i;
                _cards[k].Invalidate();
            }
            Content.Invalidate();
        }

        LevelData Sel => _index >= 0 && _index < _data.Count ? _data[_index] : null;

        /// <summary>Кнопка трека прямо на карточке: скачать его или удалить.</summary>
        async Task SongAction(LevelCard card)
        {
            var lv = card?.Level;
            int id = lv?.Settings.SongId ?? 0;
            if (id <= 0) return;

            if (MusicPlayer.IsCached(id))
            {
                if (!GdDialog.Confirm(this, "Удалить трек",
                        $"Удалить скачанный трек #{id}? Уровень без него будет играть молча."))
                    return;
                _msg = MusicPlayer.Delete(id) ? $"Трек #{id} удалён" : $"Не вышло удалить трек #{id}";
                card.Invalidate();
                Content.Invalidate();
                return;
            }

            _msg = $"Качаю трек #{id}…";
            Content.Invalidate();
            var path = await MusicPlayer.EnsureAsync(id, new Progress<string>(t =>
            {
                _msg = t; Content.Invalidate();
            }));

            if (path != null)
            {
                _msg = $"Трек #{id} скачан";
                Toasts.Show("Трек скачан", "#" + id + "  " + (lv.Settings.SongName ?? ""), Toasts.Kind.Download);
            }
            else
                _msg = ApiClient.Online
                    ? $"Сервер не отдал трек #{id} — залей mp3 на сайте под этим ID"
                    : "Нет сети — трек скачается позже";

            card.Invalidate();
            Content.Invalidate();
        }

        /// <summary>Карточка уровня: публикация, монетки, прогресс и комментарии.</summary>
        void Info()
        {
            var lv = Sel;
            if (lv == null) { _msg = "Выбери уровень"; Content.Invalidate(); return; }
            Nav.Go(new PublicLevelInfoForm(lv, _mode == LevelListMode.My), () => { Reload(); Content.Invalidate(); });
        }

        void Play(bool practice)
        {
            var lv = Sel;
            if (lv == null) return;
            MusicPlayer.Stop();
            Nav.Go(new GameForm(lv.DeepClone(), practice));
            Reload();
        }

        void NewLevel()
        {
            var lv = new LevelData();
            lv.Settings.Name = "Уровень " + (LocalStore.MyLevels().Count + 1);
            lv.Settings.Author = AppConfig.Settings.UserName;
            LocalStore.SaveMyLevel(lv);
            Nav.Go(new EditorForm(lv));
            Reload();
        }

        void Edit()
        {
            var lv = Sel;
            if (lv == null) return;
            Nav.Go(new EditorForm(lv));
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
                GdDialog.Info(this, "Копирование запрещено", "Автор закрыл копирование этого уровня.");
                return;
            }
            if (st.CopyMode == 1)
            {
                string pass = GdDialog.Prompt(this, "Копирование", "Уровень копируется по паролю. Введи пароль автора:", "", true);
                if (pass == null) return;
                if (!string.Equals(pass.Trim(), st.CopyPassword, StringComparison.Ordinal))
                {
                    GdDialog.Info(this, "Копирование", "Неверный пароль.");
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
            GdDialog.Info(this, "Готово", "Скопировано в «Мои уровни».");
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
            if (!AppConfig.LoggedIn) { GdDialog.Info(this, "GEO DASH", "Сначала войди в аккаунт."); return; }
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
            if (!GdDialog.Confirm(this, "Удаление", "Удалить «" + lv.Settings.Name + "»?")) return;
            if (_mode == LevelListMode.My) LocalStore.DeleteMyLevel(lv);
            else LocalStore.DeleteSaved(lv.Settings.ServerId);
            Reload();
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
