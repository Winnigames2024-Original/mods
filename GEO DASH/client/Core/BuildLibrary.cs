using System.Text.Json;

namespace GEO_DASH.Core
{
    /// <summary>Сохранённая конструкция: кусок уровня, который ставится одним кликом.</summary>
    public class BuildPiece
    {
        public string Name { get; set; } = "";
        public List<LevelObject> Objects { get; set; } = new();

        /// <summary>Размер конструкции в юнитах.</summary>
        public double Width => Objects.Count == 0 ? 30 : Objects.Max(o => o.X + ObjectCatalog.Get(o.TypeId).W) ;
        public double Height => Objects.Count == 0 ? 30 : Objects.Max(o => o.Y + ObjectCatalog.Get(o.TypeId).H);
    }

    /// <summary>
    /// Мои конструкции — как «custom objects» в игре: выделил свайпом, сохранил, ставишь целиком.
    /// Лежат в assets/builds.json.
    /// </summary>
    public static class BuildLibrary
    {
        static List<BuildPiece> _cache;
        static string FilePath => Path.Combine(AppConfig.DataDir, "builds.json");

        static readonly JsonSerializerOptions Opt = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public static List<BuildPiece> All()
        {
            if (_cache != null) return _cache;
            try
            {
                if (File.Exists(FilePath))
                    _cache = JsonSerializer.Deserialize<List<BuildPiece>>(File.ReadAllText(FilePath));
            }
            catch { }
            return _cache ??= new List<BuildPiece>();
        }

        static void Flush()
        {
            try { File.WriteAllText(FilePath, JsonSerializer.Serialize(All(), Opt)); } catch { }
        }

        /// <summary>Сохранить выделенное: координаты запоминаются от левого нижнего угла.</summary>
        public static BuildPiece Save(string name, IEnumerable<LevelObject> objects)
        {
            var list = objects?.Where(o => o != null).Select(o => o.Clone()).ToList() ?? new List<LevelObject>();
            if (list.Count == 0) return null;

            double minX = list.Min(o => o.X), minY = list.Min(o => o.Y);
            foreach (var o in list) { o.X -= minX; o.Y -= minY; o.Id = 0; }

            var all = All();
            all.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            var piece = new BuildPiece { Name = name, Objects = list };
            all.Add(piece);
            Flush();
            return piece;
        }

        public static void Delete(string name)
        {
            All().RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            Flush();
        }

        public static BuildPiece Get(string name)
            => All().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>Объекты конструкции, сдвинутые в нужную точку.</summary>
        public static List<LevelObject> Stamp(BuildPiece piece, double x, double y)
        {
            var res = new List<LevelObject>();
            if (piece == null) return res;
            foreach (var o in piece.Objects)
            {
                var c = o.Clone();
                c.Id = 0;
                c.X += x; c.Y += y;
                res.Add(c);
            }
            return res;
        }
    }
}
