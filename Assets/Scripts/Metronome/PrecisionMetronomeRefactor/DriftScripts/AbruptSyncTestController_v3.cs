using UnityEngine;
using ChangeComposer.Data;
using ChangeComposer.Indexing;

/// <summary>
/// AbruptSync Test Controller v3 - Button-Based Testing
/// Version: 2025-01-08 v3
/// 
/// WORKFLOW:
/// 1. Assign JSON files, metronomes, and AbruptSyncModule in inspector
/// 2. Click "Generate Indexes" button
/// 3. Click test buttons (Test A1, B1, B4, etc.) to run scenarios
/// 
/// All methods are public and ready to connect to UI buttons!
/// </summary>
public class AbruptSyncTestController_v3 : MonoBehaviour {
    [Header("═══ COMPOSITION DATA ═══")]
    [Tooltip("Assign your JSON files for each track")]
    public TextAsset track1JsonFile;
    public TextAsset track2JsonFile;
    public TextAsset track3JsonFile;

    [Header("═══ METRONOMES ═══")]
    public PrecisionMetronome_v4_MultipleDisplays metronome1;
    public PrecisionMetronome_v4_MultipleDisplays metronome2;
    public PrecisionMetronome_v4_MultipleDisplays metronome3;

    [Header("═══ ABRUPT SYNC MODULE ═══")]
    public AbruptSyncModule_v2_EventBased syncModule;

    [Header("═══ TEST STATUS ═══")]
    [TextArea(8, 15)]
    public string testLog = "Click 'Generate Indexes' to begin...";

    [Header("═══ INDEX SYSTEM ═══")]
    [SerializeField] private bool indexesLoaded = false;
    public string indexStatus = "Not loaded";

    // Core data (private)
    private CompositionIndex index1, index2, index3;
    private ChangeSequence sequence1, sequence2, sequence3;

    private void Start() {
        LogTest("═══ ABRUPT SYNC TEST CONTROLLER v3 ═══");
        LogTest("Step 1: Click 'Generate Indexes' button");
        LogTest("Step 2: Click any test button");
    }

    // ════════════════════════════════════════════════════════════
    // STEP 1: GENERATE INDEXES
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Generate composition indexes from JSON files
    /// CALL THIS FIRST before running any tests
    /// </summary>
    [ContextMenu("🔧 Generate Indexes")]
    public void GenerateIndexes() {
        ClearLog();
        LogTest("═══ GENERATING COMPOSITION INDEXES ═══");

        if (track1JsonFile == null || track2JsonFile == null || track3JsonFile == null) {
            LogTest("❌ ERROR: Missing JSON files!");
            LogTest("   Assign track1JsonFile, track2JsonFile, and track3JsonFile in inspector");
            indexesLoaded = false;
            indexStatus = "❌ Missing JSON files";
            return;
        }

        try {
            // Load sequences
            sequence1 = ChangeSequence.FromJSON(track1JsonFile.text);
            sequence2 = ChangeSequence.FromJSON(track2JsonFile.text);
            sequence3 = ChangeSequence.FromJSON(track3JsonFile.text);

            // Generate indexes
            index1 = CompositionIndexGenerator.GenerateIndex(sequence1, 100);
            index2 = CompositionIndexGenerator.GenerateIndex(sequence2, 100);
            index3 = CompositionIndexGenerator.GenerateIndex(sequence3, 100);

            indexesLoaded = true;
            indexStatus = $"✅ Loaded: {index1.measureStates.Count}M, {index2.measureStates.Count}M, {index3.measureStates.Count}M";

            LogTest("✅ All indexes generated successfully!");
            LogTest($"Track 1: {index1.measureStates.Count} measures");
            LogTest($"Track 2: {index2.measureStates.Count} measures");
            LogTest($"Track 3: {index3.measureStates.Count} measures");
            LogTest("");
            LogTest("🎯 Ready to run tests!");
            LogTest("═══════════════════════════════════");
            LogTest("Start with SINGLE METRONOME tests (A1-A3),");
            LogTest("then move to ABRUPT SYNC tests (B1-B6).");

        } catch (System.Exception ex) {
            LogTest($"❌ ERROR: {ex.Message}");
            indexesLoaded = false;
            indexStatus = "❌ Failed to load";
        }
    }

