using System.Text.Json;
using System.Text.Json.Serialization;

namespace Elecctron.App.Models;

public class ApiStatusResponse
{
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}

public sealed class CandidateLoginResponse : ApiStatusResponse
{
    [JsonPropertyName("candidate")] public CandidateInfo Candidate { get; set; } = new();
    [JsonPropertyName("exam")] public ExamInfo Exam { get; set; } = new();
}

public sealed class CandidateInfo
{
    [JsonPropertyName("rollNumber")] public string RollNumber { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "Candidate";
    [JsonPropertyName("examCode")] public string ExamCode { get; set; } = "";
    [JsonPropertyName("photoUrl")] public string? PhotoUrl { get; set; }
}

public sealed class ExamInfo
{
    [JsonPropertyName("examCode")] public string ExamCode { get; set; } = "";
    [JsonPropertyName("examTitle")] public string ExamTitle { get; set; } = "CBT Exam";
    [JsonPropertyName("totalDurationMinutes")] public int TotalDurationMinutes { get; set; }
    [JsonPropertyName("startTime")] public JsonElement StartTime { get; set; }
}

public sealed class QuestionPackage : ApiStatusResponse
{
    [JsonPropertyName("examCode")] public string ExamCode { get; set; } = "";
    [JsonPropertyName("examTitle")] public string ExamTitle { get; set; } = "CBT Exam";
    [JsonPropertyName("remainingSeconds")] public int RemainingSeconds { get; set; }
    [JsonPropertyName("savedAnswers")] public Dictionary<string, string> SavedAnswers { get; set; } = new();
    [JsonPropertyName("sections")] public List<QuestionSection> Sections { get; set; } = new();
}

public sealed class QuestionSection
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("orderIndex")] public int OrderIndex { get; set; }
    [JsonPropertyName("questions")] public List<ExamQuestion> Questions { get; set; } = new();
}

public sealed class ExamQuestion
{
    [JsonPropertyName("qId")] public string QId { get; set; } = "";
    [JsonPropertyName("text")] public string Text { get; set; } = "";
    [JsonPropertyName("options")] public Dictionary<string, string> Options { get; set; } = new();
    [JsonPropertyName("imageUrl")] public string? ImageUrl { get; set; }
    [JsonIgnore] public string? SelectedOption { get; set; }
    [JsonIgnore] public bool MarkedForReview { get; set; }
    [JsonIgnore] public bool Visited { get; set; }
}

public sealed class SubmitExamResponse : ApiStatusResponse
{
    [JsonPropertyName("scoreData")] public ScoreData ScoreData { get; set; } = new();
}

public sealed class ScoreData
{
    [JsonPropertyName("attempted")] public int Attempted { get; set; }
    [JsonPropertyName("correctCount")] public int CorrectCount { get; set; }
    [JsonPropertyName("wrongCount")] public int WrongCount { get; set; }
    [JsonPropertyName("totalScore")] public decimal TotalScore { get; set; }
}
