using System;
using System.Threading.Tasks;
using TrustFirstPlatform.Application.DTOs;

namespace TrustFirstPlatform.Application.Services
{
    public interface IUserManagementService
    {
        Task<UserDto> CreateUserAsync(CreateUserRequest request, Guid adminId, string ipAddress);
        Task<UserDto> UpdateUserAsync(Guid userId, UpdateUserRequest request, Guid adminId, string ipAddress);
        Task<bool> DeactivateUserAsync(Guid userId, string reason, Guid adminId, string ipAddress);
        Task<bool> ReactivateUserAsync(Guid userId, Guid adminId, string ipAddress);
        Task<UserDto> ApproveUserAsync(Guid userId, Guid adminId, string ipAddress);
        Task<bool> RejectUserAsync(Guid userId, string reason, Guid adminId, string ipAddress);
        Task<PagedResult<UserDto>> GetUsersAsync(UserFilterRequest filter);
        Task<UserDto?> GetUserByIdAsync(Guid userId);
        Task<bool> UpdateUserRoleAsync(Guid userId, string newRole, Guid adminId, string ipAddress);
        Task<UserDto[]> GetPendingUsersAsync();
    }
}