    // ════════════════════════════════════════════════════════════
    // SECTION A: SINGLE METRONOME TESTS
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Test A1: Single metronome ramps up tempo
    /// </summary>
    [ContextMenu("▶️ Test A1: Single Metronome Ramp UP")]
    public void RunTest_A1_SingleRampUp() {
        if (!ValidateMetronomes()) return;

        ClearLog();
        LogTest("═══ TEST A1: SINGLE METRONOME RAMP UP ═══");
        LogTest("Setup: M1 starts at 120 BPM");
        LogTest("Action: Gradually ramp up to 140 BPM");
        LogTest("");

        // Reset and start M1 only
        ResetAllMetronomes();
        StopAllMetronomes();

        metronome1.SetTempo(120f);
        metronome1.StartAtMeasure(1);
        LogTest("✓ M1 started at 120 BPM");

        // Schedule gradual ramp up: 120 → 125 → 130 → 135 → 140
        metronome1.ScheduleTempoChange(5, 125f);
        metronome1.ScheduleTempoChange(9, 130f);
        metronome1.ScheduleTempoChange(13, 135f);
        metronome1.ScheduleTempoChange(17, 140f);

        LogTest("✓ Scheduled ramp: 120→125→130→135→140 BPM");
        LogTest("");
        LogTest("⏰ LISTEN FOR:");
        LogTest("  M5: 125 BPM | M9: 130 BPM | M13: 135 BPM | M17: 140 BPM");
        LogTest("");
        LogTest("✅ Test running! Listen for smooth acceleration.");
    }

    /// <summary>
    /// Test A2: Single metronome ramps down tempo
    /// </summary>
    [ContextMenu("▶️ Test A2: Single Metronome Ramp DOWN")]
    public void RunTest_A2_SingleRampDown() {
        if (!ValidateMetronomes()) return;

        ClearLog();
        LogTest("═══ TEST A2: SINGLE METRONOME RAMP DOWN ═══");
        LogTest("Setup: M1 starts at 140 BPM");
        LogTest("Action: Gradually ramp down to 100 BPM");
        LogTest("");

        // Reset and start M1 only
        ResetAllMetronomes();
        StopAllMetronomes();

        metronome1.SetTempo(140f);
        metronome1.StartAtMeasure(1);
        LogTest("✓ M1 started at 140 BPM");

        // Schedule gradual ramp down: 140 → 130 → 120 → 110 → 100
        metronome1.ScheduleTempoChange(5, 130f);
        metronome1.ScheduleTempoChange(9, 120f);
        metronome1.ScheduleTempoChange(13, 110f);
        metronome1.ScheduleTempoChange(17, 100f);

        LogTest("✓ Scheduled ramp: 140→130→120→110→100 BPM");
        LogTest("");
        LogTest("⏰ LISTEN FOR:");
        LogTest("  M5: 130 BPM | M9: 120 BPM | M13: 110 BPM | M17: 100 BPM");
        LogTest("");
        LogTest("✅ Test running! Listen for smooth deceleration.");
    }

    /// <summary>
    /// Test A3: Single metronome with multiple ramps
    /// </summary>
    [ContextMenu("▶️ Test A3: Multiple Ramps")]
    public void RunTest_A3_MultipleRamps() {
        if (!ValidateMetronomes()) return;

        ClearLog();
        LogTest("═══ TEST A3: MULTIPLE RAMPS (UP & DOWN) ═══");
        LogTest("Setup: M1 starts at 120 BPM");
        LogTest("Action: Ramp up, then down, then up again");
        LogTest("");

        // Reset and start M1 only
        ResetAllMetronomes();
        StopAllMetronomes();

        metronome1.SetTempo(120f);
        metronome1.StartAtMeasure(1);
        LogTest("✓ M1 started at 120 BPM");

        // Schedule complex ramp pattern
        metronome1.ScheduleTempoChange(5, 140f);   // Ramp UP
        metronome1.ScheduleTempoChange(10, 100f);  // Ramp DOWN
        metronome1.ScheduleTempoChange(15, 130f);  // Ramp UP again
        metronome1.ScheduleTempoChange(20, 120f);  // Back to original

        LogTest("✓ Pattern: 120→140→100→130→120 BPM");
        LogTest("");
        LogTest("⏰ LISTEN FOR:");
        LogTest("  M5: 140 | M10: 100 | M15: 130 | M20: 120");
        LogTest("");
        LogTest("✅ Test running! Listen for all transitions.");
    }

