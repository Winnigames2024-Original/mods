using System;
using System.Windows.Forms;

namespace NareServerManager
{
    internal static class SMProgram
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string folder = args != null && args.Length > 0 ? args[0] : null;
            if (string.IsNullOrEmpty(folder))
            {
                using (var fb = new FolderBrowserDialog { Description = "Выбери папку сервера" })
                {
                    if (fb.ShowDialog() != DialogResult.OK) return;
                    folder = fb.SelectedPath;
                }
            }
            Application.Run(new SMForm(folder));
        }
    }
}
