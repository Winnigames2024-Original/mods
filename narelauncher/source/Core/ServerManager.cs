using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace NareLauncher.Core
{
    /// <summary>
    /// Запуск выделенного Minecraft-сервера в папке: java -jar server.jar nogui,
    /// с перехватом консоли (лог + ввод команд). Offline-режим (вход через наши
    /// аккаунты), порт задаётся лаунчером.
    /// </summary>
    public class ServerManager
    {
        private Process _proc;
        private DateTime _startedAt;
        private int _players;
        private readonly System.Collections.Generic.HashSet<string> _online = new System.Collections.Generic.HashSet<string>();
        private TimeSpan _lastCpu;
        private DateTime _lastCpuAt;
        private double _cpuPct;

        public string Folder { get; private set; }
        public int Port { get; private set; }
        public bool Running { get { return _proc != null && !_proc.HasExited; } }
        public int Players { get { return _players; } }
        public string[] OnlineNames { get { lock (_online) { var a = new string[_online.Count]; _online.CopyTo(a); return a; } } }
        private static string LastToken(string s)
        {
            s = (s ?? "").TrimEnd();
            int sp = s.LastIndexOf(' ');
            string t = sp >= 0 ? s.Substring(sp + 1) : s;
            foreach (char c in t) if (!(char.IsLetterOrDigit(c) || c == '_')) return "";
            return t;
        }
        public double CpuPct { get { return Running ? _cpuPct : 0; } }
        public int RamMb
        {
            get { try { if (!Running) return 0; _proc.Refresh(); return (int)(_proc.WorkingSet64 / (1024 * 1024)); } catch { return 0; } }
        }
        public int UptimeSec
        {
            get { try { return Running ? (int)(DateTime.UtcNow - _startedAt).TotalSeconds : 0; } catch { return 0; } }
        }
        /// <summary>Пересчитать CPU% (вызывать периодически, иначе доля не обновляется).</summary>
        public void SampleCpu() { try { var _ = StatsJson(); } catch { } }
        public Action<string> OnLine;
        public Action OnExit;

        public ServerManager(string folder, int port)
        {
            Folder = folder;
            Port = port;
        }

        public void Start()
        {
            if (Running) return;
            Directory.CreateDirectory(Folder);
            KillStalePid();   // убить зависший сервер прошлого запуска (держит мир/лог)
            File.WriteAllText(Path.Combine(Folder, "eula.txt"), "eula=true\n");
            WriteProps();

            var cfg = ReadLaunchCfg(Folder);
            string java = cfg.JavaMajor > 0 ? JavaExeFor(cfg.JavaMajor) : JavaExe2();
            if (java == null)
                throw new InvalidOperationException("Не найдена Java для запуска сервера.");

            string mem = "-Xmx" + cfg.RamMax + "M -Xms" + cfg.RamMin + "M";
            string gc = GcFlag(cfg.Gc);
            string extra = (cfg.Args ?? "").Trim();
            string jvm = (mem + " " + gc + " " + extra).Trim();

            string launchFile = Path.Combine(Folder, "nare_launch.txt");
            string args;
            if (File.Exists(launchFile))
            {
                // Forge: память/GC/доп.аргументы пишем в user_jvm_args.txt
                try { File.WriteAllText(Path.Combine(Folder, "user_jvm_args.txt"), jvm + "\n"); } catch { }
                args = (File.ReadAllText(launchFile) ?? "").Trim();   // @user_jvm_args.txt @libraries/.../win_args.txt nogui
                if (args.Length == 0)
                    throw new InvalidOperationException("nare_launch.txt пуст — переустановите ядро Forge.");
            }
            else
            {
                string jarName = string.IsNullOrEmpty(cfg.Jar) ? "server.jar" : cfg.Jar;
                string jar = Path.Combine(Folder, jarName);
                if (!File.Exists(jar))
                    throw new FileNotFoundException("В папке сервера нет " + jarName + ". Нажмите «Скачать jar» и выберите ядро.");
                args = jvm + " -jar \"" + jarName + "\" nogui";
            }

            var psi = new ProcessStartInfo
            {
                FileName = java,
                Arguments = args,
                WorkingDirectory = Folder,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };
            _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _proc.OutputDataReceived += (s, e) => { if (e.Data != null) Line(e.Data); };
            _proc.ErrorDataReceived += (s, e) => { if (e.Data != null) Line(e.Data); };
            _proc.Exited += (s, e) => { var h = OnExit; if (h != null) h(); };
            _startedAt = DateTime.UtcNow;
            _players = 0;
            lock (_online) _online.Clear();
            _proc.Start();
            try { File.WriteAllText(Path.Combine(Folder, "nare_pid.txt"), _proc.Id.ToString()); } catch { }
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();
            StartAntiLag();
        }

        public void Send(string cmd)
        {
            if (!Running) return;
            try { _proc.StandardInput.WriteLine(cmd); _proc.StandardInput.Flush(); }
            catch { }
        }

        // ---------------------------------------------------------- анти-лаг (TNT)
        private System.Threading.Timer _antiTimer;
        private bool _antiInit;

        public class AntiLag { public bool On; public int Max = 100; }

        public static AntiLag ReadAntiLag(string folder)
        {
            try
            {
                string f = Path.Combine(folder, "nare_antilag.json");
                if (File.Exists(f)) { var j = Json.Parse(File.ReadAllText(f)); return new AntiLag { On = j["on"].AsBool(false), Max = j["max"].AsInt(100) }; }
            }
            catch { }
            return new AntiLag();
        }
        public static void WriteAntiLag(string folder, AntiLag a)
        {
            try { File.WriteAllText(Path.Combine(folder, "nare_antilag.json"), "{\"on\":" + (a.On ? "true" : "false") + ",\"max\":" + a.Max + "}"); } catch { }
        }

        private void StartAntiLag()
        {
            _antiInit = false;
            if (_antiTimer == null)
                _antiTimer = new System.Threading.Timer(_ => AntiTick(), null, 8000, 8000);
        }
        private void AntiTick()
        {
            try
            {
                if (!Running) return;
                var a = ReadAntiLag(Folder);
                if (!a.On) return;
                if (!_antiInit) { Send("scoreboard objectives add nareTnt dummy"); _antiInit = true; }
                int max = a.Max < 10 ? 10 : a.Max;
                Send("scoreboard players set #c nareTnt 0");
                Send("execute as @e[type=minecraft:tnt] run scoreboard players add #c nareTnt 1");
                Send("execute if score #c nareTnt matches " + max + ".. run kill @e[type=minecraft:tnt]");
            }
            catch { }
        }

        // ---------------------------------------------------------- вайтлист / опы
        /// <summary>Имена из whitelist.json или ops.json (читаются с диска).</summary>
        public string[] ReadNameList(string file)
        {
            var list = new List<string>();
            try
            {
                string f = Path.Combine(Folder, file);
                if (!File.Exists(f)) return list.ToArray();
                string txt = File.ReadAllText(f);
                int i = 0;
                while (true)
                {
                    int k = txt.IndexOf("\"name\"", i, StringComparison.Ordinal);
                    if (k < 0) break;
                    int q1 = txt.IndexOf('"', k + 6); if (q1 < 0) break;
                    int q2 = txt.IndexOf('"', q1 + 1); if (q2 < 0) break;
                    q1 = txt.IndexOf('"', q2 + 1); if (q1 < 0) break;      // открывающая кавычка значения
                    q2 = txt.IndexOf('"', q1 + 1); if (q2 < 0) break;      // закрывающая
                    list.Add(txt.Substring(q1 + 1, q2 - q1 - 1));
                    i = q2 + 1;
                }
            }
            catch { }
            return list.ToArray();
        }

        // ---------------------------------------------------------- бэкапы
        /// <summary>Делает zip мира(ов) в backups/. Если сервер онлайн — безопасно
        /// через save-off/save-all. Возвращает имя файла.</summary>
        public string MakeBackup()
        {
            string dir = Path.Combine(Folder, "backups");
            Directory.CreateDirectory(dir);
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string zip = Path.Combine(dir, "backup-" + stamp + ".zip");
            bool online = Running;
            try
            {
                if (online) { Send("save-off"); Send("save-all flush"); System.Threading.Thread.Sleep(2500); }
                using (var z = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create))
                {
                    foreach (var world in new[] { "world", "world_nether", "world_the_end" })
                    {
                        string wp = Path.Combine(Folder, world);
                        if (Directory.Exists(wp)) AddDir(z, wp, world);
                    }
                    foreach (var f in new[] { "server.properties", "whitelist.json", "ops.json", "banned-players.json" })
                    {
                        string fp = Path.Combine(Folder, f);
                        if (File.Exists(fp)) z.CreateEntryFromFile(fp, f);
                    }
                }
            }
            finally { if (online) Send("save-on"); }
            return Path.GetFileName(zip);
        }
        private static void AddDir(System.IO.Compression.ZipArchive z, string dir, string prefix)
        {
            foreach (var f in Directory.GetFiles(dir))
            {
                try { z.CreateEntryFromFile(f, prefix + "/" + Path.GetFileName(f)); } catch { }
            }
            foreach (var d in Directory.GetDirectories(dir))
                AddDir(z, d, prefix + "/" + Path.GetFileName(d));
        }
        public string[] ListBackups()
        {
            try
            {
                string dir = Path.Combine(Folder, "backups");
                if (!Directory.Exists(dir)) return new string[0];
                var files = new List<string>(Directory.GetFiles(dir, "*.zip"));
                files.Sort(); files.Reverse();
                for (int i = 0; i < files.Count; i++) files[i] = Path.GetFileName(files[i]);
                return files.ToArray();
            }
            catch { return new string[0]; }
        }
        /// <summary>Восстанавливает бэкап. Сервер должен быть остановлен (проверяет вызывающий).</summary>
        public void RestoreBackup(string name)
        {
            string zip = Path.Combine(Folder, "backups", Path.GetFileName(name));
            if (!File.Exists(zip)) throw new Exception("нет такого бэкапа");
            foreach (var world in new[] { "world", "world_nether", "world_the_end" })
            {
                string wp = Path.Combine(Folder, world);
                try { if (Directory.Exists(wp)) Directory.Delete(wp, true); } catch { }
            }
            System.IO.Compression.ZipFile.ExtractToDirectory(zip, Folder);
        }

        public void Stop()
        {
            try { if (_antiTimer != null) { _antiTimer.Dispose(); _antiTimer = null; } } catch { }
            try { File.Delete(Path.Combine(Folder, "nare_pid.txt")); } catch { }
            if (!Running) return;
            try
            {
                Send("stop");
                if (_proc != null && !_proc.WaitForExit(10000)) _proc.Kill();
            }
            catch { try { if (_proc != null) _proc.Kill(); } catch { } }
        }

        /// <summary>Убивает процесс сервера, оставшийся от прошлого запуска (по PID-файлу),
        /// иначе он держит session.lock мира и новый сервер не стартует.</summary>
        private void KillStalePid()
        {
            try
            {
                string f = Path.Combine(Folder, "nare_pid.txt");
                if (!File.Exists(f)) return;
                int pid;
                if (int.TryParse((File.ReadAllText(f) ?? "").Trim(), out pid) && pid > 0)
                {
                    try
                    {
                        var pr = Process.GetProcessById(pid);
                        string n = (pr.ProcessName ?? "").ToLowerInvariant();
                        if (n == "java" || n == "javaw") { pr.Kill(); pr.WaitForExit(6000); }
                    }
                    catch { }
                }
                try { File.Delete(f); } catch { }
            }
            catch { }
        }

        private void Line(string s)
        {
            try
            {
                int ji = s.IndexOf(" joined the game", StringComparison.Ordinal);
                int li = s.IndexOf(" left the game", StringComparison.Ordinal);
                if (ji > 0) { var nm = LastToken(s.Substring(0, ji)); if (nm.Length > 0) lock (_online) _online.Add(nm); }
                else if (li > 0) { var nm = LastToken(s.Substring(0, li)); if (nm.Length > 0) lock (_online) _online.Remove(nm); }
                lock (_online) _players = _online.Count;
            }
            catch { }
            var h = OnLine; if (h != null) h(s);
        }

        /// <summary>Внутренний JSON статистики для веб-панели: ram, up, players.</summary>
        public string StatsJson()
        {
            if (!Running) return null;
            int ram = 0, up = 0;
            try { _proc.Refresh(); ram = (int)(_proc.WorkingSet64 / (1024 * 1024)); } catch { }
            try { up = (int)(DateTime.UtcNow - _startedAt).TotalSeconds; } catch { }
            try
            {
                var cpu = _proc.TotalProcessorTime;
                var now = DateTime.UtcNow;
                if (_lastCpuAt != default(DateTime))
                {
                    double wall = (now - _lastCpuAt).TotalMilliseconds;
                    if (wall > 0)
                    {
                        double used = (cpu - _lastCpu).TotalMilliseconds;
                        double pct = used / wall / Environment.ProcessorCount * 100.0;
                        if (pct < 0) pct = 0; if (pct > 100) pct = 100;
                        _cpuPct = pct;
                    }
                }
                _lastCpu = cpu; _lastCpuAt = now;
            }
            catch { }
            int cpuI = (int)Math.Round(_cpuPct * 10);   // десятые доли %, делим на 10 на клиенте
            return "\"ram\":" + ram + ",\"up\":" + up + ",\"players\":" + _players + ",\"cpu\":" + cpuI;
        }

        private void WriteProps()
        {
            string path = Path.Combine(Folder, "server.properties");
            var lines = File.Exists(path) ? new List<string>(File.ReadAllLines(path)) : new List<string>();
            SetProp(lines, "server-port", Port.ToString());
            SetProp(lines, "online-mode", "false");          // вход через аккаунты лаунчера
            SetProp(lines, "enforce-secure-profile", "false");
            if (!lines.Any(l => l.StartsWith("motd="))) lines.Add("motd=NareLauncher server");
            File.WriteAllLines(path, lines);
        }

        private static void SetProp(List<string> lines, string key, string value)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].StartsWith(key + "="))
                {
                    lines[i] = key + "=" + value;
                    return;
                }
            }
            lines.Add(key + "=" + value);
        }

        /// <summary>java.exe под версию этого сервера (MC 1.21→Java21, 1.18–1.20→17, ≤1.16→8).</summary>
        public static string JavaExe() { return JavaExeFor(0); }

        public string JavaExe2() { return JavaExeFor(RequiredJava(ReadMcVersion())); }

        private static string JavaExeFor(int required)
        {
            var all = JavaLocator.FindAll();
            if (all == null || all.Count == 0) return "java";
            JavaInstall pick = null;
            if (required > 0)
            {
                // точное совпадение по major
                foreach (var j in all) if (j.Major == required) { pick = j; break; }
                // иначе наименьшая major >= required
                if (pick == null)
                {
                    foreach (var j in all.OrderBy(x => x.Major))
                        if (j.Major >= required) { pick = j; break; }
                }
            }
            if (pick == null) pick = all.OrderByDescending(j => j.Major).FirstOrDefault();
            if (pick == null) return "java";
            string dir = Path.GetDirectoryName(pick.Path) ?? "";
            string javaExe = Path.Combine(dir, "java.exe");
            return File.Exists(javaExe) ? javaExe : pick.Path;
        }

        private string ReadMcVersion()
        {
            try
            {
                string f = Path.Combine(Folder, "nare_core.txt");
                if (File.Exists(f))
                {
                    var p = File.ReadAllText(f).Split('|');
                    if (p.Length > 1) return p[1].Trim();
                }
            }
            catch { }
            return "";
        }

        public class LaunchCfg
        {
            public string Jar = "server.jar";
            public int RamMax = 2048, RamMin = 1024;
            public int JavaMajor = 0;      // 0 = авто по версии
            public string Gc = "g1";       // g1 | parallel | serial | none
            public string Args = "";
        }

        public static LaunchCfg ReadLaunchCfg(string folder)
        {
            var c = new LaunchCfg();
            try
            {
                string f = Path.Combine(folder, "nare_launch.json");
                if (File.Exists(f))
                {
                    var j = Json.Parse(File.ReadAllText(f));
                    c.Jar = j["jar"].AsString("server.jar");
                    c.RamMax = Math.Max(256, j["ramMax"].AsInt(2048));
                    c.RamMin = Math.Max(128, j["ramMin"].AsInt(1024));
                    if (c.RamMin > c.RamMax) c.RamMin = c.RamMax;
                    c.JavaMajor = j["java"].AsInt(0);
                    c.Gc = j["gc"].AsString("g1");
                    c.Args = j["args"].AsString("");
                }
            }
            catch { }
            return c;
        }

        public static void WriteLaunchCfg(string folder, LaunchCfg c)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"jar\":\"").Append((c.Jar ?? "server.jar").Replace("\\", "/").Replace("\"", ""))
              .Append("\",\"ramMax\":").Append(c.RamMax).Append(",\"ramMin\":").Append(c.RamMin)
              .Append(",\"java\":").Append(c.JavaMajor).Append(",\"gc\":\"").Append(c.Gc ?? "g1")
              .Append("\",\"args\":\"").Append((c.Args ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\"}");
            File.WriteAllText(Path.Combine(folder, "nare_launch.json"), sb.ToString());
        }

        private static string GcFlag(string gc)
        {
            switch ((gc ?? "").ToLowerInvariant())
            {
                case "parallel": return "-XX:+UseParallelGC";
                case "serial": return "-XX:+UseSerialGC";
                case "none": return "";
                default: return "-XX:+UseG1GC";
            }
        }

        /// <summary>Нужная major-версия Java для версии Minecraft.</summary>
        public static int RequiredJava(string mc)
        {
            if (string.IsNullOrEmpty(mc)) return 21;   // по умолчанию современная
            var p = mc.Split('.');
            int minor = 0, patch = 0;
            if (p.Length > 1) int.TryParse(new string(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.TakeWhile(p[1], char.IsDigit))), out minor);
            if (p.Length > 2) int.TryParse(new string(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.TakeWhile(p[2], char.IsDigit))), out patch);
            if (minor >= 21) return 21;
            if (minor == 20) return patch >= 5 ? 21 : 17;
            if (minor >= 18) return 17;
            if (minor == 17) return 17;
            return 8;
        }
    }
}
