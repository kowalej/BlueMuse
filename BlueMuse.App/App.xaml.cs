using BlueMuse.AppService;
using BlueMuse.Bluetooth;
using BlueMuse.Helpers;
using BlueMuse.Settings;
using Serilog;
using Serilog.Exceptions;
using System;
using System.IO;
using System.Linq;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace BlueMuse
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();

            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            var logPath = Path.Combine(localFolder, "Logs", "BlueMuse-Log-{Date}.log");
            Log.Logger = new LoggerConfiguration()
                .Enrich.WithExceptionDetails()
                .MinimumLevel.Information()
                .WriteTo.RollingFile(
                    logPath,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] {Message}{NewLine}{Exception}")
                .CreateLogger();

            Suspending += OnSuspending;
            UnhandledException += App_UnhandledException1; ;
            AppSettings.Instance.LoadInitialSettings();
        }

        private void App_UnhandledException1(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Log.Error(e.Exception, "BlueMuse unhandled exception.");
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Log.Information("BlueMuse started.");
            Launch(e.PreviousExecutionState, e.PrelaunchActivated);
        }

        private void Launch(ApplicationExecutionState previousExecutionState, bool prelaunchActivated, LaunchActivatedEventArgs e = null)
        {
            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (!(Window.Current.Content is Frame rootFrame))
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (previousExecutionState == ApplicationExecutionState.Terminated)
                {
                    // TODO: Load state from previously suspended application.
                }
                // Place the frame in the current Window.
                Window.Current.Content = rootFrame;
            }

            if (prelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter.
                    if (e == null) rootFrame.Navigate(typeof(MainPage));
                    else rootFrame.Navigate(typeof(MainPage), e);
                }

                // Ensure the current window is active.
                var desiredSize = new Size(468d, 660d);
                ApplicationView.PreferredLaunchViewSize = desiredSize;
                ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
                ApplicationView.GetForCurrentView().SetPreferredMinSize(desiredSize);
                Window.Current.Activate();
            }
        }

        protected async override void OnActivated(IActivatedEventArgs args)
        {
            // Check for protocol activation.
            if (args.Kind == ActivationKind.Protocol)
            {
                bool launching = args.PreviousExecutionState != ApplicationExecutionState.Running;

                var protocolArgs = (ProtocolActivatedEventArgs)args;
                string argStr = string.Empty;
                try
                {
                    argStr = protocolArgs.Uri.PathAndQuery;

                    var splitArgs = argStr.Replace("/?", "").Split('!'); // Note: not sure why the syystem ads the forward slash...

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

                        if (!launching)
                        {
                            bluetoothManager.ResolveAutoStreamAll();
                        }
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

                        else if(splitArgs.FirstOrDefault(x => x.Contains(Constants.ARGS_STOPALL, StringComparison.OrdinalIgnoreCase)) != null)
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
                        App.Current.Exit();
                    }

                }
                catch (UriFormatException) {}
                if (launching)
                    Launch(ApplicationExecutionState.NotRunning, false);
            }
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

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            // TODO: Save application state and stop any background activity.
            BluetoothManager.Instance.Close();
            deferral.Complete();
        }

        protected override async void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            await AppServiceManager.HandleIncomingConnectionAsync(args.TaskInstance);
        }
    }
}
