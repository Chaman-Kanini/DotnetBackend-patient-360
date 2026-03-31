using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TrustFirstPlatform.Application.DTOs;
using TrustFirstPlatform.Domain.Entities;
using TrustFirstPlatform.Infrastructure.Data;

namespace TrustFirstPlatform.Application.Services
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AdminDashboardService> _logger;

        private static readonly Dictionary<string, string> ActionCategoryMap = new()
        {
            { "LOGIN_SUCCESS", "Auth" },
            { "LOGIN_FAILED", "Auth" },
            { "ACCOUNT_LOCKED", "Auth" },
            { "LOGOUT", "Auth" },
            { "USER_CREATED", "User Management" },
            { "USER_UPDATED", "User Management" },
            { "USER_DEACTIVATED", "User Management" },
            { "USER_REACTIVATED", "User Management" },
            { "USER_APPROVED", "User Management" },
            { "USER_REJECTED", "User Management" },
            { "USER_ROLE_CHANGED", "User Management" },
            { "USER_REGISTERED", "User Management" },
            { "PASSWORD_RESET_REQUESTED", "Password" },
            { "PASSWORD_RESET_COMPLETED", "Password" },
            { "DOCUMENT_UPLOAD_SUCCESS", "Documents" },
            { "DOCUMENT_UPLOAD_FAILED", "Documents" },
            { "DOCUMENT_VALIDATION_REJECTED", "Documents" },
            { "DOCUMENT_STORAGE_FAILED", "Documents" },
            { "DOCUMENT_DOWNLOADED", "Documents" },
            { "DOCUMENT_DOWNLOAD_FAILED", "Documents" },
            { "DOCUMENT_DELETED", "Documents" },
            { "DOCUMENT_DELETE_FAILED", "Documents" },
            { "DOCUMENT_LIST_VIEWED", "Documents" },
            { "DOCUMENT_PROCESSING_STARTED", "Processing" },
            { "DOCUMENT_PROCESSING_COMPLETED", "Processing" },
            { "DOCUMENT_PROCESSING_FAILED", "Processing" },
            { "CONSOLIDATION_COMPLETED", "Processing" },
            { "CONSOLIDATION_FAILED", "Processing" },
            { "BATCH_CONSOLIDATION_COMPLETED", "Processing" },
            { "BATCH_CONSOLIDATION_FAILED", "Processing" },
            { "VIEW_PATIENT", "Patient" },
            { "CHATBOT_QUERY", "Chatbot" },
            { "AUDIT_LOG_VIEWED", "Admin" },
            { "AUDIT_LOG_EXPORTED", "Admin" },
        };

        public AdminDashboardService(AppDbContext context, ILogger<AdminDashboardService> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Audit Logs

        public async Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(AuditLogFilterRequest filter)
        {
            var query = BuildAuditLogQuery(filter.SearchTerm, filter.Action, filter.UserId,
                filter.StartDate, filter.EndDate, filter.IpAddress);

            var totalCount = await query.CountAsync();

            query = filter.SortBy?.ToLower() switch
            {
                "action" => filter.SortDirection?.ToLower() == "asc"
                    ? query.OrderBy(a => a.Action)
                    : query.OrderByDescending(a => a.Action),
                "ipaddress" => filter.SortDirection?.ToLower() == "asc"
                    ? query.OrderBy(a => a.IpAddress)
                    : query.OrderByDescending(a => a.IpAddress),
                _ => filter.SortDirection?.ToLower() == "asc"
                    ? query.OrderBy(a => a.OccurredAt)
                    : query.OrderByDescending(a => a.OccurredAt),
            };

            var rawLogs = await query
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(a => new
                {
                    a.Id,
                    a.UserId,
                    UserEmail = a.User != null ? a.User.Email : string.Empty,
                    UserName = a.User != null
                        ? ((a.User.FirstName ?? "") + " " + (a.User.LastName ?? "")).Trim()
                        : string.Empty,
                    a.Action,
                    a.OccurredAt,
                    a.IpAddress,
                    a.Metadata
                })
                .ToArrayAsync();

            var logs = rawLogs.Select(a => new AuditLogDto
            {
                Id = a.Id,
                UserId = a.UserId,
                UserEmail = a.UserEmail,
                UserName = a.UserName,
                Action = a.Action,
                OccurredAt = a.OccurredAt,
                IpAddress = a.IpAddress,
                Metadata = a.Metadata != null
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(a.Metadata.RootElement.GetRawText())
                    : null
            }).ToArray();

            var totalPages = (int)Math.Ceiling((double)totalCount / filter.PageSize);

            return new PagedResult<AuditLogDto>(
                logs, totalCount, filter.Page, filter.PageSize, totalPages,
                filter.Page < totalPages, filter.Page > 1);
        }

        public async Task<List<AuditLogDto>> ExportAuditLogsAsync(AuditLogExportRequest filter)
        {
            var query = BuildAuditLogQuery(filter.SearchTerm, filter.Action, filter.UserId,
                filter.StartDate, filter.EndDate, filter.IpAddress);

            var rawLogs = await query
                .OrderByDescending(a => a.OccurredAt)
                .Take(10000)
                .Select(a => new
                {
                    a.Id,
                    a.UserId,
                    UserEmail = a.User != null ? a.User.Email : string.Empty,
                    UserName = a.User != null
                        ? ((a.User.FirstName ?? "") + " " + (a.User.LastName ?? "")).Trim()
                        : string.Empty,
                    a.Action,
                    a.OccurredAt,
                    a.IpAddress,
                    a.Metadata
                })
                .ToListAsync();

            return rawLogs.Select(a => new AuditLogDto
            {
                Id = a.Id,
                UserId = a.UserId,
                UserEmail = a.UserEmail,
                UserName = a.UserName,
                Action = a.Action,
                OccurredAt = a.OccurredAt,
                IpAddress = a.IpAddress,
                Metadata = a.Metadata != null
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(a.Metadata.RootElement.GetRawText())
                    : null
            }).ToList();
        }

        public async Task<List<string>> GetDistinctActionsAsync()
        {
            return await _context.AuditLogs
                .Select(a => a.Action)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();
        }

        private IQueryable<AuditLog> BuildAuditLogQuery(
            string? searchTerm, string? action, Guid? userId,
            DateTime? startDate, DateTime? endDate, string? ipAddress)
        {
            var query = _context.AuditLogs
                .Include(a => a.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(a =>
                    a.Action.Contains(searchTerm) ||
                    a.IpAddress.Contains(searchTerm) ||
                    (a.User != null && a.User.Email.Contains(searchTerm)) ||
                    (a.User != null && a.User.FirstName != null && a.User.FirstName.Contains(searchTerm)) ||
                    (a.User != null && a.User.LastName != null && a.User.LastName.Contains(searchTerm)));
            }

            if (!string.IsNullOrWhiteSpace(action))
            {
                query = query.Where(a => a.Action == action);
            }

            if (userId.HasValue)
            {
                query = query.Where(a => a.UserId == userId.Value);
            }

            if (startDate.HasValue)
            {
                query = query.Where(a => a.OccurredAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(a => a.OccurredAt <= endDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(ipAddress))
            {
                query = query.Where(a => a.IpAddress.Contains(ipAddress));
            }

            return query;
        }

        #endregion

        #region Dashboard Summary

        public async Task<DashboardSummaryDto> GetDashboardSummaryAsync()
        {
            var now = DateTime.UtcNow;
            var todayStart = now.Date;
            var weekStart = todayStart.AddDays(-(int)todayStart.DayOfWeek);
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var totalUsers = await _context.Users.CountAsync();
            var activeUsers = await _context.Users.CountAsync(u => u.IsActive && u.Status == "Active");
            var inactiveUsers = await _context.Users.CountAsync(u => !u.IsActive || u.Status == "Inactive");
            var pendingUsers = await _context.Users.CountAsync(u => u.Status == "Pending");

            var totalDocuments = await _context.ClinicalDocuments.CountAsync();
            var documentsToday = await _context.ClinicalDocuments.CountAsync(d => d.UploadedAt >= todayStart);
            var documentsThisWeek = await _context.ClinicalDocuments.CountAsync(d => d.UploadedAt >= weekStart);
            var documentsThisMonth = await _context.ClinicalDocuments.CountAsync(d => d.UploadedAt >= monthStart);

            var totalPatientContexts = await _context.PatientContexts.CountAsync();
            var patientsWithConflicts = await _context.PatientContexts.CountAsync(p => p.HasConflicts);
            var totalConflicts = await _context.PatientContexts.SumAsync(p => p.ConflictCount);

            var activeSessions = await _context.UserSessions
                .CountAsync(s => !s.IsRevoked && s.ExpiresAt > now);

            var totalChatbotQueries = await _context.ChatHistories.CountAsync();

            var completedDocs = await _context.ClinicalDocuments
                .CountAsync(d => d.Status == DocumentStatus.Completed);
            var processedDocs = await _context.ClinicalDocuments
                .CountAsync(d => d.Status == DocumentStatus.Completed || d.Status == DocumentStatus.Failed);
            var successRate = processedDocs > 0
                ? Math.Round((double)completedDocs / processedDocs * 100, 1)
                : 0;

            var avgDocsPerUser = activeUsers > 0
                ? Math.Round((double)totalDocuments / activeUsers, 1)
                : 0;

            return new DashboardSummaryDto
            {
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                InactiveUsers = inactiveUsers,
                PendingUsers = pendingUsers,
                TotalDocuments = totalDocuments,
                DocumentsToday = documentsToday,
                DocumentsThisWeek = documentsThisWeek,
                DocumentsThisMonth = documentsThisMonth,
                TotalPatientContexts = totalPatientContexts,
                PatientsWithConflicts = patientsWithConflicts,
                TotalConflictsIdentified = totalConflicts,
                ActiveSessions = activeSessions,
                TotalChatbotQueries = totalChatbotQueries,
                DocumentProcessingSuccessRate = successRate,
                AvgDocumentsPerUser = avgDocsPerUser
            };
        }

        #endregion

        #region User Analytics

        public async Task<List<UserGrowthTrendDto>> GetUserGrowthTrendAsync(int days)
        {
            var startDate = DateTime.UtcNow.Date.AddDays(-days);

            var dailyCounts = await _context.Users
                .Where(u => u.CreatedAt >= startDate)
                .GroupBy(u => u.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToListAsync();

            var totalBefore = await _context.Users.CountAsync(u => u.CreatedAt < startDate);

            var result = new List<UserGrowthTrendDto>();
            var cumulative = totalBefore;

            for (var date = startDate; date <= DateTime.UtcNow.Date; date = date.AddDays(1))
            {
                var dayCount = dailyCounts.FirstOrDefault(d => d.Date == date)?.Count ?? 0;
                cumulative += dayCount;
                result.Add(new UserGrowthTrendDto
                {
                    Date = date,
                    NewUsers = dayCount,
                    TotalUsers = cumulative
                });
            }

            return result;
        }

        public async Task<List<UsersByDepartmentDto>> GetUsersByDepartmentAsync()
        {
            return await _context.Users
                .Where(u => u.IsActive)
                .GroupBy(u => u.Department ?? "Unassigned")
                .Select(g => new UsersByDepartmentDto
                {
                    Department = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync();
        }

        public async Task<List<UsersByStatusDto>> GetUsersByStatusAsync()
        {
            return await _context.Users
                .GroupBy(u => u.Status ?? "Unknown")
                .Select(g => new UsersByStatusDto
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync();
        }

        public async Task<List<TopActiveUsersDto>> GetTopActiveUsersAsync(int days, int limit)
        {
            var startDate = DateTime.UtcNow.AddDays(-days);

            var users = await _context.Users
                .Where(u => u.IsActive)
                .Select(u => new TopActiveUsersDto
                {
                    UserId = u.Id,
                    Email = u.Email,
                    Name = ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim(),
                    Department = u.Department ?? "Unassigned",
                    DocumentsUploaded = _context.ClinicalDocuments
                        .Count(d => d.UserId == u.Id && d.UploadedAt >= startDate),
                    PatientsCreated = _context.PatientContexts
                        .Count(p => p.CreatedByUserId == u.Id && p.CreatedAt >= startDate),
                    ChatbotQueries = _context.ChatHistories
                        .Count(c => c.UserId == u.Id && c.Timestamp >= startDate),
                    LastActive = u.LastLoginAt
                })
                .OrderByDescending(u => u.DocumentsUploaded + u.PatientsCreated + u.ChatbotQueries)
                .Take(limit)
                .ToListAsync();

            return users;
        }

        #endregion

        #region Document Analytics

        public async Task<List<DocumentUploadTrendDto>> GetDocumentUploadTrendAsync(int days)
        {
            var startDate = DateTime.UtcNow.Date.AddDays(-days);

            var dailyData = await _context.ClinicalDocuments
                .Where(d => d.UploadedAt >= startDate)
                .GroupBy(d => d.UploadedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Uploaded = g.Count(),
                    Processed = g.Count(d => d.Status == DocumentStatus.Completed),
                    Failed = g.Count(d => d.Status == DocumentStatus.Failed)
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            var result = new List<DocumentUploadTrendDto>();
            for (var date = startDate; date <= DateTime.UtcNow.Date; date = date.AddDays(1))
            {
                var day = dailyData.FirstOrDefault(d => d.Date == date);
                result.Add(new DocumentUploadTrendDto
                {
                    Date = date,
                    Uploaded = day?.Uploaded ?? 0,
                    Processed = day?.Processed ?? 0,
                    Failed = day?.Failed ?? 0
                });
            }

            return result;
        }

        public async Task<List<DocumentStatusDistributionDto>> GetDocumentStatusDistributionAsync()
        {
            var total = await _context.ClinicalDocuments.CountAsync();
            if (total == 0) return new List<DocumentStatusDistributionDto>();

            return await _context.ClinicalDocuments
                .GroupBy(d => d.Status)
                .Select(g => new DocumentStatusDistributionDto
                {
                    Status = g.Key.ToString(),
                    Count = g.Count(),
                    Percentage = Math.Round((double)g.Count() / total * 100, 1)
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync();
        }

        public async Task<List<DocumentTypeDistributionDto>> GetDocumentTypeDistributionAsync()
        {
            return await _context.ClinicalDocuments
                .GroupBy(d => d.FileExtension)
                .Select(g => new DocumentTypeDistributionDto
                {
                    FileExtension = g.Key,
                    Count = g.Count(),
                    TotalSizeBytes = g.Sum(d => d.FileSizeBytes)
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync();
        }

        public async Task<StorageUsageDto> GetStorageUsageAsync()
        {
            var docCount = await _context.ClinicalDocuments.CountAsync();
            var totalSize = docCount > 0
                ? await _context.ClinicalDocuments.SumAsync(d => d.FileSizeBytes)
                : 0;

            return new StorageUsageDto
            {
                TotalSizeBytes = totalSize,
                DocumentCount = docCount,
                AvgFileSizeBytes = docCount > 0 ? Math.Round((double)totalSize / docCount, 0) : 0
            };
        }

        public async Task<List<ProcessingPerformanceDto>> GetProcessingPerformanceAsync(int days)
        {
            var startDate = DateTime.UtcNow.Date.AddDays(-days);

            var rawData = await _context.ClinicalDocuments
                .Where(d => d.ProcessedAt.HasValue && d.UploadedAt >= startDate
                    && d.Status == DocumentStatus.Completed)
                .Select(d => new
                {
                    Date = d.ProcessedAt!.Value.Date,
                    ProcessingTimeMs = (d.ProcessedAt!.Value - d.UploadedAt).TotalMilliseconds
                })
                .ToListAsync();

            var dailyData = rawData
                .GroupBy(d => d.Date)
                .Select(g => new ProcessingPerformanceDto
                {
                    Date = g.Key,
                    AvgProcessingTimeMs = Math.Round(g.Average(d => d.ProcessingTimeMs), 0),
                    DocumentsProcessed = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToList();

            return dailyData;
        }

        public async Task<BatchProcessingSummaryDto> GetBatchProcessingSummaryAsync()
        {
            var totalBatches = await _context.UploadBatches.CountAsync();
            var completedBatches = await _context.UploadBatches
                .CountAsync(b => b.Status == "Completed");
            var failedBatches = await _context.UploadBatches
                .CountAsync(b => b.Status == "Failed");

            var avgDocsPerBatch = totalBatches > 0
                ? await _context.UploadBatches.AverageAsync(b => (double)b.TotalDocuments)
                : 0;

            return new BatchProcessingSummaryDto
            {
                TotalBatches = totalBatches,
                CompletedBatches = completedBatches,
                FailedBatches = failedBatches,
                AvgDocsPerBatch = Math.Round(avgDocsPerBatch, 1)
            };
        }

        #endregion

        #region Patient Analytics

        public async Task<List<PatientConflictTrendDto>> GetPatientConflictTrendAsync(int days)
        {
            var startDate = DateTime.UtcNow.Date.AddDays(-days);

            var dailyData = await _context.PatientContexts
                .Where(p => p.CreatedAt >= startDate)
                .GroupBy(p => p.CreatedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    PatientsCreated = g.Count(),
                    ConflictsDetected = g.Sum(p => p.ConflictCount)
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            var result = new List<PatientConflictTrendDto>();
            for (var date = startDate; date <= DateTime.UtcNow.Date; date = date.AddDays(1))
            {
                var day = dailyData.FirstOrDefault(d => d.Date == date);
                result.Add(new PatientConflictTrendDto
                {
                    Date = date,
                    PatientsCreated = day?.PatientsCreated ?? 0,
                    ConflictsDetected = day?.ConflictsDetected ?? 0
                });
            }

            return result;
        }

        #endregion

        #region Chatbot Analytics

        public async Task<List<ChatbotUsageTrendDto>> GetChatbotUsageTrendAsync(int days)
        {
            var startDate = DateTime.UtcNow.Date.AddDays(-days);

            var dailyData = await _context.ChatHistories
                .Where(c => c.Timestamp >= startDate)
                .GroupBy(c => c.Timestamp.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    QueriesCount = g.Count(),
                    UniqueUsers = g.Select(c => c.UserId).Distinct().Count()
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            var result = new List<ChatbotUsageTrendDto>();
            for (var date = startDate; date <= DateTime.UtcNow.Date; date = date.AddDays(1))
            {
                var day = dailyData.FirstOrDefault(d => d.Date == date);
                result.Add(new ChatbotUsageTrendDto
                {
                    Date = date,
                    QueriesCount = day?.QueriesCount ?? 0,
                    UniqueUsers = day?.UniqueUsers ?? 0
                });
            }

            return result;
        }

        #endregion

        #region Security Analytics

        public async Task<List<LoginActivityTrendDto>> GetLoginActivityTrendAsync(int days)
        {
            var startDate = DateTime.UtcNow.Date.AddDays(-days);

            var dailyData = await _context.AuditLogs
                .Where(a => a.OccurredAt >= startDate &&
                    (a.Action == "LOGIN_SUCCESS" || a.Action == "LOGIN_FAILED" || a.Action == "ACCOUNT_LOCKED"))
                .GroupBy(a => a.OccurredAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    SuccessfulLogins = g.Count(a => a.Action == "LOGIN_SUCCESS"),
                    FailedLogins = g.Count(a => a.Action == "LOGIN_FAILED"),
                    AccountLockouts = g.Count(a => a.Action == "ACCOUNT_LOCKED")
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            var result = new List<LoginActivityTrendDto>();
            for (var date = startDate; date <= DateTime.UtcNow.Date; date = date.AddDays(1))
            {
                var day = dailyData.FirstOrDefault(d => d.Date == date);
                result.Add(new LoginActivityTrendDto
                {
                    Date = date,
                    SuccessfulLogins = day?.SuccessfulLogins ?? 0,
                    FailedLogins = day?.FailedLogins ?? 0,
                    AccountLockouts = day?.AccountLockouts ?? 0
                });
            }

            return result;
        }

        public async Task<List<SecurityEventDto>> GetSecurityEventTrendAsync(int days)
        {
            var startDate = DateTime.UtcNow.Date.AddDays(-days);
            var securityActions = new[]
            {
                "LOGIN_FAILED", "ACCOUNT_LOCKED",
                "PASSWORD_RESET_REQUESTED", "PASSWORD_RESET_COMPLETED",
                "USER_DEACTIVATED"
            };

            var dailyData = await _context.AuditLogs
                .Where(a => a.OccurredAt >= startDate && securityActions.Contains(a.Action))
                .GroupBy(a => a.OccurredAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    FailedLogins = g.Count(a => a.Action == "LOGIN_FAILED"),
                    AccountLockouts = g.Count(a => a.Action == "ACCOUNT_LOCKED"),
                    PasswordResets = g.Count(a => a.Action == "PASSWORD_RESET_REQUESTED" || a.Action == "PASSWORD_RESET_COMPLETED"),
                    UserDeactivations = g.Count(a => a.Action == "USER_DEACTIVATED")
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            var result = new List<SecurityEventDto>();
            for (var date = startDate; date <= DateTime.UtcNow.Date; date = date.AddDays(1))
            {
                var day = dailyData.FirstOrDefault(d => d.Date == date);
                result.Add(new SecurityEventDto
                {
                    Date = date,
                    FailedLogins = day?.FailedLogins ?? 0,
                    AccountLockouts = day?.AccountLockouts ?? 0,
                    PasswordResets = day?.PasswordResets ?? 0,
                    UserDeactivations = day?.UserDeactivations ?? 0
                });
            }

            return result;
        }

        public async Task<List<HourlyActivityDto>> GetHourlyActivityAsync(int days)
        {
            var startDate = DateTime.UtcNow.AddDays(-days);

            var hourlyData = await _context.AuditLogs
                .Where(a => a.OccurredAt >= startDate)
                .GroupBy(a => a.OccurredAt.Hour)
                .Select(g => new HourlyActivityDto
                {
                    Hour = g.Key,
                    ActionCount = g.Count()
                })
                .OrderBy(x => x.Hour)
                .ToListAsync();

            var result = new List<HourlyActivityDto>();
            for (var hour = 0; hour < 24; hour++)
            {
                var existing = hourlyData.FirstOrDefault(h => h.Hour == hour);
                result.Add(new HourlyActivityDto
                {
                    Hour = hour,
                    ActionCount = existing?.ActionCount ?? 0
                });
            }

            return result;
        }

        public async Task<List<AuditActionBreakdownDto>> GetAuditActionBreakdownAsync(int days)
        {
            var startDate = DateTime.UtcNow.AddDays(-days);

            var actionCounts = await _context.AuditLogs
                .Where(a => a.OccurredAt >= startDate)
                .GroupBy(a => a.Action)
                .Select(g => new { Action = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            return actionCounts.Select(a => new AuditActionBreakdownDto
            {
                Category = ActionCategoryMap.GetValueOrDefault(a.Action, "Other"),
                Action = a.Action,
                Count = a.Count
            }).ToList();
        }

        #endregion
    }
}
