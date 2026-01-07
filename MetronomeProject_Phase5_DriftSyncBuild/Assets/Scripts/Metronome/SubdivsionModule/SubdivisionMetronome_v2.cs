using System;
using UnityEngine;
using ChangeComposer.Data;

/// <summary>
/// SubdivisionMetronome - Parallel metronome for rhythm subdivisions
/// Version: 2025-11-09 v1.1 (Compatible with PrecisionMetronome_v4_MultipleDisplays)
/// 
/// PURPOSE: Provide subdivision clicks (eighth notes, triplets, sixteenths)
/// - Runs parallel to parent PrecisionMetronome
/// - Automatically follows parent's tempo and time signature changes
/// - Simple on/off controls for audio and visual feedback
/// - Zero impact on parent metronome's functionality
/// - Uses same precision timing architecture as PrecisionMetronome
/// 
/// COMPATIBILITY: Works with PrecisionMetronome_v4_MultipleDisplays
/// 
/// USAGE:
/// 1. Add this component to a GameObject
/// 2. Assign the parent PrecisionMetronome_v4_MultipleDisplays
/// 3. Set subdivision type (Eighth, Triplet, or Sixteenth)
/// 4. Assign subdivision audio clip
/// 5. Optionally assign visual indicator
/// 6. Toggle audio/visual on/off as needed
/// </summary>

public class SubdivisionMetronome_v2 : MonoBehaviour
{
    [Header("Parent Connection")]
    [SerializeField] private PrecisionMetronome_v4_MultipleDisplays parentMetronome;
    [SerializeField] private SubdivisionType subdivisionType = SubdivisionType.Eighth;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip subdivisionClick;
    [SerializeField] private bool audioEnabled = false;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.7f;

    [Header("Visual Settings")]
    [SerializeField] private MetronomeVisualIndicator visualIndicator;
    [SerializeField] private bool visualEnabled = false;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool logBeatTriggers = false;

    // Events for external systems
    public event Action OnSubdivisionBeatTriggered;
    public event Action<bool> OnAudioToggled;
    public event Action<bool> OnVisualToggled;

    // Internal timing state
    private AudioSource audioSource;
    private double nextBeatTime;
    private double beatIntervalSeconds;
    private int subdivisionFactor;
    private bool isPlaying = false;
    private int beatCount = 0;

    // Properties for external access
    public bool IsPlaying => isPlaying;
    public bool AudioEnabled => audioEnabled;
    public bool VisualEnabled => visualEnabled;
    public int SubdivisionFactor => subdivisionFactor;
    public float CurrentBPM => parentMetronome != null ? parentMetronome.Bpm * subdivisionFactor : 0;
    public int CurrentMeasure => parentMetronome != null ? parentMetronome.CurrentMeasure : 0;
    public SubdivisionType Type => subdivisionType;

    // === INITIALIZATION ===

    void Start() {
        Initialize();
    }

    void Initialize() {
        // Validate parent metronome
        if (parentMetronome == null) {
            LogError("Parent metronome not assigned! SubdivisionMetronome will not function.");
            enabled = false;
            return;
        }

        // Setup audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.volume = volume;

        // Calculate subdivision factor
        subdivisionFactor = GetSubdivisionFactor(subdivisionType);

        // Subscribe to parent events
        SubscribeToParentEvents();

        // Initialize timing from parent
        UpdateTimingFromParent();

        LogDebug($"SubdivisionMetronome initialized: {subdivisionType} ({subdivisionFactor}x)");
    }

    void SubscribeToParentEvents() {
        if (parentMetronome == null) return;

        parentMetronome.OnChangeApplied += OnParentChangeApplied;
        parentMetronome.OnStarted += OnParentStarted;
        parentMetronome.OnPaused += OnParentPaused;
        parentMetronome.OnStopped += OnParentStopped;
        parentMetronome.OnReset += OnParentReset;

        LogDebug("Subscribed to parent metronome events");
    }

    void OnDestroy() {
        // Unsubscribe from parent events
        if (parentMetronome != null) {
            parentMetronome.OnChangeApplied -= OnParentChangeApplied;
            parentMetronome.OnStarted -= OnParentStarted;
            parentMetronome.OnPaused -= OnParentPaused;
            parentMetronome.OnStopped -= OnParentStopped;
            parentMetronome.OnReset -= OnParentReset;
        }
    }

    // === UPDATE LOOP ===

    void Update() {
        if (!isPlaying) return;

        double currentTime = AudioSettings.dspTime;

        // Check if it's time for next beat
        if (currentTime >= nextBeatTime) {
            TriggerBeat();
            ScheduleNextBeat();
        }
    }

    // === PARENT EVENT HANDLERS ===

    void OnParentChangeApplied(MetronomeChange change) {
        LogDebug($"Parent change detected: Type={change.type}, HasTempo={change.hasTempo}, HasTimeSignature={change.hasTimeSignature}");

        // React immediately to tempo or time signature changes
        if (change.hasTempo || change.hasTimeSignature) {
            UpdateTimingFromParent();
            LogDebug($"Updated timing: New BPM = {CurrentBPM:F1}");
        }
    }

    void OnParentStarted() {
        // Update timing first in case parent state changed since initialization
        UpdateTimingFromParent();
        StartSubdivision();
    }

    void OnParentPaused() {
        PauseSubdivision();
    }

    void OnParentStopped() {
        StopSubdivision();
    }

    void OnParentReset() {
        ResetSubdivision();
    }

    // === CORE TIMING LOGIC ===

