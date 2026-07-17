using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Display container configuration
/// Each display shows the same time signature but can be in different locations
/// </summary>
[System.Serializable]
public class DisplayContainer {
    [Tooltip("Name for this display (e.g., 'Musician 1', 'Conductor', etc.)")]
    public string displayName = "Display";

    [Tooltip("The GameObject container for this display")]
    public GameObject container;

    [Tooltip("Array of Image components - should have enough for maximum beats per measure")]
    public Image[] beatIndicators;

    /// <summary>
    /// Validates that the configuration is correct
    /// </summary>
    public bool IsValid(int minRequiredIndicators) {
        if (container == null) return false;
        if (beatIndicators == null || beatIndicators.Length == 0) return false;

        // Must have enough indicators for the requested time signature
        if (beatIndicators.Length < minRequiredIndicators) return false;

        // Check that at least the required indicators are assigned
        for (int i = 0; i < minRequiredIndicators && i < beatIndicators.Length; i++) {
            if (beatIndicators[i] == null) return false;
        }

        return true;
    }
}

/// <summary>
/// Multi-Display Time Signature Visual Controller
/// Manages multiple synchronized displays, each showing the same metronome
/// All displays update together when time signature changes
/// </summary>
public class MultiDisplayTimeSignatureController : MonoBehaviour {

    [Header("Display Containers")]
    [Tooltip("Add one entry for each physical display (all will show the same time signature)")]
    [SerializeField] private List<DisplayContainer> displays = new List<DisplayContainer>();

    [Header("Beat Colors")]
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color downbeatColor = Color.blue;
    [SerializeField] private Color regularBeatColor = Color.red;
    [Space(10)]
    [SerializeField] private bool useCustomUpbeatColor = false;
    [SerializeField] private Color upbeatColor = Color.yellow;

    // Runtime tracking
    private int currentBeatsPerMeasure = 1;
    private List<DisplayContainer> activeDisplays;

    private void Awake() {
        InitializeDisplays();
    }

    private void InitializeDisplays() {
        activeDisplays = new List<DisplayContainer>();

        // Validate and activate all displays
        foreach (var display in displays) {
            // We'll validate against current beats per measure
            // (can be re-validated when time signature changes)
            if (!display.IsValid(1)) {
                Debug.LogWarning($"Invalid DisplayContainer configuration for '{display.displayName}'. Skipping.");
                continue;
            }

            // Activate the container
            if (display.container != null) {
                display.container.SetActive(true);
            }

            activeDisplays.Add(display);
        }

        // Reset all displays to default
        ResetAllDisplays();

        Debug.Log($"MultiDisplayTimeSignatureController initialized with {activeDisplays.Count} displays.");
    }

    /// <summary>
    /// Switch to a different time signature
    /// Updates ALL displays to show the new time signature
    /// </summary>
    public void SetTimeSignature(int beatsPerMeasure) {
        // Validate the beat count
        if (beatsPerMeasure < 1) {
            Debug.LogError($"Invalid beatsPerMeasure: {beatsPerMeasure}. Must be at least 1.");
            return;
        }

        // Check if any display doesn't have enough indicators
        foreach (var display in activeDisplays) {
            if (display.beatIndicators.Length < beatsPerMeasure) {
                Debug.LogError($"Display '{display.displayName}' only has {display.beatIndicators.Length} indicators but needs {beatsPerMeasure}!");
                return;
            }
        }

        // Update current time signature
        currentBeatsPerMeasure = beatsPerMeasure;

        // Reset all displays
        ResetAllDisplays();

        Debug.Log($"Switched to {beatsPerMeasure}/4 time signature across {activeDisplays.Count} displays");
    }

