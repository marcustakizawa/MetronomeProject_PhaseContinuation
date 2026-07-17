using System;
using System.Collections.Generic;
using UnityEngine;
using ChangeComposer.Data;

/// <summary>
/// MetronomeScheduler - Phase 1 Refactoring Component
/// Version: 2025-08-19 v1
/// 
/// PURPOSE: Extract all change scheduling logic from PrecisionMetronome
/// - Single responsibility: Change management only
/// - Event-driven communication with metronome
/// - Maintains all existing scheduling functionality
/// - 100% backward compatibility through delegation
/// </summary>
public class MetronomeScheduler_v1 : MonoBehaviour {

    [Header("Debug Settings")]
    [SerializeField] private bool debugChangeSystem = true;
    [SerializeField] private bool verboseLogging = false;

    // === EVENTS FOR COMMUNICATION ===

    /// <summary>
    /// Fired when a change is ready to be applied to the metronome
    /// </summary>
    public event Action<MetronomeChange> OnChangeReadyToApply;

    /// <summary>
    /// Fired when a change notification should be displayed
    /// </summary>
    public event Action<MetronomeChange, string> OnChangeNotification;

    /// <summary>
    /// Fired when a change is successfully scheduled
    /// </summary>
    public event Action<MetronomeChange> OnChangeScheduled;

    /// <summary>
    /// Fired after a change has been processed
    /// </summary>
    public event Action<MetronomeChange> OnChangeProcessed;

    // === INTERNAL STATE ===

    /// <summary>
    /// All pending changes, sorted by target measure
    /// </summary>
    private List<MetronomeChange> pendingChanges = new List<MetronomeChange>();

    /// <summary>
    /// Current measure for change processing
    /// </summary>
    private int currentMeasure = 1;

    // === PUBLIC API (Extracted from PrecisionMetronome) ===

    /// <summary>
    /// Schedule a change to occur at a specific measure
    /// </summary>
    public void ScheduleChange(MetronomeChange change) {
        if (change == null) {
            LogError("Cannot schedule null change");
            return;
        }

        if (change.targetMeasure < currentMeasure) {
            LogWarning($"Cannot schedule change for measure {change.targetMeasure} - we're already at measure {currentMeasure}");
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
    public List<MetronomeChange> GetPendingChanges() {
        return new List<MetronomeChange>(pendingChanges);
    }

    /// <summary>
    /// Called by metronome when measure changes - check for pending changes
    /// </summary>
    public void HandleMeasureChanged(int newMeasure) {
        currentMeasure = newMeasure;
        CheckForPendingChanges(newMeasure);
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
    /// Set current measure (for jumping scenarios)
    /// </summary>
    public void SetCurrentMeasure(int measure) {
        currentMeasure = measure;
    }

    // === CONVENIENCE SCHEDULING METHODS ===

    /// <summary>
    /// Schedule a tempo change
    /// </summary>
    public void ScheduleTempoChange(int targetMeasure, float newBpm, string description = "") {
        ScheduleChange(new MetronomeChange(targetMeasure, newBpm, description));
    }

    /// <summary>
    /// Schedule a time signature change
    /// </summary>
    public void ScheduleTimeSignatureChange(int targetMeasure, int newBeatsPerMeasure, string description = "") {
        ScheduleChange(new MetronomeChange(targetMeasure, newBeatsPerMeasure, description));
    }

    /// <summary>
    /// Schedule a combined tempo and time signature change
    /// </summary>
    public void ScheduleCombinedChange(int targetMeasure, float newBpm, int newBeatsPerMeasure, string description = "") {
        ScheduleChange(new MetronomeChange(targetMeasure, newBpm, newBeatsPerMeasure, description));
    }

    /// <summary>
    /// Schedule a stop event
    /// </summary>
    public void ScheduleStopEvent(int targetMeasure, string description = "") {
        ScheduleChange(MetronomeChange.CreateStopEvent(targetMeasure, description));
    }

    /// <summary>
    /// Schedule a mute event
    /// </summary>
    public void ScheduleMuteEvent(int targetMeasure, string description = "") {
        ScheduleChange(MetronomeChange.CreateMute(targetMeasure, description));
    }

    /// <summary>
    /// Schedule an unmute event
    /// </summary>
    public void ScheduleUnmuteEvent(int targetMeasure, string description = "") {
        ScheduleChange(MetronomeChange.CreateUnmute(targetMeasure, description));
    }

    /// <summary>
    /// Schedule a visual off event
    /// </summary>
    public void ScheduleVisualOffEvent(int targetMeasure, string description = "") {
        ScheduleChange(MetronomeChange.CreateVisualOff(targetMeasure, description));
    }

    /// <summary>
    /// Schedule a visual on event
    /// </summary>
    public void ScheduleVisualOnEvent(int targetMeasure, string description = "") {
        ScheduleChange(MetronomeChange.CreateVisualOn(targetMeasure, description));
    }

    // === INTERNAL PROCESSING ===

    /// <summary>
    /// Check for and process any changes that should occur at the current measure
    /// </summary>
    private void CheckForPendingChanges(int currentMeasure) {
        bool foundChange = false;

        // Check for notifications first
        foreach (var change in pendingChanges) {
            if (change.ShouldNotifyAtMeasure(currentMeasure)) {
                TriggerChangeNotification(change, currentMeasure);
                change.MarkNotificationSent(currentMeasure);
            }
        }

        // Process changes that should occur at this measure
        foreach (var change in pendingChanges) {
            if (!change.isProcessed && change.targetMeasure == currentMeasure) {
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
    private void ProcessChange(MetronomeChange change) {
        if (debugChangeSystem) {
            LogDebug($"Processing change: {change.GetChangeDescription()} at M{change.targetMeasure}");
        }

        // Fire event for metronome to apply the change
        OnChangeReadyToApply?.Invoke(change);

        // Fire processed event
        OnChangeProcessed?.Invoke(change);
    }

    /// <summary>
    /// Trigger a change notification
    /// </summary>
    private void TriggerChangeNotification(MetronomeChange change, int currentMeasure) {
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
    /// Sort pending changes by target measure
    /// </summary>
    private void SortPendingChanges() {
        pendingChanges.Sort((a, b) => a.targetMeasure.CompareTo(b.targetMeasure));
    }

    // === LOGGING ===

    private void LogDebug(string message) {
        if (debugChangeSystem) {
            Debug.Log($"[MetronomeScheduler] {message}");
        }
    }

    private void LogWarning(string message) {
        Debug.LogWarning($"[MetronomeScheduler] ⚠️ {message}");
    }

    private void LogError(string message) {
        Debug.LogError($"[MetronomeScheduler] ❌ {message}");
    }

    // === DEBUG METHODS ===

    /// <summary>
    /// Debug: Log all pending changes
    /// </summary>
    [ContextMenu("Debug Pending Changes")]
    public void DebugPendingChanges() {
        Debug.Log($"=== METRONOME SCHEDULER DEBUG ===");
        Debug.Log($"Current measure: {currentMeasure}");
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
    /// Debug: Test scheduling various changes
    /// </summary>
    [ContextMenu("Test Schedule Changes")]
    public void TestScheduleChanges() {
        LogDebug("Testing change scheduling...");

        ScheduleTempoChange(5, 140f, "Test tempo change");
        ScheduleTimeSignatureChange(8, 3, "Test time signature change");
        ScheduleMuteEvent(10, "Test mute event");

        DebugPendingChanges();
    }
}