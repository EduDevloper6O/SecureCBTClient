#nullable disable
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;

namespace SecureCBTClient
{
    public partial class LobbyWindow : Window
    {
        private DispatcherTimer countdownTimer;
        private DateTime examStartTime;
        private string candidateName;
        private string rollNumber;
        private string examCode;
        private string candidatePhotoUrl;

        private bool isTimerFinished = false;
        private static readonly HttpClient translateClient = new HttpClient();

        private string enHeading, enInst0, enInst1, enInst2, enInst3, enInst4, enInst5, enAgree, enBtn, enProfile, enTimerHeading;
        private string hiHeading, hiInst0, hiInst1, hiInst2, hiInst3, hiInst4, hiInst5, hiAgree, hiBtn, hiProfile, hiTimerHeading;

        public LobbyWindow(string candName, string rollNo, string examCd, string startTimeStr, string photoUrl = "")
        {
            InitializeComponent();

            this.candidateName = candName;
            this.rollNumber = rollNo;
            this.examCode = examCd;
            this.candidatePhotoUrl = photoUrl;

            lblCandidateName.Text = $"Name: {candidateName}";
            lblRollNumber.Text = $"Roll No: {rollNumber}";
            lblExamCode.Text = $"Exam: {examCode}";

            CacheEnglishText();
            _ = LoadCandidatePhotoAsync();

            if (DateTime.TryParse(startTimeStr, out DateTime parsedTime))
            {
                examStartTime = parsedTime.ToLocalTime();
                StartCountdown();
            }
            else
            {
                examStartTime = DateTime.Now;
                StartCountdown();
            }
        }

        private void CacheEnglishText()
        {
            enHeading = txtHeading.Text;
            enInst0 = txtInst0.Text;
            enInst1 = txtInst1.Text;
            enInst2 = txtInst2.Text;
            enInst3 = txtInst3.Text;
            enInst4 = txtInst4.Text;
            enInst5 = txtInst5.Text;
            enAgree = txtAgree.Text;
            enBtn = btnStartExam.Content.ToString();
            enProfile = txtProfileHeading.Text;
            enTimerHeading = txtTimerHeading.Text;
        }

        private void chkAgree_Click(object sender, RoutedEventArgs e)
        {
            ValidateStartButton();
        }

        private async Task LoadCandidatePhotoAsync()
        {
            if (!string.IsNullOrEmpty(candidatePhotoUrl))
            {
                try
                {
                    using (HttpClient client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true }))
                    {
                        byte[] imageBytes = await client.GetByteArrayAsync(candidatePhotoUrl);
                        using (MemoryStream ms = new MemoryStream(imageBytes))
                        {
                            BitmapImage bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.StreamSource = ms;
                            bitmap.EndInit();
                            bitmap.Freeze();

                            Dispatcher.Invoke(() => imgCandidatePhoto.Source = bitmap);
                        }
                    }
                }
                catch { }
            }
        }

        private async void cmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbLanguage == null || txtInst0 == null) return;
            bool isHindi = cmbLanguage.SelectedIndex == 1;

            if (isHindi)
            {
                if (string.IsNullOrEmpty(hiInst0))
                {
                    txtHeading.Text = "अनुवाद हो रहा है...";
                    hiHeading = await TranslateTextToHindiAsync(enHeading);
                    hiInst0 = await TranslateTextToHindiAsync(enInst0);
                    hiInst1 = await TranslateTextToHindiAsync(enInst1);
                    hiInst2 = await TranslateTextToHindiAsync(enInst2);
                    hiInst3 = await TranslateTextToHindiAsync(enInst3);
                    hiInst4 = await TranslateTextToHindiAsync(enInst4);
                    hiInst5 = await TranslateTextToHindiAsync(enInst5);
                    hiAgree = await TranslateTextToHindiAsync(enAgree);
                    hiBtn = await TranslateTextToHindiAsync(enBtn);
                    hiProfile = await TranslateTextToHindiAsync(enProfile);
                    hiTimerHeading = await TranslateTextToHindiAsync(enTimerHeading);
                }

                txtHeading.Text = hiHeading;
                txtInst0.Text = hiInst0;
                txtInst1.Text = hiInst1;
                txtInst2.Text = hiInst2;
                txtInst3.Text = hiInst3;
                txtInst4.Text = hiInst4;
                txtInst5.Text = hiInst5;
                txtAgree.Text = hiAgree;
                btnStartExam.Content = hiBtn;
                txtProfileHeading.Text = hiProfile;
                txtTimerHeading.Text = hiTimerHeading;
            }
            else
            {
                txtHeading.Text = enHeading;
                txtInst0.Text = enInst0;
                txtInst1.Text = enInst1;
                txtInst2.Text = enInst2;
                txtInst3.Text = enInst3;
                txtInst4.Text = enInst4;
                txtInst5.Text = enInst5;
                txtAgree.Text = enAgree;
                btnStartExam.Content = enBtn;
                txtProfileHeading.Text = enProfile;
                txtTimerHeading.Text = enTimerHeading;
            }
        }

        private async Task<string> TranslateTextToHindiAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            try
            {
                string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=hi&dt=t&q={Uri.EscapeDataString(text)}";
                string result = await translateClient.GetStringAsync(url);
                JArray json = JArray.Parse(result);
                string translatedText = "";
                foreach (JArray item in json[0])
                {
                    translatedText += item[0].ToString();
                }
                return translatedText;
            }
            catch
            {
                return text;
            }
        }

        private void StartCountdown()
        {
            countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            countdownTimer.Tick += CountdownTimer_Tick;
            countdownTimer.Start();
            CountdownTimer_Tick(null, null);
        }

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            TimeSpan timeRemaining = examStartTime - DateTime.Now;

            if (timeRemaining <= TimeSpan.Zero)
            {
                countdownTimer.Stop();
                lblTimer.Text = "00:00:00";
                lblTimer.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                isTimerFinished = true;
                ValidateStartButton();
            }
            else
            {
                lblTimer.Text = string.Format("{0:D2}:{1:D2}:{2:D2}",
                    (int)timeRemaining.TotalHours,
                    timeRemaining.Minutes,
                    timeRemaining.Seconds);
            }
        }

        private void ValidateStartButton()
        {
            if (isTimerFinished && chkAgree.IsChecked == true)
            {
                btnStartExam.IsEnabled = true;
                btnStartExam.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }
            else
            {
                btnStartExam.IsEnabled = false;
                btnStartExam.Background = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            }
        }

        private void btnStartExam_Click(object sender, RoutedEventArgs e)
        {
            countdownTimer?.Stop();
            ExamDashboardWindow examWindow = new ExamDashboardWindow(candidateName, rollNumber, examCode, candidatePhotoUrl);
            examWindow.Show();
            this.Close();
        }
    }
}