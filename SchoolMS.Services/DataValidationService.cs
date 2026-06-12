using SchoolMS.Domain;
using System.Text.RegularExpressions;

namespace SchoolMS.Services
{
    public class ValidationError
    {
        public string FieldName { get; set; }
        public string ErrorMessage { get; set; }
        public string? ExtractedValue { get; set; }
        public ValidationErrorLevel Level { get; set; } // Error, Warning, Info
    }

    public enum ValidationErrorLevel
    {
        Error,      // Critical - must fix
        Warning,    // Potential issue - review
        Info        // FYI - no action needed
    }

    public interface IDataValidationService
    {
    }

    public class DataValidationService : IDataValidationService
    {
    }
}
