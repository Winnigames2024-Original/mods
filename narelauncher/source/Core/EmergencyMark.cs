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
    }
}
