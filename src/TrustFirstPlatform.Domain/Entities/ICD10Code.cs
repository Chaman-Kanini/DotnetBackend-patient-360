using System;

namespace TrustFirstPlatform.Domain.Entities
{
    public class ICD10Code
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Diagnosis { get; set; } = string.Empty;
    }
}
