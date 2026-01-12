using ChangeComposer.Data;
using ChangeComposer.Indexing;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Abrupt Sync Module - Simple synchronization without smooth transitions
/// Forces source metronome to instantly match target metronome's tempo and beat at a specific moment
/// </summary>
public class AbruptSyncModule : MonoBehaviour {
    [Header("Metronomes")]
    [SerializeField]
    [Tooltip("The metronome that will sync (adopts target's tempo/beat)")]
    private PrecisionMetronome_v4_MultipleDisplays sourceMetronome;

    [SerializeField]
    [Tooltip("The reference metronome to sync to")]
    private PrecisionMetronome_v4_MultipleDisplays targetMetronome;

    [Header("Sync Configuration")]
    [Tooltip("Which measure of the TARGET metronome to sync at")]
    public int targetSyncMeasure = 20;

    [Tooltip("Which beat to sync on (1 = downbeat)")]
    public int syncOnBeat = 1;

    [Header("Debug")]
    public bool showDebugLogs = true;

    // Composition indexes - set via SetupSync() method
    private CompositionIndex sourceIndex;
    private CompositionIndex targetIndex;

    // Beat reset scheduling (no coroutines)
    private struct PendingBeatReset {
        public double syncTime;
        public int sourceMeasure;
        public int targetBeat;
        public bool isActive;
    }

    private PendingBeatReset pendingReset;

    void Update() {
        ProcessPendingBeatReset();
    }

    /// <summary>
    /// Process any pending beat resets (no coroutines)
    /// </summary>
    private void ProcessPendingBeatReset() {
        if (!pendingReset.isActive) return;

        // Check if we've reached the sync time
        if (AudioSettings.dspTime >= pendingReset.syncTime) {
            // Execute beat reset
            LogDebug($"[SYNC] Resetting {sourceMetronome.name} to beat {pendingReset.targetBeat} at M{pendingReset.sourceMeasure}");

            sourceMetronome.SetCurrentBeat(pendingReset.targetBeat);

            LogDebug($"[SYNC] Beat reset to {pendingReset.targetBeat} completed");

            // Mark as completed
            pendingReset.isActive = false;
        }
    }

    /// <summary>
    /// Schedule an abrupt sync at the specified target measure
    /// </summary>
    [ContextMenu("Schedule Abrupt Sync")]
    public void ScheduleAbruptSync() {
        if (!ValidateConfiguration()) {
            return;
        }

        LogDebug("=== SCHEDULING ABRUPT SYNC ===");
        LogDebug($"Target: {targetMetronome.name} M{targetSyncMeasure} B{syncOnBeat}");
        LogDebug($"Source: {sourceMetronome.name} will sync to match");

        // STEP 1: Calculate when the target reaches its sync point
        double syncTime = CalculateTargetSyncTime();
        LogDebug($"Sync will occur at timeline position: {syncTime:F3}s");

        // STEP 2: Calculate what measure the source will be at that time
        int sourceMeasureAtSync = CalculateSourceMeasureAtTime(syncTime);
        LogDebug($"Source will be at M{sourceMeasureAtSync} when sync occurs");

        // STEP 3: Get target's state at sync point
        MeasureState targetState = targetIndex.GetStateAtMeasure(targetSyncMeasure);
        LogDebug($"Target state: {targetState.bpm} BPM, {targetState.beatsPerMeasure}/4");

        // STEP 4: Create sync change for source metronome
        MetronomeChange syncChange = CreateSyncChange(
            sourceMeasureAtSync,
            targetState.bpm,
            targetState.beatsPerMeasure
        );

        // STEP 5: Schedule the sync change
        sourceMetronome.ScheduleChange(syncChange);

        LogDebug($"✓ Scheduled abrupt sync:");
        LogDebug($"  Source M{sourceMeasureAtSync} → {targetState.bpm} BPM, {targetState.beatsPerMeasure}/4, reset to B{syncOnBeat}");
        LogDebug($"  This matches Target M{targetSyncMeasure} at {syncTime:F3}s");
        LogDebug("==============================");

        // STEP 6: Schedule beat reset (no coroutines)
        ScheduleBeatReset(syncTime, sourceMeasureAtSync);
    }

    /// <summary>
    /// Calculate the absolute timeline position when target reaches its sync point
    /// </summary>
    private double CalculateTargetSyncTime() {
        // Calculate time to reach the target measure
        double timeToMeasure = CalculateMeasureStartTime(targetIndex, targetSyncMeasure);

        // If syncing on a beat other than beat 1, add time for those beats
        if (syncOnBeat > 1) {
            MeasureState state = targetIndex.GetStateAtMeasure(targetSyncMeasure);
            double beatDuration = 60.0 / state.bpm;
            timeToMeasure += (syncOnBeat - 1) * beatDuration;
        }

        return timeToMeasure;
    }

