using BlueMuse.AppService;
using BlueMuse.Bluetooth;
using System;
using System.Collections.Generic;
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
    sealed partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Launch(e.PreviousExecutionState, e.PrelaunchActivated);
        }

        private void Launch(ApplicationExecutionState previousExecutionState, bool prelaunchActivated, LaunchActivatedEventArgs e = null)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (previousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }
                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (prelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    if (e == null) rootFrame.Navigate(typeof(MainPage));
                    else rootFrame.Navigate(typeof(MainPage), e);
                }

                float DPI = Windows.Graphics.Display.DisplayInformation.GetForCurrentView().LogicalDpi;
                var desiredSize = new Size(410d, 700d);
                ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
                ApplicationView.PreferredLaunchViewSize = desiredSize;
                ApplicationView.GetForCurrentView().SetPreferredMinSize(desiredSize);

                // Ensure the current window is active
                Window.Current.Activate();
            }
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            // Check for protocol activation
            if (args.Kind == ActivationKind.Protocol)
            {
                bool launching = args.PreviousExecutionState != ApplicationExecutionState.Running;

                var protocolArgs = (ProtocolActivatedEventArgs)args;
                string argStr = string.Empty;
                try
                {
                    argStr = protocolArgs.Uri.PathAndQuery;

                    var splitArgs = argStr.Replace("/?", "").Split('&'); // Note: not sure why the syystem ads the forward slash...

                    if (protocolArgs.Uri.Host.Equals("start", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var addressesStr = splitArgs.FirstOrDefault(x => x.Contains("addresses"));
                        string[] addresses = null;

                        BluetoothManager bluetoothManager = BluetoothManager.Instance;

                        if (addressesStr != null)
                        {
                            addresses = addressesStr.Trim().Replace("addresses=", "").Split(',');
                            foreach (var address in addresses)
                            {
                                bluetoothManager.MusesToAutoStream.Add(address);
                            }
                        }

                        var streamFirstStr = splitArgs.FirstOrDefault(x => x.Contains("streamfirst"));
                        if (streamFirstStr != null)
                        {
                            bluetoothManager.StreamFirst = streamFirstStr.Trim().Replace("streamfirst=", "")
                                .Equals("true", StringComparison.CurrentCultureIgnoreCase) ? true : false;
                        }

                        if (!launching)
                        {
                            bluetoothManager.ResolveAutoStreamAll();
                        }
                    }

                    else if (protocolArgs.Uri.Host.Equals("stop", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var addressesStr = splitArgs.FirstOrDefault(x => x.Contains("addresses"));
                        string[] addresses = null;

                        BluetoothManager bluetoothManager = BluetoothManager.Instance;

                        if (addressesStr != null)
                        {
                            addresses = addressesStr.Trim().Replace("addresses=", "").Split(',');
                            foreach (var address in addresses)
                            {
                                bluetoothManager.MusesToAutoStream.Remove(address);
                                var muse = bluetoothManager.Muses.FirstOrDefault(x => x.MacAddress == address);
                                if (muse != null)
                                    bluetoothManager.StopStreaming(address);
                            }
                        }
                    }
                }
                catch (UriFormatException)
                {

                }
                if (launching)
                    Launch(ApplicationExecutionState.NotRunning, false);
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
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
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }

        protected override async void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            await AppServiceManager.HandleIncomingConnectionAsync(args.TaskInstance);
        }
    }
}
