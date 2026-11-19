using System.Text.Json;

namespace GEO_DASH.Audio
{
    /// <summary>Что уже скачано в assets/songs: список файлов плюс запомненные названия треков.</summary>
    public static class SongLibrary
    {
        public class Entry
        {
            public int Id;
            public string Name = "";
            public string Artist = "";
            public long Size;
            public bool BuiltIn;
            public string Path = "";
            public string SizeText => Size >= 1024 * 1024
                ? (Size / 1024.0 / 1024.0).ToString("0.0") + " МБ"
                : (Size / 1024.0).ToString("0") + " КБ";
        }

        class Meta { public string Name { get; set; } = ""; public string Artist { get; set; } = ""; }

        static Dictionary<string, Meta> _meta;
        static string MetaPath => Path.Combine(AppConfig.MusicDir, "songs.json");

        static Dictionary<string, Meta> Load()
        {
            if (_meta != null) return _meta;
            try
            {
                if (File.Exists(MetaPath))
                    _meta = JsonSerializer.Deserialize<Dictionary<string, Meta>>(File.ReadAllText(MetaPath));
            }
            catch { }
            return _meta ??= new Dictionary<string, Meta>();
        }

        /// <summary>Запомнить название трека, чтобы оно было видно и офлайн.</summary>
        public static void Remember(int id, string name, string artist)
        {
            if (id == 0 || (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(artist))) return;
            var m = Load();
            m[id.ToString()] = new Meta { Name = name ?? "", Artist = artist ?? "" };
            try { File.WriteAllText(MetaPath, JsonSerializer.Serialize(m, new JsonSerializerOptions { WriteIndented = true })); }
            catch { }
        }

        public static (string name, string artist) Known(int id)
            => Load().TryGetValue(id.ToString(), out var m) ? (m.Name, m.Artist) : ("", "");

        /// <summary>Все скачанные песни: встроенные (builtinN.wav) и по ID (N.mp3).</summary>
        public static List<Entry> Scan()
        {
            var list = new List<Entry>();
            string dir;
            try { dir = AppConfig.MusicDir; } catch { return list; }

            string[] files;
            try { files = Directory.GetFiles(dir); } catch { return list; }

            foreach (var f in files)
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext != ".mp3" && ext != ".wav" && ext != ".ogg") continue;
                var stem = Path.GetFileNameWithoutExtension(f);
                var e = new Entry { Path = f };
                try { e.Size = new FileInfo(f).Length; } catch { }

                if (stem.StartsWith("builtin", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(stem.Substring(7), out int bid))
                {
                    e.BuiltIn = true;
                    e.Id = -bid;
                    var t = Soundtrack.Tracks.FirstOrDefault(x => x.Id == e.Id);
                    e.Name = t?.Name ?? ("Встроенный трек " + bid);
                    e.Artist = t?.Artist ?? AppConfig.Brand;
                }
                else if (int.TryParse(stem, out int id))
                {
                    e.Id = id;
                    var (n, a) = Known(id);
                    e.Name = string.IsNullOrEmpty(n) ? "Трек #" + id : n;
                    e.Artist = a;
                }
                else continue;

                list.Add(e);
            }
            return list.OrderBy(x => x.BuiltIn ? 0 : 1).ThenBy(x => Math.Abs(x.Id)).ToList();
        }
    }
}
