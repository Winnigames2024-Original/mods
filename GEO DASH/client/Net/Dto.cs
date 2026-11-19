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
}
