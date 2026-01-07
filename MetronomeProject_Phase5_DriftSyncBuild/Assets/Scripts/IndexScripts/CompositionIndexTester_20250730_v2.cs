using UnityEngine;
using UnityEngine.UI;
using ChangeComposer.Data;
using ChangeComposer.Indexing;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// CompositionIndexTester - Enhanced version with proper change scheduling
/// 
/// VERSION: 20250730_v2
/// DATE: July 30, 2025
/// 
/// CHANGES FROM v1:
/// - Removed automatic metronome start from "Start From Measure"
/// - Added explicit UI update confirmation messages
/// - Enhanced debug output for UI state changes
/// - Manual play control for better testing workflow
/// 
/// WORKFLOW:
/// 1. Enter measure number → 2. "Start From Measure" (prepares) → 3. "Play" (starts)
/// 
/// FIXES FROM v1:
/// - "Start From Measure" now prepares everything but requires manual "Play"
/// - UI updates automatically show correct state for target measure
/// - Better separation between setup phase and execution phase
/// </summary>
public class CompositionIndexTester_20250730_v2 : MonoBehaviour {
    [Header("Script Info")]
    [SerializeField] private string scriptVersion = "20250730_v2";
    [SerializeField] private string lastUpdated = "July 30, 2025";

    [Header("Test Input")]
    [SerializeField] private TextAsset testCompositionJSON;
    [SerializeField] private int maxMeasuresToIndex = 50;

    [Header("Test Controls")]
    [SerializeField] private InputField startMeasureInput;
    [SerializeField] private Button generateIndexButton;
    [SerializeField] private Button testJumpButton;
    [SerializeField] private Button showFullIndexButton;
    [SerializeField] private Button performanceTestButton;

    [Header("Enhanced Testing (NEW v1)")]
    [SerializeField] private Button scheduleAllChangesButton;
    [SerializeField] private Button clearScheduledChangesButton;
    [SerializeField] private Button startFromMeasureButton;

    [Header("Debug Output")]
    [SerializeField] private Text debugOutput;
    [SerializeField] private int maxDebugLines = 50;

    [Header("Test Metronome")]
    [SerializeField] private PrecisionMetronome testMetronome;

    [Header("Test Metronome UI")]
    [SerializeField] private Text metronomeBpmText;
    [SerializeField] private Text metronomeBeatsText;
    [SerializeField] private Text metronomeMeasureText;
    [SerializeField] private Text scheduledChangesText;

    [Header("Test Metronome Controls")]
    [SerializeField] private Button playMetronomeButton;
    [SerializeField] private Button stopMetronomeButton;
    [SerializeField] private Button resetMetronomeButton;

    [Header("Performance Testing")]
    [SerializeField] private int performanceTestIterations = 1000;

    // Generated index and data
    private CompositionIndex generatedIndex;
    private ChangeSequence loadedSequence;

    // Debug output management
    private List<string> debugLines = new List<string>();

    void Start() {
        AppendDebugText($"=== CompositionIndexTester {scriptVersion} ===");
        AppendDebugText($"Last Updated: {lastUpdated}");
        AppendDebugText("");

        SetupButtons();

        if (testCompositionJSON != null) {
            AppendDebugText("✓ Test composition JSON loaded");
        } else {
            AppendDebugText("⚠ No test composition JSON assigned");
        }

        if (testMetronome != null) {
            AppendDebugText("✓ Test metronome assigned");

            // Subscribe to metronome events for real-time feedback
            testMetronome.OnMeasureChanged += OnMetronomeMeasureChanged;
            testMetronome.OnChangeApplied += OnMetronomeChangeApplied;
            testMetronome.OnBeatTriggered += OnMetronomeBeatTriggered;

            // Subscribe to settings changes for UI updates
            testMetronome.OnMetronomeSettingsChanged += OnSettingsChanged;
        } else {
            AppendDebugText("⚠ No test metronome assigned");
        }

        AppendDebugText("Ready to test indexing system...");
        AppendDebugText("💡 NEW: Use 'Start From Measure' for dynamic playback with changes!");
    }

