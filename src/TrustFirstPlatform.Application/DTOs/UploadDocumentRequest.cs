using Microsoft.AspNetCore.Http;

namespace TrustFirstPlatform.Application.DTOs
{
    public class UploadDocumentRequest
    {
        public IFormFile File { get; set; } = null!;
        public Guid? PatientContextId { get; set; }
    }
}
