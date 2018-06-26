using Serilog;
using System;
using System.IO;
using System.Windows;

namespace LSLBridge
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            Startup += App_Startup;
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
            var localFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var logPath = Path.Combine(localFolder, "BlueMuse", "Logs", "BlueMuse-{Date}.log");
            Log.Logger = new LoggerConfiguration()
                .WriteTo.RollingFile(logPath,
                                     outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] {Message}{NewLine}{Exception}")
                .CreateLogger();
            Log.Information("LSL Bridge started.");
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error(e.Exception, "LSL Bridge unhandled exception.");
        }
    }
}
