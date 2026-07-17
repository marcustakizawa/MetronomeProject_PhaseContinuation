using System.Collections.Generic;
using UnityEngine;
using ChangeComposer.Data;
using ChangeComposer.Serialization;

namespace ChangeComposer.Data {

    /// <summary>
    /// ChangeSequence_v2
    /// Version: 2026-03-20 v2
    ///
    /// PURPOSE:
    ///   Holds a complete track sequence — initial settings plus all scheduled
    ///   changes — using MetronomeChange_v2 throughout.
    ///
    /// DIFFERENCES FROM ChangeSequence (v1):
    ///   - changes list is List&lt;MetronomeChange_v2&gt; (was MetronomeChange)
    ///   - FromJSON / ToJSON delegate to CompositionJsonSerializer_v2
    ///   - GetSortedChanges sorts by measure AND beat
    ///   - IsValid checks targetBeat >= 1 in addition to existing checks
    ///
    /// USED BY:
    ///   CompositionCoordinator_v2 (loads sequences, builds indexes)
    ///   CompositionEditorController (editor scene, Step 7)
    ///   CompositionJsonSerializer_v2 (creates instances via CreateInstance)
    /// </summary>
    [CreateAssetMenu(fileName = "New Change Sequence v2", menuName = "ChangeComposer/Change Sequence v2")]
    public class ChangeSequence_v2 : ScriptableObject {

        [Header("Basic Info")]
        public string title = "New Composition";

        [Header("Initial Settings")]
        public float initialBpm = 120f;
        public int initialBeatsPerMeasure = 4;

        [Header("Changes")]
        public List<MetronomeChange_v2> changes = new List<MetronomeChange_v2>();

        // =========================================================
        // CHANGE MANAGEMENT
        // =========================================================

        /// <summary>Add a change and re-sort by measure then beat.</summary>
        public void AddChange(MetronomeChange_v2 change) {
            if (change == null) return;
            changes.Add(change);
            SortChanges();
        }

        /// <summary>Remove a change by index.</summary>
        public void RemoveChange(int index) {
            if (index >= 0 && index < changes.Count)
                changes.RemoveAt(index);
        }

        /// <summary>Clear all changes.</summary>
        public void ClearChanges() {
            changes.Clear();
        }

        /// <summary>
        /// Return a sorted copy of the changes list.
        /// Sorted by measure first, then by beat within the same measure.
        /// </summary>
        public List<MetronomeChange_v2> GetSortedChanges() {
            var sorted = new List<MetronomeChange_v2>(changes);
            sorted.Sort((a, b) => {
                int measureCompare = a.targetMeasure.CompareTo(b.targetMeasure);
                return measureCompare != 0
                    ? measureCompare
                    : a.targetBeat.CompareTo(b.targetBeat);
            });
            return sorted;
        }

        /// <summary>Sort the internal changes list by measure then beat.</summary>
        public void SortChanges() {
            changes.Sort((a, b) => {
                int measureCompare = a.targetMeasure.CompareTo(b.targetMeasure);
                return measureCompare != 0
                    ? measureCompare
                    : a.targetBeat.CompareTo(b.targetBeat);
            });
        }

        // =========================================================
        // VALIDATION
        // =========================================================

        /// <summary>
        /// Basic sanity check. Returns true if all changes have valid
        /// measure numbers, beat numbers, and required values for their type.
        /// </summary>
        public bool IsValid() {
            foreach (var change in changes) {
                if (change.targetMeasure <= 0) return false;
                if (change.targetBeat < 1) return false;

                if (change.type == MetronomeChange_v2.ChangeType.Tempo &&
                    change.newBpm <= 0) return false;

                if (change.type == MetronomeChange_v2.ChangeType.TimeSignature &&
                    change.newBeatsPerMeasure <= 0) return false;
            }
            return true;
        }

        /// <summary>Check if the sequence has any changes.</summary>
        public bool HasChanges() => changes != null && changes.Count > 0;

        // =========================================================
        // SERIALIZATION
        // =========================================================

        /// <summary>Export to v2 JSON string via CompositionJsonSerializer_v2.</summary>
        public string ToJSON(bool prettyPrint = true) {
            return CompositionJsonSerializer_v2.Save(this, prettyPrint);
        }

        /// <summary>
        /// Load a v2 JSON string into a new ChangeSequence_v2 instance.
        /// Called by CompositionCoordinator_v2 and the editor controller.
        /// </summary>
        public static ChangeSequence_v2 FromJSON(string json) {
            return CompositionJsonSerializer_v2.Load(json);
        }

        // =========================================================
        // DISPLAY
        // =========================================================

        /// <summary>Simple text summary for debugging and log display.</summary>
        public string GetDisplayText() {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== {title} ===");
            sb.AppendLine($"Initial: {initialBpm} BPM, {initialBeatsPerMeasure}/4");
            sb.AppendLine();

            if (!HasChanges()) {
                sb.AppendLine("No changes");
            } else {
                var sorted = GetSortedChanges();
                for (int i = 0; i < sorted.Count; i++) {
                    var change = sorted[i];
                    sb.AppendLine($"{i + 1:D2}. M{change.targetMeasure} B{change.targetBeat}: " +
                                  $"{change.GetChangeDescription()}");
                    if (!string.IsNullOrEmpty(change.description))
                        sb.AppendLine($"    \"{change.description}\"");
                }
                sb.AppendLine();
                sb.AppendLine($"Total: {changes.Count} change{(changes.Count != 1 ? "s" : "")}");
            }

            return sb.ToString();
        }
    }
}