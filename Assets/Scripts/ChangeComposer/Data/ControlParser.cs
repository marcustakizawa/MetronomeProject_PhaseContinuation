using System.Text.RegularExpressions;
using UnityEngine;

namespace ChangeComposer.Data {
    /// <summary>
    /// Parser for performance control events: mute, visual, stop
    /// Updated to work with enhanced MetronomeChange boolean properties
    /// </summary>
    public static class ControlParser {
        // Pattern: M16 mute/unmute/visual:on/visual:off/stop N2 "Description" U
        private static readonly Regex controlPattern = new Regex(
            @"M(?<measure>\d+)\s+" +                    // M16 (required)
            @"(?<event>\w+(?::\w+)?)\s*" +              // mute, visual:on, stop, etc.
            @"(?:N(?<notify>\d+))?\s*" +                // N2 (optional)
            @"(?:""(?<desc>[^""]*)"")?\s*" +            // "Description" (optional)
            @"(?<urgent>U)?\s*" +                       // U (optional)
            @"(?://.*)?$",                              // // Comment (optional)
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        /// <summary>
        /// Parse a control event line
        /// </summary>
        public static ValidationResult ParseControl(string input) {
            var result = new ValidationResult { originalInput = input.Trim() };

            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(input) || input.TrimStart().StartsWith("//")) {
                result.isValid = true;
                result.AddMessage(MessageSeverity.Info, "Skipped empty line or comment");
                return result;
            }

            var match = controlPattern.Match(input);
            if (!match.Success) {
                result.AddError("Invalid control format. Use: M16 mute/unmute/visual:on/visual:off/stop \"Description\"");
                return result;
            }

            try {
                // Parse measure (required)
                int measure = int.Parse(match.Groups["measure"].Value);
                if (measure <= 0) {
                    result.AddError("Measure must be positive");
                    return result;
                }

                // Parse event type (required)
                string eventType = match.Groups["event"].Value.ToLower();

                // Parse notification advance (optional)
                int notifyAdvance = 1;
                if (match.Groups["notify"].Success) {
                    notifyAdvance = int.Parse(match.Groups["notify"].Value);
                    if (notifyAdvance < 0 || notifyAdvance > 10) {
                        result.AddMessage(MessageSeverity.Warning,
                            $"Unusual notification advance: {notifyAdvance} measures");
                    }
                }

                // Parse description (optional)
                string description = match.Groups["desc"].Success ?
                    match.Groups["desc"].Value : "";

                // Parse urgent flag (optional)
                bool isUrgent = match.Groups["urgent"].Success;

                // Create the MetronomeChange based on event type
                MetronomeChange change = CreateControlChange(measure, eventType, description);

                if (change == null) {
                    result.AddError($"Unknown control event: {eventType}");
                    return result;
                }

                // Add notification settings if specified
                if (notifyAdvance > 1 || isUrgent) {
                    change.WithNotification(notifyAdvance, isUrgent, "");
                }

                result.parsedChange = change;
                result.isValid = true;
                result.AddMessage(MessageSeverity.Success,
                    $"✓ {change.GetChangeDescription()}");

                return result;
            } catch (System.Exception ex) {
                result.AddError($"Parse error: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Create a control change based on event type
        /// </summary>
        private static MetronomeChange CreateControlChange(int measure, string eventType, string description) {
            switch (eventType) {
                case "mute":
                    return MetronomeChange.CreateAudioEvent(measure, true, description);

                case "unmute":
                    return MetronomeChange.CreateAudioEvent(measure, false, description);

                case "visual:off":
                case "visualoff":
                case "hide":
                    return MetronomeChange.CreateVisualEvent(measure, true, description);

                case "visual:on":
                case "visualon":
                case "show":
                    return MetronomeChange.CreateVisualEvent(measure, false, description);

                case "stop":
                case "end":
                    return MetronomeChange.CreateStopEvent(measure, description);

                default:
                    return null; // Unknown event type
            }
        }

        /// <summary>
        /// Format a control change back to text
        /// </summary>
        public static string FormatControl(MetronomeChange change) {
            if (change == null) return "";

            string text = $"M{change.targetMeasure} ";

            // Add event type based on change properties
            switch (change.type) {
                case MetronomeChange.ChangeType.Mute:
                    text += "mute";
                    break;
                case MetronomeChange.ChangeType.Unmute:
                    text += "unmute";
                    break;
                case MetronomeChange.ChangeType.VisualOff:
                    text += "visual:off";
                    break;
                case MetronomeChange.ChangeType.VisualOn:
                    text += "visual:on";
                    break;
                case MetronomeChange.ChangeType.Stop:
                    text += "stop";
                    break;
                case MetronomeChange.ChangeType.Combined:
                    text += GetCombinedEventText(change);
                    break;
                default:
                    text += "unknown";
                    break;
            }

            if (change.enableNotifications && change.advanceNotificationMeasures > 1) {
                text += $" N{change.advanceNotificationMeasures}";
            }

            if (!string.IsNullOrEmpty(change.description)) {
                text += $" \"{change.description}\"";
            }

            if (change.isUrgent) {
                text += " U";
            }

            return text;
        }

        /// <summary>
        /// Get event text for combined changes
        /// </summary>
        private static string GetCombinedEventText(MetronomeChange change) {
            var events = new System.Collections.Generic.List<string>();

            if (change.hasAudioEvent) {
                events.Add(change.muteAudio ? "mute" : "unmute");
            }

            if (change.hasVisualEvent) {
                events.Add(change.hideVisual ? "visual:off" : "visual:on");
            }

            return events.Count > 0 ? string.Join("+", events) : "combined";
        }

        /// <summary>
        /// Check if input looks like control content
        /// </summary>
        public static bool IsControlContent(string input) {
            if (string.IsNullOrWhiteSpace(input)) return false;
            if (input.TrimStart().StartsWith("//")) return false;

            // Look for control event patterns
            string lower = input.ToLower();
            return lower.Contains("mute") ||
                   lower.Contains("unmute") ||
                   lower.Contains("visual") ||
                   lower.Contains("stop") ||
                   lower.Contains("hide") ||
                   lower.Contains("show");
        }

        /// <summary>
        /// Get supported control events for help/autocomplete
        /// </summary>
        public static string[] GetSupportedEvents() {
            return new string[]
            {
                "mute - Mute audio output",
                "unmute - Restore audio output",
                "visual:off - Hide visual indicators",
                "visual:on - Show visual indicators",
                "stop - End of piece/section"
            };
        }
    }
}