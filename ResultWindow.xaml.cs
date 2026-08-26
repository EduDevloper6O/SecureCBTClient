#nullable disable
using System;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace SecureCBTClient
{
    public partial class ResultWindow : Window
    {
        private string rollNumber;
        private string examCode;

        public ResultWindow(string roll, string exam)
        {
            InitializeComponent();
            this.rollNumber = roll;
            this.examCode = exam;

            _ = LoadResultsAsync();
        }

        private async Task LoadResultsAsync()
        {
            try
            {
                JObject res = await ApiService.GetResultsAsync(rollNumber, examCode);
                if (res["status"]?.ToString() == "success")
                {
                    lblAttempted.Text = res["scoreData"]["attempted"]?.ToString();
                    lblCorrect.Text = res["scoreData"]["correctCount"]?.ToString();
                    lblWrong.Text = res["scoreData"]["wrongCount"]?.ToString();
                    lblScore.Text = res["scoreData"]["totalScore"]?.ToString();
                }
            }
            catch { }
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}