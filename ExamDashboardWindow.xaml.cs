#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SecureCBTClient
{
    public partial class ExamDashboardWindow : Window
    {
        private string candidateName, rollNumber, examCode, candidatePhotoUrl;
        private ExamBranding _branding;

        private List<ExamSection> examSections = new List<ExamSection>();
        private int currentSectionIndex = 0;
        private List<Question> currentQuestionBank = new List<Question>();
        private Dictionary<string, string> studentAnswers = new Dictionary<string, string>();
        private int currentQuestionIndex = 0;

        private DispatcherTimer globalExamTimer, heartbeatTimer;
        private TimeSpan globalTimeRemaining;

        private static readonly HttpClient mediaClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true });
        private static readonly HttpClient translateClient = new HttpClient();

        public ExamDashboardWindow(string candName, string rollNo, string examCd, string photoUrl, ExamBranding branding)
        {
            InitializeComponent();
            this.candidateName = candName; this.rollNumber = rollNo; this.examCode = examCd;
            this.candidatePhotoUrl = photoUrl; this._branding = branding;

            lblCandidateName.Text = candidateName;
            lblRollExam.Text = $"Roll No: {rollNumber} | Exam: {examCode}";

            ApplyBranding();
            _ = LoadImageAsync(candidatePhotoUrl, imgCandidatePhoto);
            btnReview.Click += btnReview_Click;

            SetupHeartbeatTimer();
            _ = LoadExamDataAsync();
        }

        private void ApplyBranding()
        {
            try
            {
                var colorBrush = (SolidColorBrush)new BrushConverter().ConvertFromString(_branding.ThemeColor);
                HeaderBorder.Background = colorBrush;
                TimerBorder.Background = colorBrush;
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

        private void SetupHeartbeatTimer()
        {
            heartbeatTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            heartbeatTimer.Tick += async (s, e) => await SyncHeartbeatAsync();
            heartbeatTimer.Start();
        }

        private async Task LoadExamDataAsync()
        {
            try
            {
                JObject jsonResponse = await ApiService.GetQuestionsAsync(examCode, rollNumber);
                if (jsonResponse["status"]?.ToString() == "success")
                {
                    int remainingSeconds = (int)jsonResponse["remainingSeconds"];
                    globalTimeRemaining = TimeSpan.FromSeconds(remainingSeconds);
                    StartGlobalTimer();

                    JToken savedAnswersToken = jsonResponse["savedAnswers"];
                    if (savedAnswersToken != null && savedAnswersToken.HasValues)
                        studentAnswers = JsonConvert.DeserializeObject<Dictionary<string, string>>(savedAnswersToken.ToString());

                    JArray sectionsArray = (JArray)jsonResponse["sections"];
                    foreach (var secToken in sectionsArray)
                    {
                        ExamSection newSection = new ExamSection { Id = secToken["id"].ToString(), Name = secToken["name"].ToString() };
                        foreach (var q in secToken["questions"])
                        {
                            string qId = q["qId"].ToString();
                            QuestionState initialState = studentAnswers.ContainsKey(qId) ? QuestionState.Answered : QuestionState.NotVisited;
                            newSection.Questions.Add(new Question
                            {
                                Id = qId,
                                Text = q["text"].ToString(),
                                ImageUrl = q["imageUrl"]?.ToString(),
                                OptA = q["options"]["A"].ToString(),
                                OptB = q["options"]["B"].ToString(),
                                OptC = q["options"]["C"].ToString(),
                                OptD = q["options"]["D"].ToString(),
                                State = initialState
                            });
                        }
                        examSections.Add(newSection);
                    }

                    if (examSections.Count > 0) { gridLoading.Visibility = Visibility.Collapsed; LoadSection(0); }
                    else { MessageBox.Show("No questions found."); Application.Current.Shutdown(); }
                }
                else { MessageBox.Show(jsonResponse["message"]?.ToString()); Application.Current.Shutdown(); }
            }
            catch (Exception ex) { MessageBox.Show("Network Error: " + ex.Message); }
        }

        private void StartGlobalTimer()
        {
            globalExamTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            globalExamTimer.Tick += (s, e) => {
                globalTimeRemaining = globalTimeRemaining.Subtract(TimeSpan.FromSeconds(1));
                lblTimer.Text = string.Format("{0:D2}:{1:D2}:{2:D2}", globalTimeRemaining.Hours, globalTimeRemaining.Minutes, globalTimeRemaining.Seconds);
                if (globalTimeRemaining.TotalSeconds <= 0) { globalExamTimer.Stop(); _ = SubmitExamAsync(); }
            };
            globalExamTimer.Start();
        }

        private void LoadSection(int index)
        {
            currentSectionIndex = index;
            currentQuestionBank = examSections[currentSectionIndex].Questions;
            currentQuestionIndex = 0;
            BuildSectionTabs(); BuildPalette(); DisplayQuestion(0);
        }

        private void BuildSectionTabs()
        {
            spSectionTabs.Children.Clear();
            for (int i = 0; i < examSections.Count; i++)
            {
                Button tabBtn = new Button { Content = examSections[i].Name, Height = 38, Padding = new Thickness(20, 0, 20, 0), BorderThickness = new Thickness(0, 0, 1, 0), Tag = i };
                tabBtn.Click += (s, e) => { int target = (int)((Button)s).Tag; if (target != currentSectionIndex) LoadSection(target); };
                if (i == currentSectionIndex) { tabBtn.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)); tabBtn.Foreground = Brushes.White; }
                else { tabBtn.Background = Brushes.Transparent; tabBtn.Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)); }
                spSectionTabs.Children.Add(tabBtn);
            }
        }

        private void BuildPalette()
        {
            wpPalette.Children.Clear();
            for (int i = 0; i < currentQuestionBank.Count; i++)
            {
                Button btn = new Button { Content = (i + 1).ToString(), Width = 40, Height = 40, Margin = new Thickness(5), Tag = i };
                btn.Click += (s, e) => DisplayQuestion((int)((Button)s).Tag);
                wpPalette.Children.Add(btn);
                UpdatePaletteColor(i);
            }
        }

        private void DisplayQuestion(int index)
        {
            if (index < 0 || index >= currentQuestionBank.Count) return;
            if (currentQuestionBank[currentQuestionIndex].State == QuestionState.NotVisited) { currentQuestionBank[currentQuestionIndex].State = QuestionState.NotAnswered; UpdatePaletteColor(currentQuestionIndex); }

            currentQuestionIndex = index;
            Question q = currentQuestionBank[currentQuestionIndex];
            if (q.State == QuestionState.NotVisited) { q.State = QuestionState.NotAnswered; UpdatePaletteColor(currentQuestionIndex); }

            lblQuestionNumber.Text = $"Question No. {currentQuestionIndex + 1}";
            UpdateQuestionTextUI();

            imgQuestionPhoto.Source = null; borderQuestionImg.Visibility = Visibility.Collapsed;
            if (!string.IsNullOrEmpty(q.ImageUrl)) { borderQuestionImg.Visibility = Visibility.Visible; _ = LoadImageAsync(q.ImageUrl, imgQuestionPhoto); }

            optA.Checked -= Option_Checked; optB.Checked -= Option_Checked; optC.Checked -= Option_Checked; optD.Checked -= Option_Checked;
            optA.IsChecked = false; optB.IsChecked = false; optC.IsChecked = false; optD.IsChecked = false;

            if (studentAnswers.ContainsKey(q.Id))
            {
                string ans = studentAnswers[q.Id];
                if (ans == "A") optA.IsChecked = true; else if (ans == "B") optB.IsChecked = true; else if (ans == "C") optC.IsChecked = true; else if (ans == "D") optD.IsChecked = true;
            }
            optA.Checked += Option_Checked; optB.Checked += Option_Checked; optC.Checked += Option_Checked; optD.Checked += Option_Checked;
            btnPrev.IsEnabled = (currentQuestionIndex > 0);
            btnNext.Content = (currentQuestionIndex == currentQuestionBank.Count - 1) ? "Save Answer" : "Save & Next";
        }

        private async void UpdateQuestionTextUI()
        {
            if (currentQuestionBank == null || currentQuestionBank.Count == 0) return;
            Question q = currentQuestionBank[currentQuestionIndex];
            int activeIndex = currentQuestionIndex;
            bool isHindi = cmbLanguage != null && cmbLanguage.SelectedIndex == 1;

            if (isHindi)
            {
                if (string.IsNullOrEmpty(q.Text_Hi))
                {
                    txtQuestion.Text = "अनुवाद हो रहा है..."; txtOptA.Text = "..."; txtOptB.Text = "..."; txtOptC.Text = "..."; txtOptD.Text = "...";
                    q.Text_Hi = await TranslateTextAsync(q.Text); q.OptA_Hi = await TranslateTextAsync(q.OptA); q.OptB_Hi = await TranslateTextAsync(q.OptB); q.OptC_Hi = await TranslateTextAsync(q.OptC); q.OptD_Hi = await TranslateTextAsync(q.OptD);
                }
                if (activeIndex == currentQuestionIndex) { txtQuestion.Text = q.Text_Hi; txtOptA.Text = q.OptA_Hi; txtOptB.Text = q.OptB_Hi; txtOptC.Text = q.OptC_Hi; txtOptD.Text = q.OptD_Hi; }
            }
            else
            {
                txtQuestion.Text = q.Text; txtOptA.Text = q.OptA; txtOptB.Text = q.OptB; txtOptC.Text = q.OptC; txtOptD.Text = q.OptD;
            }
        }

        private async Task<string> TranslateTextAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            try { string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=hi&dt=t&q={Uri.EscapeDataString(text)}"; string res = await translateClient.GetStringAsync(url); JArray json = JArray.Parse(res); string trans = ""; foreach (JArray item in json[0]) trans += item[0]; return trans; }
            catch { return text; }
        }

        private void cmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateQuestionTextUI();
        private void Option_Checked(object sender, RoutedEventArgs e) { if (((RadioButton)sender).IsChecked == true) { studentAnswers[currentQuestionBank[currentQuestionIndex].Id] = ((RadioButton)sender).Tag.ToString(); currentQuestionBank[currentQuestionIndex].State = QuestionState.Answered; UpdatePaletteColor(currentQuestionIndex); } }
        private void btnReview_Click(object sender, RoutedEventArgs e) { currentQuestionBank[currentQuestionIndex].State = QuestionState.MarkedForReview; UpdatePaletteColor(currentQuestionIndex); DisplayQuestion(currentQuestionIndex + 1); }
        private void btnClear_Click(object sender, RoutedEventArgs e) { studentAnswers.Remove(currentQuestionBank[currentQuestionIndex].Id); optA.IsChecked = false; optB.IsChecked = false; optC.IsChecked = false; optD.IsChecked = false; currentQuestionBank[currentQuestionIndex].State = QuestionState.NotAnswered; UpdatePaletteColor(currentQuestionIndex); }
        private void btnPrev_Click(object sender, RoutedEventArgs e) => DisplayQuestion(currentQuestionIndex - 1);
        private void btnNext_Click(object sender, RoutedEventArgs e) => DisplayQuestion(currentQuestionIndex + 1);

        private void UpdatePaletteColor(int index)
        {
            Button btn = (Button)wpPalette.Children[index]; QuestionState s = currentQuestionBank[index].State;
            if (s == QuestionState.NotVisited) { btn.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)); btn.Foreground = Brushes.Black; }
            else if (s == QuestionState.NotAnswered) { btn.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68)); btn.Foreground = Brushes.White; }
            else if (s == QuestionState.Answered) { btn.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)); btn.Foreground = Brushes.White; }
            else if (s == QuestionState.MarkedForReview) { btn.Background = new SolidColorBrush(Color.FromRgb(139, 92, 246)); btn.Foreground = Brushes.White; }
        }

        private async Task SyncHeartbeatAsync() { try { bool ok = await ApiService.SyncHeartbeatAsync(rollNumber, examCode, studentAnswers); lblSyncStatus.Text = ok ? "Connected" : "Offline"; } catch { lblSyncStatus.Text = "Offline"; } }

        private void btnSubmitExam_Click(object sender, RoutedEventArgs e) { if (MessageBox.Show("Final submit the entire exam?", "Submit", MessageBoxButton.YesNo) == MessageBoxResult.Yes) _ = SubmitExamAsync(); }

        private async Task SubmitExamAsync()
        {
            globalExamTimer?.Stop(); heartbeatTimer?.Stop(); btnSubmitExam.IsEnabled = false;
            try { await ApiService.SubmitExamAsync(rollNumber, examCode, studentAnswers); new ResultWindow(rollNumber, examCode, _branding).Show(); this.Close(); } catch { btnSubmitExam.IsEnabled = true; }
        }
    }
}
