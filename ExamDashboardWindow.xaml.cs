#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
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
    public enum QuestionState
    {
        NotVisited,
        NotAnswered,
        Answered,
        MarkedForReview
    }

    public partial class ExamDashboardWindow : Window
    {
        private string candidateName;
        private string rollNumber;
        private string examCode;
        private string candidatePhotoUrl;

        private List<ExamSection> examSections = new List<ExamSection>();
        private int currentSectionIndex = 0;

        private List<Question> currentQuestionBank = new List<Question>();
        private Dictionary<string, string> studentAnswers = new Dictionary<string, string>();
        private int currentQuestionIndex = 0;

        private DispatcherTimer globalExamTimer;
        private DispatcherTimer heartbeatTimer;
        private TimeSpan globalTimeRemaining;

        private static readonly HttpClient mediaClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true });
        private static readonly HttpClient translateClient = new HttpClient();

        public ExamDashboardWindow(string candName, string rollNo, string examCd, string photoUrl = "")
        {
            InitializeComponent();

            this.candidateName = candName;
            this.rollNumber = rollNo;
            this.examCode = examCd;
            this.candidatePhotoUrl = photoUrl;

            lblCandidateName.Text = candidateName;
            lblRollExam.Text = $"Roll No: {rollNumber} | Exam: {examCode}";

            _ = LoadCandidatePhotoAsync();
            btnReview.Click += btnReview_Click;

            SetupHeartbeatTimer();
            _ = LoadExamDataAsync();
        }

        private async Task LoadCandidatePhotoAsync()
        {
            if (!string.IsNullOrEmpty(candidatePhotoUrl))
            {
                try
                {
                    byte[] imageBytes = await mediaClient.GetByteArrayAsync(candidatePhotoUrl);
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
                catch { }
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
                txtLoadingMessage.Text = "Retrieving section sheets & question media...";
                JObject jsonResponse = await ApiService.GetQuestionsAsync(examCode, rollNumber);

                if (jsonResponse["status"]?.ToString() == "success")
                {
                    int remainingSeconds = (int)jsonResponse["remainingSeconds"];
                    globalTimeRemaining = TimeSpan.FromSeconds(remainingSeconds);
                    StartGlobalTimer();

                    // Restore Answers for Crash Recovery
                    JToken savedAnswersToken = jsonResponse["savedAnswers"];
                    if (savedAnswersToken != null && savedAnswersToken.HasValues)
                    {
                        studentAnswers = JsonConvert.DeserializeObject<Dictionary<string, string>>(savedAnswersToken.ToString());
                    }

                    // Build Sections & Questions
                    JArray sectionsArray = (JArray)jsonResponse["sections"];
                    foreach (var secToken in sectionsArray)
                    {
                        ExamSection newSection = new ExamSection
                        {
                            Id = secToken["id"].ToString(),
                            Name = secToken["name"].ToString()
                        };

                        foreach (var q in secToken["questions"])
                        {
                            string qId = q["qId"].ToString();
                            QuestionState initialState = studentAnswers.ContainsKey(qId)
                                                         ? QuestionState.Answered
                                                         : QuestionState.NotVisited;

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

                    if (examSections.Count > 0)
                    {
                        gridLoading.Visibility = Visibility.Collapsed;
                        LoadSection(0);
                    }
                    else
                    {
                        gridLoading.Visibility = Visibility.Collapsed;
                        MessageBox.Show("Configuration Error: No sections or questions found.", "Exam Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        Application.Current.Shutdown();
                    }
                }
                else
                {
                    gridLoading.Visibility = Visibility.Collapsed;
                    MessageBox.Show(jsonResponse["message"]?.ToString(), "Schedule Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                gridLoading.Visibility = Visibility.Collapsed;
                MessageBox.Show("Network Error fetching exam data: " + ex.Message, "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartGlobalTimer()
        {
            globalExamTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            globalExamTimer.Tick += GlobalExamTimer_Tick;
            globalExamTimer.Start();
        }

        private void GlobalExamTimer_Tick(object sender, EventArgs e)
        {
            globalTimeRemaining = globalTimeRemaining.Subtract(TimeSpan.FromSeconds(1));
            lblTimer.Text = string.Format("{0:D2}:{1:D2}:{2:D2}", globalTimeRemaining.Hours, globalTimeRemaining.Minutes, globalTimeRemaining.Seconds);

            if (globalTimeRemaining.TotalSeconds <= 0)
            {
                globalExamTimer.Stop();
                MessageBox.Show("Global Examination Time is up. Your exam will now be automatically submitted.", "Exam Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                _ = SubmitExamAsync(true);
            }
        }

        private void LoadSection(int index)
        {
            if (index < 0 || index >= examSections.Count) return;

            currentSectionIndex = index;
            ExamSection activeSection = examSections[currentSectionIndex];

            currentQuestionBank = activeSection.Questions;
            currentQuestionIndex = 0;

            BuildSectionTabs();
            BuildPalette();
            DisplayQuestion(0);
        }

        private void BuildSectionTabs()
        {
            spSectionTabs.Children.Clear();
            for (int i = 0; i < examSections.Count; i++)
            {
                Button tabBtn = new Button
                {
                    Content = examSections[i].Name,
                    Height = 38,
                    Padding = new Thickness(20, 0, 20, 0),
                    BorderThickness = new Thickness(0, 0, 1, 0),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 13,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = i
                };

                tabBtn.Click += (s, e) =>
                {
                    int targetIndex = (int)((Button)s).Tag;
                    if (targetIndex != currentSectionIndex) LoadSection(targetIndex);
                };

                if (i == currentSectionIndex)
                {
                    tabBtn.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                    tabBtn.Foreground = Brushes.White;
                }
                else
                {
                    tabBtn.Background = Brushes.Transparent;
                    tabBtn.Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105));
                }

                spSectionTabs.Children.Add(tabBtn);
            }
        }

        private void BuildPalette()
        {
            wpPalette.Children.Clear();
            for (int i = 0; i < currentQuestionBank.Count; i++)
            {
                Button btn = new Button
                {
                    Content = (i + 1).ToString(),
                    Width = 40,
                    Height = 40,
                    Margin = new Thickness(5),
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = i
                };

                btn.Click += (s, e) => DisplayQuestion((int)((Button)s).Tag);
                wpPalette.Children.Add(btn);
                UpdatePaletteColor(i);
            }
        }

        private void DisplayQuestion(int index)
        {
            if (index < 0 || index >= currentQuestionBank.Count) return;

            if (currentQuestionBank[currentQuestionIndex].State == QuestionState.NotVisited)
            {
                currentQuestionBank[currentQuestionIndex].State = QuestionState.NotAnswered;
                UpdatePaletteColor(currentQuestionIndex);
            }

            currentQuestionIndex = index;
            Question q = currentQuestionBank[currentQuestionIndex];

            if (q.State == QuestionState.NotVisited)
            {
                q.State = QuestionState.NotAnswered;
                UpdatePaletteColor(currentQuestionIndex);
            }

            lblQuestionNumber.Text = $"Question No. {currentQuestionIndex + 1}";
            UpdateQuestionTextUI();

            imgQuestionPhoto.Source = null;
            borderQuestionImg.Visibility = Visibility.Collapsed;

            if (!string.IsNullOrEmpty(q.ImageUrl))
            {
                borderQuestionImg.Visibility = Visibility.Visible;
                _ = LoadQuestionImageAsync(q.ImageUrl);
            }

            optA.Checked -= Option_Checked; optB.Checked -= Option_Checked; optC.Checked -= Option_Checked; optD.Checked -= Option_Checked;
            optA.IsChecked = false; optB.IsChecked = false; optC.IsChecked = false; optD.IsChecked = false;

            if (studentAnswers.ContainsKey(q.Id))
            {
                string ans = studentAnswers[q.Id];
                if (ans == "A") optA.IsChecked = true;
                else if (ans == "B") optB.IsChecked = true;
                else if (ans == "C") optC.IsChecked = true;
                else if (ans == "D") optD.IsChecked = true;
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
                    txtQuestion.Text = "अनुवाद हो रहा है...";
                    txtOptA.Text = "..."; txtOptB.Text = "..."; txtOptC.Text = "..."; txtOptD.Text = "...";

                    q.Text_Hi = await TranslateTextToHindiAsync(q.Text);
                    q.OptA_Hi = await TranslateTextToHindiAsync(q.OptA);
                    q.OptB_Hi = await TranslateTextToHindiAsync(q.OptB);
                    q.OptC_Hi = await TranslateTextToHindiAsync(q.OptC);
                    q.OptD_Hi = await TranslateTextToHindiAsync(q.OptD);
                }

                if (activeIndex == currentQuestionIndex)
                {
                    txtQuestion.Text = q.Text_Hi;
                    txtOptA.Text = q.OptA_Hi;
                    txtOptB.Text = q.OptB_Hi;
                    txtOptC.Text = q.OptC_Hi;
                    txtOptD.Text = q.OptD_Hi;
                }
            }
            else
            {
                txtQuestion.Text = q.Text;
                txtOptA.Text = q.OptA;
                txtOptB.Text = q.OptB;
                txtOptC.Text = q.OptC;
                txtOptD.Text = q.OptD;
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

        private void cmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateQuestionTextUI();
        }

        private async Task LoadQuestionImageAsync(string url)
        {
            try
            {
                byte[] imageBytes = await mediaClient.GetByteArrayAsync(url);
                using (MemoryStream ms = new MemoryStream(imageBytes))
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    Dispatcher.Invoke(() => imgQuestionPhoto.Source = bitmap);
                }
            }
            catch { }
        }

        private void Option_Checked(object sender, RoutedEventArgs e)
        {
            if (((RadioButton)sender).IsChecked == true)
            {
                studentAnswers[currentQuestionBank[currentQuestionIndex].Id] = ((RadioButton)sender).Tag.ToString();
                currentQuestionBank[currentQuestionIndex].State = QuestionState.Answered;
                UpdatePaletteColor(currentQuestionIndex);
            }
        }

        private void btnReview_Click(object sender, RoutedEventArgs e)
        {
            currentQuestionBank[currentQuestionIndex].State = QuestionState.MarkedForReview;
            UpdatePaletteColor(currentQuestionIndex);
            DisplayQuestion(currentQuestionIndex + 1);
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            studentAnswers.Remove(currentQuestionBank[currentQuestionIndex].Id);
            optA.IsChecked = false; optB.IsChecked = false; optC.IsChecked = false; optD.IsChecked = false;
            currentQuestionBank[currentQuestionIndex].State = QuestionState.NotAnswered;
            UpdatePaletteColor(currentQuestionIndex);
        }

        private void btnPrev_Click(object sender, RoutedEventArgs e) => DisplayQuestion(currentQuestionIndex - 1);
        private void btnNext_Click(object sender, RoutedEventArgs e) => DisplayQuestion(currentQuestionIndex + 1);

        private void UpdatePaletteColor(int index)
        {
            if (index < 0 || index >= wpPalette.Children.Count) return;

            Button btn = (Button)wpPalette.Children[index];
            QuestionState state = currentQuestionBank[index].State;

            switch (state)
            {
                case QuestionState.NotVisited:
                    btn.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                    break;
                case QuestionState.NotAnswered:
                    btn.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    btn.Foreground = Brushes.White;
                    break;
                case QuestionState.Answered:
                    btn.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    btn.Foreground = Brushes.White;
                    break;
                case QuestionState.MarkedForReview:
                    btn.Background = new SolidColorBrush(Color.FromRgb(139, 92, 246));
                    btn.Foreground = Brushes.White;
                    break;
            }
        }

        private async Task SyncHeartbeatAsync()
        {
            try
            {
                bool success = await ApiService.SyncHeartbeatAsync(rollNumber, examCode, studentAnswers);
                if (success) { lblSyncStatus.Text = "Connected to Server"; lblSyncStatus.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); }
            }
            catch { lblSyncStatus.Text = "Offline"; lblSyncStatus.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)); }
        }

        private void btnSubmitExam_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to final submit the entire exam? You cannot return after this.", "Final Submit", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                _ = SubmitExamAsync(false);
        }

        private async Task SubmitExamAsync(bool autoSubmit)
        {
            globalExamTimer?.Stop(); heartbeatTimer?.Stop(); btnSubmitExam.IsEnabled = false;

            try
            {
                await ApiService.SubmitExamAsync(rollNumber, examCode, studentAnswers);
                ResultWindow resultWindow = new ResultWindow(rollNumber, examCode);
                resultWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to submit. " + ex.Message, "Error");
                btnSubmitExam.IsEnabled = true;
            }
        }
    }

    public class ExamSection
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<Question> Questions { get; set; } = new List<Question>();
    }

    public class Question
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public string ImageUrl { get; set; }
        public string OptA { get; set; }
        public string OptB { get; set; }
        public string OptC { get; set; }
        public string OptD { get; set; }
        public string Text_Hi { get; set; }
        public string OptA_Hi { get; set; }
        public string OptB_Hi { get; set; }
        public string OptC_Hi { get; set; }
        public string OptD_Hi { get; set; }
        public QuestionState State { get; set; }
    }
}