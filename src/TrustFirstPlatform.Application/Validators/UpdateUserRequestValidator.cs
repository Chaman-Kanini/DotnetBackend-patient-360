using FluentValidation;
using TrustFirstPlatform.Application.DTOs;

namespace TrustFirstPlatform.Application.Validators
{
    public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
    {
        public UpdateUserRequestValidator()
        {
            RuleFor(x => x.FirstName)
                .MaximumLength(100).WithMessage("First name cannot exceed 100 characters")
                .When(x => x.FirstName != null);

            RuleFor(x => x.LastName)
                .MaximumLength(100).WithMessage("Last name cannot exceed 100 characters")
                .When(x => x.LastName != null);

            RuleFor(x => x.PhoneNumber)
                .MaximumLength(20).WithMessage("Phone number cannot exceed 20 characters")
                .Matches(@"^[\d\s\-\+\(\)]+$").When(x => !string.IsNullOrEmpty(x.PhoneNumber))
                .WithMessage("Phone number can only contain digits, spaces, and special characters (+-())");

            RuleFor(x => x.Department)
                .MaximumLength(100).WithMessage("Department cannot exceed 100 characters")
                .When(x => x.Department != null);

            // Ensure at least one field is being updated
            RuleFor(x => new { x.FirstName, x.LastName, x.PhoneNumber, x.Department })
                .Must(fields => fields.FirstName != null || fields.LastName != null || 
                              fields.PhoneNumber != null || fields.Department != null)
                .WithMessage("At least one field must be provided for update");
        }
    }
}
