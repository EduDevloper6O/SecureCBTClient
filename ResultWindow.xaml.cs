#nullable disable
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace SecureCBTClient
{
    public partial class ResultWindow : Window
    {
        private string rollNumber, examCode;
        private ExamBranding _branding;

        public ResultWindow(string roll, string exam, ExamBranding branding)
        {
            InitializeComponent();
            this.rollNumber = roll;
            this.examCode = exam;
            this._branding = branding;

            ApplyBranding();

            // Show loading overlay while fetching data
            OverlayLoading.Visibility = Visibility.Visible;
            _ = LoadResultsAsync();
        }

        private void ApplyBranding()
        {
            try
            {
                // Dynamically sets the Exit button color to match the Exam's Theme Color
                btnExit.Background = (SolidColorBrush)new BrushConverter().ConvertFromString(_branding.ThemeColor);
            }
            catch { }
        }

        private async Task LoadResultsAsync()
        {
            try
            {
                JObject res = await ApiService.GetResultsAsync(rollNumber, examCode);

                if (res["status"]?.ToString() == "success")
                {
                    lblAttempted.Text = res["scoreData"]["attempted"]?.ToString() ?? "0";
                    lblCorrect.Text = res["scoreData"]["correctCount"]?.ToString() ?? "0";
                    lblWrong.Text = res["scoreData"]["wrongCount"]?.ToString() ?? "0";
                    lblScore.Text = res["scoreData"]["totalScore"]?.ToString() ?? "0";
                }
            }
            catch
            {
                // On failure, values remain "--"
            }
            finally
            {
                // Hide loading overlay to reveal the beautiful data cards
                OverlayLoading.Visibility = Visibility.Collapsed;
            }
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}