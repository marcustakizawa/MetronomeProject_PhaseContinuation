using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MetronomeWidgetDisplay v5 - Compatible with PrecisionMetronome_v5_BeatLevel
/// Version: 2025-01-13 v5
/// 
/// CHANGES FROM v4:
/// - Updated OnSettingsChanged signature to match v5 (removed ChangeType parameter)
/// - Updated type references from v4 to v5
/// - Maintains all display functionality
/// </summary>
public class MetronomeWidgetDisplay_v5 : MonoBehaviour {

    [Header("Metronome Reference")]
    [SerializeField] private PrecisionMetronome_v5_BeatLevel metronome;
    [SerializeField] private string metronomeLabel = "Metronome";

    [Header("UI Text References")]
    [SerializeField] private Text metronomeTitle;
    [SerializeField] private Text beatsText;
    [SerializeField] private Text bpmText;
    [SerializeField] private Text measureText;

    [Header("Status Indicator (Optional)")]
    [SerializeField] private Image statusIndicator;
    [SerializeField] private Color defaultIndicatorColor = Color.white;

    [Header("Display Options")]
    [SerializeField] private bool showBeats = true;
    [SerializeField] private bool showBPM = true;
    [SerializeField] private bool showMeasure = true;

    void Start() {
        if (metronome == null) {
            Debug.LogWarning($"MetronomeWidgetDisplay '{metronomeLabel}' has no metronome assigned!");
            SetError("No Metronome");
            return;
        }

        // Subscribe to only the essential metronome events
        SubscribeToMetronome();

        // Set the title
        SetTitle(metronomeLabel);

        // Set up indicator (if assigned)
        SetupIndicator();

        // Initial display update
        UpdateDisplay();
    }

    private void SubscribeToMetronome() {
        if (metronome == null) return;

        // Only subscribe to events that affect the display data
        metronome.OnBeatTriggered += OnBeatTriggered;
        metronome.OnMeasureChanged += OnMeasureChanged;
        metronome.OnMetronomeSettingsChanged += OnSettingsChanged;
    }

    private void OnDestroy() {
        // Unsubscribe to prevent errors
        if (metronome != null) {
            metronome.OnBeatTriggered -= OnBeatTriggered;
            metronome.OnMeasureChanged -= OnMeasureChanged;
            metronome.OnMetronomeSettingsChanged -= OnSettingsChanged;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // EVENT HANDLERS - UPDATED for v5
    // ═══════════════════════════════════════════════════════════

    private void OnBeatTriggered(int beat) => UpdateDisplay();
    private void OnMeasureChanged() => UpdateDisplay();

    /// <summary>
    /// UPDATED in v5: Only receives bpm and beats (no ChangeType)
    /// </summary>
    private void OnSettingsChanged(float bpm, int beats) => UpdateDisplay();

    /// <summary>
    /// Update the widget display with current metronome data
    /// </summary>
    public void UpdateDisplay() {
        if (metronome == null) {
            SetError("No Metronome");
            return;
        }

        // Update individual text components - just the data, no state logic
        if (showBeats && beatsText != null)
            beatsText.text = $"{metronome.BeatsPerMeasure} Beats";

        if (showBPM && bpmText != null) {
            bpmText.text = $"{metronome.Bpm:F2} BPM";
        }

        if (showMeasure && measureText != null)
            measureText.text = $"M{metronome.CurrentMeasure}";
    }

    /// <summary>
    /// Set up the status indicator (if assigned)
    /// </summary>
    private void SetupIndicator() {
        if (statusIndicator != null) {
            statusIndicator.color = defaultIndicatorColor;
        }
    }

    /// <summary>
    /// Set the metronome title
    /// </summary>
    private void SetTitle(string title) {
        if (metronomeTitle != null)
            metronomeTitle.text = title;
    }

    /// <summary>
    /// Set error state on all text components
    /// </summary>
    private void SetError(string errorText) {
        if (metronomeTitle != null) metronomeTitle.text = errorText;
        if (beatsText != null) beatsText.text = "--";
        if (bpmText != null) bpmText.text = "--";
        if (measureText != null) measureText.text = "--";
    }

    // ═══════════════════════════════════════════════════════════
    // PUBLIC CONFIGURATION METHODS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Set the metronome to monitor
    /// </summary>
    public void SetMetronome(PrecisionMetronome_v5_BeatLevel newMetronome, string label = "") {
        // Unsubscribe from old metronome
        if (metronome != null) {
            metronome.OnBeatTriggered -= OnBeatTriggered;
            metronome.OnMeasureChanged -= OnMeasureChanged;
            metronome.OnMetronomeSettingsChanged -= OnSettingsChanged;
        }

        // Set new metronome
        metronome = newMetronome;

        if (!string.IsNullOrEmpty(label))
            metronomeLabel = label;

        // Subscribe to new metronome
        if (metronome != null)
            SubscribeToMetronome();

        // Update display
        UpdateDisplay();
    }

    /// <summary>
    /// Configure what information to show
    /// </summary>
    public void SetDisplayOptions(bool beats = true, bool bpm = true, bool measure = true) {
        showBeats = beats;
        showBPM = bpm;
        showMeasure = measure;

        // Hide/show text components based on options
        if (beatsText != null) beatsText.gameObject.SetActive(showBeats);
        if (bpmText != null) bpmText.gameObject.SetActive(showBPM);
        if (measureText != null) measureText.gameObject.SetActive(showMeasure);

        UpdateDisplay();
    }

    /// <summary>
    /// Set the label for this widget
    /// </summary>
    public void SetLabel(string label) {
        metronomeLabel = label;
        SetTitle(label);
    }

    /// <summary>
    /// Set indicator color (if you want a specific color)
    /// </summary>
    public void SetIndicatorColor(Color color) {
        defaultIndicatorColor = color;
        if (statusIndicator != null) {
            statusIndicator.color = color;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // DEBUG METHODS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Manual refresh method for debugging
    /// </summary>
    [ContextMenu("Force Update Display")]
    public void ForceUpdateDisplay() {
        UpdateDisplay();
    }

    /// <summary>
    /// Getter for current metronome (useful for debugging)
    /// </summary>
    public PrecisionMetronome_v5_BeatLevel GetMetronome() => metronome;
}