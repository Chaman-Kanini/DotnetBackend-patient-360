using FluentValidation.TestHelper;
using TrustFirstPlatform.Application.DTOs;
using TrustFirstPlatform.Application.Validators;

namespace TrustFirstPlatform.Application.Tests.Validators
{
    public class CreateUserRequestValidatorTests
    {
        private readonly CreateUserRequestValidator _validator;

        public CreateUserRequestValidatorTests()
        {
            _validator = new CreateUserRequestValidator();
        }

        [Fact]
        public void Validate_ValidRequest_ShouldNotHaveErrors()
        {
            var request = new CreateUserRequest(
                Email: "user@test.com",
                FirstName: "John",
                LastName: "Doe",
                Role: "StandardUser",
                PhoneNumber: "1234567890",
                Department: "IT"
            );

            var result = _validator.TestValidate(request);

            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_EmptyEmail_ShouldHaveError()
        {
            var request = new CreateUserRequest(
                Email: "",
                FirstName: "John",
                LastName: "Doe",
                Role: "StandardUser",
                PhoneNumber: null,
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldHaveValidationErrorFor(x => x.Email)
                .WithErrorMessage("Email is required");
        }

        [Fact]
        public void Validate_InvalidEmailFormat_ShouldHaveError()
        {
            var request = new CreateUserRequest(
                Email: "not-an-email",
                FirstName: "John",
                LastName: "Doe",
                Role: "StandardUser",
                PhoneNumber: null,
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldHaveValidationErrorFor(x => x.Email)
                .WithErrorMessage("Invalid email format");
        }

        [Fact]
        public void Validate_EmptyFirstName_ShouldHaveError()
        {
            var request = new CreateUserRequest(
                Email: "user@test.com",
                FirstName: "",
                LastName: "Doe",
                Role: "StandardUser",
                PhoneNumber: null,
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldHaveValidationErrorFor(x => x.FirstName)
                .WithErrorMessage("First name is required");
        }

        [Fact]
        public void Validate_FirstNameExceedsMaxLength_ShouldHaveError()
        {
            var request = new CreateUserRequest(
                Email: "user@test.com",
                FirstName: new string('A', 101),
                LastName: "Doe",
                Role: "StandardUser",
                PhoneNumber: null,
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldHaveValidationErrorFor(x => x.FirstName)
                .WithErrorMessage("First name cannot exceed 100 characters");
        }

        [Fact]
        public void Validate_EmptyLastName_ShouldHaveError()
        {
            var request = new CreateUserRequest(
                Email: "user@test.com",
                FirstName: "John",
                LastName: "",
                Role: "StandardUser",
                PhoneNumber: null,
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldHaveValidationErrorFor(x => x.LastName)
                .WithErrorMessage("Last name is required");
        }

        [Fact]
        public void Validate_EmptyRole_ShouldHaveError()
        {
            var request = new CreateUserRequest(
                Email: "user@test.com",
                FirstName: "John",
                LastName: "Doe",
                Role: "",
                PhoneNumber: null,
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldHaveValidationErrorFor(x => x.Role);
        }

        [Fact]
        public void Validate_InvalidRole_ShouldHaveError()
        {
            var request = new CreateUserRequest(
                Email: "user@test.com",
                FirstName: "John",
                LastName: "Doe",
                Role: "SuperAdmin",
                PhoneNumber: null,
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldHaveValidationErrorFor(x => x.Role)
                .WithErrorMessage("Role must be 'Admin' or 'StandardUser'");
        }

        [Theory]
        [InlineData("Admin")]
        [InlineData("StandardUser")]
        public void Validate_ValidRoles_ShouldNotHaveError(string role)
        {
            var request = new CreateUserRequest(
                Email: "user@test.com",
                FirstName: "John",
                LastName: "Doe",
                Role: role,
                PhoneNumber: null,
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldNotHaveValidationErrorFor(x => x.Role);
        }

        [Fact]
        public void Validate_PhoneNumberExceedsMaxLength_ShouldHaveError()
        {
            var request = new CreateUserRequest(
                Email: "user@test.com",
                FirstName: "John",
                LastName: "Doe",
                Role: "StandardUser",
                PhoneNumber: new string('1', 21),
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
        }

        [Fact]
        public void Validate_PhoneNumberWithInvalidChars_ShouldHaveError()
        {
            var request = new CreateUserRequest(
                Email: "user@test.com",
                FirstName: "John",
                LastName: "Doe",
                Role: "StandardUser",
                PhoneNumber: "abc-invalid",
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
        }

        [Theory]
        [InlineData("1234567890")]
        [InlineData("+1 (234) 567-8901")]
        [InlineData("123 456 7890")]
        public void Validate_ValidPhoneNumbers_ShouldNotHaveError(string phone)
        {
            var request = new CreateUserRequest(
                Email: "user@test.com",
                FirstName: "John",
                LastName: "Doe",
                Role: "StandardUser",
                PhoneNumber: phone,
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldNotHaveValidationErrorFor(x => x.PhoneNumber);
        }

        [Fact]
        public void Validate_DepartmentExceedsMaxLength_ShouldHaveError()
        {
            var request = new CreateUserRequest(
                Email: "user@test.com",
                FirstName: "John",
                LastName: "Doe",
                Role: "StandardUser",
                PhoneNumber: null,
                Department: new string('A', 101)
            );

            var result = _validator.TestValidate(request);

            result.ShouldHaveValidationErrorFor(x => x.Department);
        }

        [Fact]
        public void Validate_NullOptionalFields_ShouldNotHaveErrors()
        {
            var request = new CreateUserRequest(
                Email: "user@test.com",
                FirstName: "John",
                LastName: "Doe",
                Role: "Admin",
                PhoneNumber: null,
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldNotHaveAnyValidationErrors();
        }
    }
}
