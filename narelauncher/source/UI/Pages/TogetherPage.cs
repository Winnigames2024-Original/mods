using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI.Pages
{
    /// <summary>
    /// «Вместе» — кто прямо сейчас открыл свой мир. Видно версию, загрузчик
    /// и моды хозяина; по кнопке лаунчер доносит недостающие моды в нужную
    /// сборку и заходит на мир.
    ///
    /// Свой мир открывается не здесь, а в игре: мод делает это сам и
    /// объявляет комнату — он один знает, какой мир открыт и на каком порту.
    /// </summary>
    public class TogetherPage : PageBase
    {
        private readonly VirtualList _rooms = new VirtualList
        {
            ItemHeight = 72,
            EmptyText = "Открытых миров нет — пока никто не позвал"
        };

        private readonly VirtualList _mods = new VirtualList
        {
            ItemHeight = 34,
            EmptyText = "Выберите мир слева"
        };

        private readonly NareButton _play = new NareButton
        { Text = "Играть", Style = BtnStyle.Accent, Icon = Ico.Play, Size = new Size(176, 42) };

        private readonly NareButton _refresh = new NareButton
        { Text = "Обновить", Style = BtnStyle.Ghost, Size = new Size(140, 42) };

        private readonly NareButton _mine = new NareButton
        { Text = "Как открыть свой", Style = BtnStyle.Ghost, Size = new Size(196, 42) };

        private readonly NareProgress _progress = new NareProgress();
        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);
        private readonly NareLabel _server = new NareLabel("", 8.6f, FontStyle.Regular, Theme.Dim);

        /// <summary>Кнопки выбора сборного пункта: их столько, сколько адресов в настройках.</summary>
        private readonly List<NareButton> _hosts = new List<NareButton>();

        private readonly System.Windows.Forms.Timer _timer =
            new System.Windows.Forms.Timer { Interval = 5000 };

        private readonly List<Room> _list = new List<Room>();
        private CancellationTokenSource _cts;
        private bool _busy;

        private Room Current { get { return _rooms.SelectedItem as Room; } }

        public TogetherPage()
        {
            _rooms.Renderer = DrawRoom;
            _rooms.SelectionChanged += delegate { ShowRoom(); };
            _rooms.ItemActivated += delegate { Play(); };
            Controls.Add(_rooms);

            _mods.Renderer = DrawMod;
            Controls.Add(_mods);

            _play.Click += delegate { Play(); };
            _refresh.Click += async delegate { await Load(true); };
            _mine.Click += delegate { HowTo(); };

            Controls.Add(_play);
            Controls.Add(_refresh);
            Controls.Add(_mine);
            Controls.Add(_progress);
            Controls.Add(_status);
            Controls.Add(_server);

            _timer.Tick += async delegate { if (Visible && !_busy) await Load(false); };

            VisibleChanged += delegate
            {
                if (Visible) _timer.Start(); else _timer.Stop();
            };

            BuildHosts();
            Layout1();
        }

        /// <summary>Пересобираем кнопки серверов: список мог измениться в настройках.</summary>
        private void BuildHosts()
        {
            foreach (var old in _hosts) { Controls.Remove(old); old.Dispose(); }
            _hosts.Clear();

            var known = NareNet.Known();
            if (known.Count < 2) return;            // один адрес — выбирать нечего

            string active = NareNet.Where;

            foreach (var one in known)
            {
                string host = one;
                var button = new NareButton
                {
                    Text = one,
                    Style = one == active ? BtnStyle.Accent : BtnStyle.Ghost,
                    Size = new Size(Math.Max(150, 20 + one.Length * 8), 28),
                    Radius = 8,
                    Font = Theme.F(8.6f, FontStyle.Bold)
                };

                button.Click += async delegate
                {
                    if (NareNet.Where == host) return;

                    NareNet.Use(host);
                    BuildHosts();
                    Layout1();
                    _server.Text = "Сборный пункт: " + NareNet.Where;
                    await Load(true);
                };

                _hosts.Add(button);
                Controls.Add(button);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Dispose();
                if (_cts != null) _cts.Cancel();
            }
            base.Dispose(disposing);
        }

        public override async void PageShown()
        {
            BuildHosts();
            Layout1();
            _server.Text = _hosts.Count > 1 ? "Сборные пункты:" : "Сборный пункт: " + NareNet.Where;
            await Load(true);
        }

        // ------------------------------------------------------------- список

        private async Task Load(bool loud)
        {
            if (_busy) return;

            if (_cts != null) _cts.Cancel();
            _cts = new CancellationTokenSource();

            if (loud) Say("Спрашиваю сборный пункт…", Theme.Muted);

            try
            {
                var found = await NareNet.RoomsAsync(_cts.Token);

                string keep = Current == null ? null : Current.Id;

                _list.Clear();
                _list.AddRange(found);

                var items = new List<object>();
                foreach (var room in _list) items.Add(room);
                _rooms.SetItems(items);

                if (keep != null)
                    for (int i = 0; i < _list.Count; i++)
                        if (_list[i].Id == keep) { _rooms.SelectedIndex = i; break; }

                if (_rooms.SelectedIndex < 0 && _list.Count > 0) _rooms.SelectedIndex = 0;

                ShowRoom();

                if (loud || _list.Count > 0)
                    Say(_list.Count == 0
                        ? "Пока никто не открыл мир. Список обновляется сам."
                        : "Открытых миров: " + _list.Count, Theme.Muted);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _rooms.EmptyText = "Сборный пункт не отвечает";
                Say("Сборный пункт " + NareNet.Where + " не отвечает: " + ex.Message, Theme.Bad);
                Log.Warn("NareNet не ответил: " + ex.Message);
            }

            Buttons();
        }

        private void ShowRoom()
        {
            var room = Current;
            var items = new List<object>();

            if (room != null)
                foreach (var mod in room.Mods) items.Add(mod);

            _mods.EmptyText = room == null
                ? "Выберите мир слева"
                : "Хозяин не передал список модов";

            _mods.SetItems(items);
            Buttons();
            Invalidate();
        }

        private void Buttons()
        {
            var room = Current;

            // кнопку не гасим: пусть нажмётся и скажет, почему пока нельзя —
            // потухшая кнопка без объяснений хуже отказа словами
            _play.Enabled = !_busy && room != null;
            _refresh.Enabled = !_busy;
            _play.Invalidate();
            _refresh.Invalidate();
        }

        private void Say(string text, Color color)
        {
            _status.Text = text ?? "";
            _status.ForeColor = color;
        }

        // ------------------------------------------------------------- заход

        private async void Play()
        {
            var room = Current;
            if (room == null || _busy) return;

            if (NareAuth.Last != null && NareAuth.Last.BlockedTogether)
            {
                Say("Администратор запретил вам функцию «Вместе».", Theme.Bad);
                return;
            }

            if (!room.Joinable)
            {
                Say(room.Reachable == null
                    ? "Хозяин только что открыл мир — сервер ещё проверяет порт. Пара секунд, и кнопка заработает."
                    : "У хозяина закрыт порт, а туннель не поднялся. Пусть он в игре закроет и снова откроет мир.",
                    Theme.Warn);

                return;
            }

            if (AccountStore.Selected == null)
            {
                Host.ManageAccounts();
                if (AccountStore.Selected == null) return;
            }

            var request = new PlayLink.Request
            {
                Version = room.Version,
                Loader = string.IsNullOrEmpty(room.Loader) ? "vanilla" : room.Loader,
                Server = room.Address
            };

            var build = HomePage.FindInstalled(request);

            _busy = true;
            Buttons();
            _progress.SetIndeterminate(true);

            try
            {
                if (build != null)
                {
                    // сборка есть — доносим моды сами и сразу заходим
                    Say("Готовлю сборку " + build.Id + "…", Theme.Muted);

                    var missing = await NareNet.DeliverAsync(room, build.Id,
                        text => Say(text, Theme.Muted), CancellationToken.None);

                    if (!Warn(missing)) return;

                    Settings.Current.SelectedVersion = build.Id;
                    Settings.Save();
                }
                else
                {
                    // сборки нет: её поставит сам лаунчер, а моды донесём
                    // крючком, когда она будет готова
                    Say("Нужна сборка " + room.Version + " " + request.Loader + " — ставлю её…", Theme.Muted);

                    var roomCopy = room;
                    Host.Home.BeforeLaunch = async (id, progress, ct) =>
                    {
                        var missing = await NareNet.DeliverAsync(roomCopy, id, null, ct);
                        foreach (var one in missing)
                            Log.Warn("Мод не достался: " + one.Mod.Title + " — " + one.Why);
                    };
                }

                Say("Захожу к " + room.Nick + " на " + room.Address + "…", Theme.Muted);
                Host.Home.PlayFromLink(request);
                Host.Navigate(0);
            }
            catch (Exception ex)
            {
                Host.ShowError("К " + room.Nick + " зайти не вышло", ex);
                Say("Не вышло: " + ex.Message, Theme.Bad);
            }
            finally
            {
                _progress.SetIndeterminate(false);
                _busy = false;
                Buttons();
            }
        }

        /// <summary>Спрашиваем, идти ли без модов, которых не нашлось нигде.</summary>
        private bool Warn(List<NareNet.Missing> missing)
        {
            if (missing == null || missing.Count == 0) return true;

            var text = new System.Text.StringBuilder();
            text.Append(missing.Count == 1
                ? "Один мод хозяина достать не удалось:"
                : "Моды хозяина, которых не удалось достать (" + missing.Count + "):");

            int shown = 0;
            foreach (var one in missing)
            {
                if (shown++ >= 6) { text.Append("\n…и ещё " + (missing.Count - 6)); break; }
                text.Append("\n\n• " + one.Mod.Title + "\n  " + one.Why);
            }

            text.Append("\n\nБез них игра, скорее всего, не пустит на мир.");
            text.Append("\nПопросите хозяина нажать «Выложить мои моды» в настройках мода.");
            text.Append("\n\nВсё равно попробовать зайти?");

            var answer = MessageBox.Show(FindForm(), text.ToString(),
                "Не все моды нашлись", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (answer == DialogResult.Yes) return true;

            Say("Отменено: не хватает модов хозяина.", Theme.Warn);
            return false;
        }

        private void HowTo()
        {
            MessageBox.Show(FindForm(),
                "Свой мир открывается из игры, а не отсюда — так лаунчер не угадывает,\n" +
                "какой мир вы играете и на каком порту.\n\n" +
                "1. Зайдите в свой одиночный мир.\n" +
                "2. Правый Shift — настройки мода NareVisuals.\n" +
                "3. Вкладка «Вместе» → «Открыть мир для друзей».\n\n" +
                "Мод откроет мир в multiplayer, объявит его здесь и выложит ваши моды,\n" +
                "чтобы гости скачали ровно такие же. Если у вас закрыт порт, он поднимет\n" +
                "туннель через наш сервер — тогда гости пойдут через него, а ваш адрес\n" +
                "им виден не будет.",
                "Как открыть свой мир", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // --------------------------------------------------------- рисование

        private void DrawRoom(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var room = item as Room;
            if (room == null) return;

            var box = Rectangle.Inflate(r, -4, -3);
            if (box.Width <= 0 || box.Height <= 0) return;

            if (selected) Theme.FillRound(g, box, 10, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 10, Color.FromArgb(16, 255, 255, 255));
            if (selected) Theme.DrawRound(g, box, 10, Color.FromArgb(120, Theme.Accent));

            var dot = new Rectangle(box.X + 12, box.Y + box.Height / 2 - 5, 10, 10);
            using (var b = new SolidBrush(room.Joinable ? Theme.Good : Theme.Warn))
                g.FillEllipse(b, dot);

            Theme.Text1(g, room.Nick, Theme.F(11f, FontStyle.Bold), Theme.Text,
                new Rectangle(dot.Right + 10, box.Y + 8, Math.Max(60, box.Width - 220), 20));

            string what = "Minecraft " + room.Version;
            if (!string.IsNullOrEmpty(room.Loader) && room.Loader != "vanilla")
                what += " · " + room.Loader;
            if (room.Mods.Count > 0) what += " · модов " + room.Mods.Count;
            if (!string.IsNullOrEmpty(room.World)) what += " · «" + room.World + "»";

            Theme.Text1(g, what, Theme.F(8.8f), Theme.Muted,
                new Rectangle(dot.Right + 10, box.Y + 28, Math.Max(60, box.Width - 180), 18));

            Theme.Text1(g, room.Address + "   " + room.Where, Theme.F(8.2f),
                room.Joinable ? Theme.Dim : Theme.Warn,
                new Rectangle(dot.Right + 10, box.Y + 46, Math.Max(60, box.Width - 180), 16));

            string players = room.Max > 0 ? room.Players + " / " + room.Max : room.Players.ToString();
            Theme.Text1(g, players, Theme.F(9.4f, FontStyle.Bold), Theme.Text,
                new Rectangle(box.Right - 90, box.Y + 10, 78, 18),
                StringAlignment.Far, StringAlignment.Center);

            Theme.Text1(g, room.Age <= 25 ? "сейчас" : room.Age + " с назад", Theme.F(8f), Theme.Dim,
                new Rectangle(box.Right - 90, box.Y + 32, 78, 16),
                StringAlignment.Far, StringAlignment.Center);
        }

        private void DrawMod(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var mod = item as RoomMod;
            if (mod == null) return;

            var box = Rectangle.Inflate(r, -4, -1);
            if (box.Width <= 0 || box.Height <= 0) return;

            if (hover) Theme.FillRound(g, box, 7, Color.FromArgb(14, 255, 255, 255));

            string title = mod.Title + (mod.Version.Length > 0 ? "  " + mod.Version : "");

            Theme.Text1(g, title, Theme.F(9f), mod.Need ? Theme.Text : Theme.Dim,
                new Rectangle(box.X + 10, box.Y, Math.Max(40, box.Width - 110), box.Height));

            string mark;
            Color color;

            if (!mod.Need) { mark = "не нужен"; color = Theme.Dim; }
            else if (mod.Sha1.Length != 40) { mark = "без sha1"; color = Theme.Warn; }
            else { mark = "доставлю"; color = Theme.Accent; }

            Theme.Text1(g, mark, Theme.F(8.2f, FontStyle.Bold), color,
                new Rectangle(box.Right - 100, box.Y, 90, box.Height),
                StringAlignment.Far, StringAlignment.Center);
        }

        // ----------------------------------------------------------- вёрстка

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Layout1();
        }

        private void Layout1()
        {
            if (_rooms == null) return;

            int pad = 28;
            int top = 96;
            int bottom = 74;

            int rightW = Math.Min(360, Math.Max(220, Width / 3));
            int rightX = Math.Max(pad + 240, Width - pad - rightW);

            int listH = Math.Max(80, Height - top - bottom);

            _rooms.SetBounds(pad, top, Math.Max(180, rightX - pad - 20), listH);
            _mods.SetBounds(rightX, top, rightW, listH);

            int by = Height - bottom + 10;
            _play.Location = new Point(pad, by);
            _refresh.Location = new Point(pad + 188, by);
            _mine.Location = new Point(pad + 340, by);

            _progress.SetBounds(pad, Height - 20, Math.Max(50, Width - pad * 2), 6);
            _status.SetBounds(pad + 548, by + 10, Math.Max(100, Width - pad * 2 - 548), 22);

            if (_hosts.Count > 1)
            {
                _server.SetBounds(pad, top - 30, 150, 24);

                int hx = pad + 152;
                foreach (var button in _hosts)
                {
                    button.Location = new Point(hx, top - 32);
                    hx += button.Width + 8;
                }
            }
            else _server.SetBounds(pad, top - 26, 420, 18);

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Theme.Smooth(g);

            Theme.Text1(g, "ВМЕСТЕ", Theme.F(19f, FontStyle.Bold), Theme.Text,
                new Rectangle(28, 26, 400, 32));

            Theme.Text1(g, "Кто открыл свой мир прямо сейчас", Theme.F(9.5f), Theme.Muted,
                new Rectangle(190, 34, 460, 22));

            var room = Current;
            if (room != null)
                Theme.Text1(g, "МОДЫ У " + room.Nick.ToUpperInvariant(), Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                    new Rectangle(_mods.Left, _mods.Top - 20, _mods.Width, 16));
            else
                Theme.Text1(g, "МОДЫ ХОЗЯИНА", Theme.F(8.5f, FontStyle.Bold), Theme.Dim,
                    new Rectangle(_mods.Left, _mods.Top - 20, _mods.Width, 16));
        }
    }
}