    // ════════════════════════════════════════════════════════════
    // SECTION B: ABRUPT SYNC TESTS
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Test B1: Basic drift and abrupt sync
    /// </summary>
    [ContextMenu("▶️ Test B1: Basic Drift and Sync")]
    public void RunTest_B1_BasicDriftAndSync() {
        if (!ValidateSetup()) return;

        ClearLog();
        LogTest("═══ TEST B1: BASIC DRIFT AND SYNC ═══");
        LogTest("Setup: M1=120 BPM, M2 drifts to 124 BPM at M5");
        LogTest("Expected: M2 syncs back to M1 at M20");
        LogTest("");

        // Reset and start both metronomes
        ResetAllMetronomes();
        StopAllMetronomes();

        metronome1.SetTempo(120f);
        metronome1.StartAtMeasure(1);

        metronome2.SetTempo(120f);
        metronome2.StartAtMeasure(1);

        LogTest("✓ Both metronomes started at 120 BPM");

        // Schedule drift
        metronome2.ScheduleTempoChange(5, 124f);
        LogTest("✓ Scheduled: M2 → 124 BPM at M5 (drift)");

        // Setup and schedule sync (WITH INDEX PARAMETERS!)
        syncModule.SetupSync(
            source: metronome2,
            target: metronome1,
            sourceIdx: index2,      // ← Need this!
            targetIdx: index1,      // ← Need this!
            syncAtMeasure: 20,
            syncAtBeat: 1
        );
        syncModule.ScheduleAbruptSync();
        LogTest("✓ Scheduled: M2 syncs to M1 at M20 B1");
        LogTest("");

        LogTest("⏰ LISTEN FOR:");
        LogTest("  M1-M4: Both together");
        LogTest("  M5: M2 speeds up (drift begins)");
        LogTest("  M5-M19: Two separate clicks");
        LogTest("  M20 B1: ONE combined click (SYNC!)");
        LogTest("  M21+: Stay together");
        LogTest("");
        LogTest("✅ Test running! Listen for sync at M20.");
    }

    /// <summary>
    /// Test B2: Different target metronome (M3)
    /// </summary>
    [ContextMenu("▶️ Test B2: M3 as Target")]
    public void RunTest_B2_DifferentTarget() {
        if (!ValidateSetup()) return;

        ClearLog();
        LogTest("═══ TEST B2: M3 AS SYNC TARGET ═══");
        LogTest("Setup: M3=100 BPM, M1 drifts to 110 BPM at M5");
        LogTest("Expected: M1 syncs to M3");
        LogTest("");

        // Reset and start metronomes
        ResetAllMetronomes();
        StopAllMetronomes();

        metronome1.SetTempo(100f);
        metronome1.StartAtMeasure(1);

        metronome3.SetTempo(100f);
        metronome3.StartAtMeasure(1);

        LogTest("✓ M1 and M3 started at 100 BPM");

        // Schedule drift
        metronome1.ScheduleTempoChange(5, 110f);
        LogTest("✓ Scheduled: M1 → 110 BPM at M5");

        // Setup sync to M3 (not M1!)
        syncModule.SetupSync(
            source: metronome1,
            target: metronome3,
            sourceIdx: index1,      // ← M1's index
            targetIdx: index3,      // ← M3's index
            syncAtMeasure: 15,
            syncAtBeat: 1
        );
        syncModule.ScheduleAbruptSync();
        LogTest("✓ Scheduled: M1 syncs to M3 at M15 B1");
        LogTest("");
        LogTest("✅ Test running! M1 should sync to M3's tempo.");
    }

    /// <summary>
    /// Test B3: Large drift amount
    /// </summary>
    [ContextMenu("▶️ Test B3: Large Drift")]
    public void RunTest_B3_LargeDrift() {
        if (!ValidateSetup()) return;

        ClearLog();
        LogTest("═══ TEST B3: LARGE DRIFT AMOUNT ═══");
        LogTest("Setup: M2 accelerates to 150 BPM!");
        LogTest("");

        // Reset and start metronomes
        ResetAllMetronomes();
        StopAllMetronomes();

        metronome1.SetTempo(120f);
        metronome1.StartAtMeasure(1);

        metronome2.SetTempo(120f);
        metronome2.StartAtMeasure(1);

        LogTest("✓ Both started at 120 BPM");

        // Schedule large drift
        metronome2.ScheduleTempoChange(5, 150f);
        LogTest("✓ Scheduled: M2 → 150 BPM at M5 (very fast!)");

        // Setup sync
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
        LogTest("⏰ Listen for large tempo drop at M25");
        LogTest("✅ Test running!");
    }

