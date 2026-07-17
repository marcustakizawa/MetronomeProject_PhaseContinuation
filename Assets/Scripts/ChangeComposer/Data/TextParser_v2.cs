using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using ChangeComposer.Data;
using ChangeComposer.Coordination;

namespace ChangeComposer.Parsing {

    /// <summary>
    /// TextParser_v2 — Unified text parser for ChangeComposer editor input
    /// Version: 2026-03-31 v3 (Added stopAll SYNC action)
    ///
    /// PURPOSE:
    ///   Parses free-text input from the editor scene into MetronomeChange_v2
    ///   objects (track events) and BeatLevelAlignment objects (SYNC events).
    ///   Replaces the old MusicalParser + ControlParser pair with a single
    ///   unified pass over all lines.
    ///
    /// TRACK EVENT SYNTAX:
    ///   M<measure> [B<beat>] [T<bpm>] [TS<beats>/<unit>] [<control>] ["description"]
    ///
    ///   M5 T110                    → tempo change at M5 B1
    ///   M5 B2 T110                 → tempo change at M5 B2
    ///   M7 B3 T130 TS3/4           → tempo + time sig at M7 B3
    ///   M7 TS3/4                   → time sig only at M7 B1
    ///   M10 MUTE                   → mute at M10 B1
    ///   M10 UNMUTE                 → unmute at M10 B1
    ///   M10 STOP                   → stop at M10 B1
    ///   M10 VISOFF                 → visual off at M10 B1
    ///   M10 VISON                  → visual on at M10 B1
    ///   M5 T110 "Drop to 110"      → with description
    ///   // comment                 → skipped
    ///
    /// SYNC EVENT SYNTAX:
    ///   SYNC T<n> M<m> [B<b>] -> T<n> [T<bpm>] ["description"]   (resetBeat)
    ///   SYNC T<n> M<m> [B<b>] → T<n> [T<bpm>] ["description"]    (Unicode arrow also accepted)
    ///   SYNC T<n> M<m> [B<b>] -> stopAll ["description"]          (stop all metronomes)
    ///
    ///   SYNC T1 M30 B1 -> T2 T120 "Lock track 2 at 120"
    ///   SYNC T1 M30 B1 -> T3                                      (no BPM change, just reset beat)
    ///   SYNC T1 M30 -> T2 T120                                    (B defaults to 1)
    ///   SYNC T1 M11 B1 -> stopAll "Stop all when T1 hits M11"     (coordinator-level stop)
    ///
    /// OUTPUT:
    ///   ParseTrackEvents()  → List of ParsedTrackEvent (one per valid line)
    ///   ParseSyncEvents()   → List of ParsedSyncEvent (one per valid SYNC line)
    ///   ParseAll()          → ParseResult containing both lists + error summary
    ///
    /// ROUND-TRIP:
    ///   FormatChange()      → formats a MetronomeChange_v2 back to syntax string
    ///   FormatAlignment()   → formats a BeatLevelAlignment back to SYNC string
    /// </summary>
    public static class TextParser_v2 {

        // =========================================================
        // RESULT TYPES
        // =========================================================

        public class ParsedTrackEvent {
            public MetronomeChange_v2 change;
            public int lineNumber;
            public string originalLine;
            public bool isValid;
            public string errorMessage;
        }

        public class ParsedSyncEvent {
            public BeatLevelAlignment alignment;
            public int lineNumber;
            public string originalLine;
            public bool isValid;
            public string errorMessage;
        }

        public class ParseResult {
            public List<ParsedTrackEvent> trackEvents = new List<ParsedTrackEvent>();
            public List<ParsedSyncEvent> syncEvents = new List<ParsedSyncEvent>();
            public int successCount;
            public int errorCount;
            public string errorSummary; // first error, for status display
        }

        // =========================================================
        // REGEX PATTERNS
        // =========================================================

