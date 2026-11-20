using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GEO_DASH.Net
{
    /// <summary>
    /// Лицензия игры. Ключ вида GD-XXXX-XXXX-XXXX-XXXX активируется один раз и
    /// навсегда остаётся за аккаунтом — на всех устройствах игрока.
    ///
    /// Проверяем две вещи:
    ///   1) игра стоит из установщика (рядом с exe лежит его метка install.json);
    ///   2) на аккаунте активирован ключ.
    ///
    /// Активация запоминается рядом с настройками, поэтому без интернета игра
    /// запускается как обычно. У админа игра открыта всегда.
    /// </summary>
    public static class Licence
    {
        // алфавит Крокфорда: без похожих друг на друга I, L, O, U
        const string Alpha = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        public const string InstallerMark = "GEO DASH Setup";

        class State
        {
            public string Key { get; set; } = "";
            public string User { get; set; } = "";
            public string Device { get; set; } = "";
            public string CheckedAt { get; set; } = "";
        }

        static State _state;
        static string StatePath => Path.Combine(AppConfig.DataDir, "licence.json");

        static State St
        {
            get
            {
                if (_state != null) return _state;
                try
                {
                    _state = File.Exists(StatePath)
                        ? JsonSerializer.Deserialize<State>(File.ReadAllText(StatePath)) ?? new State()
                        : new State();
                }
                catch { _state = new State(); }
                return _state;
            }
        }

        static void SaveState()
        {
            try { File.WriteAllText(StatePath, JsonSerializer.Serialize(St)); } catch { }
        }

        /// <summary>Ключ, которым открыт этот аккаунт (для показа в настройках).</summary>
        public static string Key => St.Key ?? "";

        /// <summary>Лицензия подтверждена в этом запуске.</summary>
        public static bool Ok { get; private set; }

        /// <summary>Почему не пускает: none, off (ключ отключён), revoked (отозван),
        /// offline (первый запуск без интернета).</summary>
        public static string Reason { get; private set; } = "none";

        /// <summary>Сервер в этом запуске ответил — значит проверка настоящая.</summary>
        public static bool Online { get; private set; }

        /// <summary>Игра поставлена установщиком.</summary>
        public static bool Installed => InstallInfo() != null;

        // ------------------------------------------------------------ ключ

        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(16);
            foreach (var ch in s.ToUpperInvariant())
            {
                char c = ch;
                if (c == 'O') c = '0';
                else if (c == 'I' || c == 'L') c = '1';
                else if (c == 'U') c = 'V';
                if (Alpha.IndexOf(c) >= 0) sb.Append(c);
            }
            var t = sb.ToString();
            // «GD» в начале — часть записи ключа, а не его тело; но если без
            // этих двух знаков остаётся меньше 16, значит сам ключ с них и начат
            if (t.StartsWith("GD") && t.Length - 2 >= 16) t = t.Substring(2);
            return t.Length > 16 ? t.Substring(0, 16) : t;
        }

        static char CheckChar(string body)
        {
            int sum = 0;
            for (int i = 0; i < body.Length; i++)
            {
                int v = Alpha.IndexOf(body[i]);
                sum += (v < 0 ? 0 : v) * (i + 2);
            }
            return Alpha[sum % 32];
        }

        /// <summary>Контрольная сумма сходится — описку видно сразу, без сервера.</summary>
        public static bool WellFormed(string s)
        {
            var raw = Normalize(s);
            return raw.Length == 16 && CheckChar(raw.Substring(0, 15)) == raw[15];
        }

        /// <summary>Привести к виду GD-XXXX-XXXX-XXXX-XXXX.</summary>
        public static string Pretty(string s)
        {
            var raw = Normalize(s);
            if (raw.Length == 0) return "";
            var sb = new StringBuilder("GD");
            for (int i = 0; i < raw.Length; i++)
            {
                if (i % 4 == 0) sb.Append('-');
                sb.Append(raw[i]);
            }
            return sb.ToString();
        }

        // --------------------------------------------------- метка установщика

        public class Install
        {
            public string InstalledBy { get; set; } = "";
            public string Version { get; set; } = "";
            public string At { get; set; } = "";
            /// <summary>Ключ, введённый в установщике — активируем его сами.</summary>
            public string Key { get; set; } = "";
        }

        static Install _install;
        static bool _installRead;

        /// <summary>Метка установщика рядом с exe (её кладёт «GEO DASH Setup»).</summary>
        public static Install InstallInfo()
        {
            if (_installRead) return _install;
            _installRead = true;
            try
            {
                var dir = AppContext.BaseDirectory;
                var p = Path.Combine(dir, "install.json");
                if (File.Exists(p))
                {
                    var i = JsonSerializer.Deserialize<Install>(File.ReadAllText(p),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (i != null && string.Equals(i.InstalledBy, InstallerMark, StringComparison.OrdinalIgnoreCase))
                        _install = i;
                }
            }
            catch { }
            return _install;
        }

        /// <summary>Номер этого компьютера — чтобы в админке было видно устройства.</summary>
        public static string Device
        {
            get
            {
                if (!string.IsNullOrEmpty(St.Device)) return St.Device;
                string seed = Environment.MachineName + "|" + Environment.UserName + "|" +
                              Environment.ProcessorCount + "|" + Environment.OSVersion.Platform;
                using var sha = SHA256.Create();
                var h = sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
                St.Device = "pc-" + Convert.ToHexString(h, 0, 6).ToLowerInvariant();
                SaveState();
                return St.Device;
            }
        }

        // ------------------------------------------------------------ проверка

        /// <summary>
        /// Запомненная активация годится, только если её сделали на этом же
        /// компьютере и под этим же аккаунтом, и хотя бы раз подтвердил сервер.
        /// Поэтому подложенный из другой папки файл игру не открывает.
        /// </summary>
        public static bool LocalOkFor(string user) =>
            !string.IsNullOrEmpty(St.Key) &&
            !string.IsNullOrEmpty(St.CheckedAt) &&
            string.Equals(St.User, user, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(St.Device, Device, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Спросить сервер про лицензию. Если сети нет — верим запомненной активации.
        /// </summary>
        public static async Task<bool> CheckAsync()
        {
            string user = AppConfig.Settings.UserName ?? "";

            Online = false;
            var r = await ApiClient.LicenceMineAsync();
            if (r.Ok && r.Data != null)
            {
                Online = true;
                Reason = string.IsNullOrEmpty(r.Data.Reason) ? "none" : r.Data.Reason;
                if (r.Data.Licensed)
                {
                    St.Key = string.IsNullOrEmpty(r.Data.Key) ? (St.Key ?? "") : r.Data.Key;
                    St.User = user;
                    St.Device = Device;
                    St.CheckedAt = DateTime.Now.ToString("s");
                    SaveState();
                    Ok = true;
                    return true;
                }
                // сервер ответил «нет» — запомненную активацию держать смысла нет
                if (LocalOkFor(user)) { St.Key = ""; SaveState(); }
                Ok = false;
                return false;
            }

            // сервер молчит. Пускаем только если проверка уже проходила на этом
            // компьютере: первый запуск без интернета игру не открывает.
            Ok = LocalOkFor(user);
            if (!Ok) Reason = "offline";
            return Ok;
        }

        /// <summary>Активировать ключ. Возвращает текст ошибки или null, если всё вышло.</summary>
        public static async Task<string> ActivateAsync(string keyText)
        {
            if (!WellFormed(keyText)) return "Ключ набран неверно — проверьте символы";

            var r = await ApiClient.LicenceActivateAsync(Pretty(keyText), Device, "pc");
            if (!r.Ok) return r.Error ?? "Сервер не отвечает";
            if (r.Data == null || !r.Data.Ok) return r.Data?.Error ?? "Ключ не принят";

            St.Key = string.IsNullOrEmpty(r.Data.Key) ? Pretty(keyText) : r.Data.Key;
            St.User = AppConfig.Settings.UserName ?? "";
            St.Device = Device;
            St.CheckedAt = DateTime.Now.ToString("s");
            SaveState();
            Ok = true;
            return null;
        }

        /// <summary>Ключ, оставленный установщиком: сначала файл, потом метка.</summary>
        public static string InstallerKey()
        {
            var fromFile = KeyFile.Read(AppContext.BaseDirectory);
            if (WellFormed(fromFile)) return Pretty(fromFile);
            var i = InstallInfo();
            return i != null && WellFormed(i.Key) ? Pretty(i.Key) : "";
        }

        /// <summary>Ключ, который ввели в установщике — пробуем его сами, молча.</summary>
        public static async Task<bool> TryInstallerKeyAsync()
        {
            var k = InstallerKey();
            if (k.Length == 0) return false;
            return await ActivateAsync(k) == null;
        }

        /// <summary>Что показать игроку, когда игра не открывается.</summary>
        public static string ReasonText() => Reason switch
        {
            "off" => "Ваш ключ отключён — включите его в аккаунте на сайте",
            "revoked" => "Ваш ключ отозван администратором",
            "offline" => "Включите интернет: ключ проверяется на сервере",
            _ => "Вы не ввели лицензионный ключ"
        };

        /// <summary>Забыть активацию на этом компьютере (смена аккаунта).</summary>
        public static void Forget()
        {
            St.Key = ""; St.User = "";
            SaveState();
            Ok = false;
        }
    }
}
