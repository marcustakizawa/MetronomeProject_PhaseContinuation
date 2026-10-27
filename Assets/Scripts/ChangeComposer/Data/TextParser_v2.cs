using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using ChangeComposer.Data;
using ChangeComposer.Coordination;

namespace ChangeComposer.Parsing
{

    /// <summary>
    /// TextParser_v2 — Unified text parser for ChangeComposer editor input
    /// Version: 2026-08-11 v4 (Syntax revision — spelled-out keywords)
    ///
    /// PURPOSE:
    ///   Parses free-text input from the editor scene into MetronomeChange_v2
    ///   objects (track events) and BeatLevelAlignment objects (SYNC events).
    ///
    /// WHAT CHANGED IN v4:
    ///   The old syntax overloaded 'T' — it meant both "track" and "tempo"
    ///   depending on position (SYNC T1 M31 B1 -> T2 T120). That ambiguity is
    ///   gone. Keywords are now spelled out:
    ///
    ///     T120   → BPM120        M7    → Measure7      B3     → Beat3
    ///     TS3/4  → TimeSig3/4    T1    → Track1
    ///     VISOFF → VisualOff     VISON → VisualOn
    ///     MUTE   → Mute          UNMUTE → Unmute       STOP   → Stop
    ///
    ///   Control keywords now match the MetronomeChange_v2.ChangeType enum
    ///   member names exactly, so what you read in the editor is what is valid
    ///   in the JSON. 'stopAll' is unchanged — it matches the coordination
    ///   JSON "action" string, which is a different layer.
    ///
    ///   MIGRATION: old syntax still parses. Every old keyword is kept as a
    ///   regex alternate, so nothing already written breaks. The formatters
    ///   emit ONLY the new form, so any composition loaded from JSON re-renders
    ///   in new syntax with no conversion step. There is no hard cutover.
    ///
    /// TRACK EVENT SYNTAX:
    ///   Measure&lt;n&gt; [Beat&lt;n&gt;] [BPM&lt;n&gt;] [TimeSig&lt;n&gt;/&lt;n&gt;] [&lt;control&gt;] ["description"]
    ///
    ///   Measure5 BPM110                      → tempo change at M5 B1
    ///   Measure5 Beat2 BPM110                → tempo change at M5 B2
    ///   Measure7 Beat3 BPM130 TimeSig3/4     → tempo + time sig at M7 B3
    ///   Measure7 TimeSig3/4                  → time sig only at M7 B1
    ///   Measure10 Mute                       → mute at M10 B1
    ///   Measure10 Unmute                     → unmute at M10 B1
    ///   Measure10 Stop                       → stop at M10 B1
    ///   Measure10 VisualOff                  → visual off at M10 B1
    ///   Measure10 VisualOn                   → visual on at M10 B1
    ///   Measure5 BPM110 "Drop to 110"        → with description
    ///   // comment                           → skipped
    ///
    /// SYNC EVENT SYNTAX:
    ///   SYNC Track&lt;n&gt; Measure&lt;m&gt; [Beat&lt;b&gt;] (-&gt;/→) Track&lt;n&gt; [BPM&lt;n&gt;] ["description"]
    ///   SYNC Track&lt;n&gt; Measure&lt;m&gt; [Beat&lt;b&gt;] (-&gt;/→) stopAll ["description"]
    ///
    ///   SYNC Track1 Measure30 Beat1 -> Track2 BPM120 "Lock track 2 at 120"
    ///   SYNC Track1 Measure30 Beat1 -> Track3            (no BPM change, just reset beat)
    ///   SYNC Track1 Measure30 -> Track2 BPM120           (Beat defaults to 1)
    ///   SYNC Track1 Measure33 Beat1 -> stopAll "End of take"
    ///
    /// TRACK NUMBERING:
    ///   Track tokens are numeric only. Track1 maps to the id "track1", which
    ///   must match the Track Id field on the coordinator's trackJsonBindings
    ///   and the "id" field in the coordination JSON. Arbitrary track names are
    ///   not expressible in SYNC lines.
    ///
    /// TIME SIGNATURE DENOMINATOR:
    ///   Parsed and validated, but NOT YET IMPLEMENTED downstream — there is no
    ///   beatUnit field in MetronomeChange_v2 or the JSON schema, and the
    ///   metronome computes 60/bpm flat. Anything other than /4 logs a warning
    ///   and is treated as /4. See TimeSignature_Denominator_Design_Handoff.
    ///
    /// OUTPUT:
    ///   ParseTrackEvents()  → List of ParsedTrackEvent (one per valid line)
    ///   ParseSyncEvents()   → List of ParsedSyncEvent (one per valid SYNC line)
    ///   ParseAll()          → ParseResult containing both lists + error summary
    ///
    /// ROUND-TRIP:
    ///   FormatChange()      → formats a MetronomeChange_v2 to new syntax
    ///   FormatAlignment()   → formats a BeatLevelAlignment to new SYNC syntax
    ///
    /// SELF-TEST:
    ///   RunSelfTest()       → parses a fixture list covering both vocabularies
    ///                         and logs a pass/fail report to the Console.
    /// </summary>
    public static class TextParser_v2
    {

