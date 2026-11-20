using System.Text.Json.Serialization;

namespace GEO_DASH.Net
{
    public class AccountDto
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("stars")] public int Stars { get; set; }
        [JsonPropertyName("coins")] public int Coins { get; set; }
        [JsonPropertyName("demons")] public int Demons { get; set; }
        [JsonPropertyName("levels")] public int Levels { get; set; }
        [JsonPropertyName("attempts")] public int Attempts { get; set; }
        [JsonPropertyName("jumps")] public int Jumps { get; set; }
        [JsonPropertyName("about")] public string About { get; set; } = "";
        [JsonPropertyName("admin")] public bool Admin { get; set; }
        [JsonPropertyName("banned")] public bool Banned { get; set; }
        [JsonPropertyName("created")] public string Created { get; set; } = "";
        [JsonPropertyName("friends")] public int FriendCount { get; set; }
        [JsonPropertyName("orbs")] public int Orbs { get; set; }
        [JsonPropertyName("completed")] public int Completed { get; set; }
        /// <summary>Значение в выбранной таблице рекордов.</summary>
        [JsonPropertyName("score")] public int Score { get; set; }
    }

    public class LevelDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("author")] public string Author { get; set; } = "";
        [JsonPropertyName("desc")] public string Desc { get; set; } = "";
        [JsonPropertyName("songId")] public int SongId { get; set; }
        [JsonPropertyName("songName")] public string SongName { get; set; } = "";
        [JsonPropertyName("difficulty")] public int Difficulty { get; set; }
        [JsonPropertyName("stars")] public int Stars { get; set; }
        [JsonPropertyName("likes")] public int Likes { get; set; }
        [JsonPropertyName("downloads")] public int Downloads { get; set; }
        [JsonPropertyName("plays")] public int Plays { get; set; }
        [JsonPropertyName("featured")] public bool Featured { get; set; }
        [JsonPropertyName("epic")] public bool Epic { get; set; }
        [JsonPropertyName("offline")] public bool OfflinePack { get; set; }
        [JsonPropertyName("daily")] public bool Daily { get; set; }
        [JsonPropertyName("weekly")] public bool Weekly { get; set; }
        [JsonPropertyName("version")] public int Version { get; set; }
        [JsonPropertyName("length")] public double Length { get; set; }
        [JsonPropertyName("created")] public string Created { get; set; } = "";
        [JsonPropertyName("updated")] public string Updated { get; set; } = "";

        public override string ToString() => $"#{Id} {Name} — {Author}";
    }

    public class LevelFullDto : LevelDto
    {
        [JsonPropertyName("data")] public string Data { get; set; } = "";
    }

    public class FriendDto
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("since")] public string Since { get; set; } = "";
        [JsonPropertyName("stars")] public int Stars { get; set; }
    }

    public class FriendRequestDto
    {
        [JsonPropertyName("from")] public string From { get; set; } = "";
        [JsonPropertyName("to")] public string To { get; set; } = "";
        [JsonPropertyName("created")] public string Created { get; set; } = "";
    }

    public class ScoreDto
    {
        [JsonPropertyName("user")] public string User { get; set; } = "";
        [JsonPropertyName("percent")] public double Percent { get; set; }
        [JsonPropertyName("attempts")] public int Attempts { get; set; }
        [JsonPropertyName("coins")] public int Coins { get; set; }
        [JsonPropertyName("date")] public string Date { get; set; } = "";
    }

    public class SongDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("artist")] public string Artist { get; set; } = "";
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("url")] public string Url { get; set; } = "";
        [JsonPropertyName("cached")] public bool Cached { get; set; }
    }

    /// <summary>Комментарий к уровню.</summary>
    public class CommentDto
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("author")] public string Author { get; set; } = "";
        [JsonPropertyName("text")] public string Text { get; set; } = "";
        [JsonPropertyName("created")] public string Created { get; set; } = "";
        [JsonPropertyName("avatar")] public int Avatar { get; set; }
    }

    /// <summary>Ответ на вход в комнату мультиплеера.</summary>
    public class MpJoinDto
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("room")] public string Room { get; set; } = "";
        [JsonPropertyName("max")] public int Max { get; set; }
        [JsonPropertyName("players")] public int Players { get; set; }
    }

    /// <summary>Другой игрок в комнате.</summary>
    public class MpPlayerDto
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("x")] public double X { get; set; }
        [JsonPropertyName("y")] public double Y { get; set; }
        [JsonPropertyName("mode")] public int Mode { get; set; }
        [JsonPropertyName("dir")] public int Dir { get; set; } = 1;
        [JsonPropertyName("mini")] public bool Mini { get; set; }
        [JsonPropertyName("dead")] public bool Dead { get; set; }
        [JsonPropertyName("percent")] public double Percent { get; set; }
        [JsonPropertyName("color")] public int Color { get; set; }
    }

    public class MpStateDto
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("room")] public string Room { get; set; } = "";
        [JsonPropertyName("max")] public int Max { get; set; }
        [JsonPropertyName("players")] public List<MpPlayerDto> Players { get; set; } = new();
    }

    public class DailyDto
    {
        [JsonPropertyName("date")] public string Date { get; set; } = "";
        [JsonPropertyName("level")] public LevelDto Level { get; set; }
        [JsonPropertyName("secondsLeft")] public long SecondsLeft { get; set; }
    }

    public class VersionDto
    {
        [JsonPropertyName("version")] public string Version { get; set; } = "";
        [JsonPropertyName("exe")] public string Exe { get; set; } = "";
        [JsonPropertyName("apk")] public string Apk { get; set; } = "";
        [JsonPropertyName("notes")] public string Notes { get; set; } = "";
        [JsonPropertyName("date")] public string Date { get; set; } = "";
    }

    public class LoginResult
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("token")] public string Token { get; set; } = "";
        [JsonPropertyName("account")] public AccountDto Account { get; set; }
        [JsonPropertyName("error")] public string Error { get; set; } = "";
    }

    public class ApiReply<T>
    {
        public bool Ok;
        public string Error = "";
        public T Data;
        public bool FromCache;
        public static ApiReply<T> Fail(string e) => new ApiReply<T> { Ok = false, Error = e };
        public static ApiReply<T> Good(T d, bool cache = false) => new ApiReply<T> { Ok = true, Data = d, FromCache = cache };
    }

    /// <summary>Короткий ответ сервера «готово».</summary>
    public class OkDto
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("error")] public string Error { get; set; } = "";
    }

    /// <summary>Сообщение в чате или в поддержке.</summary>
    public class ChatMessageDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("from")] public string From { get; set; } = "";
        [JsonPropertyName("to")] public string To { get; set; } = "";
        [JsonPropertyName("text")] public string Text { get; set; } = "";
        [JsonPropertyName("at")] public string At { get; set; } = "";
    }

    /// <summary>Переписка целиком.</summary>
    public class ChatDto
    {
        [JsonPropertyName("peer")] public string Peer { get; set; } = "";
        [JsonPropertyName("messages")] public List<ChatMessageDto> Messages { get; set; } = new();
    }

    /// <summary>Строчка в списке переписок.</summary>
    public class ChatHeadDto
    {
        [JsonPropertyName("peer")] public string Peer { get; set; } = "";
        [JsonPropertyName("support")] public bool Support { get; set; }
        [JsonPropertyName("last")] public string Last { get; set; } = "";
        [JsonPropertyName("lastFrom")] public string LastFrom { get; set; } = "";
        [JsonPropertyName("at")] public string At { get; set; } = "";
        [JsonPropertyName("unread")] public int Unread { get; set; }
    }

    /// <summary>Один лицензионный ключ.</summary>
    public class LicenceDto
    {
        [JsonPropertyName("key")] public string Key { get; set; } = "";
        [JsonPropertyName("created")] public string Created { get; set; } = "";
        [JsonPropertyName("note")] public string Note { get; set; } = "";
        [JsonPropertyName("user")] public string User { get; set; } = "";
        [JsonPropertyName("usedAt")] public string UsedAt { get; set; } = "";
        [JsonPropertyName("used")] public bool Used { get; set; }
        [JsonPropertyName("revoked")] public bool Revoked { get; set; }
        [JsonPropertyName("off")] public bool Off { get; set; }
    }

    /// <summary>Ответ сервера про лицензию этого аккаунта.</summary>
    public class LicenceMineDto
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("error")] public string Error { get; set; } = "";
        [JsonPropertyName("licensed")] public bool Licensed { get; set; }
        /// <summary>ok · none · off (владелец отключил) · revoked (отозван админом).</summary>
        [JsonPropertyName("reason")] public string Reason { get; set; } = "";
        [JsonPropertyName("admin")] public bool Admin { get; set; }
        [JsonPropertyName("key")] public string Key { get; set; } = "";
        [JsonPropertyName("licences")] public List<LicenceDto> Licences { get; set; } = new();
    }

    /// <summary>Обращение в поддержку — для админки.</summary>
    public class SupportHeadDto
    {
        [JsonPropertyName("user")] public string User { get; set; } = "";
        [JsonPropertyName("last")] public string Last { get; set; } = "";
        [JsonPropertyName("lastFrom")] public string LastFrom { get; set; } = "";
        [JsonPropertyName("at")] public string At { get; set; } = "";
        [JsonPropertyName("count")] public int Count { get; set; }
        [JsonPropertyName("waiting")] public bool Waiting { get; set; }
    }
}
