using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace TrustFirstPlatform.Application.Services
{
    public interface IPythonTextExtractionService
    {
        Task<TextExtractionResult> ExtractTextAsync(string filePath);
    }

    public class PythonTextExtractionService : IPythonTextExtractionService
    {
        private readonly string _pythonPath;
        private readonly string _scriptPath;
        private readonly ILogger<PythonTextExtractionService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public PythonTextExtractionService(IConfiguration configuration, ILogger<PythonTextExtractionService> logger)
        {
            _pythonPath = configuration["Python:Path"] ?? "python";
            _scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "extract_text.py");
            _logger = logger;
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<TextExtractionResult> ExtractTextAsync(string filePath)
        {
            _logger.LogInformation("Starting text extraction for file: {FilePath}", filePath);
            _logger.LogInformation("Python path: {PythonPath}, Script path: {ScriptPath}", _pythonPath, _scriptPath);

            if (!File.Exists(filePath))
            {
                _logger.LogError("File not found at path: {FilePath}", filePath);
                return new TextExtractionResult { Error = "File not found" };
            }

            if (!File.Exists(_scriptPath))
            {
                _logger.LogError("Python script not found at path: {ScriptPath}", _scriptPath);
                return new TextExtractionResult { Error = "Python script not found" };
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = $"\"{_scriptPath}\" \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            _logger.LogInformation("Python process exited with code: {ExitCode}", process.ExitCode);
            
            if (!string.IsNullOrWhiteSpace(error))
            {
                _logger.LogInformation("Python stderr output: {StdErr}", error);
            }

            if (!string.IsNullOrWhiteSpace(output))
            {
                _logger.LogInformation("Python stdout length: {OutputLength} characters", output.Length);
            }
            else
            {
                _logger.LogWarning("Python stdout is empty");
            }

            if (process.ExitCode != 0)
            {
                _logger.LogError("Python script failed with exit code {ExitCode}. Error: {Error}", process.ExitCode, error);
                return new TextExtractionResult { Error = error };
            }

            try
            {
                _logger.LogInformation("Raw JSON output (first 500 chars): {Output}", output.Length > 500 ? output.Substring(0, 500) + "..." : output);
                
                var result = JsonSerializer.Deserialize<TextExtractionResult>(output, _jsonOptions);
                if (result == null)
                {
                    _logger.LogError("Failed to deserialize Python output. Output: {Output}", output);
                    return new TextExtractionResult { Error = "Failed to parse output" };
                }

                _logger.LogInformation("Deserialized result - Text length: {TextLength}, Error: {Error}", 
                    result.Text?.Length ?? 0, result.Error);

                if (!string.IsNullOrWhiteSpace(result.Text))
                {
                    _logger.LogInformation("Successfully extracted {TextLength} characters", result.Text.Length);
                }
                else if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    _logger.LogWarning("Text extraction returned error: {Error}", result.Error);
                }
                else
                {
                    _logger.LogWarning("Text extraction returned empty text with no error");
                }

                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization failed. Output: {Output}", output);
                return new TextExtractionResult { Error = $"Failed to parse output: {ex.Message}" };
            }
        }
    }

    public class TextExtractionResult
    {
        public string? Text { get; set; }
        public string? Error { get; set; }
    }
}