        // =========================================================
        // RESULT TYPES
        // =========================================================

        public class ParsedTrackEvent
        {
            public MetronomeChange_v2 change;
            public int lineNumber;
            public string originalLine;
            public bool isValid;
            public string errorMessage;
        }

        public class ParsedSyncEvent
        {
            public BeatLevelAlignment alignment;
            public int lineNumber;
            public string originalLine;
            public bool isValid;
            public string errorMessage;
        }

        public class ParseResult
        {
            public List<ParsedTrackEvent> trackEvents = new List<ParsedTrackEvent>();
            public List<ParsedSyncEvent> syncEvents = new List<ParsedSyncEvent>();
            public int successCount;
            public int errorCount;
            public string errorSummary; // first error, for status display
        }

        // =========================================================
        // SYNTAX CONSTANTS
        // =========================================================

        // The only time signature denominator the runtime currently honours.
        private const int SupportedBeatUnit = 4;

        // Valid denominators once beatUnit support lands. Used for validation
        // now so bad input is rejected consistently before and after.
        private static readonly int[] ValidBeatUnits = { 1, 2, 4, 8, 16, 32, 64 };

        // Human-readable syntax reminders, used in error messages so the
        // message and the grammar can't drift apart.
        private const string TrackSyntaxHint =
            "Measure<n> [Beat<n>] [BPM<n>] [TimeSig<n>/<n>] " +
            "[Mute|Unmute|Stop|VisualOff|VisualOn] [\"desc\"]";

        private const string SyncSyntaxHint =
            "SYNC Track<n> Measure<m> [Beat<b>] -> Track<n> [BPM<n>] [\"desc\"]  " +
            "or:  SYNC Track<n> Measure<m> [Beat<b>] -> stopAll [\"desc\"]";

        // =========================================================
        // REGEX PATTERNS
        // =========================================================
        //
        // Every keyword accepts the new spelled-out form and the old short
        // form as alternates. The LONGER alternate is always listed first:
        // 'Measure7' must not be consumed as 'M' followed by a failed digit
        // match. .NET would backtrack and recover, but ordering avoids it.
        //
        // Tokens disambiguate on the digits that follow, which is why
        // Beat/BPM and TimeSig/TS don't collide:
        //   " BPM120" vs the beat group → 'B' matches, 'P' is not a digit,
        //   the optional group matches empty, and the bpm group takes it.

        private const string MeasureToken = @"(?:Measure|M)";
        private const string BeatToken = @"(?:Beat|B)";
        private const string BpmToken = @"(?:BPM|T)";
        private const string TimeSigToken = @"(?:TimeSig|TS)";
        private const string TrackToken = @"(?:Track|T)";

        // Control keywords: new names first, then old. Unmute before Mute.
        private const string ControlToken =
            @"(?:VisualOff|VisualOn|VISOFF|VISON|Unmute|UNMUTE|Mute|MUTE|Stop|STOP)";

