using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace LSLBridge
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ViewModels.MainWindowVM ViewModel { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new ViewModels.MainWindowVM();
            DataContext = ViewModel;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (ViewModel.MuseStreamCount > 0)
                e.Cancel = true; // Don't allow user to exit while streaming.
            base.OnClosing(e);
        }

    }
}