    /// <summary>
    /// Test B4: Pure AbruptSync - No Ramps (ISOLATION TEST)
    /// This removes all tempo changes to isolate sync latency
    /// </summary>
    [ContextMenu("▶️ Test B4: Pure Sync ⭐ (Isolation)")]
    public void RunTest_B4_PureAbruptSync_NoRamps() {
        if (!ValidateSetup()) return;

        ClearLog();
        LogTest("═══ TEST B4: PURE ABRUPT SYNC ═══");
        LogTest("⭐ ISOLATION TEST - NO TEMPO RAMPS ⭐");
        LogTest("");
        LogTest("Setup:");
        LogTest("  M1: 120 BPM constant (no changes)");
        LogTest("  M2: 130 BPM constant (no changes)");
        LogTest("  Sync at M10 B1");
        LogTest("");

        // Reset and start both metronomes
        ResetAllMetronomes();
        StopAllMetronomes();

        // M1: Constant 120 BPM
        metronome1.SetTempo(120f);
        metronome1.StartAtMeasure(1);
        LogTest("✓ M1: 120 BPM (constant)");

        // M2: Constant 130 BPM (faster)
        metronome2.SetTempo(130f);
        metronome2.StartAtMeasure(1);
        LogTest("✓ M2: 130 BPM (constant)");

        // NO TEMPO CHANGES!
        LogTest("✓ NO tempo changes (isolation test)");

        // Setup sync at M10
        syncModule.SetupSync(
            source: metronome2,
            target: metronome1,
            sourceIdx: index2,
            targetIdx: index1,
            syncAtMeasure: 10,
            syncAtBeat: 1
        );
        syncModule.ScheduleAbruptSync();
        LogTest("✓ Sync scheduled at M10 B1");
        LogTest("");

        LogTest("⏰ CRITICAL LISTENING:");
        LogTest("  M1-M9: Two separate clicks");
        LogTest("  M10 B1: Should hear ONE click");
        LogTest("  If two clicks = latency present");
        LogTest("");
        LogTest("🔍 This test isolates sync timing!");
        LogTest("✅ Test running!");
    }

    /// <summary>
    /// Test B5: Deceleration drift
    /// </summary>
    [ContextMenu("▶️ Test B5: Deceleration")]
    public void RunTest_B5_Deceleration() {
        if (!ValidateSetup()) return;

        ClearLog();
        LogTest("═══ TEST B5: DECELERATION DRIFT ═══");
        LogTest("Setup: M2 slows down (falls behind)");
        LogTest("");

        // Reset and start metronomes
        ResetAllMetronomes();
        StopAllMetronomes();

        metronome1.SetTempo(120f);
        metronome1.StartAtMeasure(1);

        metronome2.SetTempo(120f);
        metronome2.StartAtMeasure(1);

        LogTest("✓ Both started at 120 BPM");

        // Schedule deceleration
        metronome2.ScheduleTempoChange(5, 110f);
        LogTest("✓ Scheduled: M2 → 110 BPM at M5 (slower)");

        // Setup sync
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
        LogTest("✅ Test running! M2 falls behind, then syncs.");
    }

    /// <summary>
    /// Test B6: Mid-measure sync (beat 3)
    /// </summary>
    [ContextMenu("▶️ Test B6: Mid-Measure Sync")]
    public void RunTest_B6_MidMeasureSync() {
        if (!ValidateSetup()) return;

        ClearLog();
        LogTest("═══ TEST B6: MID-MEASURE SYNC ═══");
        LogTest("Setup: Sync on beat 3 (not downbeat)");
        LogTest("");

        // Reset and start metronomes
        ResetAllMetronomes();
        StopAllMetronomes();

        metronome1.SetTempo(120f);
        metronome1.StartAtMeasure(1);

        metronome2.SetTempo(120f);
        metronome2.StartAtMeasure(1);

        LogTest("✓ Both started at 120 BPM");

        // Schedule drift
        metronome2.ScheduleTempoChange(5, 124f);
        LogTest("✓ Scheduled: M2 → 124 BPM at M5");

        // Setup sync on BEAT 3 (not beat 1)
        syncModule.SetupSync(
            source: metronome2,
            target: metronome1,
            sourceIdx: index2,
            targetIdx: index1,
            syncAtMeasure: 15,
            syncAtBeat: 3  // Beat 3!
        );
        syncModule.ScheduleAbruptSync();
        LogTest("✓ Scheduled: M2 syncs at M15 BEAT 3");
        LogTest("");
        LogTest("⏰ Listen for sync on beat 3, not beat 1");
        LogTest("✅ Test running!");
    }

