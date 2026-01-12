using System;
using System.Collections.Generic;
using UnityEngine;
using ChangeComposer.Data;

/// <summary>
/// MetronomeScheduler v2 - Beat-Level Scheduling Enhancement
/// Version: 2025-01-10 v2
/// 
/// NEW FEATURE: HandleBeatChanged() enables beat-level change checking
/// - CheckForPendingChanges() now checks both measure AND beat
/// - Backward compatible: measure-level changes still work (targetBeat = 1)
/// - Enables smooth tempo ramps via beat-level interpolation
/// 
/// MAINTAINS: All existing functionality from v1
/// </summary>
public class MetronomeScheduler_v2 : MonoBehaviour {

    [Header("Debug Settings")]
    [SerializeField] private bool debugChangeSystem = true;
    [SerializeField] private bool verboseLogging = false;

    // === EVENTS FOR COMMUNICATION ===

    /// <summary>
    /// Fired when a change is ready to be applied to the metronome
    /// </summary>
    public event Action<MetronomeChange_v2> OnChangeReadyToApply;

    /// <summary>
    /// Fired when a change notification should be displayed
    /// </summary>
    public event Action<MetronomeChange_v2, string> OnChangeNotification;

    /// <summary>
    /// Fired when a change is successfully scheduled
    /// </summary>
    public event Action<MetronomeChange_v2> OnChangeScheduled;

    /// <summary>
    /// Fired after a change has been processed
    /// </summary>
    public event Action<MetronomeChange_v2> OnChangeProcessed;

    // === INTERNAL STATE ===

    /// <summary>
    /// All pending changes, sorted by target measure and beat
    /// </summary>
    private List<MetronomeChange_v2> pendingChanges = new List<MetronomeChange_v2>();

    /// <summary>
    /// Current measure and beat for change processing
    /// </summary>
    private int currentMeasure = 1;
    private int currentBeat = 1;

    // === PUBLIC API (Enhanced from v1) ===

    /// <summary>
    /// Schedule a change to occur at a specific measure (and optionally beat)
    /// </summary>
    public void ScheduleChange(MetronomeChange_v2 change) {
        if (change == null) {
            LogError("Cannot schedule null change");
            return;
        }

        // Check if change is in the past
        if (change.targetMeasure < currentMeasure ||
            (change.targetMeasure == currentMeasure && change.targetBeat < currentBeat)) {
            LogWarning($"Cannot schedule change for M{change.targetMeasure} B{change.targetBeat} - we're already at M{currentMeasure} B{currentBeat}");
            return;
        }

        // Add to pending changes
        pendingChanges.Add(change);
        SortPendingChanges();

        if (debugChangeSystem) {
            LogDebug($"Scheduled change: {change}");
        }

        // Fire event
        OnChangeScheduled?.Invoke(change);
    }

    /// <summary>
    /// Clear all pending changes
    /// </summary>
    public void ClearPendingChanges() {
        int clearedCount = pendingChanges.Count;
        pendingChanges.Clear();

        if (debugChangeSystem && clearedCount > 0) {
            LogDebug($"Cleared {clearedCount} pending changes");
        }
    }

    /// <summary>
    /// Get copy of all pending changes
    /// </summary>
    public List<MetronomeChange_v2> GetPendingChanges() {
        return new List<MetronomeChange_v2>(pendingChanges);
    }

    /// <summary>
    /// Called by metronome when measure changes - check for pending changes
    /// This maintains backward compatibility with measure-level scheduling
    /// </summary>
    public void HandleMeasureChanged(int newMeasure) {
        currentMeasure = newMeasure;
        currentBeat = 1;  // Reset to beat 1 at start of new measure
        CheckForPendingChanges(newMeasure, 1);
    }

    /// <summary>
    /// ✨ NEW: Called by metronome when beat changes - enables beat-level scheduling
    /// This is the key enhancement for smooth tempo ramps
    /// </summary>
    public void HandleBeatChanged(int measure, int beat) {
        currentMeasure = measure;
        currentBeat = beat;
        CheckForPendingChanges(measure, beat);
    }

