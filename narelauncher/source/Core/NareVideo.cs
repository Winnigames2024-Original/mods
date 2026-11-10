using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace NareLauncher.Core
{
    /// <summary>
    /// Клиент видеосервиса NareVideo (как YouTube): лента, загрузка роликов,
    /// лайки/комментарии. Файлы роликов лежат на NareNet, стрим — /video/stream.
    /// </summary>
    public class NareVideo
    {
        public string Id = "", Nick = "", User = "", Title = "", Thumb = "", Mime = "";
        public long At;
        public int Views, Likes, Dur;

        public const string DOMAIN = "narelauncher.winnigames2024.ru";

        /// <summary>Веб-страница просмотра ролика (играет в браузере).</summary>
        public static string WatchUrl(string id) { return "http://" + DOMAIN + "/videos#/watch/" + id; }
        public static string ChannelUrl(string nick) { return "http://" + DOMAIN + "/videos#/channel/" + Uri.EscapeDataString(nick); }
        public string StreamUrl { get { return "http://" + NareNet.Where + "/video/stream/" + Id; } }

        private static NareVideo From(Json v)
        {
            return new NareVideo
            {
                Id = v["id"].AsString(""),
                Nick = v["nick"].AsString(""),
                User = v["user"].AsString(""),
                Title = v["title"].AsString(""),
                Thumb = v["thumb"].AsString(""),
                Mime = v["mime"].AsString("video/mp4"),
                Views = v["views"].AsInt(0),
                Likes = v["likes"].AsInt(0),
                At = v["at"].AsLong(0),
                Dur = v["dur"].AsInt(0)
            };
        }

        public static List<NareVideo> List(string channel, string q)
        {
            var list = new List<NareVideo>();
            string u = "http://" + NareNet.Where + "/video/list";
            var qs = new List<string>();
            if (!string.IsNullOrEmpty(channel)) qs.Add("channel=" + Uri.EscapeDataString(channel));
            if (!string.IsNullOrEmpty(q)) qs.Add("q=" + Uri.EscapeDataString(q));
            if (qs.Count > 0) u += "?" + string.Join("&", qs);
            string resp = HttpGet(u);
            if (resp == null) return list;
            var j = Json.Parse(resp);
            foreach (var v in j["videos"].Items) list.Add(From(v));
            return list;
        }

        /// <summary>Создать ролик и залить файл. progress: 0..100. Возвращает id.</summary>
        public static string Upload(string token, string path, string title, string desc, Action<int> progress)
        {
            string mime = MimeFor(path);
            string body = "{\"token\":\"" + Esc(token) + "\",\"title\":\"" + Esc(title) +
                          "\",\"desc\":\"" + Esc(desc) + "\",\"mime\":\"" + mime + "\"}";
            string cr = HttpPost("/video/create", body);
            if (cr == null) throw new Exception("нет связи с сервером");
            var j = Json.Parse(cr);
            string id = j["id"].AsString("");
            if (id.Length == 0) throw new Exception(j["error"].AsString("не удалось создать"));

            var req = (HttpWebRequest)WebRequest.Create("http://" + NareNet.Where + "/video/raw/" + id);
            req.Method = "PUT";
            req.ContentType = mime;
            req.Headers["x-nare-token"] = token;
            req.Timeout = 1000000;
            req.ReadWriteTimeout = 1000000;
            req.AllowWriteStreamBuffering = false;
            var fi = new FileInfo(path);
            req.ContentLength = fi.Length;

            using (var fs = File.OpenRead(path))
            using (var os = req.GetRequestStream())
            {
                var buf = new byte[65536];
                long sent = 0; int n; int last = -1;
                while ((n = fs.Read(buf, 0, buf.Length)) > 0)
                {
                    os.Write(buf, 0, n);
                    sent += n;
                    if (progress != null)
                    {
                        int pct = (int)(sent * 100 / Math.Max(1, fi.Length));
                        if (pct != last) { last = pct; progress(pct); }
                    }
                }
            }
            using (var r = (HttpWebResponse)req.GetResponse())
            using (var rd = new StreamReader(r.GetResponseStream(), Encoding.UTF8))
            {
                var rr = Json.Parse(rd.ReadToEnd());
                string err = rr["error"].AsString("");
                if (err.Length > 0) throw new Exception(err);
            }
            return id;
        }

        private static string MimeFor(string path)
        {
            string e = (Path.GetExtension(path) ?? "").ToLowerInvariant();
            if (e == ".webm") return "video/webm";
            if (e == ".ogg" || e == ".ogv") return "video/ogg";
            return "video/mp4";
        }

        private static string Esc(string s)
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

        private static string HttpPost(string route, string body)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("http://" + NareNet.Where + route);
                req.Method = "POST";
                req.ContentType = "application/json";
                req.Timeout = 15000;
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
