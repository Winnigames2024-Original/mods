using System;
using System.IO;

namespace NareLauncher.Core
{
    /// <summary>
    /// Устойчивый идентификатор этого компьютера для списка устройств аккаунта
    /// (как «Устройства» в Telegram). Хранится рядом с данными лаунчера, чтобы
    /// при переустановке лаунчера устройство оставалось тем же.
    /// </summary>
    public static class DeviceId
    {
        private static string _id;

        private static string File1
        {
            get { return Path.Combine(Paths.DataDir, "device.id"); }
        }

        /// <summary>GUID этого устройства — создаётся один раз и запоминается.</summary>
        public static string Get()
        {
            if (!string.IsNullOrEmpty(_id)) return _id;
            try
            {
                Directory.CreateDirectory(Paths.DataDir);
                if (File.Exists(File1))
                {
                    string saved = File.ReadAllText(File1).Trim();
                    if (saved.Length >= 8) { _id = saved; return _id; }
                }
                _id = Guid.NewGuid().ToString("N");
                File.WriteAllText(File1, _id);
            }
            catch
            {
                if (string.IsNullOrEmpty(_id)) _id = Guid.NewGuid().ToString("N");
            }
            return _id;
        }

        /// <summary>Человеческое имя устройства: «DESKTOP-ABC · Windows».</summary>
        public static string Name()
        {
            string machine;
            try { machine = Environment.MachineName; }
            catch { machine = "ПК"; }

            string os;
            try { os = OsLabel(); }
            catch { os = "Windows"; }

            return machine + " · " + os;
        }

        private static string OsLabel()
        {
            var v = Environment.OSVersion.Version;
            // Windows 11 всё ещё рапортует 10.0, различаем по номеру сборки
            if (v.Major == 10 && v.Build >= 22000) return "Windows 11";
            if (v.Major == 10) return "Windows 10";
            if (v.Major == 6 && v.Minor == 3) return "Windows 8.1";
            if (v.Major == 6 && v.Minor == 1) return "Windows 7";
            return "Windows";
        }
    }
}
