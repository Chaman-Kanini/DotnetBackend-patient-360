using FluentValidation;
using TrustFirstPlatform.Application.DTOs;

namespace TrustFirstPlatform.Application.Validators
{
    public class UploadDocumentsRequestValidator : AbstractValidator<UploadDocumentsRequest>
    {
        private const int MaxFiles = 10;

        public UploadDocumentsRequestValidator()
        {
            RuleFor(x => x.Files)
                .NotNull();

            RuleFor(x => x.Files.Count)
                .GreaterThan(0)
                .WithMessage("No files provided")
                .When(x => x.Files != null);

            RuleFor(x => x.Files.Count)
                .LessThanOrEqualTo(MaxFiles)
                .WithMessage("Maximum 10 files allowed per upload")
                .When(x => x.Files != null);
        }
    }
}
