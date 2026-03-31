namespace TrustFirstPlatform.Application.Models
{
    public class ChatbotResponse
    {
        public bool Success { get; set; }
        public string Answer { get; set; } = string.Empty;
        public string? Question { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ChatbotRequest
    {
        public string Question { get; set; } = string.Empty;
    }
}