    void OnDestroy() {
        // Clean up event subscriptions
        if (testMetronome != null) {
            testMetronome.OnMeasureChanged -= OnMetronomeMeasureChanged;
            testMetronome.OnChangeApplied -= OnMetronomeChangeApplied;
            testMetronome.OnBeatTriggered -= OnMetronomeBeatTriggered;
            testMetronome.OnMetronomeSettingsChanged -= OnSettingsChanged;
        }
    }

    void SetupButtons() {
        // Original buttons
        if (generateIndexButton != null)
            generateIndexButton.onClick.AddListener(GenerateTestIndex);

        if (testJumpButton != null)
            testJumpButton.onClick.AddListener(TestJumpToMeasure);

        if (showFullIndexButton != null)
            showFullIndexButton.onClick.AddListener(ShowFullIndex);

        if (performanceTestButton != null)
            performanceTestButton.onClick.AddListener(RunPerformanceTest);

        // Enhanced testing buttons (NEW in v1)
        if (scheduleAllChangesButton != null)
            scheduleAllChangesButton.onClick.AddListener(ScheduleAllChanges);

        if (clearScheduledChangesButton != null)
            clearScheduledChangesButton.onClick.AddListener(ClearScheduledChanges);

        if (startFromMeasureButton != null)
            startFromMeasureButton.onClick.AddListener(StartFromMeasure);

        // Test metronome controls
        if (playMetronomeButton != null)
            playMetronomeButton.onClick.AddListener(PlayTestMetronome);

        if (stopMetronomeButton != null)
            stopMetronomeButton.onClick.AddListener(StopTestMetronome);

        if (resetMetronomeButton != null)
            resetMetronomeButton.onClick.AddListener(ResetTestMetronome);
    }

    // === EVENT HANDLERS (NEW in v1) ===

    private void OnMetronomeMeasureChanged() {
        if (testMetronome != null) {
            int currentMeasure = testMetronome.CurrentMeasure;
            AppendDebugText($"🎵 Metronome reached M{currentMeasure}");

            // Update UI in real-time
            UpdateUIFromMetronome();
            UpdateScheduledChangesDisplay();
        }
    }

    private void OnMetronomeChangeApplied(MetronomeChange change) {
        AppendDebugText($"🔄 Change applied: M{change.targetMeasure} - {change.GetChangeDescription()}");

        // Update UI immediately when changes are applied
        UpdateUIFromMetronome();
    }

    private void OnMetronomeBeatTriggered(int beatNumber) {
        // Optional: Could show beat feedback here
        // AppendDebugText($"Beat {beatNumber}");
    }

    private void OnSettingsChanged(MetronomeChange.ChangeType changeType, float newBpm, int newBeats) {
        AppendDebugText($"⚙️ Settings changed: {changeType} - {newBpm} BPM, {newBeats}/4");
        UpdateUIFromMetronome();
    }

    // === NEW FUNCTIONALITY (v1) ===

    /// <summary>
    /// Schedule all changes to the metronome for real-time playback
    /// </summary>
    public void ScheduleAllChanges() {
        if (loadedSequence == null) {
            AppendDebugText("❌ No composition loaded - generate index first");
            return;
        }

        if (testMetronome == null) {
            AppendDebugText("❌ No test metronome assigned");
            return;
        }

        try {
            AppendDebugText("🔄 Scheduling all changes to metronome...");

            // Clear existing changes first
            testMetronome.ClearPendingChanges();

            // Schedule each change
            int scheduledCount = 0;
            foreach (var change in loadedSequence.changes) {
                testMetronome.ScheduleChange(change);
                scheduledCount++;
                AppendDebugText($"📅 Scheduled: M{change.targetMeasure} - {change.GetChangeDescription()}");
            }

            AppendDebugText($"✅ Scheduled {scheduledCount} changes");
            UpdateScheduledChangesDisplay();

        } catch (System.Exception ex) {
            AppendDebugText($"❌ Error scheduling changes: {ex.Message}");
        }
    }

