using UnityEngine;
using UnityEngine.UI;
using ChangeComposer.Data;
using ChangeComposer.Indexing;

/// <summary>
/// Phase 3: Three Metronome Coordination System - LEAN VERSION
/// Version: 2025-08-13 v5
/// Date: August 13, 2025
/// 
/// FOCUSED ON: Temporal coordinate logic testing
/// STRIPPED OUT: Extensive logging, validation, test automation
/// 
/// CORE PRINCIPLE:
/// - M1 measure number = temporal coordinate
/// - Calculate timeline position when M1 reaches target measure
/// - Resolve where each track should be at that timeline moment
/// - Start all simultaneously with correct states
/// </summary>
public class ThreeMetronomeCoordinator_20250813_v5 : MonoBehaviour {

    [Header("JSON Files")]
    [SerializeField] private TextAsset track1JsonFile;
    [SerializeField] private TextAsset track2JsonFile;
    [SerializeField] private TextAsset track3JsonFile;

    [Header("Metronomes")]
    [SerializeField] private PrecisionMetronome metronome1;
    [SerializeField] private PrecisionMetronome metronome2;
    [SerializeField] private PrecisionMetronome metronome3;

    [Header("UI Controls")]
    [SerializeField] private Button generateIndexButton;
    [SerializeField] private InputField startMeasureInput;
    [SerializeField] private Button testJumpButton;
    [SerializeField] private Button playMetronomeButton;
    [SerializeField] private Button stopMetronomeButton;
    [SerializeField] private Button resetMetronomeButton;

    // Core data
    private CompositionIndex index1, index2, index3;
    private bool indexesLoaded = false;

    void Start() {
        SetupUI();
        Debug.Log("ThreeMetronomeCoordinator v5 - LEAN VERSION");
    }

    void SetupUI() {
        if (generateIndexButton) {
            generateIndexButton.onClick.RemoveAllListeners();
            generateIndexButton.onClick.AddListener(LoadIndexes);
        }

        if (testJumpButton) {
            testJumpButton.onClick.RemoveAllListeners();
            testJumpButton.onClick.AddListener(StartFromInput);
        }

        if (playMetronomeButton) {
            playMetronomeButton.onClick.RemoveAllListeners();
            playMetronomeButton.onClick.AddListener(() => StartFromTemporalCoordinate(1));
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
            var sequence1 = ChangeSequence.FromJSON(track1JsonFile.text);
            var sequence2 = ChangeSequence.FromJSON(track2JsonFile.text);
            var sequence3 = ChangeSequence.FromJSON(track3JsonFile.text);

            index1 = CompositionIndexGenerator.GenerateIndex(sequence1);
            index2 = CompositionIndexGenerator.GenerateIndex(sequence2);
            index3 = CompositionIndexGenerator.GenerateIndex(sequence3);

            indexesLoaded = true;
            Debug.Log($"Loaded indexes: {index1.measureStates.Count}, {index2.measureStates.Count}, {index3.measureStates.Count} measures");

        } catch (System.Exception e) {
            Debug.LogError($"Failed to load indexes: {e.Message}");
            indexesLoaded = false;
        }
    }

    public void StartFromInput() {
        int measure = 1;
        if (startMeasureInput && int.TryParse(startMeasureInput.text, out int input)) {
            measure = Mathf.Max(1, input);
        }
        StartFromTemporalCoordinate(measure);
    }

