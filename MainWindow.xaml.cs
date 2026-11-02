#nullable disable
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using AutoUpdaterDotNET;

namespace SecureCBTClient
{
    public partial class MainWindow : Window
    {
        private const string UpdateManifestUrl =
            "https://github.com/EduDevloper6O/SecureCBTClient/releases/latest/download/update.xml";

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Configure the updater before starting its asynchronous check.
            AutoUpdater.ShowSkipButton = false;
            AutoUpdater.ShowRemindLaterButton = false;
            AutoUpdater.Mandatory = true;
            AutoUpdater.RunUpdateAsAdmin = true;
            AutoUpdater.ReportErrors = false;
            AutoUpdater.Start(UpdateManifestUrl);

            // 2. Smoothly fill the progress bar over 3.5 seconds
            DoubleAnimation progressAnim = new DoubleAnimation
            {
                From = 0,
                To = 100,
                Duration = TimeSpan.FromSeconds(3.5),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            pbLoading.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, progressAnim);

            // 3. Pre-warm Google Apps Script server
            txtStatus.Text = "Connecting to the examination service...";
            _ = ApiService.WakeUpServerAsync();

            await Task.Delay(1400);
            txtStatus.Text = "Loading examination client settings...";

            await Task.Delay(1400);
            txtStatus.Text = "Ready. Opening candidate access...";
            await Task.Delay(700);

            // 4. Open the Login Window and Close the Splash Screen
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
    }
}
