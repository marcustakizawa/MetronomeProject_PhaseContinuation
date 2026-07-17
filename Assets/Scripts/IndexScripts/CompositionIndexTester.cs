using UnityEngine;
using UnityEngine.UI;
using ChangeComposer.Data;
using ChangeComposer.Indexing;
using System.Diagnostics;
using Debug = UnityEngine.Debug; // Explicitly use Unity's Debug

/// <summary>
/// Test component for validating the Composition Index system
/// Phase 1: Single metronome testing
/// </summary>
public class CompositionIndexTester : MonoBehaviour {
    [Header("Test Input")]
    [SerializeField] private TextAsset testCompositionJSON;
    [SerializeField] private int maxMeasuresToIndex = 50;

    [Header("Test Controls")]
    [SerializeField] private InputField startMeasureInput;
    [SerializeField] private Button generateIndexButton;
    [SerializeField] private Button testJumpButton;
    [SerializeField] private Button showFullIndexButton;
    [SerializeField] private Text debugOutput;
    [SerializeField] private ScrollRect debugScrollRect;

    [Header("Test Metronome")]
    [SerializeField] private PrecisionMetronome testMetronome;

    [Header("Test Metronome UI")]
    [SerializeField] private Text metronomeBpmText;
    [SerializeField] private Text metronomeBeatsText;
    [SerializeField] private Text metronomeMeasureText;

    [Header("Test Metronome Controls")]
    [SerializeField] private Button playMetronomeButton;
    [SerializeField] private Button stopMetronomeButton;
    [SerializeField] private Button resetMetronomeButton;

    [Header("Performance Testing")]
    [SerializeField] private Button performanceTestButton;
    [SerializeField] private int performanceTestIterations = 1000;

    // Generated index
    private CompositionIndex generatedIndex;
    private ChangeSequence loadedSequence;

    void Start() {
        SetupButtons();

        if (testCompositionJSON != null) {
            AppendDebugText("✓ Test composition JSON loaded");
        } else {
            AppendDebugText("⚠ No test composition JSON assigned");
        }

        if (testMetronome != null) {
            AppendDebugText("✓ Test metronome assigned");
        } else {
            AppendDebugText("⚠ No test metronome assigned");
        }

        AppendDebugText("Ready to test indexing system...");

    }

    void SetupButtons() {
        if (generateIndexButton != null)
            generateIndexButton.onClick.AddListener(GenerateTestIndex);

        if (testJumpButton != null)
            testJumpButton.onClick.AddListener(TestJumpToMeasure);

        if (showFullIndexButton != null)
            showFullIndexButton.onClick.AddListener(ShowFullIndex);

        if (performanceTestButton != null)
            performanceTestButton.onClick.AddListener(RunPerformanceTest);

        if (playMetronomeButton != null)
            playMetronomeButton.onClick.AddListener(PlayTestMetronome);

        if (stopMetronomeButton != null)
            stopMetronomeButton.onClick.AddListener(StopTestMetronome);

        if (resetMetronomeButton != null)
            resetMetronomeButton.onClick.AddListener(ResetTestMetronome);
    }

    /// <summary>
    /// Buttons to test and control playback on test metronome
    /// </summary>
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
        }
    }

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
            Debug.Log($"JSON text preview: {testCompositionJSON.text.Substring(0, 200)}...");

            // DEBUG: Check what was actually loaded
            Debug.Log("=== LOADED CHANGES ===");
            for (int i = 0; i < loadedSequence.changes.Count; i++) {
                var change = loadedSequence.changes[i];
                Debug.Log($"Change {i}: M{change.targetMeasure} - Type: {change.type} ({(int)change.type}) - Desc: '{change.description}'");
            }
            Debug.Log("=== END LOADED CHANGES ===");

            if (loadedSequence == null) {
                AppendDebugText("❌ Failed to parse JSON");
                return;
            }

            AppendDebugText($"✓ Loaded: '{loadedSequence.title}'");
            AppendDebugText($"✓ Changes: {loadedSequence.changes.Count}");
            AppendDebugText($"✓ Initial: {loadedSequence.initialBpm} BPM, {loadedSequence.initialBeatsPerMeasure}/4");
            AppendDebugText("");

            // Generate the index
            AppendDebugText("🔄 Generating composition index...");
            var stopwatch = Stopwatch.StartNew();

            generatedIndex = CompositionIndexGenerator.GenerateIndex(loadedSequence, maxMeasuresToIndex);

            stopwatch.Stop();

            AppendDebugText($"✅ Index generated successfully!");
            AppendDebugText($"📊 Measures indexed: {generatedIndex.measureStates.Count}");
            AppendDebugText($"⏱ Generation time: {stopwatch.ElapsedMilliseconds}ms");
            AppendDebugText($"💾 Memory usage: ~{generatedIndex.measureStates.Count * 50} bytes");

            var (minMeasure, maxMeasure) = generatedIndex.GetMeasureRange();
            AppendDebugText($"📏 Measure range: M{minMeasure} - M{maxMeasure}");
            AppendDebugText("");

            // Show a few key states
            ShowKeyStates();

        } catch (System.Exception ex) {
            AppendDebugText($"❌ Error generating index: {ex.Message}");
        }
    }

    /// <summary>
    /// Test jumping to a specific measure
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

            // Apply state to test metronome if available
            if (testMetronome != null) {
                ApplyStateToMetronome(state);
                AppendDebugText("✓ State applied to test metronome");
            }

            AppendDebugText("");

        } catch (System.Exception ex) {
            AppendDebugText($"❌ Error testing jump: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply a measure state to the test metronome
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

        // Set current measure (if possible)
        // Note: This might require extending PrecisionMetronome with a SetCurrentMeasure method

        AppendDebugText($"🎵 Metronome configured: {state.bpm} BPM, {state.beatsPerMeasure}/4");

        if (state.isAudioMuted) AppendDebugText("🔇 Audio muted");
        if (state.areVisualsHidden) AppendDebugText("👁 Visuals hidden");
        if (state.shouldStop) AppendDebugText("⏹ Stop event at this measure");
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

        var stopwatch = Stopwatch.StartNew();

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
    /// Append text to debug output with auto-scroll
    /// </summary>
    private void AppendDebugText(string text) {
        if (debugOutput != null) {
            debugOutput.text += text + "\n";

            // Auto-scroll to bottom
            if (debugScrollRect != null) {
                UnityEngine.Canvas.ForceUpdateCanvases();
                debugScrollRect.verticalNormalizedPosition = 0f;
            }
        }

        // Also log to console for development
        UnityEngine.Debug.Log($"[IndexTester] {text}");
    }

    /// <summary>
    /// Clear debug output
    /// </summary>
    [ContextMenu("Clear Debug Output")]
    public void ClearDebugOutput() {
        if (debugOutput != null) {
            debugOutput.text = "";
        }
    }

    /// <summary>
    /// Quick test with the complex composition
    /// </summary>
    [ContextMenu("Quick Test")]
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
}