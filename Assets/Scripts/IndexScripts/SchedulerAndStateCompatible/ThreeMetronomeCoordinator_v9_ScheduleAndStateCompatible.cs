using UnityEngine;
using UnityEngine.UI;
using ChangeComposer.Data;
using ChangeComposer.Indexing;

/// <summary>
/// Phase 3: Three Metronome Coordination System - DAW TIMELINE VERSION v8 - compatible with StateManager
/// Version: 2025-08-23 v8 (Universal Timeline Matching + StateManager Compatible)
/// Date: August 23, 2025
/// 
/// CORE PRINCIPLE: Universal timeline synchronization
/// - If timeline position exactly matches start of any metronome's next event → Start immediately
/// - Otherwise → Wait for next event  
/// - Works for any starting measure (M1, M5, M17, etc.)
/// - Eliminates special cases and creates perfect DAW-style behavior
/// 
/// COMPATIBILITY: Works with PrecisionMetronome_v3_StateExtract (StateManager + Scheduler)
/// </summary>

public class ThreeMetronomeCoordinator_v9_ScheduleAndStateCompatible : MonoBehaviour {
    [Header("JSON Files")]
    [SerializeField] private TextAsset track1JsonFile;
    [SerializeField] private TextAsset track2JsonFile;
    [SerializeField] private TextAsset track3JsonFile;

    [Header("Metronomes")]
    [SerializeField] private PrecisionMetronome_v3_StateExtract metronome1;
    [SerializeField] private PrecisionMetronome_v3_StateExtract metronome2;
    [SerializeField] private PrecisionMetronome_v3_StateExtract metronome3;

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

    // Scheduling system (no coroutines)
    private struct ScheduledActivation {
        public PrecisionMetronome_v3_StateExtract metronome;
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
        Debug.Log("ThreeMetronomeCoordinator v8 - UNIVERSAL TIMELINE MATCHING");
    }

