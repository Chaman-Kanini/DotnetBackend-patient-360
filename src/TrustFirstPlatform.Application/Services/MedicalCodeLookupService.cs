using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TrustFirstPlatform.Application.Models;
using TrustFirstPlatform.Infrastructure.Data;

namespace TrustFirstPlatform.Application.Services
{
    public class MedicalCodeLookupService : IMedicalCodeLookupService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<MedicalCodeLookupService> _logger;

        public MedicalCodeLookupService(
            AppDbContext dbContext,
            ILogger<MedicalCodeLookupService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<string?> GetICD10CodeAsync(string diagnosis)
        {
            var result = await GetICD10CodesAsync(new List<string> { diagnosis });
            return result.TryGetValue(diagnosis, out var code) ? code : null;
        }

        public async Task<string?> GetCPTCodeAsync(string procedure)
        {
            var result = await GetCPTCodesAsync(new List<string> { procedure });
            return result.TryGetValue(procedure, out var code) ? code : null;
        }

        public async Task<Dictionary<string, string>> GetICD10CodesAsync(List<string> diagnoses)
        {
            if (!diagnoses.Any()) return new Dictionary<string, string>();

            var result = new Dictionary<string, string>();

            try
            {
                var allICD10Codes = await _dbContext.ICD10Codes.ToListAsync();

                foreach (var diagnosis in diagnoses)
                {
                    if (string.IsNullOrWhiteSpace(diagnosis))
                        continue;

                    var normalizedDiagnosis = diagnosis.Trim().ToLowerInvariant();

                    // Try exact match first
                    var exactMatch = allICD10Codes.FirstOrDefault(c => 
                        c.Diagnosis.Trim().Equals(normalizedDiagnosis, StringComparison.OrdinalIgnoreCase));

                    if (exactMatch != null)
                    {
                        result[diagnosis] = exactMatch.Code;
                        continue;
                    }

                    // Try partial match (contains)
                    var partialMatch = allICD10Codes.FirstOrDefault(c => 
                        c.Diagnosis.Trim().ToLowerInvariant().Contains(normalizedDiagnosis) ||
                        normalizedDiagnosis.Contains(c.Diagnosis.Trim().ToLowerInvariant()));

                    if (partialMatch != null)
                    {
                        result[diagnosis] = partialMatch.Code;
                        continue;
                    }

                    // Try fuzzy match based on word overlap
                    var diagnosisWords = normalizedDiagnosis.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var bestMatch = allICD10Codes
                        .Select(c => new
                        {
                            Code = c,
                            Score = CalculateWordOverlapScore(diagnosisWords, c.Diagnosis.Trim().ToLowerInvariant())
                        })
                        .Where(x => x.Score > 0.3)
                        .OrderByDescending(x => x.Score)
                        .FirstOrDefault();

                    if (bestMatch != null)
                    {
                        result[diagnosis] = bestMatch.Code.Code;
                    }
                }

                _logger.LogInformation("ICD-10 lookup completed: {MatchCount}/{TotalCount} diagnoses matched", 
                    result.Count, diagnoses.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error looking up ICD-10 codes from database");
            }

            return result;
        }

        public async Task<Dictionary<string, string>> GetCPTCodesAsync(List<string> procedures)
        {
            if (!procedures.Any()) return new Dictionary<string, string>();

            var result = new Dictionary<string, string>();

            try
            {
                var allCPTCodes = await _dbContext.CPTCodes.ToListAsync();

                foreach (var procedure in procedures)
                {
                    if (string.IsNullOrWhiteSpace(procedure))
                        continue;

                    var normalizedProcedure = procedure.Trim().ToLowerInvariant();

                    // Try exact match first
                    var exactMatch = allCPTCodes.FirstOrDefault(c => 
                        c.Procedure.Trim().Equals(normalizedProcedure, StringComparison.OrdinalIgnoreCase));

                    if (exactMatch != null)
                    {
                        result[procedure] = exactMatch.Code;
                        continue;
                    }

                    // Try partial match (contains)
                    var partialMatch = allCPTCodes.FirstOrDefault(c => 
                        c.Procedure.Trim().ToLowerInvariant().Contains(normalizedProcedure) ||
                        normalizedProcedure.Contains(c.Procedure.Trim().ToLowerInvariant()));

                    if (partialMatch != null)
                    {
                        result[procedure] = partialMatch.Code;
                        continue;
                    }

                    // Try fuzzy match based on word overlap
                    var procedureWords = normalizedProcedure.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var bestMatch = allCPTCodes
                        .Select(c => new
                        {
                            Code = c,
                            Score = CalculateWordOverlapScore(procedureWords, c.Procedure.Trim().ToLowerInvariant())
                        })
                        .Where(x => x.Score > 0.3)
                        .OrderByDescending(x => x.Score)
                        .FirstOrDefault();

                    if (bestMatch != null)
                    {
                        result[procedure] = bestMatch.Code.Code;
                    }
                }

                _logger.LogInformation("CPT lookup completed: {MatchCount}/{TotalCount} procedures matched", 
                    result.Count, procedures.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error looking up CPT codes from database");
            }

            return result;
        }

        public async Task<MedicalCodesResult> EnrichConsolidatedDataWithCodesAsync(JsonDocument consolidatedData)
        {
            var result = new MedicalCodesResult();

            try
            {
                // Debug: Log the actual consolidated data structure
                _logger.LogInformation("Consolidated data structure: {ConsolidatedData}", consolidatedData.RootElement.GetRawText());
                
                var diagnoses = ExtractDiagnoses(consolidatedData);
                var procedures = ExtractProcedures(consolidatedData);
                
                // Debug: Log what was extracted
                _logger.LogInformation("Extracted {DiagnosisCount} diagnoses: {Diagnoses}", diagnoses.Count, string.Join(", ", diagnoses));
                _logger.LogInformation("Extracted {ProcedureCount} procedures: {Procedures}", procedures.Count, string.Join(", ", procedures));

                result.TotalDiagnoses = diagnoses.Count;
                result.TotalProcedures = procedures.Count;

                if (diagnoses.Any())
                {
                    var icd10Matches = await GetICD10CodesAsync(diagnoses);
                    var allICD10Codes = await _dbContext.ICD10Codes.ToListAsync();

                    foreach (var match in icd10Matches)
                    {
                        var codeEntity = allICD10Codes.FirstOrDefault(c => c.Code == match.Value);
                        result.ICD10Codes.Add(new ICD10CodeDto
                        {
                            Diagnosis = match.Key,
                            Code = match.Value,
                            Description = codeEntity?.Diagnosis ?? match.Key
                        });
                    }

                    result.DiagnosesMatched = result.ICD10Codes.Count;
                }

                if (procedures.Any())
                {
                    var cptMatches = await GetCPTCodesAsync(procedures);
                    var allCPTCodes = await _dbContext.CPTCodes.ToListAsync();

                    foreach (var match in cptMatches)
                    {
                        var codeEntity = allCPTCodes.FirstOrDefault(c => c.Code == match.Value);
                        result.CPTCodes.Add(new CPTCodeDto
                        {
                            Procedure = match.Key,
                            Code = match.Value,
                            Description = codeEntity?.Procedure ?? match.Key
                        });
                    }

                    result.ProceduresMatched = result.CPTCodes.Count;
                }

                _logger.LogInformation(
                    "Medical code enrichment completed: {ICD10Count}/{TotalDiagnoses} diagnoses, {CPTCount}/{TotalProcedures} procedures",
                    result.DiagnosesMatched, result.TotalDiagnoses, result.ProceduresMatched, result.TotalProcedures);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enriching consolidated data with medical codes");
            }

            return result;
        }

        private List<string> ExtractDiagnoses(JsonDocument consolidatedData)
        {
            var diagnoses = new List<string>();

            try
            {
                var root = consolidatedData.RootElement;

                // Try multiple possible diagnosis field names (case-insensitive)
                var diagnosisFieldNames = new[] { "diagnoses", "Diagnoses", "diagnosis", "Diagnosis", "conditions", "Conditions", "medical_conditions", "MedicalConditions", "health_conditions", "HealthConditions" };
                
                foreach (var fieldName in diagnosisFieldNames)
                {
                    if (root.TryGetProperty(fieldName, out var diagnosesProperty))
                    {
                        if (diagnosesProperty.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var diagnosis in diagnosesProperty.EnumerateArray())
                            {
                                string? diagnosisText = null;

                                if (diagnosis.ValueKind == JsonValueKind.String)
                                {
                                    diagnosisText = diagnosis.GetString();
                                }
                                else if (diagnosis.ValueKind == JsonValueKind.Object)
                                {
                                    // Try multiple possible property names within each object
                                    var propertyNames = new[] { "diagnosis", "Diagnosis", "condition", "Condition", "name", "Name", "description", "Description", "text", "Text", "value", "Value" };
                                    foreach (var propName in propertyNames)
                                    {
                                        if (diagnosis.TryGetProperty(propName, out var prop) && !string.IsNullOrWhiteSpace(prop.GetString()))
                                        {
                                            diagnosisText = prop.GetString();
                                            break;
                                        }
                                    }
                                }

                                if (!string.IsNullOrWhiteSpace(diagnosisText))
                                {
                                    diagnoses.Add(diagnosisText);
                                }
                            }
                        }
                        else if (diagnosesProperty.ValueKind == JsonValueKind.String)
                        {
                            // Single diagnosis as string
                            var diagnosisText = diagnosesProperty.GetString();
                            if (!string.IsNullOrWhiteSpace(diagnosisText))
                            {
                                diagnoses.Add(diagnosisText);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting diagnoses from consolidated data");
            }

            return diagnoses.Distinct().ToList();
        }

        private List<string> ExtractProcedures(JsonDocument consolidatedData)
        {
            var procedures = new List<string>();

            try
            {
                var root = consolidatedData.RootElement;

                // Try multiple possible procedure field names (case-insensitive)
                var procedureFieldNames = new[] { "procedures", "Procedures", "procedure", "Procedure", "treatments", "Treatments", "interventions", "Interventions", "operations", "Operations", "tests", "Tests" };
                
                foreach (var fieldName in procedureFieldNames)
                {
                    if (root.TryGetProperty(fieldName, out var proceduresProperty))
                    {
                        if (proceduresProperty.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var procedure in proceduresProperty.EnumerateArray())
                            {
                                string? procedureText = null;

                                if (procedure.ValueKind == JsonValueKind.String)
                                {
                                    procedureText = procedure.GetString();
                                }
                                else if (procedure.ValueKind == JsonValueKind.Object)
                                {
                                    // Try multiple possible property names within each object
                                    var propertyNames = new[] { "procedure", "Procedure", "name", "Name", "description", "Description", "text", "Text", "value", "Value", "type", "Type" };
                                    foreach (var propName in propertyNames)
                                    {
                                        if (procedure.TryGetProperty(propName, out var prop) && !string.IsNullOrWhiteSpace(prop.GetString()))
                                        {
                                            procedureText = prop.GetString();
                                            break;
                                        }
                                    }
                                }

                                if (!string.IsNullOrWhiteSpace(procedureText))
                                {
                                    procedures.Add(procedureText);
                                }
                            }
                        }
                        else if (proceduresProperty.ValueKind == JsonValueKind.String)
                        {
                            // Single procedure as string
                            var procedureText = proceduresProperty.GetString();
                            if (!string.IsNullOrWhiteSpace(procedureText))
                            {
                                procedures.Add(procedureText);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting procedures from consolidated data");
            }

            return procedures.Distinct().ToList();
        }

        private double CalculateWordOverlapScore(string[] words1, string text2)
        {
            var words2 = text2.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            if (words1.Length == 0 || words2.Length == 0)
                return 0;

            var matchCount = words1.Count(w1 => words2.Any(w2 => w2.Contains(w1) || w1.Contains(w2)));
            return (double)matchCount / Math.Max(words1.Length, words2.Length);
        }
    }
}
