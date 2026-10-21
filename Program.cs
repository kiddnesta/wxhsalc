using System;
using System.Threading;
using System.Windows.Forms;
using ClashXW.Services;

namespace ClashXW
{
    internal static class Program
    {
        private const string MutexName = "ClashXW_SingleInstance_Mutex";
        private static Mutex? _mutex;

        [STAThread]
        static void Main(string[] args)
        {
            // This diagnostic can run alongside the main instance without touching Mihomo.
            if (args.Length == 1 && args[0] == "--test-notification")
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new StatusToastForm("Notification test",
                    "ClashXW can display operation results.", ToolTipIcon.Info));
                return;
            }

            _mutex = new Mutex(true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                // Another instance is already running
                return;
            }

            try
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Initialize dark mode support before creating any windows
                DarkModeHelper.Initialize();

                Application.Run(new TrayApplicationContext());
            }
            finally
            {
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
            }
        }
    }
}
