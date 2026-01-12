using ChangeComposer.Indexing;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Test Controller for AbruptSyncModule
/// Provides easy-to-use test scenarios via inspector context menu
/// 
/// Gets CompositionIndexes from ThreeMetronomeCoordinator_v11
/// NO COROUTINES - follows project code precedent
/// </summary>
public class AbruptSyncTestController : MonoBehaviour {
    [Header("Metronomes")]
    [SerializeField] private PrecisionMetronome_v4_MultipleDisplays metronome1;
    [SerializeField] private PrecisionMetronome_v4_MultipleDisplays metronome2;
    [SerializeField] private PrecisionMetronome_v4_MultipleDisplays metronome3;

    [Header("Modules")]
    [SerializeField] private AbruptSyncModule syncModule;
    [SerializeField] private ThreeMetronomeCoordinator_v11_Simple_Validated coordinator;  // NEW: Need this to get indexes

    [Header("Test Results")]
    [TextArea(5, 10)]
    public string testLog = "Test results will appear here...";

    // Cache the indexes
    private CompositionIndex index1, index2, index3;

    private void Start() {
        LogTest("AbruptSyncTestController ready.");
        LogTest("IMPORTANT: Click 'Load Indexes' in ThreeMetronomeCoordinator first!");
        LogTest("Then right-click this component header and select a test.");
    }

