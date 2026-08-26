#nullable disable
using System;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace SecureCBTClient
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string roll = txtRollNumber.Text.Trim();
            string pass = txtPassword.Password.Trim();
            string code = txtExamCode.Text.Trim();

            if (string.IsNullOrEmpty(roll) || string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(code))
            {
                lblStatus.Text = "Please fill in all fields.";
                return;
            }

            btnLogin.IsEnabled = false;
            btnLogin.Content = "Verifying...";
            lblStatus.Text = "";

            try
            {
                JObject res = await ApiService.CandidateLoginAsync(roll, pass, code);
                if (res["status"]?.ToString() == "success")
                {
                    string candName = res["candidate"]["name"]?.ToString();
                    string photoUrl = res["candidate"]["photoUrl"]?.ToString();
                    string startTimeStr = res["exam"]["startTime"]?.ToString();

                    LobbyWindow lobby = new LobbyWindow(candName, roll, code, startTimeStr, photoUrl);
                    lobby.Show();
                    this.Close();
                }
                else
                {
                    lblStatus.Text = res["message"]?.ToString() ?? "Login failed. Check credentials.";
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Network Error: " + ex.Message;
            }
            finally
            {
                btnLogin.IsEnabled = true;
                btnLogin.Content = "LOGIN TO EXAM";
            }
        }
    }
}