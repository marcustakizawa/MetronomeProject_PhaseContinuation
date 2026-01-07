using System.Collections.Generic;
using UnityEngine;
using ChangeComposer.Data;

namespace ChangeComposer.Indexing {
    /// <summary>
    /// Represents the complete state of a single metronome at a specific measure
    /// </summary>
    [System.Serializable]
    public class MeasureState {
        public int measureNumber;
        public float bpm;
        public int beatsPerMeasure;
        public bool isAudioMuted;
        public bool areVisualsHidden;
        public bool shouldStop;

        // For debugging/display
        public string appliedChanges = ""; // What changes were applied at this measure

        public MeasureState(int measure) {
            measureNumber = measure;
            // Default initial state
            bpm = 120f;
            beatsPerMeasure = 4;
            isAudioMuted = false;
            areVisualsHidden = false;
            shouldStop = false;
        }

        public MeasureState Clone() {
            var clone = new MeasureState(measureNumber);
            clone.bpm = bpm;
            clone.beatsPerMeasure = beatsPerMeasure;
            clone.isAudioMuted = isAudioMuted;
            clone.areVisualsHidden = areVisualsHidden;
            clone.shouldStop = shouldStop;
            clone.appliedChanges = appliedChanges;
            return clone;
        }

        public override string ToString() {
            var status = new List<string>();
            if (isAudioMuted) status.Add("MUTED");
            if (areVisualsHidden) status.Add("NO_VISUAL");
            if (shouldStop) status.Add("STOP");

            string statusText = status.Count > 0 ? $" [{string.Join(", ", status)}]" : "";
            return $"M{measureNumber}: {bpm} BPM, {beatsPerMeasure}/4{statusText}";
        }
    }

    /// <summary>
    /// Complete index of all measure states for a composition
    /// Phase 1: Single metronome only
    /// </summary>
    [System.Serializable]
    public class CompositionIndex {
        public string compositionTitle;
        public int totalMeasures;
        public List<MeasureState> measureStates = new List<MeasureState>();

        // Quick lookup dictionary (not serialized)
        private Dictionary<int, MeasureState> measureLookup;

        public void BuildLookupTable() {
            measureLookup = new Dictionary<int, MeasureState>();
            foreach (var state in measureStates) {
                measureLookup[state.measureNumber] = state;
            }
        }

        /// <summary>
        /// Get the state at a specific measure
        /// </summary>
        public MeasureState GetStateAtMeasure(int measureNumber) {
            if (measureLookup == null) BuildLookupTable();

            if (measureLookup.ContainsKey(measureNumber)) {
                return measureLookup[measureNumber];
            }

            Debug.LogWarning($"Measure {measureNumber} not found in index");
            return null;
        }

        /// <summary>
        /// Check if a measure exists in the index
        /// </summary>
        public bool HasMeasure(int measureNumber) {
            if (measureLookup == null) BuildLookupTable();
            return measureLookup.ContainsKey(measureNumber);
        }

        /// <summary>
        /// Get valid measure range for UI validation
        /// </summary>
        public (int min, int max) GetMeasureRange() {
            if (measureStates.Count == 0) return (1, 1);

            int min = int.MaxValue;
            int max = int.MinValue;

            foreach (var state in measureStates) {
                if (state.measureNumber < min) min = state.measureNumber;
                if (state.measureNumber > max) max = state.measureNumber;
            }

            return (min, max);
        }
    }

    /// <summary>
    /// Generates composition indexes from ChangeSequence data
    /// Phase 1: Single metronome support
    /// </summary>
    public static class CompositionIndexGenerator {
        /// <summary>
        /// Generate a complete measure index from a ChangeSequence
        /// </summary>
        public static CompositionIndex GenerateIndex(ChangeSequence sequence, int maxMeasures = 100) {
            var index = new CompositionIndex();
            index.compositionTitle = sequence.title;
            index.totalMeasures = maxMeasures;

            Debug.Log($"Generating index for '{sequence.title}' - {maxMeasures} measures");

            // Get initial state from sequence
            var currentState = new MeasureState(1);
            currentState.bpm = sequence.initialBpm;
            currentState.beatsPerMeasure = sequence.initialBeatsPerMeasure;

            // Get all changes sorted by measure
            var sortedChanges = sequence.GetSortedChanges();
            int changeIndex = 0;

            // Generate state for each measure
            for (int measure = 1; measure <= maxMeasures; measure++) {
                // Create state for this measure (start with previous state)
                var measureState = currentState.Clone();
                measureState.measureNumber = measure;
                measureState.appliedChanges = "";

                // Apply any changes that occur at this measure
                var appliedChangesList = new List<string>();
                while (changeIndex < sortedChanges.Count && sortedChanges[changeIndex].targetMeasure == measure) {
                    var change = sortedChanges[changeIndex];
                    ApplyChangeToState(measureState, change);
                    appliedChangesList.Add(GetChangeDescription(change));
                    changeIndex++;
                }

                if (appliedChangesList.Count > 0) {
                    measureState.appliedChanges = string.Join(", ", appliedChangesList);
                    Debug.Log($"M{measure}: Applied {appliedChangesList.Count} changes - {measureState.appliedChanges}");
                }

                // Add to index
                index.measureStates.Add(measureState);

                // Update current state for next measure
                currentState = measureState;

                // Stop if we hit a stop event
                if (measureState.shouldStop) {
                    Debug.Log($"Stop event at M{measure} - ending index generation");
                    index.totalMeasures = measure;
                    break;
                }
            }

            Debug.Log($"Index generation complete: {index.measureStates.Count} measures indexed");
            return index;
        }

