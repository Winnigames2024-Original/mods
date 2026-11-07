using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace NareLauncher.Core
{
    public static class LibraryUtil
    {
        public const string MojangLibs = "https://libraries.minecraft.net/";

        public static bool Is64Bit { get { return IntPtr.Size == 8; } }

        /// <summary>Проверка блока "rules" (ОС, архитектура, фичи).</summary>
        public static bool RulesAllow(Json rules, Dictionary<string, bool> features = null)
        {
            if (!rules.IsArray || rules.Count == 0) return true;

            bool allowed = false;
            foreach (var rule in rules.Items)
            {
                bool match = true;

                var os = rule["os"];
                if (os.IsObject)
                {
                    string name = os["name"].AsString(null);
                    if (!string.IsNullOrEmpty(name) && name != "windows") match = false;

                    string arch = os["arch"].AsString(null);
                    if (match && !string.IsNullOrEmpty(arch))
                    {
                        bool wantX86 = arch == "x86";
                        bool wantX64 = arch == "x64" || arch == "x86_64";
                        if (wantX86 && Is64Bit) match = false;
                        if (wantX64 && !Is64Bit) match = false;
                        if (arch == "arm64" || arch == "aarch64") match = false;
                    }

                    string ver = os["version"].AsString(null);
                    if (match && !string.IsNullOrEmpty(ver))
                    {
                        try
                        {
                            string winVer = Environment.OSVersion.Version.Major + "." +
                                            Environment.OSVersion.Version.Minor;
                            if (!Regex.IsMatch(winVer, ver)) match = false;
                        }
                        catch { }
                    }
                }

                var feats = rule["features"];
                if (match && feats.IsObject)
                {
                    foreach (var kv in feats.Pairs)
                    {
                        bool want = kv.Value.AsBool(false);
                        bool have = false;
                        if (features != null) features.TryGetValue(kv.Key, out have);
                        if (want != have) { match = false; break; }
                    }
                }

                if (match)
                    allowed = rule["action"].AsString("allow") == "allow";
            }

            return allowed;
        }

        /// <summary>group:artifact:version[:classifier][@ext] -> относительный путь в libraries.</summary>
        public static string MavenToPath(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            string ext = "jar";
            int at = name.IndexOf('@');
            if (at >= 0)
            {
                ext = name.Substring(at + 1);
                name = name.Substring(0, at);
            }

            var parts = name.Split(':');
            if (parts.Length < 3) return null;

            string group = parts[0].Replace('.', '/');
            string artifact = parts[1];
            string version = parts[2];
            string classifier = parts.Length > 3 ? parts[3] : null;

            string file = artifact + "-" + version;
            if (!string.IsNullOrEmpty(classifier)) file += "-" + classifier;
            file += "." + ext;

            return group + "/" + artifact + "/" + version + "/" + file;
        }

        public static string LocalPath(string relative)
        {
            if (string.IsNullOrEmpty(relative)) return null;
            return Path.Combine(Paths.LibrariesDir, relative.Replace('/', '\\'));
        }

        /// <summary>Ключ классификатора нативов для Windows или null.</summary>
        public static string NativeKey(Json lib)
        {
            var natives = lib["natives"];
            if (!natives.IsObject) return null;
            string key = natives["windows"].AsString(null);
            if (string.IsNullOrEmpty(key)) return null;
            return key.Replace("${arch}", Is64Bit ? "64" : "32");
        }

        /// <summary>Новый формат (1.19+): натив оформлен как обычная библиотека с классификатором в имени.</summary>
        public static bool IsNewStyleNative(Json lib)
        {
            string name = lib["name"].AsString("");
            return name.IndexOf(":natives-", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public class LibFile
        {
            public string Url;
            public string Path;
            public string Sha1;
            public long Size;
            public bool Native;      // требуется распаковка в natives
            public bool Classpath;   // добавляется в -cp
            public Json ExtractRules;
            public string Name;
        }

        /// <summary>Разбирает одну библиотеку в список файлов для скачивания.</summary>
        public static List<LibFile> Resolve(Json lib, Dictionary<string, bool> features = null)
        {
            var result = new List<LibFile>();
            if (!lib.IsObject) return result;

            if (!RulesAllow(lib["rules"], features)) return result;

            string name = lib["name"].AsString("");
            var downloads = lib["downloads"];
            string nativeKey = NativeKey(lib);

            // --- основной артефакт ---
            var artifact = downloads["artifact"];
            if (artifact.IsObject)
            {
                string rel = artifact["path"].AsString(MavenToPath(name));
                string local = LocalPath(rel);
                if (local != null)
                {
                    result.Add(new LibFile
                    {
                        Name = name,
                        Url = artifact["url"].AsString(null),
                        Path = local,
                        Sha1 = artifact["sha1"].AsString(null),
                        Size = artifact["size"].AsLong(0),
                        Native = IsNewStyleNative(lib),
                        Classpath = nativeKey == null,
                        ExtractRules = lib["extract"]
                    });
                }
            }
            else if (nativeKey == null && !string.IsNullOrEmpty(name))
            {
                // Forge/кастомные библиотеки без секции downloads: собираем URL из поля "url" или maven Mojang
                string rel = MavenToPath(name);
                string local = LocalPath(rel);
                if (local != null)
                {
                    string baseUrl = lib["url"].AsString(MojangLibs);
                    if (!baseUrl.EndsWith("/")) baseUrl += "/";
                    result.Add(new LibFile
                    {
                        Name = name,
                        Url = baseUrl + rel,
                        Path = local,
                        Sha1 = lib["checksums"][0].AsString(null),
                        Size = 0,
                        Native = false,
                        Classpath = true,
                        ExtractRules = lib["extract"]
                    });
                }
            }

            // --- нативы старого формата ---
            if (nativeKey != null)
            {
                var cls = downloads["classifiers"][nativeKey];
                string rel = cls["path"].AsString(MavenToPath(name + ":" + nativeKey));
                string local = LocalPath(rel);
                if (local != null)
                {
                    string url = cls["url"].AsString(null);
                    if (string.IsNullOrEmpty(url))
                    {
                        string baseUrl = lib["url"].AsString(MojangLibs);
                        if (!baseUrl.EndsWith("/")) baseUrl += "/";
                        url = baseUrl + rel;
                    }
                    result.Add(new LibFile
                    {
                        Name = name + ":" + nativeKey,
                        Url = url,
                        Path = local,
                        Sha1 = cls["sha1"].AsString(null),
                        Size = cls["size"].AsLong(0),
                        Native = true,
                        Classpath = false,
                        ExtractRules = lib["extract"]
                    });
                }
            }

            return result;
        }

        /// <summary>Все файлы библиотек версии.</summary>
        public static List<LibFile> ResolveAll(Json versionJson, Dictionary<string, bool> features = null)
        {
            var list = new List<LibFile>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenArtifact = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var lib in versionJson["libraries"].Items)
            {
                foreach (var f in Resolve(lib, features))
                {
                    if (f.Path == null) continue;
                    if (!seen.Add(f.Path)) continue;

                    // Если одна и та же библиотека встречается в разных версиях (Forge + ваниль),
                    // приоритет у той, что объявлена раньше (child json идёт первым).
                    string key = GroupArtifactKey(f.Name);
                    if (key != null && f.Classpath)
                    {
                        int existing;
                        if (seenArtifact.TryGetValue(key, out existing))
                        {
                            list[existing].Classpath = true;
                            f.Classpath = false;
                        }
                        else seenArtifact[key] = list.Count;
                    }

                    list.Add(f);
                }
            }
            return list;
        }

        private static string GroupArtifactKey(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var parts = name.Split(':');
            if (parts.Length < 2) return null;
            // классификатор входит в ключ, иначе natives-вариант «съел» бы обычную библиотеку
            string key = parts[0] + ":" + parts[1];
            if (parts.Length > 3) key += ":" + parts[3];
            return key;
        }
    }
}