    /// <summary>
    /// CORE COORDINATION LOGIC - Temporal Coordinate System
    /// </summary>
    public void StartFromTemporalCoordinate(int m1ReferenceMeasure) {
        if (!indexesLoaded) {
            Debug.LogError("Indexes not loaded!");
            return;
        }

        Debug.Log($"=== STARTING FROM TEMPORAL COORDINATE M{m1ReferenceMeasure} ===");

        // Stop any playing metronomes
        StopAll();

        // Calculate timeline position when M1 reaches the reference measure
        double timelinePosition = CalculateM1TimelinePosition(m1ReferenceMeasure);

        // Resolve which measure each track should be at that timeline moment
        int m1Measure = m1ReferenceMeasure; // By definition
        int m2Measure = CalculateTrackMeasureAtTime(index2, timelinePosition);
        int m3Measure = CalculateTrackMeasureAtTime(index3, timelinePosition);

        Debug.Log($"Timeline resolution: M1:{m1Measure}, M2:{m2Measure}, M3:{m3Measure}");

        // Apply states for each resolved measure
        ApplyState(metronome1, index1.GetStateAtMeasure(m1Measure));
        ApplyState(metronome2, index2.GetStateAtMeasure(m2Measure));
        ApplyState(metronome3, index3.GetStateAtMeasure(m3Measure));

        // Schedule future changes for each metronome
        ScheduleFutureChanges(metronome1, track1JsonFile, m1Measure);
        ScheduleFutureChanges(metronome2, track2JsonFile, m2Measure);
        ScheduleFutureChanges(metronome3, track3JsonFile, m3Measure);

        // Start all simultaneously at their resolved measures
        double startTime = AudioSettings.dspTime + 0.1;

        metronome1.SetStartTime(startTime);
        metronome2.SetStartTime(startTime);
        metronome3.SetStartTime(startTime);

        metronome1.StartAtMeasure(m1Measure);
        metronome2.StartAtMeasure(m2Measure);
        metronome3.StartAtMeasure(m3Measure);

        Debug.Log("Coordination started - each metronome will progress independently");
    }

    /// <summary>
    /// Calculate absolute timeline position when M1 reaches specified measure
    /// </summary>
    double CalculateM1TimelinePosition(int targetMeasure) {
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
    int CalculateTrackMeasureAtTime(CompositionIndex trackIndex, double targetTime) {
        double accumulatedTime = 0;
        int currentMeasure = 1;

        while (currentMeasure <= trackIndex.measureStates.Count && accumulatedTime < targetTime) {
            var state = trackIndex.measureStates[currentMeasure - 1];
            double measureDuration = (60.0 / state.bpm) * state.beatsPerMeasure;

            if (accumulatedTime + measureDuration > targetTime) {
                break; // Target time falls within this measure
            }

            accumulatedTime += measureDuration;
            currentMeasure++;
        }

        return currentMeasure;
    }

    /// <summary>
    /// Apply measure state to metronome
    /// </summary>
    void ApplyState(PrecisionMetronome metronome, MeasureState state) {
        if (metronome == null || state == null) return;

        metronome.SetTempo(state.bpm);
        metronome.SetTimeSignature(state.beatsPerMeasure);
        metronome.SetAudioMute(state.isAudioMuted);
        metronome.SetVisualFeedback(!state.areVisualsHidden);

        Debug.Log($"Applied: {state.bpm}bpm {state.beatsPerMeasure}/4 Audio:{!state.isAudioMuted}");
    }

    /// <summary>
    /// Schedule future changes for a metronome
    /// </summary>
    void ScheduleFutureChanges(PrecisionMetronome metronome, TextAsset jsonFile, int currentMeasure) {
        if (metronome == null || jsonFile == null) return;

        metronome.ClearPendingChanges();

        try {
            var sequence = ChangeSequence.FromJSON(jsonFile.text);
            int scheduledCount = 0;

            foreach (var change in sequence.changes) {
                if (change.targetMeasure > currentMeasure) {
                    metronome.ScheduleChange(change);
                    scheduledCount++;
                }
            }

            Debug.Log($"Scheduled {scheduledCount} future changes");

        } catch (System.Exception e) {
            Debug.LogError($"Failed to schedule changes: {e.Message}");
        }
    }

    public void StopAll() {
        if (metronome1) metronome1.PauseMetronome();
        if (metronome2) metronome2.PauseMetronome();
        if (metronome3) metronome3.PauseMetronome();
    }

    public void ResetAll() {
        if (metronome1) metronome1.ResetMetronome();
        if (metronome2) metronome2.ResetMetronome();
        if (metronome3) metronome3.ResetMetronome();
    }
}