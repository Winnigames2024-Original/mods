using System.Security.Cryptography;
using System.Text;

namespace GEO_DASH.Net
{
    /// <summary>
    /// Файл с лицензионным ключом рядом с игрой — его кладёт установщик.
    ///
    /// Ключ лежит не открытым текстом: он зашифрован AES-256 на пароле, который
    /// знают только установщик и игра. Это не защита от того, кто разберёт
    /// программу — пароль всё равно внутри неё, — а защита от случайного взгляда:
    /// ключ не виден в блокноте и не утечёт со скриншотом папки.
    /// </summary>
    public static class KeyFile
    {
        public const string Name = "licence.txt";

        // пароль и соль одинаковы у игры и установщика
        const string Pass = "GEO DASH licence 2026 — Winnigames2024";
        static readonly byte[] Salt =
            { 0x47, 0x44, 0x4C, 0x49, 0x43, 0x32, 0x30, 0x32, 0x36, 0x57, 0x47, 0x32, 0x30, 0x32, 0x34, 0x21 };

        static (byte[] key, byte[] iv) Derive()
        {
            using var kdf = new Rfc2898DeriveBytes(Pass, Salt, 50_000, HashAlgorithmName.SHA256);
            return (kdf.GetBytes(32), kdf.GetBytes(16));
        }

        public static string Path(string dir) => System.IO.Path.Combine(dir, Name);

        /// <summary>Зашифровать ключ для записи в файл.</summary>
        public static string Protect(string licence)
        {
            try
            {
                var (k, iv) = Derive();
                using var aes = Aes.Create();
                aes.Key = k; aes.IV = iv;
                using var enc = aes.CreateEncryptor();
                var data = Encoding.UTF8.GetBytes(licence ?? "");
                return Convert.ToBase64String(enc.TransformFinalBlock(data, 0, data.Length));
            }
            catch { return ""; }
        }

        public static string Unprotect(string text)
        {
            try
            {
                var (k, iv) = Derive();
                using var aes = Aes.Create();
                aes.Key = k; aes.IV = iv;
                using var dec = aes.CreateDecryptor();
                var data = Convert.FromBase64String((text ?? "").Trim());
                return Encoding.UTF8.GetString(dec.TransformFinalBlock(data, 0, data.Length));
            }
            catch { return ""; }
        }

        /// <summary>Записать файл с ключом рядом с игрой.</summary>
        public static bool Write(string dir, string licence)
        {
            try
            {
                var body = new StringBuilder();
                body.AppendLine("GEO DASH — лицензионный ключ");
                body.AppendLine("Строка ниже зашифрована, читать её не нужно: игра разберёт сама.");
                body.AppendLine("Файл не удаляйте — без него игра спросит ключ заново.");
                body.AppendLine();
                body.AppendLine(Protect(licence));
                File.WriteAllText(Path(dir), body.ToString(), Encoding.UTF8);
                return true;
            }
            catch { return false; }
        }

        /// <summary>Прочитать ключ из файла рядом с игрой. Пусто — файла нет.</summary>
        public static string Read(string dir)
        {
            try
            {
                var p = Path(dir);
                if (!File.Exists(p)) return "";
                foreach (var line in File.ReadAllLines(p))
                {
                    var t = line.Trim();
                    if (t.Length < 16 || t.Contains(' ')) continue;   // пояснения пропускаем
                    var k = Unprotect(t);
                    if (!string.IsNullOrEmpty(k)) return k;
                }
            }
            catch { }
            return "";
        }
    }
}
