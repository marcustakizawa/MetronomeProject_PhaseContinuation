using System.Collections.Generic;
using System.Linq;

namespace ChangeComposer.Data {
    /// <summary>
    /// Validation result for individual changes or entire sequences
    /// Compatible with MusicalParser and ControlParser
    /// </summary>
    [System.Serializable]
    public class ValidationResult {
        public bool isValid;
        public string originalInput;
        public MetronomeChange parsedChange;
        public List<ValidationMessage> messages = new List<ValidationMessage>();

        public bool HasErrors => messages.Any(m => m.severity == MessageSeverity.Error);
        public bool HasWarnings => messages.Any(m => m.severity == MessageSeverity.Warning);

        /// <summary>
        /// Add a validation message with severity
        /// </summary>
        public void AddMessage(MessageSeverity severity, string message) {
            messages.Add(new ValidationMessage(severity, message));

            // Set isValid based on severity
            if (severity == MessageSeverity.Error) {
                isValid = false;
            }
        }

        /// <summary>
        /// Add a validation message with severity and suggestion
        /// </summary>
        public void AddMessage(MessageSeverity severity, string message, string suggestion) {
            messages.Add(new ValidationMessage(severity, message, suggestion));

            // Set isValid based on severity
            if (severity == MessageSeverity.Error) {
                isValid = false;
            }
        }

        /// <summary>
        /// Add an error message (compatibility method)
        /// </summary>
        public void AddError(string message) {
            AddMessage(MessageSeverity.Error, message);
        }

        /// <summary>
        /// Get summary of all error messages
        /// </summary>
        public string GetErrorSummary() {
            var errorMessages = messages
                .Where(m => m.severity == MessageSeverity.Error)
                .Select(m => m.message)
                .ToList();

            if (errorMessages.Count == 0)
                return "No errors";

            return string.Join("; ", errorMessages);
        }

        /// <summary>
        /// Get all messages as formatted text
        /// </summary>
        public string GetAllMessages() {
            if (messages.Count == 0)
                return "No messages";

            return string.Join("\n", messages.Select(m => $"{GetMessageIcon(m.severity)} {m.message}"));
        }

        private string GetMessageIcon(MessageSeverity severity) {
            return severity switch {
                MessageSeverity.Error => "✗",
                MessageSeverity.Warning => "⚠",
                MessageSeverity.Info => "ℹ",
                MessageSeverity.Success => "✓",
                _ => "•"
            };
        }
    }

    /// <summary>
    /// Individual validation message with severity and optional suggestion
    /// </summary>
    [System.Serializable]
    public class ValidationMessage {
        public MessageSeverity severity;
        public string message;
        public string suggestion;

        public ValidationMessage(MessageSeverity severity, string message, string suggestion = "") {
            this.severity = severity;
            this.message = message;
            this.suggestion = suggestion;
        }

        /// <summary>
        /// Get formatted display text for this message
        /// </summary>
        public string GetDisplayText() {
            string icon = severity switch {
                MessageSeverity.Error => "✗",
                MessageSeverity.Warning => "⚠",
                MessageSeverity.Info => "ℹ",
                MessageSeverity.Success => "✓",
                _ => "•"
            };

            string text = $"{icon} {message}";
            if (!string.IsNullOrEmpty(suggestion))
                text += $" (Suggestion: {suggestion})";

            return text;
        }
    }

    /// <summary>
    /// Severity levels for validation messages
    /// </summary>
    public enum MessageSeverity {
        Error,    // Parsing failed, cannot proceed
        Warning,  // Parsed but might be unusual
        Info,     // General information
        Success   // Parsed successfully
    }
}