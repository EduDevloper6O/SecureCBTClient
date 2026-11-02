#nullable disable
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Newtonsoft.Json.Linq;

namespace SecureCBTClient
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        // 1. Allows the candidate to click and drag the borderless window
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        // 2. Custom Close Button logic
        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string roll = txtRollNumber.Text.Trim();
            string pass = txtPassword.Password.Trim();
            string code = txtExamCode.Text.Trim();

            if (string.IsNullOrEmpty(roll) || string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(code))
            {
                lblStatus.Text = "Please fill in all security fields.";
                return;
            }

            lblStatus.Text = "";

            // SHOW THE CINEMATIC LOADING SPINNER
            if (this.FindName("OverlayLoading") is UIElement overlay)
            {
                overlay.Visibility = Visibility.Visible;
            }

            Storyboard spin = this.Resources["SpinAnimation"] as Storyboard;
            spin?.Begin();

            try
            {
                JObject res = await ApiService.CandidateLoginAsync(roll, pass, code);

                if (res["status"]?.ToString() == "success")
                {
                    string candName = res["candidate"]["name"]?.ToString();
                    string photoUrl = res["candidate"]["photoUrl"]?.ToString();
                    string startTimeStr = res["exam"]["startTime"]?.ToString();

                    // PULL ALL DYNAMIC BRANDING & ADVANCED TYPOGRAPHY FROM SERVER
                    ExamBranding branding = new ExamBranding
                    {
                        AuthorityName = res["exam"]["authorityName"]?.ToString() ?? "EXAMINATION BOARD",
                        ExamTitle = res["exam"]["examTitle"]?.ToString() ?? "CBT EXAM",
                        LogoUrl = res["exam"]["logoUrl"]?.ToString() ?? "",
                        ThemeColor = res["exam"]["themeColor"]?.ToString() ?? "#1E293B",
                        TextAlignment = res["exam"]["textAlignment"]?.ToString() ?? "Left",
                        IsBold = (res["exam"]["isBold"]?.ToString() ?? "true").ToLower() == "true",
                        TextSize = int.TryParse(res["exam"]["textSize"]?.ToString(), out int size) ? size : 20,

                        // New Advanced Rich-Text Formatting
                        FontFamily = res["exam"]["fontFamily"]?.ToString() ?? "Segoe UI",
                        TextColor = res["exam"]["textColor"]?.ToString() ?? "#FFFFFF",
                        BgColor = res["exam"]["bgColor"]?.ToString() ?? "transparent",
                        IsItalic = (res["exam"]["isItalic"]?.ToString() ?? "false").ToLower() == "true",
                        IsUnderline = (res["exam"]["isUnderline"]?.ToString() ?? "false").ToLower() == "true",
                        LetterSpacing = int.TryParse(res["exam"]["letterSpacing"]?.ToString(), out int spacing) ? spacing : 0
                    };

                    LobbyWindow lobby = new LobbyWindow(candName, roll, code, startTimeStr, photoUrl, branding);
                    lobby.Show();

                    spin?.Stop();
                    this.Close();
                }
                else
                {
                    // IF LOGIN FAILS
                    spin?.Stop();
                    if (this.FindName("OverlayLoading") is UIElement overlayOff) overlayOff.Visibility = Visibility.Collapsed;
                    lblStatus.Text = res["message"]?.ToString() ?? "Authentication failed. Check your credentials.";
                }
            }
            catch (Exception)
            {
                spin?.Stop();
                if (this.FindName("OverlayLoading") is UIElement overlayOff) overlayOff.Visibility = Visibility.Collapsed;
                lblStatus.Text = "Network Error: Could not connect to the secure database.";
            }
        }
    }
}