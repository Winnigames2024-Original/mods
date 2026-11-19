using GEO_DASH.Core;
using GEO_DASH.Net;
using GEO_DASH.UI;

namespace GEO_DASH
{
    internal static class Program
    {
        /// <summary>Файл уровня, с которым игру открыли двойным кликом.</summary>
        public static string OpenLevelPath;

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

            // без аккаунта в игру не пускаем: авто-вход, иначе вход или регистрация
            if (!AuthForm.Ensure()) return;

            Application.Run(new MainForm());
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
                MessageBox.Show("Не получилось открыть уровень:\n" + ex.Message,
                    "GEO DASH", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        static void Crash(Exception ex)
        {
            try
            {
                var log = Path.Combine(AppConfig.DataDir, "crash.log");
                File.AppendAllText(log, $"[{DateTime.Now:s}] {ex}\n\n");
                MessageBox.Show("Ошибка: " + ex?.Message + "\n\nПодробности: " + log,
                    "GEO DASH", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}
