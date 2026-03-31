using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustFirstPlatform.Infrastructure.Data;

namespace TrustFirstPlatform.API.Controllers
{
    [ApiController]
    [Route("api/clinical-codes")]
    [Authorize]
    public class ClinicalCodesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ClinicalCodesController> _logger;

        public ClinicalCodesController(AppDbContext context, ILogger<ClinicalCodesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost("lookup")]
        public async Task<IActionResult> LookupCodes([FromBody] ClinicalCodesLookupRequest request)
        {
            try
            {
                var icd10Results = new Dictionary<string, List<object>>();
                var cptResults = new Dictionary<string, List<object>>();

                // Search ICD-10 codes for diagnosis terms
                if (request.DiagnosisTerms?.Any() == true)
                {
                    foreach (var term in request.DiagnosisTerms)
                    {
                        // Extract keywords for better matching
                        var keywords = ExtractKeywords(term);
                        
                        // Try exact match first
                        var allMatches = await _context.ICD10Codes
                            .Where(c => EF.Functions.ILike(c.Diagnosis, $"%{term}%") || 
                                       EF.Functions.ILike(c.Code, $"%{term}%"))
                            .Select(c => new { c.Code, c.Diagnosis })
                            .ToListAsync();
                        
                        // Remove duplicates by code and take top 3
                        var matches = allMatches
                            .GroupBy(c => c.Code)
                            .Select(g => g.First())
                            .Take(3)
                            .ToList();
                        
                        // If no exact matches, try keyword matching
                        if (!matches.Any() && keywords.Any())
                        {
                            var allCodes = await _context.ICD10Codes
                                .Select(c => new { c.Code, c.Diagnosis })
                                .ToListAsync();
                            
                            matches = allCodes
                                .Where(c => keywords.Any(k => 
                                    c.Diagnosis.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                                    c.Code.Contains(k, StringComparison.OrdinalIgnoreCase)))
                                .GroupBy(c => c.Code)
                                .Select(g => g.First())
                                .Take(3)
                                .ToList();
                        }
                        
                        icd10Results[term] = matches.Cast<object>().ToList();
                    }
                }

                // Search CPT codes for procedure terms
                if (request.ProcedureTerms?.Any() == true)
                {
                    foreach (var term in request.ProcedureTerms)
                    {
                        // Extract keywords for better matching
                        var keywords = ExtractKeywords(term);
                        
                        // Try exact match first
                        var allMatches = await _context.CPTCodes
                            .Where(c => EF.Functions.ILike(c.Procedure, $"%{term}%") || 
                                       EF.Functions.ILike(c.Code, $"%{term}%"))
                            .Select(c => new { c.Code, c.Procedure })
                            .ToListAsync();
                        
                        // Remove duplicates by code and take top 3
                        var matches = allMatches
                            .GroupBy(c => c.Code)
                            .Select(g => g.First())
                            .Take(3)
                            .ToList();
                        
                        // If no exact matches, try keyword matching
                        if (!matches.Any() && keywords.Any())
                        {
                            var allCodes = await _context.CPTCodes
                                .Select(c => new { c.Code, c.Procedure })
                                .ToListAsync();
                            
                            matches = allCodes
                                .Where(c => keywords.Any(k => 
                                    c.Procedure.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                                    c.Code.Contains(k, StringComparison.OrdinalIgnoreCase)))
                                .GroupBy(c => c.Code)
                                .Select(g => g.First())
                                .Take(3)
                                .ToList();
                        }
                        
                        cptResults[term] = matches.Cast<object>().ToList();
                    }
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        icd10Codes = icd10Results,
                        cptCodes = cptResults
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error looking up clinical codes");
                return StatusCode(500, new { success = false, message = "An error occurred while looking up codes" });
            }
        }

        private List<string> ExtractKeywords(string term)
        {
            // Remove common words and extract meaningful keywords
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                "with", "without", "the", "a", "an", "and", "or", "of", "in", "on", "at", "to", "for",
                "associated", "primary", "dx", "unspecified", "nos"
            };
            
            // Split by spaces, commas, and parentheses
            var words = term.Split(new[] { ' ', ',', '(', ')', '-' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && !stopWords.Contains(w))
                .Select(w => w.Trim())
                .ToList();
            
            return words;
        }
    }

    public class ClinicalCodesLookupRequest
    {
        public List<string>? DiagnosisTerms { get; set; }
        public List<string>? ProcedureTerms { get; set; }
    }
}