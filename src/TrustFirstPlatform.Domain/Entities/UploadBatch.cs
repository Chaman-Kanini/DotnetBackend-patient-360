namespace TrustFirstPlatform.Domain.Entities
{
    public class UploadBatch
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public int TotalDocuments { get; set; }
        public int ProcessedDocuments { get; set; }
        public string Status { get; set; } = "Processing"; // "Processing", "Completed", "Failed"
        
        public User? User { get; set; }
        public ICollection<ClinicalDocument> Documents { get; set; } = new List<ClinicalDocument>();
    }
}
