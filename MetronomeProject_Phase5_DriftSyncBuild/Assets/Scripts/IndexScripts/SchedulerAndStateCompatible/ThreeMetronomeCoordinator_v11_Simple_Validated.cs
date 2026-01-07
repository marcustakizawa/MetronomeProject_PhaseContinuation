using UnityEngine;
using UnityEngine.UI;
using ChangeComposer.Data;
using ChangeComposer.Indexing;

/// <summary>
/// ThreeMetronomeCoordinator - Option A (Downbeat-Only Validation)
/// Version: 2025-12-08 v11_Simple_Validated
/// 
/// SIMPLIFIED APPROACH:
/// - Only allows jumps where ALL metronomes land on measure downbeats (beat 1)
/// - Validates alignment before jumping
/// - Always stopped before jumping (never during playback)
/// - All three metronomes start immediately if validated
/// - Clear error messages for invalid jumps
/// 
/// REMOVED COMPLEXITY:
/// - No scheduled activations (no staggered starts)
/// - No timeline matching checks (validation handles this)
/// - No conditional start logic
/// - Simple: validate → start all immediately or reject
/// 
/// WORKFLOW:
/// 1. User enters measure number
/// 2. Click "Jump to Measure"
/// 3. System validates all metronomes align at downbeats
/// 4. If valid: Reset → Apply states → Start all immediately
/// 5. If invalid: Show error, suggest trying different measure
/// 
/// NOTE: For beat-level precision (staggered starts), see Option B planning document
/// </summary>

public class ThreeMetronomeCoordinator_v11_Simple_Validated : MonoBehaviour {

    [Header("JSON Files")]
    [SerializeField] private TextAsset track1JsonFile;
    [SerializeField] private TextAsset track2JsonFile;
    [SerializeField] private TextAsset track3JsonFile;

    [Header("Metronomes")]
    [SerializeField] private PrecisionMetronome_v4_MultipleDisplays metronome1;
    [SerializeField] private PrecisionMetronome_v4_MultipleDisplays metronome2;
    [SerializeField] private PrecisionMetronome_v4_MultipleDisplays metronome3;

    [Header("Reset Module")]
    [SerializeField] private RuntimeResetModule_v4_PrecisionMetronome_v4 resetModule;

    [Header("UI Controls")]
    [SerializeField] private Button generateIndexButton;
    [SerializeField] private InputField startMeasureInput;
    [SerializeField] private Button jumpToMeasureButton;
    [SerializeField] private Button playMetronomeButton;
    [SerializeField] private Button stopMetronomeButton;
    [SerializeField] private Button resetMetronomeButton;

    [Header("Validation Settings")]
    [SerializeField] private double alignmentTolerance = 0.01;  // 10ms tolerance

    // Core data
    private CompositionIndex index1, index2, index3;
    private ChangeSequence sequence1, sequence2, sequence3;
    private bool indexesLoaded = false;

    // Pending jump system (no coroutines)
    private bool jumpPending = false;
    private int pendingJumpMeasure = 0;
    private int jumpDelayFrames = 0;

    void Start() {
        SetupUI();
        Debug.Log("ThreeMetronomeCoordinator v11 (Simple Validated) - DOWNBEAT-ONLY JUMPS");
    }

    void Update() {
        ProcessPendingJump();
    }

    void SetupUI() {
        if (generateIndexButton) {
            generateIndexButton.onClick.RemoveAllListeners();
            generateIndexButton.onClick.AddListener(LoadIndexes);
        }

        if (jumpToMeasureButton) {
            jumpToMeasureButton.onClick.RemoveAllListeners();
            jumpToMeasureButton.onClick.AddListener(JumpToMeasureFromInput);
        }

        if (playMetronomeButton) {
            playMetronomeButton.onClick.RemoveAllListeners();
            playMetronomeButton.onClick.AddListener(() => JumpToMeasure(1));
        }

        if (stopMetronomeButton) {
            stopMetronomeButton.onClick.RemoveAllListeners();
            stopMetronomeButton.onClick.AddListener(StopAll);
        }

        if (resetMetronomeButton) {
            resetMetronomeButton.onClick.RemoveAllListeners();
            resetMetronomeButton.onClick.AddListener(ResetAll);
        }
    }

