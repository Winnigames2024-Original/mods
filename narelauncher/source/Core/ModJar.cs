using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace NareLauncher.Core
{
    /// <summary>
    /// Что мод сам про себя пишет: имя, версия, загрузчик и для каких версий
    /// игры он сделан.
    ///
    /// Читаем из тех же файлов, что читает сама игра: META-INF/mods.toml у
    /// Forge и NeoForge, fabric.mod.json у Fabric, quilt.mod.json у Quilt и
    /// совсем старый mcmod.info. Никакой магии: что мод написал, то и
    /// показываем.
    /// </summary>
    public class ModJar
    {
        public string Id = "";             // modId — по нему видно два одинаковых мода
        public string Name = "";
        public string Version = "";
        public string Loader = "";          // forge / neoforge / fabric / quilt / ""
        public string GameRaw = "";         // как записано в моде: [1.21,1.22) или ~1.20.1

        /// <summary>Удалось ли вообще что-то прочитать.</summary>
        public bool Known { get { return !string.IsNullOrEmpty(Loader) || !string.IsNullOrEmpty(GameRaw); } }

        /// <summary>Читаемое имя: как мод себя назвал, иначе его modId.</summary>
        public string Label
        {
            get { return !string.IsNullOrEmpty(Name) ? Name : Id; }
        }

        public string LoaderRu
        {
            get
            {
                switch (Loader)
                {
                    case "forge": return "Forge";
                    case "neoforge": return "NeoForge";
                    case "fabric": return "Fabric";
                    case "quilt": return "Quilt";
                    default: return "";
                }
            }
        }

        /// <summary>Короткая строка под именем мода в списке.</summary>
        public string Short
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(GameRaw)) parts.Add(Pretty(GameRaw));
                if (!string.IsNullOrEmpty(LoaderRu)) parts.Add(LoaderRu);
                return string.Join(" · ", parts.ToArray());
            }
        }

        // ------------------------------------------------------------ чтение

        private static readonly Dictionary<string, ModJar> Cache =
            new Dictionary<string, ModJar>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, long> Stamps =
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Читает описание мода из jar. Никогда не бросает исключений.</summary>
        public static ModJar Read(string path)
        {
            var empty = new ModJar();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return empty;

            long stamp;
            try { stamp = File.GetLastWriteTimeUtc(path).Ticks; }
            catch { return empty; }

            lock (Cache)
            {
                long was;
                ModJar had;
                if (Stamps.TryGetValue(path, out was) && was == stamp
                    && Cache.TryGetValue(path, out had)) return had;
            }

            var found = ReadNow(path);

            lock (Cache)
            {
                Cache[path] = found;
                Stamps[path] = stamp;
            }

            return found;
        }

        private static ModJar ReadNow(string path)
        {
            var info = new ModJar();

            try
            {
                using (var zip = ZipFile.OpenRead(path))
                {
                    string toml = Text(zip, "META-INF/mods.toml");
                    if (toml == null) toml = Text(zip, "META-INF/neoforge.mods.toml");

                    if (toml != null)
                    {
                        FromToml(info, toml);
                        if (string.IsNullOrEmpty(info.Loader))
                            info.Loader = Text(zip, "META-INF/neoforge.mods.toml") != null
                                ? "neoforge" : "forge";
                        return info;
                    }

                    string fabric = Text(zip, "fabric.mod.json");
                    if (fabric != null) { FromFabric(info, fabric); return info; }

                    string quilt = Text(zip, "quilt.mod.json");
                    if (quilt != null) { FromQuilt(info, quilt); return info; }

                    string old = Text(zip, "mcmod.info");
                    if (old != null) { FromOld(info, old); return info; }
                }
            }
            catch (Exception e)
            {
                Log.Warn("Не прочитал " + Path.GetFileName(path) + ": " + e.Message);
            }

            return info;
        }

        private static string Text(ZipArchive zip, string name)
        {
            foreach (var entry in zip.Entries)
            {
                if (!string.Equals(entry.FullName.Replace('\\', '/'), name,
                        StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
                        return reader.ReadToEnd();
                }
                catch { return null; }
            }

            return null;
        }

        // ------------------------------------------------------------ разбор

        /// <summary>mods.toml — обычный ini-подобный текст, читаем его построчно.</summary>
        private static void FromToml(ModJar info, string text)
        {
            info.Id = TomlValue(text, "modId");
            info.Name = TomlValue(text, "displayName");
            info.Version = TomlValue(text, "version");

            // зависимость от minecraft и говорит, под какие версии мод
            var minecraft = Regex.Match(text,
                @"modId\s*=\s*[""']minecraft[""'](?:.|\n|\r)*?versionRange\s*=\s*[""']([^""']+)[""']",
                RegexOptions.IgnoreCase);

            if (minecraft.Success) info.GameRaw = minecraft.Groups[1].Value.Trim();

            if (Regex.IsMatch(text, @"modId\s*=\s*[""']neoforge[""']", RegexOptions.IgnoreCase))
                info.Loader = "neoforge";
            else if (Regex.IsMatch(text, @"modId\s*=\s*[""']forge[""']", RegexOptions.IgnoreCase))
                info.Loader = "forge";
        }

        /// <summary>
        /// Значение ключа из mods.toml. Ключ ищем с начала строки: иначе
        /// «version» находится внутри «loaderVersion» и мод получает чужой
        /// номер версии.
        /// </summary>
        private static string TomlValue(string text, string key)
        {
            var m = Regex.Match(text, @"^[ \t]*" + Regex.Escape(key) + @"[ \t]*=[ \t]*[""']([^""']*)[""']",
                                RegexOptions.IgnoreCase | RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value.Trim() : "";
        }

        private static void FromFabric(ModJar info, string text)
        {
            info.Loader = "fabric";
            info.Id = JsonValue(text, "id");
            info.Name = JsonValue(text, "name");
            info.Version = JsonValue(text, "version");

            var m = Regex.Match(text, @"""minecraft""\s*:\s*""([^""]+)""");
            if (!m.Success) m = Regex.Match(text, @"""minecraft""\s*:\s*\[\s*""([^""]+)""");
            if (m.Success) info.GameRaw = m.Groups[1].Value.Trim();
        }

        private static void FromQuilt(ModJar info, string text)
        {
            info.Loader = "quilt";
            info.Id = JsonValue(text, "id");
            info.Name = JsonValue(text, "name");
            info.Version = JsonValue(text, "version");

            var m = Regex.Match(text, @"""id""\s*:\s*""minecraft""\s*,\s*""versions""\s*:\s*""([^""]+)""");
            if (m.Success) info.GameRaw = m.Groups[1].Value.Trim();
        }

        private static void FromOld(ModJar info, string text)
        {
            info.Loader = "forge";
            info.Id = JsonValue(text, "modid");
            info.Name = JsonValue(text, "name");
            info.Version = JsonValue(text, "version");
            info.GameRaw = JsonValue(text, "mcversion");
        }

        private static string JsonValue(string text, string key)
        {
            var m = Regex.Match(text, @"""" + key + @"""\s*:\s*""([^""]*)""");
            return m.Success ? m.Groups[1].Value.Trim() : "";
        }


        // ------------------------------------------- версия игры из её jar

        /// <summary>
        /// Версия игры, прочитанная из самого jar сборки. Клиентский jar
        /// Minecraft носит внутри version.json, где написан его номер —
        /// это надёжнее, чем угадывать по имени папки, которое игрок
        /// может назвать как угодно.
        /// </summary>
        public static string GameFromJar(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "";

            try
            {
                using (var zip = ZipFile.OpenRead(path))
                {
                    string json = Text(zip, "version.json");
                    if (json != null)
                    {
                        string id = JsonValue(json, "id");
                        if (Clean(id) != "") return Clean(id);

                        string name = JsonValue(json, "name");
                        if (Clean(name) != "") return Clean(name);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("Версию из " + Path.GetFileName(path) + " не прочитал: " + e.Message);
            }

            return "";
        }

        /// <summary>Чистый номер версии игры или пустая строка.</summary>
        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var m = Regex.Match(text.Trim(), @"^(\d+\.\d+(\.\d+)?)");
            return m.Success ? m.Groups[1].Value : "";
        }

        /// <summary>Годится ли строка как фильтр версии для каталогов модов.</summary>
        public static bool LooksLikeGameVersion(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            return Regex.IsMatch(text.Trim(), @"^[0-9][0-9A-Za-z\.\-_]*$");
        }

        // ------------------------------------------------------------ сверка

        /// <summary>
        /// Подойдёт ли мод к этой сборке. null — не поняли и молчим: лучше
        /// ничего не сказать, чем напугать зря.
        /// </summary>
        public bool? Fits(string gameVersion, string loader)
        {
            if (!Known) return null;

            if (!string.IsNullOrEmpty(Loader) && !string.IsNullOrEmpty(loader))
            {
                // Quilt понимает моды Fabric; Forge и NeoForge — уже нет
                bool sameFamily = string.Equals(Loader, loader, StringComparison.OrdinalIgnoreCase)
                        || (Loader == "fabric" && loader == "quilt");

                if (!sameFamily) return false;
            }

            if (string.IsNullOrEmpty(GameRaw) || string.IsNullOrEmpty(gameVersion)) return null;

            return Covers(GameRaw, gameVersion);
        }

        /// <summary>Попадает ли версия в то, что написал мод.</summary>
        public static bool? Covers(string raw, string version)
        {
            if (string.IsNullOrEmpty(raw) || string.IsNullOrEmpty(version)) return null;

            string text = raw.Trim();

            // прямое совпадение или перечисление
            foreach (var piece in text.Split(new[] { ' ', ',', '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string clean = piece.Trim('[', ']', '(', ')', '"', '\'');
                if (clean == version) return true;
            }

            // диапазон вида [1.21,1.22) — как пишет Forge
            var range = Regex.Match(text, @"([\[\(])\s*([0-9][0-9\.]*)?\s*,\s*([0-9][0-9\.]*)?\s*([\]\)])");
            if (range.Success)
            {
                string lowText = range.Groups[2].Value;
                string highText = range.Groups[3].Value;

                bool lowOk = string.IsNullOrEmpty(lowText)
                        || Compare(version, lowText) > (range.Groups[1].Value == "[" ? -1 : 0);

                bool highOk = string.IsNullOrEmpty(highText)
                        || Compare(version, highText) < (range.Groups[4].Value == "]" ? 1 : 0);

                return lowOk && highOk;
            }

            // ~1.20.1 и >=1.20 — как пишет Fabric
            var tilde = Regex.Match(text, @"^[~^]\s*([0-9][0-9\.]*)$");
            if (tilde.Success)
            {
                string want = tilde.Groups[1].Value;
                int cut = want.LastIndexOf('.');
                string family = cut > 0 ? want.Substring(0, cut) : want;
                return version == want || version.StartsWith(family + ".", StringComparison.Ordinal);
            }

            var atLeast = Regex.Match(text, @"^>=?\s*([0-9][0-9\.]*)$");
            if (atLeast.Success) return Compare(version, atLeast.Groups[1].Value) >= 0;

            // звёздочка значит «любая»
            if (text == "*" || text.Equals("any", StringComparison.OrdinalIgnoreCase)) return true;

            return null;
        }

        /// <summary>Сравнение версий по числам: 1.21.4 новее 1.21.</summary>
        public static int Compare(string a, string b)
        {
            var left = a.Split('.');
            var right = b.Split('.');
            int count = Math.Max(left.Length, right.Length);

            for (int i = 0; i < count; i++)
            {
                int x = i < left.Length ? Number(left[i]) : 0;
                int y = i < right.Length ? Number(right[i]) : 0;
                if (x != y) return x < y ? -1 : 1;
            }

            return 0;
        }

        private static int Number(string text)
        {
            int value = 0;
            foreach (char c in text)
            {
                if (!char.IsDigit(c)) break;
                value = value * 10 + (c - '0');
            }
            return value;
        }

        /// <summary>Диапазон человеческим языком: [1.21,1.22) -> 1.21.x</summary>
        public static string Pretty(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";

            var range = Regex.Match(raw, @"[\[\(]\s*([0-9][0-9\.]*)\s*,\s*([0-9][0-9\.]*)?\s*[\]\)]");
            if (range.Success)
            {
                string low = range.Groups[1].Value;
                string high = range.Groups[2].Value;

                if (string.IsNullOrEmpty(high)) return low + " и новее";

                int cut = low.LastIndexOf('.');
                string family = cut > 0 ? low.Substring(0, cut) : low;

                return high.StartsWith(family + ".", StringComparison.Ordinal) || high == family
                        ? low + "–" + high : low + " … " + high;
            }

            return raw.Trim('~', '^', '>', '=', ' ');
        }
    }
}
