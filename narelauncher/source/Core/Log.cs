using System;
using System.IO;
using System.Text;

namespace NareLauncher.Core
{
    public static class Log
    {
        private static readonly object Lock = new object();

        /// <summary>Подписка на строки лога (используется страницей «Консоль»).</summary>
        public static event Action<string> Line;

        public static void Info(string msg) { Write("INFO", msg); }
        public static void Warn(string msg) { Write("WARN", msg); }

        public static void Error(string msg, Exception ex = null)
        {
            Write("ERROR", ex == null ? msg : msg + " :: " + ex.Message);
            if (ex != null) Write("ERROR", ex.ToString());
        }

        public static void Game(string msg) { Write("GAME", msg); }

        private static void Write(string level, string msg)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] [" + level + "] " + msg;
            try
            {
                lock (Lock)
                {
                    File.AppendAllText(Paths.LogFile, line + Environment.NewLine, new UTF8Encoding(false));
                }
            }
            catch { /* лог не должен ронять лаунчер */ }

            var h = Line;
            if (h != null)
            {
                try { h(line); } catch { }
            }
        }

        public static void Reset()
        {
            try { if (File.Exists(Paths.LogFile)) File.Delete(Paths.LogFile); }
            catch { }
        }
    }
}
