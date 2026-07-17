using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChangeComposer.Data {
    /// <summary>
    /// Streamlined ChangeSequence - just title, changes, and save/load
    /// Removed metadata complexity for testing focus
    /// </summary>
    [CreateAssetMenu(fileName = "New Change Sequence", menuName = "ChangeComposer/Change Sequence")]
    public class ChangeSequence : ScriptableObject {
        [Header("Basic Info")]
        public string title = "New Composition";

        [Header("Initial Settings")]
        public float initialBpm = 120f;
        public int initialBeatsPerMeasure = 4;

        [Header("Changes")]
        public List<MetronomeChange> changes = new List<MetronomeChange>();

        /// <summary>
        /// Add a change to the sequence
        /// </summary>
        public void AddChange(MetronomeChange change) {
            if (change == null) return;

            changes.Add(change);
            SortChangesByMeasure();
        }

        /// <summary>
        /// Remove a change by index
        /// </summary>
        public void RemoveChange(int index) {
            if (index >= 0 && index < changes.Count) {
                changes.RemoveAt(index);
            }
        }

        /// <summary>
        /// Clear all changes
        /// </summary>
        public void ClearChanges() {
            changes.Clear();
        }

        /// <summary>
        /// Get all changes sorted by measure
        /// </summary>
        public List<MetronomeChange> GetSortedChanges() {
            var sorted = new List<MetronomeChange>(changes);
            sorted.Sort((a, b) => a.targetMeasure.CompareTo(b.targetMeasure));
            return sorted;
        }

        /// <summary>
        /// Sort the changes list by measure number
        /// </summary>
        public void SortChangesByMeasure() {
            changes.Sort((a, b) => a.targetMeasure.CompareTo(b.targetMeasure));
        }

        /// <summary>
        /// Basic validation - just check for obvious errors
        /// </summary>
        public bool IsValid() {
            foreach (var change in changes) {
                if (change.targetMeasure <= 0) return false;
                if (change.type == MetronomeChange.ChangeType.Tempo && change.newBpm <= 0) return false;
                if (change.type == MetronomeChange.ChangeType.TimeSignature && change.newBeatsPerMeasure <= 0) return false;
            }
            return true;
        }

        /// <summary>
        /// Export to JSON string
        /// </summary>
        public string ToJSON() {
            return JsonUtility.ToJson(this, true);
        }

        /// <summary>
        /// Load from JSON string
        /// </summary>
        public static ChangeSequence FromJSON(string json) {
            var sequence = CreateInstance<ChangeSequence>();
            JsonUtility.FromJsonOverwrite(json, sequence);
            return sequence;
        }

        /// <summary>
        /// Get simple text representation for display
        /// </summary>
        public string GetDisplayText() {
            var text = new System.Text.StringBuilder();
            text.AppendLine($"=== {title} ===");
            text.AppendLine($"Initial: {initialBpm} BPM, {initialBeatsPerMeasure}/4");
            text.AppendLine();

            if (changes.Count == 0) {
                text.AppendLine("No changes");
            } else {
                var sortedChanges = GetSortedChanges();
                for (int i = 0; i < sortedChanges.Count; i++) {
                    var change = sortedChanges[i];
                    text.AppendLine($"{i + 1:D2}. M{change.targetMeasure}: {change.GetChangeDescription()}");
                    if (!string.IsNullOrEmpty(change.description))
                        text.AppendLine($"    \"{change.description}\"");
                }

                text.AppendLine();
                text.AppendLine($"Total: {changes.Count} changes");
            }

            return text.ToString();
        }
    }
}