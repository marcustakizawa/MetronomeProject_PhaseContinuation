using UnityEngine;
using UnityEngine.UI;
using ChangeComposer.Data;
using ChangeComposer.Indexing;

/// <summary>
/// Phase 3: Three Metronome Coordination System - DAW TIMELINE VERSION v7
/// Version: 2025-08-15 v7 (No Coroutines)
/// Date: August 15, 2025
/// 
/// CORE PRINCIPLE: DAW-style timeline activation WITHOUT coroutines
/// - Calculate timeline position for reference metronome
/// - Only start metronomes that have events at that timeline moment
/// - Use precise AudioSettings.dspTime scheduling for delayed starts
/// - Each metronome runs independently after activation
/// 
/// KEY INSIGHT: Like a DAW, only tracks with content at the timeline position should be active
/// Others join when they naturally reach their next event measure
/// </summary>
public class ThreeMetronomeCoordinator_20250815_v7 : MonoBehaviour {

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
    private ChangeSequence sequence1, sequence2, sequence3;
    private bool indexesLoaded = false;

    // Timeline tracking for delayed activations
    private struct ScheduledActivation {
        public PrecisionMetronome metronome;
        public CompositionIndex index;
        public TextAsset jsonFile;
        public int measure;
        public double activationTime;
        public bool isActive;
    }

    private ScheduledActivation[] scheduledActivations = new ScheduledActivation[3];
    private int activeScheduledCount = 0;

    void Start() {
        SetupUI();
        Debug.Log("ThreeMetronomeCoordinator v7 - DAW TIMELINE VERSION (No Coroutines)");
    }