    /// <summary>
    /// Clear all scheduled changes from metronome
    /// </summary>
    public void ClearScheduledChanges() {
        if (testMetronome == null) {
            AppendDebugText("❌ No test metronome assigned");
            return;
        }

        testMetronome.ClearPendingChanges();
        AppendDebugText("🗑️ Cleared all scheduled changes");
        UpdateScheduledChangesDisplay();
    }

    /// <summary>
    /// Start metronome from specific measure with proper state
    /// CORRECTED: Now includes SetCurrentMeasure() call
    /// </summary>
    public void StartFromMeasure() {
        if (generatedIndex == null) {
            AppendDebugText("❌ No index generated yet");
            return;
        }

        if (testMetronome == null) {
            AppendDebugText("❌ No test metronome assigned");
            return;
        }

        if (startMeasureInput == null || string.IsNullOrEmpty(startMeasureInput.text)) {
            AppendDebugText("❌ Enter a measure number to start from");
            return;
        }

        if (!int.TryParse(startMeasureInput.text, out int startMeasure)) {
            AppendDebugText("❌ Invalid measure number");
            return;
        }

        try {
            AppendDebugText($"🚀 Preparing metronome to start from M{startMeasure}...");

            // Get the state at the start measure
            var startState = generatedIndex.GetStateAtMeasure(startMeasure);
            if (startState == null) {
                AppendDebugText($"❌ Measure {startMeasure} not found in index");
                return;
            }

            // Reset and configure metronome
            testMetronome.ResetMetronome();

            // Apply initial state
            testMetronome.SetTempo(startState.bpm);
            testMetronome.SetTimeSignature(startState.beatsPerMeasure);
            testMetronome.SetAudioMute(startState.isAudioMuted);
            testMetronome.SetVisualFeedback(!startState.areVisualsHidden);

            // CRITICAL: Set the current measure on the metronome
            // This tells the metronome it's at M17, not M1
            testMetronome.SetCurrentMeasure(startMeasure);
            AppendDebugText($"🎯 Set metronome current measure to M{startMeasure}");

            // Schedule future changes (only changes AFTER the start measure)
            var futureChanges = loadedSequence.changes.Where(c => c.targetMeasure > startMeasure).ToList();

            testMetronome.ClearPendingChanges();
            foreach (var change in futureChanges) {
                testMetronome.ScheduleChange(change);
            }

            AppendDebugText($"✅ Applied initial state: {startState.bpm} BPM, {startState.beatsPerMeasure}/4");
            AppendDebugText($"✅ Scheduled {futureChanges.Count} future changes");

            // Debug: Show what changes were scheduled
            if (futureChanges.Count > 0) {
                AppendDebugText($"📅 Future changes scheduled:");
                foreach (var change in futureChanges.Take(3)) {
                    AppendDebugText($"   M{change.targetMeasure}: {change.GetChangeDescription()}");
                }
                if (futureChanges.Count > 3) {
                    AppendDebugText($"   ... and {futureChanges.Count - 3} more");
                }
            }

            if (!string.IsNullOrEmpty(startState.appliedChanges)) {
                AppendDebugText($"📝 Changes at start measure: {startState.appliedChanges}");
            }

            // Update UI immediately to show the new state
            UpdateUIFromMetronome();
            UpdateScheduledChangesDisplay();

            // Force UI refresh to show current state at this measure
            AppendDebugText($"🔄 UI updated to show state at M{startMeasure}");

            // DON'T start automatically - let user press Play manually
            AppendDebugText($"✅ Ready to start from M{startMeasure} - Press PLAY to begin");

        } catch (System.Exception ex) {
            AppendDebugText($"❌ Error starting from measure: {ex.Message}");
        }
    }

