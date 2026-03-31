using System;

namespace TrustFirstPlatform.Domain.Entities
{
    public class CPTCode
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Procedure { get; set; } = string.Empty;
    }
}
