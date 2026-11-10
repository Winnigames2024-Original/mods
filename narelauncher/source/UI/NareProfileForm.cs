using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// NareProfileForm — профиль/канал пользователя в стиле YouTube: шапка
    /// (аватар, имя, подписчики, статус) и лента его роликов. Если открыт свой
    /// канал (передан токен), статус можно редактировать.
    /// </summary>
    public class NareProfileForm : Form
    {
        private static readonly Color CBg = VideosPage.CBg;
        private static readonly Color CText = VideosPage.CText;
        private static readonly Color CDim = VideosPage.CDim;
        private static readonly Color CRed = VideosPage.CRed;
        private static readonly Color CCard = VideosPage.CCard;

        private readonly string _nick;
        private readonly string _token;   // непусто — это мой канал
        private readonly FlowLayoutPanel _feed = new FlowLayoutPanel();
        private readonly Label _name = new Label();
        private readonly Label _subs = new Label();
        private readonly TextBox _status = new TextBox();
        private readonly Button _saveStatus = new Button();

        public NareProfileForm(string nick, string token)
        {
            _nick = (nick ?? "").ToLowerInvariant();
            _token = token ?? "";

            Text = "NareVideo — канал " + _nick;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(960, 640);
            BackColor = CBg;
            ForeColor = CText;
            Font = Theme.F(10f);

            var ava = new AvatarCircle { Letter = _nick.Length > 0 ? char.ToUpper(_nick[0]) : '?', Bounds = new Rectangle(28, 24, 84, 84) };
            Controls.Add(ava);

            _name.AutoSize = false; _name.Bounds = new Rectangle(128, 30, 760, 32);
            _name.Font = Theme.F(18f, FontStyle.Bold); _name.ForeColor = CText; _name.BackColor = Color.Transparent;
            _name.Text = _nick;
            Controls.Add(_name);

            _subs.AutoSize = false; _subs.Bounds = new Rectangle(130, 66, 760, 20);
            _subs.ForeColor = CDim; _subs.BackColor = Color.Transparent; _subs.Font = Theme.F(9.5f);
            Controls.Add(_subs);

            _status.Bounds = new Rectangle(130, 90, 540, 26);
            _status.BackColor = Color.FromArgb(18, 18, 18); _status.ForeColor = CText;
            _status.BorderStyle = BorderStyle.FixedSingle; _status.Font = Theme.F(9.5f);
            _status.ReadOnly = _token.Length == 0;
            Controls.Add(_status);

            VideosPage.StyleBtn(_saveStatus, CCard, CText);
            _saveStatus.Text = "Сохранить статус";
            _saveStatus.Bounds = new Rectangle(680, 89, 150, 28);
            _saveStatus.Visible = _token.Length > 0;
            _saveStatus.Click += (s, e) => SaveStatus();
            Controls.Add(_saveStatus);

            _feed.AutoScroll = true; _feed.WrapContents = true; _feed.BackColor = CBg;
            _feed.Bounds = new Rectangle(16, 130, 924, 470);
            _feed.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            _feed.Padding = new Padding(8);
            Controls.Add(_feed);

            Load += (s, e) => Reload();
        }

        private void Reload()
        {
            new System.Threading.Thread(() =>
            {
                string prof = HttpGet("http://" + NareNet.Where + "/profile/get?nick=" + Uri.EscapeDataString(_nick));
                List<NareVideo> vids;
                try { vids = NareVideo.List(_nick, null); } catch { vids = new List<NareVideo>(); }
                string uname = _nick, status = ""; int subs = 0;
                if (prof != null)
                {
                    try
                    {
                        var j = Json.Parse(prof);
                        uname = j["username"].AsString(_nick);
                        status = j["status"].AsString("");
                        int c = 0; foreach (var _ in j["subs"].Items) c++; subs = c;
                    }
                    catch { }
                }
                Ui(() =>
                {
                    _name.Text = uname;
                    _subs.Text = VideosPage.Nfmt(subs) + " подписчиков · " + vids.Count + " видео";
                    if (!_status.Focused) _status.Text = status;
                    if (_token.Length == 0 && status.Length == 0) _status.Text = "(без статуса)";
                    _feed.Controls.Clear();
                    foreach (var v in vids) _feed.Controls.Add(VideosPage.BuildCard(v, (n, m) => { }));
                    if (vids.Count == 0)
                        _feed.Controls.Add(new Label { Text = "На этом канале пока нет видео.", AutoSize = true, ForeColor = CDim, BackColor = Color.Transparent, Margin = new Padding(12) });
                });
            })
            { IsBackground = true }.Start();
        }

        private void SaveStatus()
        {
            if (_token.Length == 0) return;
            string body = "{\"token\":\"" + _token.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\",\"status\":\"" +
                          (_status.Text ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";
            _saveStatus.Enabled = false;
            new System.Threading.Thread(() =>
            {
                HttpPost("/profile/status", body);
                Ui(() => { _saveStatus.Enabled = true; _saveStatus.Text = "Сохранено"; });
            })
            { IsBackground = true }.Start();
        }

        private void Ui(Action a) { try { if (IsHandleCreated) BeginInvoke(a); else a(); } catch { } }

        private static string HttpGet(string url)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = 12000;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    return rd.ReadToEnd();
            }
            catch { return null; }
        }
        private static void HttpPost(string route, string body)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("http://" + NareNet.Where + route);
                req.Method = "POST"; req.ContentType = "application/json"; req.Timeout = 12000;
                var data = Encoding.UTF8.GetBytes(body);
                req.ContentLength = data.Length;
                using (var s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                using (var resp = (HttpWebResponse)req.GetResponse()) { }
            }
            catch { }
        }

        private class AvatarCircle : Control
        {
            public char Letter = '?';
            public AvatarCircle() { DoubleBuffered = true; SetStyle(ControlStyles.SupportsTransparentBackColor, true); BackColor = Color.Transparent; }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(CRed)) g.FillEllipse(b, 0, 0, Width - 1, Height - 1);
                using (var f = Theme.F(30f, FontStyle.Bold))
                    TextRenderer.DrawText(g, Letter.ToString(), f, new Rectangle(0, 0, Width, Height), Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }
}
