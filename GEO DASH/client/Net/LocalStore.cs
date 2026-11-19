using System.Text.Json;
using GEO_DASH.Core;
using GEO_DASH.Audio;

namespace GEO_DASH.Net
{
    public class QueueItem
    {
        public string Path { get; set; } = "";
        public string BodyJson { get; set; } = "";
        public string Tag { get; set; } = "";
        public string Added { get; set; } = DateTime.Now.ToString("s");
        public int Tries { get; set; }
    }

    public class LevelProgress
    {
        public string Key { get; set; } = "";       // local:<id> или srv:<id>
        public double Best { get; set; }
        public int Attempts { get; set; }
        public int Coins { get; set; }
        public bool Completed { get; set; }
        public bool Uploaded { get; set; }
        public string LastPlayed { get; set; } = "";
    }

    /// <summary>Локальное хранилище: мои уровни, скачанные (Saved), прогресс и очередь синхронизации.
    /// Игра полностью работает без интернета — онлайн-разделы просто не грузятся.</summary>
    public static class LocalStore
    {
        static readonly JsonSerializerOptions J = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

        // --------------------------------------------------------- мои уровни

        public static string MyLevelPath(LevelData lv) =>
            Path.Combine(AppConfig.LevelsDir, Safe(lv.Settings.LocalId) + ".gdl");

        public static List<LevelData> MyLevels()
        {
            var res = new List<LevelData>();
            foreach (var f in Directory.GetFiles(AppConfig.LevelsDir, "*.gdl"))
            {
                try { res.Add(LevelData.Load(f)); } catch { }
            }
            res.Sort((a, b) => string.Compare(a.Settings.Name, b.Settings.Name, StringComparison.OrdinalIgnoreCase));
            return res;
        }

        public static void SaveMyLevel(LevelData lv)
        {
            if (string.IsNullOrEmpty(lv.Settings.LocalId)) lv.Settings.LocalId = Guid.NewGuid().ToString("N");
            lv.Save(MyLevelPath(lv));
        }

        public static void DeleteMyLevel(LevelData lv)
        {
            try { var p = MyLevelPath(lv); if (File.Exists(p)) File.Delete(p); } catch { }
        }

        public static LevelData CopyLevel(LevelData src, string newName = null)
        {
            var c = src.DeepClone();
            c.Settings.LocalId = Guid.NewGuid().ToString("N");
            c.Settings.IsCopy = true;
            c.Settings.CopiedFrom = src.Settings.ServerId;
            c.Settings.CopiedAuthor = string.IsNullOrEmpty(src.Settings.Author)
                ? c.Settings.CopiedAuthor : src.Settings.Author;
            c.Settings.ServerId = 0;
            c.Settings.Version = 1;
            c.Settings.Password = "";
            c.Settings.Name = newName ?? (src.Settings.Name + " (копия)");
            SaveMyLevel(c);
            return c;
        }

        /// <summary>Свои уровни: созданные с нуля (copied = false) или скопированные (copied = true).</summary>
        public static List<LevelData> MyLevels(bool copied)
            => MyLevels().Where(l => l.Settings.IsCopy == copied).ToList();

        // ------------------------------------------------------- скачанные (Saved)

        public static string SavedPath(int serverId) => Path.Combine(AppConfig.SavedDir, "srv_" + serverId + ".gdl");

        public static bool IsSaved(int serverId) => File.Exists(SavedPath(serverId));

        public static List<LevelData> SavedLevels()
        {
            var res = new List<LevelData>();
            foreach (var f in Directory.GetFiles(AppConfig.SavedDir, "*.gdl"))
            {
                try { res.Add(LevelData.Load(f)); } catch { }
            }
            return res;
        }

        public static LevelData SaveDownloaded(LevelFullDto dto)
        {
            LevelData lv;
            try { lv = LevelData.FromJson(dto.Data); } catch { return null; }
            lv.Settings.ServerId = dto.Id;
            if (string.IsNullOrEmpty(lv.Settings.Author)) lv.Settings.Author = dto.Author;
            if (string.IsNullOrWhiteSpace(lv.Settings.Name)) lv.Settings.Name = dto.Name;
            lv.Settings.Difficulty = (Difficulty)dto.Difficulty;
            if (dto.SongId > 0) lv.Settings.SongId = dto.SongId;
            if (!string.IsNullOrEmpty(dto.SongName)) lv.Settings.SongName = dto.SongName;
            lv.Save(SavedPath(dto.Id));
            return lv;
        }

