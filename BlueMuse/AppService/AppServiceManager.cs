using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;

namespace BlueMuse.AppService
{
    /// <summary>
    /// Modified service messaging class courtesy of Mike Taulty - https://mtaulty.com/2016/10/12/windows-10-1607-uwp-apps-packaged-with-companion-desktop-apps/
    /// </summary>
    static class AppServiceManager
    {
        static AppServiceConnection connection;
        static BackgroundTaskDeferral deferral;
        static ConcurrentQueue<Tuple<string, ValueSet>> messages;

        static AppServiceManager()
        {
            messages = new ConcurrentQueue<Tuple<string, ValueSet>>();
        }

        public static async Task HandleIncomingConnectionAsync(IBackgroundTaskInstance taskInstance)
        {
            var details = taskInstance.TriggerDetails as AppServiceTriggerDetails;

            if ((details != null) &&
              (details.CallerPackageFamilyName == Package.Current.Id.FamilyName) &&
              (connection == null))
            {
                deferral = taskInstance.GetDeferral();
                connection = details.AppServiceConnection;
                details.AppServiceConnection.ServiceClosed += OnServiceClosed;
                await DrainQueueAsync();
            }
        }

        public static async Task SendMessageAsync(string messageType, ValueSet message)
        {
            messages.Enqueue(new Tuple<string, ValueSet>(messageType, message));
            await DrainQueueAsync();
        }

        static async Task DrainQueueAsync()
        {
            while ((connection != null) && !messages.IsEmpty)
            {
                Tuple<string, ValueSet> message;
                if (messages.TryDequeue(out message))
                {
                    await SendMessageInternalAsync(message.Item1, message.Item2);
                }
            }
        }

        static async Task SendMessageInternalAsync(string messageType, ValueSet message)
        {
            message.Add(LSLBridge.Constants.LSL_MESSAGE_TYPE, messageType);
            await connection.SendMessageAsync(message);
        }

        static void OnServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            connection.ServiceClosed -= OnServiceClosed;
            deferral.Complete();
            deferral = null;
            connection = null;
        }
    }
}