    /// <summary>
    /// Update UI from current metronome state (NEW v1)
    /// </summary>
    private void UpdateUIFromMetronome() {
        if (testMetronome == null) return;

        if (metronomeBpmText != null) {
            metronomeBpmText.text = $"{testMetronome.Bpm:F0} BPM";
        }

        if (metronomeBeatsText != null) {
            metronomeBeatsText.text = $"{testMetronome.BeatsPerMeasure}/4";
        }

        if (metronomeMeasureText != null) {
            metronomeMeasureText.text = $"M{testMetronome.CurrentMeasure}";
        }

        // Debug confirmation that UI was updated
        AppendDebugText($"📱 UI Updated - BPM: {testMetronome.Bpm:F0}, Beats: {testMetronome.BeatsPerMeasure}/4, Measure: M{testMetronome.CurrentMeasure}");
    }

    /// <summary>
    /// Update display showing scheduled changes (NEW v1)
    /// </summary>
    private void UpdateScheduledChangesDisplay() {
        if (scheduledChangesText == null || testMetronome == null) return;

        var pendingChanges = testMetronome.GetPendingChanges();
        if (pendingChanges.Count == 0) {
            scheduledChangesText.text = "No scheduled changes";
        } else {
            var changeList = pendingChanges.Take(5).Select(c => $"M{c.targetMeasure}: {c.GetChangeDescription()}");
            string displayText = string.Join("\n", changeList);
            if (pendingChanges.Count > 5) {
                displayText += $"\n... and {pendingChanges.Count - 5} more";
            }
            scheduledChangesText.text = displayText;
        }
    }

    // === ORIGINAL FUNCTIONALITY ===

    /// <summary>
    /// Generate index from the test JSON file
    /// </summary>
    public void GenerateTestIndex() {
        if (testCompositionJSON == null) {
            AppendDebugText("❌ No test composition JSON assigned");
            return;
        }

        try {
            AppendDebugText("🔄 Loading ChangeSequence from JSON...");

            // Load the ChangeSequence
            loadedSequence = ChangeSequence.FromJSON(testCompositionJSON.text);

            if (loadedSequence == null) {
                AppendDebugText("❌ Failed to parse JSON");
                return;
            }

            // Debug: Check what was actually loaded
            AppendDebugText("=== LOADED CHANGES ===");
            for (int i = 0; i < loadedSequence.changes.Count; i++) {
                var change = loadedSequence.changes[i];
                AppendDebugText($"Change {i}: M{change.targetMeasure} - Type: {change.type} ({(int)change.type}) - Desc: '{change.description}'");
            }
            AppendDebugText("=== END LOADED CHANGES ===");

            AppendDebugText($"✓ Loaded: '{loadedSequence.title}'");
            AppendDebugText($"✓ Changes: {loadedSequence.changes.Count}");
            AppendDebugText($"✓ Initial: {loadedSequence.initialBpm} BPM, {loadedSequence.initialBeatsPerMeasure}/4");
            AppendDebugText("");

            // Generate the index
            AppendDebugText("🔄 Generating composition index...");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            generatedIndex = CompositionIndexGenerator.GenerateIndex(loadedSequence, maxMeasuresToIndex);

            stopwatch.Stop();

            AppendDebugText($"✅ Index generated successfully!");
            AppendDebugText($"📊 Measures indexed: {generatedIndex.measureStates.Count}");
            AppendDebugText($"⏱ Generation time: {stopwatch.ElapsedMilliseconds}ms");

            var (minMeasure, maxMeasure) = generatedIndex.GetMeasureRange();
            AppendDebugText($"📏 Measure range: M{minMeasure} - M{maxMeasure}");
            AppendDebugText("");

            ShowKeyStates();

        } catch (System.Exception ex) {
            AppendDebugText($"❌ Error generating index: {ex.Message}");
        }
    }

