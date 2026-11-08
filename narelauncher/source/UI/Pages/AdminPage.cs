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
        private readonly NareTextBox _banNick = new NareTextBox { Placeholder = "Ник игрока" };
        private readonly NareTextBox _banMinutes = new NareTextBox { Placeholder = "Минут (0 или пусто — навсегда)" };
        private readonly NareTextBox _banReason = new NareTextBox { Placeholder = "Причина" };
        private readonly NareButton _ban = new NareButton { Text = "Забанить", Style = BtnStyle.Danger, Size = new Size(140, 38) };
        private readonly NareButton _unban = new NareButton { Text = "Разбанить", Style = BtnStyle.Ghost, Size = new Size(140, 38) };

        // предупреждение
        private readonly NareTextBox _warnNick = new NareTextBox { Placeholder = "Ник" };
        private readonly NareTextBox _warnText = new NareTextBox { Placeholder = "Текст предупреждения" };
        private readonly NareButton _warn = new NareButton { Text = "Предупредить", Style = BtnStyle.Accent, Size = new Size(160, 38) };

        // объявление и команда
        private readonly NareTextBox _broadcast = new NareTextBox { Placeholder = "Объявление всем (пусто — убрать)" };
        private readonly NareButton _broadcastGo = new NareButton { Text = "Объявить", Style = BtnStyle.Accent, Size = new Size(150, 38) };
        private readonly NareTextBox _command = new NareTextBox { Placeholder = "Команда серверу, например say привет" };
        private readonly NareButton _commandGo = new NareButton { Text = "Выполнить", Style = BtnStyle.Ghost, Size = new Size(150, 38) };

        // запрет «Вместе»
        private readonly NareTextBox _togNick = new NareTextBox { Placeholder = "Ник" };
        private readonly NareButton _togOff = new NareButton { Text = "Запретить «Вместе»", Style = BtnStyle.Danger, Size = new Size(190, 38) };
        private readonly NareButton _togOn = new NareButton { Text = "Разрешить", Style = BtnStyle.Ghost, Size = new Size(140, 38) };

        // блок модов
        private readonly NareTextBox _modId = new NareTextBox { Placeholder = "ID мода, например xray" };
        private readonly NareTextBox _modVer = new NareTextBox { Placeholder = "Версия игры или * для всех" };
        private readonly NareButton _modBlock = new NareButton { Text = "Заблокировать мод", Style = BtnStyle.Danger, Size = new Size(190, 38) };
        private readonly NareButton _modUnblock = new NareButton { Text = "Разблокировать", Style = BtnStyle.Ghost, Size = new Size(160, 38) };

        // экстренное обновление
        private readonly NareTextBox _updVer = new NareTextBox { Placeholder = "Версия (подтянется сама)" };
        private readonly NareTextBox _updLog = new NareTextBox { Placeholder = "Что нового (changelog)" };
        private readonly NareButton _updGo = new NareButton { Text = "Объявить обновление", Style = BtnStyle.Accent, Size = new Size(200, 38) };
        private readonly NareButton _updCrit = new NareButton { Text = "Экстренное: закрыть всем через 30с", Style = BtnStyle.Danger, Size = new Size(300, 38) };

        private readonly NareLabel _title = new NareLabel("АДМИН-ПАНЕЛЬ", 19f, FontStyle.Bold, Theme.Text);
        private readonly NareLabel[] _heads = new NareLabel[7];
        private readonly NareLabel _status = new NareLabel("", 9f, FontStyle.Regular, Theme.Muted);
        private readonly VirtualList _info = new VirtualList { ItemHeight = 26, EmptyText = "Пусто" };

        private string Token { get { var a = AccountStore.Selected; return a == null ? "" : a.Token; } }

        public AdminPage()
        {
            Controls.Add(_scroll);

            string[] titles = { "БАН ИГРОКА", "ПРЕДУПРЕЖДЕНИЕ", "ОБЪЯВЛЕНИЕ И КОМАНДА СЕРВЕРУ",
                "ЗАПРЕТ «ВМЕСТЕ»", "БЛОКИРОВКА МОДОВ", "ОБНОВЛЕНИЕ", "" };
            for (int i = 0; i < _heads.Length; i++)
            {
                _heads[i] = new NareLabel(titles[i], 9f, FontStyle.Bold, Theme.Accent);
                _scroll.Controls.Add(_heads[i]);
            }
            _scroll.Controls.Add(_title);

            Add(_banNick); Add(_banMinutes); Add(_banReason); Add(_ban); Add(_unban);
            Add(_warnNick); Add(_warnText); Add(_warn);
            Add(_broadcast); Add(_broadcastGo); Add(_command); Add(_commandGo);
            Add(_togNick); Add(_togOff); Add(_togOn);
            Add(_modId); Add(_modVer); Add(_modBlock); Add(_modUnblock);
            Add(_updVer); Add(_updLog); Add(_updGo); Add(_updCrit);
            _scroll.Controls.Add(_status);
            _scroll.Controls.Add(_info);
            _info.Renderer = DrawInfo;

            _ban.Click += async (s, e) => await Do(() => NareAuth.BanAsync(Token, _banNick.Text.Trim(),
                ParseInt(_banMinutes.Text), _banReason.Text.Trim(), Ct()), "Забанен " + _banNick.Text.Trim());
            _unban.Click += async (s, e) => await Do(() => NareAuth.UnbanAsync(Token, _banNick.Text.Trim(), Ct()),
                "Разбанен " + _banNick.Text.Trim());

            _warn.Click += async (s, e) => await Do(() => NareAuth.WarnAsync(Token, _warnNick.Text.Trim(), _warnText.Text.Trim(), Ct()),
                "Предупреждение отправлено");

            _broadcastGo.Click += async (s, e) => await Do(() => NareAuth.BroadcastAsync(Token, _broadcast.Text.Trim(), Ct()),
                "Объявление обновлено");
            _commandGo.Click += async (s, e) => await Do(() => NareAuth.CommandAsync(Token, _command.Text.Trim(), Ct()),
                "Команда поставлена в очередь серверу");

            _togOff.Click += async (s, e) => await Do(() => NareAuth.TogetherAsync(Token, _togNick.Text.Trim(), true, Ct()),
                "«Вместе» запрещено для " + _togNick.Text.Trim());
            _togOn.Click += async (s, e) => await Do(() => NareAuth.TogetherAsync(Token, _togNick.Text.Trim(), false, Ct()),
                "«Вместе» снова разрешено");

            _modBlock.Click += async (s, e) => await Do(() => NareAuth.BlockModAsync(Token, _modId.Text.Trim(), _modVer.Text.Trim(), true, Ct()),
                "Мод заблокирован");
            _modUnblock.Click += async (s, e) => await Do(() => NareAuth.BlockModAsync(Token, _modId.Text.Trim(), _modVer.Text.Trim(), false, Ct()),
                "Мод разблокирован");

            _updGo.Click += async (s, e) => await Announce(false);
            _updCrit.Click += async (s, e) =>
            {
                if (!Host.Confirm("Экстренное обновление закроет Minecraft и лаунчер у всех игроков через 30 секунд. Продолжить?"))
                    return;
                await Announce(true);
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

        private async System.Threading.Tasks.Task Announce(bool critical)
        {
            string version = _updVer.Text.Trim();
            if (version.Length == 0) { _status.Text = "Укажите версию обновления."; return; }

            await Do(() => NareAuth.AnnounceUpdateAsync(Token, version, _updLog.Text.Trim(), critical, Ct()),
                critical ? "Экстренное обновление объявлено" : "Обновление объявлено");
        }

        private async System.Threading.Tasks.Task Do(Func<System.Threading.Tasks.Task> action, string okText)
        {
            if (string.IsNullOrEmpty(Token)) { _status.Text = "Нет прав администратора."; return; }

            try
            {
                await action();
                _status.Text = okText;
                _status.ForeColor = Theme.Good;
                await Reload();
            }
            catch (Exception ex)
            {
                _status.Text = "Ошибка: " + ex.Message;
                _status.ForeColor = Theme.Bad;
            }
        }

        private async System.Threading.Tasks.Task Reload()
        {
            try
            {
                var state = await NareAuth.StateAsync(Token, Ct());
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

                _info.SetItems(items);
            }
            catch (Exception ex) { _status.Text = "Сводка не пришла: " + ex.Message; }
        }

        private void DrawInfo(Graphics g, Rectangle r, object item, int index, bool hover, bool selected)
        {
            string line = item as string;
            if (line == null) return;
            Color c = line.StartsWith("БАН") ? Theme.Bad
                    : line.StartsWith("АПЕЛЛЯЦИЯ") ? Theme.Warn
                    : Theme.Muted;
            Theme.Text1(g, line, Theme.F(9f), c,
                new Rectangle(r.X + 8, r.Y, r.Width - 16, r.Height));
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Layout1(); }

        private void Layout1()
        {
            if (_scroll == null) return;
            _head = 0;
            _title.SetBounds(28, 22, 500, 32);
            int pad = 28, w = Math.Min(560, Width - pad * 2 - 360), y = 64;

            Row("БАН ИГРОКА", ref y);
            _banNick.SetBounds(pad, y, w, 36); y += 42;
            _banMinutes.SetBounds(pad, y, w, 36); y += 42;
            _banReason.SetBounds(pad, y, w, 36); y += 42;
            _ban.Location = new Point(pad, y); _unban.Location = new Point(pad + 150, y); y += 54;

            Row("ПРЕДУПРЕЖДЕНИЕ", ref y);
            _warnNick.SetBounds(pad, y, w, 36); y += 42;
            _warnText.SetBounds(pad, y, w, 36); y += 42;
            _warn.Location = new Point(pad, y); y += 54;

            Row("ОБЪЯВЛЕНИЕ И КОМАНДА СЕРВЕРУ", ref y);
            _broadcast.SetBounds(pad, y, w, 36); _broadcastGo.Location = new Point(pad + w + 10, y); y += 44;
            _command.SetBounds(pad, y, w, 36); _commandGo.Location = new Point(pad + w + 10, y); y += 54;

            Row("ЗАПРЕТ «ВМЕСТЕ»", ref y);
            _togNick.SetBounds(pad, y, w, 36); y += 42;
            _togOff.Location = new Point(pad, y); _togOn.Location = new Point(pad + 200, y); y += 54;

            Row("БЛОКИРОВКА МОДОВ", ref y);
            _modId.SetBounds(pad, y, w, 36); y += 42;
            _modVer.SetBounds(pad, y, w, 36); y += 42;
            _modBlock.Location = new Point(pad, y); _modUnblock.Location = new Point(pad + 200, y); y += 54;

            Row("ОБНОВЛЕНИЕ", ref y);
            _updVer.SetBounds(pad, y, w, 36); y += 42;
            _updLog.SetBounds(pad, y, w, 36); y += 42;
            _updGo.Location = new Point(pad, y); y += 44;
            _updCrit.Location = new Point(pad, y); y += 54;

            _status.SetBounds(pad, y, w + 200, 20); y += 28;

            // правая колонка: сводка
            int infoX = pad + w + 180;
            _info.SetBounds(infoX, 64, Math.Max(220, Width - infoX - pad), Math.Max(200, Height - 100));
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
