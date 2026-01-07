using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ChangeComposer.Data;

/// <summary>
/// Enhanced PrecisionMetronome with full audio/visual event support
/// Compatible with MetronomeWidgetDisplay and all ChangeComposer features
/// Handles stop, mute, unmute, visual events properly
/// </summary>
public class PrecisionMetronome : MonoBehaviour {
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

    // Change system
    [Header("Change System")]
    [SerializeField] private List<MetronomeChange> pendingChanges = new List<MetronomeChange>();
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

        Debug.Log($"Enhanced Precision Metronome {gameObject.name} initialized in STOPPED state");
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

                CheckForPendingChanges(_currentMeasure);
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

    private void CheckForPendingChanges(int currentMeasure) {
        bool foundChange = false;

        // Check for notifications
        foreach (var change in pendingChanges) {
            if (change.ShouldNotifyAtMeasure(currentMeasure)) {
                TriggerChangeNotification(change, currentMeasure);
                change.MarkNotificationSent(currentMeasure);
            }
        }

        // Apply changes
        foreach (var change in pendingChanges) {
            if (!change.isProcessed && change.targetMeasure == currentMeasure) {
                ApplyChange(change);
                change.isProcessed = true;
                foundChange = true;
            }
        }

        if (foundChange) {
            pendingChanges.RemoveAll(c => c.isProcessed);
        }
    }

    private void TriggerChangeNotification(MetronomeChange change, int currentMeasure) {
        string message = change.GetNotificationMessage(currentMeasure);

        if (change.isUrgent) {
            message = "⚠️ URGENT: " + message;
        }

        Debug.Log($"[{gameObject.name}] Notification: {message}");
        OnChangeNotification?.Invoke(change, message);
    }

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

        OnChangeApplied?.Invoke(change);
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

        if (pendingChanges.Count > 0) {
            if (debugChangeSystem) {
                Debug.Log("Resetting all pending changes");
            }

            foreach (var change in pendingChanges) {
                change.isProcessed = false;
                change.ResetNotificationState();
            }

            pendingChanges.Sort((a, b) => a.targetMeasure.CompareTo(b.targetMeasure));
        }

        UpdateUI();
        OnReset?.Invoke();
        OnStopped?.Invoke();      // ← Fire stopped event after reset
        OnAudioMuteChanged?.Invoke(false);
    }

    // Change scheduling
    public void ScheduleChange(MetronomeChange change) {
        if (change.targetMeasure < _currentMeasure) {
            Debug.LogWarning($"Cannot schedule change for measure {change.targetMeasure} - we're already at measure {_currentMeasure}");
            return;
        }

        pendingChanges.Add(change);
        pendingChanges.Sort((a, b) => a.targetMeasure.CompareTo(b.targetMeasure));

        if (debugChangeSystem) {
            Debug.Log($"Scheduled change: {change}");
        }

        OnChangeScheduled?.Invoke(change);
    }

    // Set correct start measure in metroome after generating index and pressing start measure button

    public void SetCurrentMeasure(int measure) {
        _currentMeasure = measure;
        _currentBeat = 1;
        _isFirstBeat = true;

        // Update UI if needed
        UpdateUI();
    }

    // Convenience methods
    public void ScheduleTempoChange(int targetMeasure, float newBpm) {
        ScheduleChange(new MetronomeChange(targetMeasure, newBpm));
    }

    public void ScheduleTimeSignatureChange(int targetMeasure, int newBeatsPerMeasure) {
        ScheduleChange(new MetronomeChange(targetMeasure, newBeatsPerMeasure));
    }

    public void ScheduleCombinedChange(int targetMeasure, float newBpm, int newBeatsPerMeasure) {
        ScheduleChange(new MetronomeChange(targetMeasure, newBpm, newBeatsPerMeasure));
    }

    public void ScheduleStopEvent(int targetMeasure, string description = "") {
        ScheduleChange(MetronomeChange.CreateStopEvent(targetMeasure, description));
    }

    public void ScheduleMuteEvent(int targetMeasure, string description = "") {
        ScheduleChange(MetronomeChange.CreateMute(targetMeasure, description));
    }

    public void ScheduleUnmuteEvent(int targetMeasure, string description = "") {
        ScheduleChange(MetronomeChange.CreateUnmute(targetMeasure, description));
    }

    public void ScheduleVisualOffEvent(int targetMeasure, string description = "") {
        ScheduleChange(MetronomeChange.CreateVisualOff(targetMeasure, description));
    }

    public void ScheduleVisualOnEvent(int targetMeasure, string description = "") {
        ScheduleChange(MetronomeChange.CreateVisualOn(targetMeasure, description));
    }

    public List<MetronomeChange> GetPendingChanges() {
        return new List<MetronomeChange>(pendingChanges);
    }

    public void ClearPendingChanges() {
        pendingChanges.Clear();

        if (debugChangeSystem) {
            Debug.Log("Cleared all pending changes");
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