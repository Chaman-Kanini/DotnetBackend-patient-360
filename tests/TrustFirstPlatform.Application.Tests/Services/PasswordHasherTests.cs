using TrustFirstPlatform.Application.Services;

namespace TrustFirstPlatform.Application.Tests.Services
{
    public class PasswordHasherTests
    {
        [Fact]
        public void HashPassword_ValidPassword_ReturnsHashedPassword()
        {
            var password = "Test@1234";

            var hash = PasswordHasher.HashPassword(password);

            Assert.NotNull(hash);
            Assert.NotEmpty(hash);
            Assert.NotEqual(password, hash);
        }

        [Fact]
        public void HashPassword_SamePassword_ReturnsDifferentHashes()
        {
            var password = "Test@1234";

            var hash1 = PasswordHasher.HashPassword(password);
            var hash2 = PasswordHasher.HashPassword(password);

            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void VerifyPassword_CorrectPassword_ReturnsTrue()
        {
            var password = "Test@1234";
            var hash = PasswordHasher.HashPassword(password);

            var result = PasswordHasher.VerifyPassword(password, hash);

            Assert.True(result);
        }

        [Fact]
        public void VerifyPassword_IncorrectPassword_ReturnsFalse()
        {
            var password = "Test@1234";
            var wrongPassword = "Wrong@1234";
            var hash = PasswordHasher.HashPassword(password);

            var result = PasswordHasher.VerifyPassword(wrongPassword, hash);

            Assert.False(result);
        }

        [Fact]
        public void VerifyPassword_EmptyPassword_ReturnsFalse()
        {
            var password = "Test@1234";
            var hash = PasswordHasher.HashPassword(password);

            var result = PasswordHasher.VerifyPassword("", hash);

            Assert.False(result);
        }

        [Fact]
        public void VerifyPassword_CaseSensitive_ReturnsFalse()
        {
            var password = "Test@1234";
            var wrongCasePassword = "test@1234";
            var hash = PasswordHasher.HashPassword(password);

            var result = PasswordHasher.VerifyPassword(wrongCasePassword, hash);

            Assert.False(result);
        }

        [Fact]
        public void HashPassword_ComplexPassword_ReturnsValidHash()
        {
            var password = "C0mpl3x!P@ssw0rd#2024";

            var hash = PasswordHasher.HashPassword(password);

            Assert.NotNull(hash);
            Assert.True(PasswordHasher.VerifyPassword(password, hash));
        }

        [Fact]
        public void HashPassword_MinimumLength_ReturnsValidHash()
        {
            var password = "Test@123";

            var hash = PasswordHasher.HashPassword(password);

            Assert.NotNull(hash);
            Assert.True(PasswordHasher.VerifyPassword(password, hash));
        }

        [Fact]
        public void HashPassword_LongPassword_ReturnsValidHash()
        {
            var password = "ThisIsAVeryLongPasswordWithManyCharacters@1234567890!";

            var hash = PasswordHasher.HashPassword(password);

            Assert.NotNull(hash);
            Assert.True(PasswordHasher.VerifyPassword(password, hash));
        }

        [Fact]
        public void HashPassword_SpecialCharacters_ReturnsValidHash()
        {
            var password = "P@$$w0rd!#%&*()_+-=[]{}|;:',.<>?/~`";

            var hash = PasswordHasher.HashPassword(password);

            Assert.NotNull(hash);
            Assert.True(PasswordHasher.VerifyPassword(password, hash));
        }

        [Fact]
        public void HashPassword_UnicodeCharacters_ReturnsValidHash()
        {
            var password = "Pässwörd@123";

            var hash = PasswordHasher.HashPassword(password);

            Assert.NotNull(hash);
            Assert.True(PasswordHasher.VerifyPassword(password, hash));
        }

        [Fact]
        public void VerifyPassword_InvalidHash_ReturnsFalse()
        {
            var password = "Test@1234";
            var invalidHash = "invalid-hash-string";

            var exception = Record.Exception(() => PasswordHasher.VerifyPassword(password, invalidHash));

            Assert.NotNull(exception);
        }
    }
}