    /// <summary>
    /// Test B7: Complete Drift-Ramp-Sync Cycle
    /// Combines smooth tempo ramping with event-based sync at convergence
    /// </summary>
    [ContextMenu("▶️ Test B7: Drift-Ramp-Sync Cycle ⭐")]
    public void RunTest_B7_DriftRampSyncCycle() {
        if (!ValidateSetup()) return;

        ClearLog();
        LogTest("═══ TEST B7: DRIFT-RAMP-SYNC CYCLE ═══");
        LogTest("⭐ COMBINES SMOOTH RAMPS + EVENT-BASED SYNC ⭐");
        LogTest("");
        LogTest("Timeline (M1 = source metronome):");
        LogTest("  M1-M4:   Both 120 BPM (in sync)");
        LogTest("  M5-M10:  M2 ramps UP to 130 BPM");
        LogTest("  M10-M24: M2 stays 130 BPM (drifting ahead)");
        LogTest("  M25-M30: M2 ramps DOWN to 120 BPM");
        LogTest("  M30 B1:  EVENT SYNC (perfect alignment!)");
        LogTest("  M30+:    Both locked at 120 BPM");
        LogTest("");

        // Reset and start both metronomes
        ResetAllMetronomes();
        StopAllMetronomes();

        // Both start at 120 BPM
        metronome1.SetTempo(120f);
        metronome1.StartAtMeasure(1);

        metronome2.SetTempo(120f);
        metronome2.StartAtMeasure(1);

        LogTest("✓ Both started at 120 BPM");

        // ACCELERATION RAMP: M5 → M10 (120 → 130 BPM)
        metronome2.ScheduleTempoChange(5, 122f);   // M5
        metronome2.ScheduleTempoChange(6, 124f);   // M6
        metronome2.ScheduleTempoChange(7, 126f);   // M7
        metronome2.ScheduleTempoChange(8, 128f);   // M8
        metronome2.ScheduleTempoChange(10, 130f);  // M10 - target reached

        LogTest("✓ Acceleration ramp: M5-M10 (120→130 BPM)");

        // DECELERATION RAMP: M25 → M30 (130 → 120 BPM)
        metronome2.ScheduleTempoChange(25, 128f);  // M25
        metronome2.ScheduleTempoChange(26, 126f);  // M26
        metronome2.ScheduleTempoChange(27, 124f);  // M27
        metronome2.ScheduleTempoChange(28, 122f);  // M28
        metronome2.ScheduleTempoChange(30, 120f);  // M30 - back to sync tempo

        LogTest("✓ Deceleration ramp: M25-M30 (130→120 BPM)");

        // EVENT-BASED SYNC at M30 B1
        syncModule.SetupSync(
            source: metronome2,
            target: metronome1,
            sourceIdx: index2,
            targetIdx: index1,
            syncAtMeasure: 30,
            syncAtBeat: 1
        );
        syncModule.ScheduleAbruptSync();
        LogTest("✓ Event sync scheduled at M30 B1");
        LogTest("");

        LogTest("⏰ CRITICAL LISTENING:");
        LogTest("  M5-M10:  Hear M2 gradually speed up");
        LogTest("  M10-M24: Two distinct clicks (M2 faster)");
        LogTest("  M25-M30: Hear M2 gradually slow down");
        LogTest("  M30 B1:  ONE UNIFIED CLICK (perfect sync!)");
        LogTest("  M30+:    Single click continues");
        LogTest("");
        LogTest("🔍 KEY TEST:");
        LogTest("  At M30, ramp brings tempo close (120 BPM)");
        LogTest("  Event sync perfects beat alignment");
        LogTest("  Result: Smooth convergence + perfect sync!");
        LogTest("");
        LogTest("✅ Test running!");
    }

