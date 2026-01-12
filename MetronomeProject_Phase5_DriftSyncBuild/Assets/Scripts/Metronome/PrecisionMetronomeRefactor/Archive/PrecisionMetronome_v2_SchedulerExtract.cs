using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ChangeComposer.Data;

/// <summary>
/// Enhanced PrecisionMetronome v2 - Scheduler Extraction Phase
/// Version: 2025-08-19 v2 (Scheduler Extract)
/// 
/// CHANGES FROM ORIGINAL:
/// - Added MetronomeScheduler_v1 component reference
/// - Delegated all scheduling methods to scheduler
/// - Removed internal CheckForPendingChanges logic
/// - Added event listeners for scheduler communication
/// - Maintained 100% backward compatibility for v8
/// </summary>
public class PrecisionMetronome_v2_SchedulerExtract : MonoBehaviour {

    // === NEW: SCHEDULER COMPONENT REFERENCE ===
    [Header("Refactoring Components")]
    [SerializeField] private MetronomeScheduler_v1 scheduler;

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

    // Pre-roll settings
    [Header("Pre-roll Settings")]
    [SerializeField] private bool _allowNegativeMeasures = true;
    [SerializeField] private Color _preRollTextColor = new Color(0.8f, 0.5f, 0.5f);
    [SerializeField] private Color _normalTextColor = Color.white;

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

    // Change system - REMOVED: pendingChanges (now handled by scheduler)
    [Header("Change System")]
    [SerializeField] private bool debugChangeSystem = true;

    // Enhanced state tracking
    private bool _isAudioMuted = false;
    private bool _isStopped = false;

    // Precision timing variables
    private double _nextBeatTime;
    private double _beatIntervalSeconds;
    private int _currentBeat = 1;
    private bool _isPlaying = false;
    private bool _isFirstBeat = true;
    private int _currentMeasure = 1;
    private int _firstMeasure = 1;

    // Properties that MetronomeWidgetDisplay expects
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
    public bool IsPlaying => _isPlaying;
    public int CurrentMeasure => _currentMeasure;
    public bool IsAudioMuted => _isAudioMuted;
    public bool IsStopped => _isStopped;

