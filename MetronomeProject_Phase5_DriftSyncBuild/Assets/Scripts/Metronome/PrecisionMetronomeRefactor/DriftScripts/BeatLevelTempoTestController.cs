using UnityEngine;
using ChangeComposer.Data;

/// <summary>
/// Beat-Level Tempo Test Controller
/// Version: 2025-01-10 v1.1
/// 
/// PURPOSE: Test Phase 1 of Beat-Level Tempo Ramps
/// - Verify beat-level scheduling infrastructure
/// - Test single beat-level tempo changes
/// - Test manual beat-level ramp generation
/// 
/// FOLLOWS: AbruptSyncTestController_v3 pattern
/// - Simple setup without CompositionIndex
/// - Button-based testing
/// - Clear logging
/// </summary>
public class BeatLevelTempoTestController : MonoBehaviour {

    [Header("Test Metronome")]
    [SerializeField] private PrecisionMetronome_v5_BeatLevel testMetronome;
    [SerializeField] private MetronomeScheduler_v2 scheduler;

    [Header("Phase 1 Test Configuration")]
    [SerializeField] private int testMeasure = 5;
    [SerializeField] private int testBeat = 3;
    [SerializeField] private float testBpm = 140f;

    [Header("Manual Ramp Configuration")]
    [SerializeField] private int rampStartMeasure = 10;
    [SerializeField] private int rampDurationMeasures = 3;
    [SerializeField] private float rampStartBpm = 120f;
    [SerializeField] private float rampEndBpm = 140f;

    [Header("Status")]
    [SerializeField] private string statusMessage = "Ready";
    [SerializeField] private bool setupComplete = false;

    // ════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ════════════════════════════════════════════════════════════

    void Start() {
        LogInfo("BeatLevelTempoTestController initialized");
        LogInfo("Click buttons in order: 1) Setup Metronome, 2) Run tests");
    }

    // ════════════════════════════════════════════════════════════
    // SETUP
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Setup test metronome with default settings
    /// Simple 120 BPM, 4/4 time - ready for beat-level testing
    /// </summary>
    [ContextMenu("1. Setup Test Metronome")]
    public void SetupTestMetronome() {
        LogSection("SETTING UP TEST METRONOME");

        if (!ValidateReferences()) return;

        // Configure metronome to basic settings
        testMetronome.SetTempo(120f);
        testMetronome.SetTimeSignature(4, 4); // 4/4 time

        setupComplete = true;
        statusMessage = "Setup complete - Ready for testing";

        LogSuccess("Test metronome configured");
        LogInfo("  Initial: 120 BPM, 4/4 time");
        LogInfo("  Scheduler: v2 (beat-level scheduling enabled)");
        LogInfo("  Ready for Phase 1 tests!");
    }

    // ════════════════════════════════════════════════════════════
    // PHASE 1 TESTS
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// TEST 1: Single beat-level tempo change
    /// Verify that tempo changes can occur at specific beats, not just measure starts
    /// </summary>
    [ContextMenu("2a. Test Single Beat-Level Change")]
    public void TestSingleBeatLevelChange() {
        if (!ValidateSetup()) return;

        LogSection("TEST 1: SINGLE BEAT-LEVEL TEMPO CHANGE");
        LogInfo($"Scheduling tempo change to {testBpm} BPM at M{testMeasure} B{testBeat}");

        // Schedule beat-level change using the new v2 constructor
        var change = new MetronomeChange_v2(
            testMeasure,
            testBeat,
            testBpm,
            $"Test beat-level change at B{testBeat}"
        );

        scheduler.ScheduleChange(change);

        statusMessage = $"Test scheduled: {testBpm} BPM at M{testMeasure} B{testBeat}";
        LogSuccess($"✓ Change scheduled");
        LogInfo($"Watch the metronome - tempo should change at M{testMeasure} B{testBeat}, NOT at measure start");
    }

    /// <summary>
    /// TEST 2: Manual beat-level ramp
    /// Generate smooth tempo ramp by manually creating beat-level changes
    /// This proves the infrastructure works before we build automatic ramp generation
    /// </summary>
    [ContextMenu("2b. Test Manual Beat-Level Ramp")]
    public void TestManualBeatLevelRamp() {
        if (!ValidateSetup()) return;

        LogSection("TEST 2: MANUAL BEAT-LEVEL RAMP");
        LogInfo($"Creating {rampDurationMeasures}-measure ramp from {rampStartBpm} to {rampEndBpm} BPM");

        // Calculate ramp parameters
        int totalBeats = rampDurationMeasures * 4; // Assuming 4/4 time
        float bpmRange = rampEndBpm - rampStartBpm;
        float bpmPerBeat = bpmRange / totalBeats;

        LogInfo($"  Total beats: {totalBeats}");
        LogInfo($"  BPM change per beat: {bpmPerBeat:F2}");

        int changesScheduled = 0;

        // Generate beat-level changes
        for (int m = 0; m < rampDurationMeasures; m++) {
            int measure = rampStartMeasure + m;

            for (int b = 1; b <= 4; b++) {
                int beatIndex = (m * 4) + (b - 1);
                float targetBpm = rampStartBpm + (beatIndex * bpmPerBeat);

                var change = new MetronomeChange_v2(
                    measure,
                    b,
                    targetBpm,
                    $"Ramp beat {beatIndex + 1}/{totalBeats}"
                );

                scheduler.ScheduleChange(change);
                changesScheduled++;

                LogInfo($"  M{measure} B{b}: {targetBpm:F1} BPM");
            }
        }

        statusMessage = $"Manual ramp: {changesScheduled} beat-level changes scheduled";
        LogSuccess($"✓ {changesScheduled} beat-level changes scheduled");
        LogInfo($"Watch the metronome - tempo should smoothly accelerate from M{rampStartMeasure}");
    }

