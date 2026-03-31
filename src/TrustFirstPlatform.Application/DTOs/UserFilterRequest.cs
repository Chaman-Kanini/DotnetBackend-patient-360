namespace TrustFirstPlatform.Application.DTOs
{
    public record UserFilterRequest(
        string? SearchTerm,
        string? Role,
        string? Status,
        int Page = 1,
        int PageSize = 20
    );
}
