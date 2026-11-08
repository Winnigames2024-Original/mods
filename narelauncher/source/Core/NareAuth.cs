using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NareLauncher.Core
{
    /// <summary>Правила, которые сервер сообщает про игрока.</summary>
    public class PlayerPolicy
    {
        public bool Known;                 // удалось ли вообще спросить сервер

        public bool Banned;
        public long Until;                 // 0 — навсегда
        public string BanReason = "";
        public string BanLeft = "";
        public bool AppealOnly;            // в бане работает только апелляция

        public bool BlockedTogether;

        public readonly List<string> Warnings = new List<string>();

        public bool HasBroadcast;
        public string Broadcast = "";
        public long BroadcastAt;

        public bool HasUpdate;
        public string UpdateVersion = "";
        public string Changelog = "";
        public bool Critical;
        public long UpdateAt;

        /// <summary>Устройство отвязали — токен больше не наш, пора выйти.</summary>
        public bool LoggedOut;

        /// <summary>Заблокированные моды: пары id|version, version "*" — все.</summary>
        public readonly List<string> BlockedMods = new List<string>();

        public long Coins;
        public string Username = "";
        public bool IsAdmin;

        public bool ModBlocked(string modId, string gameVersion)
        {
            if (string.IsNullOrEmpty(modId)) return false;
            foreach (var pair in BlockedMods)
            {
                int bar = pair.IndexOf('|');
                if (bar < 0) continue;
                string id = pair.Substring(0, bar);
                string version = pair.Substring(bar + 1);
                if (!string.Equals(id, modId, StringComparison.OrdinalIgnoreCase)) continue;
                if (version == "*" || string.Equals(version, gameVersion, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Разговор с сервером про аккаунты, баны и админку. Сервер тот же,
    /// что и сборный пункт NareNet, поэтому адрес берём у него.
    /// </summary>
    public static class NareAuth
    {
        /// <summary>Последняя политика по текущему игроку — читают страницы лаунчера.</summary>
        public static PlayerPolicy Last = new PlayerPolicy();

        private static string Url(string route) { return "http://" + NareNet.Where + route; }

        private static async Task<Json> PostAsync(string route, Json body, CancellationToken ct)
        {
            using (var content = new StringContent(body.ToJson(false), Encoding.UTF8, "application/json"))
            using (var request = new HttpRequestMessage(HttpMethod.Post, Url(route)) { Content = content })
            using (var response = await Net.Client.SendAsync(request, ct).ConfigureAwait(false))
            {
                string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var json = Json.Parse(text);

                if (!response.IsSuccessStatusCode)
                {
                    string why = json["error"].AsString("сервер ответил " + (int)response.StatusCode);
                    throw new Exception(why);
                }
                return json;
            }
        }

        // ------------------------------------------------------------ аккаунты

        public class LoginResult
        {
            public string Nick = "";
            public string Token = "";
            public bool Admin;
        }

        private static LoginResult Result(Json json)
        {
            return new LoginResult
            {
                Nick = json["nick"].AsString(""),
                Token = json["token"].AsString(""),
                Admin = json["admin"].AsBool(false)
            };
        }

        /// <summary>Описание этого устройства для сервера.</summary>
        private static void AttachDevice(Json body)
        {
            var dev = Json.NewObject();
            dev.Set("id", DeviceId.Get());
            dev.Set("name", DeviceId.Name());
            body.Set("device", dev);
        }

        public static async Task<LoginResult> RegisterAsync(string nick, string password, CancellationToken ct)
        {
            var body = Json.NewObject();
            body.Set("nick", nick);
            body.Set("password", password);
            AttachDevice(body);
            return Result(await PostAsync("/account/register", body, ct).ConfigureAwait(false));
        }

        public static async Task<LoginResult> LoginAsync(string nick, string password, CancellationToken ct)
        {
            var body = Json.NewObject();
            body.Set("nick", nick);
            body.Set("password", password);
            AttachDevice(body);
            return Result(await PostAsync("/account/login", body, ct).ConfigureAwait(false));
        }

        public static async Task ChangePasswordAsync(string nick, string password, string newPassword, CancellationToken ct)
        {
            var body = Json.NewObject();
            body.Set("nick", nick);
            body.Set("password", password);
            body.Set("newPassword", newPassword);
            await PostAsync("/account/password", body, ct).ConfigureAwait(false);
        }

        public static async Task DeleteAsync(string nick, string password, CancellationToken ct)
        {
            var body = Json.NewObject();
            body.Set("nick", nick);
            body.Set("password", password);
            await PostAsync("/account/delete", body, ct).ConfigureAwait(false);
        }

        // -------------------------------------------------------------- статус

        public static async Task<PlayerPolicy> StatusAsync(string nick, CancellationToken ct, string token = null)
        {
            var policy = new PlayerPolicy();
            if (string.IsNullOrEmpty(nick)) return policy;

            string u = Url("/status?nick=" + Net.UrlEncode(nick));
            if (!string.IsNullOrEmpty(token)) u += "&token=" + Net.UrlEncode(token);
            var json = await Net.GetJsonAsync(u, ct).ConfigureAwait(false);

            policy.Known = true;
            policy.LoggedOut = json["loggedOut"].AsBool(false);
            policy.Banned = json["banned"].AsBool(false);
            policy.Until = json["until"].AsLong(0);
            policy.BanReason = json["banReason"].AsString("");
            policy.BanLeft = json["banLeft"].AsString("");
            policy.AppealOnly = json["appealOnly"].AsBool(false);
            policy.BlockedTogether = json["blockedTogether"].AsBool(false);

            foreach (var w in json["warnings"].Items)
            {
                string text = w["text"].AsString("");
                if (text.Length > 0) policy.Warnings.Add(text);
            }

            var broadcast = json["broadcast"];
            if (broadcast.IsObject)
            {
                policy.HasBroadcast = true;
                policy.Broadcast = broadcast["text"].AsString("");
                policy.BroadcastAt = broadcast["at"].AsLong(0);
            }

            var update = json["update"];
            if (update.IsObject)
            {
                policy.HasUpdate = true;
                policy.UpdateVersion = update["version"].AsString("");
                policy.Changelog = update["changelog"].AsString("");
                policy.Critical = update["critical"].AsBool(false);
                policy.UpdateAt = update["at"].AsLong(0);
            }

            foreach (var m in json["blockedMods"].Items)
            {
                string id = m["id"].AsString("");
                string version = m["version"].AsString("*");
                if (id.Length > 0) policy.BlockedMods.Add(id + "|" + version);
            }

            policy.Coins = json["coins"].AsLong(0);
            policy.Username = json["username"].AsString("");
            policy.IsAdmin = json["admin"].AsBool(false);

            return policy;
        }

        public static async Task AppealAsync(string nick, string text, CancellationToken ct)
        {
            var body = Json.NewObject();
            body.Set("nick", nick);
            body.Set("text", text);
            await PostAsync("/appeal", body, ct).ConfigureAwait(false);
        }

        // --------------------------------------------------------- админ-панель

        private static Json Admin(string token)
        {
            var body = Json.NewObject();
            body.Set("token", token);
            return body;
        }

        public static async Task BanAsync(string token, string nick, int minutes, string reason, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("nick", nick);
            body.Set("minutes", minutes);
            body.Set("reason", reason ?? "");
            await PostAsync("/admin/ban", body, ct).ConfigureAwait(false);
        }

        public static async Task UnbanAsync(string token, string nick, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("nick", nick);
            await PostAsync("/admin/unban", body, ct).ConfigureAwait(false);
        }

        public static async Task WarnAsync(string token, string nick, string text, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("nick", nick);
            body.Set("text", text);
            await PostAsync("/admin/warn", body, ct).ConfigureAwait(false);
        }

        public static async Task BroadcastAsync(string token, string text, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("text", text);
            await PostAsync("/admin/broadcast", body, ct).ConfigureAwait(false);
        }

        public static async Task CommandAsync(string token, string text, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("text", text);
            await PostAsync("/admin/command", body, ct).ConfigureAwait(false);
        }

        public static async Task TogetherAsync(string token, string nick, bool on, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("nick", nick);
            body.Set("on", on);
            await PostAsync("/admin/together", body, ct).ConfigureAwait(false);
        }

        public static async Task BlockModAsync(string token, string id, string version, bool on, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("id", id);
            body.Set("version", string.IsNullOrEmpty(version) ? "*" : version);
            body.Set("on", on);
            await PostAsync("/admin/blockmod", body, ct).ConfigureAwait(false);
        }

        public static async Task AnnounceUpdateAsync(string token, string version, string changelog, bool critical, CancellationToken ct, string nick = null)
        {
            var body = Admin(token);
            body.Set("version", version);
            body.Set("changelog", changelog ?? "");
            body.Set("critical", critical);
            if (!string.IsNullOrEmpty(nick)) body.Set("nick", nick);   // точечно одному игроку
            await PostAsync("/admin/update", body, ct).ConfigureAwait(false);
        }

        // ----------------------------------------------------------- устройства

        public class DeviceInfo
        {
            public string Id = "";
            public string Name = "";
            public long At;
            public bool Current;
        }

        public static async Task<List<DeviceInfo>> DevicesAsync(string token, CancellationToken ct)
        {
            var body = Json.NewObject();
            body.Set("token", token);
            var json = await PostAsync("/account/devices", body, ct).ConfigureAwait(false);
            var list = new List<DeviceInfo>();
            foreach (var d in json["devices"].Items)
                list.Add(new DeviceInfo
                {
                    Id = d["id"].AsString(""),
                    Name = d["name"].AsString("Устройство"),
                    At = d["at"].AsLong(0),
                    Current = d["current"].AsBool(false)
                });
            return list;
        }

        public static async Task DeviceUnlinkAsync(string token, string id, CancellationToken ct)
        {
            var body = Json.NewObject();
            body.Set("token", token);
            body.Set("id", id);
            await PostAsync("/account/device/unlink", body, ct).ConfigureAwait(false);
        }

        /// <summary>Выйти из аккаунта на этом устройстве (сбросить сессию).</summary>
        public static async Task LogoutAsync(string token, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(token)) return;
            var body = Json.NewObject();
            body.Set("token", token);
            try { await PostAsync("/account/logout", body, ct).ConfigureAwait(false); }
            catch { }   // выход локально важнее ответа сервера
        }

        /// <summary>Выйти на всех других устройствах — текущее остаётся.</summary>
        public static async Task<int> LogoutOthersAsync(string token, CancellationToken ct)
        {
            var body = Json.NewObject();
            body.Set("token", token);
            var j = await PostAsync("/account/logout/others", body, ct).ConfigureAwait(false);
            return (int)j["kicked"].AsLong(0);
        }

        public static Task<Json> StateAsync(string token, CancellationToken ct)
        {
            return PostAsync("/admin/state", Admin(token), ct);
        }

        // ------------------------------------------------ реклама и монеты

        public class AdServer
        {
            public string Id = "";
            public string Owner = "";
            public string Name = "";
            public string Ip = "";
            public string Version = "";
            public string Loader = "";
            public string Desc = "";
            public long Reward = 100;
            public string Status = "";
            public string Reason = "";
        }

        private static AdServer ToServer(Json s)
        {
            return new AdServer
            {
                Id = s["id"].AsString(""),
                Owner = s["owner"].AsString(""),
                Name = s["name"].AsString(""),
                Ip = s["ip"].AsString(""),
                Version = s["version"].AsString(""),
                Loader = s["loader"].AsString(""),
                Desc = s["desc"].AsString(""),
                Reward = s["reward"].AsLong(100),
                Status = s["status"].AsString(""),
                Reason = s["reason"].AsString("")
            };
        }

        public static async Task<List<AdServer>> AdServersAsync(CancellationToken ct)
        {
            var json = await Net.GetJsonAsync(Url("/servers"), ct).ConfigureAwait(false);
            var list = new List<AdServer>();
            foreach (var s in json["servers"].Items) list.Add(ToServer(s));
            return list;
        }

        public static async Task<string> SubmitServerAsync(string token, AdServer server, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("name", server.Name);
            body.Set("ip", server.Ip);
            body.Set("version", server.Version ?? "");
            body.Set("loader", server.Loader ?? "");
            body.Set("desc", server.Desc ?? "");
            var r = await PostAsync("/servers/submit", body, ct).ConfigureAwait(false);
            return r["id"].AsString("");
        }

        public static async Task<List<AdServer>> MyServersAsync(string token, CancellationToken ct)
        {
            var json = await PostAsync("/servers/mine", Admin(token), ct).ConfigureAwait(false);
            var list = new List<AdServer>();
            foreach (var s in json["servers"].Items) list.Add(ToServer(s));
            return list;
        }

        public class EarnResult { public long Awarded; public long Coins; public int CooldownLeft; }

        public static async Task<EarnResult> EarnCoinsAsync(string token, string serverId, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("serverId", serverId);
            var r = await PostAsync("/coins/earn", body, ct).ConfigureAwait(false);
            return new EarnResult
            {
                Awarded = r["awarded"].AsLong(0),
                Coins = r["coins"].AsLong(0),
                CooldownLeft = r["cooldownLeft"].AsInt(0)
            };
        }

        public static async Task<long> ChangeUsernameAsync(string token, string username, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("username", username);
            var r = await PostAsync("/username/change", body, ct).ConfigureAwait(false);
            return r["coins"].AsLong(0);
        }

        // ---- модерация серверов (админ)

        public static async Task<List<AdServer>> AdminServersAsync(string token, CancellationToken ct)
        {
            var json = await PostAsync("/admin/servers", Admin(token), ct).ConfigureAwait(false);
            var list = new List<AdServer>();
            foreach (var s in json["servers"].Items) list.Add(ToServer(s));
            return list;
        }

        public static async Task ApproveServerAsync(string token, string id, long reward, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("id", id);
            if (reward > 0) body.Set("reward", reward);
            await PostAsync("/admin/server/approve", body, ct).ConfigureAwait(false);
        }

        public static async Task RejectServerAsync(string token, string id, string reason, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("id", id);
            body.Set("reason", reason ?? "");
            await PostAsync("/admin/server/reject", body, ct).ConfigureAwait(false);
        }

        // ---------------------------------------------- чат по бану

        public class ChatMessage
        {
            public string From = "";
            public bool Admin;
            public string Text = "";
            public long At;
        }

        private static List<ChatMessage> Messages(Json json)
        {
            var list = new List<ChatMessage>();
            foreach (var m in json["messages"].Items)
                list.Add(new ChatMessage
                {
                    From = m["from"].AsString(""),
                    Admin = m["admin"].AsBool(false),
                    Text = m["text"].AsString(""),
                    At = m["at"].AsLong(0)
                });
            return list;
        }

        public static async Task<List<ChatMessage>> BanChatAsync(string token, CancellationToken ct)
        {
            return Messages(await PostAsync("/ban/chat", Admin(token), ct).ConfigureAwait(false));
        }

        public static async Task<List<ChatMessage>> BanSendAsync(string token, string text, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("text", text);
            return Messages(await PostAsync("/ban/send", body, ct).ConfigureAwait(false));
        }

        public static async Task<List<ChatMessage>> AdminChatAsync(string token, string nick, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("nick", nick);
            return Messages(await PostAsync("/admin/chat", body, ct).ConfigureAwait(false));
        }

        public static async Task<List<ChatMessage>> AdminChatSendAsync(string token, string nick, string text, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("nick", nick);
            body.Set("text", text);
            return Messages(await PostAsync("/admin/chat/send", body, ct).ConfigureAwait(false));
        }

        public class ChatSummary
        {
            public string Nick = "";
            public bool Banned;
            public int Count;
            public string Last = "";
            public bool LastAdmin;
            public long At;
            public string Assigned = "";
        }

        public static async Task<List<ChatSummary>> AdminChatsAsync(string token, CancellationToken ct)
        {
            var json = await PostAsync("/admin/chats", Admin(token), ct).ConfigureAwait(false);
            var list = new List<ChatSummary>();
            foreach (var c in json["chats"].Items)
                list.Add(new ChatSummary
                {
                    Nick = c["nick"].AsString(""),
                    Banned = c["banned"].AsBool(false),
                    Count = (int)c["count"].AsLong(0),
                    Last = c["last"].AsString(""),
                    LastAdmin = c["lastAdmin"].AsBool(false),
                    At = c["at"].AsLong(0),
                    Assigned = c["assigned"].AsString("")
                });
            return list;
        }

        public static async Task AdminChatAssignAsync(string token, string nick, string to, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("nick", nick);
            body.Set("to", to);
            await PostAsync("/admin/chat/assign", body, ct).ConfigureAwait(false);
        }

        public static async Task GrantAdminAsync(string token, string nick, bool grant, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("nick", nick);
            await PostAsync(grant ? "/admin/grant" : "/admin/revoke", body, ct).ConfigureAwait(false);
        }

        public static async Task<long> AdminCoinsAsync(string token, string nick, long amount, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("nick", nick);
            body.Set("amount", amount);
            var j = await PostAsync("/admin/coins", body, ct).ConfigureAwait(false);
            return j["coins"].AsLong(0);
        }

        public static async Task AdminSetNickAsync(string token, string nick, string username, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("nick", nick);
            body.Set("username", username);
            await PostAsync("/admin/setnick", body, ct).ConfigureAwait(false);
        }

        /// <summary>Админ сбрасывает вход игрока на всех устройствах.</summary>
        public static async Task<int> AdminLogoutAsync(string token, string nick, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("nick", nick);
            var j = await PostAsync("/admin/logout", body, ct).ConfigureAwait(false);
            return (int)j["kicked"].AsLong(0);
        }

        // ---------------------------------------------- подаккаунты (ники)

        public class Subs
        {
            public readonly List<string> List = new List<string>();
            public string Active = "";
            public long Coins;
        }

        private static Subs ToSubs(Json json)
        {
            var subs = new Subs { Active = json["active"].AsString(""), Coins = json["coins"].AsLong(0) };
            foreach (var n in json["subs"].Items)
            {
                string s = n.AsString("");
                if (s.Length > 0) subs.List.Add(s);
            }
            return subs;
        }

        public static async Task<Subs> SubListAsync(string token, CancellationToken ct)
        {
            return ToSubs(await PostAsync("/subaccount/list", Admin(token), ct).ConfigureAwait(false));
        }

        public static async Task<Subs> SubAddAsync(string token, string nick, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("nick", nick);
            return ToSubs(await PostAsync("/subaccount/add", body, ct).ConfigureAwait(false));
        }

        public static async Task<string> SubUseAsync(string token, string nick, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("nick", nick);
            var r = await PostAsync("/subaccount/use", body, ct).ConfigureAwait(false);
            return r["active"].AsString(nick);
        }

        public static async Task SubRemoveAsync(string token, string nick, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("nick", nick);
            await PostAsync("/subaccount/remove", body, ct).ConfigureAwait(false);
        }
    }
}
