using FluentValidation;
using TrustFirstPlatform.Application.DTOs;

namespace TrustFirstPlatform.Application.Validators
{
    public class UserFilterRequestValidator : AbstractValidator<UserFilterRequest>
    {
        public UserFilterRequestValidator()
        {
            RuleFor(x => x.SearchTerm)
                .MaximumLength(200).WithMessage("Search term cannot exceed 200 characters")
                .When(x => x.SearchTerm != null);

            RuleFor(x => x.Role)
                .Must(role => role == null || role == "Admin" || role == "StandardUser")
                .WithMessage("Role filter must be 'Admin', 'StandardUser', or null");

            RuleFor(x => x.Status)
                .Must(status => status == null || status == "Active" || status == "Pending" || status == "Inactive")
                .WithMessage("Status filter must be 'Active', 'Pending', 'Inactive', or null");

            RuleFor(x => x.Page)
                .GreaterThan(0).WithMessage("Page number must be greater than 0");

            RuleFor(x => x.PageSize)
                .GreaterThan(0).WithMessage("Page size must be greater than 0")
                .LessThanOrEqualTo(100).WithMessage("Page size cannot exceed 100");
        }
    }
}
