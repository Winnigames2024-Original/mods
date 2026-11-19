using GEO_DASH.Audio;
using GEO_DASH.Core;
using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    /// <summary>
    /// Карточка уровня, как в игре: лицо сложности, звёзды, монетки, длина и кнопки.
    /// Отсюда уровень можно запустить, опубликовать и снять с публикации.
    /// Опубликовать разрешено только пройденный уровень со всеми собранными монетками.
    /// </summary>
    public class PublicLevelInfoForm : GdForm
    {
        readonly LevelData _lv;
        readonly bool _mine;
        GdButton _publish, _unpublish;
        string _msg = "";
        bool _busy;

        /// <param name="lv">уровень (свой или скачанный)</param>
        /// <param name="mine">уровень мой — показываем публикацию</param>
        public PublicLevelInfoForm(LevelData lv, bool mine = false)
        {
            _lv = lv;
            _mine = mine || string.Equals(lv.Settings.Author, AppConfig.Settings.UserName,
                                          StringComparison.OrdinalIgnoreCase);

            Text = "GEO DASH — " + lv.Settings.Name;
            ClientSize = new Size(980, 660);
            BgA = Color.FromArgb(40, 70, 220); BgId = 7; GroundId = 3;

            int bx = 60, by = 470, bw = 270, bh = 60;

            Add("ИГРАТЬ", GdIcon.Play, bx, by, bw, bh,
                Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100), (s, e) => Play(false));
            Add("ТРЕНИРОВКА", GdIcon.Check, bx + bw + 16, by, bw, bh,
                Color.FromArgb(120, 190, 255), Color.FromArgb(30, 90, 190), (s, e) => Play(true));
            Add("КОММЕНТАРИИ", GdIcon.Online, bx + (bw + 16) * 2, by, bw, bh,
                Color.FromArgb(200, 170, 255), Color.FromArgb(100, 60, 180), (s, e) => Comments());

            by += bh + 14;
            if (_mine)
            {
                _publish = Add(Published ? "ОБНОВИТЬ НА СЕРВЕРЕ" : "ОПУБЛИКОВАТЬ", GdIcon.Upload,
                    bx, by, bw, 54, Color.FromArgb(255, 210, 90), Color.FromArgb(170, 120, 10),
                    async (s, e) => await Publish());

                _unpublish = Add("СНЯТЬ С ПУБЛИКАЦИИ", GdIcon.Cross, bx + bw + 16, by, bw, 54,
                    Color.FromArgb(230, 130, 130), Color.FromArgb(150, 40, 50),
                    async (s, e) => await Unpublish());
                _unpublish.Visible = Published;

                Add("РЕДАКТОР", GdIcon.Edit, bx + (bw + 16) * 2, by, bw, 54,
                    Color.FromArgb(255, 170, 60), Color.FromArgb(180, 90, 10),
                    (s, e) => Nav.Go(new EditorForm(_lv), () => Content.Invalidate()));
            }
            else
            {
                Add("АВТОР", GdIcon.Profile, bx, by, bw, 54,
                    Color.FromArgb(90, 190, 255), Color.FromArgb(20, 90, 170),
                    (s, e) => Nav.Go(new ProfileForm(_lv.Settings.Author)));
            }

            Add("НАЗАД", GdIcon.Back, 24, 24, 150, 50,
                Color.FromArgb(160, 160, 190), Color.FromArgb(70, 70, 110), (s, e) => CloseScreen());

            UpdateHint();
        }

        bool Published => _lv.Settings.ServerId > 0;
        int CoinsTotal => _lv.Objects.Count(o => o.TypeId == 80);

        /// <summary>Можно ли публиковать: уровень пройден и монетки собраны все.</summary>
        bool CanPublish(out string why)
        {
            var p = LocalStore.GetProgress(_lv);
            if (!p.Completed)
            {
                why = "Пройди свой уровень целиком — тогда его можно будет опубликовать";
                return false;
            }
            if (CoinsTotal > 0 && p.Coins < CoinsTotal)
            {
                why = $"Собери все монетки: {p.Coins} из {CoinsTotal}";
                return false;
            }
            why = "";
            return true;
        }

        void UpdateHint()
        {
            if (!_mine) return;
            if (_busy) return;
            _msg = CanPublish(out var why) ? (Published ? "Уровень опубликован" : "Уровень готов к публикации") : why;
            if (_publish != null) _publish.Enabled = CanPublish(out _);
            Content.Invalidate();
        }

        void Play(bool practice)
        {
            MusicPlayer.Stop();
            Nav.Go(new GameForm(_lv.DeepClone(), practice), () => { UpdateHint(); Content.Invalidate(); });
        }

        void Comments()
        {
            if (!Published) { _msg = "Комментарии появятся после публикации"; Content.Invalidate(); return; }
            Nav.Go(new CommentsForm(_lv.Settings.ServerId, _lv.Settings.Name));
        }

        async Task Publish()
        {
            if (!CanPublish(out var why)) { _msg = why; Content.Invalidate(); return; }

            _busy = true; _msg = "Отправляю на сервер…"; Content.Invalidate();
            var r = await ApiClient.UploadLevelAsync(_lv, _lv.Settings.ServerId, _lv.Settings.Password);
            _busy = false;

            if (r.Ok && r.Data != null)
            {
                _lv.Settings.ServerId = r.Data.Id;
                LocalStore.SaveMyLevel(_lv);
                _publish.Text = "ОБНОВИТЬ НА СЕРВЕРЕ";
                _unpublish.Visible = true;
                _msg = "Опубликовано: уровень #" + r.Data.Id;
            }
            else _msg = string.IsNullOrEmpty(r.Error) ? "Не получилось опубликовать" : r.Error;
            Content.Invalidate();
        }

        async Task Unpublish()
        {
            if (!Published) return;
            if (!GdDialog.Confirm(this, "Снять с публикации", "Уровень удалится с сервера. Снимаем?")) return;

            _busy = true; _msg = "Снимаю с публикации…"; Content.Invalidate();
            var r = await ApiClient.UnpublishAsync(_lv.Settings.ServerId);
            _busy = false;

            if (r.Ok)
            {
                _lv.Settings.ServerId = 0;
                LocalStore.SaveMyLevel(_lv);
                _unpublish.Visible = false;
                _publish.Text = "ОПУБЛИКОВАТЬ";
                _msg = "Уровень снят с публикации";
            }
            else _msg = string.IsNullOrEmpty(r.Error) ? "Не получилось снять" : r.Error;
            Content.Invalidate();
        }

        protected override void OnRender(Graphics g)
        {
            var st = _lv.Settings;
            var pr = LocalStore.GetProgress(_lv);

            Header(g, st.Name.ToUpperInvariant(), "автор: " + st.Author);

            var card = new RectangleF(60, 112, ClientSize.Width - 120, 330);
            Gd.Plate(g, card, Color.FromArgb(44, 38, 92), Color.FromArgb(22, 18, 54), 22f, 4f);

            // лицо сложности и звёзды
            var face = new RectangleF(card.X + 34, card.Y + 58, 130, 130);
            Gd.DrawFace(g, face, st.Difficulty);
            using (var f = Gd.Bold(12f))
                Gd.Outlined(g, Difficulties.Label(st.Difficulty), f,
                    new RectangleF(face.X - 24, face.Bottom + 8, face.Width + 48, 24),
                    Difficulties.Tint(st.Difficulty), thick: 2.6f);

            int stars = Difficulties.Stars(st.Difficulty);
            Gd.DrawStar(g, new RectangleF(face.X + 18, face.Y - 38, 28, 28), Gd.Gold);
            using (var f = Gd.Bold(12f))
                Gd.Outlined(g, stars.ToString(), f, new RectangleF(face.X + 50, face.Y - 38, 44, 28), Gd.Gold, thick: 2.4f);

            float tx = card.X + 200, tw = card.Width - 230;
            using (var f = Gd.Bold(11f))
            {
                string id = Published ? "#" + st.ServerId : "не опубликован";
                Gd.Outlined(g, id, f, new RectangleF(tx, card.Y + 22, tw, 22),
                    Published ? Gd.Lime : Gd.Gold, StringAlignment.Near, thick: 2.2f);
                Gd.Outlined(g, "музыка: " + (string.IsNullOrEmpty(st.SongName) ? "без музыки" : st.SongName), f,
                    new RectangleF(tx, card.Y + 50, tw, 22), Gd.Neon, StringAlignment.Near, thick: 2.2f);
                Gd.Outlined(g, $"длина: {_lv.Minutes():0.0} мин   объектов: {_lv.Objects.Count}", f,
                    new RectangleF(tx, card.Y + 78, tw, 22), Color.FromArgb(220, 200, 220, 255), StringAlignment.Near, thick: 2.2f);
                if (!string.IsNullOrWhiteSpace(st.Description))
                    Gd.Outlined(g, st.Description, f, new RectangleF(tx, card.Y + 106, tw, 22),
                        Color.FromArgb(210, 190, 210, 245), StringAlignment.Near, thick: 2.2f);
            }

            // прогресс
            var bar = new RectangleF(tx, card.Y + 146, tw - 10, 26);
            using (var br = new SolidBrush(Color.FromArgb(160, 10, 8, 26))) g.FillRectangle(br, bar);
            using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(bar, Gd.Neon, Gd.Lime,
                       System.Drawing.Drawing2D.LinearGradientMode.Horizontal))
                g.FillRectangle(br, new RectangleF(bar.X, bar.Y, (float)(bar.Width * Math.Clamp(pr.Best / 100.0, 0, 1)), bar.Height));
            using (var pen = new Pen(Gd.Ink, 3f)) g.DrawRectangle(pen, bar.X, bar.Y, bar.Width, bar.Height);
            using (var f = Gd.Bold(11f))
                Gd.Outlined(g, $"{pr.Best:0}%" + (pr.Completed ? "  ПРОЙДЕН" : ""), f, bar, Gd.Paper, thick: 2.4f);

            // монетки уровня
            float cx = tx;
            int total = Math.Max(CoinsTotal, 0);
            for (int i = 1; i <= Math.Min(total, 3); i++)
            {
                Gd.DrawCoin(g, new RectangleF(cx, card.Y + 186, 32, 32), pr.Coins >= i);
                cx += 42;
            }
            using (var f = Gd.Bold(10f))
                Gd.Outlined(g, total == 0 ? "монеток нет" : $"монетки: {pr.Coins} из {total}", f,
                    new RectangleF(cx + 8, card.Y + 190, 260, 24),
                    Color.FromArgb(225, 215, 225, 255), StringAlignment.Near, thick: 2.2f);

            if (!string.IsNullOrEmpty(_msg))
                using (var f = Gd.Bold(11f))
                    Gd.Outlined(g, _msg, f, new RectangleF(40, ClientSize.Height - 66, ClientSize.Width - 80, 30),
                        _busy ? Gd.Paper : Gd.Gold, thick: 2.4f);
        }
    }
}
