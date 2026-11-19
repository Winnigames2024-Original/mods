using GEO_DASH.UI;

namespace GEO_DASH
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AppConfig.Load();

            Application.ThreadException += (s, e) => Crash(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Crash(e.ExceptionObject as Exception);

            Application.Run(new MainForm());
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