    public void LoadIndexes() {
        try {
            sequence1 = ChangeSequence.FromJSON(track1JsonFile.text);
            sequence2 = ChangeSequence.FromJSON(track2JsonFile.text);
            sequence3 = ChangeSequence.FromJSON(track3JsonFile.text);

            index1 = CompositionIndexGenerator.GenerateIndex(sequence1);
            index2 = CompositionIndexGenerator.GenerateIndex(sequence2);
            index3 = CompositionIndexGenerator.GenerateIndex(sequence3);

            indexesLoaded = true;
            Debug.Log("✅ All indexes loaded successfully");
            Debug.Log($"Track 1: {index1.measureStates.Count} measures");
            Debug.Log($"Track 2: {index2.measureStates.Count} measures");
            Debug.Log($"Track 3: {index3.measureStates.Count} measures");

        } catch (System.Exception ex) {
            Debug.LogError($"❌ Error loading indexes: {ex.Message}");
            indexesLoaded = false;
        }
    }

    /// <summary>
    /// Jump to measure from UI input
    /// </summary>
    public void JumpToMeasureFromInput() {
        if (!indexesLoaded) {
            Debug.LogError("❌ Cannot jump: Indexes not loaded. Click 'Generate Index' first.");
            return;
        }

        if (startMeasureInput && int.TryParse(startMeasureInput.text, out int measure)) {
            JumpToMeasure(measure);
        } else {
            Debug.LogError("❌ Invalid measure number in input field");
        }
    }

    /// <summary>
    /// Main jump method with validation
    /// </summary>
    public void JumpToMeasure(int m1Measure) {
        if (!indexesLoaded) {
            Debug.LogError("❌ Cannot jump: Indexes not loaded");
            return;
        }

        Debug.Log($"🎯 === JUMP TO MEASURE {m1Measure} REQUESTED ===");

        // VALIDATE: Check if all metronomes align at downbeats
        if (!ValidateDownbeatAlignment(m1Measure, out string errorMessage)) {
            Debug.LogError($"❌ JUMP REJECTED: {errorMessage}");
            Debug.LogError("💡 Try a different measure number where all metronomes align.");
            return;
        }

        Debug.Log("✅ Jump validated - all metronomes align at downbeats");

        // RESET: Always reset before jumping
        if (resetModule != null) {
            resetModule.ExecuteSystemReset();
        } else {
            Debug.LogWarning("⚠️ No reset module assigned - using fallback reset");
            ResetAll();
        }

        // SCHEDULE: Jump after reset settles (1 frame delay)
        ScheduleJumpForNextFrame(m1Measure);
    }

