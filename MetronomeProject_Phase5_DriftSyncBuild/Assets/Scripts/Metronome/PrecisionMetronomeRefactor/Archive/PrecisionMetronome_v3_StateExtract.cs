using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ChangeComposer.Data;

/// <summary>
/// Enhanced PrecisionMetronome v3 - State Management Extraction Phase
/// Version: 2025-08-23 v3 (State Extract)
/// 
/// CHANGES FROM v2:
/// - Added MetronomeStateManager_v1 component reference
/// - Delegated all state management to state manager
/// - Removed internal state tracking fields
/// - Added event listeners for state manager communication
/// - Maintained 100% backward compatibility for v8 coordinator
/// </summary>
public class PrecisionMetronome_v3_StateExtract : MonoBehaviour {

    // === REFACTORING COMPONENTS ===
    [Header("Refactoring Components")]
    [SerializeField] private MetronomeScheduler_v1 scheduler;
    [SerializeField] private MetronomeStateManager_v1 stateManager; // NEW in v3

    // All events that MetronomeWidgetDisplay expects
    public event Action OnStarted;
    public event Action OnPaused;
    public event Action OnReset;
    public event Action OnStopped;
    public event Action<bool> OnAudioMuteChanged;

    // Timing events
    public event Action<int> OnBeatTriggered;
    public event Action OnMeasureChanged;
    public event Action<MetronomeChange, string> OnChangeNotification;
    public event Action OnLastBeatOfMeasure;

    // Change system events
    public event Action OnPreRollCompleted;
    public event Action<MetronomeChange.ChangeType, float, int> OnMetronomeSettingsChanged;
    public event Action<MetronomeChange> OnChangeScheduled;
    public event Action<MetronomeChange> OnChangeApplied;

    // Audio settings
    [Header("Audio")]
    [SerializeField] private AudioClip strongBeatClip;
    [SerializeField] private AudioClip regularBeatClip;
    [SerializeField] private AudioSource _audioSource;

    // Metronome settings
    [Header("Metronome Settings")]
    [SerializeField] private float _bpm = 120f;
    [SerializeField] private int _beatsPerMeasure = 4;
    [SerializeField] private int _beatUnit = 4;

