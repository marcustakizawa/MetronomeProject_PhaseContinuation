using ChangeComposer.Data;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PrecisionMetronome v5 - Beat-Level Tempo Ramps (COMPLETE VERSION)
/// Version: 2026-02-18 v5.3
/// 
/// CHANGES FROM v5.2:
/// - Added SetCurrentBeat(int beat) for coordinator beat-level alignment
///   Complements existing SetCurrentMeasure() — forces beat counter directly
///   Used by CompositionCoordinator to execute beat-level sync at runtime
/// 
/// CHANGES FROM v5.1:
/// - Added ALL missing methods from v4
/// - GetPendingChanges(), ScheduleCombinedChange(), all Schedule event methods
/// - AddVisualIndicator(), RemoveVisualIndicator(), VisualIndicator property
/// - MuteAudio(), UnmuteAudio(), ToggleAudioMute() helpers
/// - GetMeasureDisplayText(), GetMeasureDisplayColor()
/// - SetStartTime()
/// 
/// CHANGES FROM v5.0:
/// - Added TimeSignatureVisualController support
/// - Added visual beat indicators support
/// - Full visual feedback during Phase 1 testing
/// 
/// PURPOSE: Phase 1 testing of beat-level tempo scheduling
/// - v4 functionality (100% backward compatible)
/// - PLUS beat-level scheduling (new in v5)
/// - Uses MetronomeScheduler_v2 exclusively
/// - Calls scheduler.HandleBeatChanged(measure, beat) on every beat
/// - Isolated from production v4 for safe testing
/// </summary>
public class PrecisionMetronome_v5_BeatLevel : MonoBehaviour {

    // === REFACTORING COMPONENTS ===
    [Header("Refactoring Components")]
    [SerializeField] private MetronomeScheduler_v2 scheduler;
    [SerializeField] private MetronomeStateManager_v1 stateManager;

    // === EVENTS ===
    public event Action OnStarted;
    public event Action OnPaused;
    public event Action OnReset;
    public event Action OnStopped;
    public event Action<bool> OnAudioMuteChanged;
    public event Action<int> OnBeatTriggered;
    public event Action OnMeasureChanged;
    public event Action<MetronomeChange_v2, string> OnChangeNotification;
    public event Action OnLastBeatOfMeasure;
    public event Action OnPreRollCompleted;
    public event Action<float, int> OnMetronomeSettingsChanged;
    public event Action<MetronomeChange_v2> OnChangeScheduled;
    public event Action<MetronomeChange_v2> OnChangeApplied;

    // === AUDIO SETTINGS ===
    [Header("Audio")]
    [SerializeField] private AudioClip strongBeatClip;
    [SerializeField] private AudioClip regularBeatClip;
    [SerializeField] private AudioSource _audioSource;

    // === METRONOME SETTINGS ===
    [Header("Metronome Settings")]
    [SerializeField] private float _bpm = 120f;
    [SerializeField] private int _beatsPerMeasure = 4;
    [SerializeField] private int _beatUnit = 4;