    void Update() {
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

    public void StartFromInput() {
        if (!indexesLoaded) {
            Debug.LogError("Indexes not loaded. Click 'Generate Index' first.");
            return;
        }

        if (startMeasureInput && int.TryParse(startMeasureInput.text, out int measure)) {
            StartFromTemporalCoordinate(measure);
        } else {
            Debug.LogError("Invalid measure number in input field");
        }
    }

    /// <summary>
    /// Main coordination method - Universal Timeline Matching
    /// </summary>
    public void StartFromTemporalCoordinate(int referenceM1Measure) {
        if (!indexesLoaded) {
            Debug.LogError("Cannot start: indexes not loaded");
            return;
        }

        Debug.Log($"=== UNIVERSAL TIMELINE START FROM M{referenceM1Measure} ===");

        // Stop any playing metronomes and clear scheduled activations
        StopAll();
        ClearScheduledActivations();

        // Calculate the timeline position when M1 reaches the reference measure
        double timelinePosition = CalculateM1TimelinePosition(referenceM1Measure);
        Debug.Log($"Timeline position when M1 reaches M{referenceM1Measure}: {timelinePosition:F2} seconds");

        // Handle each metronome with universal timeline matching
        HandleMetronome1Timeline(referenceM1Measure);
        HandleMetronome2Timeline(timelinePosition);
        HandleMetronome3Timeline(timelinePosition);

        Debug.Log("Universal timeline coordination started");
    }

    /// <summary>
    /// Handle M1 timeline - always starts immediately since it's the reference
    /// </summary>
    void HandleMetronome1Timeline(int measure) {
        Debug.Log($"M1: Starting immediately at measure {measure} (reference track)");
        StartMetronomeNow(metronome1, index1, track1JsonFile, measure);
    }

    /// <summary>
    /// Handle M2 timeline - Universal timeline matching logic
    /// </summary>
    void HandleMetronome2Timeline(double timelinePosition) {
        var nextEvent = FindNextEventWithTimelineMatching(sequence2, index2, timelinePosition);

        if (nextEvent.shouldStartNow) {
            Debug.Log($"M2: Timeline matches M{nextEvent.measure} - Starting immediately");
            StartMetronomeNow(metronome2, index2, track2JsonFile, nextEvent.measure);
        } else if (nextEvent.measure > 0) {
            Debug.Log($"M2: Timeline doesn't match - Will activate at T={nextEvent.activationTime:F2}s (M{nextEvent.measure})");
            ScheduleMetronomeActivation(metronome2, index2, track2JsonFile, nextEvent.measure, nextEvent.activationTime);
        }
    }

    /// <summary>
    /// Handle M3 timeline - Universal timeline matching logic
    /// </summary>
    void HandleMetronome3Timeline(double timelinePosition) {
        var nextEvent = FindNextEventWithTimelineMatching(sequence3, index3, timelinePosition);

        if (nextEvent.shouldStartNow) {
            Debug.Log($"M3: Timeline matches M{nextEvent.measure} - Starting immediately");
            StartMetronomeNow(metronome3, index3, track3JsonFile, nextEvent.measure);
        } else if (nextEvent.measure > 0) {
            Debug.Log($"M3: Timeline doesn't match - Will activate at T={nextEvent.activationTime:F2}s (M{nextEvent.measure})");
            ScheduleMetronomeActivation(metronome3, index3, track3JsonFile, nextEvent.measure, nextEvent.activationTime);
        }
    }

    /// <summary>
    /// CORE NEW LOGIC: Universal Timeline Matching
    /// Find next event and check if timeline position matches its start
    /// </summary>
    (bool shouldStartNow, int measure, double activationTime) FindNextEventWithTimelineMatching(
        ChangeSequence sequence, CompositionIndex index, double timelinePosition) {

        // Calculate which measure this track would be at given the timeline position
        int currentMeasure = CalculateTrackMeasureAtTime(index, timelinePosition);

        // Find events and check for timeline matching
        var sortedChanges = sequence.GetSortedChanges();
        int nextEventMeasure = 0;
        bool shouldStartNow = false;

        foreach (var change in sortedChanges) {
            if (change.targetMeasure == currentMeasure) {
                // Check if timeline position matches the exact start of this measure
                if (TimelinePositionMatchesStartOfMeasure(change.targetMeasure, index, timelinePosition)) {
                    Debug.Log($"   🎯 Timeline matches start of M{change.targetMeasure} - Start immediately");
                    nextEventMeasure = change.targetMeasure;
                    shouldStartNow = true;
                    break;
                }
            } else if (change.targetMeasure > currentMeasure) {
                // Check if timeline position matches the exact start of this next event
                if (TimelinePositionMatchesStartOfMeasure(change.targetMeasure, index, timelinePosition)) {
                    Debug.Log($"   🎯 Timeline matches start of next event M{change.targetMeasure} - Start immediately");
                    nextEventMeasure = change.targetMeasure;
                    shouldStartNow = true;
                    break;
                } else {
                    // Timeline doesn't match - wait for this next event
                    Debug.Log($"   ⏳ Timeline doesn't match M{change.targetMeasure} - Schedule for later");
                    nextEventMeasure = change.targetMeasure;
                    shouldStartNow = false;
                    break;
                }
            }
        }

        // Calculate activation time
        double activationTime = 0;
        if (nextEventMeasure > 0 && !shouldStartNow) {
            activationTime = CalculateTimeToReachMeasure(index, nextEventMeasure, timelinePosition);
        }

        return (shouldStartNow, nextEventMeasure, activationTime);
    }

    /// <summary>
    /// NEW: Check if timeline position matches the exact start of a specific measure
    /// </summary>
    bool TimelinePositionMatchesStartOfMeasure(int targetMeasure, CompositionIndex index, double timelinePosition) {
        double measureStartTime = CalculateMeasureStartTime(index, targetMeasure);
        double tolerance = 0.1; // 100ms tolerance for floating point precision

        bool matches = Mathf.Abs((float)(timelinePosition - measureStartTime)) <= tolerance;

        Debug.Log($"   📍 Timeline check: M{targetMeasure} starts at {measureStartTime:F2}s, timeline at {timelinePosition:F2}s, matches: {matches}");

        return matches;
    }

    /// <summary>
    /// Calculate the exact start time of a specific measure within a track
    /// </summary>
    double CalculateMeasureStartTime(CompositionIndex index, int targetMeasure) {
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
    /// Schedule a metronome to activate at a specific time (no coroutines)
    /// </summary>
    void ScheduleMetronomeActivation(PrecisionMetronome_v3_StateExtract metronome, CompositionIndex index, TextAsset jsonFile, int measure, double activationTime) {
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
                break;
            }
        }
    }

