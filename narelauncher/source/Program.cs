using System;
using System.Threading;
using System.Windows.Forms;
using NareLauncher.Core;
using NareLauncher.UI;

namespace NareLauncher
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += (s, e) =>
            {
                Log.Error("Необработанная ошибка интерфейса", e.Exception);
                MessageBox.Show("Произошла ошибка:\n\n" + e.Exception.Message +
                                "\n\nПодробности записаны в launcher.log",
                                "NareLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Log.Error("Критическая ошибка", e.ExceptionObject as Exception);
            };

            try
            {
                Paths.MigrateOldData();
                Settings.Load();
                Paths.MigrateGameFolder();
                Paths.EnsureAll();
                AccountStore.Load();

                Log.Info("=== NareLauncher " + GameLauncher.LauncherVersion + " запущен ===");
                Log.Info("Папка игры: " + Paths.GameRoot);

                // чтобы кнопка «Играть» на сайте открывала именно этот лаунчер
                PlayLink.Register();

                PlayLink.Pending = PlayLink.FromArgs(args);
                if (PlayLink.Pending != null)
                    Log.Info("Запуск по ссылке с сайта: " + PlayLink.Pending);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось подготовить рабочие папки:\n\n" + ex.Message,
                                "NareLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Application.AddMessageFilter(new NareLauncher.UI.WheelFilter());
            Application.Run(new MainForm());
        }
    }
}