    // === UI ELEMENTS ===
    [Header("UI References")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private InputField bpmInput;
    [SerializeField] private InputField beatsInput;
    [SerializeField] private Text statusText;

    // === VISUAL FEEDBACK (NEW in v5.1) ===
    [Header("Visual Feedback")]
    [SerializeField] private bool _enableVisualFeedback = true;
    [SerializeField] private List<MetronomeVisualIndicator> _visualIndicators = new List<MetronomeVisualIndicator>();
    [SerializeField] private List<TimeSignatureVisualController> timeSignatureVisualControllers = new List<TimeSignatureVisualController>();

    // === CHANGE SYSTEM ===
    [Header("Change System")]
    [SerializeField] private bool debugChangeSystem = true;

    // === INTERNAL STATE ===
    private bool _isAudioMuted = false;
    private bool _isStopped = false;
    private double _nextBeatTime;
    private double _beatIntervalSeconds;
    private int _currentBeat = 1;
    private bool _isPlaying = false;
    private bool _isFirstBeat = true;

    // === PROPERTIES ===
    public float Bpm {
        get => _bpm;
        set {
            _bpm = value;
            _beatIntervalSeconds = 60.0 / _bpm;
            if (bpmInput != null) bpmInput.text = _bpm.ToString("F2");
        }
    }

    public int BeatsPerMeasure {
        get => _beatsPerMeasure;
        set {
            _beatsPerMeasure = Mathf.Max(1, value);
            if (beatsInput != null) beatsInput.text = _beatsPerMeasure.ToString();
        }
    }

    public int BeatUnit => _beatUnit;
    public int CurrentBeat => _currentBeat;
    public double NextBeatTime => _nextBeatTime + _beatIntervalSeconds;
    public bool IsPlaying => _isPlaying;
    public int CurrentMeasure => stateManager?.CurrentMeasure ?? 1;
    public bool IsAudioMuted => _isAudioMuted;
    public bool IsStopped => _isStopped;

    // === INITIALIZATION ===
    void Start() {
        ValidateSchedulerReference();
        ValidateStateManagerReference();

        SetupSchedulerEventListeners();
        SetupStateManagerEventListeners();

        // Initialize audio
        if (_audioSource == null) {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        _beatIntervalSeconds = 60.0 / _bpm;
        _isPlaying = false;
        _isStopped = true;
        _isAudioMuted = false;
        _currentBeat = 1;
        _isFirstBeat = true;

        // Setup button listeners
        if (playButton != null) playButton.onClick.AddListener(StartMetronome);
        if (pauseButton != null) pauseButton.onClick.AddListener(PauseMetronome);
        if (resetButton != null) resetButton.onClick.AddListener(ResetMetronome);

        // Setup input fields
        if (bpmInput != null) {
            bpmInput.text = _bpm.ToString("F2");
            bpmInput.onEndEdit.AddListener(OnBpmInputChanged);
        }

        if (beatsInput != null) {
            beatsInput.text = _beatsPerMeasure.ToString();
            beatsInput.onEndEdit.AddListener(OnBeatsInputChanged);
        }

        UpdateUI();
        OnStopped?.Invoke();

        Debug.Log($"PrecisionMetronome v5.1 (Beat-Level + Visuals) {gameObject.name} initialized");
    }

    // === VALIDATION ===
    private void ValidateSchedulerReference() {
        if (scheduler == null) {
            Debug.LogError($"{gameObject.name}: MetronomeScheduler_v2 not assigned! Beat-level scheduling will not work.");
        }
    }

    private void ValidateStateManagerReference() {
        if (stateManager == null) {
            Debug.LogError($"{gameObject.name}: MetronomeStateManager_v1 not assigned! State management will not work.");
        }
    }

    // === EVENT LISTENERS ===
    private void SetupSchedulerEventListeners() {
        if (scheduler == null) return;

        scheduler.OnChangeReadyToApply += ApplyChangeFromScheduler;
        scheduler.OnChangeNotification += RelayChangeNotification;
        scheduler.OnChangeScheduled += RelayChangeScheduled;
        scheduler.OnChangeProcessed += RelayChangeProcessed;
    }

    private void SetupStateManagerEventListeners() {
        if (stateManager == null) return;

        stateManager.OnMeasureStateChanged += HandleStateManagerMeasureChanged;
        stateManager.OnBeatStateChanged += HandleStateManagerBeatChanged;
        stateManager.OnCompositionStarted += RelayCompositionStarted;
        stateManager.OnDisplayStateChanged += UpdateUI;
    }

    private void HandleStateManagerMeasureChanged(int newMeasure) {
        if (debugChangeSystem) {
            Debug.Log($"StateManager measure changed to: {newMeasure}");
        }
    }

    private void HandleStateManagerBeatChanged(int newBeat) {
        // State manager tracking beat changes
    }

    private void RelayCompositionStarted() {
        OnPreRollCompleted?.Invoke();
        if (debugChangeSystem) {
            Debug.Log("Composition started (from StateManager)");
        }
    }

    private void ApplyChangeFromScheduler(MetronomeChange_v2 change) {
        ApplyChange(change);
    }

    private void RelayChangeNotification(MetronomeChange_v2 change, string message) {
        OnChangeNotification?.Invoke(change, message);
    }

    private void RelayChangeScheduled(MetronomeChange_v2 change) {
        OnChangeScheduled?.Invoke(change);
    }

    private void RelayChangeProcessed(MetronomeChange_v2 change) {
        OnChangeApplied?.Invoke(change);
    }

    // === UPDATE LOOP (BEAT-LEVEL SCHEDULING) ===
    void Update() {
        if (!_isPlaying) return;

        double currentDspTime = AudioSettings.dspTime;

        if (currentDspTime >= _nextBeatTime) {
            // === MEASURE CHANGES ===
            if (_currentBeat == 1 && !_isFirstBeat) {
                if (stateManager != null) {
                    stateManager.HandleMeasureAdvancement();
                }

                // CRITICAL: Notify scheduler of measure change
                if (scheduler != null) {
                    scheduler.HandleMeasureChanged(CurrentMeasure);
                }

                OnMeasureChanged?.Invoke();

                if (debugChangeSystem) {
                    Debug.Log($"Measure {CurrentMeasure} started");
                }
            } else if (_currentBeat == _beatsPerMeasure) {
                OnLastBeatOfMeasure?.Invoke();
            }

            // === BEAT-LEVEL SCHEDULING (Phase 1 Feature) ===
            // This is THE critical call that enables beat-level tempo changes!
            if (scheduler != null) {
                scheduler.HandleBeatChanged(CurrentMeasure, _currentBeat);
            }

            OnBeatTriggered?.Invoke(_currentBeat);

            if (stateManager != null) {
                stateManager.HandleBeatAdvancement(_currentBeat);
            }

            _isFirstBeat = false;

            ScheduleNextBeat();

            // === VISUAL FEEDBACK (NEW in v5.1) ===
            if (_enableVisualFeedback) {
                TriggerVisualFeedback();
            }

            UpdateUI();
            IncrementBeat();
            _nextBeatTime += _beatIntervalSeconds;
        }
    }

    // === VISUAL FEEDBACK METHODS (NEW in v5.1) ===

    /// <summary>
    /// Trigger visual feedback for current beat
    /// Calls both simple indicators and time signature controllers
    /// </summary>
    private void TriggerVisualFeedback() {
        if (!_enableVisualFeedback) return;

        bool isStrongBeat = _currentBeat == 1;

        // Simple visual indicators (flash on/off)
        foreach (var indicator in _visualIndicators) {
            if (indicator != null) {
                indicator.Flash(isStrongBeat);
            }
        }

        // Time signature visual controllers (beat position display)
        foreach (var controller in timeSignatureVisualControllers) {
            if (controller != null) {
                controller.OnBeat(_currentBeat);
            }
        }
    }

    /// <summary>
    /// Add a time signature visual controller for beat display
    /// </summary>
    public void AddTimeSignatureController(TimeSignatureVisualController controller) {
        if (controller != null && !timeSignatureVisualControllers.Contains(controller)) {
            timeSignatureVisualControllers.Add(controller);
            Debug.Log($"Added TimeSignatureVisualController to {gameObject.name}");
        }
    }

    /// <summary>
    /// Remove a time signature visual controller
    /// </summary>
    public void RemoveTimeSignatureController(TimeSignatureVisualController controller) {
        if (controller != null && timeSignatureVisualControllers.Contains(controller)) {
            timeSignatureVisualControllers.Remove(controller);
            Debug.Log($"Removed TimeSignatureVisualController from {gameObject.name}");
        }
    }

    /// <summary>
    /// Add a simple visual indicator (flash on/off style)
    /// </summary>
    public void AddVisualIndicator(MetronomeVisualIndicator indicator) {
        if (indicator != null && !_visualIndicators.Contains(indicator)) {
            _visualIndicators.Add(indicator);
            Debug.Log($"Added visual indicator to {gameObject.name}");
        }
    }

    /// <summary>
    /// Remove a simple visual indicator
    /// </summary>
    public void RemoveVisualIndicator(MetronomeVisualIndicator indicator) {
        if (indicator != null && _visualIndicators.Contains(indicator)) {
            _visualIndicators.Remove(indicator);
            Debug.Log($"Removed visual indicator from {gameObject.name}");
        }
    }

    /// <summary>
    /// Single visual indicator property for backward compatibility
    /// Gets/sets the first indicator in the list
    /// </summary>
    public MetronomeVisualIndicator VisualIndicator {
        get { return _visualIndicators.Count > 0 ? _visualIndicators[0] : null; }
        set {
            _visualIndicators.Clear();
            if (value != null) {
                _visualIndicators.Add(value);
            }
        }
    }

    // === BEAT/MEASURE MECHANICS ===
    private void ScheduleNextBeat() {
        if (_audioSource != null && !_isAudioMuted) {
            AudioClip clipToPlay = _currentBeat == 1 ? strongBeatClip : regularBeatClip;

            if (clipToPlay != null) {
                _audioSource.clip = clipToPlay;
                _audioSource.PlayScheduled(_nextBeatTime);
            }
        }
    }

    private void IncrementBeat() {
        _currentBeat++;
        if (_currentBeat > _beatsPerMeasure) {
            _currentBeat = 1;
        }
    }

    // === CHANGE APPLICATION ===
    private void ApplyChange(MetronomeChange_v2 change) {
        if (debugChangeSystem) {
            Debug.Log($"Applying change: {change}");
        }

        switch (change.type) {
            case MetronomeChange_v2.ChangeType.Tempo:
                Bpm = change.newBpm;
                Debug.Log($"Applied tempo change: {change.newBpm} BPM");
                break;

            case MetronomeChange_v2.ChangeType.TimeSignature:
                BeatsPerMeasure = change.newBeatsPerMeasure;
                foreach (var controller in timeSignatureVisualControllers) {
                    if (controller != null) {
                        controller.SetTimeSignature(change.newBeatsPerMeasure);
                    }
                }
                Debug.Log($"Applied time signature change: {change.newBeatsPerMeasure} beats per measure");
                break;

            case MetronomeChange_v2.ChangeType.Both:
                Bpm = change.newBpm;
                BeatsPerMeasure = change.newBeatsPerMeasure;
                foreach (var controller in timeSignatureVisualControllers) {
                    if (controller != null) {
                        controller.SetTimeSignature(change.newBeatsPerMeasure);
                    }
                }
                Debug.Log($"Applied combined change: {change.newBpm} BPM, {change.newBeatsPerMeasure} beats per measure");
                break;

            case MetronomeChange_v2.ChangeType.Stop:
                StopMetronome();
                Debug.Log($"Applied stop event: {change.description}");
                break;

            case MetronomeChange_v2.ChangeType.Mute:
                SetAudioMute(true);
                Debug.Log($"Applied mute event: {change.description}");
                break;

            case MetronomeChange_v2.ChangeType.Unmute:
                SetAudioMute(false);
                Debug.Log($"Applied unmute event: {change.description}");
                break;

            case MetronomeChange_v2.ChangeType.VisualOff:
                SetVisualFeedback(false);
                Debug.Log($"Applied visual off event: {change.description}");
                break;

            case MetronomeChange_v2.ChangeType.VisualOn:
                SetVisualFeedback(true);
                Debug.Log($"Applied visual on event: {change.description}");
                break;

            case MetronomeChange_v2.ChangeType.Combined:
                if (change.hasTempo) Bpm = change.newBpm;
                if (change.hasTimeSignature) BeatsPerMeasure = change.newBeatsPerMeasure;
                if (change.hasAudioEvent) SetAudioMute(change.muteAudio);
                if (change.hasVisualEvent) SetVisualFeedback(!change.hideVisual);
                Debug.Log($"Applied combined change: {change.description}");
                break;

            default:
                Debug.LogWarning($"Unknown change type: {change.type}");
                break;
        }

        // Fire settings changed event for tempo/time signature changes
        if (change.type == MetronomeChange_v2.ChangeType.Tempo ||
            change.type == MetronomeChange_v2.ChangeType.TimeSignature ||
            change.type == MetronomeChange_v2.ChangeType.Both) {
            OnMetronomeSettingsChanged?.Invoke(_bpm, _beatsPerMeasure);
        }

        UpdateUI();
    }

    // === CONTROL METHODS ===
    public void StartMetronome() {
        if (_isPlaying) return;

        _isPlaying = true;
        _isFirstBeat = true;
        _isStopped = false;

        if (_nextBeatTime <= AudioSettings.dspTime) {
            _nextBeatTime = AudioSettings.dspTime + 0.1;
        }

        ScheduleNextBeat();
        UpdateUI();
        OnStarted?.Invoke();
    }

    public void StartAtMeasure(int measure) {
        _isPlaying = false;
        _currentBeat = 1;
        _isFirstBeat = true;

        if (stateManager != null) {
            stateManager.SetCurrentMeasure(measure);
        }

        _isPlaying = true;

        _nextBeatTime = AudioSettings.dspTime + 0.1;
        ScheduleNextBeat();
        UpdateUI();
        OnStarted?.Invoke();
    }

    public void PauseMetronome() {
        _isPlaying = false;

        if (_audioSource != null) {
            _audioSource.Stop();
        }

        UpdateUI();
        OnPaused?.Invoke();
    }

    public void StopMetronome() {
        _isPlaying = false;
        _isStopped = true;

        if (_audioSource != null) {
            _audioSource.Stop();
        }

        // Reset visual indicators
        if (_enableVisualFeedback) {
            foreach (var indicator in _visualIndicators) {
                if (indicator != null) {
                    indicator.Reset();
                }
            }

            foreach (var controller in timeSignatureVisualControllers) {
                if (controller != null) {
                    controller.Reset();
                }
            }
        }

        UpdateUI();
        Debug.Log($"{gameObject.name} stopped");
        OnStopped?.Invoke();
    }

    public void ResetMetronome() {
        _isPlaying = false;
        _currentBeat = 1;
        _isFirstBeat = true;
        _isStopped = true;
        _isAudioMuted = false;

        if (_audioSource != null) {
            _audioSource.Stop();
            _audioSource.mute = false;
        }

        // Reset visual indicators
        if (_enableVisualFeedback) {
            foreach (var indicator in _visualIndicators) {
                if (indicator != null) {
                    indicator.Reset();
                }
            }

            foreach (var controller in timeSignatureVisualControllers) {
                if (controller != null) {
                    controller.Reset();
                }
            }
        }

        if (stateManager != null) {
            stateManager.ResetState();
        }

        if (scheduler != null) {
            scheduler.ResetNotificationStates();
        }

        UpdateUI();
        OnReset?.Invoke();
        OnStopped?.Invoke();
        OnAudioMuteChanged?.Invoke(false);
    }

    // === SCHEDULING CONVENIENCE METHODS ===
    public void ScheduleChange(MetronomeChange_v2 change) {
        if (scheduler != null) {
            scheduler.ScheduleChange(change);
        } else {
            Debug.LogError("Cannot schedule change: MetronomeScheduler_v2 not assigned!");
        }
    }

    public void ScheduleTempoChange(int targetMeasure, float newBpm, string description = "") {
        if (scheduler != null) {
            scheduler.ScheduleTempoChange(targetMeasure, newBpm, description);
        }
    }

    public void ScheduleTimeSignatureChange(int targetMeasure, int newBeatsPerMeasure, string description = "") {
        if (scheduler != null) {
            scheduler.ScheduleTimeSignatureChange(targetMeasure, newBeatsPerMeasure, description);
        }
    }

    public void ScheduleCombinedChange(int targetMeasure, float newBpm, int newBeatsPerMeasure, string description = "") {
        if (scheduler != null) {
            scheduler.ScheduleCombinedChange(targetMeasure, newBpm, newBeatsPerMeasure, description);
        }
    }

    public void ScheduleStopEvent(int targetMeasure, string description = "") {
        if (scheduler != null) {
            scheduler.ScheduleStopEvent(targetMeasure, description);
        }
    }

    public void ScheduleMuteEvent(int targetMeasure, string description = "") {
        if (scheduler != null) {
            scheduler.ScheduleMuteEvent(targetMeasure, description);
        }
    }

    public void ScheduleUnmuteEvent(int targetMeasure, string description = "") {
        if (scheduler != null) {
            scheduler.ScheduleUnmuteEvent(targetMeasure, description);
        }
    }

    public void ScheduleVisualOffEvent(int targetMeasure, string description = "") {
        if (scheduler != null) {
            scheduler.ScheduleVisualOffEvent(targetMeasure, description);
        }
    }

    public void ScheduleVisualOnEvent(int targetMeasure, string description = "") {
        if (scheduler != null) {
            scheduler.ScheduleVisualOnEvent(targetMeasure, description);
        }
    }

    public List<MetronomeChange_v2> GetPendingChanges() {
        if (scheduler != null) {
            return scheduler.GetPendingChanges();
        }
        return new List<MetronomeChange_v2>();
    }

    public void ClearPendingChanges() {
        if (scheduler != null) {
            scheduler.ClearPendingChanges();
        }
    }

    public void SetCurrentMeasure(int measure) {
        _currentBeat = 1;
        _isFirstBeat = true;

        if (stateManager != null) {
            stateManager.SetCurrentMeasure(measure);
        }

        UpdateUI();
    }

    /// <summary>
    /// Force the beat counter to a specific beat without affecting measure or tempo.
    /// Used by CompositionCoordinator for beat-level alignment at sync points.
    /// </summary>
    public void SetCurrentBeat(int beat) {
        _currentBeat = Mathf.Clamp(beat, 1, _beatsPerMeasure);
        UpdateUI();
        Debug.Log($"{gameObject.name}: Beat forced to {_currentBeat}");
    }

    public void SyncBeatTiming(double targetBeatTime) {
        _nextBeatTime = targetBeatTime;
    }

    public void SetBpm(float bpm) {
        _bpm = bpm;
        _beatIntervalSeconds = 60.0 / bpm;
        UpdateUI();
        Debug.Log($"{gameObject.name}: BPM set to {bpm}");
    }

    // === AUDIO CONTROL ===
    public void SetAudioMute(bool muted) {
        if (_isAudioMuted == muted) return;

        _isAudioMuted = muted;

        if (_audioSource != null) {
            _audioSource.mute = muted;
        }

        Debug.Log($"{gameObject.name} audio {(muted ? "MUTED" : "UNMUTED")}");
        OnAudioMuteChanged?.Invoke(muted);
        UpdateUI();
    }

    public void MuteAudio() => SetAudioMute(true);
    public void UnmuteAudio() => SetAudioMute(false);
    public void ToggleAudioMute() => SetAudioMute(!_isAudioMuted);

    // === VISUAL CONTROL ===
    public void SetVisualFeedback(bool enable) {
        _enableVisualFeedback = enable;

        if (!enable) {
            foreach (var indicator in _visualIndicators) {
                if (indicator != null) {
                    indicator.Reset();
                }
            }

            foreach (var controller in timeSignatureVisualControllers) {
                if (controller != null) {
                    controller.Reset();
                }
            }
        }
    }

    // === PUBLIC SETTERS ===
    public void SetTempo(float newTempo) {
        Bpm = newTempo;
    }

    public void SetTimeSignature(int newBeatsPerMeasure, int newBeatUnit = 4) {
        BeatsPerMeasure = newBeatsPerMeasure;
        _beatUnit = newBeatUnit;

        // Notify time signature visual controllers
        foreach (var controller in timeSignatureVisualControllers) {
            if (controller != null) {
                controller.SetTimeSignature(newBeatsPerMeasure);
            }
        }
    }

    public void SetStartTime(double startTime) {
        _nextBeatTime = startTime;
    }

    // === DISPLAY HELPER METHODS ===
    public string GetMeasureDisplayText() {
        return stateManager?.GetMeasureDisplayText() ?? CurrentMeasure.ToString();
    }

    public Color GetMeasureDisplayColor() {
        return stateManager?.GetMeasureDisplayColor() ?? Color.white;
    }

    // === UI ===
    private void UpdateUI() {
        if (statusText != null) {
            statusText.text = $"BPM: {_bpm:F0}\nBeats: {_beatsPerMeasure}/4\nMeasure: {CurrentMeasure}\nBeat: {_currentBeat}\nStatus: {(_isPlaying ? "Playing" : (_isStopped ? "Stopped" : "Paused"))}{(_isAudioMuted ? " [MUTED]" : "")}";

            if (stateManager != null) {
                statusText.color = stateManager.GetMeasureDisplayColor();
            }
        }
    }

    private void OnBpmInputChanged(string value) {
        if (float.TryParse(value, out float newBpm)) {
            Bpm = Mathf.Clamp(newBpm, 20f, 400f);
        } else {
            bpmInput.text = _bpm.ToString("F2");
        }
    }

    private void OnBeatsInputChanged(string value) {
        if (int.TryParse(value, out int newBeats)) {
            BeatsPerMeasure = Mathf.Clamp(newBeats, 1, 16);
        } else {
            beatsInput.text = _beatsPerMeasure.ToString();
        }
    }
}