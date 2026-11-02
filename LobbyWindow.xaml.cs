#nullable disable
using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
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
        private string candidateName, rollNumber, examCode, candidatePhotoUrl;
        private ExamBranding _branding;
        private bool isTimerFinished = false;

        private static readonly HttpClient translateClient = new HttpClient();
        private static readonly HttpClient mediaClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true });

        private string enHeading, enInst0, enInst1, enInst2, enInst3, enInst4, enInst5, enAgree, enBtn, enProfile, enTimerHeading;
        private string hiHeading, hiInst0, hiInst1, hiInst2, hiInst3, hiInst4, hiInst5, hiAgree, hiBtn, hiProfile, hiTimerHeading;

        public LobbyWindow(string candName, string rollNo, string examCd, string startTimeStr, string photoUrl, ExamBranding branding)
        {
            InitializeComponent();

            this.candidateName = candName; this.rollNumber = rollNo; this.examCode = examCd;
            this.candidatePhotoUrl = photoUrl; this._branding = branding;

            lblCandidateName.Text = $"Name: {candidateName}";
            lblRollNumber.Text = $"Roll No: {rollNumber}";

            ApplyBranding();
            CacheEnglishText();
            _ = LoadImageAsync(candidatePhotoUrl, imgCandidatePhoto);

            if (DateTime.TryParse(startTimeStr, out DateTime parsedTime)) { examStartTime = parsedTime.ToLocalTime(); }
            else { examStartTime = DateTime.Now; }
            StartCountdown();
        }

        private void ApplyBranding()
        {
            try
            {
                var colorBrush = (SolidColorBrush)new BrushConverter().ConvertFromString(_branding.ThemeColor);
                HeaderBorder.Background = colorBrush; TimerBorder.Background = colorBrush;
            }
            catch { }

            lblAuthorityName.Text = _branding.AuthorityName.ToUpper();
            lblExamTitle.Text = _branding.ExamTitle.ToUpper();

            lblExamTitle.FontSize = _branding.TextSize;
            lblAuthorityName.FontSize = Math.Max(10, _branding.TextSize - 6);
            lblExamTitle.FontWeight = _branding.IsBold ? FontWeights.Bold : FontWeights.Normal;
            lblAuthorityName.FontWeight = _branding.IsBold ? FontWeights.SemiBold : FontWeights.Normal;

            if (_branding.TextAlignment.Equals("Center", StringComparison.OrdinalIgnoreCase))
            {
                HeaderTextPanel.HorizontalAlignment = HorizontalAlignment.Center;
                lblAuthorityName.TextAlignment = TextAlignment.Center;
                lblExamTitle.TextAlignment = TextAlignment.Center;
            }
            else if (_branding.TextAlignment.Equals("Right", StringComparison.OrdinalIgnoreCase))
            {
                HeaderTextPanel.HorizontalAlignment = HorizontalAlignment.Right;
                lblAuthorityName.TextAlignment = TextAlignment.Right;
                lblExamTitle.TextAlignment = TextAlignment.Right;
            }
            else
            {
                HeaderTextPanel.HorizontalAlignment = HorizontalAlignment.Left;
                lblAuthorityName.TextAlignment = TextAlignment.Left;
                lblExamTitle.TextAlignment = TextAlignment.Left;
            }

            if (!string.IsNullOrEmpty(_branding.LogoUrl))
            {
                imgAuthorityLogo.Visibility = Visibility.Visible;
                _ = LoadImageAsync(_branding.LogoUrl, imgAuthorityLogo);
            }
        }

        private static string NormalizeImageUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;
            string fileId = null;
            if (url.Contains("drive.google.com") || url.Contains("googleusercontent.com"))
            {
                var match = Regex.Match(url, @"(?:id=|\/d\/|d\/)([a-zA-Z0-9_-]{20,})");
                if (match.Success) fileId = match.Groups[1].Value;
            }
            if (!string.IsNullOrEmpty(fileId))
            {
                return $"https://lh3.googleusercontent.com/d/{fileId}";
            }
            return url;
        }

        private async Task LoadImageAsync(string rawUrl, Image targetImage)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return;
            string url = NormalizeImageUrl(rawUrl);

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0 Safari/537.36");
                    using (var response = await mediaClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                    {
                        byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();

                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            using (MemoryStream ms = new MemoryStream(imageBytes))
                            {
                                BitmapImage bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.StreamSource = ms;
                                bitmap.EndInit();
                                bitmap.Freeze();

                                targetImage.Source = bitmap;
                                targetImage.Visibility = Visibility.Visible;
                            }
                        });
                        return;
                    }
                }
            }
            catch
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(url, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        targetImage.Source = bitmap;
                        targetImage.Visibility = Visibility.Visible;
                    }
                    catch { }
                });
            }
        }

        private void CacheEnglishText()
        {
            enHeading = txtHeading.Text; enInst0 = txtInst0.Text; enInst1 = txtInst1.Text; enInst2 = txtInst2.Text; enInst3 = txtInst3.Text; enInst4 = txtInst4.Text; enInst5 = txtInst5.Text; enAgree = txtAgree.Text; enBtn = btnStartExam.Content.ToString(); enProfile = txtProfileHeading.Text; enTimerHeading = txtTimerHeading.Text;
        }

        private void chkAgree_Click(object sender, RoutedEventArgs e) => ValidateStartButton();

        private async void cmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbLanguage == null || txtInst0 == null) return;
            if (cmbLanguage.SelectedIndex == 1)
            {
                if (string.IsNullOrEmpty(hiInst0))
                {
                    txtHeading.Text = "अनुवाद हो रहा है...";
                    hiHeading = await TranslateTextAsync(enHeading); hiInst0 = await TranslateTextAsync(enInst0); hiInst1 = await TranslateTextAsync(enInst1); hiInst2 = await TranslateTextAsync(enInst2); hiInst3 = await TranslateTextAsync(enInst3); hiInst4 = await TranslateTextAsync(enInst4); hiInst5 = await TranslateTextAsync(enInst5); hiAgree = await TranslateTextAsync(enAgree); hiBtn = await TranslateTextAsync(enBtn); hiProfile = await TranslateTextAsync(enProfile); hiTimerHeading = await TranslateTextAsync(enTimerHeading);
                }
                txtHeading.Text = hiHeading; txtInst0.Text = hiInst0; txtInst1.Text = hiInst1; txtInst2.Text = hiInst2; txtInst3.Text = hiInst3; txtInst4.Text = hiInst4; txtInst5.Text = hiInst5; txtAgree.Text = hiAgree; btnStartExam.Content = hiBtn; txtProfileHeading.Text = hiProfile; txtTimerHeading.Text = hiTimerHeading;
            }
            else
            {
                txtHeading.Text = enHeading; txtInst0.Text = enInst0; txtInst1.Text = enInst1; txtInst2.Text = enInst2; txtInst3.Text = enInst3; txtInst4.Text = enInst4; txtInst5.Text = enInst5; txtAgree.Text = enAgree; btnStartExam.Content = enBtn; txtProfileHeading.Text = enProfile; txtTimerHeading.Text = enTimerHeading;
            }
        }

        private async Task<string> TranslateTextAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            try { string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=hi&dt=t&q={Uri.EscapeDataString(text)}"; string result = await translateClient.GetStringAsync(url); JArray json = JArray.Parse(result); string translatedText = ""; foreach (JArray item in json[0]) { translatedText += item[0].ToString(); } return translatedText; } catch { return text; }
        }

        private void StartCountdown()
        {
            countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            countdownTimer.Tick += CountdownTimer_Tick; countdownTimer.Start(); CountdownTimer_Tick(null, null);
        }

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            TimeSpan timeRemaining = examStartTime - DateTime.Now;
            if (timeRemaining <= TimeSpan.Zero) { countdownTimer.Stop(); lblTimer.Text = "00:00:00"; lblTimer.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); isTimerFinished = true; ValidateStartButton(); }
            else { lblTimer.Text = string.Format("{0:D2}:{1:D2}:{2:D2}", (int)timeRemaining.TotalHours, timeRemaining.Minutes, timeRemaining.Seconds); }
        }

        private void ValidateStartButton()
        {
            if (isTimerFinished && chkAgree.IsChecked == true) { btnStartExam.IsEnabled = true; btnStartExam.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)); }
            else { btnStartExam.IsEnabled = false; btnStartExam.Background = new SolidColorBrush(Color.FromRgb(148, 163, 184)); }
        }

        private void btnStartExam_Click(object sender, RoutedEventArgs e)
        {
            countdownTimer?.Stop();
            ExamDashboardWindow examWindow = new ExamDashboardWindow(candidateName, rollNumber, examCode, candidatePhotoUrl, _branding);
            examWindow.Show(); this.Close();
        }
    }
}