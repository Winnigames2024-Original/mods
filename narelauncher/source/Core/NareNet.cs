using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NareLauncher.Core
{
    /// <summary>Мод, который стоит у хозяина комнаты.</summary>
    public class RoomMod
    {
        public string Id = "";
        public string Name = "";
        public string Version = "";
        public string File = "";
        public string Sha1 = "";
        public long Size;
        public bool Need = true;

        public string Title
        {
            get
            {
                if (!string.IsNullOrEmpty(Name)) return Name;
                if (!string.IsNullOrEmpty(Id)) return Id;
                return File;
            }
        }
    }

    /// <summary>Открытый мир, который кто-то показал на сборном пункте.</summary>
    public class Room
    {
        public string Id = "";
        public string Nick = "";
        public string Ip = "";
        public int Port;
        public string Mode = "direct";      // direct или tunnel
        public bool? Reachable;
        public string Version = "";
        public string Loader = "";
        public string World = "";
        public string Note = "";
        public int Players;
        public int Max;
        public int Age;
        public List<RoomMod> Mods = new List<RoomMod>();

        public string PackName = "";
        public string PackSha1 = "";
        public long PackSize;

        public bool Tunnel { get { return Mode == "tunnel"; } }

        /// <summary>Куда подключаться. У туннеля это адрес самого сборного пункта.</summary>
        public string Address
        {
            get
            {
                string host = Tunnel ? NareNet.HostOnly : Ip;
                if (string.IsNullOrEmpty(host) || Port <= 0) return "";
                return host + ":" + Port;
            }
        }

        public string Where
        {
            get
            {
                if (Tunnel) return "через наш сервер";
                if (Reachable == false) return "порт закрыт";
                if (Reachable == true) return "напрямую";
                return "проверяется";
            }
        }

        /// <summary>Можно ли вообще зайти: закрытый порт без туннеля — нельзя.</summary>
        public bool Joinable
        {
            get { return Address.Length > 0 && (Tunnel || Reachable != false); }
        }
    }

    /// <summary>
    /// Разговор со сборным пунктом NareNet: список открытых миров и выдача
    /// модов по sha1.
    ///
    /// Туннель держит мод в самой игре — он знает, когда мир открыт и на
    /// каком порту. Лаунчеру остаётся гостевая часть.
    /// </summary>
    public static class NareNet
    {
        public static string Where
        {
            get
            {
                string value = (Settings.Current.NareNetHost ?? "").Trim();
                return value.Length == 0 ? "151.245.217.104:2445" : value;
            }
        }

        public static string HostOnly
        {
            get
            {
                string value = Where;
                int colon = value.LastIndexOf(':');
                return colon > 0 ? value.Substring(0, colon) : value;
            }
        }

        /// <summary>Все сборные пункты, которые знает лаунчер. Выбранный — первым в списке настроек.</summary>
        public static List<string> Known()
        {
            var list = new List<string>();

            foreach (var part in (Settings.Current.NareNetHosts ?? "").Split(';', ','))
            {
                string one = part.Trim();
                if (one.Length == 0) continue;
                if (!list.Contains(one)) list.Add(one);
            }

            string active = Where;
            if (!list.Contains(active)) list.Insert(0, active);

            return list;
        }

        /// <summary>Переключиться на другой сборный пункт.</summary>
        public static void Use(string host)
        {
            string one = (host ?? "").Trim();
            if (one.Length == 0) return;

            Settings.Current.NareNetHost = one;

            var list = Known();
            if (!list.Contains(one)) list.Insert(0, one);
            Settings.Current.NareNetHosts = string.Join(";", list.ToArray());

            Settings.Save();
            Log.Info("Сборный пункт: " + one);
        }

        private static string Url(string route)
        {
            return "http://" + Where + route;
        }

        // ------------------------------------------------------------- список

        public static async Task<List<Room>> RoomsAsync(CancellationToken ct)
        {
            var json = await Net.GetJsonAsync(Url("/rooms"), ct).ConfigureAwait(false);
            var list = new List<Room>();

            foreach (var raw in json["rooms"].Items)
            {
                var room = new Room
                {
                    Id = raw["id"].AsString(""),
                    Nick = raw["nick"].AsString("Игрок"),
                    Ip = raw["ip"].AsString(""),
                    Port = raw["port"].AsInt(0),
                    Mode = raw["mode"].AsString("direct"),
                    Version = raw["version"].AsString(""),
                    Loader = raw["loader"].AsString(""),
                    World = raw["world"].AsString(""),
                    Note = raw["note"].AsString(""),
                    Players = raw["players"].AsInt(0),
                    Max = raw["max"].AsInt(0),
                    Age = raw["age"].AsInt(0)
                };

                var reachable = raw["reachable"];
                if (reachable.Type == JsonType.Bool) room.Reachable = reachable.AsBool(false);

                foreach (var m in raw["mods"].Items)
                    room.Mods.Add(new RoomMod
                    {
                        Id = m["id"].AsString(""),
                        Name = m["name"].AsString(""),
                        Version = m["version"].AsString(""),
                        File = m["file"].AsString(""),
                        Sha1 = m["sha1"].AsString(""),
                        Size = m["size"].AsLong(0),
                        Need = m["need"].AsBool(true)
                    });

                var pack = raw["pack"];
                if (pack.IsObject)
                {
                    room.PackName = pack["name"].AsString("");
                    room.PackSha1 = pack["sha1"].AsString("");
                    room.PackSize = pack["size"].AsLong(0);
                }

                list.Add(room);
            }

            return list;
        }

        public static async Task<string> PingAsync(CancellationToken ct)
        {
            var json = await Net.GetJsonAsync(Url("/ping"), ct).ConfigureAwait(false);
            if (!json["ok"].AsBool(false)) throw new Exception("сборный пункт ответил непонятно");

            return "NareNet " + json["version"].AsString("?") + ", комнат: " + json["rooms"].AsInt(0);
        }

        // -------------------------------------------------------------- файлы

        public static async Task<bool> HasFileAsync(string sha1, CancellationToken ct)
        {
            if (!Ok(sha1)) return false;

            using (var request = new HttpRequestMessage(HttpMethod.Head, Url("/file/" + sha1)))
            using (var response = await Net.Client.SendAsync(request, ct).ConfigureAwait(false))
                return response.IsSuccessStatusCode;
        }

        public static async Task GetFileAsync(string sha1, string target, CancellationToken ct)
        {
            if (!Ok(sha1)) throw new Exception("это не sha1");
            await Net.DownloadFileAsync(Url("/file/" + sha1), target, null, ct).ConfigureAwait(false);

            string real = Sha1Of(target);
            if (!string.Equals(real, sha1, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(target); } catch { }
                throw new Exception("скачанный файл не сошёлся по sha1");
            }
        }

        /// <summary>Выкладывает файл, если его там ещё нет.</summary>
        public static async Task<bool> PutFileAsync(string path, CancellationToken ct)
        {
            if (!File.Exists(path)) return false;

            string sha1 = Sha1Of(path);
            if (await HasFileAsync(sha1, ct).ConfigureAwait(false)) return true;

            using (var stream = File.OpenRead(path))
            using (var content = new StreamContent(stream))
            using (var request = new HttpRequestMessage(HttpMethod.Put, Url("/file/" + sha1)) { Content = content })
            using (var response = await Net.Client.SendAsync(request, ct).ConfigureAwait(false))
            {
                if (response.IsSuccessStatusCode) return true;

                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Log.Warn("Файл не выложился: " + (int)response.StatusCode + " " + body);
                return false;
            }
        }

        public static string Sha1Of(string path)
        {
            using (var sha = SHA1.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static bool Ok(string sha1)
        {
            if (string.IsNullOrEmpty(sha1) || sha1.Length != 40) return false;

            foreach (char c in sha1)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;

            return true;
        }

        // ------------------------------------------------------ доставка модов

        public class Missing
        {
            public RoomMod Mod;
            public string Why = "";
        }

        /// <summary>Есть ли уже такой мод в сборке — ищем по sha1, а не по имени.</summary>
        public static string FindInBuild(string versionId, string sha1)
        {
            if (!Ok(sha1)) return null;

            string dir = Paths.ModsDir(versionId);
            if (!Directory.Exists(dir)) return null;

            foreach (var file in Directory.GetFiles(dir, "*.jar"))
            {
                try { if (string.Equals(Sha1Of(file), sha1, StringComparison.OrdinalIgnoreCase)) return file; }
                catch { }
            }

            return null;
        }

        /// <summary>
        /// Доносит до сборки те моды, которых не хватает: сперва смотрим на
        /// сборном пункте, потом на Modrinth по хэшу файла. Возвращает то,
        /// чего достать не удалось.
        /// </summary>
        public static async Task<List<Missing>> DeliverAsync(Room room, string versionId,
                                                             Action<string> status, CancellationToken ct)
        {
            var missing = new List<Missing>();
            string dir = Paths.ModsDir(versionId);
            Paths.Ensure(dir);

            foreach (var mod in room.Mods)
            {
                if (!mod.Need) continue;

                if (!Ok(mod.Sha1))
                {
                    missing.Add(new Missing { Mod = mod, Why = "хозяин не передал sha1 — сверить нечем" });
                    continue;
                }

                if (FindInBuild(versionId, mod.Sha1) != null) continue;

                string name = SafeName(mod.File.Length > 0 ? mod.File : mod.Title + ".jar");
                string target = Path.Combine(dir, name);

                if (status != null) status("Беру " + mod.Title + "…");

                try
                {
                    if (await HasFileAsync(mod.Sha1, ct).ConfigureAwait(false))
                    {
                        await GetFileAsync(mod.Sha1, target, ct).ConfigureAwait(false);
                        Log.Info("Мод от хозяина: " + name);
                        continue;
                    }
                }
                catch (Exception e) { Log.Warn("Со сборного пункта не вышло: " + e.Message); }

                try
                {
                    string url = await ModrinthByHashAsync(mod.Sha1, ct).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(url))
                    {
                        await Net.DownloadFileAsync(url, target, null, ct).ConfigureAwait(false);
                        Log.Info("Мод с Modrinth по хэшу: " + name);
                        continue;
                    }
                }
                catch (Exception e) { Log.Warn("Modrinth по хэшу не дал: " + e.Message); }

                missing.Add(new Missing
                {
                    Mod = mod,
                    Why = "нет ни у хозяина на сервере, ни на Modrinth"
                });
            }

            // ресурспак, если хозяин его выложил
            if (Ok(room.PackSha1))
            {
                string packs = Paths.ResourcePacksDir(versionId);
                Paths.Ensure(packs);

                string name = SafeName(room.PackName.Length > 0 ? room.PackName : "pack");
                if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) name += ".zip";

                string target = Path.Combine(packs, name);

                try
                {
                    bool already = File.Exists(target) &&
                                   string.Equals(Sha1Of(target), room.PackSha1, StringComparison.OrdinalIgnoreCase);

                    if (!already && await HasFileAsync(room.PackSha1, ct).ConfigureAwait(false))
                    {
                        if (status != null) status("Беру ресурспак…");
                        await GetFileAsync(room.PackSha1, target, ct).ConfigureAwait(false);
                        Log.Info("Ресурспак от хозяина: " + name);
                    }
                }
                catch (Exception e) { Log.Warn("Ресурспак не достался: " + e.Message); }
            }

            return missing;
        }

        /// <summary>Modrinth умеет находить файл по его sha1 — этим и пользуемся.</summary>
        private static async Task<string> ModrinthByHashAsync(string sha1, CancellationToken ct)
        {
            string url = ModrinthApi.Api + "version_file/" + sha1.ToLowerInvariant() + "?algorithm=sha1";

            Json json;
            try { json = await Net.GetJsonAsync(url, ct).ConfigureAwait(false); }
            catch { return null; }

            foreach (var file in json["files"].Items)
            {
                if (!string.Equals(file["hashes"]["sha1"].AsString(""), sha1, StringComparison.OrdinalIgnoreCase))
                    continue;

                return file["url"].AsString(null);
            }

            return null;
        }

        private static string SafeName(string name)
        {
            var clean = new StringBuilder();
            foreach (char c in name)
                clean.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);

            string result = clean.ToString().Trim();
            return result.Length == 0 ? "mod.jar" : result;
        }
    }
}