    /// <summary>
    /// Test jumping to a specific measure (static state application)
    /// </summary>
    public void TestJumpToMeasure() {
        if (generatedIndex == null) {
            AppendDebugText("❌ No index generated yet - click 'Generate Index' first");
            return;
        }

        if (startMeasureInput == null || string.IsNullOrEmpty(startMeasureInput.text)) {
            AppendDebugText("❌ Enter a measure number to test");
            return;
        }

        if (!int.TryParse(startMeasureInput.text, out int targetMeasure)) {
            AppendDebugText("❌ Invalid measure number");
            return;
        }

        try {
            AppendDebugText($"🎯 Testing jump to M{targetMeasure}...");

            var state = generatedIndex.GetStateAtMeasure(targetMeasure);

            if (state == null) {
                AppendDebugText($"❌ Measure {targetMeasure} not found in index");
                return;
            }

            AppendDebugText($"✓ Found state: {state}");

            if (!string.IsNullOrEmpty(state.appliedChanges)) {
                AppendDebugText($"📝 Changes applied: {state.appliedChanges}");
            }

            // Apply state to test metronome (static - for testing only)
            if (testMetronome != null) {
                ApplyStateToMetronome(state);
                AppendDebugText("✓ State applied to test metronome (static)");
                AppendDebugText("💡 Use 'Start From Measure' for dynamic playback with changes");
            }

            AppendDebugText("");

        } catch (System.Exception ex) {
            AppendDebugText($"❌ Error testing jump: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply a measure state to the test metronome (static application)
    /// </summary>
    private void ApplyStateToMetronome(MeasureState state) {
        if (testMetronome == null) return;

        // Reset metronome first
        testMetronome.ResetMetronome();

        // Apply settings
        testMetronome.SetTempo(state.bpm);
        testMetronome.SetTimeSignature(state.beatsPerMeasure);
        testMetronome.SetAudioMute(state.isAudioMuted);
        testMetronome.SetVisualFeedback(!state.areVisualsHidden);

        // Update UI displays
        UpdateUIFromMetronome();

        // Debug info
        AppendDebugText($"🎵 Metronome configured: {state.bpm} BPM, {state.beatsPerMeasure}/4");

        if (state.isAudioMuted) AppendDebugText("🔇 Audio muted");
        if (state.areVisualsHidden) AppendDebugText("👁 Visuals hidden");
        if (state.shouldStop) AppendDebugText("⏹ Stop event at this measure");
    }

    // Manual metronome controls
    public void PlayTestMetronome() {
        if (testMetronome != null) {
            testMetronome.StartMetronome();
            AppendDebugText("▶️ Test metronome started");
        }
    }

    public void StopTestMetronome() {
        if (testMetronome != null) {
            testMetronome.PauseMetronome();
            AppendDebugText("⏸️ Test metronome stopped");
        }
    }

    public void ResetTestMetronome() {
        if (testMetronome != null) {
            testMetronome.ResetMetronome();
            AppendDebugText("🔄 Test metronome reset");
            UpdateUIFromMetronome();
            UpdateScheduledChangesDisplay();
        }
    }

    /// <summary>
    /// Show key states for validation
    /// </summary>
    private void ShowKeyStates() {
        AppendDebugText("🔍 Key states for validation:");

        // Show first few measures
        for (int i = 1; i <= 5 && generatedIndex.HasMeasure(i); i++) {
            var state = generatedIndex.GetStateAtMeasure(i);
            AppendDebugText($"  {state}");
        }

        // Show states with changes
        AppendDebugText("🔄 Measures with changes:");
        foreach (var state in generatedIndex.measureStates) {
            if (!string.IsNullOrEmpty(state.appliedChanges)) {
                AppendDebugText($"  {state} - {state.appliedChanges}");
            }
        }

        AppendDebugText("");
    }

    /// <summary>
    /// Show the complete index for debugging
    /// </summary>
    public void ShowFullIndex() {
        if (generatedIndex == null) {
            AppendDebugText("❌ No index generated yet");
            return;
        }

        AppendDebugText($"📋 FULL INDEX - {generatedIndex.compositionTitle}");
        AppendDebugText("".PadRight(50, '='));

        foreach (var state in generatedIndex.measureStates) {
            string line = state.ToString();
            if (!string.IsNullOrEmpty(state.appliedChanges)) {
                line += $" | Changes: {state.appliedChanges}";
            }
            AppendDebugText(line);
        }

        AppendDebugText("".PadRight(50, '='));
        AppendDebugText("");
    }

    /// <summary>
    /// Run performance tests on the index system
    /// </summary>
    public void RunPerformanceTest() {
        if (generatedIndex == null) {
            AppendDebugText("❌ No index generated yet");
            return;
        }

        AppendDebugText($"⚡ Running performance test ({performanceTestIterations} iterations)...");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Test random lookups
        System.Random random = new System.Random();
        var (minMeasure, maxMeasure) = generatedIndex.GetMeasureRange();

        for (int i = 0; i < performanceTestIterations; i++) {
            int randomMeasure = random.Next(minMeasure, maxMeasure + 1);
            var state = generatedIndex.GetStateAtMeasure(randomMeasure);
            // Do something with the state to prevent optimization
            if (state != null && state.bpm > 0) { /* keep the lookup */ }
        }

        stopwatch.Stop();

        AppendDebugText($"✅ Performance test complete!");
        AppendDebugText($"⏱ Total time: {stopwatch.ElapsedMilliseconds}ms");
        AppendDebugText($"📊 Average lookup time: {(float)stopwatch.ElapsedMilliseconds / performanceTestIterations:F3}ms");
        AppendDebugText($"🚀 Lookups per second: {(int)(performanceTestIterations / (stopwatch.ElapsedMilliseconds / 1000f))}");
        AppendDebugText("");
    }

    /// <summary>
    /// Append text to debug output with line limit management
    /// </summary>
    private void AppendDebugText(string text) {
        debugLines.Add(text);

        // Keep only recent lines
        if (debugLines.Count > maxDebugLines) {
            debugLines.RemoveAt(0);
        }

        // Update display
        if (debugOutput != null) {
            debugOutput.text = string.Join("\n", debugLines);
        }

        // Also log to console for development
        UnityEngine.Debug.Log($"[IndexTester] {text}");
    }

    /// <summary>
    /// Clear debug output
    /// </summary>
    [ContextMenu("Clear Debug Output")]
    public void ClearDebugOutput() {
        debugLines.Clear();
        if (debugOutput != null) {
            debugOutput.text = "";
        }
    }

    /// <summary>
    /// Quick test with the complex composition (static)
    /// </summary>
    [ContextMenu("Quick Test - Static")]
    public void QuickTest() {
        GenerateTestIndex();

        if (generatedIndex != null) {
            // Test a few key measures
            int[] testMeasures = { 1, 5, 12, 17, 29, 41, 50 };

            AppendDebugText("🧪 QUICK TEST RESULTS:");
            AppendDebugText("".PadRight(30, '-'));

            foreach (int measure in testMeasures) {
                var state = generatedIndex.GetStateAtMeasure(measure);
                if (state != null) {
                    AppendDebugText($"M{measure}: {state.bpm} BPM, {state.beatsPerMeasure}/4");
                    if (!string.IsNullOrEmpty(state.appliedChanges)) {
                        AppendDebugText($"     Changes: {state.appliedChanges}");
                    }
                }
            }

            AppendDebugText("".PadRight(30, '-'));
        }
    }

    /// <summary>
    /// Quick test with dynamic playback (NEW v1)
    /// </summary>
    [ContextMenu("Quick Test - Dynamic")]
    public void QuickTestDynamic() {
        GenerateTestIndex();
        ScheduleAllChanges();

        if (startMeasureInput != null) {
            startMeasureInput.text = "1";
        }

        StartFromMeasure();
        AppendDebugText("🎵 Started dynamic playback test from M1");
        AppendDebugText("💡 Watch for changes to apply automatically as measures progress!");
    }
}