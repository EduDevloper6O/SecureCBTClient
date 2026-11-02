using System.Text.Json;
using System.Windows;
using Elecctron.App.Services;

namespace Elecctron.App;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await Task.Delay(1400);
        var main = new SecureCBTClient.MainWindow();
        Application.Current.MainWindow = main;
        main.Show();
        Close();
    }
}