    void Start() {
        // === NEW: VALIDATE SCHEDULER REFERENCE ===
        ValidateSchedulerReference();

        // === NEW: SETUP SCHEDULER EVENT LISTENERS ===
        SetupSchedulerEventListeners();

        // Initialize audio
        if (_audioSource == null) {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Set beat interval
        _beatIntervalSeconds = 60.0 / _bpm;

        // FIXED: Initialize to proper stopped state (not paused)
        _isPlaying = false;
        _isStopped = true;           // ← This is the key fix!
        _isAudioMuted = false;
        _currentBeat = 1;
        _currentMeasure = 1;
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

        Debug.Log($"Enhanced Precision Metronome v2 {gameObject.name} initialized in STOPPED state with scheduler");
    }

    // === NEW: SCHEDULER VALIDATION ===
    private void ValidateSchedulerReference() {
        if (scheduler == null) {
            Debug.LogError($"{gameObject.name}: MetronomeScheduler_v1 not assigned! Scheduling will not work.");
        }
    }

    // === NEW: SCHEDULER EVENT LISTENERS ===
    private void SetupSchedulerEventListeners() {
        if (scheduler == null) return;

        // Listen for changes ready to apply
        scheduler.OnChangeReadyToApply += ApplyChangeFromScheduler;

        // Listen for notifications
        scheduler.OnChangeNotification += RelayChangeNotification;

        // Listen for scheduling confirmations
        scheduler.OnChangeScheduled += RelayChangeScheduled;

        // Listen for processed changes
        scheduler.OnChangeProcessed += RelayChangeProcessed;
    }

    // === NEW: EVENT HANDLERS FOR SCHEDULER ===
    private void ApplyChangeFromScheduler(MetronomeChange change) {
        // Apply the change using existing logic
        ApplyChange(change);
    }

    private void RelayChangeNotification(MetronomeChange change, string message) {
        // Relay to existing event system
        OnChangeNotification?.Invoke(change, message);
    }

    private void RelayChangeScheduled(MetronomeChange change) {
        // Relay to existing event system
        OnChangeScheduled?.Invoke(change);
    }

    private void RelayChangeProcessed(MetronomeChange change) {
        // Relay to existing event system
        OnChangeApplied?.Invoke(change);
    }

    void Update() {
        if (!_isPlaying) return;

        double currentDspTime = AudioSettings.dspTime;

        if (currentDspTime >= _nextBeatTime) {
            // Handle measure changes
            if (_currentBeat == 1 && !_isFirstBeat) {
                _currentMeasure++;

                if (_currentMeasure == 0 && _allowNegativeMeasures) {
                    _currentMeasure = 1;
                }

                // === CHANGED: NOTIFY SCHEDULER OF MEASURE CHANGE ===
                if (scheduler != null) {
                    scheduler.HandleMeasureChanged(_currentMeasure);
                }

                OnMeasureChanged?.Invoke();

                if (_currentMeasure == _firstMeasure && _currentMeasure > 0 &&
                    (_currentMeasure - 1) < _firstMeasure) {
                    OnPreRollCompleted?.Invoke();
                }

                if (debugChangeSystem) {
                    Debug.Log($"Measure {_currentMeasure} started");
                }
            } else if (_currentBeat == _beatsPerMeasure) {
                OnLastBeatOfMeasure?.Invoke();
            }

            OnBeatTriggered?.Invoke(_currentBeat);
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

    // === REMOVED: CheckForPendingChanges - now handled by scheduler ===

    /// <summary>
    /// Apply a change - UNCHANGED from original
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
                Debug.Log($"Applied time signature change: {change.newBeatsPerMeasure} beats per measure");
                break;

            case MetronomeChange.ChangeType.Both:
                Bpm = change.newBpm;
                BeatsPerMeasure = change.newBeatsPerMeasure;
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

        // Note: OnChangeApplied is now fired by scheduler via RelayChangeProcessed
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
    }

    private void IncrementBeat() {
        _currentBeat++;
        if (_currentBeat > _beatsPerMeasure) {
            _currentBeat = 1;
        }
    }

    // Audio control methods
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
        _isStopped = false;  // ← Clear the stopped flag when starting

        if (_nextBeatTime <= AudioSettings.dspTime) {
            _nextBeatTime = AudioSettings.dspTime + 0.1;
        }

        ScheduleNextBeat();
        UpdateUI();
        OnStarted?.Invoke();
    }

    public void StartWithPreRoll(int preRollMeasures) {
        if (preRollMeasures <= 0 || !_allowNegativeMeasures) {
            StartMetronome();
            return;
        }

        int startMeasure = 1 - preRollMeasures;
        StartAtMeasure(startMeasure);
    }

    public void StartAtMeasure(int measure) {
        if (!_allowNegativeMeasures && measure < 1) {
            measure = 1;
        }

        _isPlaying = false;
        _currentMeasure = measure;
        _currentBeat = 1;
        _isFirstBeat = true;
        _isPlaying = true;

        // === CHANGED: NOTIFY SCHEDULER OF MEASURE CHANGE ===
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
        // _isStopped remains false - this is paused, not stopped

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
        _currentMeasure = 1;
        _isFirstBeat = true;
        _isStopped = true;        // ← Reset returns to stopped state
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

        // === CHANGED: RESET SCHEDULER STATE ===
        if (scheduler != null) {
            scheduler.ResetNotificationStates();
            scheduler.SetCurrentMeasure(1);
        }

        UpdateUI();
        OnReset?.Invoke();
        OnStopped?.Invoke();      // ← Fire stopped event after reset
        OnAudioMuteChanged?.Invoke(false);
    }

    // === MODIFIED: SCHEDULING METHODS (Now delegate to scheduler) ===

    /// <summary>
    /// Schedule a change - DELEGATES TO SCHEDULER
    /// </summary>
    public void ScheduleChange(MetronomeChange change) {
        if (scheduler != null) {
            scheduler.ScheduleChange(change);
        } else {
            Debug.LogError("Cannot schedule change: MetronomeScheduler not assigned!");
        }
    }

    /// <summary>
    /// Clear pending changes - DELEGATES TO SCHEDULER
    /// </summary>
    public void ClearPendingChanges() {
        if (scheduler != null) {
            scheduler.ClearPendingChanges();
        }
    }

    /// <summary>
    /// Get pending changes - DELEGATES TO SCHEDULER
    /// </summary>
    public List<MetronomeChange> GetPendingChanges() {
        if (scheduler != null) {
            return scheduler.GetPendingChanges();
        }
        return new List<MetronomeChange>();
    }

    // Set correct start measure in metroome after generating index and pressing start measure button

    public void SetCurrentMeasure(int measure) {
        _currentMeasure = measure;
        _currentBeat = 1;
        _isFirstBeat = true;

        // === CHANGED: NOTIFY SCHEDULER OF MEASURE CHANGE ===
        if (scheduler != null) {
            scheduler.SetCurrentMeasure(measure);
        }

        // Update UI if needed
        UpdateUI();
    }

    // Convenience methods - DELEGATE TO SCHEDULER
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

    public string GetMeasureDisplayText() {
        return _currentMeasure.ToString();
    }

    public Color GetMeasureDisplayColor() {
        return (_currentMeasure < _firstMeasure) ? _preRollTextColor : _normalTextColor;
    }

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
            statusText.text = $"BPM: {_bpm:F0}\nBeats: {_beatsPerMeasure}/4\nMeasure: {_currentMeasure}\nBeat: {_currentBeat}\nStatus: {(_isPlaying ? "Playing" : (_isStopped ? "Stopped" : "Paused"))}{(_isAudioMuted ? " [MUTED]" : "")}";
            statusText.color = (_currentMeasure < _firstMeasure) ? _preRollTextColor : _normalTextColor;
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