    /// <summary>
    /// Reset notification states (for use with reset operations)
    /// </summary>
    public void ResetNotificationStates() {
        foreach (var change in pendingChanges) {
            change.ResetNotificationState();
        }

        if (debugChangeSystem) {
            LogDebug("Reset all notification states");
        }
    }

    /// <summary>
    /// Set current measure and beat (for jumping scenarios)
    /// </summary>
    public void SetCurrentPosition(int measure, int beat = 1) {
        currentMeasure = measure;
        currentBeat = beat;
    }

    // === CONVENIENCE SCHEDULING METHODS ===

    /// <summary>
    /// Schedule a tempo change at measure start (backward compatible)
    /// </summary>
    public void ScheduleTempoChange(int targetMeasure, float newBpm, string description = "") {
        ScheduleChange(new MetronomeChange_v2(targetMeasure, newBpm, description));
    }

    /// <summary>
    /// ✨ NEW: Schedule a tempo change at specific beat
    /// </summary>
    public void ScheduleTempoChangeAtBeat(int targetMeasure, int targetBeat, float newBpm, string description = "") {
        ScheduleChange(new MetronomeChange_v2(targetMeasure, targetBeat, newBpm, description));
    }

    /// <summary>
    /// Schedule a time signature change at measure start
    /// </summary>
    public void ScheduleTimeSignatureChange(int targetMeasure, int newBeatsPerMeasure, string description = "") {
        ScheduleChange(new MetronomeChange_v2(targetMeasure, newBeatsPerMeasure, description));
    }

    /// <summary>
    /// Schedule a combined tempo and time signature change at measure start
    /// </summary>
    public void ScheduleCombinedChange(int targetMeasure, float newBpm, int newBeatsPerMeasure, string description = "") {
        ScheduleChange(new MetronomeChange_v2(targetMeasure, newBpm, newBeatsPerMeasure, description));
    }

    /// <summary>
    /// Schedule a stop event
    /// </summary>
    public void ScheduleStopEvent(int targetMeasure, string description = "") {
        ScheduleChange(MetronomeChange_v2.CreateStopEvent(targetMeasure, description));
    }

    /// <summary>
    /// Schedule a mute event
    /// </summary>
    public void ScheduleMuteEvent(int targetMeasure, string description = "") {
        ScheduleChange(MetronomeChange_v2.CreateMute(targetMeasure, description));
    }

    /// <summary>
    /// Schedule an unmute event
    /// </summary>
    public void ScheduleUnmuteEvent(int targetMeasure, string description = "") {
        ScheduleChange(MetronomeChange_v2.CreateUnmute(targetMeasure, description));
    }

    /// <summary>
    /// Schedule a visual off event
    /// </summary>
    public void ScheduleVisualOffEvent(int targetMeasure, string description = "") {
        ScheduleChange(MetronomeChange_v2.CreateVisualOff(targetMeasure, description));
    }

    /// <summary>
    /// Schedule a visual on event
    /// </summary>
    public void ScheduleVisualOnEvent(int targetMeasure, string description = "") {
        ScheduleChange(MetronomeChange_v2.CreateVisualOn(targetMeasure, description));
    }

    // === INTERNAL PROCESSING ===

    /// <summary>
    /// ✨ ENHANCED: Check for and process any changes that should occur at the current measure AND beat
    /// This is the core enhancement for beat-level scheduling
    /// </summary>
    private void CheckForPendingChanges(int currentMeasure, int currentBeat) {
        bool foundChange = false;

        // Check for notifications first (still measure-level)
        foreach (var change in pendingChanges) {
            if (change.ShouldNotifyAtMeasure(currentMeasure)) {
                TriggerChangeNotification(change, currentMeasure);
                change.MarkNotificationSent(currentMeasure);
            }
        }

        // ✨ ENHANCED: Process changes that should occur at this measure AND beat
        foreach (var change in pendingChanges) {
            if (!change.isProcessed &&
                change.targetMeasure == currentMeasure &&
                change.targetBeat == currentBeat) {
                ProcessChange(change);
                change.isProcessed = true;
                foundChange = true;
            }
        }

        // Remove processed changes
        if (foundChange) {
            pendingChanges.RemoveAll(c => c.isProcessed);
        }
    }

