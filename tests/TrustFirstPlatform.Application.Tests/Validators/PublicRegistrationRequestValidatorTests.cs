using FluentValidation.TestHelper;
using TrustFirstPlatform.Application.DTOs;
using TrustFirstPlatform.Application.Validators;

namespace TrustFirstPlatform.Application.Tests.Validators
{
    public class PublicRegistrationRequestValidatorTests
    {
        private readonly PublicRegistrationRequestValidator _validator;

        public PublicRegistrationRequestValidatorTests()
        {
            _validator = new PublicRegistrationRequestValidator();
        }

        [Fact]
        public void Validate_ValidRequest_ShouldNotHaveErrors()
        {
            var request = new PublicRegistrationRequest(
                Email: "newuser@test.com",
                Password: "SecurePass123!",
                ConfirmPassword: "SecurePass123!",
                FirstName: "John",
                LastName: "Doe",
                PhoneNumber: "1234567890",
                Department: "IT"
            );

            var result = _validator.TestValidate(request);

            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_EmptyEmail_ShouldHaveError()
        {
            var request = new PublicRegistrationRequest(
                Email: "",
                Password: "SecurePass123!",
                ConfirmPassword: "SecurePass123!",
                FirstName: "John",
                LastName: "Doe",
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
            var request = new PublicRegistrationRequest(
                Email: "not-an-email",
                Password: "SecurePass123!",
                ConfirmPassword: "SecurePass123!",
                FirstName: "John",
                LastName: "Doe",
                PhoneNumber: null,
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldHaveValidationErrorFor(x => x.Email)
                .WithErrorMessage("Invalid email format");
        }

        [Fact]
        public void Validate_EmptyPassword_ShouldHaveError()
        {
            var request = new PublicRegistrationRequest(
                Email: "user@test.com",
                Password: "",
                ConfirmPassword: "",
                FirstName: "John",
                LastName: "Doe",
                PhoneNumber: null,
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldHaveValidationErrorFor(x => x.Password)
                .WithErrorMessage("Password is required");
        }

        [Fact]
        public void Validate_ShortPassword_ShouldHaveError()
        {
            var request = new PublicRegistrationRequest(
                Email: "user@test.com",
                Password: "Sh0rt!",
                ConfirmPassword: "Sh0rt!",
                FirstName: "John",
                LastName: "Doe",
                PhoneNumber: null,
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldHaveValidationErrorFor(x => x.Password)
                .WithErrorMessage("Password must be at least 8 characters long");
        }

        [Theory]
        [InlineData("nouppercase1!")]
        [InlineData("NOLOWERCASE1!")]
        [InlineData("NoDigitsHere!")]
        [InlineData("NoSpecialChar1")]
        public void Validate_PasswordMissingComplexity_ShouldHaveError(string password)
        {
            var request = new PublicRegistrationRequest(
                Email: "user@test.com",
                Password: password,
                ConfirmPassword: password,
                FirstName: "John",
                LastName: "Doe",
                PhoneNumber: null,
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldHaveValidationErrorFor(x => x.Password);
        }

        [Fact]
        public void Validate_MismatchedPasswords_ShouldHaveError()
        {
            var request = new PublicRegistrationRequest(
                Email: "user@test.com",
                Password: "SecurePass123!",
                ConfirmPassword: "DifferentPass456!",
                FirstName: "John",
                LastName: "Doe",
                PhoneNumber: null,
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword)
                .WithErrorMessage("Password and confirmation must match");
        }

        [Fact]
        public void Validate_EmptyConfirmPassword_ShouldHaveError()
        {
            var request = new PublicRegistrationRequest(
                Email: "user@test.com",
                Password: "SecurePass123!",
                ConfirmPassword: "",
                FirstName: "John",
                LastName: "Doe",
                PhoneNumber: null,
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword)
                .WithErrorMessage("Password confirmation is required");
        }

        [Fact]
        public void Validate_EmptyFirstName_ShouldHaveError()
        {
            var request = new PublicRegistrationRequest(
                Email: "user@test.com",
                Password: "SecurePass123!",
                ConfirmPassword: "SecurePass123!",
                FirstName: "",
                LastName: "Doe",
                PhoneNumber: null,
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldHaveValidationErrorFor(x => x.FirstName)
                .WithErrorMessage("First name is required");
        }

        [Fact]
        public void Validate_EmptyLastName_ShouldHaveError()
        {
            var request = new PublicRegistrationRequest(
                Email: "user@test.com",
                Password: "SecurePass123!",
                ConfirmPassword: "SecurePass123!",
                FirstName: "John",
                LastName: "",
                PhoneNumber: null,
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldHaveValidationErrorFor(x => x.LastName)
                .WithErrorMessage("Last name is required");
        }

        [Fact]
        public void Validate_PhoneNumberExceedsMaxLength_ShouldHaveError()
        {
            var request = new PublicRegistrationRequest(
                Email: "user@test.com",
                Password: "SecurePass123!",
                ConfirmPassword: "SecurePass123!",
                FirstName: "John",
                LastName: "Doe",
                PhoneNumber: new string('1', 21),
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
        }

        [Fact]
        public void Validate_PhoneNumberWithInvalidChars_ShouldHaveError()
        {
            var request = new PublicRegistrationRequest(
                Email: "user@test.com",
                Password: "SecurePass123!",
                ConfirmPassword: "SecurePass123!",
                FirstName: "John",
                LastName: "Doe",
                PhoneNumber: "abc-invalid",
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
        }

        [Fact]
        public void Validate_DepartmentExceedsMaxLength_ShouldHaveError()
        {
            var request = new PublicRegistrationRequest(
                Email: "user@test.com",
                Password: "SecurePass123!",
                ConfirmPassword: "SecurePass123!",
                FirstName: "John",
                LastName: "Doe",
                PhoneNumber: null,
                Department: new string('A', 101)
            );

            var result = _validator.TestValidate(request);

            result.ShouldHaveValidationErrorFor(x => x.Department);
        }

        [Fact]
        public void Validate_NullOptionalFields_ShouldNotHaveErrors()
        {
            var request = new PublicRegistrationRequest(
                Email: "user@test.com",
                Password: "SecurePass123!",
                ConfirmPassword: "SecurePass123!",
                FirstName: "John",
                LastName: "Doe",
                PhoneNumber: null,
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData("ValidPass123!")]
        [InlineData("AnotherGood1@")]
        [InlineData("C0mpl3x!P@ss")]
        public void Validate_ValidPasswords_ShouldNotHaveError(string password)
        {
            var request = new PublicRegistrationRequest(
                Email: "user@test.com",
                Password: password,
                ConfirmPassword: password,
                FirstName: "John",
                LastName: "Doe",
                PhoneNumber: null,
                Department: null
            );

            var result = _validator.TestValidate(request);

            result.ShouldNotHaveValidationErrorFor(x => x.Password);
        }
    }
}