    /// <summary>
    /// VALIDATION: Check if all metronomes land on downbeats (beat 1) at target timeline
    /// </summary>
    private bool ValidateDownbeatAlignment(int m1Measure, out string errorMessage) {
        errorMessage = "";

        // Calculate timeline position at M1's target measure downbeat
        double timelinePosition = CalculateM1TimelinePosition(m1Measure);

        // Calculate where M2 and M3 should be at this timeline moment
        int m2Measure = CalculateTrackMeasureAtTime(index2, timelinePosition);
        int m3Measure = CalculateTrackMeasureAtTime(index3, timelinePosition);

        Debug.Log($"Validation: Timeline at M1={m1Measure} is {timelinePosition:F3}s");
        Debug.Log($"  M2 should be at: M{m2Measure}");
        Debug.Log($"  M3 should be at: M{m3Measure}");

        // Check if M1 timeline position exactly matches M1's measure start
        double m1ExpectedTime = CalculateMeasureStartTime(index1, m1Measure);
        bool m1Aligns = Mathf.Abs((float)(timelinePosition - m1ExpectedTime)) <= alignmentTolerance;

        // Check if M2's calculated measure start matches the timeline position
        double m2ExpectedTime = CalculateMeasureStartTime(index2, m2Measure);
        bool m2Aligns = Mathf.Abs((float)(timelinePosition - m2ExpectedTime)) <= alignmentTolerance;

        // Check if M3's calculated measure start matches the timeline position
        double m3ExpectedTime = CalculateMeasureStartTime(index3, m3Measure);
        bool m3Aligns = Mathf.Abs((float)(timelinePosition - m3ExpectedTime)) <= alignmentTolerance;

        // If all align, jump is valid
        if (m1Aligns && m2Aligns && m3Aligns) {
            Debug.Log($"  ✅ M1 aligned (diff: {Mathf.Abs((float)(timelinePosition - m1ExpectedTime)) * 1000:F1}ms)");
            Debug.Log($"  ✅ M2 aligned (diff: {Mathf.Abs((float)(timelinePosition - m2ExpectedTime)) * 1000:F1}ms)");
            Debug.Log($"  ✅ M3 aligned (diff: {Mathf.Abs((float)(timelinePosition - m3ExpectedTime)) * 1000:F1}ms)");
            return true;
        }

        // Build error message showing misalignments
        errorMessage = $"Cannot jump to M{m1Measure} - Misaligned downbeats:";

        if (!m2Aligns) {
            double timeDiff = timelinePosition - m2ExpectedTime;
            var m2State = index2.GetStateAtMeasure(m2Measure);
            double beatDuration = 60.0 / m2State.bpm;
            double beatOffset = timeDiff / beatDuration;
            errorMessage += $"\n  M2 would be at M{m2Measure}, beat {1 + beatOffset:F2} (not downbeat)";
            Debug.Log($"  ❌ M2 misaligned by {Mathf.Abs((float)timeDiff) * 1000:F1}ms ({beatOffset:F2} beats)");
        }

        if (!m3Aligns) {
            double timeDiff = timelinePosition - m3ExpectedTime;
            var m3State = index3.GetStateAtMeasure(m3Measure);
            double beatDuration = 60.0 / m3State.bpm;
            double beatOffset = timeDiff / beatDuration;
            errorMessage += $"\n  M3 would be at M{m3Measure}, beat {1 + beatOffset:F2} (not downbeat)";
            Debug.Log($"  ❌ M3 misaligned by {Mathf.Abs((float)timeDiff) * 1000:F1}ms ({beatOffset:F2} beats)");
        }

        return false;
    }

    /// <summary>
    /// Schedule a jump to execute after frame delay
    /// </summary>
    private void ScheduleJumpForNextFrame(int measure) {
        jumpPending = true;
        pendingJumpMeasure = measure;
        jumpDelayFrames = 1;  // Wait 1 frame for reset to complete
    }

    /// <summary>
    /// Process pending jump in Update()
    /// </summary>
    private void ProcessPendingJump() {
        if (!jumpPending) return;

        // Count down delay frames
        if (jumpDelayFrames > 0) {
            jumpDelayFrames--;
            return;
        }

        // Execute the jump
        Debug.Log($"🚀 Executing jump to measure {pendingJumpMeasure}...");
        ExecuteJump(pendingJumpMeasure);

        // Clear pending flag
        jumpPending = false;
        pendingJumpMeasure = 0;
    }

    /// <summary>
    /// Execute the actual jump (after validation and reset)
    /// </summary>
    private void ExecuteJump(int m1Measure) {
        // Calculate timeline position
        double timelinePosition = CalculateM1TimelinePosition(m1Measure);

        // Calculate where each metronome should be
        int m2Measure = CalculateTrackMeasureAtTime(index2, timelinePosition);
        int m3Measure = CalculateTrackMeasureAtTime(index3, timelinePosition);

        Debug.Log($"=== STARTING COORDINATION ===");
        Debug.Log($"Timeline: {timelinePosition:F3}s");
        Debug.Log($"M1 → M{m1Measure}, M2 → M{m2Measure}, M3 → M{m3Measure}");

        // PHASE 1: Do ALL setup work first (heavy operations)
        ApplyStateAndScheduleChanges(metronome1, index1, track1JsonFile, m1Measure);
        ApplyStateAndScheduleChanges(metronome2, index2, track2JsonFile, m2Measure);
        ApplyStateAndScheduleChanges(metronome3, index3, track3JsonFile, m3Measure);

        // PHASE 2: NOW calculate synchronized start time (after all heavy work)
        double startTime = AudioSettings.dspTime + 0.1;
        Debug.Log($"Synchronized start time: {startTime:F3}");

        // PHASE 3: Set synchronized time and start (lightweight, fast operations)
        metronome1.SetStartTime(startTime);
        metronome1.StartAtMeasure(m1Measure);

        metronome2.SetStartTime(startTime);
        metronome2.StartAtMeasure(m2Measure);

        metronome3.SetStartTime(startTime);
        metronome3.StartAtMeasure(m3Measure);

        Debug.Log("✅ All metronomes started at same temporal coordinate");
    }

