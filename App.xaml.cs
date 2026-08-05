using System;
using System.IO;
using System.Windows;

namespace HiResAudioPlayerTagMaster
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                LogException("AppDomain.UnhandledException", args.ExceptionObject as Exception);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                LogException("DispatcherUnhandledException", args.Exception);
                args.Handled = true;
            };

            base.OnStartup(e);
        }

        private static void LogException(string source, Exception? ex)
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
            string logMsg = $"[{DateTime.Now}] [{source}] Exception:\n{ex}\n\n";
            File.AppendAllText(logPath, logMsg);
        }
    }
}
