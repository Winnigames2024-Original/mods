using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace NareLauncher.Core
{
    /// <summary>
    /// Фоновый агент лаунчера: опрашивает NareNet про кнопки питания (старт/стоп/
    /// перезапуск), нажатые в веб-панели владельцем, и передаёт их в «Мои серверы».
    /// Работает по токену активного аккаунта, пока лаунчер открыт — поэтому сервер
    /// можно запустить из браузера, даже если он сейчас выключен (но лаунчер открыт).
    /// </summary>
    public static class NareAgent
    {
        private static Thread _th;
        private static volatile bool _run;

        /// <summary>Вызывается при команде из веба: (имя сервера «ник-id», действие start/stop/restart).</summary>
        public static Action<string, string> OnControl;

        public static void Start()
        {
            if (_run) return;
            _run = true;
            _th = new Thread(Loop) { IsBackground = true, Name = "nare-agent" };
            _th.Start();
        }

        public static void Stop() { _run = false; }

        private static void Loop()
        {
            while (_run)
            {
                try
                {
                    var acc = AccountStore.Selected;
                    string token = acc == null ? "" : (acc.Token ?? "");
                    if (token.Length > 0)
                    {
                        string resp = HttpPost("/server/agent", "{\"token\":\"" + Esc(token) + "\"}");
                        if (resp != null)
                        {
                            var j = Json.Parse(resp);
                            foreach (var c in j["controls"].Items)
                            {
                                string name = c["name"].AsString("");
                                string action = c["action"].AsString("");
                                var h = OnControl;
                                if (h != null && name.Length > 0 && action.Length > 0)
                                {
                                    try { h(name, action); } catch { }
                                }
                            }
                        }
                    }
                }
                catch { }
                for (int i = 0; i < 20 && _run; i++) Thread.Sleep(100);   // ~2с
            }
        }

        private static string Esc(string s) { return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\""); }

        private static string HttpPost(string route, string body)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("http://" + NareNet.Where + route);
                req.Method = "POST";
                req.ContentType = "application/json";
                req.Timeout = 8000;
                var data = Encoding.UTF8.GetBytes(body);
                req.ContentLength = data.Length;
                using (var s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    return rd.ReadToEnd();
            }
            catch { return null; }
        }
    }
}