    /// <summary>
    /// Apply state and schedule future changes (setup only, no starting)
    /// </summary>
    private void ApplyStateAndScheduleChanges(PrecisionMetronome_v4_MultipleDisplays metronome,
                                              CompositionIndex index,
                                              TextAsset jsonFile,
                                              int measure) {
        // Clear any existing scheduled changes
        metronome.ClearPendingChanges();

        // Apply state for this measure
        var state = index.GetStateAtMeasure(measure);
        metronome.SetTempo(state.bpm);
        metronome.SetTimeSignature(state.beatsPerMeasure);
        metronome.SetAudioMute(state.isAudioMuted);
        metronome.SetVisualFeedback(!state.areVisualsHidden);

        // Schedule future changes (after this measure)
        var sequence = ChangeSequence.FromJSON(jsonFile.text);
        var futureChanges = sequence.changes.FindAll(c => c.targetMeasure > measure);
        foreach (var change in futureChanges) {
            metronome.ScheduleChange(change);
        }

        Debug.Log($"  {metronome.name}: Configured for M{measure} ({futureChanges.Count} changes scheduled)");
    }

    /// <summary>
    /// Calculate timeline position when M1 reaches specified measure
    /// </summary>
    private double CalculateM1TimelinePosition(int targetMeasure) {
        double totalTime = 0;

        for (int measure = 1; measure < targetMeasure; measure++) {
            if (measure <= index1.measureStates.Count) {
                var state = index1.measureStates[measure - 1];
                double measureDuration = (60.0 / state.bpm) * state.beatsPerMeasure;
                totalTime += measureDuration;
            }
        }

        return totalTime;
    }

    /// <summary>
    /// Calculate which measure a track would be at given timeline position
    /// </summary>
    private int CalculateTrackMeasureAtTime(CompositionIndex trackIndex, double targetTime) {
        double accumulatedTime = 0;
        int currentMeasure = 1;

        while (currentMeasure <= trackIndex.measureStates.Count && accumulatedTime < targetTime) {
            var state = trackIndex.measureStates[currentMeasure - 1];
            double measureDuration = (60.0 / state.bpm) * state.beatsPerMeasure;

            if (accumulatedTime + measureDuration > targetTime) {
                break;
            }

            accumulatedTime += measureDuration;
            currentMeasure++;
        }

        return currentMeasure;
    }

    /// <summary>
    /// Calculate exact start time of a specific measure
    /// </summary>
    private double CalculateMeasureStartTime(CompositionIndex index, int targetMeasure) {
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

    public void StopAll() {
        metronome1?.StopMetronome();
        metronome2?.StopMetronome();
        metronome3?.StopMetronome();
        Debug.Log("⏹️ All metronomes stopped");
    }

    public void ResetAll() {
        Debug.Log("🔄 === COMPREHENSIVE RESET ===");

        // Use RuntimeResetModule if available (preferred)
        if (resetModule != null) {
            resetModule.ExecuteSystemReset();
        } else {
            // Fallback reset if no module assigned
            Debug.LogWarning("⚠️ No reset module - using fallback reset");
            StopAll();

            // Clear pending changes
            metronome1?.ClearPendingChanges();
            metronome2?.ClearPendingChanges();
            metronome3?.ClearPendingChanges();

            // Reset metronomes (includes StateManager reset)
            metronome1?.ResetMetronome();
            metronome2?.ResetMetronome();
            metronome3?.ResetMetronome();
        }

        Debug.Log("✅ Reset complete - system ready");
    }
}