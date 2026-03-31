using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;

namespace TrustFirstPlatform.Application.Services
{
    public class FileValidationService : IFileValidationService
    {
        private const long MaxFileSizeBytes = 52_428_800; // 50MB
        private readonly HashSet<string> _allowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx"
        };

        private readonly Dictionary<string, string> _allowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            { ".pdf", "application/pdf" },
            { ".doc", "application/msword" },
            { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" }
        };

        private readonly Dictionary<string, byte[]> _fileSignatures = new()
        {
            { ".pdf", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D } }, // %PDF-
            { ".doc", new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } }, // Old DOC format
            { ".docx", new byte[] { 0x50, 0x4B, 0x03, 0x04 } } // ZIP-based DOCX
        };

        public async Task<FileValidationResult> ValidateFileAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "File is empty",
                    ErrorType = ValidationErrorType.EmptyFile
                };
            }

            // Check file size
            if (!IsWithinSizeLimit(file.Length))
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "File size exceeds 50MB limit",
                    ErrorType = ValidationErrorType.FileTooLarge
                };
            }

            // Check file extension
            var extension = Path.GetExtension(file.FileName);
            if (!IsAllowedExtension(extension))
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "File type not allowed. Only PDF, DOC, and DOCX files are accepted",
                    ErrorType = ValidationErrorType.InvalidExtension
                };
            }

            // Check MIME type
            if (!IsValidMimeType(file.ContentType, extension))
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "File content type does not match extension",
                    ErrorType = ValidationErrorType.InvalidMimeType
                };
            }

            // Check file signature
            using var stream = file.OpenReadStream();
            if (!await IsValidFileSignatureAsync(stream, extension))
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "File signature does not match expected format",
                    ErrorType = ValidationErrorType.InvalidMimeType
                };
            }

            // Reset stream position for further checks
            stream.Position = 0;

            // Check for password protection (PDF only)
            if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                if (await IsPasswordProtectedPdfAsync(stream))
                {
                    return new FileValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Password-protected PDFs are not allowed",
                        ErrorType = ValidationErrorType.PasswordProtected
                    };
                }
                stream.Position = 0;
            }

            // Check for file corruption
            if (await IsCorruptedFileAsync(stream, file.ContentType))
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "File appears to be corrupted or incomplete",
                    ErrorType = ValidationErrorType.CorruptedFile
                };
            }

            return new FileValidationResult { IsValid = true };
        }

        public bool IsAllowedExtension(string extension)
        {
            return !string.IsNullOrEmpty(extension) && _allowedExtensions.Contains(extension);
        }

        public bool IsWithinSizeLimit(long fileSize)
        {
            return fileSize > 0 && fileSize <= MaxFileSizeBytes;
        }

        public async Task<bool> IsPasswordProtectedPdfAsync(Stream fileStream)
        {
            try
            {
                using var reader = new StreamReader(fileStream, Encoding.Default, leaveOpen: true);
                var content = await reader.ReadToEndAsync();
                fileStream.Position = 0;

                // Look for encryption dictionary in PDF
                return content.Contains("/Encrypt") || content.Contains("/Standard");
            }
            catch
            {
                return true; // Assume password-protected if we can't read it
            }
        }

        public async Task<bool> IsCorruptedFileAsync(Stream fileStream, string contentType)
        {
            try
            {
                fileStream.Position = 0;
                
                if (contentType == "application/pdf")
                {
                    // Basic PDF structure validation
                    using var reader = new StreamReader(fileStream, Encoding.ASCII, leaveOpen: true);
                    var header = await reader.ReadLineAsync();
                    fileStream.Position = 0;
                    
                    return !header?.StartsWith("%PDF-") ?? true;
                }
                else if (contentType == "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
                {
                    // Basic DOCX validation - check if it's a valid ZIP
                    fileStream.Position = 0;
                    var signature = new byte[4];
                    await fileStream.ReadAsync(signature, 0, 4);
                    fileStream.Position = 0;
                    
                    return signature[0] != 0x50 || signature[1] != 0x4B || signature[2] != 0x03 || signature[3] != 0x04;
                }
                else if (contentType == "application/msword")
                {
                    // Basic DOC validation
                    fileStream.Position = 0;
                    var signature = new byte[8];
                    await fileStream.ReadAsync(signature, 0, 8);
                    fileStream.Position = 0;
                    
                    var docSignature = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
                    return !signature.Take(8).SequenceEqual(docSignature);
                }

                return false;
            }
            catch
            {
                return true; // Assume corrupted if we can't validate
            }
        }

        public bool IsValidMimeType(string contentType, string extension)
        {
            if (string.IsNullOrEmpty(contentType) || string.IsNullOrEmpty(extension))
                return false;

            return _allowedMimeTypes.TryGetValue(extension, out var expectedMimeType) &&
                   contentType.Equals(expectedMimeType, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> IsValidFileSignatureAsync(Stream fileStream, string extension)
        {
            if (!_fileSignatures.TryGetValue(extension, out var expectedSignature))
                return false;

            fileStream.Position = 0;
            var actualSignature = new byte[expectedSignature.Length];
            var bytesRead = await fileStream.ReadAsync(actualSignature, 0, actualSignature.Length);
            fileStream.Position = 0;

            return bytesRead == expectedSignature.Length && 
                   actualSignature.SequenceEqual(expectedSignature);
        }
    }
}
