using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Storage;
using Windows.System;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace BlueMuse
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public ViewModels.MainPageVM ViewModel { get; set; }

        public MainPage()
        {
            InitializeComponent();
            ViewModel = new ViewModels.MainPageVM();
        }

        private void SettingsDone_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            SettingsSplitView.IsPaneOpen = false;
        }

        private void BlueMuseSettings_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            CommandBar.IsOpen = false;
            SettingsSplitView.IsPaneOpen = !SettingsSplitView.IsPaneOpen;
        }

        private async void OpenLogFolder_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(ViewModel.BlueMuseLogFolder);
            await Launcher.LaunchFolderAsync(folder);
        }
    }
}
