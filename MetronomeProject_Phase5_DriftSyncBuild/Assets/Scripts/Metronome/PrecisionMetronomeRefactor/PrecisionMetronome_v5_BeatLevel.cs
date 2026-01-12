using ChangeComposer.Data;
using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// PrecisionMetronome v5 - Beat-Level Scheduling Testing Version
/// Version: 2025-01-11 v5 (Phase 1 Testing)
/// 
/// PURPOSE: Clean implementation for testing Phase 1 beat-level tempo ramps
/// - Uses MetronomeScheduler_v2 exclusively (no v1 compatibility)
/// - Uses MetronomeChange_v2 with targetBeat field
/// - Beat-level scheduling built in from the start
/// - No backward compatibility gymnastics
/// 
/// BASED ON: PrecisionMetronome_v4_MultipleDisplays
/// DIFFERENCES FROM v4:
/// - Uses MetronomeScheduler_v2 (not v1)
/// - Uses MetronomeChange_v2 (not MetronomeChange)
/// - Calls scheduler.HandleBeatChanged() on every beat
/// - Simplified for testing (removed some v4 complexity)
/// 
/// USE FOR: Phase 1 beat-level tempo ramps testing ONLY
/// PRODUCTION: Keep using v4 until Phase 1 is proven
/// </summary>
public class PrecisionMetronome_v5_BeatLevel : MonoBehaviour {

    // === PHASE 1 COMPONENTS ===
    [Header("Phase 1 Components")]
    [SerializeField] private MetronomeScheduler_v2 scheduler;  // ✨ v2 only!
    [SerializeField] private MetronomeStateManager_v1 stateManager;

    // === EVENTS ===
    public event Action OnStarted;
    public event Action OnPaused;
    public event Action OnReset;
    public event Action OnStopped;
    public event Action<bool> OnAudioMuteChanged;

    // Timing events
    public event Action<int> OnBeatTriggered;
    public event Action OnMeasureChanged;
    public event Action<MetronomeChange_v2, string> OnChangeNotification;  // ✨ v2 type!
    public event Action OnLastBeatOfMeasure;

    // Change system events
    public event Action OnPreRollCompleted;
    public event Action<MetronomeChange_v2.ChangeType, float, int> OnMetronomeSettingsChanged;  // ✨ v2 type!
    public event Action<MetronomeChange_v2> OnChangeScheduled;  // ✨ v2 type!
    public event Action<MetronomeChange_v2> OnChangeApplied;  // ✨ v2 type!

    // === AUDIO ===
    [Header("Audio")]
    [SerializeField] private AudioClip strongBeatClip;
    [SerializeField] private AudioClip regularBeatClip;
    [SerializeField] private AudioSource _audioSource;

    // === METRONOME SETTINGS ===
    [Header("Metronome Settings")]
    [SerializeField] private float _bpm = 120f;
    [SerializeField] private int _beatsPerMeasure = 4;
    [SerializeField] private int _beatUnit = 4;

