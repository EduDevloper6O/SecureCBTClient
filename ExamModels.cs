using System.Collections.Generic;

namespace SecureCBTClient
{
    // Tracks the current state of a question for the right-side palette
    public enum QuestionState
    {
        NotVisited,
        NotAnswered,
        Answered,
        MarkedForReview
    }

    // Represents a section in the exam (e.g., Mathematics, Physics)
    public class ExamSection
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<Question> Questions { get; set; } = new List<Question>();
    }

    // Represents an individual question and its translated states
    public class Question
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public string ImageUrl { get; set; }
        public string OptA { get; set; }
        public string OptB { get; set; }
        public string OptC { get; set; }
        public string OptD { get; set; }

        // Hindi Translation Properties
        public string Text_Hi { get; set; }
        public string OptA_Hi { get; set; }
        public string OptB_Hi { get; set; }
        public string OptC_Hi { get; set; }
        public string OptD_Hi { get; set; }

        public QuestionState State { get; set; }
    }
}