    /// <summary>
    /// Called by the metronome on each beat
    /// Updates ALL displays simultaneously
    /// beatNumber is 1-based (1, 2, 3, etc.)
    /// </summary>
    public void OnBeat(int beatNumber) {
        if (activeDisplays == null || activeDisplays.Count == 0) {
            Debug.LogWarning("No active displays available!");
            return;
        }

        // Validate beat number
        // TEMPORARILY DISABLED: Allow all beats through for debugging
        // if (beatNumber < 1 || beatNumber > currentBeatsPerMeasure) {
        //     Debug.LogWarning($"Invalid beat number {beatNumber} for {currentBeatsPerMeasure}/4 time signature");
        //     return;
        // }

        // Update currentBeatsPerMeasure dynamically based on incoming beats
        if (beatNumber > currentBeatsPerMeasure) {
            currentBeatsPerMeasure = beatNumber;
        }

        // Determine which color to use for this beat
        Color beatColor = GetColorForBeat(beatNumber);

        // Convert from 1-based beat number to 0-based array index
        int indicatorIndex = beatNumber - 1;

        // Update ALL displays
        foreach (var display in activeDisplays) {
            // Reset this display's indicators
            ResetDisplay(display);

            // Color the current beat
            if (indicatorIndex < display.beatIndicators.Length &&
                display.beatIndicators[indicatorIndex] != null) {
                display.beatIndicators[indicatorIndex].color = beatColor;
            }
        }
    }

    /// <summary>
    /// Reset all displays to default color
    /// </summary>
    private void ResetAllDisplays() {
        if (activeDisplays == null) return;

        foreach (var display in activeDisplays) {
            ResetDisplay(display);
        }
    }

    /// <summary>
    /// Reset a single display to default color
    /// Only resets the indicators currently in use
    /// </summary>
    private void ResetDisplay(DisplayContainer display) {
        if (display == null || display.beatIndicators == null) return;

        // Only reset the indicators we're actually using
        for (int i = 0; i < currentBeatsPerMeasure && i < display.beatIndicators.Length; i++) {
            if (display.beatIndicators[i] != null) {
                display.beatIndicators[i].color = defaultColor;
            }
        }
    }

    /// <summary>
    /// Determine the appropriate color for a given beat
    /// </summary>
    private Color GetColorForBeat(int beatNumber) {
        // Beat 1 is always the downbeat
        if (beatNumber == 1) {
            return downbeatColor;
        }

        // Last beat is the upbeat
        if (beatNumber == currentBeatsPerMeasure) {
            return useCustomUpbeatColor ? upbeatColor : regularBeatColor;
        }

        // Everything else is a regular beat
        return regularBeatColor;
    }

    /// <summary>
    /// Reset all visual indicators to default state
    /// </summary>
    public void Reset() {
        ResetAllDisplays();
    }

    /// <summary>
    /// Configure visual properties at runtime
    /// </summary>
    public void ConfigureColors(Color newDefaultColor, Color newDownbeatColor,
                                Color newRegularColor, Color newUpbeatColor,
                                bool useCustomUpbeat) {
        defaultColor = newDefaultColor;
        downbeatColor = newDownbeatColor;
        regularBeatColor = newRegularColor;
        upbeatColor = newUpbeatColor;
        useCustomUpbeatColor = useCustomUpbeat;

        // Apply default color immediately to all displays
        ResetAllDisplays();
    }

    /// <summary>
    /// Get the current time signature
    /// </summary>
    public int GetCurrentBeatsPerMeasure() {
        return currentBeatsPerMeasure;
    }

    /// <summary>
    /// Get number of active displays
    /// </summary>
    public int GetActiveDisplayCount() {
        return activeDisplays != null ? activeDisplays.Count : 0;
    }

#if UNITY_EDITOR
    private void OnValidate() {
        // Validate configurations in the editor
        foreach (var display in displays) {
            if (display.beatIndicators != null && display.beatIndicators.Length > 0) {
                // Check for null indicators
                for (int i = 0; i < display.beatIndicators.Length; i++) {
                    if (display.beatIndicators[i] == null) {
                        Debug.LogWarning($"Display '{display.displayName}': Beat indicator at index {i} is not assigned!");
                    }
                }
            }

            if (display.container == null) {
                Debug.LogWarning($"Display '{display.displayName}': Container is not assigned!");
            }
        }
    }
#endif
}