    /// <summary>
    /// TEST 3: Mixed measure-level and beat-level changes
    /// Verify backward compatibility - measure-level changes (targetBeat=1) still work
    /// </summary>
    [ContextMenu("2c. Test Mixed Changes")]
    public void TestMixedChanges() {
        if (!ValidateSetup()) return;

        LogSection("TEST 3: MIXED MEASURE-LEVEL AND BEAT-LEVEL CHANGES");
        LogInfo("Testing backward compatibility with traditional measure-level changes");

        // Traditional measure-level change (targetBeat defaults to 1)
        scheduler.ScheduleTempoChange(8, 100f, "Traditional measure-level change");
        LogInfo("  M8 B1: 100 BPM (measure-level)");

        // Beat-level changes
        scheduler.ScheduleTempoChangeAtBeat(10, 2, 110f, "Beat-level change");
        LogInfo("  M10 B2: 110 BPM (beat-level)");

        scheduler.ScheduleTempoChangeAtBeat(10, 4, 120f, "Beat-level change");
        LogInfo("  M10 B4: 120 BPM (beat-level)");

        // Another measure-level change
        scheduler.ScheduleTempoChange(12, 130f, "Back to measure-level");
        LogInfo("  M12 B1: 130 BPM (measure-level)");

        statusMessage = "Mixed changes scheduled";
        LogSuccess("✓ Mixed changes scheduled - verifying backward compatibility");
    }

    // ════════════════════════════════════════════════════════════
    // VALIDATION & UTILITIES
    // ════════════════════════════════════════════════════════════

    private bool ValidateReferences() {
        if (testMetronome == null) {
            LogError("Test metronome not assigned!");
            return false;
        }

        if (scheduler == null) {
            LogError("Scheduler not assigned!");
            return false;
        }

        return true;
    }

    private bool ValidateSetup() {
        if (!ValidateReferences()) return false;

        if (!setupComplete) {
            LogError("Must setup test metronome first! Click '1. Setup Test Metronome'");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reset everything for fresh testing
    /// </summary>
    [ContextMenu("0. Reset All")]
    public void ResetAll() {
        LogSection("RESET");

        if (scheduler != null) {
            scheduler.ClearPendingChanges();
        }

        if (testMetronome != null && testMetronome.IsPlaying) {
            testMetronome.StopMetronome();
        }

        setupComplete = false;
        statusMessage = "Reset complete - Ready";

        LogInfo("All pending changes cleared");
        LogInfo("Metronome stopped");
        LogInfo("Ready for fresh test run");
    }

    /// <summary>
    /// Debug: View all pending changes
    /// </summary>
    [ContextMenu("Debug: View Pending Changes")]
    public void DebugPendingChanges() {
        if (scheduler != null) {
            scheduler.DebugPendingChanges();
        } else {
            LogError("Scheduler not assigned!");
        }
    }

    // ════════════════════════════════════════════════════════════
    // LOGGING
    // ════════════════════════════════════════════════════════════

    private void LogSection(string title) {
        Debug.Log($"\n{'═',60}\n[BeatLevelTest] {title}\n{'═',60}");
    }

    private void LogInfo(string message) {
        Debug.Log($"[BeatLevelTest] {message}");
    }

    private void LogSuccess(string message) {
        Debug.Log($"[BeatLevelTest] ✅ {message}");
    }

    private void LogError(string message) {
        Debug.LogError($"[BeatLevelTest] ❌ {message}");
    }

    // ════════════════════════════════════════════════════════════
    // INSPECTOR HELPERS
    // ════════════════════════════════════════════════════════════

    private void OnValidate() {
        // Ensure beat is valid (1-4 for 4/4 time)
        if (testBeat < 1) testBeat = 1;
        if (testBeat > 4) testBeat = 4;

        // Ensure ramp duration is reasonable
        if (rampDurationMeasures < 1) rampDurationMeasures = 1;
        if (rampDurationMeasures > 10) rampDurationMeasures = 10;
    }
}