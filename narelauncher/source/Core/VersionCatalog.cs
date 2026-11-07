using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace NareLauncher.Core
{
    public class InstalledVersion
    {
        public string Id = "";
        public string BaseVersion = "";     // ванильная версия в основе
        public string Loader = "vanilla";   // vanilla / fabric / quilt / forge / neoforge / custom
        public string LoaderVersion = "";
        public bool HasJar;

        public string LoaderRu
        {
            get
            {
                switch (Loader)
                {
                    case "fabric": return "Fabric";
                    case "quilt": return "Quilt";
                    case "forge": return "Forge";
                    case "neoforge": return "NeoForge";
                    case "vanilla": return "Ваниль";
                    default: return "Своя сборка";
                }
            }
        }

        public bool SupportsMods
        {
            get { return Loader == "fabric" || Loader == "quilt" || Loader == "forge" || Loader == "neoforge"; }
        }

        /// <summary>Идентификатор загрузчика для Modrinth.</summary>
        public string ModrinthLoader
        {
            get
            {
                switch (Loader)
                {
                    case "fabric": return "fabric";
                    case "quilt": return "quilt";
                    case "forge": return "forge";
                    case "neoforge": return "neoforge";
                    default: return null;
                }
            }
        }

        public override string ToString()
        {
            return Id;
        }
    }

    public static class VersionCatalog
    {
        /// <summary>Сканирует папку versions — включая вручную поставленные и кастомные сборки.</summary>
        public static List<InstalledVersion> Scan()
        {
            var list = new List<InstalledVersion>();
            try
            {
                if (!Directory.Exists(Paths.VersionsDir)) return list;

                foreach (var dir in Directory.GetDirectories(Paths.VersionsDir))
                {
                    string id = Path.GetFileName(dir);
                    string jsonPath = Path.Combine(dir, id + ".json");

                    if (!File.Exists(jsonPath))
                    {
                        // иногда json назван иначе — берём первый подходящий
                        var any = Directory.GetFiles(dir, "*.json");
                        if (any.Length == 0) continue;
                        jsonPath = any[0];
                    }

                    var raw = Json.ParseFile(jsonPath);
                    if (!raw.IsObject) continue;

                    var v = new InstalledVersion
                    {
                        Id = id,
                        HasJar = File.Exists(Path.Combine(dir, id + ".jar"))
                    };

                    DetectLoader(raw, id, v);
                    list.Add(v);
                }
            }
            catch (Exception ex) { Log.Error("Не удалось прочитать список версий", ex); }

            list.Sort((a, b) => string.Compare(b.Id, a.Id, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        public static InstalledVersion Find(string id)
        {
            foreach (var v in Scan())
                if (string.Equals(v.Id, id, StringComparison.OrdinalIgnoreCase)) return v;
            return null;
        }

        public static bool Exists(string id)
        {
            return !string.IsNullOrEmpty(id) && File.Exists(Paths.VersionJson(id));
        }

        private static void DetectLoader(Json raw, string id, InstalledVersion v)
        {
            // Профиль может наследоваться от другого профиля (например, сборка поверх Forge),
            // поэтому признаки загрузчика собираем по всей цепочке inheritsFrom.
            string mainClass = raw["mainClass"].AsString("");
            var names = new List<string>();
            string root = raw["inheritsFrom"].AsString(null);

            var current = raw;
            for (int hop = 0; hop < 6; hop++)
            {
                foreach (var lib in current["libraries"].Items)
                {
                    string n = lib["name"].AsString("");
                    if (!string.IsNullOrEmpty(n)) names.Add(n);
                }
                if (string.IsNullOrEmpty(mainClass))
                    mainClass = current["mainClass"].AsString("");

                string next = current["inheritsFrom"].AsString(null);
                if (string.IsNullOrEmpty(next)) break;

                root = next;
                var parent = Json.ParseFile(Paths.VersionJson(next));
                if (!parent.IsObject) break;
                current = parent;
            }

            v.BaseVersion = !string.IsNullOrEmpty(root) ? root : raw["jar"].AsString(id);
            string blob = mainClass + "\n" + string.Join("\n", names.ToArray());

            if (blob.IndexOf("net.fabricmc:fabric-loader", StringComparison.OrdinalIgnoreCase) >= 0 ||
                mainClass.IndexOf("net.fabricmc", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                v.Loader = "fabric";
                v.LoaderVersion = ExtractVersion(blob, "net.fabricmc:fabric-loader");
            }
            else if (blob.IndexOf("org.quiltmc:quilt-loader", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     mainClass.IndexOf("org.quiltmc", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                v.Loader = "quilt";
                v.LoaderVersion = ExtractVersion(blob, "org.quiltmc:quilt-loader");
            }
            else if (blob.IndexOf("net.neoforged", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                v.Loader = "neoforge";
                v.LoaderVersion = ExtractVersion(blob, "net.neoforged:neoforge");
            }
            else if (blob.IndexOf("net.minecraftforge:forge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     blob.IndexOf("net.minecraftforge:fmlloader", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     mainClass.IndexOf("fml", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     mainClass.IndexOf("minecraftforge", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                v.Loader = "forge";
                v.LoaderVersion = ExtractVersion(blob, "net.minecraftforge:forge");
            }
            else if (!string.IsNullOrEmpty(root))
            {
                v.Loader = "custom";
            }
            else
            {
                v.Loader = "vanilla";
            }

            if (string.IsNullOrEmpty(v.BaseVersion) || v.BaseVersion == id)
            {
                var m = Regex.Match(id, @"(\d+\.\d+(\.\d+)?)");
                if (m.Success && v.Loader != "vanilla") v.BaseVersion = m.Groups[1].Value;
                else v.BaseVersion = id;
            }

            // BaseVersion может оказаться вида "1.21.4-forge-54.1.0" — для Modrinth
            // и подбора Java нужен чистый номер версии игры.
            if (v.Loader != "vanilla")
            {
                var clean = Regex.Match(v.BaseVersion ?? "", @"^(\d+\.\d+(\.\d+)?)");
                if (clean.Success) v.BaseVersion = clean.Groups[1].Value;
            }

            // Имя сборки игрок придумывает сам — в нём может не быть номера
            // версии вообще («Моя сборка»). Тогда спрашиваем у самого jar:
            // внутри клиентского jar лежит version.json со своим номером.
            if (ModJar.Clean(v.BaseVersion) == "")
            {
                string fromJar = ModJar.GameFromJar(Paths.VersionJar(id));

                if (fromJar == "" && !string.IsNullOrEmpty(root))
                    fromJar = ModJar.GameFromJar(Paths.VersionJar(root));

                if (fromJar == "")
                {
                    // сборка может наследоваться через несколько звеньев
                    string hop = raw["inheritsFrom"].AsString(null);
                    for (int i = 0; i < 6 && !string.IsNullOrEmpty(hop) && fromJar == ""; i++)
                    {
                        fromJar = ModJar.GameFromJar(Paths.VersionJar(hop));
                        var up = Json.ParseFile(Paths.VersionJson(hop));
                        hop = up.IsObject ? up["inheritsFrom"].AsString(null) : null;
                    }
                }

                if (fromJar != "")
                {
                    Log.Info("Версия игры для «" + id + "» взята из jar: " + fromJar);
                    v.BaseVersion = fromJar;
                }
            }
        }

        private static string ExtractVersion(string blob, string prefix)
        {
            var m = Regex.Match(blob, Regex.Escape(prefix) + @":([^\s:]+)", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : "";
        }

        /// <summary>
        /// Импорт кастомной версии: папка с &lt;id&gt;.json либо ZIP-архив такой папки.
        /// </summary>
        public static string Import(string path)
        {
            if (Directory.Exists(path))
            {
                string id = Path.GetFileName(path.TrimEnd('\\'));
                string target = Paths.VersionDir(id);
                CopyDir(path, target);
                if (!File.Exists(Paths.VersionJson(id)))
                    throw new Exception("В папке нет файла " + id + ".json");
                return id;
            }

            if (File.Exists(path) && Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                string temp = Path.Combine(Paths.CacheDir, "import-" + Guid.NewGuid().ToString("N"));
                Paths.Ensure(temp);
                ZipFile.ExtractToDirectory(path, temp);

                string root = temp;
                var dirs = Directory.GetDirectories(temp);
                var files = Directory.GetFiles(temp, "*.json");
                if (files.Length == 0 && dirs.Length == 1) root = dirs[0];

                var json = Directory.GetFiles(root, "*.json");
                if (json.Length == 0) throw new Exception("В архиве не найден файл версии (*.json)");

                string id = Path.GetFileNameWithoutExtension(json[0]);
                CopyDir(root, Paths.VersionDir(id));

                try { Directory.Delete(temp, true); } catch { }
                return id;
            }

            if (File.Exists(path) && Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                string id = Path.GetFileNameWithoutExtension(path);
                string target = Paths.VersionJson(id);
                Paths.EnsureForFile(target);
                File.Copy(path, target, true);

                string srcJar = Path.ChangeExtension(path, ".jar");
                if (File.Exists(srcJar)) File.Copy(srcJar, Paths.VersionJar(id), true);
                return id;
            }

            throw new Exception("Выберите папку версии, её .json или ZIP-архив.");
        }

        private static void CopyDir(string from, string to)
        {
            Paths.Ensure(to);
            foreach (var f in Directory.GetFiles(from))
                File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
            foreach (var d in Directory.GetDirectories(from))
                CopyDir(d, Path.Combine(to, Path.GetFileName(d)));
        }

        public static void Delete(string id, bool withInstance)
        {
            try
            {
                string dir = Paths.VersionDir(id);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);

                if (withInstance)
                {
                    string inst = Path.Combine(Paths.InstancesDir, Paths.SafeName(id));
                    if (Directory.Exists(inst)) Directory.Delete(inst, true);
                }
            }
            catch (Exception ex) { Log.Error("Не удалось удалить версию " + id, ex); }
        }
    }
}
