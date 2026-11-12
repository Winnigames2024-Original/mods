using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Win32;

namespace NareLauncher.Core
{
    /// <summary>
    /// Ссылка вида <c>narelauncher://play?version=1.21.4&amp;loader=forge</c>.
    ///
    /// Её открывает сайт: Windows находит в реестре нашу программу и запускает
    /// её, передав ссылку одним аргументом. Лаунчер разбирает ссылку, при
    /// надобности спрашивает ник, ставит версию и запускает игру.
    ///
    /// Запись идёт в HKCU — прав администратора не нужно, и настройка живёт
    /// только у того, кто поставил лаунчер.
    /// </summary>
    public static class PlayLink
    {
        public const string Scheme = "narelauncher";

        /// <summary>Что попросили сделать. Пустая версия — просто открыть лаунчер.</summary>
        public class Request
        {
            public string Version = "";     // ванильная версия: 1.21.4
            public string Loader = "vanilla";
            public string Server = "";      // host[:port], если надо сразу зайти
            public bool OpenStudio = false; // открыть студию трансляции (narelauncher://stream)

            public bool HasVersion { get { return !string.IsNullOrEmpty(Version); } }

            public override string ToString()
            {
                return Version + " / " + Loader + (string.IsNullOrEmpty(Server) ? "" : " -> " + Server);
            }
        }

        /// <summary>Ссылка, с которой лаунчер запустили. Обрабатывается после загрузки окна.</summary>
        public static Request Pending;

        // ----------------------------------------------------------- регистрация

        /// <summary>
        /// Прописывает протокол на себя. Зовётся при каждом запуске: лаунчер
        /// мог переехать в другую папку, а обновление — подменить файл.
        /// </summary>
        public static void Register()
        {
            try
            {
                string exe = Assembly.GetEntryAssembly().Location;
                if (string.IsNullOrEmpty(exe)) return;

                string command = "\"" + exe + "\" \"%1\"";

                using (var classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + Scheme))
                {
                    if (classes == null) return;

                    classes.SetValue("", "URL:NareLauncher");
                    classes.SetValue("URL Protocol", "");

                    using (var icon = classes.CreateSubKey("DefaultIcon"))
                        if (icon != null) icon.SetValue("", exe + ",0");

                    using (var open = classes.CreateSubKey(@"shell\open\command"))
                    {
                        if (open == null) return;

                        string already = open.GetValue("") as string;
                        if (string.Equals(already, command, StringComparison.OrdinalIgnoreCase)) return;

                        open.SetValue("", command);
                    }
                }

                Log.Info("Ссылка " + Scheme + ":// теперь открывается лаунчером");
            }
            catch (Exception ex)
            {
                // не трагедия: просто кнопка «Играть» на сайте не сработает
                Log.Warn("Не удалось прописать ссылку " + Scheme + ":// — " + ex.Message);
            }
        }

        /// <summary>Убирает запись — на случай удаления лаунчера.</summary>
        public static void Unregister()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + Scheme, false); }
            catch (Exception ex) { Log.Warn("Не удалось убрать ссылку: " + ex.Message); }
        }

        // -------------------------------------------------------------- разбор

        /// <summary>Разбирает аргументы запуска. null — обычный запуск.</summary>
        public static Request FromArgs(string[] args)
        {
            if (args == null) return null;

            foreach (string arg in args)
            {
                if (string.IsNullOrEmpty(arg)) continue;
                if (!arg.StartsWith(Scheme + ":", StringComparison.OrdinalIgnoreCase)) continue;

                var request = Parse(arg);
                if (request != null) return request;
            }

            return null;
        }

        /// <summary>
        /// Разбирает саму ссылку. Делаем это вручную, а не через Uri: браузеры
        /// иногда дописывают в конец слеш, и Uri на таком спотыкается.
        /// </summary>
        public static Request Parse(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;

            int colon = url.IndexOf(':');
            if (colon < 0) return null;
            if (!string.Equals(url.Substring(0, colon), Scheme, StringComparison.OrdinalIgnoreCase))
                return null;

            string rest = url.Substring(colon + 1).TrimStart('/');

            string query = "";
            int question = rest.IndexOf('?');
            if (question >= 0)
            {
                query = rest.Substring(question + 1);
                rest = rest.Substring(0, question);
            }

            string action = rest.TrimEnd('/');
            bool isStudio = string.Equals(action, "stream", StringComparison.OrdinalIgnoreCase);
            if (action.Length > 0 && !isStudio && !string.Equals(action, "play", StringComparison.OrdinalIgnoreCase))
                return null;

            var values = Split(query);
            var request = new Request();

            request.OpenStudio = isStudio;
            request.Version = Clean(Value(values, "version"), 32);
            request.Loader = Loader(Value(values, "loader"));
            request.Server = Clean(Value(values, "server"), 128);

            return request;
        }

        private static Dictionary<string, string> Split(string query)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query)) return values;

            foreach (string piece in query.Split('&'))
            {
                if (piece.Length == 0) continue;

                int eq = piece.IndexOf('=');
                string key = eq < 0 ? piece : piece.Substring(0, eq);
                string value = eq < 0 ? "" : piece.Substring(eq + 1);

                try { value = Uri.UnescapeDataString(value.Replace('+', ' ')); }
                catch { }

                values[key] = value;
            }

            return values;
        }

        private static string Value(Dictionary<string, string> values, string key)
        {
            string found;
            return values.TryGetValue(key, out found) ? found : "";
        }

        /// <summary>
        /// Из ссылки приходит текст из интернета, поэтому оставляем только то,
        /// из чего состоят имена версий и адреса серверов.
        /// </summary>
        private static string Clean(string text, int limit)
        {
            if (string.IsNullOrEmpty(text)) return "";

            var kept = new System.Text.StringBuilder();
            foreach (char c in text.Trim())
            {
                if (char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' || c == ':')
                    kept.Append(c);

                if (kept.Length >= limit) break;
            }

            return kept.ToString();
        }

        private static string Loader(string text)
        {
            switch ((text ?? "").Trim().ToLowerInvariant())
            {
                case "fabric": return "fabric";
                case "quilt": return "quilt";
                case "forge": return "forge";
                case "neoforge":
                case "neo": return "neoforge";
                case "visuals": return "visuals";
                default: return "vanilla";
            }
        }

        public static string LoaderRu(string loader)
        {
            switch (loader)
            {
                case "fabric": return "Fabric";
                case "quilt": return "Quilt";
                case "forge": return "Forge";
                case "neoforge": return "NeoForge";
                case "visuals": return "Forge Visuals";
                default: return "без модов";
            }
        }
    }
}