    void UpdateTimingFromParent() {
        if (parentMetronome == null) return;

        // Calculate subdivided BPM
        float subdividedBPM = parentMetronome.Bpm * subdivisionFactor;
        beatIntervalSeconds = 60.0 / subdividedBPM;

        LogDebug($"Timing updated: Parent BPM={parentMetronome.Bpm:F1}, Subdivision BPM={subdividedBPM:F1}");
    }

    void StartSubdivision() {
        if (isPlaying) return;

        isPlaying = true;
        beatCount = 0;

        // Schedule first beat slightly in the future
        nextBeatTime = AudioSettings.dspTime + 0.1;

        LogDebug($"Subdivision started at M{CurrentMeasure}");
    }

    void PauseSubdivision() {
        if (!isPlaying) return;

        isPlaying = false;

        // Stop any playing audio
        if (audioSource != null) {
            audioSource.Stop();
        }

        LogDebug("Subdivision paused");
    }

    void StopSubdivision() {
        if (!isPlaying) return;

        isPlaying = false;

        // Stop any playing audio
        if (audioSource != null) {
            audioSource.Stop();
        }

        LogDebug("Subdivision stopped");
    }

    void ResetSubdivision() {
        isPlaying = false;
        beatCount = 0;
        nextBeatTime = 0;

        LogDebug("Subdivision reset");
    }

    void ScheduleNextBeat() {
        nextBeatTime += beatIntervalSeconds;
    }

    void TriggerBeat() {
        beatCount++;

        if (logBeatTriggers) {
            LogDebug($"Beat {beatCount} triggered at M{CurrentMeasure}");
        }

        // Play audio if enabled
        if (audioEnabled) {
            PlayAudio();
        }

        // Trigger visual if enabled
        if (visualEnabled && visualIndicator != null) {
            visualIndicator.Flash(false); // Always regular beat (no strong beats for subdivisions)
        }

        // Fire event for external systems
        OnSubdivisionBeatTriggered?.Invoke();
    }

    void PlayAudio() {
        if (audioSource == null || subdivisionClick == null) return;

        audioSource.clip = subdivisionClick;
        audioSource.volume = volume;
        audioSource.PlayScheduled(nextBeatTime);
    }

    // === PUBLIC API ===

    /// <summary>
    /// Enable or disable subdivision audio
    /// </summary>
    public void SetAudioEnabled(bool enabled) {
        if (audioEnabled == enabled) return;

        audioEnabled = enabled;
        LogDebug($"Audio {(enabled ? "ENABLED" : "DISABLED")}");

        OnAudioToggled?.Invoke(enabled);
    }

    /// <summary>
    /// Enable or disable subdivision visual feedback
    /// </summary>
    public void SetVisualEnabled(bool enabled) {
        if (visualEnabled == enabled) return;

        visualEnabled = enabled;
        LogDebug($"Visual {(enabled ? "ENABLED" : "DISABLED")}");

        OnVisualToggled?.Invoke(enabled);
    }

    /// <summary>
    /// Toggle audio on/off
    /// </summary>
    public void ToggleAudio() {
        SetAudioEnabled(!audioEnabled);
    }

    /// <summary>
    /// Toggle visual on/off
    /// </summary>
    public void ToggleVisual() {
        SetVisualEnabled(!visualEnabled);
    }

    /// <summary>
    /// Set the volume for subdivision audio
    /// </summary>
    public void SetVolume(float newVolume) {
        volume = Mathf.Clamp01(newVolume);
        if (audioSource != null) {
            audioSource.volume = volume;
        }
    }

    /// <summary>
    /// Change the subdivision type (will update timing immediately)
    /// </summary>
    public void SetSubdivisionType(SubdivisionType newType) {
        if (subdivisionType == newType) return;

        subdivisionType = newType;
        subdivisionFactor = GetSubdivisionFactor(newType);
        UpdateTimingFromParent();

        LogDebug($"Subdivision type changed to: {newType} ({subdivisionFactor}x)");
    }

    // === HELPER METHODS ===

    int GetSubdivisionFactor(SubdivisionType type) {
        switch (type) {
            case SubdivisionType.Eighth:
                return 2;
            case SubdivisionType.Triplet:
                return 3;
            case SubdivisionType.Sixteenth:
                return 4;
            default:
                return 2;
        }
    }

    // === LOGGING ===

    void LogDebug(string message) {
        if (debugMode) {
            Debug.Log($"[SubdivisionMetronome/{subdivisionType}] {message}");
        }
    }

    void LogError(string message) {
        Debug.LogError($"[SubdivisionMetronome/{subdivisionType}] ❌ {message}");
    }

    // === DEBUG CONTEXT MENU ===

    [ContextMenu("Toggle Audio")]
    void ContextToggleAudio() {
        ToggleAudio();
    }

    [ContextMenu("Toggle Visual")]
    void ContextToggleVisual() {
        ToggleVisual();
    }

    [ContextMenu("Debug Status")]
    void ContextDebugStatus() {
        Debug.Log($"=== SUBDIVISION METRONOME STATUS ===");
        Debug.Log($"Type: {subdivisionType} ({subdivisionFactor}x)");
        Debug.Log($"Parent BPM: {parentMetronome?.Bpm:F1}");
        Debug.Log($"Subdivision BPM: {CurrentBPM:F1}");
        Debug.Log($"Current Measure: M{CurrentMeasure}");
        Debug.Log($"Playing: {isPlaying}");
        Debug.Log($"Audio: {(audioEnabled ? "ON" : "OFF")}");
        Debug.Log($"Visual: {(visualEnabled ? "ON" : "OFF")}");
        Debug.Log($"Beat Count: {beatCount}");
    }
}