    /// <summary>
    /// Process scheduled activations (called from Update)
    /// </summary>
    void ProcessScheduledActivations() {
        if (activeScheduledCount == 0) return;

        double currentTime = AudioSettings.dspTime;

        for (int i = 0; i < scheduledActivations.Length; i++) {
            if (scheduledActivations[i].isActive && currentTime >= scheduledActivations[i].activationTime) {
                var activation = scheduledActivations[i];
                Debug.Log($"🎵 ACTIVATING: {activation.metronome.name} at measure {activation.measure}");
                StartMetronomeNow(activation.metronome, activation.index, activation.jsonFile, activation.measure);
                scheduledActivations[i].isActive = false;
                activeScheduledCount--;
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
        Debug.Log("🧹 Cleared all scheduled activations");
    }

    /// <summary>
    /// Start a metronome immediately
    /// </summary>
    void StartMetronomeNow(PrecisionMetronome_v3_StateExtract metronome, CompositionIndex index, TextAsset jsonFile, int measure) {
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
                break;
            }

            accumulatedTime += measureDuration;
            currentMeasure++;
        }

        return currentMeasure;
    }

    /// <summary>
    /// Calculate when a track will reach a specific measure from current timeline position
    /// </summary>
    double CalculateTimeToReachMeasure(CompositionIndex index, int targetMeasure, double currentTimelinePosition) {
        double targetMeasureTime = CalculateMeasureStartTime(index, targetMeasure);
        double timeFromCurrentPosition = targetMeasureTime - currentTimelinePosition;
        return AudioSettings.dspTime + timeFromCurrentPosition;
    }

    /// <summary>
    /// Apply state to metronome
    /// </summary>
    void ApplyState(PrecisionMetronome_v3_StateExtract metronome, MeasureState state) {
        metronome.SetTempo(state.bpm);
        metronome.SetTimeSignature(state.beatsPerMeasure);
        metronome.SetAudioMute(state.isAudioMuted);
        metronome.SetVisualFeedback(!state.areVisualsHidden);
    }

    /// <summary>
    /// Schedule future changes for a metronome
    /// </summary>
    void ScheduleFutureChanges(PrecisionMetronome_v3_StateExtract metronome, TextAsset jsonFile, int startMeasure) {
        var sequence = ChangeSequence.FromJSON(jsonFile.text);
        var futureChanges = sequence.changes.FindAll(c => c.targetMeasure > startMeasure);

        foreach (var change in futureChanges) {
            metronome.ScheduleChange(change);
        }

        Debug.Log($"📅 Scheduled {futureChanges.Count} future changes for {metronome.name}");
    }

    public void StopAll() {
        metronome1?.StopMetronome();
        metronome2?.StopMetronome();
        metronome3?.StopMetronome();
        ClearScheduledActivations();
        Debug.Log("⏹️ All metronomes stopped");
    }

    public void ResetAll() {
        StopAll();
        Debug.Log("🔄 All metronomes reset");
    }
}