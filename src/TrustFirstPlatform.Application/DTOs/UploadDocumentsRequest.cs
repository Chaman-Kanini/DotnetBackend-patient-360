using Microsoft.AspNetCore.Http;

namespace TrustFirstPlatform.Application.DTOs
{
    public class UploadDocumentsRequest
    {
        public IFormFileCollection Files { get; set; } = new FormFileCollection();
        public Guid? PatientContextId { get; set; }
    }
}
