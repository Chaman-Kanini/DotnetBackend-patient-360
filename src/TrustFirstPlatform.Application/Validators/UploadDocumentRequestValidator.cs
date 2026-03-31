using FluentValidation;
using TrustFirstPlatform.Application.DTOs;

namespace TrustFirstPlatform.Application.Validators
{
    public class UploadDocumentRequestValidator : AbstractValidator<UploadDocumentRequest>
    {
        private static readonly string[] AllowedExtensions = { ".pdf", ".doc", ".docx" };
        private const long MaxFileSizeBytes = 52_428_800; // 50MB

        public UploadDocumentRequestValidator()
        {
            RuleFor(x => x.File)
                .NotNull();

            RuleFor(x => x.File.Length)
                .GreaterThan(0)
                .WithMessage("No file provided")
                .When(x => x.File != null);

            RuleFor(x => x.File.Length)
                .LessThanOrEqualTo(MaxFileSizeBytes)
                .WithMessage("File size exceeds 50MB limit")
                .When(x => x.File != null);

            RuleFor(x => x.File.FileName)
                .Must(fileName => AllowedExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                .WithMessage("File type not supported. Please upload PDF, DOC, or DOCX files only.")
                .When(x => x.File != null && !string.IsNullOrWhiteSpace(x.File.FileName));
        }
    }
}
