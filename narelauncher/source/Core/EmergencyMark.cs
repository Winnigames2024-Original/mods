using System;
using System.IO;

namespace NareLauncher.Core
{
    /// <summary>
    /// Запоминает время последнего отработанного экстренного обновления, чтобы
    /// после перезапуска лаунчер не показывал одно и то же окно по кругу.
    /// </summary>
    public static class EmergencyMark
    {
        private static string File1
        {
            get { return Path.Combine(Paths.DataDir, "emergency.at"); }
        }

        public static long Load()
        {
            try
            {
                if (File.Exists(File1))
                {
                    long v;
                    if (long.TryParse(File.ReadAllText(File1).Trim(), out v)) return v;
                }
            }
            catch { }
            return 0;
        }

        public static void Save(long at)
        {
            try
            {
                Directory.CreateDirectory(Paths.DataDir);
                File.WriteAllText(File1, at.ToString());
            }
            catch { }
        }

        private static string FileFor(string key)
        {
            return Path.Combine(Paths.DataDir, key + ".at");
        }

        /// <summary>Отметка «последнее увиденное» по ключу (например «broadcast»).</summary>
        public static long LoadKey(string key)
        {
            try
            {
                string f = FileFor(key);
                if (File.Exists(f))
                {
                    long v;
                    if (long.TryParse(File.ReadAllText(f).Trim(), out v)) return v;
                }
            }
            catch { }
            return 0;
        }

        public static void SaveKey(string key, long at)
        {
            try
            {
                Directory.CreateDirectory(Paths.DataDir);
                File.WriteAllText(FileFor(key), at.ToString());
            }
            catch { }
        }
    }
}
