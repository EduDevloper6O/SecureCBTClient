namespace SecureCBTClient
{
    public class ExamBranding
    {
        public string AuthorityName { get; set; } = "CBT EXAMINATION BOARD";
        public string ExamTitle { get; set; } = "COMPUTER BASED TEST";
        public string LogoUrl { get; set; } = "";

        // Standard Formatting
        public string ThemeColor { get; set; } = "#1E293B";
        public string TextAlignment { get; set; } = "Left";
        public bool IsBold { get; set; } = true;
        public int TextSize { get; set; } = 20;

        // NEW Advanced Rich-Text Formatting (This fixes your errors)
        public string FontFamily { get; set; } = "Segoe UI";
        public string TextColor { get; set; } = "#FFFFFF";
        public string BgColor { get; set; } = "transparent";
        public bool IsItalic { get; set; } = false;
        public bool IsUnderline { get; set; } = false;
        public int LetterSpacing { get; set; } = 0;
    }
}