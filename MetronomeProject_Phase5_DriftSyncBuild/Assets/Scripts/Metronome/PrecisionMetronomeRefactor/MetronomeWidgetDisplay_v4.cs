using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Metronome Widget Display v4 - Compatible with PrecisionMetronome_v4_MultipleDisplays
/// Version: 2025-10-12 v4
/// 
/// Simplified metronome widget display - removed color state logic
/// Shows: "Metronome 1 | 4 Beats | 120 BPM | M10"
/// Just displays information without status colors
/// 
/// CHANGES FROM v3:
/// - Updated to work with PrecisionMetronome_v4_MultipleDisplays
/// - All type references updated to v4 metronome class
/// </summary>
public class MetronomeWidgetDisplay_v4 : MonoBehaviour {
    [Header("Metronome Reference")]
    [SerializeField] private PrecisionMetronome_v4_MultipleDisplays metronome;
    [SerializeField] private string metronomeLabel = "Metronome 1";

    [Header("UI References")]
    [SerializeField] private Text metronomeTitle; // "Metronome 1"
    [SerializeField] private Text beatsText; // "4 Beats"
    [SerializeField] private Text bpmText; // "120 BPM"  
    [SerializeField] private Text measureText; // "M10"
    [SerializeField] private Image statusIndicator; // Optional - now just decorative

    [Header("Display Format")]
    [SerializeField] private bool showBeats = true;
    [SerializeField] private bool showBPM = true;
    [SerializeField] private bool showMeasure = true;

    [Header("Simple Visual Settings")]
    [SerializeField] private Color defaultIndicatorColor = new Color(0.3f, 0.3f, 0.3f); // Neutral gray

    private void Start() {
        // No auto-finding - everything must be explicitly assigned in inspector

        // Auto-find metronome only if not assigned (this one is probably okay to keep)
        if (metronome == null)
            metronome = GetComponent<PrecisionMetronome_v4_MultipleDisplays>();

        if (metronome == null) {
            Debug.LogError($"MetronomeWidgetDisplay_v4 on {gameObject.name} has no metronome assigned!");
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

    // Simplified event handlers - no state logic
    private void OnBeatTriggered(int beat) => UpdateDisplay();
    private void OnMeasureChanged() => UpdateDisplay();
    private void OnSettingsChanged(ChangeComposer.Data.MetronomeChange.ChangeType type, float bpm, int beats) => UpdateDisplay();

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

    // Public configuration methods

    /// <summary>
    /// Set the metronome to monitor
    /// </summary>
    public void SetMetronome(PrecisionMetronome_v4_MultipleDisplays newMetronome, string label = "") {
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

    // Manual refresh method for debugging
    [ContextMenu("Force Update Display")]
    public void ForceUpdateDisplay() {
        UpdateDisplay();
    }

    // Getter for current metronome (useful for debugging)
    public PrecisionMetronome_v4_MultipleDisplays GetMetronome() => metronome;
}