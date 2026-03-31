namespace TrustFirstPlatform.Application.Models
{
    public class ICD10CodeDto
    {
        public string Diagnosis { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class CPTCodeDto
    {
        public string Procedure { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class MedicalCodesResult
    {
        public List<ICD10CodeDto> ICD10Codes { get; set; } = new();
        public List<CPTCodeDto> CPTCodes { get; set; } = new();
        public int DiagnosesMatched { get; set; }
        public int ProceduresMatched { get; set; }
        public int TotalDiagnoses { get; set; }
        public int TotalProcedures { get; set; }
    }
}
