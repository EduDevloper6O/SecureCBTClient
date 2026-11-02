#nullable disable
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SecureCBTClient
{
    public static class ApiService
    {
        private static readonly string BASE_URL = "https://script.google.com/macros/s/AKfycbwzEJ3jT301kOp7GQnRZHYUE71GHJlSeVlYOA67FO4juVa2aiZKjxmtBOz2WZBrURwQ5g/exec";

        private static readonly HttpClient client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true }) { Timeout = TimeSpan.FromSeconds(45) };

        public static async Task WakeUpServerAsync() { try { await client.GetStringAsync(BASE_URL); } catch { } }

        public static async Task<JObject> CandidateLoginAsync(string rollNumber, string password, string examCode)
        {
            var payload = new { action = "candidateLogin", rollNumber = rollNumber.Trim(), password = password.Trim(), examCode = examCode.Trim().ToUpper() };
            return await PostJsonAsync(payload);
        }

        public static async Task<JObject> GetQuestionsAsync(string examCode, string rollNumber)
        {
            string url = $"{BASE_URL}?action=getQuestions&examCode={Uri.EscapeDataString(examCode)}&rollNumber={Uri.EscapeDataString(rollNumber)}";
            string response = await client.GetStringAsync(url);
            return JObject.Parse(response);
        }

        public static async Task<bool> SyncHeartbeatAsync(string rollNumber, string examCode, Dictionary<string, string> answers)
        {
            var payload = new { action = "syncHeartbeat", rollNumber = rollNumber, examCode = examCode, answers = answers };
            var res = await PostJsonAsync(payload);
            return res["status"]?.ToString() == "success";
        }

        public static async Task<JObject> SubmitExamAsync(string rollNumber, string examCode, Dictionary<string, string> answers)
        {
            var payload = new { action = "submitExam", rollNumber = rollNumber, examCode = examCode, answers = answers };
            return await PostJsonAsync(payload);
        }

        public static async Task<JObject> GetResultsAsync(string rollNumber, string examCode)
        {
            string url = $"{BASE_URL}?action=getResults&rollNumber={Uri.EscapeDataString(rollNumber)}&examCode={Uri.EscapeDataString(examCode)}";
            string response = await client.GetStringAsync(url);
            return JObject.Parse(response);
        }

        private static async Task<JObject> PostJsonAsync(object payload)
        {
            string json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(BASE_URL, content);
            string resultStr = await response.Content.ReadAsStringAsync();
            return JObject.Parse(resultStr);
        }
    }
}