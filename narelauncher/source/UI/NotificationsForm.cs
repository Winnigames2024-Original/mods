using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Уведомления в приложении: лента событий (подписки, лайки, зрители эфиров,
    /// рассылки), покупка подписки на рассылку (10000 монет) и отправка
    /// уведомлений всем/кому-то (админы — раз в секунду). Данные — NareNet.
    /// </summary>
    public class NotificationsForm : Form
    {
        private readonly Label _status = new Label();
        private readonly FlowLayoutPanel _feed = new FlowLayoutPanel();
        private readonly Button _buy = new Button();
        private readonly Panel _sendRow = new Panel();
        private readonly ComboBox _target = new ComboBox();
        private readonly TextBox _nick = new TextBox();
        private readonly TextBox _text = new TextBox();
        private readonly Button _send = new Button();
        private readonly Timer _tick = new Timer { Interval = 15000 };

        private static readonly Color Bg = Color.FromArgb(14, 17, 24);
        private static readonly Color Panel = Color.FromArgb(21, 27, 38);
        private static readonly Color Blue = Color.FromArgb(0, 119, 255);
        private static readonly Color Txt = Color.FromArgb(232, 238, 248);
        private static readonly Color Dim = Color.FromArgb(140, 150, 170);
        private static readonly Color Green = Color.FromArgb(75, 179, 75);

        public NotificationsForm()
        {
            Text = "Уведомления";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(620, 640);
            MinimumSize = new Size(480, 420);
            BackColor = Bg;
            Font = Theme.F(10f);

            var head = new Label { Text = "🔔 Уведомления", Font = Theme.F(15f, FontStyle.Bold), ForeColor = Txt,
                                   AutoSize = false, Bounds = new Rectangle(18, 14, 560, 30), BackColor = Color.Transparent };
            Controls.Add(head);

            _status.AutoSize = false; _status.ForeColor = Dim; _status.BackColor = Color.Transparent;
            _status.Bounds = new Rectangle(18, 46, 580, 20); _status.Font = Theme.F(9.5f);
            Controls.Add(_status);

            // покупка подписки
            Style(_buy, Blue, Color.White); _buy.Text = "Купить подписку на рассылку — 10000 монет";
            _buy.Bounds = new Rectangle(18, 72, 320, 32); _buy.Visible = false;
            _buy.Click += (s, e) => Buy();
            Controls.Add(_buy);

            // отправка уведомления
            _sendRow.Bounds = new Rectangle(18, 72, 580, 36);
            _sendRow.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _sendRow.Visible = false; _sendRow.BackColor = Color.Transparent;
            _target.DropDownStyle = ComboBoxStyle.DropDownList;
            _target.Items.AddRange(new object[] { "Всем", "Кому-то" });
            _target.SelectedIndex = 0;
            _target.SetBounds(0, 4, 90, 28);
            _target.SelectedIndexChanged += (s, e) => { _nick.Visible = _target.SelectedIndex == 1; };
            _nick.BorderStyle = BorderStyle.FixedSingle; _nick.BackColor = Color.FromArgb(10, 13, 19); _nick.ForeColor = Txt;
            _nick.SetBounds(96, 4, 110, 28); _nick.Visible = false;
            var ph = new Label { Text = "ник", ForeColor = Dim, BackColor = Color.Transparent, Bounds = new Rectangle(98, 7, 40, 18) };
            _text.BorderStyle = BorderStyle.FixedSingle; _text.BackColor = Color.FromArgb(10, 13, 19); _text.ForeColor = Txt;
            _text.SetBounds(212, 4, 290, 28);
            _text.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _text.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Send(); } };
            Style(_send, Blue, Color.White); _send.Text = "Отправить"; _send.SetBounds(508, 3, 72, 30);
            _send.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _send.Click += (s, e) => Send();
            _sendRow.Controls.AddRange(new Control[] { _target, _nick, _text, _send });
            Controls.Add(_sendRow);

            var fl = new Label { Text = "Лента событий", Font = Theme.F(11f, FontStyle.Bold), ForeColor = Txt,
                                 AutoSize = false, Bounds = new Rectangle(18, 118, 560, 22), BackColor = Color.Transparent };
            Controls.Add(fl);

            _feed.AutoScroll = true; _feed.WrapContents = false; _feed.FlowDirection = FlowDirection.TopDown;
            _feed.BackColor = Bg; _feed.Bounds = new Rectangle(14, 144, 586, 450);
            _feed.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            Controls.Add(_feed);

            _tick.Tick += (s, e) => Load();
            Load(); _tick.Start();
        }

        private static void Style(Button b, Color bg, Color fg)
        {
            b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0;
            b.BackColor = bg; b.ForeColor = fg; b.Font = Theme.F(10f, FontStyle.Bold); b.Cursor = Cursors.Hand; b.UseVisualStyleBackColor = false;
        }

        private string Token { get { var a = AccountStore.Selected; return a == null ? "" : (a.Token ?? ""); } }

        private void Load()
        {
            if (Token.Length == 0) { _status.Text = "Войдите в аккаунт."; return; }
            new System.Threading.Thread(() =>
            {
                string r = Post("/notify/status", "{\"token\":\"" + Esc(Token) + "\"}");
                Ui(() =>
                {
                    if (r == null) { _status.Text = "Нет связи с сервером."; return; }
                    var j = Json.Parse(r);
                    bool admin = j["admin"].AsBool(false), pro = j["pro"].AsBool(false);
                    int coins = j["coins"].AsInt(0);
                    if (admin) { _status.Text = "Вы администратор — рассылка раз в секунду. Монет: " + coins; _sendRow.Visible = true; _buy.Visible = false; }
                    else if (pro) { _status.Text = "Подписка активна — рассылка раз в 10 минут. Монет: " + coins; _sendRow.Visible = true; _buy.Visible = false; }
                    else { _status.Text = "Подписки на рассылку нет. Монет: " + coins; _sendRow.Visible = false; _buy.Visible = true; }

                    _feed.Controls.Clear();
                    int n = 0;
                    foreach (var ev in j["events"].Items)
                    {
                        _feed.Controls.Add(Card(ev["type"].AsString(""), ev["text"].AsString(""), ev["at"].AsLong(0)));
                        n++;
                    }
                    if (n == 0) _feed.Controls.Add(new Label { Text = "Пока событий нет. Когда на вас подпишутся, лайкнут видео или зайдут на эфир — появится тут.",
                        ForeColor = Dim, AutoSize = false, Width = 560, Height = 40, Font = Theme.F(9.5f) });
                });
            }) { IsBackground = true }.Start();
        }

        private Control Card(string type, string text, long at)
        {
            var p = new Panel { Width = 560, Height = 44, BackColor = Panel, Margin = new Padding(0, 0, 0, 8) };
            var ic = new Label { Text = Icon(type), Font = Theme.F(14f), AutoSize = false, Bounds = new Rectangle(10, 10, 28, 24), BackColor = Color.Transparent, ForeColor = Txt };
            var tx = new Label { Text = text, ForeColor = Txt, AutoSize = false, Bounds = new Rectangle(44, 6, 420, 20), Font = Theme.F(10f), BackColor = Color.Transparent };
            var tm = new Label { Text = Ago(at), ForeColor = Dim, AutoSize = false, Bounds = new Rectangle(44, 24, 420, 16), Font = Theme.F(8.5f), BackColor = Color.Transparent };
            p.Controls.AddRange(new Control[] { ic, tx, tm });
            return p;
        }

        private static string Icon(string t)
        {
            switch (t) { case "подписка": return "➕"; case "отписка": return "➖"; case "лайк": return "❤️"; case "анлайк": return "💔";
                case "зритель": return "👁"; case "ушёл": return "🚪"; case "рассылка": return "📣"; default: return "🔔"; }
        }
        private static string Ago(long at)
        {
            if (at <= 0) return "";
            long s = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - at) / 1000;
            if (s < 60) return "только что"; if (s < 3600) return (s / 60) + " мин назад";
            if (s < 86400) return (s / 3600) + " ч назад"; return (s / 86400) + " дн назад";
        }

        private void Buy()
        {
            if (MessageBox.Show(this, "Купить подписку на рассылку за 10000 монет?", "Подписка", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            new System.Threading.Thread(() =>
            {
                string r = Post("/notify/buy", "{\"token\":\"" + Esc(Token) + "\"}");
                Ui(() =>
                {
                    var j = r != null ? Json.Parse(r) : null;
                    if (j != null && j["ok"].AsBool(false)) { MessageBox.Show(this, "Подписка активна!"); Load(); }
                    else MessageBox.Show(this, (j != null ? j["error"].AsString("Ошибка") : "Нет связи"));
                });
            }) { IsBackground = true }.Start();
        }

        private void Send()
        {
            string t = _text.Text.Trim(); if (t.Length == 0) return;
            string tgt = _target.SelectedIndex == 1 ? _nick.Text.Trim() : "all";
            _text.Clear();
            new System.Threading.Thread(() =>
            {
                string r = Post("/notify/send", "{\"token\":\"" + Esc(Token) + "\",\"target\":\"" + Esc(tgt) + "\",\"text\":\"" + Esc(t) + "\"}");
                Ui(() =>
                {
                    var j = r != null ? Json.Parse(r) : null;
                    if (j != null && j["ok"].AsBool(false)) _status.Text = "Отправлено " + (tgt == "all" ? "всем" : tgt) + " ✓";
                    else _status.Text = (j != null ? j["error"].AsString("Ошибка") : "Нет связи");
                });
            }) { IsBackground = true }.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e) { _tick.Stop(); base.OnFormClosing(e); }
        private void Ui(Action a) { if (IsDisposed) return; try { if (InvokeRequired) BeginInvoke(a); else a(); } catch { } }

        private static string Post(string route, string body)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("http://" + NareNet.Where + route);
                req.Method = "POST"; req.ContentType = "application/json"; req.Timeout = 15000;
                var data = Encoding.UTF8.GetBytes(body); req.ContentLength = data.Length;
                using (var s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) return rd.ReadToEnd();
            }
            catch (WebException we) { try { using (var rd = new StreamReader(we.Response.GetResponseStream(), Encoding.UTF8)) return rd.ReadToEnd(); } catch { return null; } }
            catch { return null; }
        }
        private static string Esc(string s)
        {
            s = s ?? ""; var b = new StringBuilder(s.Length + 8);
            foreach (char c in s) { if (c == '\\') b.Append("\\\\"); else if (c == '"') b.Append("\\\""); else if (c == '\n') b.Append("\\n"); else if (c == '\r') b.Append("\\r"); else if (c < 0x20) b.Append("\\u").Append(((int)c).ToString("x4")); else b.Append(c); }
            return b.ToString();
        }
    }
}
