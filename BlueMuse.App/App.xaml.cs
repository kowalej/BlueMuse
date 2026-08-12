using BlueMuse.Bluetooth;
using BlueMuse.Helpers;
using BlueMuse.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.AppLifecycle;
using Serilog;
using Serilog.Exceptions;
using System;
using System.IO;
using System.Linq;

namespace BlueMuse
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window window;

        /// <summary>
        /// Initializes the singleton application object.
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();

            BlueMuse.Helpers.UIDispatcher.Initialize();

            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            var logPath = Path.Combine(localFolder, "Logs", "BlueMuse-Log-.log");
            Log.Logger = new LoggerConfiguration()
                .Enrich.WithExceptionDetails()
                .MinimumLevel.Information()
                .WriteTo.File(
                    logPath,
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] {Message}{NewLine}{Exception}")
                .CreateLogger();

            UnhandledException += App_UnhandledException1;
            AppSettings.Instance.LoadInitialSettings();
        }

        private void App_UnhandledException1(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Log.Error(e.Exception, "BlueMuse unhandled exception.");
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file (e.g. protocol activation).
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            Log.Information("BlueMuse started.");

            Launch();

            var activatedEventArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (activatedEventArgs != null && activatedEventArgs.Kind == ExtendedActivationKind.Protocol)
            {
                HandleProtocolActivation(activatedEventArgs);
            }
        }

        private void Launch()
        {
            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active.
            if (window == null)
            {
                window = new Window();
            }

            if (!(window.Content is Frame rootFrame))
            {
                // Create a Frame to act as the navigation context and navigate to the first page.
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;

                // Place the frame in the current Window.
                window.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                // When the navigation stack isn't restored navigate to the first page.
                rootFrame.Navigate(typeof(MainPage));
            }

            RestoreWindowSize(window, 500, 820);
            window.Closed += Window_Closed;

            window.Activate();
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            SaveWindowSize(window);
        }

        private static Microsoft.UI.Windowing.AppWindow GetAppWindow(Window window)
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        }

        private static void RestoreWindowSize(Window window, int defaultWidth, int defaultHeight)
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            var savedWidth = localSettings.Values[Constants.SETTINGS_KEY_WINDOW_WIDTH] as int?;
            var savedHeight = localSettings.Values[Constants.SETTINGS_KEY_WINDOW_HEIGHT] as int?;

            int width = savedWidth ?? defaultWidth;
            int height = savedHeight ?? defaultHeight;

            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            double scale = 1.0;
            try
            {
                uint dpi = GetDpiForWindow(hWnd);
                scale = dpi / 96.0;
            }
            catch
            {
                // Fall back to no scaling if unable to query DPI.
            }

            var appWindow = GetAppWindow(window);
            appWindow.Resize(new Windows.Graphics.SizeInt32(
                (int)(width * scale),
                (int)(height * scale)));
        }

        private static void SaveWindowSize(Window window)
        {
            var appWindow = GetAppWindow(window);
            if (appWindow == null)
                return;

            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            localSettings.Values[Constants.SETTINGS_KEY_WINDOW_WIDTH] = appWindow.Size.Width;
            localSettings.Values[Constants.SETTINGS_KEY_WINDOW_HEIGHT] = appWindow.Size.Height;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        private async void HandleProtocolActivation(Microsoft.Windows.AppLifecycle.AppActivationArguments activatedEventArgs)
        {
            if (activatedEventArgs.Kind != ExtendedActivationKind.Protocol) return;
            if (!(activatedEventArgs.Data is Windows.ApplicationModel.Activation.IProtocolActivatedEventArgs protocolArgs)) return;

            string argStr = string.Empty;
            try
            {
                argStr = protocolArgs.Uri.PathAndQuery;

                var splitArgs = argStr.Replace("/?", "").Split('!'); // Note: not sure why the system adds the forward slash...

                if (protocolArgs.Uri.Host.Equals(Constants.CMD_START, StringComparison.CurrentCultureIgnoreCase))
                {
                    BluetoothManager bluetoothManager = BluetoothManager.Instance;

                    var addressesStr = splitArgs.FirstOrDefault(x => x.Contains(Constants.ARGS_ADDRESSES, StringComparison.OrdinalIgnoreCase));
                    string[] addresses = null;
                    var streamFirstStr = splitArgs.FirstOrDefault(x => x.Contains(Constants.ARGS_STREAMFIRST, StringComparison.OrdinalIgnoreCase));

                    if (addressesStr != null)
                    {
                        addresses = addressesStr.Trim().Replace(Constants.ARGS_ADDRESSES + "=", "").Split(',');
                        foreach (var address in addresses)
                        {
                            bluetoothManager.MusesToAutoStream.Add(address);
                        }
                    }

                    else if (splitArgs.FirstOrDefault(x => x.Contains(Constants.ARGS_STARTALL, StringComparison.OrdinalIgnoreCase)) != null)
                    {
                        await bluetoothManager.StartStreamingAll();
                    }

                    else if (streamFirstStr != null)
                    {
                        bluetoothManager.StreamFirst = streamFirstStr.Trim().Replace(Constants.ARGS_STREAMFIRST + "=", "")
                            .Equals("true", StringComparison.CurrentCultureIgnoreCase) ? true : false;
                    }

                    bluetoothManager.ResolveAutoStreamAll();
                }

                else if (protocolArgs.Uri.Host.Equals(Constants.CMD_STOP, StringComparison.CurrentCultureIgnoreCase))
                {
                    var addressesStr = splitArgs.FirstOrDefault(x => x.Contains(Constants.ARGS_ADDRESSES, StringComparison.OrdinalIgnoreCase));
                    string[] addresses = null;

                    BluetoothManager bluetoothManager = BluetoothManager.Instance;

                    if (addressesStr != null)
                    {
                        addresses = addressesStr.Trim().Replace(Constants.ARGS_ADDRESSES + "=", "").Split(',');
                        foreach (var address in addresses)
                        {
                            bluetoothManager.MusesToAutoStream.Remove(address);
                            bluetoothManager.StopStreamingAddress(address);
                        }
                    }

                    else if (splitArgs.FirstOrDefault(x => x.Contains(Constants.ARGS_STOPALL, StringComparison.OrdinalIgnoreCase)) != null)
                    {
                        bluetoothManager.MusesToAutoStream.Clear();
                        await bluetoothManager.StopStreamingAll();
                    }
                }

                else if (protocolArgs.Uri.Host.Equals(Constants.CMD_FORCE_REFRESH, StringComparison.CurrentCultureIgnoreCase))
                {
                    BluetoothManager.Instance.ForceRefresh();
                }

                else if (protocolArgs.Uri.Host.Equals(Constants.CMD_SET_SETTING, StringComparison.CurrentCultureIgnoreCase))
                {
                    var keyStr = splitArgs.FirstOrDefault(x => x.Contains(Constants.ARGS_SETTING_KEY, StringComparison.OrdinalIgnoreCase));
                    var valueStr = splitArgs.FirstOrDefault(x => x.Contains(Constants.ARGS_SETTING_VALUE, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(keyStr) && !string.IsNullOrEmpty(valueStr))
                    {
                        AppSettings.Instance.SetCMDSetting(
                            keyStr.Trim().Replace(Constants.ARGS_SETTING_KEY + "=", ""),
                            valueStr.Trim().Replace(Constants.ARGS_SETTING_VALUE + "=", "")
                        );
                    }
                }

                else if (protocolArgs.Uri.Host.Equals(Constants.CMD_CLOSE_PROGRAM, StringComparison.CurrentCultureIgnoreCase))
                {
                    BluetoothManager.Instance.Close();
                    Application.Current.Exit();
                }
            }
            catch (UriFormatException) { }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails.
        /// </summary>
        /// <param name="sender">The Frame which failed navigation.</param>
        /// <param name="e">Details about the navigation failure.</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }
    }
}
