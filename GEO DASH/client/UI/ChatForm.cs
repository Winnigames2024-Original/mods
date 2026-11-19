using GEO_DASH.Net;

namespace GEO_DASH.UI
{
    /// <summary>
    /// Переписка: с игроком, с другом или с поддержкой.
    /// Поддержка — собеседник с именем SUPPORT: игрок пишет туда, админ отвечает оттуда же.
    /// </summary>
    public class ChatForm : GdForm
    {
        public const string Support = "SUPPORT";

        readonly string _peer;
        readonly bool _asSupport;      // админ отвечает от имени поддержки
        readonly ListBox _log;
        readonly TextBox _text;
        readonly System.Windows.Forms.Timer _poll = new() { Interval = 4000 };
        string _msg = "";

        public ChatForm(string peer, bool asSupport = false)
        {
            _peer = peer;
            _asSupport = asSupport;

            Text = "GEO DASH — " + (IsSupport ? "Поддержка" : "Чат с " + peer);
            ClientSize = new Size(900, 640);
            BgA = IsSupport ? Color.FromArgb(230, 150, 30) : Color.FromArgb(40, 120, 230);
            BgId = IsSupport ? 4 : 8; GroundId = 2;

            _log = new ListBox
            {
                Left = 40, Top = 110, Width = ClientSize.Width - 80, Height = 400,
                BackColor = Color.FromArgb(18, 16, 44), ForeColor = Gd.Paper,
                BorderStyle = BorderStyle.FixedSingle, Font = Gd.Reg(10.5f),
                IntegralHeight = false, HorizontalScrollbar = true
            };
            Host(_log);

            _text = new TextBox
            {
                Left = 40, Top = 524, Width = ClientSize.Width - 260, Height = 38,
                BackColor = Color.FromArgb(22, 20, 52), ForeColor = Gd.Paper,
                BorderStyle = BorderStyle.FixedSingle, Font = Gd.Bold(11f)
            };
            Host(_text);
            _text.KeyDown += async (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                await Send();
            };

            Add("ОТПРАВИТЬ", GdIcon.Upload, ClientSize.Width - 208, 518, 168, 48,
                Color.FromArgb(120, 255, 170), Color.FromArgb(20, 150, 100), async (s, e) => await Send());

            Add("НАЗАД", GdIcon.Back, 24, 24, 150, 50,
                Color.FromArgb(160, 160, 190), Color.FromArgb(70, 70, 110), (s, e) => CloseScreen());

            Shown += async (s, e) => { await Reload(); _poll.Start(); };
            FormClosed += (s, e) => _poll.Stop();
            _poll.Tick += async (s, e) => await Reload();
        }

        bool IsSupport => string.Equals(_peer, Support, StringComparison.OrdinalIgnoreCase);

        async Task Reload()
        {
            var r = await ApiClient.ChatAsync(_peer);
            if (!r.Ok || r.Data == null)
            {
                _msg = ApiClient.Online ? (r.Error ?? "Не получилось загрузить") : "Нет сети — переписка недоступна";
                Content.Invalidate();
                return;
            }

            int top = _log.TopIndex;
            bool atEnd = _log.Items.Count == 0 || _log.TopIndex >= _log.Items.Count - 12;
            _log.BeginUpdate();
            _log.Items.Clear();
            foreach (var m in r.Data.Messages)
            {
                string who = string.Equals(m.From, AppConfig.Settings.UserName, StringComparison.OrdinalIgnoreCase)
                    ? "я" : m.From;
                string time = m.At.Length >= 16 ? m.At.Substring(11, 5) : "";
                _log.Items.Add($"[{time}] {who}: {m.Text}");
            }
            _log.EndUpdate();
            if (_log.Items.Count > 0) _log.TopIndex = atEnd ? Math.Max(0, _log.Items.Count - 1) : top;

            _msg = r.Data.Messages.Count == 0
                ? (IsSupport ? "Напиши, что случилось — поддержка ответит здесь" : "Переписки ещё нет")
                : "";
            Content.Invalidate();
        }

        async Task Send()
        {
            string t = _text.Text.Trim();
            if (t.Length == 0) return;
            _text.Text = "";

            var r = await ApiClient.ChatSendAsync(_peer, t, _asSupport);
            if (!r.Ok) { _msg = r.Error ?? "Сообщение не ушло"; Content.Invalidate(); return; }
            await Reload();
        }

        protected override void OnRender(Graphics g)
        {
            Header(g, IsSupport ? "ПОДДЕРЖКА" : "ЧАТ · " + _peer.ToUpperInvariant(),
                   _asSupport ? "отвечаешь как SUPPORT" : AppConfig.ServerLabel);

            if (!string.IsNullOrEmpty(_msg))
                using (var f = Gd.Bold(11f))
                    Gd.Outlined(g, _msg, f, new RectangleF(40, ClientSize.Height - 58, ClientSize.Width - 80, 28),
                        Gd.Gold, thick: 2.4f);
        }
    }
}
