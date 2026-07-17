using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CompositionCoordination - JSON Schema Classes
/// Version: 2026-03-20 v2
/// 
/// PURPOSE: Data classes for loading coordination JSON files.
/// These describe cross-track beat-level alignment relationships.
/// 
/// CHANGES FROM v1:
///   BeatLevelAlignment: replaced referenceMeasure/referenceBeat with
///   lockAtMeasure/lockAtBeat. The coordinator now computes the pickup
///   beat trigger internally using CompositionIndex — the JSON author
///   specifies the desired lock point, not the implementation detail.
/// 
/// USAGE:
///   var coord = CompositionCoordination.FromJSON(jsonText);
///   foreach (var alignment in coord.beatLevelAlignments) { ... }
/// </summary>
namespace ChangeComposer.Coordination {

    // =========================================================
    // TRACK REFERENCE
    // Describes a single track in the composition
    // =========================================================

    [Serializable]
    public class TrackReference {
        /// <summary>Unique ID for this track, e.g. "track1"</summary>
        public string id;

        /// <summary>
        /// Filename of the track's tempo JSON, e.g. "track1_steady.json".
        /// Used as a lookup key when coordinator has multiple TextAssets assigned.
        /// </summary>
        public string jsonFile;

        /// <summary>
        /// Which inspector metronome slot this track maps to.
        /// Supported values: "metronome1", "metronome2", "metronome3"
        /// </summary>
        public string metronomeReference;
    }

    // =========================================================
    // BEAT LEVEL ALIGNMENT
    // "When reference reaches lockAtMeasure/lockAtBeat, reset action track's beat"
    //
    // NOTE: lockAtMeasure/lockAtBeat describe the desired lock point —
    // the moment both tracks should be aligned. The coordinator computes
    // the actual pickup beat trigger internally (one beat before the lock
    // point) using CompositionIndex. JSON authors never need to calculate
    // this themselves.
    //
    // Example: to lock both tracks at M31 B1, write lockAtMeasure=31,
    // lockAtBeat=1. In 4/4, the coordinator will fire at M30 B4.
    // =========================================================

    [Serializable]
    public class BeatLevelAlignment {
        /// <summary>Human-readable description for logging</summary>
        public string description;

        // --- REFERENCE TRACK (watch this) ---

        /// <summary>Track ID of the reference timeline to watch</summary>
        public string referenceTrack;

        // --- LOCK POINT (when both tracks should be aligned) ---

        /// <summary>
        /// Measure number where both tracks should be aligned.
        /// The coordinator fires one beat before this point (pickup beat).
        /// </summary>
        public int lockAtMeasure;

        /// <summary>
        /// Beat number where both tracks should be aligned (1-based).
        /// Default = 1 (downbeat). The coordinator fires one beat before this.
        /// </summary>
        public int lockAtBeat = 1;

        // --- ACTION TRACK (do this) ---

        /// <summary>Track ID of the metronome to act on</summary>
        public string actionTrack;

        /// <summary>
        /// Action to execute. Currently supported:
        ///   "resetBeat" - call SetCurrentBeat(resetToBeat) on the action metronome
        /// </summary>
        public string action = "resetBeat";

        /// <summary>Beat number to reset to (used by "resetBeat" action)</summary>
        public int resetToBeat = 1;

        /// <summary>BPM to set on the action metronome at the moment of alignment. 0 = no change</summary>
        public float targetBpm = 0f;
    }

    // =========================================================
    // START ALL CONFIG
    // Optional: start all tracks simultaneously at composition start
    // =========================================================

    [Serializable]
    public class StartAllConfig {
        /// <summary>If true, coordinator will auto-start all metronomes</summary>
        public bool enabled = true;

        /// <summary>Which measure to start from (usually 1)</summary>
        public int atMeasure = 1;

        /// <summary>Seconds to wait before starting (gives Unity time to settle)</summary>
        public float delaySeconds = 0.5f;
    }

    // =========================================================
    // COMPOSITION COORDINATION (root object)
    // =========================================================

    [Serializable]
    public class CompositionCoordination {
        /// <summary>Human-readable title for logging/display</summary>
        public string compositionTitle;

        /// <summary>Optional description for documentation</summary>
        public string description;

        /// <summary>All tracks in this composition</summary>
        public List<TrackReference> tracks;

        /// <summary>All beat-level sync alignments to execute</summary>
        public List<BeatLevelAlignment> beatLevelAlignments;

        /// <summary>Auto-start configuration</summary>
        public StartAllConfig startAll;

        // -------------------------------------------------------

        /// <summary>Parse a CompositionCoordination from a JSON string</summary>
        public static CompositionCoordination FromJSON(string json) {
            return JsonUtility.FromJson<CompositionCoordination>(json);
        }

        /// <summary>Look up a TrackReference by its ID</summary>
        public TrackReference GetTrack(string id) {
            if (tracks == null) return null;
            return tracks.Find(t => t.id == id);
        }
    }
}