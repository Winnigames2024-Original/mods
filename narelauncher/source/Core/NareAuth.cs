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

        /// <summary>Заблокированные моды: пары id|version, version "*" — все.</summary>
        public readonly List<string> BlockedMods = new List<string>();

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

        public static async Task<LoginResult> RegisterAsync(string nick, string password, CancellationToken ct)
        {
            var body = Json.NewObject();
            body.Set("nick", nick);
            body.Set("password", password);
            return Result(await PostAsync("/account/register", body, ct).ConfigureAwait(false));
        }

        public static async Task<LoginResult> LoginAsync(string nick, string password, CancellationToken ct)
        {
            var body = Json.NewObject();
            body.Set("nick", nick);
            body.Set("password", password);
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

        public static async Task<PlayerPolicy> StatusAsync(string nick, CancellationToken ct)
        {
            var policy = new PlayerPolicy();
            if (string.IsNullOrEmpty(nick)) return policy;

            var json = await Net.GetJsonAsync(Url("/status?nick=" + Net.UrlEncode(nick)), ct).ConfigureAwait(false);

            policy.Known = true;
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
            }

            foreach (var m in json["blockedMods"].Items)
            {
                string id = m["id"].AsString("");
                string version = m["version"].AsString("*");
                if (id.Length > 0) policy.BlockedMods.Add(id + "|" + version);
            }

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

        public static async Task AnnounceUpdateAsync(string token, string version, string changelog, bool critical, CancellationToken ct)
        {
            var body = Admin(token);
            body.Set("version", version);
            body.Set("changelog", changelog ?? "");
            body.Set("critical", critical);
            await PostAsync("/admin/update", body, ct).ConfigureAwait(false);
        }

        public static Task<Json> StateAsync(string token, CancellationToken ct)
        {
            return PostAsync("/admin/state", Admin(token), ct);
        }
    }
}
