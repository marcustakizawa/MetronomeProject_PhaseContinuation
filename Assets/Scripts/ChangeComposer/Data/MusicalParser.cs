using System.Text.RegularExpressions;
using UnityEngine;

namespace ChangeComposer.Data {
    /// <summary>
    /// Parser for musical content: tempo and time signature changes
    /// Updated to work with enhanced MetronomeChange boolean properties
    /// </summary>
    public static class MusicalParser {
        // Pattern: M16 T140 TS3/4 N2 "Description" U
        private static readonly Regex musicalPattern = new Regex(
            @"M(?<measure>\d+)\s*" +                    // M16 (required)
            @"(?:T(?<tempo>\d+(?:\.\d+)?))?\s*" +       // T140 (optional)
            @"(?:TS(?<beats>\d+)/(?<unit>\d+))?\s*" +   // TS3/4 (optional)
            @"(?:N(?<notify>\d+))?\s*" +                // N2 (optional)
            @"(?:""(?<desc>[^""]*)"")?\s*" +            // "Description" (optional)
            @"(?<urgent>U)?\s*" +                       // U (optional)
            @"(?://.*)?$",                              // // Comment (optional)
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        /// <summary>
        /// Parse a musical event line
        /// </summary>
        public static ValidationResult ParseMusical(string input) {
            var result = new ValidationResult { originalInput = input.Trim() };

            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(input) || input.TrimStart().StartsWith("//")) {
                result.isValid = true;
                result.AddMessage(MessageSeverity.Info, "Skipped empty line or comment");
                return result;
            }

            var match = musicalPattern.Match(input);
            if (!match.Success) {
                result.AddError("Invalid musical format. Use: M16 T140 TS3/4 N2 \"Description\" U");
                return result;
            }

            try {
                // Parse measure (required)
                int measure = int.Parse(match.Groups["measure"].Value);
                if (measure <= 0) {
                    result.AddError("Measure must be positive");
                    return result;
                }

                // Parse tempo (optional)
                float? tempo = null;
                if (match.Groups["tempo"].Success) {
                    tempo = float.Parse(match.Groups["tempo"].Value);
                    if (tempo <= 0 || tempo > 500) {
                        result.AddMessage(MessageSeverity.Warning,
                            $"Unusual tempo: {tempo} BPM", "Typical range: 60-200 BPM");
                    }
                }

                // Parse time signature (optional)
                int? beats = null, unit = null;
                if (match.Groups["beats"].Success && match.Groups["unit"].Success) {
                    beats = int.Parse(match.Groups["beats"].Value);
                    unit = int.Parse(match.Groups["unit"].Value);

                    if (beats <= 0 || beats > 16) {
                        result.AddMessage(MessageSeverity.Warning,
                            $"Unusual beats per measure: {beats}");
                    }

                    if (unit != 2 && unit != 4 && unit != 8 && unit != 16) {
                        result.AddMessage(MessageSeverity.Warning,
                            $"Unusual beat unit: {unit}", "Common units: 2, 4, 8, 16");
                    }
                }

                // Check that at least tempo or time signature is specified
                if (!tempo.HasValue && !beats.HasValue) {
                    result.AddError("Must specify at least tempo (T) or time signature (TS)");
                    return result;
                }

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

                // Create the MetronomeChange object with proper boolean properties
                MetronomeChange change;

                if (tempo.HasValue && beats.HasValue) {
                    // Both tempo and time signature
                    change = new MetronomeChange(measure, tempo.Value, beats.Value, description);
                    // Properties are automatically set by constructor
                } else if (tempo.HasValue) {
                    // Tempo only
                    change = new MetronomeChange(measure, tempo.Value, description);
                    // hasTempo = true set by constructor
                } else {
                    // Time signature only
                    change = new MetronomeChange(measure, beats.Value, description);
                    // hasTimeSignature = true set by constructor
                }

                // Add notification settings
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
        /// Format a musical change back to text
        /// </summary>
        public static string FormatMusical(MetronomeChange change) {
            if (change == null) return "";

            string text = $"M{change.targetMeasure}";

            if (change.hasTempo) {
                text += $" T{change.newBpm}";
            }

            if (change.hasTimeSignature) {
                text += $" TS{change.newBeatsPerMeasure}/4";
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
        /// Check if input looks like musical content
        /// </summary>
        public static bool IsMusicalContent(string input) {
            if (string.IsNullOrWhiteSpace(input)) return false;
            if (input.TrimStart().StartsWith("//")) return false;

            // Look for tempo or time signature patterns
            return input.Contains("T") || input.Contains("TS");
        }
    }
}