    void Update() {
        // Check for scheduled activations that should trigger now
        ProcessScheduledActivations();
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

    /// <summary>
    /// Process any scheduled metronome activations that should trigger now
    /// </summary>
    void ProcessScheduledActivations() {
        if (activeScheduledCount == 0) return;

        double currentTime = AudioSettings.dspTime;

        for (int i = 0; i < scheduledActivations.Length; i++) {
            if (scheduledActivations[i].isActive && currentTime >= scheduledActivations[i].activationTime) {
                // Activate this metronome
                var activation = scheduledActivations[i];
                Debug.Log($"ACTIVATING: {activation.metronome.name} at measure {activation.measure} (T={currentTime:F2}s)");

                StartMetronomeNow(activation.metronome, activation.index, activation.jsonFile, activation.measure);

                // Mark as inactive
                scheduledActivations[i].isActive = false;
                activeScheduledCount--;
            }
        }
    }

    /// <summary>
    /// Schedule a metronome to activate at a specific timeline moment
    /// </summary>
    void ScheduleMetronomeActivation(PrecisionMetronome metronome, CompositionIndex index, TextAsset jsonFile, int measure, double activationTime) {
        // Find an available slot
        for (int i = 0; i < scheduledActivations.Length; i++) {
            if (!scheduledActivations[i].isActive) {
                scheduledActivations[i] = new ScheduledActivation {
                    metronome = metronome,
                    index = index,
                    jsonFile = jsonFile,
                    measure = measure,
                    activationTime = activationTime,
                    isActive = true
                };
                activeScheduledCount++;
                Debug.Log($"SCHEDULED: {metronome.name} to activate at T={activationTime:F2}s (measure {measure})");
                break;
            }
        }
    }

    /// <summary>
    /// Clear all scheduled activations
    /// </summary>
    void ClearScheduledActivations() {
        for (int i = 0; i < scheduledActivations.Length; i++) {
            scheduledActivations[i].isActive = false;
        }
        activeScheduledCount = 0;
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
    /// DAW-STYLE TIMELINE COORDINATION
    /// Only start metronomes that should be active at the reference timeline moment
    /// Schedule others to activate when they reach their next events
    /// </summary>
    public void StartFromTemporalCoordinate(int referenceM1Measure) {
        if (!indexesLoaded) {
            Debug.LogError("Indexes not loaded!");
            return;
        }

        Debug.Log($"=== DAW TIMELINE START FROM M{referenceM1Measure} ===");

        // Stop any playing metronomes and clear scheduled activations
        StopAll();
        ClearScheduledActivations();

        // Calculate the timeline position when M1 reaches the reference measure
        double timelinePosition = CalculateM1TimelinePosition(referenceM1Measure);
        Debug.Log($"Timeline position when M1 reaches M{referenceM1Measure}: {timelinePosition:F2} seconds");

        // Handle each metronome according to DAW timeline logic
        HandleMetronome1Timeline(referenceM1Measure);
        HandleMetronome2Timeline(timelinePosition);
        HandleMetronome3Timeline(timelinePosition);

        Debug.Log("DAW timeline coordination started");
    }

    /// <summary>
    /// Handle M1 timeline - always starts immediately since it's the reference
    /// </summary>
    void HandleMetronome1Timeline(int measure) {
        Debug.Log($"M1: Starting immediately at measure {measure}");
        StartMetronomeNow(metronome1, index1, track1JsonFile, measure);
    }

    /// <summary>
    /// Handle M2 timeline - start now or schedule for later
    /// M2 should wait for its NEXT event, not start immediately
    /// </summary>
    void HandleMetronome2Timeline(double timelinePosition) {
        var nextEvent = FindNextEventAfterTimelinePosition(sequence2, index2, timelinePosition);

        if (nextEvent.shouldStartNow) {
            Debug.Log($"M2: Starting immediately at measure {nextEvent.measure}");
            StartMetronomeNow(metronome2, index2, track2JsonFile, nextEvent.measure);
        } else if (nextEvent.measure > 0) {
            Debug.Log($"M2: Will activate at T={nextEvent.activationTime:F2}s (measure {nextEvent.measure})");
            ScheduleMetronomeActivation(metronome2, index2, track2JsonFile, nextEvent.measure, nextEvent.activationTime);
        } else {
            Debug.Log("M2: No future events found - will not activate");
        }
    }

    /// <summary>
    /// Handle M3 timeline - start now or schedule for later
    /// M3 should wait for its NEXT event, not start immediately
    /// </summary>
    void HandleMetronome3Timeline(double timelinePosition) {
        var nextEvent = FindNextEventAfterTimelinePosition(sequence3, index3, timelinePosition);

        if (nextEvent.shouldStartNow) {
            Debug.Log($"M3: Starting immediately at measure {nextEvent.measure}");
            StartMetronomeNow(metronome3, index3, track3JsonFile, nextEvent.measure);
        } else if (nextEvent.measure > 0) {
            Debug.Log($"M3: Will activate at T={nextEvent.activationTime:F2}s (measure {nextEvent.measure})");
            ScheduleMetronomeActivation(metronome3, index3, track3JsonFile, nextEvent.measure, nextEvent.activationTime);
        } else {
            Debug.Log("M3: No future events found - will not activate");
        }
    }

    /// <summary>
    /// Find the next event for a track after the given timeline position
    /// For non-reference tracks, we want their NEXT event, not current event
    /// </summary>
    (bool shouldStartNow, int measure, double activationTime) FindNextEventAfterTimelinePosition(ChangeSequence sequence, CompositionIndex index, double timelinePosition) {
        // Calculate which measure this track should be at for the current timeline position
        int currentMeasure = CalculateTrackMeasureAtTime(index, timelinePosition);

        // Find the NEXT event in the sequence (not the current one)
        var sortedChanges = sequence.GetSortedChanges();
        int nextEventMeasure = -1;

        foreach (var change in sortedChanges) {
            // FIX: Include events AT current measure, not just AFTER (>= instead of >)
            // This allows metronomes to start immediately if they have events at the timeline position
            if (change.targetMeasure >= currentMeasure) {
                nextEventMeasure = change.targetMeasure;
                break;
            }
        }

        // If no future events found, this track won't activate
        if (nextEventMeasure == -1) {
            return (false, currentMeasure, 0);
        }

        // Calculate when this track will reach the next event measure from current position
        double timeFromCurrentToNextEvent = CalculateTimeBetweenMeasures(index, currentMeasure, nextEventMeasure);
        double activationTime = AudioSettings.dspTime + timeFromCurrentToNextEvent;

        // Should start now if the activation time is very close (within 0.1 seconds)
        bool shouldStartNow = timeFromCurrentToNextEvent <= 0.1;

        return (shouldStartNow, nextEventMeasure, activationTime);
    }

    /// <summary>
    /// Calculate time needed to progress from one measure to another within a track
    /// </summary>
    double CalculateTimeBetweenMeasures(CompositionIndex index, int fromMeasure, int toMeasure) {
        if (fromMeasure >= toMeasure) return 0;

        double totalTime = 0;

        for (int measure = fromMeasure; measure < toMeasure; measure++) {
            if (measure <= index.measureStates.Count) {
                var state = index.measureStates[measure - 1];
                double measureDuration = (60.0 / state.bpm) * state.beatsPerMeasure;
                totalTime += measureDuration;
            }
        }

        return totalTime;
    }

    /// <summary>
    /// Start a metronome immediately
    /// </summary>
    void StartMetronomeNow(PrecisionMetronome metronome, CompositionIndex index, TextAsset jsonFile, int measure) {
        ApplyState(metronome, index.GetStateAtMeasure(measure));
        ScheduleFutureChanges(metronome, jsonFile, measure);

        double startTime = AudioSettings.dspTime + 0.1;
        metronome.SetStartTime(startTime);
        metronome.StartAtMeasure(measure);
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

        // Also clear any pending scheduled activations
        ClearScheduledActivations();
    }

    public void ResetAll() {
        if (metronome1) metronome1.ResetMetronome();
        if (metronome2) metronome2.ResetMetronome();
        if (metronome3) metronome3.ResetMetronome();

        ClearScheduledActivations();
    }
}