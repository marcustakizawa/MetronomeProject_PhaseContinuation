using System;
using UnityEngine;

/// <summary>
/// MetronomeStateManager - Phase 2 Refactoring Component
/// Version: 2025-08-23 v1
/// 
/// PURPOSE: Extract all state management logic from PrecisionMetronome
/// - Single responsibility: State tracking and transitions
/// - Event-driven communication with metronome
/// - Handles measure progression, pre-roll logic, display formatting
/// - 100% backward compatibility through delegation
/// </summary>
public class MetronomeStateManager_v1 : MonoBehaviour {

    [Header("Debug Settings")]
    [SerializeField] private bool debugStateSystem = true;
    [SerializeField] private bool verboseLogging = false;

    [Header("Display Settings")]
    [SerializeField] private Color _normalTextColor = Color.white;
    [SerializeField] private Color _firstBeatColor = Color.yellow;

    // === EVENTS FOR COMMUNICATION ===

    /// <summary>
    /// Fired when the current measure changes
    /// </summary>
    public event Action<int> OnMeasureStateChanged;

    /// <summary>
    /// Fired when the current beat changes
    /// </summary>
    public event Action<int> OnBeatStateChanged;

    /// <summary>
    /// Fired when measure reaches measure 1 (composition start)
    /// </summary>
    public event Action OnCompositionStarted;

    /// <summary>
    /// Fired when state properties change (for UI updates)
    /// </summary>
    public event Action OnDisplayStateChanged;

    // === INTERNAL STATE ===

    private int _currentMeasure = 1;
    private int _currentBeat = 1;
    private bool _isFirstBeat = true;

    // === PUBLIC API (Extracted from PrecisionMetronome) ===

    /// <summary>
    /// Set the current measure (for jumping scenarios)
    /// </summary>
    public void SetCurrentMeasure(int measure) {
        measure = Mathf.Max(1, measure); // Ensure positive measures only

        int oldMeasure = _currentMeasure;
        _currentMeasure = measure;
        _currentBeat = 1;
        _isFirstBeat = true;

        if (debugStateSystem) {
            LogDebug($"Current measure set: {oldMeasure} → {_currentMeasure}");
        }

        OnMeasureStateChanged?.Invoke(_currentMeasure);
        OnBeatStateChanged?.Invoke(_currentBeat);
        OnDisplayStateChanged?.Invoke();
    }

    /// <summary>
    /// Handle measure advancement (called by metronome on measure changes)
    /// </summary>
    public void HandleMeasureAdvancement() {
        _currentMeasure++;

        if (debugStateSystem) {
            LogDebug($"Measure advanced to: {_currentMeasure}");
        }

        // Fire composition start event if reaching measure 1
        if (_currentMeasure == 1) {
            OnCompositionStarted?.Invoke();
            if (debugStateSystem) {
                LogDebug("Composition started (reached measure 1)!");
            }
        }

        OnMeasureStateChanged?.Invoke(_currentMeasure);
        OnDisplayStateChanged?.Invoke();
    }

    /// <summary>
    /// Handle beat advancement (called by metronome on beat changes)
    /// </summary>
    public void HandleBeatAdvancement(int newBeat) {
        _currentBeat = newBeat;
        _isFirstBeat = false;

        if (verboseLogging) {
            LogDebug($"Beat advanced to: {_currentBeat}");
        }

        OnBeatStateChanged?.Invoke(_currentBeat);
    }

    /// <summary>
    /// Reset state to initial values
    /// </summary>
    public void ResetState() {
        int oldMeasure = _currentMeasure;
        int oldBeat = _currentBeat;

        _currentMeasure = 1;
        _currentBeat = 1;
        _isFirstBeat = true;

        if (debugStateSystem) {
            LogDebug($"State reset: M{oldMeasure}B{oldBeat} → M{_currentMeasure}B{_currentBeat}");
        }

        OnMeasureStateChanged?.Invoke(_currentMeasure);
        OnBeatStateChanged?.Invoke(_currentBeat);
        OnDisplayStateChanged?.Invoke();
    }

    
    // === DISPLAY FORMATTING HELPERS ===

    /// <summary>
    /// Get formatted measure text for display
    /// </summary>
    public string GetMeasureDisplayText() {
        return _currentMeasure.ToString();
    }

    /// <summary>
    /// Get appropriate color for measure display based on beat position
    /// </summary>
    public Color GetMeasureDisplayColor() {
        return _isFirstBeat ? _firstBeatColor : _normalTextColor;
    }

    /// <summary>
    /// Get appropriate color for current beat
    /// </summary>
    public Color GetBeatDisplayColor() {
        return _isFirstBeat ? _firstBeatColor : _normalTextColor;
    }

    // === PROPERTIES ===

    public int CurrentMeasure => _currentMeasure;
    public int CurrentBeat => _currentBeat;
    public bool IsFirstBeat => _isFirstBeat;

    // === DISPLAY CONFIGURATION ===

    /// <summary>
    /// Set display colors
    /// </summary>
    public void SetDisplayColors(Color normalColor, Color firstBeatColor) {
        _normalTextColor = normalColor;
        _firstBeatColor = firstBeatColor;

        if (debugStateSystem) {
            LogDebug("Display colors updated");
        }

        OnDisplayStateChanged?.Invoke();
    }

    // === LOGGING ===

    private void LogDebug(string message) {
        if (debugStateSystem) {
            Debug.Log($"[MetronomeStateManager] {message}");
        }
    }

    private void LogWarning(string message) {
        Debug.LogWarning($"[MetronomeStateManager] ⚠️ {message}");
    }

    private void LogError(string message) {
        Debug.LogError($"[MetronomeStateManager] ❌ {message}");
    }

    // === DEBUG METHODS ===

    /// <summary>
    /// Debug: Log current state
    /// </summary>
    [ContextMenu("Debug Current State")]
    public void DebugCurrentState() {
        Debug.Log($"=== METRONOME STATE MANAGER DEBUG ===");
        Debug.Log($"Current measure: {_currentMeasure}");
        Debug.Log($"Current beat: {_currentBeat}");
        Debug.Log($"Is first beat: {_isFirstBeat}");
    }

    /// <summary>
    /// Debug: Test state transitions
    /// </summary>
    [ContextMenu("Test State Transitions")]
    public void TestStateTransitions() {
        LogDebug("Testing state transitions...");

        SetCurrentMeasure(1);
        HandleMeasureAdvancement(); // → M2
        HandleMeasureAdvancement(); // → M3
        HandleBeatAdvancement(2);
        HandleBeatAdvancement(3);

        DebugCurrentState();
    }
}