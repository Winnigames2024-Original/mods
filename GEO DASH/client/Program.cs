using GEO_DASH.Core;
using GEO_DASH.Net;
using GEO_DASH.UI;

namespace GEO_DASH
{
    internal static class Program
    {
        /// <summary>Файл уровня, с которым игру открыли двойным кликом.</summary>
        public static string OpenLevelPath;

        /// <summary>Игрок нажал «сменить аккаунт» в окне лицензии — спрашиваем вход заново.</summary>
        public static bool RestartAuth;

        [STAThread]
        static void Main(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AppConfig.Load();

            Application.ThreadException += (s, e) => Crash(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Crash(e.ExceptionObject as Exception);

            // открыли файл уровня .gdl — запомним, покажем его сразу после входа
            foreach (var a in args)
            {
                if (string.IsNullOrWhiteSpace(a) || a.StartsWith("-")) continue;
                if (a.EndsWith(".gdl", StringComparison.OrdinalIgnoreCase) && File.Exists(a))
                {
                    OpenLevelPath = a;
                    break;
                }
            }

            // без аккаунта в игру не пускаем: авто-вход, иначе вход или регистрация,
            // а дальше лицензия: без ключа игра не запускается
            for (int tries = 0; tries < 4; tries++)
            {
                RestartAuth = false;
                if (!AuthForm.Ensure()) return;
                if (LicenceForm.Ensure()) break;
                if (!RestartAuth) return;
            }

            ShowCrashReport();          // прошлый запуск оборвался? покажем и расскажем админу
            AppConfig.Stage("главное меню");
            Application.Run(new MainForm());
            AppConfig.StageClear();     // вышли штатно — чёрный ящик чистим
        }

        /// <summary>Открыть уровень из файла .gdl: кладём в «мои уровни» и показываем карточку.</summary>
        public static void OpenLevelFile(string path)
        {
            try
            {
                var lv = LevelData.Load(path);
                if (string.IsNullOrWhiteSpace(lv.Settings.LocalId))
                    lv.Settings.LocalId = "file_" + Math.Abs(path.GetHashCode());
                LocalStore.SaveMyLevel(lv);
                Nav.Go(new PublicLevelInfoForm(lv, true));
            }
            catch (Exception ex)
            {
                GdDialog.Info(null, "УРОВЕНЬ НЕ ОТКРЫЛСЯ", ex.Message);
            }
        }

        static void Crash(Exception ex)
        {
            try
            {
                var log = Path.Combine(AppConfig.DataDir, "crash.log");
                File.AppendAllText(log, $"[{DateTime.Now:s}] {ex}\n\n");
                Report("ошибка", ex?.ToString() ?? "неизвестно", AppConfig.LastStage());
                GdDialog.Info(null, "ОШИБКА", (ex?.Message ?? "неизвестно") + "\n\nПодробности: " + log);
            }
            catch { }
        }

        /// <summary>Молча рассказать админу об ошибке — игроку ничего делать не нужно.</summary>
        static void Report(string kind, string text, string stage)
        {
            try { _ = Net.ApiClient.ReportAsync(kind, text, stage); } catch { }
        }

        /// <summary>
        /// Если в прошлый раз игра умерла жёстко (без исключения), чёрный ящик
        /// хранит последний этап: показываем это игроку окном и отправляем
        /// отчёт админу. Админу окно показывается и просто так — со списком
        /// всего, что прислали игроки.
        /// </summary>
        static void ShowCrashReport()
        {
            try
            {
                string stage = AppConfig.LastStage();
                bool crashed = !string.IsNullOrWhiteSpace(stage);
                bool admin = AppConfig.IsAdminUser;
                if (!crashed && !admin) return;

                string tail = "";
                var log = Path.Combine(AppConfig.DataDir, "crash.log");
                if (File.Exists(log))
                {
                    var lines = File.ReadAllLines(log);
                    tail = string.Join("\r\n", lines.Skip(Math.Max(0, lines.Length - 14)));
                }

                if (crashed)
                {
                    AppConfig.Trace("прошлый запуск оборвался на этапе: " + stage);
                    Report("вылет", "Игра закрылась без ошибки.\n\nПоследние записи:\n" + tail, stage);
                    AppConfig.StageClear();
                }

                // админу окно нужно только когда есть что показать
                if (!crashed && admin && !HasFreshReports()) return;

                using var f = new UI.CrashForm(crashed ? stage : "", tail, admin);
                f.ShowDialog();
            }
            catch { }
        }

        /// <summary>Есть ли непрочитанные отчёты — чтобы не дёргать админа зря.</summary>
        static bool HasFreshReports()
        {
            try
            {
                var r = Net.ApiClient.ReportsAsync().GetAwaiter().GetResult();
                return r.Ok && r.Data != null && r.Data.Fresh > 0;
            }
            catch { return false; }
        }
    }
}
