using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace NareLauncher.Core
{
    public class NareEvent
    {
        public string Id = "", Name = "", Desc = "", Accent = "", NotifyTitle = "", NotifyText = "";
        public int Coins, Joins;
        public bool Active, Joined;
        public List<string> Images = new List<string>();
    }

    public class EventJoin
    {
        public bool Ok;
        public int Awarded, Coins;
        public string NotifyTitle = "", NotifyText = "", Name = "";
    }

    /// <summary>
    /// Эвенты NareLauncher: активные эвенты для приложения (монеты за заход +
    /// уведомление + картинки на главном меню) и админ-управление (создать,
    /// изменить, запустить/остановить, удалить, загрузить картинку).
    /// </summary>
    public static class NareEvents
    {
        public static string AbsUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            if (url.StartsWith("http://") || url.StartsWith("https://")) return url;
            return "http://" + NareNet.Where + (url.StartsWith("/") ? url : "/" + url);
        }

        private static NareEvent Parse(Json e)
        {
            var ev = new NareEvent
            {
                Id = e["id"].AsString(""),
                Name = e["name"].AsString(""),
                Desc = e["desc"].AsString(""),
                Accent = e["accent"].AsString(""),
                Coins = e["coins"].AsInt(0),
                Joins = e["joins"].AsInt(0),
                Active = e["active"].AsBool(false),
                Joined = e["joined"].AsBool(false),
                NotifyTitle = e["notifyTitle"].AsString(""),
                NotifyText = e["notifyText"].AsString("")
            };
            foreach (var u in e["images"].Items) ev.Images.Add(u.AsString(""));
            return ev;
        }

        // ---- пользователь ----
        public static List<NareEvent> Active(string token)
        {
            var list = new List<NareEvent>();
            string resp = Post("/events", "{\"token\":\"" + Esc(token) + "\"}");
            if (resp == null) return list;
            foreach (var e in Json.Parse(resp)["events"].Items) list.Add(Parse(e));
            return list;
        }

        public static EventJoin Join(string token, string id)
        {
            var r = new EventJoin();
            string resp = Post("/events/join", "{\"token\":\"" + Esc(token) + "\",\"id\":\"" + Esc(id) + "\"}");
            if (resp == null) return r;
            var j = Json.Parse(resp);
            r.Ok = j["ok"].AsBool(false);
            r.Awarded = j["awarded"].AsInt(0);
            r.Coins = j["coins"].AsInt(0);
            r.Name = j["name"].AsString("");
            r.NotifyTitle = j["notify"]["title"].AsString("");
            r.NotifyText = j["notify"]["text"].AsString("");
            return r;
        }

        // ---- админ ----
        public static List<NareEvent> All(string token)
        {
            var list = new List<NareEvent>();
            string resp = Post("/events/all", "{\"token\":\"" + Esc(token) + "\"}");
            if (resp == null) return list;
            var j = Json.Parse(resp);
            if (!j["ok"].AsBool(false)) throw new Exception(j["error"].AsString("нет доступа"));
            foreach (var e in j["events"].Items) list.Add(Parse(e));
            return list;
        }

        public static string Save(string token, NareEvent ev)
        {
            var sb = new StringBuilder("{\"token\":\"" + Esc(token) + "\"");
            if (!string.IsNullOrEmpty(ev.Id)) sb.Append(",\"id\":\"").Append(Esc(ev.Id)).Append('"');
            sb.Append(",\"name\":\"").Append(Esc(ev.Name)).Append('"');
            sb.Append(",\"desc\":\"").Append(Esc(ev.Desc)).Append('"');
            sb.Append(",\"accent\":\"").Append(Esc(ev.Accent)).Append('"');
            sb.Append(",\"coins\":").Append(ev.Coins);
            sb.Append(",\"notifyTitle\":\"").Append(Esc(ev.NotifyTitle)).Append('"');
            sb.Append(",\"notifyText\":\"").Append(Esc(ev.NotifyText)).Append('"');
            sb.Append(",\"images\":[");
            for (int i = 0; i < ev.Images.Count; i++) { if (i > 0) sb.Append(','); sb.Append('"').Append(Esc(ev.Images[i])).Append('"'); }
            sb.Append("]}");
            string resp = Post("/events/save", sb.ToString());
            if (resp == null) throw new Exception("нет связи с сервером");
            var j = Json.Parse(resp);
            string id = j["id"].AsString("");
            if (id.Length == 0) throw new Exception(j["error"].AsString("не сохранилось"));
            return id;
        }

        public static void SetState(string token, string id, bool active)
        {
            string resp = Post("/events/state", "{\"token\":\"" + Esc(token) + "\",\"id\":\"" + Esc(id) + "\",\"active\":" + (active ? "true" : "false") + "}");
            if (resp == null) throw new Exception("нет связи");
            var j = Json.Parse(resp);
            if (!j["ok"].AsBool(false)) throw new Exception(j["error"].AsString("ошибка"));
        }

        public static void Delete(string token, string id)
        {
            string resp = Post("/events/delete", "{\"token\":\"" + Esc(token) + "\",\"id\":\"" + Esc(id) + "\"}");
            if (resp == null) throw new Exception("нет связи");
            var j = Json.Parse(resp);
            if (!j["ok"].AsBool(false)) throw new Exception(j["error"].AsString("ошибка"));
        }

        public static string UploadImage(string token, string path)
        {
            string mime = MimeFor(path);
            var req = (HttpWebRequest)WebRequest.Create("http://" + NareNet.Where + "/events/img/upload");
            req.Method = "PUT";
            req.ContentType = mime;
            req.Headers["x-nare-token"] = token;
            req.Timeout = 60000; req.ReadWriteTimeout = 60000;
            var fi = new FileInfo(path);
            if (fi.Length > 8 * 1024 * 1024) throw new Exception("картинка больше 8 МБ");
            req.ContentLength = fi.Length;
            using (var fs = File.OpenRead(path))
            using (var os = req.GetRequestStream())
            {
                var buf = new byte[65536]; int n;
                while ((n = fs.Read(buf, 0, buf.Length)) > 0) os.Write(buf, 0, n);
            }
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                var j = Json.Parse(rd.ReadToEnd());
                string url = j["url"].AsString("");
                if (url.Length == 0) throw new Exception(j["error"].AsString("не загрузилось"));
                return url;
            }
        }

        private static string MimeFor(string p)
        {
            string e = (Path.GetExtension(p) ?? "").ToLowerInvariant();
            if (e == ".png") return "image/png";
            if (e == ".webp") return "image/webp";
            if (e == ".gif") return "image/gif";
            return "image/jpeg";
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
                else if (c < 0x20) b.Append(' ');
                else b.Append(c);
            }
            return b.ToString();
        }

        public static byte[] DownloadImage(string url)
        {
            try
            {
                using (var wc = new WebClient()) { wc.Headers.Add("User-Agent", "NareLauncher"); return wc.DownloadData(AbsUrl(url)); }
            }
            catch { return null; }
        }

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
            catch (WebException we)
            {
                try { using (var rd = new StreamReader(we.Response.GetResponseStream(), Encoding.UTF8)) return rd.ReadToEnd(); }
                catch { return null; }
            }
            catch { return null; }
        }
    }
}
