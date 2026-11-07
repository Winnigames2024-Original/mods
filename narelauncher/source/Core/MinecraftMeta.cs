using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NareLauncher.Core
{
    public class RemoteVersion
    {
        public string Id;
        public string Type;        // release / snapshot / old_beta / old_alpha
        public string Url;
        public string Sha1;
        public DateTime ReleaseTime;

        public string TypeRu
        {
            get
            {
                switch (Type)
                {
                    case "release": return "Релиз";
                    case "snapshot": return "Снапшот";
                    case "old_beta": return "Бета";
                    case "old_alpha": return "Альфа";
                    default: return Type;
                }
            }
        }
    }

    public static class MinecraftMeta
    {
        public const string ManifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";
        public const string ResourcesUrl = "https://resources.download.minecraft.net/";

        private static List<RemoteVersion> _cache;
        public static string LatestRelease = "";
        public static string LatestSnapshot = "";

        public static async Task<List<RemoteVersion>> GetManifestAsync(bool force = false,
            CancellationToken ct = default(CancellationToken))
        {
            if (_cache != null && !force) return _cache;

            string cacheFile = Path.Combine(Paths.CacheDir, "version_manifest.json");
            Json root = Json.Null;

            try
            {
                string text = await Net.GetStringAsync(ManifestUrl, ct).ConfigureAwait(false);
                root = Json.Parse(text);
                if (root.IsObject)
                {
                    Paths.EnsureForFile(cacheFile);
                    File.WriteAllText(cacheFile, text);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Манифест версий не загружен, пробуем кэш: " + ex.Message);
            }

            if (!root.IsObject) root = Json.ParseFile(cacheFile);
            if (!root.IsObject) return _cache ?? new List<RemoteVersion>();

            LatestRelease = root["latest"]["release"].AsString("");
            LatestSnapshot = root["latest"]["snapshot"].AsString("");

            var list = new List<RemoteVersion>();
            foreach (var v in root["versions"].Items)
            {
                DateTime rt;
                DateTime.TryParse(v["releaseTime"].AsString(""), out rt);
                list.Add(new RemoteVersion
                {
                    Id = v["id"].AsString(""),
                    Type = v["type"].AsString("release"),
                    Url = v["url"].AsString(""),
                    Sha1 = v["sha1"].AsString(null),
                    ReleaseTime = rt
                });
            }

            _cache = list;
            return list;
        }

        public static RemoteVersion FindRemote(string id)
        {
            if (_cache == null) return null;
            foreach (var v in _cache)
                if (v.Id == id) return v;
            return null;
        }

        /// <summary>Скачивает version.json, если его ещё нет локально.</summary>
        public static async Task<Json> EnsureVersionJsonAsync(string id, CancellationToken ct = default(CancellationToken))
        {
            string path = Paths.VersionJson(id);
            if (File.Exists(path))
            {
                var local = Json.ParseFile(path);
                if (local.IsObject) return local;
            }

            await GetManifestAsync(false, ct).ConfigureAwait(false);
            var remote = FindRemote(id);
            if (remote == null || string.IsNullOrEmpty(remote.Url))
                throw new Exception("Версия " + id + " не найдена ни локально, ни в манифесте Mojang.");

            string text = await Net.GetStringAsync(remote.Url, ct).ConfigureAwait(false);
            Paths.EnsureForFile(path);
            File.WriteAllText(path, text);
            return Json.Parse(text);
        }

        /// <summary>
        /// Загружает version.json и рекурсивно подмешивает родителей (inheritsFrom)
        /// — так работают Forge, Fabric, Quilt, NeoForge и ручные сборки.
        /// </summary>
        public static async Task<Json> ResolveAsync(string id, CancellationToken ct = default(CancellationToken))
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return await ResolveInner(id, seen, ct).ConfigureAwait(false);
        }

        private static async Task<Json> ResolveInner(string id, HashSet<string> seen, CancellationToken ct)
        {
            if (!seen.Add(id))
                throw new Exception("Циклическая зависимость версий: " + id);

            var child = await EnsureVersionJsonAsync(id, ct).ConfigureAwait(false);
            if (!child.IsObject) throw new Exception("Повреждён файл версии: " + id);

            string parentId = child["inheritsFrom"].AsString(null);
            if (string.IsNullOrEmpty(parentId)) return child;

            var parent = await ResolveInner(parentId, seen, ct).ConfigureAwait(false);
            return Merge(child, parent);
        }

        /// <summary>Слияние дочернего профиля с родительским по правилам Mojang.</summary>
        public static Json Merge(Json child, Json parent)
        {
            var result = parent.Clone();

            // Простые поля: дочерние перекрывают родительские
            string[] overrides =
            {
                "id", "mainClass", "minecraftArguments", "type", "releaseTime", "time",
                "assets", "assetIndex", "javaVersion", "logging", "complianceLevel",
                "minimumLauncherVersion", "jar"
            };
            foreach (var key in overrides)
                if (child.Has(key)) result[key] = child[key].Clone();

            // inheritsFrom должен указывать на ванильную версию (оттуда берётся client.jar).
            // Родительский профиль ближе к ванили, поэтому его значение важнее дочернего:
            // иначе у профиля поверх Forge jar искался бы в папке Forge, где его нет.
            if (!parent.Has("inheritsFrom") && child.Has("inheritsFrom"))
                result["inheritsFrom"] = child["inheritsFrom"].Clone();

            // Если ребёнок задал minecraftArguments — убираем унаследованный arguments,
            // иначе получим двойной набор аргументов.
            if (child.Has("minecraftArguments") && !child.Has("arguments"))
                result.Remove("arguments");

            // downloads: берём родительский (там client.jar), но дочерние ключи перекрывают
            if (child["downloads"].IsObject)
            {
                var dl = result["downloads"].IsObject ? result["downloads"] : Json.NewObject();
                foreach (var kv in child["downloads"].Pairs) dl[kv.Key] = kv.Value.Clone();
                result["downloads"] = dl;
            }

            // libraries: сначала дочерние (у них приоритет), затем родительские
            var libs = Json.NewArray();
            foreach (var l in child["libraries"].Items) libs.Add(l.Clone());
            foreach (var l in parent["libraries"].Items) libs.Add(l.Clone());
            result["libraries"] = libs;

            // arguments: родительские, затем дочерние
            if (child["arguments"].IsObject || parent["arguments"].IsObject)
            {
                var args = Json.NewObject();
                foreach (var section in new[] { "game", "jvm" })
                {
                    var arr = Json.NewArray();
                    foreach (var a in parent["arguments"][section].Items) arr.Add(a.Clone());
                    foreach (var a in child["arguments"][section].Items) arr.Add(a.Clone());
                    if (arr.Count > 0) args[section] = arr;
                }
                if (args.Count > 0) result["arguments"] = args;
            }

            return result;
        }

        /// <summary>Какая версия ванильного Minecraft лежит в основе (для модов и Java).</summary>
        public static string BaseGameVersion(Json resolved, string fallbackId)
        {
            string jar = resolved["jar"].AsString(null);
            if (!string.IsNullOrEmpty(jar)) return jar;

            string inherits = resolved["inheritsFrom"].AsString(null);
            if (!string.IsNullOrEmpty(inherits)) return inherits;

            string assets = resolved["assets"].AsString(null);
            string id = resolved["id"].AsString(fallbackId);

            // Пытаемся вытащить номер версии из id вида "1.20.1-forge-47.2.0"
            var m = System.Text.RegularExpressions.Regex.Match(id ?? "", @"(\d+\.\d+(\.\d+)?)");
            if (m.Success) return m.Groups[1].Value;

            return id ?? assets;
        }
    }
}
