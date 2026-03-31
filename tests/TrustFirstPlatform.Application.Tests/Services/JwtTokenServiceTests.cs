using Microsoft.Extensions.Configuration;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TrustFirstPlatform.Application.Services;

namespace TrustFirstPlatform.Application.Tests.Services
{
    public class JwtTokenServiceTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly JwtTokenService _jwtTokenService;
        private readonly string _testSecretKey = "test-secret-key-minimum-32-characters-long-for-security";
        private readonly string _testIssuer = "test-issuer";
        private readonly string _testAudience = "test-audience";

        public JwtTokenServiceTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockConfiguration.Setup(x => x["JwtSettings:SecretKey"]).Returns(_testSecretKey);
            _mockConfiguration.Setup(x => x["JwtSettings:Issuer"]).Returns(_testIssuer);
            _mockConfiguration.Setup(x => x["JwtSettings:Audience"]).Returns(_testAudience);
            _mockConfiguration.Setup(x => x["JwtSettings:ExpirationMinutes"]).Returns("60");

            _jwtTokenService = new JwtTokenService(_mockConfiguration.Object);
        }

        [Fact]
        public void GenerateToken_ValidUser_ReturnsJwtToken()
        {
            var userId = Guid.NewGuid();
            var email = "user@test.com";
            var role = "StandardUser";

            var token = _jwtTokenService.GenerateToken(userId, email, role);

            Assert.NotNull(token);
            Assert.NotEmpty(token);
            Assert.Contains(".", token);
        }

        [Fact]
        public void GenerateToken_ValidUser_ContainsRequiredClaims()
        {
            var userId = Guid.NewGuid();
            var email = "user@test.com";
            var role = "StandardUser";

            var token = _jwtTokenService.GenerateToken(userId, email, role);

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            Assert.Contains(jwtToken.Claims, c => c.Type == "nameid" && c.Value == userId.ToString());
            Assert.Contains(jwtToken.Claims, c => c.Type == "email" && c.Value == email);
            Assert.Contains(jwtToken.Claims, c => c.Type == "role" && c.Value == role);
            Assert.Contains(jwtToken.Claims, c => c.Type == JwtRegisteredClaimNames.Jti);
            Assert.Contains(jwtToken.Claims, c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == userId.ToString());
            Assert.Contains(jwtToken.Claims, c => c.Type == JwtRegisteredClaimNames.Email && c.Value == email);
        }

        [Fact]
        public void GenerateToken_ValidUser_HasCorrectIssuerAndAudience()
        {
            var userId = Guid.NewGuid();
            var email = "user@test.com";
            var role = "StandardUser";

            var token = _jwtTokenService.GenerateToken(userId, email, role);

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            Assert.Equal(_testIssuer, jwtToken.Issuer);
            Assert.Contains(_testAudience, jwtToken.Audiences);
        }

        [Fact]
        public void GenerateToken_ValidUser_HasCorrectExpiration()
        {
            var userId = Guid.NewGuid();
            var email = "user@test.com";
            var role = "StandardUser";

            var beforeGeneration = DateTime.UtcNow;
            var token = _jwtTokenService.GenerateToken(userId, email, role);
            var afterGeneration = DateTime.UtcNow;

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            Assert.True(jwtToken.ValidTo > beforeGeneration.AddMinutes(59));
            Assert.True(jwtToken.ValidTo < afterGeneration.AddMinutes(61));
        }

        [Fact]
        public void GenerateToken_AdminUser_ContainsAdminRole()
        {
            var userId = Guid.NewGuid();
            var email = "admin@test.com";
            var role = "Admin";

            var token = _jwtTokenService.GenerateToken(userId, email, role);

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            var roleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "role");
            Assert.NotNull(roleClaim);
            Assert.Equal("Admin", roleClaim.Value);
        }

        [Fact]
        public void GetTokenJti_ValidToken_ReturnsJti()
        {
            var userId = Guid.NewGuid();
            var token = _jwtTokenService.GenerateToken(userId, "user@test.com", "StandardUser");

            var jti = _jwtTokenService.GetTokenJti(token);

            Assert.NotNull(jti);
            Assert.NotEmpty(jti);
            Assert.True(Guid.TryParse(jti, out _));
        }

        [Fact]
        public void GetTokenJti_DifferentTokens_ReturnsDifferentJtis()
        {
            var userId1 = Guid.NewGuid();
            var userId2 = Guid.NewGuid();
            var token1 = _jwtTokenService.GenerateToken(userId1, "user1@test.com", "StandardUser");
            var token2 = _jwtTokenService.GenerateToken(userId2, "user2@test.com", "StandardUser");

            var jti1 = _jwtTokenService.GetTokenJti(token1);
            var jti2 = _jwtTokenService.GetTokenJti(token2);

            Assert.NotEqual(jti1, jti2);
        }

        [Fact]
        public void GetPrincipalFromToken_ValidToken_ReturnsPrincipal()
        {
            var userId = Guid.NewGuid();
            var email = "user@test.com";
            var role = "StandardUser";
            var token = _jwtTokenService.GenerateToken(userId, email, role);

            var principal = _jwtTokenService.GetPrincipalFromToken(token);

            Assert.NotNull(principal);
            Assert.NotNull(principal.Identity);
            Assert.True(principal.Identity.IsAuthenticated);
        }

        [Fact]
        public void GetPrincipalFromToken_ValidToken_ContainsCorrectClaims()
        {
            var userId = Guid.NewGuid();
            var email = "user@test.com";
            var role = "StandardUser";
            var token = _jwtTokenService.GenerateToken(userId, email, role);

            var principal = _jwtTokenService.GetPrincipalFromToken(token);

            Assert.NotNull(principal);
            Assert.Equal(userId.ToString(), principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            Assert.Equal(email, principal.FindFirst(ClaimTypes.Email)?.Value);
            Assert.Equal(role, principal.FindFirst(ClaimTypes.Role)?.Value);
        }

        [Fact]
        public void GetPrincipalFromToken_InvalidToken_ReturnsNull()
        {
            var invalidToken = "invalid.token.here";

            var principal = _jwtTokenService.GetPrincipalFromToken(invalidToken);

            Assert.Null(principal);
        }

        [Fact]
        public void GetPrincipalFromToken_ExpiredToken_ReturnsNull()
        {
            // Manually create an already-expired token to avoid NotBefore > Expires error
            var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(_testSecretKey));
            var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature);

            var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.Email, "user@test.com"),
                    new Claim(ClaimTypes.Role, "StandardUser"),
                }),
                NotBefore = DateTime.UtcNow.AddMinutes(-10),
                Expires = DateTime.UtcNow.AddMinutes(-5),
                SigningCredentials = credentials,
                Issuer = _testIssuer,
                Audience = _testAudience,
            };

            var handler = new JwtSecurityTokenHandler();
            var token = handler.WriteToken(handler.CreateToken(tokenDescriptor));

            var principal = _jwtTokenService.GetPrincipalFromToken(token);

            Assert.Null(principal);
        }

        [Fact]
        public void GetUserIdFromToken_ValidToken_ReturnsUserId()
        {
            var userId = Guid.NewGuid();
            var token = _jwtTokenService.GenerateToken(userId, "user@test.com", "StandardUser");

            var extractedUserId = _jwtTokenService.GetUserIdFromToken(token);

            Assert.NotNull(extractedUserId);
            Assert.Equal(userId, extractedUserId.Value);
        }

        [Fact]
        public void GetUserIdFromToken_InvalidToken_ReturnsNull()
        {
            var invalidToken = "invalid.token.here";

            var userId = _jwtTokenService.GetUserIdFromToken(invalidToken);

            Assert.Null(userId);
        }

        [Fact]
        public void GetUserIdFromToken_TokenWithoutUserIdClaim_ReturnsNull()
        {
            var invalidToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

            var userId = _jwtTokenService.GetUserIdFromToken(invalidToken);

            Assert.Null(userId);
        }
    }
}