        public static void DeleteSaved(int serverId)
        {
            try { var p = SavedPath(serverId); if (File.Exists(p)) File.Delete(p); } catch { }
        }

        /// <summary>Скачать уровень с сервера в Saved (с музыкой).</summary>
        public static async Task<LevelData> DownloadAsync(int serverId, bool withMusic = true, IProgress<string> prog = null)
        {
            prog?.Report($"Скачиваю уровень #{serverId}…");
            var r = await ApiClient.LevelAsync(serverId);
            if (!r.Ok || r.Data == null) return IsSaved(serverId) ? LevelData.Load(SavedPath(serverId)) : null;
            var lv = SaveDownloaded(r.Data);
            if (lv != null && withMusic && lv.Settings.SongId > 0)
                await MusicPlayer.EnsureAsync(lv.Settings.SongId, prog);
            return lv;
        }

        // --------------------------------------------------------- прогресс

        static string ProgressPath => Path.Combine(AppConfig.DataDir, "progress.json");
        static Dictionary<string, LevelProgress> _prog;

        public static Dictionary<string, LevelProgress> Progress
        {
            get
            {
                if (_prog == null)
                {
                    try
                    {
                        _prog = File.Exists(ProgressPath)
                            ? JsonSerializer.Deserialize<Dictionary<string, LevelProgress>>(File.ReadAllText(ProgressPath), J)
                              ?? new()
                            : new();
                    }
                    catch { _prog = new(); }
                }
                return _prog;
            }
        }

        public static void SaveProgress()
        {
            try { File.WriteAllText(ProgressPath, JsonSerializer.Serialize(Progress, J)); } catch { }
        }

        public static LevelProgress GetProgress(LevelData lv)
        {
            string key = lv.Settings.ServerId > 0 ? "srv:" + lv.Settings.ServerId : "local:" + lv.Settings.LocalId;
            if (!Progress.TryGetValue(key, out var p))
            {
                p = new LevelProgress { Key = key };
                Progress[key] = p;
            }
            return p;
        }

        public static void RecordRun(LevelData lv, double percent, int attempts, int coins, bool completed, int orbs = 0)
        {
            var p = GetProgress(lv);
            p.Attempts += attempts;
            p.Coins = Math.Max(p.Coins, coins);
            if (percent > p.Best) p.Best = percent;
            if (completed) p.Completed = true;
            p.LastPlayed = DateTime.Now.ToString("s");
            SaveProgress();

            if (lv.Settings.ServerId > 0)
            {
                // онлайн — уйдёт сразу, офлайн — встанет в очередь
                _ = ApiClient.CompleteAsync(lv.Settings.ServerId, percent, attempts, coins, orbs);
            }
        }

        // --------------------------------------------------------- очередь

        static string QueuePath => Path.Combine(AppConfig.DataDir, "queue.json");
        static List<QueueItem> _queue;

        public static List<QueueItem> Queue
        {
            get
            {
                if (_queue == null)
                {
                    try
                    {
                        _queue = File.Exists(QueuePath)
                            ? JsonSerializer.Deserialize<List<QueueItem>>(File.ReadAllText(QueuePath), J) ?? new()
                            : new();
                    }
                    catch { _queue = new(); }
                }
                return _queue;
            }
        }

        public static void SaveQueue()
        {
            try { File.WriteAllText(QueuePath, JsonSerializer.Serialize(Queue, J)); } catch { }
        }

        public static void Enqueue(string path, object body, string tag)
        {
            var item = new QueueItem { Path = path, BodyJson = JsonSerializer.Serialize(body), Tag = tag ?? path };
            // одна задача на тег (последняя побеждает)
            Queue.RemoveAll(q => q.Tag == item.Tag);
            Queue.Add(item);
            SaveQueue();
        }

