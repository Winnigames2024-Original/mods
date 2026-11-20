using System.Diagnostics;
using System.ServiceProcess;

namespace GeoDashSetup
{
    /// <summary>
    /// Служба автообновления: раз в несколько минут смотрит update.json на GitHub
    /// и, если вышла новая версия, тихо скачивает новый exe. Пока игра запущена,
    /// файл не подменяется — обновление ляжет сразу после выхода из игры.
    /// </summary>
    public class UpdateService : ServiceBase
    {
        readonly string _dir;
        readonly CancellationTokenSource _stop = new();
        Task _loop;

        /// <summary>Как часто проверять обновление.</summary>
        public static TimeSpan Period = TimeSpan.FromMinutes(30);

        public UpdateService(string dir)
        {
            _dir = dir;
            ServiceName = Cfg.ServiceName;
            CanStop = true;
            CanShutdown = true;
        }

        protected override void OnStart(string[] args) => _loop = Task.Run(() => LoopAsync(_stop.Token));

        protected override void OnStop()
        {
            _stop.Cancel();
            try { _loop?.Wait(TimeSpan.FromSeconds(10)); } catch { }
        }

        async Task LoopAsync(CancellationToken ct)
        {
            // при старте системы ждём минуту: сеть обычно ещё поднимается
            try { await Task.Delay(TimeSpan.FromMinutes(1), ct); } catch { return; }

            while (!ct.IsCancellationRequested)
            {
                try { await TickAsync(_dir); }
                catch (Exception ex) { Log(_dir, "сбой: " + ex.Message); }
                try { await Task.Delay(Period, ct); } catch { return; }
            }
        }

        /// <summary>Одна проверка обновления. Используется и службой, и кнопкой в установщике.</summary>
        public static async Task<string> TickAsync(string dir)
        {
            var info = await Net.CheckAsync();
            if (info == null) return "Сервер обновлений недоступен";

            string local = Net.LocalVersion(dir);
            if (!Net.IsNewer(info.version, local)) return "Установлена последняя версия " + local;

            string staged = Path.Combine(dir, "GEO DASH.new.exe");

            Log(dir, $"есть версия {info.version} (у нас {local}) — качаю");

            if (!await Net.DownloadFirstAsync(Net.GameUrls(info), staged))
            { Log(dir, "скачать не получилось"); return "Скачать не получилось"; }

            if (GameRunning())
            {
                Log(dir, "игра запущена — подменю файл после выхода");
                return "Обновление скачано, применится после выхода из игры";
            }

            try
            {
                string exe = Cfg.GameExe(dir);
                if (File.Exists(exe)) File.Delete(exe);
                File.Move(staged, exe);
                Cfg.WriteVersion(dir, info.version);     // записали новую версию в version.txt
                Log(dir, "обновлено до " + info.version);
                return "Обновлено до " + info.version;
            }
            catch (Exception ex)
            {
                Log(dir, "подменить файл не вышло: " + ex.Message);
                return "Файл занят — обновление применится позже";
            }
        }

        /// <summary>Игра сейчас работает?</summary>
        public static bool GameRunning()
        {
            try { return Process.GetProcessesByName("GEO DASH").Length > 0; }
            catch { return false; }
        }

        public static void Log(string dir, string text)
        {
            try
            {
                File.AppendAllText(Cfg.LogPath(dir),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + Environment.NewLine);
            }
            catch { }
        }

        // ---------------------------------------------------------- установка службы

        /// <summary>Поставить службу в Windows и запустить её.</summary>
        public static string Install(string dir)
        {
            string exe = Cfg.SetupExe(dir);
            if (!File.Exists(exe)) return "Нет файла службы";

            Sc($"stop {Cfg.ServiceName}");
            Sc($"delete {Cfg.ServiceName}");
            var r = Sc($"create {Cfg.ServiceName} binPath= \"\\\"{exe}\\\" --service \\\"{dir}\\\"\" " +
                       $"start= auto DisplayName= \"{Cfg.ServiceTitle}\"");
            if (!r.ok) return "Не удалось создать службу: " + r.text;

            Sc($"description {Cfg.ServiceName} \"Проверяет новые версии GEO DASH и скачивает их.\"");
            // если упала — поднимаем снова
            Sc($"failure {Cfg.ServiceName} reset= 86400 actions= restart/60000/restart/60000/restart/120000");
            Sc($"start {Cfg.ServiceName}");
            return "Служба автообновления установлена";
        }

        public static void Uninstall()
        {
            Sc($"stop {Cfg.ServiceName}");
            Sc($"delete {Cfg.ServiceName}");
        }

        public static bool Installed()
        {
            try { return ServiceController.GetServices().Any(s => s.ServiceName == Cfg.ServiceName); }
            catch { return false; }
        }

        static (bool ok, string text) Sc(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("sc.exe", args)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                using var p = Process.Start(psi);
                string outp = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(20000);
                return (p.ExitCode == 0, outp.Trim());
            }
            catch (Exception ex) { return (false, ex.Message); }
        }
    }
}