    /// <summary>
    /// Get indexes from coordinator - call this before running tests
    /// </summary>
    private bool GetIndexesFromCoordinator() {
        if (coordinator == null) {
            LogTest("❌ ERROR: ThreeMetronomeCoordinator not assigned!");
            LogTest("   Assign it in the inspector under 'Modules' section.");
            return false;
        }

        // Use reflection to get the private indexes from coordinator
        var type = coordinator.GetType();
        var index1Field = type.GetField("index1", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var index2Field = type.GetField("index2", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var index3Field = type.GetField("index3", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (index1Field != null) index1 = (CompositionIndex)index1Field.GetValue(coordinator);
        if (index2Field != null) index2 = (CompositionIndex)index2Field.GetValue(coordinator);
        if (index3Field != null) index3 = (CompositionIndex)index3Field.GetValue(coordinator);

        if (index1 == null || index2 == null || index3 == null) {
            LogTest("❌ ERROR: Indexes not loaded!");
            LogTest("   Click 'Load Indexes' button in ThreeMetronomeCoordinator first.");
            return false;
        }

        LogTest("✓ Successfully retrieved indexes from coordinator");
        return true;
    }

    // ============================================================
    // TEST 1: Basic Drift and Abrupt Sync
    // ============================================================

    [ContextMenu("Test 1: Basic Drift and Sync (M2→M1 at M20)")]
    public void Test1_BasicDriftAndSync() {
        ClearLog();
        LogTest("=== TEST 1: BASIC DRIFT AND SYNC ===");

        if (!GetIndexesFromCoordinator()) return;

        LogTest("Setup: M1 constant 120 BPM, M2 drifts to 124 BPM at M5");
        LogTest("Expected: M2 syncs back to M1 at M20");
        LogTest("");

        // Schedule drift
        metronome2.ScheduleTempoChange(5, 124f);
        LogTest("✓ Scheduled: M2 → 124 BPM at M5 (drift starts)");

        // Setup and schedule sync
        syncModule.SetupSync(
            source: metronome2,
            target: metronome1,
            sourceIdx: index2,
            targetIdx: index1,
            syncAtMeasure: 20,
            syncAtBeat: 1
        );
        syncModule.ScheduleAbruptSync();
        LogTest("✓ Scheduled: M2 syncs to M1 at M20 B1");
        LogTest("");

        LogTest("⏰ LISTEN FOR:");
        LogTest("  M5: M2 speeds up (drift begins)");
        LogTest("  M5-M19: Two separate metronomes");
        LogTest("  M20: ONE combined click (sync!)");
        LogTest("  M21+: Stay together");
        LogTest("");
        LogTest("VERIFY:");
        LogTest("  - At M20: ONE click, not two");
        LogTest("  - M1 shows M20, M2 shows M22 (different measures OK!)");
        LogTest("  - Both on beat 1 after sync");
        LogTest("  - Both at 120 BPM after sync");
    }

    // ============================================================
    // TEST 2: Different Target (M3 as reference)
    // ============================================================

    [ContextMenu("Test 2: M3 as Target (M1→M3 at M15)")]
    public void Test2_DifferentTarget() {
        ClearLog();
        LogTest("=== TEST 2: M3 AS SYNC TARGET ===");

        if (!GetIndexesFromCoordinator()) return;

        LogTest("Setup: M3 constant 100 BPM, M1 drifts to 110 BPM at M5");
        LogTest("Expected: M1 syncs to M3 (not M1 as target!)");
        LogTest("");

        metronome1.ScheduleTempoChange(5, 110f);
        LogTest("✓ Scheduled: M1 → 110 BPM at M5");

        syncModule.SetupSync(
            source: metronome1,
            target: metronome3,
            sourceIdx: index1,
            targetIdx: index3,
            syncAtMeasure: 15,
            syncAtBeat: 1
        );
        syncModule.ScheduleAbruptSync();
        LogTest("✓ Scheduled: M1 syncs to M3 at M15 B1");
        LogTest("");

        LogTest("VERIFY:");
        LogTest("  - M1 adopts M3's tempo (100 BPM)");
        LogTest("  - Works with any metronome as target");
    }

    // ============================================================
    // TEST 3: Mid-Measure Sync (Beat 3)
    // ============================================================

    [ContextMenu("Test 3: Mid-Measure Sync (M2→M1 at M15 B3)")]
    public void Test3_MidMeasureSync() {
        ClearLog();
        LogTest("=== TEST 3: MID-MEASURE SYNC ===");

        if (!GetIndexesFromCoordinator()) return;

        LogTest("Setup: Sync on beat 3 (not downbeat!)");
        LogTest("");

        metronome2.ScheduleTempoChange(5, 124f);
        LogTest("✓ Scheduled: M2 → 124 BPM at M5");

        syncModule.SetupSync(
            source: metronome2,
            target: metronome1,
            sourceIdx: index2,
            targetIdx: index1,
            syncAtMeasure: 15,
            syncAtBeat: 3  // Beat 3!
        );
        syncModule.ScheduleAbruptSync();
        LogTest("✓ Scheduled: M2 syncs to M1 at M15 B3 (beat 3!)");
        LogTest("");

        LogTest("VERIFY:");
        LogTest("  - Sync occurs on beat 3, not beat 1");
        LogTest("  - After sync: both follow 3-4-1-2-3-4 pattern");
    }

    // ============================================================
    // TEST 4: Large Drift Amount
    // ============================================================

    [ContextMenu("Test 4: Large Drift (M2 150 BPM)")]
    public void Test4_LargeDrift() {
        ClearLog();
        LogTest("=== TEST 4: LARGE DRIFT AMOUNT ===");

        if (!GetIndexesFromCoordinator()) return;

        LogTest("Setup: M2 goes to 150 BPM (major difference!)");
        LogTest("");

        metronome2.ScheduleTempoChange(5, 150f);
        LogTest("✓ Scheduled: M2 → 150 BPM at M5 (very fast!)");

        syncModule.SetupSync(
            source: metronome2,
            target: metronome1,
            sourceIdx: index2,
            targetIdx: index1,
            syncAtMeasure: 25,
            syncAtBeat: 1
        );
        syncModule.ScheduleAbruptSync();
        LogTest("✓ Scheduled: M2 syncs to M1 at M25 B1");
        LogTest("");

        LogTest("VERIFY:");
        LogTest("  - M2 gets very far ahead");
        LogTest("  - At M25: M2 might be at M30+");
        LogTest("  - Large tempo drop (150→120) is audible");
        LogTest("  - Still achieves perfect sync");
    }

    // ============================================================
    // TEST 5: Deceleration (Falling Behind)
    // ============================================================

    [ContextMenu("Test 5: Deceleration Drift (M2 110 BPM)")]
    public void Test5_Deceleration() {
        ClearLog();
        LogTest("=== TEST 5: DECELERATION DRIFT ===");

        if (!GetIndexesFromCoordinator()) return;

        LogTest("Setup: M2 slows down (falls behind)");
        LogTest("");

        metronome2.ScheduleTempoChange(5, 110f);
        LogTest("✓ Scheduled: M2 → 110 BPM at M5 (slower)");

        syncModule.SetupSync(
            source: metronome2,
            target: metronome1,
            sourceIdx: index2,
            targetIdx: index1,
            syncAtMeasure: 20,
            syncAtBeat: 1
        );
        syncModule.ScheduleAbruptSync();
        LogTest("✓ Scheduled: M2 syncs to M1 at M20 B1");
        LogTest("");

        LogTest("VERIFY:");
        LogTest("  - M2 falls behind M1");
        LogTest("  - At M20: M2 might be at M18");
        LogTest("  - Works in reverse (slow→fast sync)");
    }

    // ============================================================
    // Quick Test Scenarios
    // ============================================================

    [ContextMenu("Quick: Drift Now, Sync in 15 Measures")]
    public void QuickTest_DriftNow() {
        ClearLog();
        LogTest("=== QUICK TEST: IMMEDIATE DRIFT ===");

        if (!GetIndexesFromCoordinator()) return;

        int currentMeasure = metronome1.CurrentMeasure;
        int syncMeasure = currentMeasure + 15;

        metronome2.ScheduleTempoChange(currentMeasure + 1, 124f);
        LogTest($"✓ Drift: M2 → 124 BPM at M{currentMeasure + 1}");

        syncModule.SetupSync(
            source: metronome2,
            target: metronome1,
            sourceIdx: index2,
            targetIdx: index1,
            syncAtMeasure: syncMeasure,
            syncAtBeat: 1
        );
        syncModule.ScheduleAbruptSync();
        LogTest($"✓ Sync: M2 → M1 at M{syncMeasure}");
        LogTest("");
        LogTest($"⏰ Listen for sync at measure {syncMeasure}");
    }

    // ============================================================
    // Utility Methods
    // ============================================================

    private void ClearLog() {
        testLog = "";
    }

    private void LogTest(string message) {
        testLog += message + "\n";
        Debug.Log($"[AbruptSyncTest] {message}");
    }

    [ContextMenu("Clear Test Log")]
    public void ClearTestLog() {
        ClearLog();
        LogTest("Test log cleared. Ready for new test.");
    }

    // ============================================================
    // Status Checks
    // ============================================================

    [ContextMenu("Check Current Status")]
    public void CheckStatus() {
        ClearLog();
        LogTest("=== CURRENT STATUS ===");
        LogTest("");

        if (metronome1 != null) {
            LogTest($"M1: M{metronome1.CurrentMeasure} B{metronome1.CurrentBeat}, {metronome1.Bpm:F1} BPM");
        }
        if (metronome2 != null) {
            LogTest($"M2: M{metronome2.CurrentMeasure} B{metronome2.CurrentBeat}, {metronome2.Bpm:F1} BPM");
        }
        if (metronome3 != null) {
            LogTest($"M3: M{metronome3.CurrentMeasure} B{metronome3.CurrentBeat}, {metronome3.Bpm:F1} BPM");
        }

        LogTest("");

        // Check if synced
        if (metronome1 != null && metronome2 != null) {
            bool beatsMatch = metronome1.CurrentBeat == metronome2.CurrentBeat;
            bool temposMatch = Mathf.Abs(metronome1.Bpm - metronome2.Bpm) < 0.5f;

            if (beatsMatch && temposMatch) {
                LogTest("✅ M1 and M2 are SYNCHRONIZED");
                LogTest($"   (Same beat, same tempo)");
            } else {
                LogTest("❌ M1 and M2 are NOT synchronized");
                if (!beatsMatch) LogTest($"   Beat mismatch: {metronome1.CurrentBeat} vs {metronome2.CurrentBeat}");
                if (!temposMatch) LogTest($"   Tempo mismatch: {metronome1.Bpm:F1} vs {metronome2.Bpm:F1} BPM");
            }
        }
    }
}