    // === UI (Simplified - optional for testing) ===
    [Header("UI References (Optional)")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private InputField bpmInput;
    [SerializeField] private InputField beatsInput;
    [SerializeField] private Text statusText;

    // === VISUAL FEEDBACK (Simplified) ===
    [Header("Visual Feedback (Optional)")]
    [SerializeField] private bool _enableVisualFeedback = true;
    [SerializeField] private List<MetronomeVisualIndicator> _visualIndicators = new List<MetronomeVisualIndicator>();

    // === DEBUG ===
    [Header("Debug")]
    [SerializeField] private bool debugChangeSystem = true;

    // === INTERNAL STATE ===
    private bool _isAudioMuted = false;
    private bool _isStopped = false;

    // Precision timing variables
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
    public bool IsPlaying => _isPlaying;
    public int CurrentMeasure => stateManager?.CurrentMeasure ?? 1;
    public bool IsAudioMuted => _isAudioMuted;
    public bool IsStopped => _isStopped;

    // ════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ════════════════════════════════════════════════════════════

    void Start() {
        ValidateComponents();
        SetupEventListeners();
        InitializeAudio();
        InitializeState();
        SetupUI();

        Debug.Log($"[v5] PrecisionMetronome v5 initialized - Phase 1 beat-level testing mode");
    }

    private void ValidateComponents() {
        if (scheduler == null) {
            Debug.LogError($"[v5] {gameObject.name}: MetronomeScheduler_v2 not assigned!");
        }
        if (stateManager == null) {
            Debug.LogError($"[v5] {gameObject.name}: MetronomeStateManager_v1 not assigned!");
        }
    }

    private void SetupEventListeners() {
        // Scheduler events
        if (scheduler != null) {
            scheduler.OnChangeReadyToApply += ApplyChangeFromScheduler;
            scheduler.OnChangeNotification += RelayChangeNotification;
            scheduler.OnChangeScheduled += RelayChangeScheduled;
            scheduler.OnChangeProcessed += RelayChangeProcessed;
        }

        // State manager events
        if (stateManager != null) {
            stateManager.OnMeasureStateChanged += HandleStateManagerMeasureChanged;
            stateManager.OnBeatStateChanged += HandleStateManagerBeatChanged;
            stateManager.OnCompositionStarted += RelayCompositionStarted;
            stateManager.OnDisplayStateChanged += UpdateUI;
        }
    }

    private void InitializeAudio() {
        if (_audioSource == null) {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
    }

    private void InitializeState() {
        _beatIntervalSeconds = 60.0 / _bpm;
        _isPlaying = false;
        _isStopped = true;
        _isAudioMuted = false;
        _currentBeat = 1;
        _isFirstBeat = true;
    }

    private void SetupUI() {
        if (playButton != null) playButton.onClick.AddListener(StartMetronome);
        if (pauseButton != null) pauseButton.onClick.AddListener(PauseMetronome);
        if (resetButton != null) resetButton.onClick.AddListener(ResetMetronome);

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
    }

    // ════════════════════════════════════════════════════════════
    // CORE TIMING - THE HEART OF PHASE 1!
    // ════════════════════════════════════════════════════════════

    void Update() {
        if (!_isPlaying) return;

        double currentDspTime = AudioSettings.dspTime;

        if (currentDspTime >= _nextBeatTime) {
            // Handle measure changes
            if (_currentBeat == 1 && !_isFirstBeat) {
                // Notify state manager
                if (stateManager != null) {
                    stateManager.HandleMeasureAdvancement();
                }

                // ✨ PHASE 1: Notify scheduler of measure change
                if (scheduler != null) {
                    scheduler.HandleMeasureChanged(CurrentMeasure);
                }

                OnMeasureChanged?.Invoke();

                if (debugChangeSystem) {
                    Debug.Log($"[v5] Measure {CurrentMeasure} started");
                }
            } else if (_currentBeat == _beatsPerMeasure) {
                OnLastBeatOfMeasure?.Invoke();
            }

            // Fire beat event
            OnBeatTriggered?.Invoke(_currentBeat);

            // ✨✨✨ PHASE 1 KEY FEATURE: BEAT-LEVEL SCHEDULING ✨✨✨
            // This is what makes smooth tempo ramps possible!
            if (scheduler != null) {
                scheduler.HandleBeatChanged(CurrentMeasure, _currentBeat);
            }

            // Notify state manager of beat
            if (stateManager != null) {
                stateManager.HandleBeatAdvancement(_currentBeat);
            }

            _isFirstBeat = false;

            ScheduleNextBeat();
            PlayBeatSound();
            TriggerVisualFeedback();
            UpdateUI();
            IncrementBeat();

            _nextBeatTime += _beatIntervalSeconds;
        }
    }

    private void ScheduleNextBeat() {
        // Schedule audio for next beat
        if (_audioSource != null && !_isAudioMuted) {
            AudioClip clip = (_currentBeat == 1) ? strongBeatClip : regularBeatClip;
            if (clip != null) {
                _audioSource.PlayScheduled(_nextBeatTime);
            }
        }
    }

    private void PlayBeatSound() {
        if (_audioSource != null && !_isAudioMuted) {
            AudioClip clip = (_currentBeat == 1) ? strongBeatClip : regularBeatClip;
            if (clip != null) {
                _audioSource.PlayOneShot(clip);
            }
        }
    }

    private void TriggerVisualFeedback() {
        if (!_enableVisualFeedback) return;

        foreach (var indicator in _visualIndicators) {
            if (indicator != null) {
                indicator.Flash(_currentBeat == 1);
            }
        }
    }

    private void IncrementBeat() {
        _currentBeat++;
        if (_currentBeat > _beatsPerMeasure) {
            _currentBeat = 1;
        }
    }

    // ════════════════════════════════════════════════════════════
    // EVENT HANDLERS
    // ════════════════════════════════════════════════════════════

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

    private void HandleStateManagerMeasureChanged(int newMeasure) {
        if (debugChangeSystem) {
            Debug.Log($"[v5] StateManager measure changed to: {newMeasure}");
        }
    }

    private void HandleStateManagerBeatChanged(int newBeat) {
        // Could add beat-specific logic here if needed
    }

    private void RelayCompositionStarted() {
        OnPreRollCompleted?.Invoke();
        if (debugChangeSystem) {
            Debug.Log("[v5] Composition started");
        }
    }

    // ════════════════════════════════════════════════════════════
    // CHANGE APPLICATION
    // ════════════════════════════════════════════════════════════

    private void ApplyChange(MetronomeChange_v2 change) {
        if (debugChangeSystem) {
            Debug.Log($"[v5] Applying change: {change.GetChangeDescription()} at M{change.targetMeasure} B{change.targetBeat}");
        }

        bool settingsChanged = false;

        // Apply tempo change
        if (change.hasTempo) {
            Bpm = change.newBpm;
            settingsChanged = true;
        }

        // Apply time signature change
        if (change.hasTimeSignature) {
            BeatsPerMeasure = change.newBeatsPerMeasure;
            settingsChanged = true;
        }

        // Apply audio/visual events
        if (change.hasAudioEvent) {
            SetAudioMute(change.muteAudio);
        }

        if (change.hasVisualEvent) {
            SetVisualFeedback(!change.hideVisual);
        }

        // Handle stop
        if (change.type == MetronomeChange_v2.ChangeType.Stop) {
            StopMetronome();
        }

        if (settingsChanged) {
            OnMetronomeSettingsChanged?.Invoke(change.type, _bpm, _beatsPerMeasure);
        }
    }

    // ════════════════════════════════════════════════════════════
    // PUBLIC API
    // ════════════════════════════════════════════════════════════

    public void StartMetronome() {
        if (_isPlaying) return;

        _isPlaying = true;
        _isStopped = false;
        _nextBeatTime = AudioSettings.dspTime + _beatIntervalSeconds;

        OnStarted?.Invoke();

        if (debugChangeSystem) {
            Debug.Log("[v5] Metronome started");
        }
    }

    public void StartAtMeasure(int measure) {
        if (stateManager != null) {
            stateManager.SetCurrentMeasure(measure);
        }
        StartMetronome();
    }

    public void PauseMetronome() {
        _isPlaying = false;
        OnPaused?.Invoke();

        if (debugChangeSystem) {
            Debug.Log("[v5] Metronome paused");
        }
    }

    public void StopMetronome() {
        _isPlaying = false;
        _isStopped = true;
        OnStopped?.Invoke();

        if (debugChangeSystem) {
            Debug.Log("[v5] Metronome stopped");
        }
    }

    public void ResetMetronome() {
        _isPlaying = false;
        _isStopped = true;
        _currentBeat = 1;
        _isFirstBeat = true;

        if (stateManager != null) {
            stateManager.ResetState();
        }

        if (scheduler != null) {
            scheduler.ClearPendingChanges();
            scheduler.ResetNotificationStates();
        }

        OnReset?.Invoke();
        UpdateUI();

        if (debugChangeSystem) {
            Debug.Log("[v5] Metronome reset");
        }
    }

    // ════════════════════════════════════════════════════════════
    // PHASE 1 SCHEDULING METHODS
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Schedule tempo change at measure start (traditional)
    /// </summary>
    public void ScheduleTempoChange(int targetMeasure, float newBpm, string description = "") {
        if (scheduler != null) {
            scheduler.ScheduleTempoChange(targetMeasure, newBpm, description);
        }
    }

    /// <summary>
    /// ✨ PHASE 1 FEATURE: Schedule tempo change at specific beat
    /// This is what enables smooth tempo ramps!
    /// </summary>
    public void ScheduleTempoChangeAtBeat(int measure, int beat, float bpm, string description = "") {
        if (scheduler != null) {
            scheduler.ScheduleTempoChangeAtBeat(measure, beat, bpm, description);
        }
    }

    /// <summary>
    /// Schedule time signature change
    /// </summary>
    public void ScheduleTimeSignatureChange(int targetMeasure, int newBeatsPerMeasure, string description = "") {
        if (scheduler != null) {
            scheduler.ScheduleTimeSignatureChange(targetMeasure, newBeatsPerMeasure, description);
        }
    }

    // ════════════════════════════════════════════════════════════
    // SETTINGS
    // ════════════════════════════════════════════════════════════

    public void SetTempo(float newTempo) {
        Bpm = newTempo;
    }

    public void SetTimeSignature(int newBeatsPerMeasure, int newBeatUnit = 4) {
        BeatsPerMeasure = newBeatsPerMeasure;
        _beatUnit = newBeatUnit;
    }

    public void SetAudioMute(bool muted) {
        _isAudioMuted = muted;
        OnAudioMuteChanged?.Invoke(muted);
    }

    public void SetVisualFeedback(bool enable) {
        _enableVisualFeedback = enable;
    }

    // ════════════════════════════════════════════════════════════
    // UI
    // ════════════════════════════════════════════════════════════

    private void UpdateUI() {
        if (statusText != null) {
            string status = _isPlaying ? "Playing" : (_isStopped ? "Stopped" : "Paused");
            statusText.text = $"M{CurrentMeasure} B{_currentBeat} | {_bpm:F0} BPM | {status}";
        }
    }

    private void OnBpmInputChanged(string value) {
        if (float.TryParse(value, out float newBpm)) {
            Bpm = Mathf.Clamp(newBpm, 30f, 300f);
        }
    }

    private void OnBeatsInputChanged(string value) {
        if (int.TryParse(value, out int newBeats)) {
            BeatsPerMeasure = Mathf.Clamp(newBeats, 1, 12);
        }
    }
}