    /// <summary>
    /// Process a single change - fire event for metronome to apply
    /// </summary>
    private void ProcessChange(MetronomeChange_v2 change) {
        if (debugChangeSystem) {
            LogDebug($"Processing change: {change.GetChangeDescription()} at M{change.targetMeasure} B{change.targetBeat}");
        }

        // Fire event for metronome to apply the change
        OnChangeReadyToApply?.Invoke(change);

        // Fire processed event
        OnChangeProcessed?.Invoke(change);
    }

    /// <summary>
    /// Trigger a change notification
    /// </summary>
    private void TriggerChangeNotification(MetronomeChange_v2 change, int currentMeasure) {
        string message = change.GetNotificationMessage(currentMeasure);

        if (change.isUrgent) {
            message = "⚠️ URGENT: " + message;
        }

        if (debugChangeSystem) {
            LogDebug($"Notification: {message}");
        }

        OnChangeNotification?.Invoke(change, message);
    }

    /// <summary>
    /// ✨ ENHANCED: Sort pending changes by target measure AND beat
    /// </summary>
    private void SortPendingChanges() {
        pendingChanges.Sort((a, b) => {
            int measureCompare = a.targetMeasure.CompareTo(b.targetMeasure);
            if (measureCompare != 0) {
                return measureCompare;
            }
            // If measures are equal, sort by beat
            return a.targetBeat.CompareTo(b.targetBeat);
        });
    }

    // === LOGGING ===

    private void LogDebug(string message) {
        if (debugChangeSystem) {
            Debug.Log($"[MetronomeScheduler v2] {message}");
        }
    }

    private void LogWarning(string message) {
        Debug.LogWarning($"[MetronomeScheduler v2] ⚠️ {message}");
    }

    private void LogError(string message) {
        Debug.LogError($"[MetronomeScheduler v2] ❌ {message}");
    }

    // === DEBUG METHODS ===

    /// <summary>
    /// Debug: Log all pending changes
    /// </summary>
    [ContextMenu("Debug Pending Changes")]
    public void DebugPendingChanges() {
        Debug.Log($"=== METRONOME SCHEDULER v2 DEBUG ===");
        Debug.Log($"Current position: M{currentMeasure} B{currentBeat}");
        Debug.Log($"Pending changes: {pendingChanges.Count}");

        if (pendingChanges.Count == 0) {
            Debug.Log("  (no pending changes)");
            return;
        }

        for (int i = 0; i < pendingChanges.Count; i++) {
            var change = pendingChanges[i];
            Debug.Log($"  {i + 1:D2}. {change}");
        }
    }

    /// <summary>
    /// ✨ NEW: Test beat-level scheduling
    /// </summary>
    [ContextMenu("Test Beat-Level Scheduling")]
    public void TestBeatLevelScheduling() {
        LogDebug("Testing beat-level scheduling...");

        // Test 1: Traditional measure-level change
        ScheduleTempoChange(5, 140f, "Measure-level tempo change");

        // Test 2: Beat-level changes (smooth ramp)
        ScheduleTempoChangeAtBeat(7, 1, 120f, "Ramp start");
        ScheduleTempoChangeAtBeat(7, 2, 125f, "Ramp +5 BPM");
        ScheduleTempoChangeAtBeat(7, 3, 130f, "Ramp +10 BPM");
        ScheduleTempoChangeAtBeat(7, 4, 135f, "Ramp +15 BPM");

        DebugPendingChanges();
    }
}