        // Track event:
        //   Measure<n> [Beat<n>] [BPM<n>] [TimeSig<n>/<n>] [control] ["desc"]
        private static readonly Regex trackPattern = new Regex(
            @"^\s*" + MeasureToken + @"(?<measure>\d+)" +                    // Measure16 (required)
            @"(?:\s+" + BeatToken + @"(?<beat>\d+))?" +                      // Beat2 (optional)
            @"(?:\s+" + BpmToken + @"(?<bpm>\d+(?:\.\d+)?))?" +              // BPM140 (optional)
            @"(?:\s+" + TimeSigToken + @"(?<beats>\d+)/(?<unit>\d+))?" +     // TimeSig3/4 (optional)
            @"(?:\s+(?<control>" + ControlToken + @"))?" +                   // control (optional)
            @"(?:\s+""(?<desc>[^""]*)"")?" +                                 // "description" (optional)
            @"\s*(?://.*)?$",                                                // trailing comment
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        // SYNC event — resetBeat:
        //   SYNC Track<n> Measure<m> [Beat<b>] (->/→) Track<n> [BPM<n>] ["desc"]
        private static readonly Regex syncPattern = new Regex(
            @"^\s*SYNC\s+" +
            TrackToken + @"(?<refTrack>\d+)\s+" +                            // Track1 (reference)
            MeasureToken + @"(?<lockMeasure>\d+)" +                           // Measure30
            @"(?:\s+" + BeatToken + @"(?<lockBeat>\d+))?" +                   // Beat1 (optional)
            @"\s+(?:->|→)\s+" +                                               // -> or →
            TrackToken + @"(?<actionTrack>\d+)" +                             // Track2 (action)
            @"(?:\s+" + BpmToken + @"(?<bpm>\d+(?:\.\d+)?))?" +               // BPM120 (optional)
            @"(?:\s+""(?<desc>[^""]*)"")?" +                                  // "description" (optional)
            @"\s*(?://.*)?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        // SYNC event — stopAll:
        //   SYNC Track<n> Measure<m> [Beat<b>] (->/→) stopAll ["desc"]
        // No action track — the coordinator stops all metronomes directly.
        private static readonly Regex syncStopAllPattern = new Regex(
            @"^\s*SYNC\s+" +
            TrackToken + @"(?<refTrack>\d+)\s+" +                            // Track1 (reference)
            MeasureToken + @"(?<lockMeasure>\d+)" +                           // Measure33
            @"(?:\s+" + BeatToken + @"(?<lockBeat>\d+))?" +                   // Beat1 (optional)
            @"\s+(?:->|→)\s+" +                                               // -> or →
            @"stopAll" +                                                      // literal keyword
            @"(?:\s+""(?<desc>[^""]*)"")?" +                                  // "description" (optional)
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
        public static ParseResult ParseAll(string text)
        {
            var result = new ParseResult();

            if (string.IsNullOrWhiteSpace(text))
                return result;

            string[] lines = text.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int lineNumber = i + 1;

                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//"))
                    continue;

                // Route on line prefix. Both 'M7' and 'Measure7' start with M,
                // so the track branch is unchanged by the syntax revision.
                if (line.TrimStart().StartsWith("SYNC", StringComparison.OrdinalIgnoreCase))
                {
                    var syncEvent = ParseSyncLine(line, lineNumber);
                    result.syncEvents.Add(syncEvent);

                    if (syncEvent.isValid)
                        result.successCount++;
                    else
                    {
                        result.errorCount++;
                        if (result.errorSummary == null)
                            result.errorSummary = $"Line {lineNumber}: {syncEvent.errorMessage}";
                    }
                }
                else if (line.TrimStart().StartsWith("M", StringComparison.OrdinalIgnoreCase))
                {
                    var trackEvent = ParseTrackLine(line, lineNumber);
                    result.trackEvents.Add(trackEvent);

                    if (trackEvent.isValid)
                        result.successCount++;
                    else
                    {
                        result.errorCount++;
                        if (result.errorSummary == null)
                            result.errorSummary = $"Line {lineNumber}: {trackEvent.errorMessage}";
                    }
                }
                else
                {
                    // Unrecognised line — report as error
                    result.errorCount++;
                    if (result.errorSummary == null)
                        result.errorSummary = $"Line {lineNumber}: unrecognised syntax — '{line.Trim()}'";
                }
            }

            return result;
        }

        /// <summary>
        /// Parse only track event lines. Ignores SYNC lines.
        /// </summary>
        public static List<ParsedTrackEvent> ParseTrackEvents(string text)
        {
            return ParseAll(text).trackEvents;
        }

        /// <summary>
        /// Parse only SYNC lines. Ignores track event lines.
        /// </summary>
        public static List<ParsedSyncEvent> ParseSyncEvents(string text)
        {
            return ParseAll(text).syncEvents;
        }

        // =========================================================
        // TRACK EVENT PARSING
        // =========================================================

        private static ParsedTrackEvent ParseTrackLine(string line, int lineNumber)
        {
            var result = new ParsedTrackEvent
            {
                lineNumber = lineNumber,
                originalLine = line.Trim()
            };

            var match = trackPattern.Match(line);
            if (!match.Success)
            {
                result.isValid = false;
                result.errorMessage = $"Invalid track event syntax. Expected: {TrackSyntaxHint}";
                return result;
            }

            // --- Measure (required) ---
            if (!int.TryParse(match.Groups["measure"].Value, out int measure) || measure <= 0)
            {
                result.isValid = false;
                result.errorMessage = "Measure number must be a positive integer.";
                return result;
            }

            // --- Beat (optional, defaults to 1) ---
            int beat = 1;
            if (match.Groups["beat"].Success)
            {
                if (!int.TryParse(match.Groups["beat"].Value, out beat) || beat < 1)
                {
                    result.isValid = false;
                    result.errorMessage = "Beat number must be >= 1.";
                    return result;
                }
            }

            // --- BPM (optional) ---
            float bpm = 0f;
            bool hasBpm = match.Groups["bpm"].Success;
            if (hasBpm)
            {
                if (!float.TryParse(match.Groups["bpm"].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out bpm) || bpm <= 0)
                {
                    result.isValid = false;
                    result.errorMessage = "BPM must be a positive number.";
                    return result;
                }
            }

            // --- Time signature (optional) ---
            int beatsPerMeasure = 0;
            bool hasTS = match.Groups["beats"].Success;
            if (hasTS)
            {
                if (!int.TryParse(match.Groups["beats"].Value, out beatsPerMeasure) ||
                    beatsPerMeasure <= 0)
                {
                    result.isValid = false;
                    result.errorMessage = "Time signature numerator must be a positive integer.";
                    return result;
                }

                // Denominator: validated here, not yet honoured downstream.
                if (!ValidateBeatUnit(match.Groups["unit"].Value, measure,
                                      out string unitError))
                {
                    result.isValid = false;
                    result.errorMessage = unitError;
                    return result;
                }
            }

            // --- Control event (optional) ---
            string control = match.Groups["control"].Success
                ? NormaliseControl(match.Groups["control"].Value)
                : null;

            // --- Description (optional) ---
            string desc = match.Groups["desc"].Success
                ? match.Groups["desc"].Value
                : "";

            // --- Validate that at least one action is specified ---
            if (!hasBpm && !hasTS && control == null)
            {
                result.isValid = false;
                result.errorMessage =
                    $"Measure{measure}: no action specified. " +
                    "Add BPM<n>, TimeSig<n>/<n>, or a control keyword.";
                return result;
            }

            // --- Build the MetronomeChange_v2 ---
            MetronomeChange_v2 change = BuildTrackChange(
                measure, beat, bpm, hasBpm, beatsPerMeasure, hasTS, control, desc);

            if (change == null)
            {
                result.isValid = false;
                result.errorMessage = $"Measure{measure}: could not build change from tokens.";
                return result;
            }

            result.change = change;
            result.isValid = true;
            return result;
        }

        /// <summary>
        /// Validates a time signature denominator.
        ///
        /// Rejects values outside the valid set outright. Accepts valid values
        /// other than 4 with a warning, because the runtime has no beatUnit
        /// field yet and will treat them as /4 — the audio will be a plain
        /// count at the written BPM. Rejecting them instead would block meters
        /// that already produce usable clicks. The warning exists so this is
        /// discovered from the Console rather than from a bar sounding wrong.
        ///
        /// Remove the warning branch once beatUnit support lands.
        /// </summary>
        private static bool ValidateBeatUnit(string rawUnit, int measure, out string error)
        {
            error = null;

            if (!int.TryParse(rawUnit, out int unit) || unit <= 0)
            {
                error = $"Measure{measure}: time signature denominator must be a positive integer.";
                return false;
            }

            if (Array.IndexOf(ValidBeatUnits, unit) < 0)
            {
                error = $"Measure{measure}: invalid time signature denominator '{unit}'. " +
                        $"Valid values: {string.Join(", ", ValidBeatUnits)}.";
                return false;
            }

            if (unit != SupportedBeatUnit)
            {
                Debug.LogWarning(
                    $"[TextParser_v2] Measure{measure}: denominator /{unit} is parsed but " +
                    $"NOT YET IMPLEMENTED — it will be treated as /{SupportedBeatUnit}. " +
                    "The click will be a flat count at the written BPM. " +
                    "See TimeSignature_Denominator_Design_Handoff.");
            }

            return true;
        }

        /// <summary>
        /// Maps any accepted control spelling — new or old — to the canonical
        /// MetronomeChange_v2.ChangeType member name.
        /// </summary>
        private static string NormaliseControl(string raw)
        {
            switch (raw.ToUpperInvariant())
            {
                case "MUTE": return "Mute";
                case "UNMUTE": return "Unmute";
                case "STOP": return "Stop";
                case "VISOFF":
                case "VISUALOFF": return "VisualOff";
                case "VISON":
                case "VISUALON": return "VisualOn";
                default: return raw;
            }
        }

        private static MetronomeChange_v2 BuildTrackChange(
                int measure, int beat,
                float bpm, bool hasBpm,
                int beatsPerMeasure, bool hasTS,
                string control, string desc)
        {

            MetronomeChange_v2 change;

            if (control != null)
            {
                // Control event — tempo/TS tokens are ignored if a control keyword is present
                switch (control)
                {
                    case "Mute": change = MetronomeChange_v2.CreateMute(measure, desc); break;
                    case "Unmute": change = MetronomeChange_v2.CreateUnmute(measure, desc); break;
                    case "Stop": change = MetronomeChange_v2.CreateStopEvent(measure, desc); break;
                    case "VisualOff": change = MetronomeChange_v2.CreateVisualOff(measure, desc); break;
                    case "VisualOn": change = MetronomeChange_v2.CreateVisualOn(measure, desc); break;
                    default:
                        Debug.LogWarning($"[TextParser_v2] Unknown control keyword '{control}'");
                        return null;
                }
            }
            else if (hasBpm && hasTS)
            {
                change = new MetronomeChange_v2(measure, bpm, beatsPerMeasure, desc);
            }
            else if (hasBpm)
            {
                change = new MetronomeChange_v2(measure, bpm, desc);
            }
            else
            {
                // hasTS only
                change = new MetronomeChange_v2(measure, beatsPerMeasure, desc);
            }

            change.targetBeat = beat;
            return change;
        }

        // =========================================================
        // SYNC EVENT PARSING
        // =========================================================

        private static ParsedSyncEvent ParseSyncLine(string line, int lineNumber)
        {
            var result = new ParsedSyncEvent
            {
                lineNumber = lineNumber,
                originalLine = line.Trim()
            };

            // --- Try stopAll pattern first ---
            // stopAll has no action track — check it before the resetBeat pattern
            // so the absent Track<n> doesn't cause a false negative.
            var stopAllMatch = syncStopAllPattern.Match(line);
            if (stopAllMatch.Success)
                return ParseStopAllAlignment(result, stopAllMatch);

            // --- Try resetBeat pattern ---
            var match = syncPattern.Match(line);
            if (!match.Success)
            {
                result.isValid = false;
                result.errorMessage = $"Invalid SYNC syntax. Expected: {SyncSyntaxHint}";
                return result;
            }

            // --- Reference track ---
            if (!int.TryParse(match.Groups["refTrack"].Value, out int refTrack) || refTrack < 1)
            {
                result.isValid = false;
                result.errorMessage = "Reference track number must be >= 1.";
                return result;
            }

            // --- Lock measure ---
            if (!int.TryParse(match.Groups["lockMeasure"].Value, out int lockMeasure) || lockMeasure < 1)
            {
                result.isValid = false;
                result.errorMessage = "Lock measure must be a positive integer.";
                return result;
            }

            // --- Lock beat (optional, defaults to 1) ---
            int lockBeat = 1;
            if (match.Groups["lockBeat"].Success)
            {
                if (!int.TryParse(match.Groups["lockBeat"].Value, out lockBeat) || lockBeat < 1)
                {
                    result.isValid = false;
                    result.errorMessage = "Lock beat must be >= 1.";
                    return result;
                }
            }

            // --- Action track ---
            if (!int.TryParse(match.Groups["actionTrack"].Value, out int actionTrack) || actionTrack < 1)
            {
                result.isValid = false;
                result.errorMessage = "Action track number must be >= 1.";
                return result;
            }

            // --- BPM (optional) ---
            float targetBpm = 0f;
            if (match.Groups["bpm"].Success)
            {
                if (!float.TryParse(match.Groups["bpm"].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out targetBpm) || targetBpm <= 0)
                {
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
            var alignment = new BeatLevelAlignment
            {
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
                ParsedSyncEvent result, Match match)
        {

            if (!int.TryParse(match.Groups["refTrack"].Value, out int refTrack) || refTrack < 1)
            {
                result.isValid = false;
                result.errorMessage = "Reference track number must be >= 1.";
                return result;
            }

            if (!int.TryParse(match.Groups["lockMeasure"].Value, out int lockMeasure) || lockMeasure < 1)
            {
                result.isValid = false;
                result.errorMessage = "Lock measure must be a positive integer.";
                return result;
            }

            int lockBeat = 1;
            if (match.Groups["lockBeat"].Success)
            {
                if (!int.TryParse(match.Groups["lockBeat"].Value, out lockBeat) || lockBeat < 1)
                {
                    result.isValid = false;
                    result.errorMessage = "Lock beat must be >= 1.";
                    return result;
                }
            }

            string desc = match.Groups["desc"].Success
                ? match.Groups["desc"].Value
                : "";

            var alignment = new BeatLevelAlignment
            {
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
        // Converts MetronomeChange_v2 / BeatLevelAlignment back to the text
        // syntax so loaded JSON can be displayed in the textarea.
        //
        // These emit the NEW syntax only. That is the migration mechanism:
        // an old composition loads through the legacy alternates and
        // immediately re-renders in the new vocabulary.
        //
        // NOTE: the time signature denominator is hardcoded to /4 because
        // MetronomeChange_v2 has no beatUnit field. Update these three sites
        // when it does.
        // =========================================================

        /// <summary>
        /// Format a MetronomeChange_v2 back to its text syntax string.
        /// </summary>
        public static string FormatChange(MetronomeChange_v2 change)
        {
            if (change == null) return "";

            string beat = change.targetBeat > 1 ? $" Beat{change.targetBeat}" : "";
            string desc = !string.IsNullOrEmpty(change.description)
                ? $" \"{change.description}\"" : "";

            switch (change.type)
            {
                case MetronomeChange_v2.ChangeType.Tempo:
                    return $"Measure{change.targetMeasure}{beat} BPM{change.newBpm:F0}{desc}";

                case MetronomeChange_v2.ChangeType.TimeSignature:
                    return $"Measure{change.targetMeasure}{beat} TimeSig{change.newBeatsPerMeasure}/4{desc}";

                case MetronomeChange_v2.ChangeType.Both:
                    return $"Measure{change.targetMeasure}{beat} BPM{change.newBpm:F0} TimeSig{change.newBeatsPerMeasure}/4{desc}";

                case MetronomeChange_v2.ChangeType.Mute:
                    return $"Measure{change.targetMeasure}{beat} Mute{desc}";

                case MetronomeChange_v2.ChangeType.Unmute:
                    return $"Measure{change.targetMeasure}{beat} Unmute{desc}";

                case MetronomeChange_v2.ChangeType.Stop:
                    return $"Measure{change.targetMeasure}{beat} Stop{desc}";

                case MetronomeChange_v2.ChangeType.VisualOff:
                    return $"Measure{change.targetMeasure}{beat} VisualOff{desc}";

                case MetronomeChange_v2.ChangeType.VisualOn:
                    return $"Measure{change.targetMeasure}{beat} VisualOn{desc}";

                case MetronomeChange_v2.ChangeType.Combined:
                    {
                        var parts = new List<string>();
                        parts.Add($"Measure{change.targetMeasure}{beat}");
                        if (change.hasTempo) parts.Add($"BPM{change.newBpm:F0}");
                        if (change.hasTimeSignature) parts.Add($"TimeSig{change.newBeatsPerMeasure}/4");
                        if (change.hasAudioEvent) parts.Add(change.muteAudio ? "Mute" : "Unmute");
                        if (change.hasVisualEvent) parts.Add(change.hideVisual ? "VisualOff" : "VisualOn");
                        if (!string.IsNullOrEmpty(change.description))
                            parts.Add($"\"{change.description}\"");
                        return string.Join(" ", parts);
                    }

                default:
                    return $"Measure{change.targetMeasure}{beat} // unknown type: {change.type}";
            }
        }

        /// <summary>
        /// Format a BeatLevelAlignment back to its SYNC syntax string.
        /// Handles both resetBeat and stopAll actions.
        /// </summary>
        public static string FormatAlignment(BeatLevelAlignment alignment)
        {
            if (alignment == null) return "";

            string refNum = ExtractTrackNumber(alignment.referenceTrack);
            string beat = alignment.lockAtBeat > 1 ? $" Beat{alignment.lockAtBeat}" : "";
            string desc = !string.IsNullOrEmpty(alignment.description)
                ? $" \"{alignment.description}\"" : "";

            // stopAll — no action track, no BPM
            if (alignment.action == "stopAll")
                return $"SYNC Track{refNum} Measure{alignment.lockAtMeasure}{beat} -> stopAll{desc}";

            // resetBeat — action track + optional BPM
            string actionNum = ExtractTrackNumber(alignment.actionTrack);
            string bpm = alignment.targetBpm > 0f ? $" BPM{alignment.targetBpm:F0}" : "";
            return $"SYNC Track{refNum} Measure{alignment.lockAtMeasure}{beat} -> Track{actionNum}{bpm}{desc}";
        }

        private static string ExtractTrackNumber(string trackId)
        {
            // "track1" → "1", handles any prefix followed by a number
            var m = Regex.Match(trackId, @"\d+$");
            return m.Success ? m.Value : trackId;
        }

        // =========================================================
        // SELF-TEST
        // =========================================================
        //
        // Static class, so no ContextMenu. Call from anywhere:
        //   TextParser_v2.RunSelfTest();
        // e.g. a temporary button on CompositionEditorController, or an
        // Editor script with [MenuItem("ChangeComposer/Run Parser Self-Test")].
        //
        // The important assertions are the EQUIVALENCE pairs: each old-syntax
        // line and its new-syntax twin must format to the same string. That is
        // what proves "accept both, emit new" actually holds.

        private struct Fixture
        {
            public string oldForm;
            public string newForm;
            public Fixture(string o, string n) { oldForm = o; newForm = n; }
        }

        private static readonly Fixture[] EquivalenceFixtures = {
            // --- track events ---
            new Fixture("M5 T110",                    "Measure5 BPM110"),
            new Fixture("M5 B2 T110",                 "Measure5 Beat2 BPM110"),
            new Fixture("M7 B3 T130 TS3/4",           "Measure7 Beat3 BPM130 TimeSig3/4"),
            new Fixture("M7 TS3/4",                   "Measure7 TimeSig3/4"),
            new Fixture("M10 MUTE",                   "Measure10 Mute"),
            new Fixture("M10 UNMUTE",                 "Measure10 Unmute"),
            new Fixture("M10 STOP",                   "Measure10 Stop"),
            new Fixture("M10 VISOFF",                 "Measure10 VisualOff"),
            new Fixture("M10 VISON",                  "Measure10 VisualOn"),
            new Fixture("M5 T110 \"Drop to 110\"",    "Measure5 BPM110 \"Drop to 110\""),
            new Fixture("M16 B4 T90 TS7/4 \"Odd\"",   "Measure16 Beat4 BPM90 TimeSig7/4 \"Odd\""),

            // --- SYNC events ---
            new Fixture("SYNC T1 M30 B2 -> T2 T120 \"Lock\"",
                        "SYNC Track1 Measure30 Beat2 -> Track2 BPM120 \"Lock\""),
            new Fixture("SYNC T1 M30 -> T3",
                        "SYNC Track1 Measure30 -> Track3"),
            new Fixture("SYNC T1 M33 B1 -> stopAll \"End of take\"",
                        "SYNC Track1 Measure33 Beat1 -> stopAll \"End of take\""),
            new Fixture("SYNC T1 M33 → stopAll",
                        "SYNC Track1 Measure33 -> stopAll"),
        };

        // Lines that MUST fail to parse. A parser that accepts these is worse
        // than one that rejects valid input, because the failure is silent.
        private static readonly string[] MustFailFixtures = {
            "Measure5",                          // no action specified
            "Measure5 BPM0",                     // BPM must be positive
            "Measure0 BPM120",                   // measure must be positive
            "Measure5 TimeSig3/7",               // denominator not in valid set
            "Measure5 TimeSig3/4.78498",         // denominator not an integer
            "SYNC Track1 -> Track2",             // no measure
            "SYNC Track1 Measure30 -> stopEverything", // unknown action
            "Frobnicate the widget",             // unrecognised line
        };

        /// <summary>
        /// Parses the fixture list and logs a pass/fail report to the Console.
        /// Returns true if everything passed.
        /// </summary>
        public static bool RunSelfTest()
        {
            int passed = 0, failed = 0;
            var log = new System.Text.StringBuilder();
            log.AppendLine("=== TextParser_v2 SELF-TEST ===");

            // --- Equivalence: old and new must produce identical output ---
            log.AppendLine("\n-- Equivalence (old syntax vs new syntax) --");
            foreach (var f in EquivalenceFixtures)
            {
                string oldOut = FormatFirst(f.oldForm);
                string newOut = FormatFirst(f.newForm);

                if (oldOut != null && oldOut == newOut)
                {
                    passed++;
                    log.AppendLine($"  PASS  {f.oldForm}\n        → {oldOut}");
                }
                else
                {
                    failed++;
                    log.AppendLine($"  FAIL  {f.oldForm}\n        old → {oldOut ?? "<parse error>"}" +
                                   $"\n        new → {newOut ?? "<parse error>"}");
                }
            }

            // --- Emission: formatted output must itself re-parse (idempotence) ---
            log.AppendLine("\n-- Idempotence (formatted output re-parses unchanged) --");
            foreach (var f in EquivalenceFixtures)
            {
                string once = FormatFirst(f.newForm);
                string twice = once != null ? FormatFirst(once) : null;

                if (once != null && once == twice)
                {
                    passed++;
                }
                else
                {
                    failed++;
                    log.AppendLine($"  FAIL  {f.newForm}\n        1st → {once ?? "<parse error>"}" +
                                   $"\n        2nd → {twice ?? "<parse error>"}");
                }
            }
            log.AppendLine($"  ({EquivalenceFixtures.Length} checked)");

            // --- Rejection: bad input must be reported, not absorbed ---
            log.AppendLine("\n-- Rejection (these must NOT parse) --");
            foreach (var bad in MustFailFixtures)
            {
                var r = ParseAll(bad);
                if (r.errorCount > 0 && r.successCount == 0)
                {
                    passed++;
                }
                else
                {
                    failed++;
                    log.AppendLine($"  FAIL  accepted invalid input: '{bad}'");
                }
            }
            log.AppendLine($"  ({MustFailFixtures.Length} checked)");

            log.AppendLine($"\n=== {passed} passed, {failed} failed ===");

            if (failed > 0) Debug.LogError(log.ToString());
            else Debug.Log(log.ToString());

            return failed == 0;
        }

        /// <summary>
        /// Parses a single line and formats the resulting object back to text.
        /// Returns null if the line did not parse. Test helper.
        /// </summary>
        private static string FormatFirst(string line)
        {
            var r = ParseAll(line);

            if (r.trackEvents.Count > 0 && r.trackEvents[0].isValid)
                return FormatChange(r.trackEvents[0].change);

            if (r.syncEvents.Count > 0 && r.syncEvents[0].isValid)
                return FormatAlignment(r.syncEvents[0].alignment);

            return null;
        }
    }
}