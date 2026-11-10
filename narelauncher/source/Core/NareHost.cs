using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NareLauncher.Core
{
    /// <summary>
    /// Тоннель-клиент лаунчера: объявляет выделенный сервер в NareNet под именем
    /// «ник-id» и держит обратный тоннель до локального порта сервера. Протокол
    /// тот же, что у «Вместе» в моде: управляющее соединение NARE1 HOST, на каждого
    /// гостя — канал NARE1 DATA, который проксируется в 127.0.0.1:localPort.
    /// </summary>
    public class NareHost
    {
        private readonly string _name;      // «ник-id»
        private readonly int _localPort;    // порт локального MC-сервера
        private volatile bool _stop;
        private string _token = "";
        private int _publicPort;

        public string Name { get { return _name; } }
        public int PublicPort { get { return _publicPort; } }
        public bool Online { get { return _token.Length > 0 && _publicPort > 0; } }
        public Action<string> OnLog;
        public Action<string> OnCommand;     // команда из веб-консоли -> в stdin сервера
        public Func<string> StatsJson;        // "ram":..,"up":..,"players":.. для карточек веб-панели
        public string McVersion = "";         // версия Minecraft сервера (для анонса)
        public string Loader = "";            // ядро: paper/forge/fabric/vanilla

        private readonly System.Collections.Generic.List<string> _logBuf = new System.Collections.Generic.List<string>();
        private readonly object _logLock = new object();

        /// <summary>Строка консоли сервера для веб-консоли (буферизуется и улетает пачкой).</summary>
        public void PushLog(string line)
        {
            lock (_logLock)
            {
                _logBuf.Add(line);
                if (_logBuf.Count > 300) _logBuf.RemoveRange(0, _logBuf.Count - 300);
            }
        }

        public NareHost(string name, int localPort)
        {
            _name = name;
            _localPort = localPort;
        }

        private static string Host
        {
            get { int c = NareNet.Where.LastIndexOf(':'); return c > 0 ? NareNet.Where.Substring(0, c) : NareNet.Where; }
        }
        private static int Port
        {
            get { int c = NareNet.Where.LastIndexOf(':'); int p; return c > 0 && int.TryParse(NareNet.Where.Substring(c + 1), out p) ? p : 2445; }
        }

        public void Start()
        {
            _stop = false;
            new Thread(BeatLoop) { IsBackground = true, Name = "narehost-beat" }.Start();
            new Thread(ControlLoop) { IsBackground = true, Name = "narehost-control" }.Start();
            new Thread(RelayLoop) { IsBackground = true, Name = "narehost-relay" }.Start();
        }

        // Гонит консоль сервера в веб и тащит команды из веб-консоли обратно в stdin.
        private void RelayLoop()
        {
            while (!_stop)
            {
                try
                {
                    if (_token.Length > 0)
                    {
                        string[] batch = null;
                        lock (_logLock)
                        {
                            if (_logBuf.Count > 0) { batch = _logBuf.ToArray(); _logBuf.Clear(); }
                        }
                        var sb = new StringBuilder("{\"token\":\"").Append(Esc(_token)).Append("\",\"lines\":[");
                        if (batch != null)
                            for (int i = 0; i < batch.Length; i++)
                            {
                                if (i > 0) sb.Append(',');
                                sb.Append('"').Append(JsonStr(batch[i])).Append('"');
                            }
                        sb.Append(']');
                        var sp = StatsJson;
                        if (sp != null) { string st = null; try { st = sp(); } catch { } if (!string.IsNullOrEmpty(st)) sb.Append(",\"stats\":{").Append(st).Append('}'); }
                        sb.Append('}');
                        try { HttpPost("/server/push", sb.ToString()); } catch { }

                        try
                        {
                            string resp = HttpPost("/server/pull", "{\"token\":\"" + Esc(_token) + "\"}");
                            if (resp != null)
                            {
                                var j = Json.Parse(resp);
                                foreach (var c in j["commands"].Items)
                                {
                                    string cmd = c.AsString("");
                                    var h = OnCommand;
                                    if (h != null && cmd.Length > 0) h(cmd);
                                }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
                for (int i = 0; i < 15 && !_stop; i++) Thread.Sleep(100);   // ~1.5с
            }
        }

        public void Stop()
        {
            _stop = true;
            try { if (_token.Length > 0) HttpPost("/bye", "{\"token\":\"" + Esc(_token) + "\"}"); } catch { }
        }

        private void Log(string s) { var h = OnLog; if (h != null) h(s); }

        // --- периодическое объявление (держит комнату живой) ---
        private void BeatLoop()
        {
            while (!_stop)
            {
                try { Announce(); }
                catch (Exception e) { Log("объявление не прошло: " + e.Message); }
                for (int i = 0; i < 20 && !_stop; i++) Thread.Sleep(1000);
            }
        }

        private void Announce()
        {
            var sb = new StringBuilder();
            sb.Append('{');
            if (_token.Length > 0) sb.Append("\"token\":\"").Append(Esc(_token)).Append("\",");
            sb.Append("\"nick\":\"").Append(Esc(_name)).Append("\",");
            sb.Append("\"port\":").Append(_localPort).Append(',');
            sb.Append("\"mode\":\"tunnel\",\"version\":\"").Append(Esc(McVersion)).Append("\",\"loader\":\"").Append(Esc(Loader)).Append("\",");
            sb.Append("\"world\":\"сервер\",\"players\":0,\"max\":20,\"dedic\":true}");
            string resp = HttpPost("/announce", sb.ToString());
            if (resp == null) return;
            var j = Json.Parse(resp);
            string tok = j["token"].AsString("");
            if (tok.Length > 0) _token = tok;
            int pp = (int)j["port"].AsLong(0);
            if (pp > 0) _publicPort = pp;
        }

        // --- управляющее соединение ---
        private void ControlLoop()
        {
            while (!_stop)
            {
                if (_token.Length == 0) { Thread.Sleep(500); continue; }
                TcpClient control = null;
                try
                {
                    control = new TcpClient { NoDelay = true };
                    control.Connect(Host, Port);
                    var stream = control.GetStream();
                    WriteLine(stream, "NARE1 HOST " + _token);

                    var keeper = control;
                    new Thread(() =>
                    {
                        try { while (!_stop && keeper.Connected) { Thread.Sleep(15000); WriteLine(keeper.GetStream(), "{\"ping\":1}"); } }
                        catch { }
                    }) { IsBackground = true }.Start();

                    var reader = new StreamReader(stream, Encoding.UTF8);
                    string line;
                    while (!_stop && (line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (line.Length == 0) continue;
                        var j = Json.Parse(line);
                        string open = j["open"].AsString("");
                        if (open.Length > 0) Pair(open);
                    }
                }
                catch (Exception e) { if (!_stop) Log("тоннель оборвался: " + e.Message); }
                finally { try { if (control != null) control.Close(); } catch { } }
                if (!_stop) Thread.Sleep(2000);
            }
        }

        // --- канал данных для одного гостя ---
        private void Pair(string id)
        {
            new Thread(() =>
            {
                TcpClient up = null, down = null;
                try
                {
                    up = new TcpClient { NoDelay = true };
                    up.Connect(Host, Port);
                    WriteLine(up.GetStream(), "NARE1 DATA " + id);

                    down = new TcpClient { NoDelay = true };
                    down.Connect("127.0.0.1", _localPort);

                    var u = up; var d = down;
                    var t1 = new Thread(() => Pipe(u.GetStream(), d.GetStream())) { IsBackground = true };
                    var t2 = new Thread(() => Pipe(d.GetStream(), u.GetStream())) { IsBackground = true };
                    t1.Start(); t2.Start();
                    t1.Join();
                }
                catch (Exception e) { Log("канал не открылся: " + e.Message); }
                finally { try { if (up != null) up.Close(); } catch { } try { if (down != null) down.Close(); } catch { } }
            }) { IsBackground = true }.Start();
        }

        private static void Pipe(Stream from, Stream to)
        {
            var buf = new byte[16384];
            try { int n; while ((n = from.Read(buf, 0, buf.Length)) > 0) { to.Write(buf, 0, n); to.Flush(); } }
            catch { }
            try { to.Close(); } catch { }
        }

        private static void WriteLine(Stream s, string line)
        {
            var b = Encoding.UTF8.GetBytes(line + "\n");
            s.Write(b, 0, b.Length);
            s.Flush();
        }

        private static string Esc(string s) { return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\""); }

        private static string JsonStr(string s)
        {
            s = s ?? "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                if (c == '\\') sb.Append("\\\\");
                else if (c == '"') sb.Append("\\\"");
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else if (c < 0x20) sb.Append(' ');
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static string HttpPost(string route, string body)
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
    }
}