        // Track event: M<measure> [B<beat>] [T<bpm>] [TS<n>/<n>] [control] ["desc"]
        private static readonly Regex trackPattern = new Regex(
            @"^\s*M(?<measure>\d+)" +                       // M16 (required)
            @"(?:\s+B(?<beat>\d+))?" +                      // B2 (optional)
            @"(?:\s+T(?<bpm>\d+(?:\.\d+)?))?" +             // T140 (optional)
            @"(?:\s+TS(?<beats>\d+)/(?<unit>\d+))?" +       // TS3/4 (optional)
            @"(?:\s+(?<control>MUTE|UNMUTE|STOP|VISOFF|VISON))?" + // control (optional)
            @"(?:\s+""(?<desc>[^""]*)"")?" +                // "description" (optional)
            @"\s*(?://.*)?$",                               // trailing comment
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        // SYNC event — resetBeat: SYNC T<n> M<m> [B<b>] (->/→) T<n> [T<bpm>] ["desc"]
        private static readonly Regex syncPattern = new Regex(
            @"^\s*SYNC\s+" +
            @"T(?<refTrack>\d+)\s+" +                       // T1 (reference track)
            @"M(?<lockMeasure>\d+)" +                       // M30
            @"(?:\s+B(?<lockBeat>\d+))?" +                  // B1 (optional, defaults to 1)
            @"\s+(?:->|→)\s+" +                             // -> or →
            @"T(?<actionTrack>\d+)" +                       // T2 (action track)
            @"(?:\s+T(?<bpm>\d+(?:\.\d+)?))?" +             // T120 (optional BPM)
            @"(?:\s+""(?<desc>[^""]*)"")?" +                // "description" (optional)
            @"\s*(?://.*)?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        // SYNC event — stopAll: SYNC T<n> M<m> [B<b>] (->/→) stopAll ["desc"]
        // No action track — the coordinator stops all metronomes directly.
        private static readonly Regex syncStopAllPattern = new Regex(
            @"^\s*SYNC\s+" +
            @"T(?<refTrack>\d+)\s+" +                       // T1 (reference track)
            @"M(?<lockMeasure>\d+)" +                       // M11
            @"(?:\s+B(?<lockBeat>\d+))?" +                  // B1 (optional, defaults to 1)
            @"\s+(?:->|→)\s+" +                             // -> or →
            @"stopAll" +                                      // literal keyword
            @"(?:\s+""(?<desc>[^""]*)"")?" +                // "description" (optional)
            @"\s*(?://.*)?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        // =========================================================
        // PUBLIC API
        // =========================================================

        /// <summary>
        /// Parse all lines from a textarea — both track events and SYNC lines
        /// in a single pass. Returns a ParseResult with both lists.
        /// </summary>
        public static ParseResult ParseAll(string text) {
            var result = new ParseResult();

            if (string.IsNullOrWhiteSpace(text))
                return result;

            string[] lines = text.Split('\n');

            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i];
                int lineNumber = i + 1;

                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//"))
                    continue;

                // Route to the correct parser based on line prefix
                if (line.TrimStart().StartsWith("SYNC", StringComparison.OrdinalIgnoreCase)) {
                    var syncEvent = ParseSyncLine(line, lineNumber);
                    result.syncEvents.Add(syncEvent);

                    if (syncEvent.isValid)
                        result.successCount++;
                    else {
                        result.errorCount++;
                        if (result.errorSummary == null)
                            result.errorSummary = $"Line {lineNumber}: {syncEvent.errorMessage}";
                    }
                } else if (line.TrimStart().StartsWith("M", StringComparison.OrdinalIgnoreCase)) {
                    var trackEvent = ParseTrackLine(line, lineNumber);
                    result.trackEvents.Add(trackEvent);

                    if (trackEvent.isValid)
                        result.successCount++;
                    else {
                        result.errorCount++;
                        if (result.errorSummary == null)
                            result.errorSummary = $"Line {lineNumber}: {trackEvent.errorMessage}";
                    }
                } else {
                    // Unrecognised line — report as error
                    result.errorCount++;
                    if (result.errorSummary == null)
                        result.errorSummary = $"Line {lineNumber}: unrecognised syntax — '{line.Trim()}'";
                }
            }

            return result;
        }

        /// <summary>
        /// Parse only track event lines (M... lines). Ignores SYNC lines.
        /// Convenience method for cases where only track events are needed.
        /// </summary>
        public static List<ParsedTrackEvent> ParseTrackEvents(string text) {
            return ParseAll(text).trackEvents;
        }

        /// <summary>
        /// Parse only SYNC lines. Ignores track event lines.
        /// Convenience method for the SYNC textarea.
        /// </summary>
        public static List<ParsedSyncEvent> ParseSyncEvents(string text) {
            return ParseAll(text).syncEvents;
        }

        // =========================================================
        // TRACK EVENT PARSING
        // =========================================================

        private static ParsedTrackEvent ParseTrackLine(string line, int lineNumber) {
            var result = new ParsedTrackEvent {
                lineNumber = lineNumber,
                originalLine = line.Trim()
            };

            var match = trackPattern.Match(line);
            if (!match.Success) {
                result.isValid = false;
                result.errorMessage = $"Invalid track event syntax. Expected: M<n> [B<n>] [T<bpm>] [TS<n>/<n>] [MUTE|UNMUTE|STOP|VISOFF|VISON] [\"desc\"]";
                return result;
            }

            // --- Measure (required) ---
            if (!int.TryParse(match.Groups["measure"].Value, out int measure) || measure <= 0) {
                result.isValid = false;
                result.errorMessage = "Measure number must be a positive integer.";
                return result;
            }

            // --- Beat (optional, defaults to 1) ---
            int beat = 1;
            if (match.Groups["beat"].Success) {
                if (!int.TryParse(match.Groups["beat"].Value, out beat) || beat < 1) {
                    result.isValid = false;
                    result.errorMessage = $"Beat number must be >= 1.";
                    return result;
                }
            }

            // --- BPM (optional) ---
            float bpm = 0f;
            bool hasBpm = match.Groups["bpm"].Success;
            if (hasBpm) {
                if (!float.TryParse(match.Groups["bpm"].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out bpm) || bpm <= 0) {
                    result.isValid = false;
                    result.errorMessage = "BPM must be a positive number.";
                    return result;
                }
            }

            // --- Time signature (optional) ---
            int beatsPerMeasure = 0;
            bool hasTS = match.Groups["beats"].Success;
            if (hasTS) {
                if (!int.TryParse(match.Groups["beats"].Value, out beatsPerMeasure) ||
                    beatsPerMeasure <= 0) {
                    result.isValid = false;
                    result.errorMessage = "Time signature numerator must be a positive integer.";
                    return result;
                }
            }

            // --- Control event (optional) ---
            string control = match.Groups["control"].Success
                ? match.Groups["control"].Value.ToUpperInvariant()
                : null;

            // --- Description (optional) ---
            string desc = match.Groups["desc"].Success
                ? match.Groups["desc"].Value
                : "";

            // --- Validate that at least one action is specified ---
            if (!hasBpm && !hasTS && control == null) {
                result.isValid = false;
                result.errorMessage = $"M{measure}: no action specified. Add T<bpm>, TS<n>/<n>, or a control keyword.";
                return result;
            }

            // --- Build the MetronomeChange_v2 ---
            MetronomeChange_v2 change = BuildTrackChange(
                measure, beat, bpm, hasBpm, beatsPerMeasure, hasTS, control, desc);

            if (change == null) {
                result.isValid = false;
                result.errorMessage = $"M{measure}: could not build change from tokens.";
                return result;
            }

            result.change = change;
            result.isValid = true;
            return result;
        }

        private static MetronomeChange_v2 BuildTrackChange(
                int measure, int beat,
                float bpm, bool hasBpm,
                int beatsPerMeasure, bool hasTS,
                string control, string desc) {

            MetronomeChange_v2 change;

            if (control != null) {
                // Control event — tempo/TS tokens are ignored if a control keyword is present
                switch (control) {
                    case "MUTE": change = MetronomeChange_v2.CreateMute(measure, desc); break;
                    case "UNMUTE": change = MetronomeChange_v2.CreateUnmute(measure, desc); break;
                    case "STOP": change = MetronomeChange_v2.CreateStopEvent(measure, desc); break;
                    case "VISOFF": change = MetronomeChange_v2.CreateVisualOff(measure, desc); break;
                    case "VISON": change = MetronomeChange_v2.CreateVisualOn(measure, desc); break;
                    default:
                        Debug.LogWarning($"[TextParser_v2] Unknown control keyword '{control}'");
                        return null;
                }
            } else if (hasBpm && hasTS) {
                change = new MetronomeChange_v2(measure, bpm, beatsPerMeasure, desc);
            } else if (hasBpm) {
                change = new MetronomeChange_v2(measure, bpm, desc);
            } else {
                // hasTS only
                change = new MetronomeChange_v2(measure, beatsPerMeasure, desc);
            }

            change.targetBeat = beat;
            return change;
        }

        // =========================================================
        // SYNC EVENT PARSING
        // =========================================================

        private static ParsedSyncEvent ParseSyncLine(string line, int lineNumber) {
            var result = new ParsedSyncEvent {
                lineNumber = lineNumber,
                originalLine = line.Trim()
            };

            // --- Try stopAll pattern first ---
            // stopAll has no action track — check it before the resetBeat pattern
            // so the absent T<n> doesn't cause a false negative.
            var stopAllMatch = syncStopAllPattern.Match(line);
            if (stopAllMatch.Success)
                return ParseStopAllAlignment(result, stopAllMatch);

            // --- Try resetBeat pattern ---
            var match = syncPattern.Match(line);
            if (!match.Success) {
                result.isValid = false;
                result.errorMessage = "Invalid SYNC syntax. Expected: " +
                    "SYNC T<n> M<m> [B<b>] -> T<n> [T<bpm>] [\"desc\"] " +
                    "or: SYNC T<n> M<m> [B<b>] -> stopAll [\"desc\"]";
                return result;
            }

            // --- Reference track ---
            if (!int.TryParse(match.Groups["refTrack"].Value, out int refTrack) || refTrack < 1) {
                result.isValid = false;
                result.errorMessage = "Reference track number must be >= 1.";
                return result;
            }

            // --- Lock measure ---
            if (!int.TryParse(match.Groups["lockMeasure"].Value, out int lockMeasure) || lockMeasure < 1) {
                result.isValid = false;
                result.errorMessage = "Lock measure must be a positive integer.";
                return result;
            }

            // --- Lock beat (optional, defaults to 1) ---
            int lockBeat = 1;
            if (match.Groups["lockBeat"].Success) {
                if (!int.TryParse(match.Groups["lockBeat"].Value, out lockBeat) || lockBeat < 1) {
                    result.isValid = false;
                    result.errorMessage = "Lock beat must be >= 1.";
                    return result;
                }
            }

            // --- Action track ---
            if (!int.TryParse(match.Groups["actionTrack"].Value, out int actionTrack) || actionTrack < 1) {
                result.isValid = false;
                result.errorMessage = "Action track number must be >= 1.";
                return result;
            }

            // --- BPM (optional) ---
            float targetBpm = 0f;
            if (match.Groups["bpm"].Success) {
                if (!float.TryParse(match.Groups["bpm"].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out targetBpm) || targetBpm <= 0) {
                    result.isValid = false;
                    result.errorMessage = "SYNC BPM must be a positive number.";
                    return result;
                }
            }

            // --- Description (optional) ---
            string desc = match.Groups["desc"].Success
                ? match.Groups["desc"].Value
                : "";

            // --- Build the BeatLevelAlignment ---
            // Track IDs follow the convention "track1", "track2", "track3"
            var alignment = new BeatLevelAlignment {
                referenceTrack = $"track{refTrack}",
                lockAtMeasure = lockMeasure,
                lockAtBeat = lockBeat,
                actionTrack = $"track{actionTrack}",
                action = "resetBeat",
                resetToBeat = 1,
                targetBpm = targetBpm,
                description = desc
            };

            result.alignment = alignment;
            result.isValid = true;
            return result;
        }

        /// <summary>
        /// Builds a stopAll BeatLevelAlignment from a matched syncStopAllPattern.
        /// No actionTrack — the coordinator stops all metronomes directly.
        /// </summary>
        private static ParsedSyncEvent ParseStopAllAlignment(
                ParsedSyncEvent result, Match match) {

            if (!int.TryParse(match.Groups["refTrack"].Value, out int refTrack) || refTrack < 1) {
                result.isValid = false;
                result.errorMessage = "Reference track number must be >= 1.";
                return result;
            }

            if (!int.TryParse(match.Groups["lockMeasure"].Value, out int lockMeasure) || lockMeasure < 1) {
                result.isValid = false;
                result.errorMessage = "Lock measure must be a positive integer.";
                return result;
            }

            int lockBeat = 1;
            if (match.Groups["lockBeat"].Success) {
                if (!int.TryParse(match.Groups["lockBeat"].Value, out lockBeat) || lockBeat < 1) {
                    result.isValid = false;
                    result.errorMessage = "Lock beat must be >= 1.";
                    return result;
                }
            }

            string desc = match.Groups["desc"].Success
                ? match.Groups["desc"].Value
                : "";

            var alignment = new BeatLevelAlignment {
                referenceTrack = $"track{refTrack}",
                lockAtMeasure = lockMeasure,
                lockAtBeat = lockBeat,
                actionTrack = null,   // not used — coordinator stops all metronomes
                action = "stopAll",
                description = desc
            };

            result.alignment = alignment;
            result.isValid = true;
            return result;
        }

        // =========================================================
        // ROUND-TRIP FORMATTING
        // Converts MetronomeChange_v2 / BeatLevelAlignment back to
        // the text syntax so loaded JSON can be displayed in the textarea.
        // =========================================================

        /// <summary>
        /// Format a MetronomeChange_v2 back to its text syntax string.
        /// </summary>
        public static string FormatChange(MetronomeChange_v2 change) {
            if (change == null) return "";

            string beat = change.targetBeat > 1 ? $" B{change.targetBeat}" : "";
            string desc = !string.IsNullOrEmpty(change.description)
                ? $" \"{change.description}\"" : "";

            switch (change.type) {
                case MetronomeChange_v2.ChangeType.Tempo:
                    return $"M{change.targetMeasure}{beat} T{change.newBpm:F0}{desc}";

                case MetronomeChange_v2.ChangeType.TimeSignature:
                    return $"M{change.targetMeasure}{beat} TS{change.newBeatsPerMeasure}/4{desc}";

                case MetronomeChange_v2.ChangeType.Both:
                    return $"M{change.targetMeasure}{beat} T{change.newBpm:F0} TS{change.newBeatsPerMeasure}/4{desc}";

                case MetronomeChange_v2.ChangeType.Mute:
                    return $"M{change.targetMeasure}{beat} MUTE{desc}";

                case MetronomeChange_v2.ChangeType.Unmute:
                    return $"M{change.targetMeasure}{beat} UNMUTE{desc}";

                case MetronomeChange_v2.ChangeType.Stop:
                    return $"M{change.targetMeasure}{beat} STOP{desc}";

                case MetronomeChange_v2.ChangeType.VisualOff:
                    return $"M{change.targetMeasure}{beat} VISOFF{desc}";

                case MetronomeChange_v2.ChangeType.VisualOn:
                    return $"M{change.targetMeasure}{beat} VISON{desc}";

                case MetronomeChange_v2.ChangeType.Combined: {
                        var parts = new List<string>();
                        parts.Add($"M{change.targetMeasure}{beat}");
                        if (change.hasTempo) parts.Add($"T{change.newBpm:F0}");
                        if (change.hasTimeSignature) parts.Add($"TS{change.newBeatsPerMeasure}/4");
                        if (change.hasAudioEvent) parts.Add(change.muteAudio ? "MUTE" : "UNMUTE");
                        if (change.hasVisualEvent) parts.Add(change.hideVisual ? "VISOFF" : "VISON");
                        if (!string.IsNullOrEmpty(change.description))
                            parts.Add($"\"{change.description}\"");
                        return string.Join(" ", parts);
                    }

                default:
                    return $"M{change.targetMeasure}{beat} // unknown type: {change.type}";
            }
        }

        /// <summary>
        /// Format a BeatLevelAlignment back to its SYNC syntax string.
        /// Handles both resetBeat and stopAll actions.
        /// </summary>
        public static string FormatAlignment(BeatLevelAlignment alignment) {
            if (alignment == null) return "";

            string refNum = ExtractTrackNumber(alignment.referenceTrack);
            string beat = alignment.lockAtBeat > 1 ? $" B{alignment.lockAtBeat}" : "";
            string desc = !string.IsNullOrEmpty(alignment.description)
                ? $" \"{alignment.description}\"" : "";

            // stopAll — no action track, no BPM
            if (alignment.action == "stopAll")
                return $"SYNC T{refNum} M{alignment.lockAtMeasure}{beat} -> stopAll{desc}";

            // resetBeat — action track + optional BPM
            string actionNum = ExtractTrackNumber(alignment.actionTrack);
            string bpm = alignment.targetBpm > 0f ? $" T{alignment.targetBpm:F0}" : "";
            return $"SYNC T{refNum} M{alignment.lockAtMeasure}{beat} -> T{actionNum}{bpm}{desc}";
        }

        private static string ExtractTrackNumber(string trackId) {
            // "track1" → "1", handles any prefix followed by a number
            var m = Regex.Match(trackId, @"\d+$");
            return m.Success ? m.Value : trackId;
        }
    }
}