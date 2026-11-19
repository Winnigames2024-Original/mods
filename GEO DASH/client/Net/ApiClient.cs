using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GEO_DASH.Net
{
    /// <summary>Клиент API. Всё кэшируется: без интернета отдаются последние скачанные данные.</summary>
    public static class ApiClient
    {
        static readonly HttpClient Http = new(new HttpClientHandler { UseProxy = false })
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        public static bool Online { get; private set; }
        public static DateTime LastCheck { get; private set; } = DateTime.MinValue;
        public static string LastError { get; private set; } = "";
        public static event Action<bool> OnlineChanged;

        static readonly JsonSerializerOptions J = new() { PropertyNameCaseInsensitive = true };

        static void SetOnline(bool v)
        {
            if (Online != v)
            {
                Online = v;
                try { OnlineChanged?.Invoke(v); } catch { }
            }
            LastCheck = DateTime.Now;
        }

        static string Url(string path) => AppConfig.ApiBase + (path.StartsWith("/") ? path : "/" + path);

        static HttpRequestMessage Req(HttpMethod m, string path, object body = null)
        {
            var r = new HttpRequestMessage(m, Url(path));
            if (body != null)
                r.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var tok = AppConfig.Settings.Token;
            if (!string.IsNullOrEmpty(tok))
                r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tok);
            r.Headers.Add("X-Client", "geodash-win/" + Application.ProductVersion);
            return r;
        }

        // ------------------------------------------------------------- кэш

        static string CachePath(string key)
        {
            var safe = new string(key.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());
            return Path.Combine(AppConfig.CacheDir, safe + ".json");
        }

        public static void WriteCache(string key, string json)
        {
            try { File.WriteAllText(CachePath(key), json); } catch { }
        }

        public static string ReadCache(string key)
        {
            try { var p = CachePath(key); return File.Exists(p) ? File.ReadAllText(p) : null; }
            catch { return null; }
        }

        public static DateTime? CacheTime(string key)
        {
            try { var p = CachePath(key); return File.Exists(p) ? File.GetLastWriteTime(p) : null; }
            catch { return null; }
        }

        // ------------------------------------------------------------ запросы

        public static async Task<ApiReply<T>> GetAsync<T>(string path, string cacheKey = null)
        {
            try
            {
                using var resp = await Http.SendAsync(Req(HttpMethod.Get, path));
                var text = await resp.Content.ReadAsStringAsync();
                SetOnline(true);
                if (!resp.IsSuccessStatusCode)
                    return ApiReply<T>.Fail(ErrorOf(text, (int)resp.StatusCode));
                if (cacheKey != null) WriteCache(cacheKey, text);
                return ApiReply<T>.Good(JsonSerializer.Deserialize<T>(text, J));
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                SetOnline(false);
                if (cacheKey != null)
                {
                    var c = ReadCache(cacheKey);
                    if (c != null)
                    {
                        try { return ApiReply<T>.Good(JsonSerializer.Deserialize<T>(c, J), true); }
                        catch { }
                    }
                }
                return ApiReply<T>.Fail("Нет связи с сервером");
            }
        }

        public static async Task<ApiReply<T>> PostAsync<T>(string path, object body, bool queueIfOffline = false, string queueTag = null)
        {
            try
            {
                using var resp = await Http.SendAsync(Req(HttpMethod.Post, path, body));
                var text = await resp.Content.ReadAsStringAsync();
                SetOnline(true);
                if (!resp.IsSuccessStatusCode) return ApiReply<T>.Fail(ErrorOf(text, (int)resp.StatusCode));
                return ApiReply<T>.Good(JsonSerializer.Deserialize<T>(text, J));
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                SetOnline(false);
                if (queueIfOffline) LocalStore.Enqueue(path, body, queueTag);
                return ApiReply<T>.Fail(queueIfOffline
                    ? "Нет сети — задача добавлена в очередь синхронизации"
                    : "Нет связи с сервером");
            }
        }

        static string ErrorOf(string text, int code)
        {
            try
            {
                using var d = JsonDocument.Parse(text);
                if (d.RootElement.TryGetProperty("error", out var e)) return e.GetString();
            }
            catch { }
            return "Ошибка сервера " + code;
        }

        public static async Task<bool> PingAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                using var resp = await Http.SendAsync(Req(HttpMethod.Get, "/api/ping"), cts.Token);
                SetOnline(resp.IsSuccessStatusCode);
                return Online;
            }
            catch { SetOnline(false); return false; }
        }

        // ------------------------------------------------------------ аккаунты

        public static Task<ApiReply<LoginResult>> RegisterAsync(string name, string pass) =>
            PostAsync<LoginResult>("/api/register", new { name, pass });

        public static Task<ApiReply<LoginResult>> LoginAsync(string name, string pass) =>
            PostAsync<LoginResult>("/api/login", new { name, pass });

        public static Task<ApiReply<AccountDto>> MeAsync() =>
            GetAsync<AccountDto>("/api/me", "me");

        public static Task<ApiReply<AccountDto>> UserAsync(string name) =>
            GetAsync<AccountDto>("/api/users/" + Uri.EscapeDataString(name), "user_" + name);

        public static Task<ApiReply<List<AccountDto>>> SearchUsersAsync(string q) =>
            GetAsync<List<AccountDto>>("/api/users?q=" + Uri.EscapeDataString(q ?? ""), "usersearch");

        public static Task<ApiReply<List<AccountDto>>> TopAsync() =>
            GetAsync<List<AccountDto>>("/api/top", "top");

        // ------------------------------------------------------------ уровни

        public static Task<ApiReply<List<LevelDto>>> LevelsAsync(string q = "", string sort = "recent",
            int difficulty = -1, string author = "", int page = 0)
        {
            string path = $"/api/levels?q={Uri.EscapeDataString(q ?? "")}&sort={sort}&diff={difficulty}" +
                          $"&author={Uri.EscapeDataString(author ?? "")}&page={page}";
            string key = $"levels_{sort}_{difficulty}_{page}_{q}_{author}";
            return GetAsync<List<LevelDto>>(path, key);
        }

        public static Task<ApiReply<LevelFullDto>> LevelAsync(int id) =>
            GetAsync<LevelFullDto>("/api/levels/" + id, "level_" + id);

        public static Task<ApiReply<LevelDto>> UploadLevelAsync(Core.LevelData lv, int serverId, string password)
        {
            var s = lv.Settings;
            var body = new
            {
                id = serverId,
                password,
                name = s.Name,
                desc = s.Description,
                songId = s.SongId,
                songName = s.SongName,
                difficulty = (int)s.Difficulty,
                length = lv.RightEdge(),
                localId = s.LocalId,
                data = lv.ToJson()
            };
            return PostAsync<LevelDto>("/api/levels", body, true, "upload:" + s.LocalId);
        }

        public static Task<ApiReply<LevelDto>> LikeAsync(int id, bool like) =>
            PostAsync<LevelDto>($"/api/levels/{id}/like", new { like }, true, $"like:{id}");

        public static Task<ApiReply<LevelDto>> CompleteAsync(int id, double percent, int attempts, int coins) =>
            PostAsync<LevelDto>($"/api/levels/{id}/complete", new { percent, attempts, coins }, true, $"complete:{id}");

        public static Task<ApiReply<List<ScoreDto>>> ScoresAsync(int id) =>
            GetAsync<List<ScoreDto>>($"/api/levels/{id}/scores", "scores_" + id);

        public static Task<ApiReply<object>> DeleteLevelAsync(int id) =>
            PostAsync<object>($"/api/levels/{id}/delete", new { });

        // ------------------------------------------------------- daily/weekly/offline

        public static Task<ApiReply<DailyDto>> DailyAsync() => GetAsync<DailyDto>("/api/daily", "daily");
        public static Task<ApiReply<DailyDto>> WeeklyAsync() => GetAsync<DailyDto>("/api/weekly", "weekly");
        public static Task<ApiReply<List<LevelDto>>> OfflinePacksAsync() =>
            GetAsync<List<LevelDto>>("/api/offline", "offlinepacks");

        // ------------------------------------------------------------ друзья

        public static Task<ApiReply<List<FriendDto>>> FriendsAsync() =>
            GetAsync<List<FriendDto>>("/api/friends", "friends");

        public static Task<ApiReply<List<FriendRequestDto>>> FriendRequestsAsync() =>
            GetAsync<List<FriendRequestDto>>("/api/friends/requests", "friendreq");

        public static Task<ApiReply<object>> FriendRequestAsync(string name) =>
            PostAsync<object>("/api/friends/request", new { name }, true, "freq:" + name);

        public static Task<ApiReply<object>> FriendAcceptAsync(string name) =>
            PostAsync<object>("/api/friends/accept", new { name }, true, "facc:" + name);

        public static Task<ApiReply<object>> FriendDeclineAsync(string name) =>
            PostAsync<object>("/api/friends/decline", new { name }, true, "fdec:" + name);

        public static Task<ApiReply<object>> FriendRemoveAsync(string name) =>
            PostAsync<object>("/api/friends/remove", new { name }, true, "frem:" + name);

        // ------------------------------------------------------------ музыка

        public static Task<ApiReply<SongDto>> SongInfoAsync(int id) =>
            GetAsync<SongDto>("/api/music/" + id, "song_" + id);

        /// <summary>Качает песню: сначала через API сервера, если не вышло — прямо с сайта (/songs/&lt;id&gt;.mp3).</summary>
        public static async Task<byte[]> DownloadSongAsync(int id)
        {
            try
            {
                using var resp = await Http.SendAsync(Req(HttpMethod.Get, $"/api/music/{id}/file"),
                                                      HttpCompletionOption.ResponseHeadersRead);
                SetOnline(true);
                if (resp.IsSuccessStatusCode) return await resp.Content.ReadAsByteArrayAsync();
            }
            catch { SetOnline(false); }

            try
            {
                var site = AppConfig.Settings.SiteUrl.TrimEnd('/');
                using var r2 = await Http.GetAsync($"{site}/songs/{id}.mp3", HttpCompletionOption.ResponseHeadersRead);
                if (r2.IsSuccessStatusCode) return await r2.Content.ReadAsByteArrayAsync();
            }
            catch { }
            return null;
        }

        // ------------------------------------------------------------ админка

        public static Task<ApiReply<List<AccountDto>>> AdminUsersAsync() =>
            GetAsync<List<AccountDto>>("/api/admin/users", "adminusers");

        public static Task<ApiReply<object>> AdminBanAsync(string name, bool ban) =>
            PostAsync<object>("/api/admin/ban", new { name, ban });

        public static Task<ApiReply<object>> AdminRateAsync(int id, int difficulty, bool featured, bool epic) =>
            PostAsync<object>("/api/admin/rate", new { id, difficulty, featured, epic });

        public static Task<ApiReply<object>> AdminSetDailyAsync(int id) =>
            PostAsync<object>("/api/admin/daily", new { id });

        public static Task<ApiReply<object>> AdminSetWeeklyAsync(int id) =>
            PostAsync<object>("/api/admin/weekly", new { id });

        /// <summary>Продвинуть текущий daily в weekly — как просил заказчик.</summary>
        public static Task<ApiReply<object>> AdminPromoteDailyToWeeklyAsync() =>
            PostAsync<object>("/api/admin/daily-to-weekly", new { });

        public static Task<ApiReply<object>> AdminSetOfflineAsync(int id, bool offline) =>
            PostAsync<object>("/api/admin/offline", new { id, offline });

        public static Task<ApiReply<object>> AdminDeleteUserAsync(string name) =>
            PostAsync<object>("/api/admin/delete-user", new { name });

        public static Task<ApiReply<object>> AdminBroadcastAsync(string text) =>
            PostAsync<object>("/api/admin/broadcast", new { text });

        /// <summary>Назначить или снять админа.</summary>
        public static Task<ApiReply<AccountDto>> AdminGrantAsync(string name, bool admin) =>
            PostAsync<AccountDto>("/api/admin/grant", new { name, admin });

        /// <summary>Выдать звёзды, монеты и демонов.</summary>
        public static Task<ApiReply<AccountDto>> AdminGiveAsync(string name, int stars, int coins, int demons) =>
            PostAsync<AccountDto>("/api/admin/give", new { name, stars, coins, demons });

        /// <summary>Сбросить игроку пароль.</summary>
        public static Task<ApiReply<object>> AdminResetPasswordAsync(string name, string password) =>
            PostAsync<object>("/api/admin/reset-password", new { name, password });

        /// <summary>Залить свой mp3 как трек с нужным ID.</summary>
        public static async Task<ApiReply<JsonElement>> AdminUploadSongAsync(int id, byte[] mp3, string name)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, AppConfig.ApiBase + "/api/music/" + id + "/upload")
                {
                    Content = new ByteArrayContent(mp3)
                };
                if (!string.IsNullOrEmpty(AppConfig.Settings.Token))
                    req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + AppConfig.Settings.Token);
                req.Headers.TryAddWithoutValidation("X-Song-Name", Uri.EscapeDataString(name ?? ""));
                using var resp = await Http.SendAsync(req);
                var text = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    return new ApiReply<JsonElement> { Ok = false, Error = "Сервер: " + (int)resp.StatusCode + " " + text };
                Online = true;
                return new ApiReply<JsonElement> { Ok = true, Data = JsonDocument.Parse(text).RootElement };
            }
            catch (Exception ex)
            {
                Online = false;
                return new ApiReply<JsonElement> { Ok = false, Error = ex.Message };
            }
        }

        public static Task<ApiReply<JsonElement>> AdminStatsAsync() =>
            GetAsync<JsonElement>("/api/admin/stats", "adminstats");

        public static Task<ApiReply<List<string>>> NewsAsync() =>
            GetAsync<List<string>>("/api/news", "news");

        public static Task<ApiReply<VersionDto>> VersionAsync() =>
            GetAsync<VersionDto>("/api/version", "version");
    }
}
