using GEO_DASH.Audio;
using GEO_DASH.Core;
using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    /// <summary>MY LEVELS — список уровней редактора: листаем карточки, создаём, правим, заливаем.</summary>
    public class MyLevelsForm : GdForm
    {
        readonly bool _copied;
        List<LevelData> _levels = new();
        int _index;
        string _msg = "";
        GdButton _bPrev, _bNext;

        public MyLevelsForm(bool copied = false)
        {
            _copied = copied;
            Text = "GEO DASH — " + (copied ? "Скопированные уровни" : "Мои уровни");
            ClientSize = new Size(1060, 700);
            BgA = copied ? Color.FromArgb(150, 90, 235) : Color.FromArgb(235, 130, 20);
            BgId = copied ? 11 : 5; GroundId = copied ? 2 : 4;

            _bPrev = Add("", GdIcon.Back, 26, 330, 64, 64, Color.FromArgb(150, 150, 185), Color.FromArgb(60, 62, 100),
                (s, e) => Move(-1));
            _bNext = Add("", GdIcon.Play, ClientSize.Width - 90, 330, 64, 64, Color.FromArgb(150, 150, 185), Color.FromArgb(60, 62, 100),
                (s, e) => Move(1));

            int y = ClientSize.Height - 190;
            int bw = 230, gap = 16;
            int x0 = ClientSize.Width / 2 - (bw * 4 + gap * 3) / 2;

            Add("СОЗДАТЬ УРОВЕНЬ", GdIcon.Plus, x0, y, bw, 58,
                Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100), (s, e) => Create());
            Add("РЕДАКТИРОВАТЬ", GdIcon.Edit, x0 + (bw + gap), y, bw, 58,
                Color.FromArgb(255, 180, 70), Color.FromArgb(175, 95, 10), (s, e) => Edit());
            Add("ИГРАТЬ", GdIcon.Play, x0 + (bw + gap) * 2, y, bw, 58,
                Color.FromArgb(120, 220, 255), Color.FromArgb(20, 110, 180), (s, e) => Play(false));
            Add("ТРЕНИРОВКА", GdIcon.Check, x0 + (bw + gap) * 3, y, bw, 58,
                Color.FromArgb(160, 190, 255), Color.FromArgb(60, 80, 170), (s, e) => Play(true));

            y += 68;
            Add("НАСТРОЙКИ УРОВНЯ", GdIcon.Settings, x0, y, bw, 54,
                Color.FromArgb(170, 175, 210), Color.FromArgb(70, 76, 125), (s, e) => Settings());
            Add("КОПИЯ", GdIcon.Copy, x0 + (bw + gap), y, bw, 54,
                Color.FromArgb(200, 170, 255), Color.FromArgb(100, 60, 180), (s, e) => Copy());
            Add("ОПУБЛИКОВАТЬ", GdIcon.Upload, x0 + (bw + gap) * 2, y, bw, 54,
                Color.FromArgb(255, 210, 90), Color.FromArgb(170, 120, 10), async (s, e) => await Upload());
            Add("УДАЛИТЬ", GdIcon.Trash, x0 + (bw + gap) * 3, y, bw, 54,
                Color.FromArgb(240, 120, 120), Color.FromArgb(150, 40, 50), (s, e) => Delete());

            Add("НАЗАД", GdIcon.Back, 24, ClientSize.Height - 64, 150, 48,
                Color.FromArgb(160, 160, 190), Color.FromArgb(70, 70, 110), (s, e) => Close());

            Load += (s, e) => Reload();
        }

        void Reload()
        {
            _levels = LocalStore.MyLevels(_copied);
            if (_index >= _levels.Count) _index = Math.Max(0, _levels.Count - 1);
            _bPrev.Visible = _bNext.Visible = _levels.Count > 1;
            Content.Invalidate();
        }

        LevelData Cur => _levels.Count > 0 && _index < _levels.Count ? _levels[_index] : null;

        void Move(int d)
        {
            if (_levels.Count == 0) return;
            Sfx.Play("click");
            _index = (_index + d + _levels.Count) % _levels.Count;
            Content.Invalidate();
        }

        void Create()
        {
            var lv = new LevelData();
            lv.Settings.Name = "Уровень " + (_levels.Count + 1);
            lv.Settings.IsCopy = _copied;
            lv.Settings.Author = AppConfig.Settings.UserName;
            LocalStore.SaveMyLevel(lv);
            Reload();
            _index = _levels.FindIndex(l => l.Settings.LocalId == lv.Settings.LocalId);
            if (_index < 0) _index = 0;
            using (var f = new EditorForm(lv)) f.ShowDialog(this);
            Reload();
        }

        void Edit()
        {
            var lv = Cur;
            if (lv == null) { _msg = "Сначала создай уровень"; Content.Invalidate(); return; }
            using (var f = new EditorForm(lv)) f.ShowDialog(this);
            Reload();
        }

        void Play(bool practice)
        {
            var lv = Cur;
            if (lv == null) return;
            MusicPlayer.Stop();
            using (var f = new GameForm(lv.DeepClone(), practice)) f.ShowDialog(this);
            Reload();
        }

        void Settings()
        {
            var lv = Cur;
            if (lv == null) return;
            using var f = new LevelSettingsForm(lv.Settings);
            if (f.ShowDialog(this) == DialogResult.OK)
            {
                LocalStore.SaveMyLevel(lv);
                Reload();
            }
        }

        void Copy()
        {
            var lv = Cur;
            if (lv == null) return;
            LocalStore.CopyLevel(lv);
            _msg = "Копия создана";
            Reload();
        }

        void Delete()
        {
            var lv = Cur;
            if (lv == null) return;
            if (!GdDialog.Confirm(this, "My Levels", $"Удалить «{lv.Settings.Name}»?")) return;
            LocalStore.DeleteMyLevel(lv);
            Reload();
        }

        async Task Upload()
        {
            var lv = Cur;
            if (lv == null) return;
            if (!AppConfig.LoggedIn) { _msg = "Сначала войди в аккаунт"; Content.Invalidate(); return; }
            _msg = "Заливаю на сервер…"; Content.Invalidate();
            var r = await ApiClient.UploadLevelAsync(lv, lv.Settings.ServerId, lv.Settings.Password);
            if (r.Ok && r.Data != null)
            {
                lv.Settings.ServerId = r.Data.Id;
                lv.Settings.Version++;
                LocalStore.SaveMyLevel(lv);
                _msg = $"Готово! ID уровня на сервере: #{r.Data.Id}";
            }
            else _msg = r.Error;
            Reload();
        }

        protected override void OnRender(Graphics g)
        {
                        Header(g, _copied ? "СКОПИРОВАННЫЕ" : "МОИ УРОВНИ",
                _copied ? "копии чужих и своих уровней — правь и публикуй как свой"
                        : "уровни редактора — хранятся в assets/levels");

            var card = new RectangleF(ClientSize.Width / 2f - 360, 120, 720, 300);
            Gd.Plate(g, card, Color.FromArgb(52, 42, 86), Color.FromArgb(24, 20, 52), 22f, 4f);

            var lv = Cur;
            if (lv == null)
            {
                using var f0 = Gd.Bold(13f);
                Gd.Outlined(g, _copied
                        ? "Копий пока нет — скопируй уровень из SAVED или из своих"
                        : "Уровней пока нет — нажми «СОЗДАТЬ УРОВЕНЬ»",
                    f0, card, Gd.Gold, thick: 2.6f);
                return;
            }

            var st = lv.Settings;
            var pr = LocalStore.GetProgress(lv);

            var face = new RectangleF(card.X + 34, card.Y + 70, 110, 110);
            Gd.DrawFace(g, face, st.Difficulty);
            using (var f = Gd.Bold(11f))
                Gd.Outlined(g, Difficulties.Label(st.Difficulty), f,
                    new RectangleF(face.X - 24, face.Bottom + 8, face.Width + 48, 22),
                    Difficulties.Tint(st.Difficulty), thick: 2.4f);

            using (var f = Gd.Title(26f))
                Gd.Outlined(g, st.Name, f, new RectangleF(card.X + 175, card.Y + 26, 520, 42),
                    Gd.Paper, StringAlignment.Near, thick: 4f);

            using (var f = Gd.Bold(11f))
            {
                string[] lines =
                {
                    "объектов: " + lv.Objects.Count + "   длина: " + (int)(st.LengthUnits / 30) + " клеток",
                    "режим старта: " + Modes.Label(st.StartMode) + "   скорость: " + Speeds.Label(st.StartSpeed),
                    "музыка: " + (st.SongId == 0 ? "нет" : st.SongId < 0 ? "встроенная — " + st.SongName : "#" + st.SongId + " " + st.SongName),
                    "копирование: " + st.CopyMode switch
                    {
                        0 => "закрыто",
                        1 => "по паролю",
                        _ => "свободное"
                    },
                    st.ServerId > 0 ? "опубликован: #" + st.ServerId + " (версия " + st.Version + ")" : "ещё не опубликован",
                    st.IsCopy ? "копия уровня #" + st.CopiedFrom + (string.IsNullOrEmpty(st.CopiedAuthor) ? "" : " · автор оригинала: " + st.CopiedAuthor) : "свой уровень",
                    $"лучший результат: {pr.Best:0}%   попыток: {pr.Attempts}"
                };
                float y = card.Y + 76;
                foreach (var s in lines)
                {
                    Gd.Outlined(g, s, f, new RectangleF(card.X + 177, y, 520, 22),
                        Color.FromArgb(225, 210, 225, 255), StringAlignment.Near, thick: 2.2f);
                    y += 26;
                }
            }

            // индикатор позиции
            using (var f = Gd.Bold(11f))
                Gd.Outlined(g, $"{_index + 1} / {_levels.Count}", f,
                    new RectangleF(card.X, card.Bottom + 10, card.Width, 24), Gd.Neon, thick: 2.2f);

            if (!string.IsNullOrEmpty(_msg))
                using (var f = Gd.Bold(10f))
                    Gd.Outlined(g, _msg, f, new RectangleF(24, ClientSize.Height - 36, 700, 22),
                        Gd.Gold, StringAlignment.Near, thick: 2.2f);
        }
    }
}