    // UI elements
    [Header("UI References")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private InputField bpmInput;
    [SerializeField] private InputField beatsInput;
    [SerializeField] private Text statusText;

    // Visual feedback
    [Header("Visual Feedback")]
    [SerializeField] private bool _enableVisualFeedback = true;
    [SerializeField] private List<MetronomeVisualIndicator> _visualIndicators = new List<MetronomeVisualIndicator>();
    [SerializeField] private TimeSignatureVisualController timeSignatureVisualController;


    // Change system
    [Header("Change System")]
    [SerializeField] private bool debugChangeSystem = true;

    // Enhanced state tracking
    private bool _isAudioMuted = false;
    private bool _isStopped = false;

    // Precision timing variables (timing-related, kept in metronome)
    private double _nextBeatTime;
    private double _beatIntervalSeconds;
    private int _currentBeat = 1; // Kept for timing precision
    private bool _isPlaying = false;
    private bool _isFirstBeat = true;

    // REMOVED STATE FIELDS (now handled by StateManager):
    // private int _currentMeasure = 1;  ← REMOVED
    // private int _firstMeasure = 1;    ← REMOVED
    // private bool _allowNegativeMeasures = true; ← REMOVED
    // private Color _preRollTextColor = new Color(0.8f, 0.5f, 0.5f); ← REMOVED
    // private Color _normalTextColor = Color.white; ← REMOVED

    // Properties that delegate to StateManager
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
    public int CurrentBeat => _currentBeat; // Kept for timing
    public bool IsPlaying => _isPlaying;
    public int CurrentMeasure => stateManager?.CurrentMeasure ?? 1; // CHANGED: Delegate to StateManager
    public bool IsAudioMuted => _isAudioMuted;
    public bool IsStopped => _isStopped;

    void Start() {
        // === VALIDATION ===
        ValidateSchedulerReference();
        ValidateStateManagerReference(); // NEW

        // === SETUP EVENT LISTENERS ===
        SetupSchedulerEventListeners();
        SetupStateManagerEventListeners(); // NEW

        // Initialize audio
        if (_audioSource == null) {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Set beat interval
        _beatIntervalSeconds = 60.0 / _bpm;

        // Initialize to proper stopped state
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

        // Initial UI update
        UpdateUI();

        // Fire the OnStopped event to ensure widgets get the correct initial state
        OnStopped?.Invoke();

        Debug.Log($"Enhanced Precision Metronome v3 {gameObject.name} initialized with scheduler and state manager");
    }

    // === SCHEDULER VALIDATION (unchanged from v2) ===
    private void ValidateSchedulerReference() {
        if (scheduler == null) {
            Debug.LogError($"{gameObject.name}: MetronomeScheduler_v1 not assigned! Scheduling will not work.");
        }
    }

    // === NEW: STATE MANAGER VALIDATION ===
    private void ValidateStateManagerReference() {
        if (stateManager == null) {
            Debug.LogError($"{gameObject.name}: MetronomeStateManager_v1 not assigned! State management will not work.");
        }
    }

    // === SCHEDULER EVENT LISTENERS (unchanged from v2) ===
    private void SetupSchedulerEventListeners() {
        if (scheduler == null) return;

        scheduler.OnChangeReadyToApply += ApplyChangeFromScheduler;
        scheduler.OnChangeNotification += RelayChangeNotification;
        scheduler.OnChangeScheduled += RelayChangeScheduled;
        scheduler.OnChangeProcessed += RelayChangeProcessed;
    }

    // === NEW: STATE MANAGER EVENT LISTENERS ===
    private void SetupStateManagerEventListeners() {
        if (stateManager == null) return;

        stateManager.OnMeasureStateChanged += HandleStateManagerMeasureChanged;
        stateManager.OnBeatStateChanged += HandleStateManagerBeatChanged;
        stateManager.OnCompositionStarted += RelayCompositionStarted;
        stateManager.OnDisplayStateChanged += UpdateUI;
    }

    // === NEW: EVENT HANDLERS FOR STATE MANAGER ===
    private void HandleStateManagerMeasureChanged(int newMeasure) {
        // StateManager is handling the measure tracking
        // This can be used for additional timing-specific logic if needed
        if (debugChangeSystem) {
            Debug.Log($"StateManager measure changed to: {newMeasure}");
        }
    }

    private void HandleStateManagerBeatChanged(int newBeat) {
        // StateManager is tracking beat changes
        // Could be used for additional beat-specific logic if needed
    }

    private void RelayCompositionStarted() {
        OnPreRollCompleted?.Invoke(); // For backward compatibility
        if (debugChangeSystem) {
            Debug.Log("Composition started (from StateManager)");
        }
    }

    // === SCHEDULER EVENT HANDLERS (unchanged from v2) ===
    private void ApplyChangeFromScheduler(MetronomeChange change) {
        ApplyChange(change);
    }

    private void RelayChangeNotification(MetronomeChange change, string message) {
        OnChangeNotification?.Invoke(change, message);
    }

    private void RelayChangeScheduled(MetronomeChange change) {
        OnChangeScheduled?.Invoke(change);
    }

    private void RelayChangeProcessed(MetronomeChange change) {
        OnChangeApplied?.Invoke(change);
    }

    void Update() {
        if (!_isPlaying) return;

        double currentDspTime = AudioSettings.dspTime;

        if (currentDspTime >= _nextBeatTime) {
            // Handle measure changes - MODIFIED for StateManager
            if (_currentBeat == 1 && !_isFirstBeat) {
                // === CHANGED: NOTIFY STATE MANAGER OF MEASURE ADVANCEMENT ===
                if (stateManager != null) {
                    stateManager.HandleMeasureAdvancement();
                }

                // === NOTIFY SCHEDULER OF MEASURE CHANGE ===
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

            OnBeatTriggered?.Invoke(_currentBeat);

            // === CHANGED: NOTIFY STATE MANAGER OF BEAT CHANGE ===
            if (stateManager != null) {
                stateManager.HandleBeatAdvancement(_currentBeat);
            }

            _isFirstBeat = false;

            ScheduleNextBeat();

            if (_enableVisualFeedback) {
                TriggerVisualFeedback();
            }

            UpdateUI();
            IncrementBeat();
            _nextBeatTime += _beatIntervalSeconds;
        }
    }

    /// <summary>
    /// Apply a change - UNCHANGED from v2
    /// </summary>
    private void ApplyChange(MetronomeChange change) {
        if (debugChangeSystem) {
            Debug.Log($"Applying change: {change}");
        }

        switch (change.type) {
            case MetronomeChange.ChangeType.Tempo:
                Bpm = change.newBpm;
                Debug.Log($"Applied tempo change: {change.newBpm} BPM");
                break;

            case MetronomeChange.ChangeType.TimeSignature:
                BeatsPerMeasure = change.newBeatsPerMeasure;
                if (timeSignatureVisualController != null) {
                    timeSignatureVisualController.SetTimeSignature(change.newBeatsPerMeasure);
                }
                Debug.Log($"Applied time signature change: {change.newBeatsPerMeasure} beats per measure");
                break;

            case MetronomeChange.ChangeType.Both:
                Bpm = change.newBpm;
                BeatsPerMeasure = change.newBeatsPerMeasure;
                if (timeSignatureVisualController != null) {
                    timeSignatureVisualController.SetTimeSignature(change.newBeatsPerMeasure);
                }
                Debug.Log($"Applied combined change: {change.newBpm} BPM, {change.newBeatsPerMeasure} beats per measure");
                break;

            case MetronomeChange.ChangeType.Stop:
                StopMetronome();
                Debug.Log($"Applied stop event: {change.description}");
                break;

            case MetronomeChange.ChangeType.Mute:
                SetAudioMute(true);
                Debug.Log($"Applied mute event: {change.description}");
                break;

            case MetronomeChange.ChangeType.Unmute:
                SetAudioMute(false);
                Debug.Log($"Applied unmute event: {change.description}");
                break;

            case MetronomeChange.ChangeType.VisualOff:
                SetVisualFeedback(false);
                Debug.Log($"Applied visual off event: {change.description}");
                break;

            case MetronomeChange.ChangeType.VisualOn:
                SetVisualFeedback(true);
                Debug.Log($"Applied visual on event: {change.description}");
                break;

            case MetronomeChange.ChangeType.Combined:
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

        // Fire events
        if (change.type == MetronomeChange.ChangeType.Tempo ||
            change.type == MetronomeChange.ChangeType.TimeSignature ||
            change.type == MetronomeChange.ChangeType.Both) {
            OnMetronomeSettingsChanged?.Invoke(change.type, change.newBpm, change.newBeatsPerMeasure);
        }
    }

    private void ScheduleNextBeat() {
        if (_audioSource == null || _isAudioMuted) return;

        bool isStrongBeat = _currentBeat == 1;
        AudioClip clipToPlay = isStrongBeat ? strongBeatClip : regularBeatClip;

        if (clipToPlay != null) {
            _audioSource.clip = clipToPlay;
            _audioSource.PlayScheduled(_nextBeatTime);
        }
    }

    private void TriggerVisualFeedback() {
        if (!_enableVisualFeedback) return;

        bool isStrongBeat = _currentBeat == 1;

        foreach (var indicator in _visualIndicators) {
            if (indicator != null) {
                indicator.Flash(isStrongBeat);
            }
        }

        if (timeSignatureVisualController != null) {
            timeSignatureVisualController.OnBeat(_currentBeat);
        }
    }

    private void IncrementBeat() {
        _currentBeat++;
        if (_currentBeat > _beatsPerMeasure) {
            _currentBeat = 1;
        }
    }

    // Audio control methods (unchanged from v2)
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

    // Control methods
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

        // === CHANGED: DELEGATE TO STATE MANAGER ===
        if (stateManager != null) {
            stateManager.SetCurrentMeasure(measure);
        }

        _isPlaying = true;

        // === NOTIFY SCHEDULER OF MEASURE CHANGE ===
        if (scheduler != null) {
            scheduler.SetCurrentMeasure(measure);
        }

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

        if (_enableVisualFeedback) {
            foreach (var indicator in _visualIndicators) {
                if (indicator != null) {
                    indicator.Reset();
                }
            }
        }

        UpdateUI();
        Debug.Log($"{gameObject.name} stopped (end of piece)");
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

        if (_enableVisualFeedback) {
            foreach (var indicator in _visualIndicators) {
                if (indicator != null) {
                    indicator.Reset();
                }
            }
        }

        // === CHANGED: RESET STATE MANAGER ===
        if (stateManager != null) {
            stateManager.ResetState();
        }

        // === RESET SCHEDULER STATE ===
        if (scheduler != null) {
            scheduler.ResetNotificationStates();
            scheduler.SetCurrentMeasure(CurrentMeasure);
        }

        UpdateUI();
        OnReset?.Invoke();
        OnStopped?.Invoke();
        OnAudioMuteChanged?.Invoke(false);
    }

    // === SCHEDULING METHODS (unchanged from v2 - delegate to scheduler) ===
    public void ScheduleChange(MetronomeChange change) {
        if (scheduler != null) {
            scheduler.ScheduleChange(change);
        } else {
            Debug.LogError("Cannot schedule change: MetronomeScheduler not assigned!");
        }
    }

    public void ClearPendingChanges() {
        if (scheduler != null) {
            scheduler.ClearPendingChanges();
        }
    }

    public List<MetronomeChange> GetPendingChanges() {
        if (scheduler != null) {
            return scheduler.GetPendingChanges();
        }
        return new List<MetronomeChange>();
    }

    public void SetCurrentMeasure(int measure) {
        _currentBeat = 1;
        _isFirstBeat = true;

        // === CHANGED: DELEGATE TO STATE MANAGER ===
        if (stateManager != null) {
            stateManager.SetCurrentMeasure(measure);
        }

        // === NOTIFY SCHEDULER OF MEASURE CHANGE ===
        if (scheduler != null) {
            scheduler.SetCurrentMeasure(CurrentMeasure);
        }

        UpdateUI();
    }

    // Convenience scheduling methods (unchanged from v2)
    public void ScheduleTempoChange(int targetMeasure, float newBpm) {
        if (scheduler != null) {
            scheduler.ScheduleTempoChange(targetMeasure, newBpm);
        }
    }

    public void ScheduleTimeSignatureChange(int targetMeasure, int newBeatsPerMeasure) {
        if (scheduler != null) {
            scheduler.ScheduleTimeSignatureChange(targetMeasure, newBeatsPerMeasure);
        }
    }

    public void ScheduleCombinedChange(int targetMeasure, float newBpm, int newBeatsPerMeasure) {
        if (scheduler != null) {
            scheduler.ScheduleCombinedChange(targetMeasure, newBpm, newBeatsPerMeasure);
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

    public void SetStartTime(double startTime) {
        _nextBeatTime = startTime;
    }

    public void SetTempo(float newTempo) {
        Bpm = newTempo;
    }

    public void SetTimeSignature(int newBeatsPerMeasure, int newBeatUnit = 4) {
        BeatsPerMeasure = newBeatsPerMeasure;
        _beatUnit = newBeatUnit;
    }

    public void SetVisualFeedback(bool enable) {
        _enableVisualFeedback = enable;

        if (!enable) {
            foreach (var indicator in _visualIndicators) {
                if (indicator != null) {
                    indicator.Reset();
                }
            }
        }
    }

    // === MODIFIED: Display methods now delegate to StateManager ===
    public string GetMeasureDisplayText() {
        return stateManager?.GetMeasureDisplayText() ?? CurrentMeasure.ToString();
    }

    public Color GetMeasureDisplayColor() {
        return stateManager?.GetMeasureDisplayColor() ?? Color.white;
    }

    // Visual indicator methods (unchanged from v2)
    public void AddVisualIndicator(MetronomeVisualIndicator indicator) {
        if (indicator != null && !_visualIndicators.Contains(indicator)) {
            _visualIndicators.Add(indicator);
            Debug.Log($"Added visual indicator to {gameObject.name}");
        }
    }

    public void RemoveVisualIndicator(MetronomeVisualIndicator indicator) {
        if (indicator != null && _visualIndicators.Contains(indicator)) {
            _visualIndicators.Remove(indicator);
            Debug.Log($"Removed visual indicator from {gameObject.name}");
        }
    }

    public MetronomeVisualIndicator VisualIndicator {
        get { return _visualIndicators.Count > 0 ? _visualIndicators[0] : null; }
        set {
            _visualIndicators.Clear();
            if (value != null) {
                _visualIndicators.Add(value);
            }
        }
    }

    private void UpdateUI() {
        if (statusText != null) {
            statusText.text = $"BPM: {_bpm:F0}\nBeats: {_beatsPerMeasure}/4\nMeasure: {CurrentMeasure}\nBeat: {_currentBeat}\nStatus: {(_isPlaying ? "Playing" : (_isStopped ? "Stopped" : "Paused"))}{(_isAudioMuted ? " [MUTED]" : "")}";

            // Use StateManager color if available
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