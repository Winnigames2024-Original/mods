using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NareLauncher.Core
{
    public static class GameLauncher
    {
        public const string LauncherName = "NareLauncher";
        /// <summary>
        /// Версия лаунчера. Берётся из самой сборки, чтобы не разъезжалась
        /// с тем, что написано в exe: правим номер в одном месте и забываем.
        /// </summary>
        public static readonly string LauncherVersion = ReadVersion();

        private static string ReadVersion()
        {
            try
            {
                var name = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                if (name != null)
                    return name.Revision > 0
                        ? name.Major + "." + name.Minor + "." + name.Build + "." + name.Revision
                        : name.Build > 0
                            ? name.Major + "." + name.Minor + "." + name.Build
                            : name.Major + "." + name.Minor;
            }
            catch { }

            return "1.1";
        }

        public static Process Current;

        /// <summary>Если задано, игра сразу зайдёт на этот сервер (адрес или адрес:порт).</summary>
        public static string QuickServer;

        /// <summary>Запуск «только на этот сервер»: в паузе не будет выхода.</summary>
        public static bool LockToServer;

        public static event Action<int> Exited;

        /// <summary>Устанавливает (если нужно) и запускает версию.</summary>
        public static async Task<Process> LaunchAsync(string versionId, Account account,
                                                      IProgress<InstallStatus> progress,
                                                      CancellationToken ct)
        {
            if (account == null) throw new Exception("Не выбран аккаунт. Добавьте его на вкладке «Аккаунты».");

            var resolved = await VersionInstaller.InstallAsync(versionId, progress, ct).ConfigureAwait(false);

            // Сборка Forge Visuals всегда идёт со встроенным модом: обновляем его
            // при каждом запуске. Именно обновляем, а не перезаписываем вслепую —
            // если в папке сборки лежит jar свежее нашего, он остаётся на месте.
            // мод нужен и в обычной сборке с Forge: раньше он доставался только
            // профилю Visuals, и люди не понимали, почему его нет
            bool visualsBuild = versionId != null &&
                versionId.EndsWith(LoaderInstaller.VisualsSuffix, StringComparison.OrdinalIgnoreCase);

            var catalogEntry = versionId == null ? null : VersionCatalog.Find(versionId);
            bool forgeBuild = catalogEntry != null &&
                (string.Equals(catalogEntry.Loader, "forge", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(catalogEntry.Loader, "neoforge", StringComparison.OrdinalIgnoreCase));

            if (visualsBuild || forgeBuild)
            {
                if (Settings.Current.AutoUpdateVisuals)
                {
                    // сначала спрашиваем GitHub, потом докладываем встроенные
                    if (Settings.Current.UpdateFromGitHub)
                    {
                        string fromWeb = await ModUpdater.UpdateAsync(versionId, progress, ct)
                                                         .ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(fromWeb)) Log.Info(fromWeb);
                    }

                    Log.Info(BundledMods.Refresh(versionId, ModUpdater.ManagedPrefix));

                    // последнее слово: в сборке остаётся по одной версии каждого мода
                    string extra = BundledMods.KeepOneVersion(Paths.ModsDir(versionId));
                    if (!string.IsNullOrEmpty(extra)) Log.Info(extra);
                }
                else
                    Log.Info("Автообновление мода выключено — что лежит в сборке, то и запускаем.");
            }

            int requiredJava = JavaLocator.RequiredMajor(resolved);
            var java = JavaLocator.Pick(requiredJava);

            if (java == null)
            {
                if (progress != null)
                    progress.Report(new InstallStatus { Text = "Java не найдена — скачиваю Java " + requiredJava + "…", Indeterminate = true });
                java = await JavaLocator.DownloadJavaAsync(requiredJava, progress, ct).ConfigureAwait(false);
            }
            else if (java.Major > 0 && java.Major < requiredJava)
            {
                if (progress != null)
                    progress.Report(new InstallStatus { Text = "Нужна Java " + requiredJava + ", скачиваю…", Indeterminate = true });
                java = await JavaLocator.DownloadJavaAsync(requiredJava, progress, ct).ConfigureAwait(false);
            }

            if (progress != null)
                progress.Report(new InstallStatus { Text = "Запускаю Minecraft…", Indeterminate = true });

            var psi = BuildStartInfo(versionId, resolved, account, java);
            QuickServer = null;
            LockToServer = false;
            Log.Info("Команда запуска: \"" + psi.FileName + "\" " + psi.Arguments);

            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.OutputDataReceived += (s, e) => { if (e.Data != null) Log.Game(e.Data); };
            proc.ErrorDataReceived += (s, e) => { if (e.Data != null) Log.Game(e.Data); };
            proc.Exited += (s, e) =>
            {
                int code = 0;
                try { code = proc.ExitCode; } catch { }
                Log.Info("Minecraft завершился с кодом " + code);
                Current = null;
                var h = Exited;
                if (h != null) h(code);
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            Current = proc;

            return proc;
        }

        public static ProcessStartInfo BuildStartInfo(string versionId, Json resolved,
                                                      Account account, JavaInstall java)
        {
            var features = VersionInstaller.Features();
            var libs = LibraryUtil.ResolveAll(resolved, features);

            string jarId = resolved["jar"].AsString(null);
            if (string.IsNullOrEmpty(jarId)) jarId = resolved["inheritsFrom"].AsString(null);
            if (string.IsNullOrEmpty(jarId)) jarId = versionId;

            // ---- classpath ----
            var cp = new List<string>();
            foreach (var l in libs)
                if (l.Classpath && File.Exists(l.Path) && !cp.Contains(l.Path)) cp.Add(l.Path);

            string clientJar = Paths.VersionJar(jarId);
            if (File.Exists(clientJar)) cp.Add(clientJar);

            string classpath = string.Join(";", cp.ToArray());

            // ---- папки ----
            string gameDir = Paths.InstanceDir(versionId);
            Paths.Ensure(gameDir);
            Paths.Ensure(Path.Combine(gameDir, "mods"));
            Paths.Ensure(Path.Combine(gameDir, "resourcepacks"));
            Paths.Ensure(Path.Combine(gameDir, "shaderpacks"));
            Paths.Ensure(Path.Combine(gameDir, "saves"));

            string natives = Paths.NativesDir(versionId);
            Paths.Ensure(natives);

            string assetsIndexName = resolved["assetIndex"]["id"].AsString(resolved["assets"].AsString("legacy"));
            string assetsRoot = Paths.AssetsDir;
            string gameAssets = assetsRoot;

            string idxPath = Path.Combine(Paths.AssetIndexesDir, assetsIndexName + ".json");
            var idx = Json.ParseFile(idxPath);
            if (idx["virtual"].AsBool(false))
                gameAssets = Path.Combine(Paths.AssetVirtualDir, assetsIndexName);
            if (idx["map_to_resources"].AsBool(false))
                gameAssets = Path.Combine(gameDir, "resources");

            // ---- подстановки ----
            var vars = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "auth_player_name", account.PlayName },
                { "version_name", versionId },
                { "game_directory", gameDir },
                { "assets_root", assetsRoot },
                { "game_assets", gameAssets },
                { "assets_index_name", assetsIndexName },
                { "auth_uuid", AccountStore.OfflineUuid(account.PlayName) },
                { "auth_access_token", account.AccessToken },
                { "auth_session", "token:" + account.AccessToken + ":" + AccountStore.OfflineUuid(account.PlayName) },
                { "user_type", account.UserType },
                { "version_type", resolved["type"].AsString("release") },
                { "user_properties", "{}" },
                { "clientid", "" },
                { "auth_xuid", "" },
                { "natives_directory", natives },
                { "launcher_name", LauncherName },
                { "launcher_version", LauncherVersion },
                { "classpath", classpath },
                { "classpath_separator", ";" },
                { "library_directory", Paths.LibrariesDir },
                { "resolution_width", Settings.Current.WindowWidth.ToString() },
                { "resolution_height", Settings.Current.WindowHeight.ToString() },
                { "profile_name", LauncherName }
            };

            // ---- JVM-аргументы ----
            var jvm = new List<string>();
            jvm.Add("-Xmx" + Settings.Current.RamMb + "M");
            jvm.Add("-Xms" + Math.Max(256, Settings.Current.RamMb / 4) + "M");
            jvm.Add("-Djava.net.preferIPv4Stack=true");
            jvm.Add("-Dminecraft.launcher.brand=" + LauncherName);
            jvm.Add("-Dminecraft.launcher.version=" + LauncherVersion);

            // защита от уязвимости log4j в версиях 1.7–1.18
            jvm.Add("-Dlog4j2.formatMsgNoLookups=true");

            foreach (var extra in SplitArgs(Settings.Current.JvmArgs))
                jvm.Add(extra);

            var logArg = resolved["logging"]["client"]["argument"].AsString(null);
            string logId = resolved["logging"]["client"]["file"]["id"].AsString(null);
            if (!string.IsNullOrEmpty(logArg) && !string.IsNullOrEmpty(logId))
            {
                string logPath = Path.Combine(Paths.AssetsDir, "log_configs", logId);
                if (File.Exists(logPath))
                    jvm.Add(logArg.Replace("${path}", logPath));
            }

            var versionJvm = CollectArgs(resolved["arguments"]["jvm"], features, vars);
            if (versionJvm.Count == 0)
            {
                // старый формат — собираем стандартный набор сами
                versionJvm.Add("-Djava.library.path=" + natives);
                versionJvm.Add("-cp");
                versionJvm.Add(classpath);
            }
            jvm.AddRange(versionJvm);

            // ---- аргументы игры ----
            List<string> game;
            string legacyArgs = resolved["minecraftArguments"].AsString(null);
            if (!string.IsNullOrEmpty(legacyArgs))
            {
                game = new List<string>();
                foreach (var token in SplitArgs(legacyArgs))
                    game.Add(Substitute(token, vars));
            }
            else
            {
                game = CollectArgs(resolved["arguments"]["game"], features, vars);
            }

            // NareVisuals включается только этим аргументом — без него мод молчит
            if (Settings.Current.Visuals && !game.Contains("--visuals"))
            {
                game.Add("--visuals");
                Log.Info("Передаём игре --visuals: визуалы NareVisuals будут показаны.");
            }

            // команды, которые мод отправит сам при входе
            string onJoin = (Settings.Current.JoinCommands ?? "").Replace("\r", " ")
                                                                 .Replace("\n", ";")
                                                                 .Replace("\"", "'")
                                                                 .Trim();
            if (onJoin.Length > 0 && Settings.Current.Visuals)
            {
                game.Add("--onjoin");
                game.Add(onJoin);
                Log.Info("При входе мод выполнит: " + onJoin);
            }

            // свои аргументы игры — как игрок их написал
            foreach (var extra in SplitArgs(Settings.Current.GameArgs))
            {
                if (game.Contains(extra)) continue;
                game.Add(Substitute(extra, vars));
                Log.Info("Свой аргумент игре: " + extra);
            }

            if (LockToServer && Settings.Current.Visuals)
            {
                game.Add("--lockserver");
                Log.Info("Запуск только на этот сервер: выход из игры в паузе будет закрыт.");
            }

            if (Settings.Current.Fullscreen)
            {
                if (!game.Contains("--fullscreen")) game.Add("--fullscreen");
            }
            else
            {
                if (!game.Contains("--width"))
                {
                    game.Add("--width"); game.Add(Settings.Current.WindowWidth.ToString());
                    game.Add("--height"); game.Add(Settings.Current.WindowHeight.ToString());
                }
            }

            // ---- сразу на сервер ----
            string quick = QuickServer;
            if (!string.IsNullOrEmpty(quick))
            {
                string host = quick;
                string port = "25565";
                int colon = quick.LastIndexOf(':');
                if (colon > 0)
                {
                    host = quick.Substring(0, colon);
                    port = quick.Substring(colon + 1);
                }

                // в новых версиях за это отвечает quickPlay, в старых — пара --server/--port
                bool modern = resolved["arguments"]["game"].ToJson(false).Contains("quickPlayMultiplayer");
                if (modern)
                {
                    game.Add("--quickPlayMultiplayer");
                    game.Add(host + ":" + port);
                }
                else
                {
                    game.Add("--server"); game.Add(host);
                    game.Add("--port"); game.Add(port);
                }

                Log.Info("Заходим сразу на сервер " + host + ":" + port);
            }

            // ---- финальная командная строка ----
            var all = new List<string>();
            all.AddRange(jvm);
            all.Add(resolved["mainClass"].AsString("net.minecraft.client.main.Main"));
            all.AddRange(game);

            var sb = new StringBuilder();
            foreach (var a in all)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(Quote(a));
            }

            var psi = new ProcessStartInfo
            {
                FileName = java.Path,
                Arguments = sb.ToString(),
                WorkingDirectory = gameDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            return psi;
        }

        /// <summary>Разворачивает массив аргументов нового формата с учётом rules.</summary>
        private static List<string> CollectArgs(Json array, Dictionary<string, bool> features,
                                                Dictionary<string, string> vars)
        {
            var result = new List<string>();
            if (!array.IsArray) return result;

            foreach (var item in array.Items)
            {
                if (item.Type == JsonType.String)
                {
                    result.Add(Substitute(item.Str, vars));
                }
                else if (item.IsObject)
                {
                    if (!LibraryUtil.RulesAllow(item["rules"], features)) continue;
                    var value = item["value"];
                    if (value.Type == JsonType.String) result.Add(Substitute(value.Str, vars));
                    else foreach (var v in value.Items) result.Add(Substitute(v.AsString(""), vars));
                }
            }
            return result;
        }

        private static string Substitute(string s, Dictionary<string, string> vars)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf("${", StringComparison.Ordinal) < 0) return s;
            foreach (var kv in vars)
                s = s.Replace("${" + kv.Key + "}", kv.Value ?? "");
            return s;
        }

        /// <summary>Разбивает строку аргументов, уважая кавычки.</summary>
        public static List<string> SplitArgs(string line)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(line)) return result;

            var sb = new StringBuilder();
            bool quoted = false;
            foreach (char c in line)
            {
                if (c == '"') { quoted = !quoted; continue; }
                if (!quoted && (c == ' ' || c == '\t'))
                {
                    if (sb.Length > 0) { result.Add(sb.ToString()); sb.Clear(); }
                    continue;
                }
                sb.Append(c);
            }
            if (sb.Length > 0) result.Add(sb.ToString());
            return result;
        }

        private static string Quote(string arg)
        {
            if (string.IsNullOrEmpty(arg)) return "\"\"";
            if (arg.IndexOf(' ') < 0 && arg.IndexOf('\t') < 0 && arg.IndexOf('"') < 0) return arg;

            // хвостовые обратные слэши удваиваем, иначе они экранируют закрывающую кавычку
            int tail = 0;
            while (tail < arg.Length && arg[arg.Length - 1 - tail] == '\\') tail++;
            string body = arg.Replace("\"", "\\\"");
            if (tail > 0) body += new string('\\', tail);
            return "\"" + body + "\"";
        }

        public static bool IsRunning
        {
            get
            {
                try { return Current != null && !Current.HasExited; }
                catch { return false; }
            }
        }

        public static void Kill()
        {
            try { if (IsRunning) Current.Kill(); }
            catch { }
        }
    }
}
