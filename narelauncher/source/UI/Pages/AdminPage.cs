using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI.Pages
{
    /// <summary>
    /// Панель администратора (только для аккаунта-админа). Всё действует
    /// на сервере, честно и на виду: баны с указанием срока, предупреждения,
    /// объявления, запрет «Вместе», блокировка модов, экстренное обновление.
    /// </summary>
    public class AdminPage : PageBase
    {
        private readonly Panel _scroll = new Panel { AutoScroll = true, Dock = DockStyle.Fill };

        // бан
        private readonly NareDropDown _banNick = new NareDropDown();
        private readonly NareDropDown _banType = new NareDropDown();
        private readonly NareTextBox _banMinutes = new NareTextBox { Placeholder = "Минут (0 или пусто — навсегда)" };
        private readonly NareTextBox _banReason = new NareTextBox { Placeholder = "Причина" };
        private readonly NareButton _ban = new NareButton { Text = "Забанить", Style = BtnStyle.Danger, Size = new Size(140, 38) };
        private readonly NareButton _unban = new NareButton { Text = "Разбанить", Style = BtnStyle.Ghost, Size = new Size(140, 38) };
        private readonly NareButton _chat = new NareButton { Text = "Чат с игроком", Style = BtnStyle.Ghost, Size = new Size(160, 38) };

        // предупреждение
        private readonly NareDropDown _warnNick = new NareDropDown();
        private readonly NareTextBox _warnText = new NareTextBox { Placeholder = "Текст предупреждения" };
        private readonly NareButton _warn = new NareButton { Text = "Предупредить", Style = BtnStyle.Accent, Size = new Size(160, 38) };

        // объявление и команда
        private readonly NareTextBox _broadcast = new NareTextBox { Placeholder = "Объявление всем (пусто — убрать)" };
        private readonly NareButton _broadcastGo = new NareButton { Text = "Объявить", Style = BtnStyle.Accent, Size = new Size(150, 38) };
        private readonly NareTextBox _command = new NareTextBox { Placeholder = "Команда серверу, например say привет" };
        private readonly NareButton _commandGo = new NareButton { Text = "Выполнить", Style = BtnStyle.Ghost, Size = new Size(150, 38) };

        // запрет «Вместе»
        private readonly NareDropDown _togNick = new NareDropDown();
        private readonly NareButton _togOff = new NareButton { Text = "Запретить «Вместе»", Style = BtnStyle.Danger, Size = new Size(190, 38) };
        private readonly NareButton _togOn = new NareButton { Text = "Разрешить", Style = BtnStyle.Ghost, Size = new Size(140, 38) };

        // блок модов
        private readonly NareTextBox _modId = new NareTextBox { Placeholder = "ID мода, например xray" };
        private readonly NareTextBox _modVer = new NareTextBox { Placeholder = "Версия игры или * для всех" };
        private readonly NareButton _modBlock = new NareButton { Text = "Заблокировать мод", Style = BtnStyle.Danger, Size = new Size(190, 38) };
        private readonly NareButton _modUnblock = new NareButton { Text = "Разблокировать", Style = BtnStyle.Ghost, Size = new Size(160, 38) };

        // модерация серверов
        private readonly NareTextBox _srvId = new NareTextBox { Placeholder = "ID заявки сервера" };
        private readonly NareButton _srvOk = new NareButton { Text = "Одобрить сервер", Style = BtnStyle.Success, Size = new Size(180, 38) };
        private readonly NareButton _srvNo = new NareButton { Text = "Отклонить", Style = BtnStyle.Danger, Size = new Size(140, 38) };

        // экстренное обновление
        private readonly NareTextBox _updVer = new NareTextBox { Placeholder = "Версия (подтянется сама)" };
        private readonly NareTextBox _updLog = new NareTextBox { Placeholder = "Что нового (changelog)" };
        private readonly NareButton _updGo = new NareButton { Text = "Объявить обновление", Style = BtnStyle.Accent, Size = new Size(200, 38) };
        private readonly NareButton _updCrit = new NareButton { Text = "Экстренное: закрыть всем через 30с", Style = BtnStyle.Danger, Size = new Size(300, 38) };
        private readonly NareDropDown _updNick = new NareDropDown();
        private readonly NareButton _updOne = new NareButton { Text = "Экстренно у выбранного (30с)", Style = BtnStyle.Danger, Size = new Size(300, 38) };

        // монеты
        private readonly NareDropDown _coinNick = new NareDropDown();
        private readonly NareTextBox _coinAmount = new NareTextBox { Placeholder = "Сколько (можно с минусом)" };
        private readonly NareButton _coinGive = new NareButton { Text = "Выдать", Style = BtnStyle.Success, Size = new Size(150, 38) };
        private readonly NareButton _coinTake = new NareButton { Text = "Забрать", Style = BtnStyle.Danger, Size = new Size(150, 38) };

        // права админа
        private readonly NareDropDown _rightsNick = new NareDropDown();
        private readonly NareDropDown _rolePick = new NareDropDown();
        private readonly NareButton _grantBtn = new NareButton { Text = "Назначить", Style = BtnStyle.Accent, Size = new Size(150, 38) };
        private readonly NareButton _revokeBtn = new NareButton { Text = "Снять права", Style = BtnStyle.Ghost, Size = new Size(160, 38) };

        // игровой ник
        private readonly NareDropDown _setNickDrop = new NareDropDown();
        private readonly NareTextBox _setNickBox = new NareTextBox { Placeholder = "Новый игровой ник" };
        private readonly NareButton _setNickBtn = new NareButton { Text = "Поставить ник", Style = BtnStyle.Accent, Size = new Size(190, 38) };

        // сброс входа
        private readonly NareDropDown _logoutNick = new NareDropDown();
        private readonly NareButton _logoutBtn = new NareButton { Text = "Сбросить вход (выйти со всех устройств)", Style = BtnStyle.Danger, Size = new Size(360, 38) };
        private readonly NareTextBox _passBox = new NareTextBox { Placeholder = "Новый пароль (от 4 знаков)" };
        private readonly NareButton _passBtn = new NareButton { Text = "Поставить новый пароль", Style = BtnStyle.Accent, Size = new Size(240, 38) };

        private readonly NareLabel _statLine = new NareLabel("", 9.5f, FontStyle.Bold, Theme.Muted);

        private readonly NareLabel _title = new NareLabel("АДМИН-ПАНЕЛЬ", 19f, FontStyle.Bold, Theme.Text);
        private readonly NareLabel[] _heads = new NareLabel[12];
        private readonly System.Collections.Generic.List<NareLabel> _caps = new System.Collections.Generic.List<NareLabel>();
        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);
        private readonly NareLabel _usersHead = new NareLabel("ПОЛЬЗОВАТЕЛИ — выберите, чтобы подставить ник", 8.5f, FontStyle.Bold, Theme.Accent);
        private readonly VirtualList _users = new VirtualList { ItemHeight = 40, EmptyText = "Список придёт с сервера" };
        private readonly VirtualList _info = new VirtualList { ItemHeight = 26, EmptyText = "Пусто" };

        private readonly System.Collections.Generic.List<object> _nicks = new System.Collections.Generic.List<object>();

        private string Token { get { var a = AccountStore.Selected; return a == null ? "" : a.Token; } }

        private static string Pick(NareDropDown d) { return d.SelectedItem == null ? "" : d.SelectedItem.ToString(); }

        public AdminPage()
        {
            Controls.Add(_scroll);

            string[] titles = { "БАН ИГРОКА", "ПРЕДУПРЕЖДЕНИЕ", "ОБЪЯВЛЕНИЕ И КОМАНДА СЕРВЕРУ",
                "ЗАПРЕТ «ВМЕСТЕ»", "БЛОКИРОВКА МОДОВ", "МОДЕРАЦИЯ СЕРВЕРОВ", "ОБНОВЛЕНИЕ",
                "МОНЕТЫ", "ПРАВА: АДМИН / МОДЕРАТОР", "ИГРОВОЙ НИК", "СБРОС ВХОДА И ПАРОЛЬ", "" };
            for (int i = 0; i < _heads.Length; i++)
            {
                _heads[i] = new NareLabel(titles[i], 9f, FontStyle.Bold, Theme.Accent);
                _scroll.Controls.Add(_heads[i]);
            }

            // мини-подписи над полями — в том же порядке, как поля в вёрстке
            string[] caps = {
                "Игрок", "Тип бана", "Минуты (для временного)", "Причина",
                "Игрок", "Текст предупреждения",
                "Текст объявления всем", "Команда серверу",
                "Игрок",
                "ID мода (например xray)", "Версия игры или * для всех",
                "ID заявки сервера (из сводки справа)",
                "Версия обновления", "Что нового (changelog)",
                "Игрок для точечного экстренного обновления",
                "Игрок (монеты)", "Сколько монет (можно с минусом)",
                "Игрок (права админа)", "Роль",
                "Игрок (сменить ник)", "Новый игровой ник",
                "Игрок (сбросить вход)", "Новый пароль игроку"
            };
            foreach (var c in caps)
            {
                var label = new NareLabel(c, 8f, FontStyle.Regular, Theme.Dim);
                _caps.Add(label);
                _scroll.Controls.Add(label);
            }
            _scroll.Controls.Add(_title);

            Add(_banNick); Add(_banType); Add(_banMinutes); Add(_banReason); Add(_ban); Add(_unban); Add(_chat);
            _banType.SetItems(new object[] { "Временный (указать минуты)", "Навсегда" });
            _banType.SelectedIndex = 0;
            _banType.SelectedIndexChanged += delegate
            {
                bool temp = _banType.SelectedIndex == 0;
                _banMinutes.Enabled = temp;
                _banMinutes.Invalidate();
            };
            Add(_warnNick); Add(_warnText); Add(_warn);
            Add(_broadcast); Add(_broadcastGo); Add(_command); Add(_commandGo);
            Add(_togNick); Add(_togOff); Add(_togOn);
            Add(_modId); Add(_modVer); Add(_modBlock); Add(_modUnblock);
            Add(_srvId); Add(_srvOk); Add(_srvNo);
            Add(_updVer); Add(_updLog); Add(_updGo); Add(_updCrit);
            Add(_updNick); Add(_updOne);
            Add(_coinNick); Add(_coinAmount); Add(_coinGive); Add(_coinTake);
            Add(_rightsNick); Add(_rolePick); Add(_grantBtn); Add(_revokeBtn);
            Add(_setNickDrop); Add(_setNickBox); Add(_setNickBtn);
            Add(_logoutNick); Add(_logoutBtn);
            Add(_passBox); Add(_passBtn);
            _rolePick.SetItems(new System.Collections.Generic.List<string> { "Админ", "Модератор" }, "Админ");
            _scroll.Controls.Add(_statLine);
            _scroll.Controls.Add(_status);
            _scroll.Controls.Add(_usersHead);
            _scroll.Controls.Add(_users);
            _scroll.Controls.Add(_info);
            _info.Renderer = DrawInfo;
            _users.Renderer = DrawUser;
            _users.SelectionChanged += delegate
            {
                var u = _users.SelectedItem as UserRow;
                if (u == null) return;
                _banNick.SetItems(_nicks, u.Nick);
                _warnNick.SetItems(_nicks, u.Nick);
                _togNick.SetItems(_nicks, u.Nick);
                _updNick.SetItems(_nicks, u.Nick);
                _coinNick.SetItems(_nicks, u.Nick);
                _rightsNick.SetItems(_nicks, u.Nick);
                _setNickDrop.SetItems(_nicks, u.Nick);
                _logoutNick.SetItems(_nicks, u.Nick);
            };

            _ban.Click += async (s, e) =>
            {
                int minutes = _banType.SelectedIndex == 1 ? 0 : ParseInt(_banMinutes.Text);
                if (_banType.SelectedIndex == 0 && minutes <= 0)
                {
                    _status.Text = "Для временного бана укажите минуты (больше 0).";
                    _status.ForeColor = Theme.Warn;
                    return;
                }
                await Do(() => NareAuth.BanAsync(Token, Pick(_banNick), minutes, _banReason.Text.Trim(), Ct()),
                    "Забанен " + Pick(_banNick) + (minutes > 0 ? " на " + minutes + " мин" : " навсегда"));
            };
            _unban.Click += async (s, e) => await Do(() => NareAuth.UnbanAsync(Token, Pick(_banNick), Ct()),
                "Разбанен " + Pick(_banNick));
            _chat.Click += delegate
            {
                string nick = Pick(_banNick);
                if (nick.Length == 0) { _status.Text = "Выберите игрока."; return; }
                using (var form = new ChatForm(Token, nick)) form.ShowDialog(FindForm());
            };

            _warn.Click += async (s, e) => await Do(() => NareAuth.WarnAsync(Token, Pick(_warnNick), _warnText.Text.Trim(), Ct()),
                "Предупреждение отправлено");

            _broadcastGo.Click += async (s, e) => await Do(() => NareAuth.BroadcastAsync(Token, _broadcast.Text.Trim(), Ct()),
                "Объявление обновлено");
            _commandGo.Click += async (s, e) => await Do(() => NareAuth.CommandAsync(Token, _command.Text.Trim(), Ct()),
                "Команда поставлена в очередь серверу");

            _togOff.Click += async (s, e) => await Do(() => NareAuth.TogetherAsync(Token, Pick(_togNick), true, Ct()),
                "«Вместе» запрещено для " + Pick(_togNick));
            _togOn.Click += async (s, e) => await Do(() => NareAuth.TogetherAsync(Token, Pick(_togNick), false, Ct()),
                "«Вместе» снова разрешено");

            _modBlock.Click += async (s, e) => await Do(() => NareAuth.BlockModAsync(Token, _modId.Text.Trim(), _modVer.Text.Trim(), true, Ct()),
                "Мод заблокирован");
            _modUnblock.Click += async (s, e) => await Do(() => NareAuth.BlockModAsync(Token, _modId.Text.Trim(), _modVer.Text.Trim(), false, Ct()),
                "Мод разблокирован");

            _srvOk.Click += async (s, e) => await Do(() => NareAuth.ApproveServerAsync(Token, _srvId.Text.Trim(), 0, Ct()),
                "Сервер одобрен");
            _srvNo.Click += async (s, e) => await Do(() => NareAuth.RejectServerAsync(Token, _srvId.Text.Trim(), "", Ct()),
                "Сервер отклонён");

            _updGo.Click += async (s, e) => await Announce(false);
            _updCrit.Click += async (s, e) =>
            {
                if (!Host.Confirm("Экстренное обновление закроет Minecraft и лаунчер у всех игроков через 30 секунд. Продолжить?"))
                    return;
                await Announce(true);
            };

            _updOne.Click += async (s, e) =>
            {
                string who = Pick(_updNick);
                if (who.Length == 0) { _status.Text = "Выберите игрока для точечного обновления."; return; }
                if (!Host.Confirm("Экстренное обновление закроет Minecraft и лаунчер у игрока " + who + " через 30 секунд. Продолжить?"))
                    return;
                await Announce(true, who);
            };

            _coinGive.Click += async (s, e) => await Coins(+1);
            _coinTake.Click += async (s, e) => await Coins(-1);
            _grantBtn.Click += async (s, e) =>
            {
                string role = Pick(_rolePick) == "Модератор" ? "moderator" : "admin";
                string nick = Pick(_rightsNick);
                await Do(() => NareAuth.GrantAdminAsync(Token, nick, true, Ct(), role),
                    "Назначен (" + (role == "moderator" ? "модератор" : "админ") + "): " + nick);
            };
            _revokeBtn.Click += async (s, e) => await Do(() => NareAuth.GrantAdminAsync(Token, Pick(_rightsNick), false, Ct()),
                "Сняты права: " + Pick(_rightsNick));
            _setNickBtn.Click += async (s, e) =>
            {
                string nick = Pick(_setNickDrop), uname = _setNickBox.Text.Trim();
                if (nick.Length == 0 || uname.Length == 0) { _status.Text = "Выберите игрока и введите ник."; return; }
                await Do(() => NareAuth.AdminSetNickAsync(Token, nick, uname, Ct()), "Ник игрока " + nick + " → " + uname);
            };
            _logoutBtn.Click += async (s, e) =>
            {
                string nick = Pick(_logoutNick);
                if (nick.Length == 0) { _status.Text = "Выберите игрока."; return; }
                if (!Host.Confirm("Сбросить вход у " + nick + " на всех устройствах? Игроку нужно будет войти заново. Помогает, если у него завис лаунчер из-за аккаунта."))
                    return;
                await Do(async () =>
                {
                    int k = await NareAuth.AdminLogoutAsync(Token, nick, Ct());
                    _status.Text = "Сброшен вход у " + nick + " (устройств: " + k + ")";
                }, null);
            };
            _passBtn.Click += async (s, e) =>
            {
                string nick = Pick(_logoutNick);
                string pw = _passBox.Text;
                if (nick.Length == 0) { _status.Text = "Выберите игрока."; return; }
                if (pw.Length < 4) { _status.Text = "Пароль — от 4 знаков."; return; }
                await Do(() => NareAuth.AdminSetPassAsync(Token, nick, pw, Ct()),
                    "Новый пароль поставлен игроку " + nick + ". Старый больше не действует.");
                _passBox.Text = "";
            };

            Layout1();
        }

        private void Add(Control c) { _scroll.Controls.Add(c); }

        private static CancellationToken Ct() { return CancellationToken.None; }

        private static int ParseInt(string text)
        {
            int v;
            return int.TryParse((text ?? "").Trim(), out v) && v > 0 ? v : 0;
        }

        public override async void PageShown()
        {
            Layout1();

            if (string.IsNullOrEmpty(Token))
            {
                _status.Text = "Эта вкладка только для администратора. Войдите в аккаунт-админ.";
                return;
            }

            // подтянем последнюю опубликованную версию в поле обновления
            try
            {
                var latest = await SelfUpdate.LatestAsync(Ct());
                if (_updVer.Text.Trim().Length == 0) _updVer.Text = latest.Version;
            }
            catch { }

            await Reload();
        }

        private async System.Threading.Tasks.Task Coins(int sign)
        {
            string nick = Pick(_coinNick);
            if (nick.Length == 0) { _status.Text = "Выберите игрока."; return; }
            int amt = ParseInt(_coinAmount.Text);
            if (amt <= 0) { _status.Text = "Сколько монет?"; return; }
            long give = sign > 0 ? amt : -amt;
            await Do(async () =>
            {
                long bal = await NareAuth.AdminCoinsAsync(Token, nick, give, Ct());
                _status.Text = nick + ": теперь " + bal + " монет";
            }, null);
        }

        private async System.Threading.Tasks.Task Announce(bool critical, string nick = null)
        {
            string version = _updVer.Text.Trim();
            if (version.Length == 0) { _status.Text = "Укажите версию обновления."; return; }

            string ok = critical
                ? (string.IsNullOrEmpty(nick) ? "Экстренное обновление объявлено всем" : "Экстренное обновление отправлено игроку " + nick)
                : "Обновление объявлено";
            await Do(() => NareAuth.AnnounceUpdateAsync(Token, version, _updLog.Text.Trim(), critical, Ct(), nick), ok);
        }

        private async System.Threading.Tasks.Task Do(Func<System.Threading.Tasks.Task> action, string okText)
        {
            if (string.IsNullOrEmpty(Token)) { _status.Text = "Нет прав администратора."; return; }

            try
            {
                await action();
                if (!string.IsNullOrEmpty(okText)) _status.Text = okText;
                _status.ForeColor = Theme.Good;
                await Reload();
            }
            catch (Exception ex)
            {
                _status.Text = "Ошибка: " + ex.Message;
                _status.ForeColor = Theme.Bad;
            }
        }

        private class UserRow
        {
            public string Nick = "";
            public bool Admin;
            public bool Banned;
            public bool BlockedTogether;
            public long Coins;
            public string Username = "";
        }

        private async System.Threading.Tasks.Task Reload()
        {
            try
            {
                var state = await NareAuth.StateAsync(Token, Ct());

                var users = new System.Collections.Generic.List<object>();
                foreach (var a in state["accounts"].Items)
                    users.Add(new UserRow
                    {
                        Nick = a["nick"].AsString(""),
                        Admin = a["admin"].AsBool(false),
                        Banned = a["banned"].AsBool(false),
                        BlockedTogether = a["blockedTogether"].AsBool(false),
                        Coins = a["coins"].AsLong(0),
                        Username = a["username"].AsString("")
                    });
                _users.SetItems(users);

                _nicks.Clear();
                foreach (var o in users) _nicks.Add(((UserRow)o).Nick);

                string keepBan = Pick(_banNick), keepWarn = Pick(_warnNick), keepTog = Pick(_togNick), keepUpd = Pick(_updNick);
                _banNick.SetItems(_nicks, keepBan.Length > 0 ? keepBan : null);
                _warnNick.SetItems(_nicks, keepWarn.Length > 0 ? keepWarn : null);
                _togNick.SetItems(_nicks, keepTog.Length > 0 ? keepTog : null);
                _updNick.SetItems(_nicks, keepUpd.Length > 0 ? keepUpd : null);
                string keepCoin = Pick(_coinNick), keepRights = Pick(_rightsNick), keepSet = Pick(_setNickDrop);
                _coinNick.SetItems(_nicks, keepCoin.Length > 0 ? keepCoin : null);
                _rightsNick.SetItems(_nicks, keepRights.Length > 0 ? keepRights : null);
                _setNickDrop.SetItems(_nicks, keepSet.Length > 0 ? keepSet : null);
                string keepLogout = Pick(_logoutNick);
                _logoutNick.SetItems(_nicks, keepLogout.Length > 0 ? keepLogout : null);

                var st = state["stats"];
                if (st.IsObject)
                    _statLine.Text = "Аккаунтов: " + st["accounts"].AsLong(0)
                        + "   ·   Банов: " + st["bans"].AsLong(0)
                        + "   ·   Онлайн: " + st["online"].AsLong(0)
                        + "   ·   Заявок: " + st["pendingServers"].AsLong(0)
                        + "   ·   Админов: " + st["admins"].AsLong(0)
                        + "   ·   Чатов: " + st["chats"].AsLong(0);
                var items = new System.Collections.Generic.List<object>();

                foreach (var b in state["bans"].Items)
                    items.Add("БАН  " + b["nick"].AsString("") + "  —  " + b["left"].AsString("") +
                              "  (" + b["reason"].AsString("") + ")");

                foreach (var a in state["appeals"].Items)
                    items.Add("АПЕЛЛЯЦИЯ  " + a["nick"].AsString("") + ":  " + a["text"].AsString(""));

                foreach (var t in state["together"].Items)
                    items.Add("ЗАПРЕТ ВМЕСТЕ  " + t.AsString(""));

                foreach (var m in state["mods"].Items)
                    items.Add("МОД БЛОК  " + m["id"].AsString("") + "  для  " + m["version"].AsString("*"));

                try
                {
                    var servers = await NareAuth.AdminServersAsync(Token, Ct());
                    foreach (var sv in servers)
                        if (sv.Status == "pending")
                            items.Add("ЗАЯВКА  " + sv.Id + "  " + sv.Name + "  (" + sv.Ip + ")");
                }
                catch { }

                _info.SetItems(items);
            }
            catch (Exception ex) { _status.Text = "Сводка не пришла: " + ex.Message; }
        }

        private void DrawUser(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            var u = item as UserRow;
            if (u == null) return;
            var box = Rectangle.Inflate(r, -3, -2);
            if (box.Width <= 0 || box.Height <= 0) return;

            if (selected) Theme.FillRound(g, box, 7, Color.FromArgb(46, Theme.Accent));
            else if (hover) Theme.FillRound(g, box, 7, Color.FromArgb(16, 255, 255, 255));

            Theme.Text1(g, u.Nick, Theme.F(9.6f, FontStyle.Bold),
                u.Banned ? Theme.Bad : Theme.Text,
                new Rectangle(box.X + 8, box.Y + 3, box.Width - 90, 18));

            string sub = u.Coins + " монет";
            if (!string.Equals(u.Username, u.Nick, StringComparison.Ordinal) && u.Username.Length > 0)
                sub += " · в игре " + u.Username;
            Theme.Text1(g, sub, Theme.F(8f), Theme.Dim,
                new Rectangle(box.X + 8, box.Y + 20, box.Width - 90, 16));

            string tag = u.Admin ? "АДМИН" : u.Banned ? "БАН" : u.BlockedTogether ? "БЕЗ ВМЕСТЕ" : "";
            if (tag.Length > 0)
                Theme.Text1(g, tag, Theme.F(7.6f, FontStyle.Bold),
                    u.Admin ? Theme.Good : u.Banned ? Theme.Bad : Theme.Warn,
                    new Rectangle(box.Right - 86, box.Y, 80, box.Height),
                    StringAlignment.Far, StringAlignment.Center);
        }

        private void DrawInfo(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            string line = item as string;
            if (line == null) return;
            Color c = line.StartsWith("БАН") ? Theme.Bad
                    : line.StartsWith("АПЕЛЛЯЦИЯ") ? Theme.Warn
                    : line.StartsWith("ЗАЯВКА") ? Theme.Accent
                    : Theme.Muted;
            Theme.Text1(g, line, Theme.F(9f), c,
                new Rectangle(r.X + 8, r.Y, r.Width - 16, r.Height));
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Layout1(); }

        private int _cap;
        private void Field(Control field, int pad, int w, ref int y)
        {
            if (_cap < _caps.Count) _caps[_cap].SetBounds(pad, y, w, 14);
            _cap++;
            field.SetBounds(pad, y + 16, w, 36);
            y += 16 + 36 + 8;
        }

        private void Layout1()
        {
            if (_scroll == null) return;
            _head = 0;
            _cap = 0;
            _title.SetBounds(28, 18, 500, 30);
            int pad = 28, w = Math.Min(560, Width - pad * 2 - 360), y = 76;
            _statLine.SetBounds(28, 50, Math.Max(300, Width - pad - 360), 18);

            Row("БАН ИГРОКА", ref y);
            Field(_banNick, pad, w, ref y);
            Field(_banType, pad, w, ref y);
            Field(_banMinutes, pad, w, ref y);
            Field(_banReason, pad, w, ref y);
            _ban.Location = new Point(pad, y); _unban.Location = new Point(pad + 150, y);
            _chat.Location = new Point(pad + 300, y); y += 54;

            Row("ПРЕДУПРЕЖДЕНИЕ", ref y);
            Field(_warnNick, pad, w, ref y);
            Field(_warnText, pad, w, ref y);
            _warn.Location = new Point(pad, y); y += 54;

            Row("ОБЪЯВЛЕНИЕ И КОМАНДА СЕРВЕРУ", ref y);
            Field(_broadcast, pad, w, ref y); _broadcastGo.Location = new Point(pad + w + 10, _broadcast.Top);
            Field(_command, pad, w, ref y); _commandGo.Location = new Point(pad + w + 10, _command.Top); y += 6;

            Row("ЗАПРЕТ «ВМЕСТЕ»", ref y);
            Field(_togNick, pad, w, ref y);
            _togOff.Location = new Point(pad, y); _togOn.Location = new Point(pad + 200, y); y += 54;

            Row("БЛОКИРОВКА МОДОВ", ref y);
            Field(_modId, pad, w, ref y);
            Field(_modVer, pad, w, ref y);
            _modBlock.Location = new Point(pad, y); _modUnblock.Location = new Point(pad + 200, y); y += 54;

            Row("МОДЕРАЦИЯ СЕРВЕРОВ", ref y);
            Field(_srvId, pad, w, ref y);
            _srvOk.Location = new Point(pad, y); _srvNo.Location = new Point(pad + 196, y); y += 54;

            Row("ОБНОВЛЕНИЕ", ref y);
            Field(_updVer, pad, w, ref y);
            Field(_updLog, pad, w, ref y);
            _updGo.Location = new Point(pad, y); y += 44;
            _updCrit.Location = new Point(pad, y); y += 48;
            Field(_updNick, pad, w, ref y);
            _updOne.Location = new Point(pad, y); y += 54;

            Row("МОНЕТЫ", ref y);
            Field(_coinNick, pad, w, ref y);
            Field(_coinAmount, pad, w, ref y);
            _coinGive.Location = new Point(pad, y); _coinTake.Location = new Point(pad + 160, y); y += 54;

            Row("ПРАВА: АДМИН / МОДЕРАТОР", ref y);
            Field(_rightsNick, pad, w, ref y);
            Field(_rolePick, pad, w, ref y);
            _grantBtn.Location = new Point(pad, y); _revokeBtn.Location = new Point(pad + 162, y); y += 54;

            Row("ИГРОВОЙ НИК", ref y);
            Field(_setNickDrop, pad, w, ref y);
            Field(_setNickBox, pad, w, ref y);
            _setNickBtn.Location = new Point(pad, y); y += 54;

            Row("СБРОС ВХОДА И ПАРОЛЬ", ref y);
            Field(_logoutNick, pad, w, ref y);
            _logoutBtn.Location = new Point(pad, y); y += 48;
            Field(_passBox, pad, w, ref y);
            _passBtn.Location = new Point(pad, y); y += 54;

            _status.SetBounds(pad, y, w + 200, 20); y += 28;

            // правая колонка: пользователи сверху, сводка снизу
            int infoX = pad + w + 180;
            int colW = Math.Max(220, Width - infoX - pad);
            int colH = Math.Max(200, Height - 100);
            int usersH = Math.Max(120, colH * 55 / 100);

            _usersHead.SetBounds(infoX, 46, colW, 16);
            _users.SetBounds(infoX, 64, colW, usersH);
            _info.SetBounds(infoX, 64 + usersH + 12, colW, colH - usersH - 12);
        }

        private int _head;
        private void Row(string title, ref int y)
        {
            if (_head < _heads.Length) _heads[_head].SetBounds(28, y, 500, 20);
            _head++;
            y += 26;
        }
    }
}
