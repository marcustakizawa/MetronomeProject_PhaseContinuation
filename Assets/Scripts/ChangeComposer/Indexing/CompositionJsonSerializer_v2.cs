using System;
using System.Collections.Generic;
using UnityEngine;
using ChangeComposer.Data;

namespace ChangeComposer.Serialization {

    /// <summary>
    /// CompositionJsonSerializer_v2
    /// Version: 2026-03-20 v2
    ///
    /// PURPOSE:
    ///   Read and write track JSON files in v2 format.
    ///   v2 is the only supported format — no backward compatibility with v1.
    ///
    /// V2 JSON FORMAT:
    ///   {
    ///     "formatVersion": 2,
    ///     "title": "Track 1 - Steady Reference",
    ///     "initialBpm": 120.0,
    ///     "initialBeatsPerMeasure": 4,
    ///     "changes": [
    ///       {
    ///         "measure": 5,
    ///         "beat": 2,
    ///         "bpm": 110.0,
    ///         "type": "Tempo",
    ///         "description": "Drop to 110"
    ///       }
    ///     ]
    ///   }
    ///
    /// SUPPORTED TYPE STRINGS:
    ///   Tempo, TimeSignature, Both, Stop, Mute, Unmute, VisualOff, VisualOn, Combined
    ///
    /// USAGE:
    ///   // Load:
    ///   ChangeSequence_v2 seq = CompositionJsonSerializer_v2.Load(jsonText);
    ///
    ///   // Save:
    ///   string json = CompositionJsonSerializer_v2.Save(seq);
    /// </summary>
    public static class CompositionJsonSerializer_v2 {

        // =========================================================
        // PUBLIC API
        // =========================================================

        /// <summary>
        /// Load a v2 track JSON string into a ChangeSequence_v2.
        /// </summary>
        public static ChangeSequence_v2 Load(string json) {
            if (string.IsNullOrWhiteSpace(json)) {
                Debug.LogError("[Serializer_v2] Cannot load: JSON string is empty.");
                return null;
            }

            SequenceRaw raw;
            try {
                raw = JsonUtility.FromJson<SequenceRaw>(json);
            } catch (Exception ex) {
                Debug.LogError($"[Serializer_v2] Failed to parse JSON: {ex.Message}");
                return null;
            }

            if (raw == null) {
                Debug.LogError("[Serializer_v2] JSON parsed to null.");
                return null;
            }

            return BuildSequence(raw);
        }

        /// <summary>
        /// Save a ChangeSequence_v2 to a v2 JSON string.
        /// </summary>
        public static string Save(ChangeSequence_v2 sequence, bool prettyPrint = true) {
            if (sequence == null) {
                Debug.LogError("[Serializer_v2] Cannot save: sequence is null.");
                return null;
            }

            var raw = new SequenceRaw {
                formatVersion = 2,
                title = sequence.title,
                initialBpm = sequence.initialBpm,
                initialBeatsPerMeasure = sequence.initialBeatsPerMeasure,
                changes = new List<ChangeRaw>()
            };

            if (sequence.changes != null) {
                foreach (var change in sequence.changes)
                    raw.changes.Add(ToRaw(change));
            }

            return JsonUtility.ToJson(raw, prettyPrint);
        }

        // =========================================================
        // BUILD ChangeSequence_v2 FROM RAW
        // =========================================================

        private static ChangeSequence_v2 BuildSequence(SequenceRaw raw) {
            var sequence = ScriptableObject.CreateInstance<ChangeSequence_v2>();
            sequence.title = string.IsNullOrEmpty(raw.title) ? "Untitled" : raw.title;
            sequence.initialBpm = raw.initialBpm > 0 ? raw.initialBpm : 120f;
            sequence.initialBeatsPerMeasure = raw.initialBeatsPerMeasure > 0 ? raw.initialBeatsPerMeasure : 4;
            sequence.changes = new List<MetronomeChange_v2>();

            if (raw.changes != null) {
                foreach (var rawChange in raw.changes) {
                    var change = BuildChange(rawChange);
                    if (change != null)
                        sequence.changes.Add(change);
                }
            }

            Debug.Log($"[Serializer_v2] Loaded '{sequence.title}': " +
                      $"{sequence.changes.Count} changes, " +
                      $"{sequence.initialBpm} BPM {sequence.initialBeatsPerMeasure}/4");

            return sequence;
        }