    /// <summary>
    /// Calculate what measure the source metronome will be at a given time
    /// </summary>
    private int CalculateSourceMeasureAtTime(double targetTime) {
        if (sourceIndex == null) return 1;

        double cumulativeTime = 0;

        for (int measure = 1; measure <= sourceIndex.measureStates.Count; measure++) {
            MeasureState state = sourceIndex.measureStates[measure - 1];
            double measureDuration = (60.0 / state.bpm) * state.beatsPerMeasure;

            if (cumulativeTime + measureDuration > targetTime) {
                // This is the measure that contains the target time
                return measure;
            }

            cumulativeTime += measureDuration;
        }

        // If we've gone past all measures, return the last measure
        return sourceIndex.measureStates.Count;
    }

    /// <summary>
    /// Calculate absolute time when a specific measure starts (same as ThreeMetronomeCoordinator)
    /// </summary>
    private double CalculateMeasureStartTime(CompositionIndex index, int targetMeasure) {
        if (index == null) return 0;

        double totalTime = 0;
        for (int measure = 1; measure < targetMeasure; measure++) {
            if (measure <= index.measureStates.Count) {
                var state = index.measureStates[measure - 1];
                double measureDuration = (60.0 / state.bpm) * state.beatsPerMeasure;
                totalTime += measureDuration;
            }
        }
        return totalTime;
    }

    /// <summary>
    /// Create the MetronomeChange that will sync the source to the target
    /// </summary>
    private MetronomeChange CreateSyncChange(int sourceMeasure, float targetBpm, int targetBeats) {
        var change = new MetronomeChange(
            sourceMeasure,
            targetBpm,
            targetBeats,
            $"Abrupt sync to {targetMetronome.name}"
        );

        return change;
    }

    /// <summary>
    /// Schedule a beat reset to occur at the exact sync moment (NO COROUTINES)
    /// This ensures both metronomes are on the same beat (e.g., both on beat 1)
    /// </summary>
    private void ScheduleBeatReset(double syncTime, int sourceMeasure) {
        // Schedule using struct - will be processed in Update()
        pendingReset = new PendingBeatReset {
            syncTime = syncTime,
            sourceMeasure = sourceMeasure,
            targetBeat = syncOnBeat,
            isActive = true
        };

        LogDebug($"[SYNC] Beat reset scheduled for {syncTime:F3}s");
    }

    /// <summary>
    /// Validate configuration before scheduling sync
    /// </summary>
    private bool ValidateConfiguration() {
        if (sourceMetronome == null) {
            Debug.LogError("[AbruptSync] Source metronome not assigned!");
            return false;
        }

        if (targetMetronome == null) {
            Debug.LogError("[AbruptSync] Target metronome not assigned!");
            return false;
        }

        if (sourceIndex == null) {
            Debug.LogError($"[AbruptSync] Source CompositionIndex not set! Call SetupSync() first.");
            return false;
        }

        if (targetIndex == null) {
            Debug.LogError($"[AbruptSync] Target CompositionIndex not set! Call SetupSync() first.");
            return false;
        }

        if (syncOnBeat < 1) {
            Debug.LogError($"[AbruptSync] Sync beat must be >= 1, got {syncOnBeat}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Setup method for testing - MUST be called before ScheduleAbruptSync()
    /// </summary>
    public void SetupSync(
        PrecisionMetronome_v4_MultipleDisplays source,
        PrecisionMetronome_v4_MultipleDisplays target,
        CompositionIndex sourceIdx,
        CompositionIndex targetIdx,
        int syncAtMeasure,
        int syncAtBeat = 1) {
        sourceMetronome = source;
        targetMetronome = target;
        sourceIndex = sourceIdx;
        targetIndex = targetIdx;
        targetSyncMeasure = syncAtMeasure;
        syncOnBeat = syncAtBeat;

        LogDebug($"[AbruptSync] Configured: {source.name} → {target.name} at M{syncAtMeasure} B{syncAtBeat}");
        LogDebug($"[AbruptSync] CompositionIndexes set");
    }

    /// <summary>
    /// Debug logging utility
    /// </summary>
    private void LogDebug(string message) {
        if (showDebugLogs) {
            Debug.Log($"[AbruptSync] {message}");
        }
    }
}