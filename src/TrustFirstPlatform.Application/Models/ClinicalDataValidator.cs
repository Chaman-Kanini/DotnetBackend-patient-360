using System.Text.Json;

namespace TrustFirstPlatform.Application.Models
{
    public class ClinicalDataValidator
    {
        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();

        public List<string> Errors => _errors;
        public List<string> Warnings => _warnings;
        public bool IsValid => !_errors.Any();

        public void ValidateExtractedData(JsonDocument extractedData)
        {
            if (extractedData == null)
            {
                _errors.Add("Extracted data is null");
                return;
            }

            var root = extractedData.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                _errors.Add("Root element must be a JSON object");
                return;
            }

            // Validate structure and _source presence
            ValidateJsonElement(root, "root");
        }

        private void ValidateJsonElement(JsonElement element, string path)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                ValidateObject(element, path);
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                ValidateArray(element, path);
            }
        }

        private void ValidateObject(JsonElement obj, string path)
        {
            // Check if this is a data object (not root) and should have _source
            bool isDataObject = path != "root" && !path.EndsWith("._source");
            bool hasSource = false;

            foreach (var property in obj.EnumerateObject())
            {
                var propertyPath = $"{path}.{property.Name}";

                if (property.Name == "_source")
                {
                    hasSource = true;
                    ValidateSourceField(property.Value, propertyPath);
                }
                else
                {
                    ValidateJsonElement(property.Value, propertyPath);
                }
            }

            // Validate _source presence for data objects
            if (isDataObject && !hasSource && !IsCategoryObject(path))
            {
                _warnings.Add($"Object at '{path}' is missing _source field");
            }
        }

        private void ValidateArray(JsonElement array, string path)
        {
            int index = 0;
            foreach (var item in array.EnumerateArray())
            {
                var itemPath = $"{path}[{index}]";
                
                if (item.ValueKind == JsonValueKind.Object)
                {
                    // Array items should have _source
                    bool hasSource = false;
                    foreach (var property in item.EnumerateObject())
                    {
                        if (property.Name == "_source")
                        {
                            hasSource = true;
                            ValidateSourceField(property.Value, $"{itemPath}._source");
                        }
                        else
                        {
                            ValidateJsonElement(property.Value, $"{itemPath}.{property.Name}");
                        }
                    }

                    if (!hasSource)
                    {
                        _warnings.Add($"Array item at '{itemPath}' is missing _source field");
                    }
                }
                else
                {
                    ValidateJsonElement(item, itemPath);
                }
                
                index++;
            }
        }

        private void ValidateSourceField(JsonElement sourceElement, string path)
        {
            if (sourceElement.ValueKind != JsonValueKind.String)
            {
                _errors.Add($"_source field at '{path}' must be a string");
                return;
            }

            var sourceValue = sourceElement.GetString();
            if (string.IsNullOrWhiteSpace(sourceValue))
            {
                _errors.Add($"_source field at '{path}' cannot be empty");
            }
        }

        private bool IsCategoryObject(string path)
        {
            // Category objects are direct children of root (e.g., root.Medications, root.Patient Demographics)
            var parts = path.Split('.');
            return parts.Length == 2 && parts[0] == "root";
        }
    }
}
