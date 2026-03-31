using System.Collections.Generic;

namespace TrustFirstPlatform.Application.DTOs
{
    public record PagedResult<T>(
        T[] Items,
        int TotalCount,
        int Page,
        int PageSize,
        int TotalPages,
        bool HasNextPage,
        bool HasPreviousPage
    );
}