        /// <summary>
        /// Apply a single change to a measure state
        /// </summary>
        private static void ApplyChangeToState(MeasureState state, MetronomeChange change) {

            Debug.Log($"APPLYING: M{state.measureNumber} - Type: {change.type} (enum value: {(int)change.type})");
            switch (change.type) {
                case MetronomeChange.ChangeType.Tempo:
                    state.bpm = change.newBpm;
                    break;

                case MetronomeChange.ChangeType.TimeSignature:
                    state.beatsPerMeasure = change.newBeatsPerMeasure;
                    Debug.Log($"  -> New beats per measure: {state.beatsPerMeasure}");
                    break;

                case MetronomeChange.ChangeType.Both:
                    state.bpm = change.newBpm;
                    state.beatsPerMeasure = change.newBeatsPerMeasure;
                    Debug.Log($"  -> New beats per measure and new bpm: {state.beatsPerMeasure},{state.bpm}");
                    break;

                case MetronomeChange.ChangeType.Mute:
                    state.isAudioMuted = true;
                    Debug.Log($"  -> Audio muted: {state.isAudioMuted}");
                    break;

                case MetronomeChange.ChangeType.Unmute:
                    state.isAudioMuted = false;
                    Debug.Log($"  -> Audio un-muted: {state.isAudioMuted}");
                    break;

                case MetronomeChange.ChangeType.VisualOff:
                    state.areVisualsHidden = true;
                    Debug.Log($"  -> Visuals off: {state.areVisualsHidden}");
                    break;

                case MetronomeChange.ChangeType.VisualOn:
                    state.areVisualsHidden = false;
                    Debug.Log($"  -> Visuals on: {state.areVisualsHidden}");
                    break;

                case MetronomeChange.ChangeType.Stop:
                    state.shouldStop = true;
                    Debug.Log($"  -> Stop metronome: {state.shouldStop}");
                    break;

                case MetronomeChange.ChangeType.Combined:
                    // Handle combined changes
                    if (change.hasTempo) state.bpm = change.newBpm;
                    if (change.hasTimeSignature) state.beatsPerMeasure = change.newBeatsPerMeasure;
                    if (change.hasAudioEvent) state.isAudioMuted = change.muteAudio;
                    if (change.hasVisualEvent) state.areVisualsHidden = change.hideVisual;
                    break;

                default:
                    Debug.LogWarning($"Unknown change type: {change.type}");
                    break;
            }
        }

        /// <summary>
        /// Get a human-readable description of a change for debugging
        /// </summary>
        private static string GetChangeDescription(MetronomeChange change) {
            switch (change.type) {
                case MetronomeChange.ChangeType.Tempo:
                    return $"T{change.newBpm}";
                case MetronomeChange.ChangeType.TimeSignature:
                    return $"TS{change.newBeatsPerMeasure}/4";
                case MetronomeChange.ChangeType.Both:
                    return $"T{change.newBpm}+TS{change.newBeatsPerMeasure}/4";
                case MetronomeChange.ChangeType.Mute:
                    return "MUTE";
                case MetronomeChange.ChangeType.Unmute:
                    return "UNMUTE";
                case MetronomeChange.ChangeType.VisualOff:
                    return "VIS_OFF";
                case MetronomeChange.ChangeType.VisualOn:
                    return "VIS_ON";
                case MetronomeChange.ChangeType.Stop:
                    return "STOP";
                case MetronomeChange.ChangeType.Combined:
                    var parts = new List<string>();
                    if (change.hasTempo) parts.Add($"T{change.newBpm}");
                    if (change.hasTimeSignature) parts.Add($"TS{change.newBeatsPerMeasure}/4");
                    if (change.hasAudioEvent) parts.Add(change.muteAudio ? "MUTE" : "UNMUTE");
                    if (change.hasVisualEvent) parts.Add(change.hideVisual ? "VIS_OFF" : "VIS_ON");
                    return string.Join("+", parts);
                default:
                    return change.type.ToString();
            }
        }
    }
}