using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChangeComposer.Data {
    /// <summary>
    /// MetronomeChange v2 - Beat-Level Scheduling Enhancement
    /// Version: 2025-01-10 v2
    /// 
    /// NEW FEATURE: targetBeat field enables beat-level scheduling
    /// - Defaults to 1 for backward compatibility (measure start)
    /// - Enables smooth tempo ramps via beat-level interpolation
    /// - All existing functionality preserved
    /// </summary>
    [System.Serializable]
    public class MetronomeChange_v2 {
        public enum ChangeType {
            Tempo,
            TimeSignature,
            Both,
            Stop,           // End of piece
            Mute,           // Audio off, timing continues
            Unmute,         // Audio on
            VisualOff,      // Hide visual indicators
            VisualOn,       // Show visual indicators
            Combined        // Multiple changes at same measure
        }

        // Core properties
        public ChangeType type;
        public int targetMeasure;

        // ✨ NEW: Beat-level scheduling support
        public int targetBeat = 1;  // Defaults to 1 (measure start) for backward compatibility

        public float newBpm;
        public int newBeatsPerMeasure;
        public bool isProcessed = false;
        public string description = "";

        // Enhanced properties
        [Header("Change Capabilities")]
        public bool hasTempo = false;
        public bool hasTimeSignature = false;
        public bool hasAudioEvent = false;
        public bool hasVisualEvent = false;

        // Audio/Visual control
        [Header("Audio/Visual Control")]
        public bool muteAudio = false;
        public bool hideVisual = false;

        // Enhanced notification system
        [Header("Notification Settings")]
        public bool enableNotifications = false;
        public int advanceNotificationMeasures = 1;
        public string customNotificationMessage = "";
        public bool isUrgent = false;

        // Internal notification tracking
        private bool notificationSent = false;
        private int lastNotificationMeasure = -1;

        // === BASIC CONSTRUCTORS ===

        /// <summary>
        /// Tempo change (defaults to measure start, beat 1)
        /// </summary>
        public MetronomeChange_v2(int measure, float bpm, string desc = "") {
            targetMeasure = measure;
            targetBeat = 1;  // Default to measure start
            newBpm = bpm;
            type = ChangeType.Tempo;
            description = desc;
            hasTempo = true;
        }

        /// <summary>
        /// Time signature change (defaults to measure start, beat 1)
        /// </summary>
        public MetronomeChange_v2(int measure, int beatsPerMeasure, string desc = "") {
            targetMeasure = measure;
            targetBeat = 1;  // Default to measure start
            newBeatsPerMeasure = beatsPerMeasure;
            type = ChangeType.TimeSignature;
            description = desc;
            hasTimeSignature = true;
        }

        /// <summary>
        /// Both tempo and time signature (defaults to measure start, beat 1)
        /// </summary>
        public MetronomeChange_v2(int measure, float bpm, int beatsPerMeasure, string desc = "") {
            targetMeasure = measure;
            targetBeat = 1;  // Default to measure start
            newBpm = bpm;
            newBeatsPerMeasure = beatsPerMeasure;
            type = ChangeType.Both;
            description = desc;
            hasTempo = true;
            hasTimeSignature = true;
        }

        // ✨ NEW: Beat-level scheduling constructors

        /// <summary>
        /// Tempo change at specific beat
        /// </summary>
        public MetronomeChange_v2(int measure, int beat, float bpm, string desc = "") {
            targetMeasure = measure;
            targetBeat = beat;
            newBpm = bpm;
            type = ChangeType.Tempo;
            description = desc;
            hasTempo = true;
        }

        /// <summary>
        /// Time signature change at specific beat
        /// </summary>
        public MetronomeChange_v2(int measure, int beat, int beatsPerMeasure, string desc = "") {
            targetMeasure = measure;
            targetBeat = beat;
            newBeatsPerMeasure = beatsPerMeasure;
            type = ChangeType.TimeSignature;
            description = desc;
            hasTimeSignature = true;
        }

        /// <summary>
        /// Both tempo and time signature at specific beat
        /// </summary>
        public MetronomeChange_v2(int measure, int beat, float bpm, int beatsPerMeasure, string desc = "") {
            targetMeasure = measure;
            targetBeat = beat;
            newBpm = bpm;
            newBeatsPerMeasure = beatsPerMeasure;
            type = ChangeType.Both;
            description = desc;
            hasTempo = true;
            hasTimeSignature = true;
        }

        // Enhanced constructors with notification settings
        public MetronomeChange_v2(int measure, float bpm, bool enableNotif, int advanceMeasures = 1, string customMessage = "") {
            targetMeasure = measure;
            targetBeat = 1;
            newBpm = bpm;
            type = ChangeType.Tempo;
            hasTempo = true;
            enableNotifications = enableNotif;
            advanceNotificationMeasures = advanceMeasures;
            customNotificationMessage = customMessage;
        }

        public MetronomeChange_v2(int measure, int beatsPerMeasure, bool enableNotif, int advanceMeasures = 1, string customMessage = "") {
            targetMeasure = measure;
            targetBeat = 1;
            newBeatsPerMeasure = beatsPerMeasure;
            type = ChangeType.TimeSignature;
            hasTimeSignature = true;
            enableNotifications = enableNotif;
            advanceNotificationMeasures = advanceMeasures;
            customNotificationMessage = customMessage;
        }

        public MetronomeChange_v2(int measure, float bpm, int beatsPerMeasure, bool enableNotif, int advanceMeasures = 1, string customMessage = "") {
            targetMeasure = measure;
            targetBeat = 1;
            newBpm = bpm;
            newBeatsPerMeasure = beatsPerMeasure;
            type = ChangeType.Both;
            hasTempo = true;
            hasTimeSignature = true;
            enableNotifications = enableNotif;
            advanceNotificationMeasures = advanceMeasures;
            customNotificationMessage = customMessage;
        }

        // === FACTORY METHODS FOR AUDIO/VISUAL EVENTS ===

        public static MetronomeChange_v2 CreateStopEvent(int measure, string desc = "") {
            var change = new MetronomeChange_v2(measure, 120f); // Dummy tempo
            change.type = ChangeType.Stop;
            change.description = desc;
            change.hasTempo = false;
            return change;
        }

        public static MetronomeChange_v2 CreateMute(int measure, string desc = "") {
            var change = new MetronomeChange_v2(measure, 120f); // Dummy tempo
            change.type = ChangeType.Mute;
            change.description = desc;
            change.hasAudioEvent = true;
            change.muteAudio = true;
            change.hasTempo = false;
            return change;
        }

        public static MetronomeChange_v2 CreateUnmute(int measure, string desc = "") {
            var change = new MetronomeChange_v2(measure, 120f); // Dummy tempo
            change.type = ChangeType.Unmute;
            change.description = desc;
            change.hasAudioEvent = true;
            change.muteAudio = false;
            change.hasTempo = false;
            return change;
        }

        public static MetronomeChange_v2 CreateVisualOff(int measure, string desc = "") {
            var change = new MetronomeChange_v2(measure, 120f); // Dummy tempo
            change.type = ChangeType.VisualOff;
            change.description = desc;
            change.hasVisualEvent = true;
            change.hideVisual = true;
            change.hasTempo = false;
            return change;
        }

        public static MetronomeChange_v2 CreateVisualOn(int measure, string desc = "") {
            var change = new MetronomeChange_v2(measure, 120f); // Dummy tempo
            change.type = ChangeType.VisualOn;
            change.description = desc;
            change.hasVisualEvent = true;
            change.hideVisual = false;
            change.hasTempo = false;
            return change;
        }

        public static MetronomeChange_v2 CreateStop(int measure, string desc = "") {
            return CreateStopEvent(measure, desc);
        }

        public static MetronomeChange_v2 CreateAudioEvent(int measure, bool mute, string desc = "") {
            return mute ? CreateMute(measure, desc) : CreateUnmute(measure, desc);
        }

        public static MetronomeChange_v2 CreateVisualEvent(int measure, bool hide, string desc = "") {
            return hide ? CreateVisualOff(measure, desc) : CreateVisualOn(measure, desc);
        }

        /// <summary>
        /// Create a combined change (multiple events at same measure)
        /// </summary>
        public static MetronomeChange_v2 CreateCombined(int measure, string desc = "") {
            var change = new MetronomeChange_v2(measure, 120f); // Default values
            change.type = ChangeType.Combined;
            change.description = desc;
            change.hasTempo = false; // Will be set by builder methods
            return change;
        }

        // === BUILDER PATTERN METHODS ===

        public MetronomeChange_v2 WithTempo(float bpm) {
            newBpm = bpm;
            hasTempo = true;
            if (type == ChangeType.TimeSignature) type = ChangeType.Both;
            else if (type != ChangeType.Both && type != ChangeType.Combined) type = ChangeType.Tempo;
            return this;
        }

        public MetronomeChange_v2 WithTimeSignature(int beatsPerMeasure) {
            newBeatsPerMeasure = beatsPerMeasure;
            hasTimeSignature = true;
            if (type == ChangeType.Tempo) type = ChangeType.Both;
            else if (type != ChangeType.Both && type != ChangeType.Combined) type = ChangeType.TimeSignature;
            return this;
        }

        public MetronomeChange_v2 WithAudio(bool mute) {
            hasAudioEvent = true;
            muteAudio = mute;
            if (type != ChangeType.Combined) type = ChangeType.Combined;
            return this;
        }

        public MetronomeChange_v2 WithVisual(bool hide) {
            hasVisualEvent = true;
            hideVisual = hide;
            if (type != ChangeType.Combined) type = ChangeType.Combined;
            return this;
        }

        public MetronomeChange_v2 WithNotification(int advanceMeasures = 1, bool urgent = false, string customMessage = "") {
            enableNotifications = true;
            advanceNotificationMeasures = advanceMeasures;
            isUrgent = urgent;
            customNotificationMessage = customMessage;
            return this;
        }

        // ✨ NEW: Builder method for beat-level scheduling
        public MetronomeChange_v2 AtBeat(int beat) {
            targetBeat = beat;
            return this;
        }

        // === NOTIFICATION METHODS ===

        public bool ShouldNotifyAtMeasure(int currentMeasure) {
            if (!enableNotifications || notificationSent) return false;

            int notificationMeasure = targetMeasure - advanceNotificationMeasures;
            return currentMeasure >= notificationMeasure && currentMeasure < targetMeasure;
        }

        public string GetNotificationMessage(int currentMeasure) {
            if (!string.IsNullOrEmpty(customNotificationMessage)) {
                return customNotificationMessage;
            }

            int measuresUntilChange = targetMeasure - currentMeasure;
            string baseMessage = GetChangeDescription();

            if (measuresUntilChange > 0) {
                baseMessage += $" in {measuresUntilChange} measure{(measuresUntilChange != 1 ? "s" : "")}";
            } else {
                baseMessage += " NOW";
            }

            return baseMessage;
        }

        public void MarkNotificationSent(int atMeasure) {
            notificationSent = true;
            lastNotificationMeasure = atMeasure;
        }

        public void ResetNotificationState() {
            notificationSent = false;
            lastNotificationMeasure = -1;
        }

        public int GetNotificationMeasure() {
            return targetMeasure - advanceNotificationMeasures;
        }

        // === DESCRIPTION METHODS ===

        public string GetChangeDescription() {
            string baseDesc = GetBaseChangeDescription();

            // Add beat info if not beat 1 (i.e., if it's a beat-level change)
            if (targetBeat != 1) {
                baseDesc += $" at beat {targetBeat}";
            }

            return baseDesc;
        }

        private string GetBaseChangeDescription() {
            switch (type) {
                case ChangeType.Tempo:
                    return $"Tempo to {newBpm} BPM";
                case ChangeType.TimeSignature:
                    return $"Time signature to {newBeatsPerMeasure}/4";
                case ChangeType.Both:
                    return $"Tempo to {newBpm} BPM, {newBeatsPerMeasure}/4";
                case ChangeType.Stop:
                    return "Stop (end of piece)";
                case ChangeType.Mute:
                    return "Mute audio";
                case ChangeType.Unmute:
                    return "Unmute audio";
                case ChangeType.VisualOff:
                    return "Hide visual indicators";
                case ChangeType.VisualOn:
                    return "Show visual indicators";
                case ChangeType.Combined:
                    return GetCombinedDescription();
                default:
                    return "Unknown change";
            }
        }

        private string GetCombinedDescription() {
            var parts = new List<string>();

            if (hasTempo) parts.Add($"Tempo to {newBpm} BPM");
            if (hasTimeSignature) parts.Add($"Time signature to {newBeatsPerMeasure}/4");
            if (hasAudioEvent) parts.Add(muteAudio ? "Mute audio" : "Unmute audio");
            if (hasVisualEvent) parts.Add(hideVisual ? "Hide visual" : "Show visual");

            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "Combined change";
        }

        /// <summary>
        /// Create a copy of this change
        /// </summary>
        public MetronomeChange_v2 Clone() {
            var clone = new MetronomeChange_v2(targetMeasure, newBpm, newBeatsPerMeasure, enableNotifications, advanceNotificationMeasures, customNotificationMessage);
            clone.targetBeat = targetBeat;  // Copy beat info
            clone.type = type;
            clone.description = description;
            clone.isUrgent = isUrgent;
            clone.isProcessed = isProcessed;
            clone.notificationSent = notificationSent;
            clone.lastNotificationMeasure = lastNotificationMeasure;

            // Copy enhanced properties
            clone.hasTempo = hasTempo;
            clone.hasTimeSignature = hasTimeSignature;
            clone.hasAudioEvent = hasAudioEvent;
            clone.hasVisualEvent = hasVisualEvent;
            clone.muteAudio = muteAudio;
            clone.hideVisual = hideVisual;

            return clone;
        }

        public override string ToString() {
            string baseString = $"[M{targetMeasure} B{targetBeat}] {GetChangeDescription()}";

            if (enableNotifications) {
                baseString += $" (Notification: {advanceNotificationMeasures} measures advance)";
            }

            if (!string.IsNullOrEmpty(description)) {
                baseString += $" - {description}";
            }

            if (isUrgent) {
                baseString += " [URGENT]";
            }

            return baseString;
        }
    }
}