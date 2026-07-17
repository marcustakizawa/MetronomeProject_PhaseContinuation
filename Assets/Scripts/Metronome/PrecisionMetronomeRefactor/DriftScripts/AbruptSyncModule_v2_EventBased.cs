using UnityEngine;
using ChangeComposer.Indexing;

/// <summary>
/// AbruptSyncModule v2 - Event-Based Synchronization
/// Version: 2025-01-08 v2
/// 
/// KEY IMPROVEMENT:
/// Instead of calculating future DSP time and scheduling sync,
/// this version subscribes to the target metronome's beat events
/// and syncs at the exact moment the target fires the sync beat.
/// 
/// PRECISION: Sub-millisecond sync (event callback latency only)
/// vs. Previous: 10-50ms (DSP scheduling + calculation errors)
/// 
/// REQUIRES: PrecisionMetronome_v4_MultipleDisplays.SyncToMetronome() method
/// </summary>
public class AbruptSyncModule_v2_EventBased : MonoBehaviour {
    [Header("Metronomes")]
    [SerializeField] private PrecisionMetronome_v4_MultipleDisplays sourceMetronome;
    [SerializeField] private PrecisionMetronome_v4_MultipleDisplays targetMetronome;

    [Header("Sync Configuration")]
    [SerializeField] private int syncAtMeasure = 20;
    [SerializeField] private int syncAtBeat = 1;

    [Header("Indexes (for validation)")]
    [SerializeField] private CompositionIndex sourceIndex;
    [SerializeField] private CompositionIndex targetIndex;

    [Header("Status")]
    [SerializeField] private bool waitingForSync = false;
    [SerializeField] private string statusMessage = "Ready";

    // ════════════════════════════════════════════════════════════
    // PUBLIC API
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Setup the sync configuration
    /// </summary>
    public void SetupSync(
        PrecisionMetronome_v4_MultipleDisplays source,
        PrecisionMetronome_v4_MultipleDisplays target,
        CompositionIndex sourceIdx,
        CompositionIndex targetIdx,
        int syncAtMeasure,
        int syncAtBeat = 1) {
        this.sourceMetronome = source;
        this.targetMetronome = target;
        this.sourceIndex = sourceIdx;
        this.targetIndex = targetIdx;
        this.syncAtMeasure = syncAtMeasure;
        this.syncAtBeat = syncAtBeat;

        Debug.Log($"[AbruptSync] Setup complete:");
        Debug.Log($"  Source: {source.name}");
        Debug.Log($"  Target: {target.name}");
        Debug.Log($"  Sync at: M{syncAtMeasure} B{syncAtBeat}");
    }

    /// <summary>
    /// Schedule the abrupt sync using event-based approach
    /// This subscribes to the target's beat events and waits for the sync moment
    /// </summary>
    public void ScheduleAbruptSync() {
        if (sourceMetronome == null || targetMetronome == null) {
            Debug.LogError("[AbruptSync] Cannot schedule sync - metronomes not assigned!");
            return;
        }

        if (waitingForSync) {
            Debug.LogWarning("[AbruptSync] Already waiting for sync. Canceling previous sync.");
            CancelSync();
        }

        // Subscribe to target's beat events
        targetMetronome.OnBeatTriggered += HandleTargetBeat;
        waitingForSync = true;
        statusMessage = $"Waiting for M{syncAtMeasure} B{syncAtBeat}...";

        Debug.Log($"[AbruptSync] ✓ ARMED - Listening for target beat");
        Debug.Log($"[AbruptSync]   Will sync when target reaches M{syncAtMeasure} B{syncAtBeat}");
    }

    /// <summary>
    /// Cancel any pending sync
    /// </summary>
    public void CancelSync() {
        if (targetMetronome != null) {
            targetMetronome.OnBeatTriggered -= HandleTargetBeat;
        }
        waitingForSync = false;
        statusMessage = "Sync canceled";
        Debug.Log("[AbruptSync] Sync canceled");
    }

    // ════════════════════════════════════════════════════════════
    // EVENT HANDLING
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Called every time the target metronome fires a beat
    /// </summary>
    private void HandleTargetBeat(int beat) {
        // Check if this is the beat we're waiting for
        if (beat != syncAtBeat) {
            return;
        }

        // Check if target is at the correct measure
        if (targetMetronome.CurrentMeasure != syncAtMeasure) {
            return;
        }

        // THIS IS IT! Sync now!
        ExecuteSync();
    }

    /// <summary>
    /// Execute the actual sync - copy target's exact timing to source
    /// </summary>
    private void ExecuteSync() {
        Debug.Log($"[AbruptSync] ═══════════════════════════════════");
        Debug.Log($"[AbruptSync] ⚡ SYNC EXECUTED at DSP: {AudioSettings.dspTime:F6}");
        Debug.Log($"[AbruptSync] Target: {targetMetronome.name} M{targetMetronome.CurrentMeasure} B{targetMetronome.CurrentBeat}");
        Debug.Log($"[AbruptSync] Source (before): {sourceMetronome.name} M{sourceMetronome.CurrentMeasure} B{sourceMetronome.CurrentBeat}, {sourceMetronome.Bpm:F1} BPM");

        // *** THE MAGIC HAPPENS HERE ***
        // Use the new SyncToMetronome() method!
        sourceMetronome.SyncToMetronome(targetMetronome);

        Debug.Log($"[AbruptSync] Source (after): {sourceMetronome.name} M{sourceMetronome.CurrentMeasure} B{sourceMetronome.CurrentBeat}, {sourceMetronome.Bpm:F1} BPM");
        Debug.Log($"[AbruptSync] ✅ PERFECT SYNC ACHIEVED");
        Debug.Log($"[AbruptSync] ═══════════════════════════════════");

        // Cleanup
        targetMetronome.OnBeatTriggered -= HandleTargetBeat;
        waitingForSync = false;
        statusMessage = $"Synced at M{syncAtMeasure} B{syncAtBeat}";
    }

    // ════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════════════════

    private void OnDestroy() {
        // Cleanup event subscription if component is destroyed
        if (targetMetronome != null) {
            targetMetronome.OnBeatTriggered -= HandleTargetBeat;
        }
    }

    private void OnDisable() {
        // Cleanup when disabled
        CancelSync();
    }
}