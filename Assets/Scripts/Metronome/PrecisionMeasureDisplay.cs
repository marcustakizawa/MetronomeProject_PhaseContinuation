using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Standard UI namespace instead of TMPro

public class PrecisionMeasureDisplay : MonoBehaviour
{
    [Header("Metronome Reference")]
    [SerializeField] private PrecisionMetronome metronome;

    [Header("UI References")]
    [SerializeField] private Text measureText; // Changed from TextMeshProUGUI to Text
    [SerializeField] private string prefix = "Measure: ";

    [Header("Visual Settings")]
    [SerializeField] private bool highlightFirstBeat = true;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color firstBeatColor = Color.yellow;
    
    [Header("Pre-roll Settings")]
    [SerializeField] private bool showPreRollMeasures = true;
    [SerializeField] private Color preRollColor = new Color(0.8f, 0.5f, 0.5f); // Match PrecisionMetronome

    private void Start() {
        if (metronome == null) {
            metronome = GetComponent<PrecisionMetronome>();
            if (metronome == null) {
                Debug.LogError("MeasureDisplay requires a reference to PrecisionMetronome");
                enabled = false;
                return;
            }
        }

        // Subscribe to metronome events
        metronome.OnMeasureChanged += OnMeasureChanged;
        metronome.OnBeatTriggered += OnBeatTriggered;
        metronome.OnReset += OnMetronomeReset;
        
        // Additionally, listen for pre-roll completed event if available
        if (metronome.GetType().GetEvent("OnPreRollCompleted") != null) {
            metronome.OnPreRollCompleted += OnPreRollCompleted;
        }
        
        // Initialize the display
        UpdateMeasureText();
    }

    private void OnMeasureChanged() {
        UpdateMeasureText();

        // Apply highlight if enabled
        if (highlightFirstBeat && measureText != null) {
            measureText.color = GetAppropriateColor(true);
        }
    }

    private void OnBeatTriggered(int beatNumber) {
        // If it's not the first beat and we're highlighting beats, revert to normal color
        if (beatNumber != 1 && highlightFirstBeat && measureText != null) {
            measureText.color = GetAppropriateColor(false);
        }
    }
    
    private void OnPreRollCompleted() {
        // Update display when pre-roll completes to ensure correct coloring
        UpdateMeasureText();
    }

    private void OnMetronomeReset() {
        // Update the display after reset
        UpdateMeasureText();

        // Reset color
        if (measureText != null) {
            measureText.color = normalColor;
        }
    }

    private void UpdateMeasureText() {
        if (measureText == null || metronome == null) return;
        
        // Use the metronome's helper methods if available for consistent formatting
        if (metronome.GetType().GetMethod("GetMeasureDisplayText") != null) {
            // Use the metronome's own formatting method for consistency
            string measureDisplayText = metronome.GetMeasureDisplayText();
            measureText.text = prefix + measureDisplayText;
            
            // Use the metronome's color method if available
            if (metronome.GetType().GetMethod("GetMeasureDisplayColor") != null) {
                measureText.color = metronome.GetMeasureDisplayColor();
            } else {
                measureText.color = GetAppropriateColor(metronome.CurrentBeat == 1);
            }
        } else {
            // Fallback to our own formatting
            int currentMeasure = metronome.CurrentMeasure;
            measureText.text = prefix + currentMeasure.ToString();
            measureText.color = GetAppropriateColor(metronome.CurrentBeat == 1);
        }
    }
    
    // Helper to determine the appropriate color based on pre-roll state and beat
    private Color GetAppropriateColor(bool isFirstBeat) {
        if (metronome == null) return normalColor;
        
        bool isPreRoll = metronome.CurrentMeasure < 1;
        
        if (isPreRoll && showPreRollMeasures) {
            return isFirstBeat ? firstBeatColor : preRollColor;
        } else {
            return isFirstBeat ? firstBeatColor : normalColor;
        }
    }
    
    // Public methods to configure at runtime
    
    public void SetShowPreRollMeasures(bool show) {
        showPreRollMeasures = show;
        UpdateMeasureText();
    }
    
    public void SetMetronome(PrecisionMetronome newMetronome) {
        // Unsubscribe from old metronome events
        if (metronome != null) {
            metronome.OnMeasureChanged -= OnMeasureChanged;
            metronome.OnBeatTriggered -= OnBeatTriggered;
            metronome.OnReset -= OnMetronomeReset;
            try { metronome.OnPreRollCompleted -= OnPreRollCompleted; } catch { }
        }
        
        // Set new metronome
        metronome = newMetronome;
        
        // Subscribe to new metronome events
        if (metronome != null) {
            metronome.OnMeasureChanged += OnMeasureChanged;
            metronome.OnBeatTriggered += OnBeatTriggered;
            metronome.OnReset += OnMetronomeReset;
            try { metronome.OnPreRollCompleted += OnPreRollCompleted; } catch { }
        }
        
        // Update display
        UpdateMeasureText();
    }
}