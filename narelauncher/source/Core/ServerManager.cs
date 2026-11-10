using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        private TimeSpan _lastCpu;
        private DateTime _lastCpuAt;
        private double _cpuPct;

        public string Folder { get; private set; }
        public int Port { get; private set; }
        public bool Running { get { return _proc != null && !_proc.HasExited; } }
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
            File.WriteAllText(Path.Combine(Folder, "eula.txt"), "eula=true\n");
            WriteProps();

            string jar = Path.Combine(Folder, "server.jar");
            if (!File.Exists(jar))
                throw new FileNotFoundException("В папке сервера нет server.jar. Положите серверный jar как server.jar.");

            string java = JavaExe();
            if (java == null)
                throw new InvalidOperationException("Не найдена Java для запуска сервера.");

            var psi = new ProcessStartInfo
            {
                FileName = java,
                Arguments = "-Xmx2G -Xms1G -jar \"server.jar\" nogui",
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
            _proc.Start();
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();
        }

        public void Send(string cmd)
        {
            if (!Running) return;
            try { _proc.StandardInput.WriteLine(cmd); _proc.StandardInput.Flush(); }
            catch { }
        }

        public void Stop()
        {
            if (!Running) return;
            try
            {
                Send("stop");
                if (_proc != null && !_proc.WaitForExit(10000)) _proc.Kill();
            }
            catch { try { if (_proc != null) _proc.Kill(); } catch { } }
        }

        private void Line(string s)
        {
            try
            {
                if (s.IndexOf(" joined the game", StringComparison.Ordinal) >= 0) _players++;
                else if (s.IndexOf(" left the game", StringComparison.Ordinal) >= 0 && _players > 0) _players--;
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

        /// <summary>java.exe (с консолью) из лучшей найденной Java; для 1.21 нужна 21+.</summary>
        private static string JavaExe()
        {
            var all = JavaLocator.FindAll();
            if (all == null || all.Count == 0) return "java"; // надежда на PATH
            var best = all.OrderByDescending(j => j.Major).FirstOrDefault();
            if (best == null) return "java";
            string dir = Path.GetDirectoryName(best.Path) ?? "";
            string javaExe = Path.Combine(dir, "java.exe");
            return File.Exists(javaExe) ? javaExe : best.Path;
        }
    }
}