    // ════════════════════════════════════════════════════════════
    // UTILITY METHODS
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Stop all metronomes
    /// </summary>
    [ContextMenu("⏹️ Stop All Metronomes")]
    public void StopAllMetronomes() {
        metronome1?.StopMetronome();
        metronome2?.StopMetronome();
        metronome3?.StopMetronome();
        Debug.Log("⏹️ All metronomes stopped");
    }

    /// <summary>
    /// Reset all metronomes
    /// </summary>
    [ContextMenu("🔄 Reset All Metronomes")]
    public void ResetAllMetronomes() {
        metronome1?.ClearPendingChanges();
        metronome1?.ResetMetronome();

        metronome2?.ClearPendingChanges();
        metronome2?.ResetMetronome();

        metronome3?.ClearPendingChanges();
        metronome3?.ResetMetronome();

        Debug.Log("🔄 All metronomes reset");
    }

    /// <summary>
    /// Check current status of all metronomes
    /// </summary>
    [ContextMenu("📊 Check Status")]
    public void CheckStatus() {
        ClearLog();
        LogTest("═══ CURRENT STATUS ═══");
        LogTest("");

        if (metronome1 != null) {
            LogTest($"M1: M{metronome1.CurrentMeasure} B{metronome1.CurrentBeat}, {metronome1.Bpm:F1} BPM");
        } else {
            LogTest("M1: Not assigned");
        }

        if (metronome2 != null) {
            LogTest($"M2: M{metronome2.CurrentMeasure} B{metronome2.CurrentBeat}, {metronome2.Bpm:F1} BPM");
        } else {
            LogTest("M2: Not assigned");
        }

        if (metronome3 != null) {
            LogTest($"M3: M{metronome3.CurrentMeasure} B{metronome3.CurrentBeat}, {metronome3.Bpm:F1} BPM");
        } else {
            LogTest("M3: Not assigned");
        }

        LogTest("");

        // Check if M1 and M2 are synced
        if (metronome1 != null && metronome2 != null) {
            bool beatsMatch = metronome1.CurrentBeat == metronome2.CurrentBeat;
            bool temposMatch = Mathf.Abs(metronome1.Bpm - metronome2.Bpm) < 0.5f;

            if (beatsMatch && temposMatch) {
                LogTest("✅ M1 and M2 are SYNCHRONIZED");
            } else {
                LogTest("❌ M1 and M2 are NOT synchronized");
                if (!beatsMatch) LogTest($"   Beat mismatch: {metronome1.CurrentBeat} vs {metronome2.CurrentBeat}");
                if (!temposMatch) LogTest($"   Tempo mismatch: {metronome1.Bpm:F1} vs {metronome2.Bpm:F1} BPM");
            }
        }
    }

    /// <summary>
    /// Clear test log
    /// </summary>
    [ContextMenu("🗑️ Clear Log")]
    public void ClearTestLog() {
        ClearLog();
        LogTest("Log cleared. Ready for new test.");
    }

    // ════════════════════════════════════════════════════════════
    // VALIDATION
    // ════════════════════════════════════════════════════════════

    private bool ValidateMetronomes() {
        if (metronome1 == null) {
            ClearLog();
            LogTest("❌ ERROR: Metronome1 not assigned!");
            LogTest("Assign metronome1 in inspector");
            return false;
        }

        if (metronome2 == null) {
            ClearLog();
            LogTest("❌ ERROR: Metronome2 not assigned!");
            LogTest("Assign metronome2 in inspector");
            return false;
        }

        return true;
    }

    private bool ValidateSetup() {
        if (!ValidateMetronomes()) return false;

        if (syncModule == null) {
            ClearLog();
            LogTest("❌ ERROR: AbruptSyncModule not assigned!");
            LogTest("Assign syncModule in inspector");
            return false;
        }

        if (!indexesLoaded) {
            ClearLog();
            LogTest("❌ ERROR: Indexes not loaded!");
            LogTest("Click 'Generate Indexes' button first");
            return false;
        }

        return true;
    }

    // ════════════════════════════════════════════════════════════
    // LOGGING
    // ════════════════════════════════════════════════════════════

    private void ClearLog() {
        testLog = "";
    }

    private void LogTest(string message) {
        testLog += message + "\n";
        Debug.Log($"[AbruptSyncTest] {message}");
    }
}