        public static async Task<int> FlushQueueAsync(IProgress<string> prog = null)
        {
            if (Queue.Count == 0) return 0;
            if (!await ApiClient.PingAsync()) return 0;
            int done = 0;
            foreach (var q in Queue.ToArray())
            {
                prog?.Report("Синхронизация: " + q.Tag);
                try
                {
                    using var doc = JsonDocument.Parse(string.IsNullOrEmpty(q.BodyJson) ? "{}" : q.BodyJson);
                    var r = await ApiClient.PostAsync<JsonElement>(q.Path, JsonSerializer.Deserialize<object>(q.BodyJson));
                    if (r.Ok) { Queue.Remove(q); done++; }
                    else
                    {
                        q.Tries++;
                        if (q.Tries > 8) Queue.Remove(q);
                    }
                }
                catch { q.Tries++; if (q.Tries > 8) Queue.Remove(q); }
            }
            SaveQueue();
            return done;
        }

        // --------------------------------------------------------- синхронизация

        public static DateTime LastSync { get; private set; } = DateTime.MinValue;

        /// <summary>Полная синхронизация: очередь → аккаунт → daily/weekly → офлайн-паки (в Saved) → музыка.</summary>
        public static async Task SyncAsync(IProgress<string> prog = null)
        {
            prog?.Report("Проверяю связь…");
            if (!await ApiClient.PingAsync())
            {
                prog?.Report("Офлайн — играем на локальных данных");
                return;
            }

            await FlushQueueAsync(prog);

            if (AppConfig.LoggedIn)
            {
                prog?.Report("Обновляю аккаунт…");
                var me = await ApiClient.MeAsync();
                if (me.Ok && me.Data != null)
                {
                    AppConfig.Settings.IsAdmin = me.Data.Admin;
                    AppConfig.Save();
                }
                await ApiClient.FriendsAsync();
                await ApiClient.FriendRequestsAsync();
            }

            prog?.Report("Обновляю Daily / Weekly…");
            var daily = await ApiClient.DailyAsync();
            var weekly = await ApiClient.WeeklyAsync();
            await ApiClient.LevelsAsync(sort: "recent");
            await ApiClient.LevelsAsync(sort: "top");
            await ApiClient.TopAsync();

            // daily/weekly тоже кладём в Saved, чтобы играть офлайн
            if (AppConfig.Settings.OfflineAutoDownload)
            {
                if (daily.Ok && daily.Data?.Level != null) await DownloadAsync(daily.Data.Level.Id, true, prog);
                if (weekly.Ok && weekly.Data?.Level != null) await DownloadAsync(weekly.Data.Level.Id, true, prog);

                prog?.Report("Скачиваю офлайн-уровни…");
                var packs = await ApiClient.OfflinePacksAsync();
                if (packs.Ok && packs.Data != null)
                    foreach (var l in packs.Data)
                    {
                        bool need = !IsSaved(l.Id);
                        if (!need)
                        {
                            try
                            {
                                var cur = LevelData.Load(SavedPath(l.Id));
                                need = cur.Settings.Version < l.Version;
                            }
                            catch { need = true; }
                        }
                        if (need) await DownloadAsync(l.Id, true, prog);
                        else if (l.SongId > 0) await MusicPlayer.EnsureAsync(l.SongId, prog);
                    }

                // музыка для всего, что лежит локально
                prog?.Report("Докачиваю музыку…");
                foreach (var lv in SavedLevels().Concat(MyLevels()))
                    if (lv.Settings.SongId > 0 && !MusicPlayer.IsCached(lv.Settings.SongId))
                        await MusicPlayer.EnsureAsync(lv.Settings.SongId, prog);
            }

            LastSync = DateTime.Now;
            prog?.Report("Синхронизация завершена");
        }

        public static string Safe(string s)
        {
            if (string.IsNullOrEmpty(s)) return "level";
            return new string(s.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());
        }

        // кэшированные списки для офлайна
        public static List<LevelDto> CachedLevels(string key)
        {
            var raw = ApiClient.ReadCache(key);
            if (raw == null) return new List<LevelDto>();
            try { return JsonSerializer.Deserialize<List<LevelDto>>(raw, J) ?? new(); }
            catch { return new List<LevelDto>(); }
        }

        public static AccountDto CachedMe()
        {
            var raw = ApiClient.ReadCache("me");
            if (raw == null) return null;
            try { return JsonSerializer.Deserialize<AccountDto>(raw, J); } catch { return null; }
        }
    }
}
