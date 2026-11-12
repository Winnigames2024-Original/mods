using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace NareLauncher.Core
{
    /// <summary>
    /// Удалённое управление лаунчером с сайта. Когда включено в настройках и
    /// пользователь вошёл в аккаунт, лаунчер раз в ~0.65с снимает своё окно,
    /// шлёт кадр на NareNet и забирает накопленные события мыши/клавиатуры,
    /// применяя их у себя (реальный курсор двигается и кликает по кнопкам).
    ///
    /// Второй фактор — пароль, который пользователь задаёт сам; на сервере он
    /// хранится только как scrypt-хэш, здесь не хранится вовсе.
    /// </summary>
    public static class RemoteControl
    {
        private static System.Threading.Timer _timer;
        private static Form _form;
        private static volatile bool _busy;

        /// <summary>Был ли последний обмен с сервером удачным.</summary>
        public static volatile bool Online;

        // ------------------------------------------------------------ WinAPI
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002, MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008, MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;

        // ------------------------------------------------------------ запуск
        public static void Start(Form form)
        {
            _form = form;
            if (_timer == null)
                _timer = new System.Threading.Timer(Tick, null, 1200, 650);
        }

        private static void Tick(object state)
        {
            if (_busy) return;
            if (_form == null || !Settings.Current.RemoteEnabled) { Online = false; return; }

            var acc = AccountStore.Selected;
            if (acc == null || string.IsNullOrEmpty(acc.Token)) { Online = false; return; }

            _busy = true;
            try
            {
                string jpg = null; int w = 0, h = 0;
                try { _form.Invoke((Action)(() => Capture(out jpg, out w, out h))); }
                catch { return; }
                if (string.IsNullOrEmpty(jpg)) return;

                string body = "{\"token\":\"" + Esc(acc.Token) + "\",\"w\":" + w + ",\"h\":" + h +
                              ",\"enabled\":true,\"jpg\":\"" + jpg + "\"}";
                string resp = Post("/remote/host", body);
                Online = resp != null;
                if (resp == null) return;

                var j = Json.Parse(resp);
                var ev = j["input"];
                if (ev.IsArray && ev.Count > 0)
                {
                    try { _form.BeginInvoke((Action)(() => Apply(ev))); } catch { }
                }
            }
            catch { }
            finally { _busy = false; }
        }

        // ------------------------------------------------------------ кадр
        private static void Capture(out string jpg, out int w, out int h)
        {
            jpg = null; w = 0; h = 0;
            try
            {
                var rect = _form.ClientRectangle;
                int sw = rect.Width, sh = rect.Height;
                if (sw < 2 || sh < 2) return;

                double scale = sw > 900 ? 900.0 / sw : 1.0;
                int ow = Math.Max(1, (int)(sw * scale));
                int oh = Math.Max(1, (int)(sh * scale));

                using (var full = new Bitmap(sw, sh))
                {
                    _form.DrawToBitmap(full, new Rectangle(0, 0, sw, sh));
                    using (var small = new Bitmap(ow, oh))
                    {
                        using (var g = Graphics.FromImage(small))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                            g.DrawImage(full, 0, 0, ow, oh);
                        }
                        using (var ms = new MemoryStream())
                        {
                            var pars = new EncoderParameters(1);
                            pars.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 45L);
                            small.Save(ms, JpegEncoder(), pars);
                            jpg = Convert.ToBase64String(ms.ToArray());
                            w = sw; h = sh;        // исходный размер; координаты приходят в долях 0..1
                        }
                    }
                }
            }
            catch { jpg = null; }
        }

        private static ImageCodecInfo _jpeg;
        private static ImageCodecInfo JpegEncoder()
        {
            if (_jpeg != null) return _jpeg;
            foreach (var c in ImageCodecInfo.GetImageEncoders())
                if (c.FormatID == ImageFormat.Jpeg.Guid) { _jpeg = c; break; }
            return _jpeg;
        }

        // ------------------------------------------------------------ ввод
        private static void Apply(Json events)
        {
            try
            {
                var rect = _form.ClientRectangle;
                int cw = rect.Width, ch = rect.Height;

                foreach (var e in events.Items)
                {
                    string t = e["t"].AsString("");
                    int px = Math.Max(0, Math.Min(cw - 1, (int)(e["x"].AsDouble(0) * cw)));
                    int py = Math.Max(0, Math.Min(ch - 1, (int)(e["y"].AsDouble(0) * ch)));
                    Point scr;
                    try { scr = _form.PointToScreen(new Point(px, py)); }
                    catch { continue; }

                    switch (t)
                    {
                        case "move":
                            SetCursorPos(scr.X, scr.Y);
                            break;
                        case "down":
                            SetCursorPos(scr.X, scr.Y);
                            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                            break;
                        case "up":
                            SetCursorPos(scr.X, scr.Y);
                            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                            break;
                        case "click":
                            Front();
                            SetCursorPos(scr.X, scr.Y);
                            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                            break;
                        case "dbl":
                            Front();
                            SetCursorPos(scr.X, scr.Y);
                            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                            break;
                        case "rclick":
                            SetCursorPos(scr.X, scr.Y);
                            mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
                            mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
                            break;
                        case "scroll":
                            SetCursorPos(scr.X, scr.Y);
                            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)(e["dy"].AsInt(0) * 120), UIntPtr.Zero);
                            break;
                        case "text":
                            Front();
                            SendText(e["text"].AsString(""));
                            break;
                        case "key":
                            Front();
                            SendKey(e["key"].AsString(""));
                            break;
                    }
                }
            }
            catch { }
        }

        private static void Front()
        {
            try { _form.Activate(); _form.BringToFront(); } catch { }
        }

        private static void SendText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var sb = new StringBuilder();
            foreach (char c in text)
            {
                if ("+^%~(){}[]".IndexOf(c) >= 0) sb.Append('{').Append(c).Append('}');
                else sb.Append(c);
            }
            try { SendKeys.SendWait(sb.ToString()); } catch { }
        }

        private static void SendKey(string name)
        {
            string code;
            switch ((name ?? "").ToLowerInvariant())
            {
                case "enter": code = "{ENTER}"; break;
                case "backspace": code = "{BACKSPACE}"; break;
                case "tab": code = "{TAB}"; break;
                case "esc": case "escape": code = "{ESC}"; break;
                case "delete": code = "{DELETE}"; break;
                case "up": code = "{UP}"; break;
                case "down": code = "{DOWN}"; break;
                case "left": code = "{LEFT}"; break;
                case "right": code = "{RIGHT}"; break;
                case "home": code = "{HOME}"; break;
                case "end": code = "{END}"; break;
                case "space": code = " "; break;
                default: return;
            }
            try { SendKeys.SendWait(code); } catch { }
        }

        // ------------------------------------------------------------ сеть
        private static string Post(string route, string body)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("http://" + NareNet.Where + route);
                req.Method = "POST"; req.ContentType = "application/json"; req.Timeout = 15000;
                var data = Encoding.UTF8.GetBytes(body);
                req.ContentLength = data.Length;
                using (var s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    return rd.ReadToEnd();
            }
            catch { return null; }
        }

        /// <summary>Сообщает серверу пароль/флаг. Пустой пароль — только меняем флаг.</summary>
        public static bool Config(string password, bool enabled, bool clear = false)
        {
            var acc = AccountStore.Selected;
            if (acc == null || string.IsNullOrEmpty(acc.Token)) return false;
            string body = clear
                ? "{\"token\":\"" + Esc(acc.Token) + "\",\"clear\":true}"
                : "{\"token\":\"" + Esc(acc.Token) + "\",\"password\":\"" + Esc(password ?? "") +
                  "\",\"enabled\":" + (enabled ? "true" : "false") + "}";
            string resp = Post("/remote/config", body);
            if (resp == null) return false;
            return Json.Parse(resp)["ok"].AsBool(false);
        }

        private static string Esc(string s)
        {
            s = s ?? "";
            var b = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                if (c == '\\') b.Append("\\\\");
                else if (c == '"') b.Append("\\\"");
                else if (c == '\n') b.Append("\\n");
                else if (c == '\r') b.Append("\\r");
                else if (c == '\t') b.Append("\\t");
                else if (c < 0x20) b.Append("\\u").Append(((int)c).ToString("x4"));
                else b.Append(c);
            }
            return b.ToString();
        }
    }
}
