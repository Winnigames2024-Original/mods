using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;
using NareLauncher.Core;

namespace NareLauncher.UI
{
    /// <summary>
    /// Прозрачный оверлей чата трансляции поверх всех окон (как в OBS/стрим-оверлеях):
    /// видно поверх игры, мышь проходит сквозь него. Крепится слева или справа.
    /// </summary>
    public class ChatOverlay : Form
    {
        private readonly string _streamId;
        private readonly System.Windows.Forms.Timer _poll = new System.Windows.Forms.Timer { Interval = 1500 };
        private readonly List<string[]> _lines = new List<string[]>();   // [from, text]
        private int _since;
        private static readonly Color Key = Color.FromArgb(1, 2, 3);

        public ChatOverlay(string streamId, bool right)
        {
            _streamId = streamId;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Key;
            TransparencyKey = Key;                 // всё, кроме нарисованного, — прозрачно
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;

            var scr = Screen.PrimaryScreen.WorkingArea;
            int w = 380, h = Math.Min(620, scr.Height - 80);
            Location = new Point(right ? scr.Right - w - 16 : scr.Left + 16, scr.Top + 40);
            Size = new Size(w, h);

            _poll.Tick += (s, e) => Poll();
            _poll.Start();
        }

        // окно «сквозное» для мыши — клики уходят в игру под ним
        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x8000000;
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        private void Poll()
        {
            try
            {
                string resp = Post("/stream/chat/get", "{\"id\":\"" + _streamId + "\",\"since\":" + _since + "}");
                if (resp == null) return;
                var j = Json.Parse(resp);
                var msgs = j["msgs"];
                if (msgs.IsArray && msgs.Count > 0)
                {
                    foreach (var m in msgs.Items)
                        _lines.Add(new[] { m["from"].AsString(""), m["text"].AsString("") });
                    while (_lines.Count > 14) _lines.RemoveAt(0);
                    _since = j["total"].AsInt(_since);
                    Invalidate();
                }
            }
            catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            int y = Height;
            using (var font = new Font("Segoe UI", 10f))
            using (var bold = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (var back = new SolidBrush(Color.FromArgb(205, 10, 12, 18)))
            using (var nameBr = new SolidBrush(Color.FromArgb(150, 180, 255)))
            using (var textBr = new SolidBrush(Color.White))
            {
                for (int i = _lines.Count - 1; i >= 0; i--)
                {
                    string who = _lines[i][0], txt = _lines[i][1];
                    var szName = g.MeasureString(who + ": ", bold);
                    var szText = g.MeasureString(txt, font, Width - 24 - (int)szName.Width);
                    int lineH = (int)Math.Max(szName.Height, szText.Height) + 10;
                    y -= lineH + 4;
                    if (y < 0) break;
                    FillRound(g, new Rectangle(0, y, Width - 2, lineH), 8, back);
                    g.DrawString(who + ":", bold, nameBr, 8, y + 5);
                    g.DrawString(txt, font, textBr, new RectangleF(8 + szName.Width, y + 5, Width - 16 - szName.Width, lineH));
                }
            }
        }

        private static void FillRound(Graphics g, Rectangle r, int rad, Brush b)
        {
            using (var p = new GraphicsPath())
            {
                p.AddArc(r.X, r.Y, rad, rad, 180, 90);
                p.AddArc(r.Right - rad, r.Y, rad, rad, 270, 90);
                p.AddArc(r.Right - rad, r.Bottom - rad, rad, rad, 0, 90);
                p.AddArc(r.X, r.Bottom - rad, rad, rad, 90, 90);
                p.CloseFigure();
                g.FillPath(b, p);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e) { _poll.Stop(); _poll.Dispose(); base.OnFormClosed(e); }

        private static string Post(string route, string body)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("http://" + NareNet.Where + route);
                req.Method = "POST"; req.ContentType = "application/json"; req.Timeout = 12000;
                var data = Encoding.UTF8.GetBytes(body);
                req.ContentLength = data.Length;
                using (var s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) return rd.ReadToEnd();
            }
            catch { return null; }
        }
    }
}