        private static MetronomeChange_v2 BuildChange(ChangeRaw raw) {
            if (raw == null) return null;

            var changeType = ParseType(raw.type);
            MetronomeChange_v2 change;

            switch (changeType) {
                case MetronomeChange_v2.ChangeType.Tempo:
                    change = new MetronomeChange_v2(raw.measure, raw.bpm, raw.description);
                    break;

                case MetronomeChange_v2.ChangeType.TimeSignature:
                    change = new MetronomeChange_v2(raw.measure, raw.beatsPerMeasure, raw.description);
                    break;

                case MetronomeChange_v2.ChangeType.Both:
                    change = new MetronomeChange_v2(raw.measure, raw.bpm, raw.beatsPerMeasure, raw.description);
                    break;

                case MetronomeChange_v2.ChangeType.Stop:
                    change = MetronomeChange_v2.CreateStopEvent(raw.measure, raw.description);
                    break;

                case MetronomeChange_v2.ChangeType.Mute:
                    change = MetronomeChange_v2.CreateMute(raw.measure, raw.description);
                    break;

                case MetronomeChange_v2.ChangeType.Unmute:
                    change = MetronomeChange_v2.CreateUnmute(raw.measure, raw.description);
                    break;

                case MetronomeChange_v2.ChangeType.VisualOff:
                    change = MetronomeChange_v2.CreateVisualOff(raw.measure, raw.description);
                    break;

                case MetronomeChange_v2.ChangeType.VisualOn:
                    change = MetronomeChange_v2.CreateVisualOn(raw.measure, raw.description);
                    break;

                case MetronomeChange_v2.ChangeType.Combined:
                    change = new MetronomeChange_v2(raw.measure, raw.bpm, raw.description);
                    change.type = MetronomeChange_v2.ChangeType.Combined;
                    change.hasTempo = raw.hasTempo;
                    change.hasTimeSignature = raw.hasTimeSignature;
                    change.hasAudioEvent = raw.hasAudioEvent;
                    change.hasVisualEvent = raw.hasVisualEvent;
                    change.newBpm = raw.bpm;
                    change.newBeatsPerMeasure = raw.beatsPerMeasure;
                    change.muteAudio = raw.muteAudio;
                    change.hideVisual = raw.hideVisual;
                    break;

                default:
                    Debug.LogWarning($"[Serializer_v2] Unhandled type '{raw.type}' at M{raw.measure}. Skipping.");
                    return null;
            }

            // Clamp beat to 1 minimum — safety net for missing or zero beat fields
            change.targetBeat = raw.beat < 1 ? 1 : raw.beat;

            return change;
        }

        // =========================================================
        // MetronomeChange_v2 → RAW (for saving)
        // =========================================================

        private static ChangeRaw ToRaw(MetronomeChange_v2 change) {
            return new ChangeRaw {
                measure = change.targetMeasure,
                beat = change.targetBeat < 1 ? 1 : change.targetBeat,
                bpm = change.newBpm,
                beatsPerMeasure = change.newBeatsPerMeasure,
                type = change.type.ToString(),
                description = change.description,
                hasTempo = change.hasTempo,
                hasTimeSignature = change.hasTimeSignature,
                hasAudioEvent = change.hasAudioEvent,
                hasVisualEvent = change.hasVisualEvent,
                muteAudio = change.muteAudio,
                hideVisual = change.hideVisual
            };
        }

        // =========================================================
        // TYPE PARSING
        // =========================================================

        private static MetronomeChange_v2.ChangeType ParseType(string typeStr) {
            switch (typeStr) {
                case "Tempo": return MetronomeChange_v2.ChangeType.Tempo;
                case "TimeSignature": return MetronomeChange_v2.ChangeType.TimeSignature;
                case "Both": return MetronomeChange_v2.ChangeType.Both;
                case "Stop": return MetronomeChange_v2.ChangeType.Stop;
                case "Mute": return MetronomeChange_v2.ChangeType.Mute;
                case "Unmute": return MetronomeChange_v2.ChangeType.Unmute;
                case "VisualOff": return MetronomeChange_v2.ChangeType.VisualOff;
                case "VisualOn": return MetronomeChange_v2.ChangeType.VisualOn;
                case "Combined": return MetronomeChange_v2.ChangeType.Combined;
                default:
                    Debug.LogWarning($"[Serializer_v2] Unknown type string '{typeStr}'. Defaulting to Tempo.");
                    return MetronomeChange_v2.ChangeType.Tempo;
            }
        }

        // =========================================================
        // RAW DATA CLASSES — JsonUtility parsing only
        // =========================================================

        [Serializable]
        private class SequenceRaw {
            public int formatVersion = 2;
            public string title = "";
            public float initialBpm = 120f;
            public int initialBeatsPerMeasure = 4;
            public List<ChangeRaw> changes = new List<ChangeRaw>();
        }

        [Serializable]
        private class ChangeRaw {
            public int measure = 1;
            public int beat = 1;
            public float bpm = 0f;
            public int beatsPerMeasure = 0;
            public string type = "Tempo";
            public string description = "";
            // Combined change flags
            public bool hasTempo = false;
            public bool hasTimeSignature = false;
            public bool hasAudioEvent = false;
            public bool hasVisualEvent = false;
            public bool muteAudio = false;
            public bool hideVisual = false;
        }
    }
}