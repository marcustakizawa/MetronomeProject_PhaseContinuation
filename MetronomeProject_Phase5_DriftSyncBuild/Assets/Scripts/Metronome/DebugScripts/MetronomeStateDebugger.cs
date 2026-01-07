using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Diagnostic component to debug why metronome widgets show different states
/// Add this to each metronome to compare their initialization and state
/// </summary>
public class MetronomeStateDebugger : MonoBehaviour {
    [Header("Target Components")]
    [SerializeField] private PrecisionMetronome metronome;
    [SerializeField] private MetronomeWidgetDisplay widget;

    [Header("Debug Settings")]
    [SerializeField] private bool logInitialization = true;
    [SerializeField] private bool logStateChanges = true;
    [SerializeField] private bool logEverySecond = false;

    [Header("State Tracking")]
    [SerializeField] private bool isPlaying;
    [SerializeField] private bool isAudioMuted;
    [SerializeField] private bool isStopped;
    [SerializeField] private string currentState = "Unknown";
    [SerializeField] private Color currentIndicatorColor;
    [SerializeField] private int initializationFrame = -1;

    private void Start() {
        initializationFrame = Time.frameCount;

        // Auto-find components
        if (metronome == null) metronome = GetComponent<PrecisionMetronome>();
        if (widget == null) widget = GetComponent<MetronomeWidgetDisplay>();

        if (logInitialization) {
            LogState("START");
        }

        // Subscribe to metronome events if available
        if (metronome != null) {
            metronome.OnStarted += () => LogStateChange("OnStarted");
            metronome.OnPaused += () => LogStateChange("OnPaused");
            metronome.OnReset += () => LogStateChange("OnReset");

            try {
                metronome.OnStopped += () => LogStateChange("OnStopped");
                metronome.OnAudioMuteChanged += (muted) => LogStateChange($"OnAudioMuteChanged: {muted}");
            } catch {
                Debug.Log($"[{gameObject.name}] Some events not available");
            }
        }

        if (logEverySecond) {
            InvokeRepeating(nameof(LogCurrentState), 1f, 1f);
        }
    }

    private void Update() {
        // Track state changes
        if (metronome != null) {
            bool newIsPlaying = metronome.IsPlaying;
            bool newIsAudioMuted = metronome.IsAudioMuted;
            bool newIsStopped = metronome.IsStopped;

            if (newIsPlaying != isPlaying || newIsAudioMuted != isAudioMuted || newIsStopped != isStopped) {
                isPlaying = newIsPlaying;
                isAudioMuted = newIsAudioMuted;
                isStopped = newIsStopped;

                UpdateCurrentState();

                if (logStateChanges) {
                    LogState("STATE CHANGE");
                }
            }
        }

        // Track widget color
        if (widget != null) {
            var statusIndicator = GetStatusIndicatorImage();
            if (statusIndicator != null) {
                Color newColor = statusIndicator.color;
                if (newColor != currentIndicatorColor) {
                    currentIndicatorColor = newColor;
                    if (logStateChanges) {
                        Debug.Log($"[{gameObject.name}] Widget color changed to: {ColorToName(newColor)} {newColor}");
                    }
                }
            }
        }
    }

    private void UpdateCurrentState() {
        if (metronome == null) {
            currentState = "No Metronome";
            return;
        }

        // Determine state based on metronome properties
        if (isStopped) {
            currentState = "Stopped";
        } else if (isPlaying) {
            currentState = isAudioMuted ? "Playing (Muted)" : "Playing";
        } else {
            currentState = isAudioMuted ? "Paused (Muted)" : "Paused";
        }
    }

    private void LogState(string context) {
        if (metronome == null) {
            Debug.Log($"[{gameObject.name}] {context}: NO METRONOME COMPONENT");
            return;
        }

        UpdateCurrentState();

        Debug.Log($"[{gameObject.name}] {context}:");
        Debug.Log($"  Frame: {Time.frameCount} (Init: {initializationFrame})");
        Debug.Log($"  IsPlaying: {isPlaying}");
        Debug.Log($"  IsAudioMuted: {isAudioMuted}");
        Debug.Log($"  IsStopped: {isStopped}");
        Debug.Log($"  Current State: {currentState}");
        Debug.Log($"  Widget Color: {ColorToName(currentIndicatorColor)} {currentIndicatorColor}");
        Debug.Log($"  Measure: {metronome.CurrentMeasure}, Beat: {metronome.CurrentBeat}");
        Debug.Log($"  BPM: {metronome.Bpm}");
    }

    private void LogStateChange(string eventName) {
        if (logStateChanges) {
            Debug.Log($"[{gameObject.name}] EVENT: {eventName} at frame {Time.frameCount}");
            LogState($"AFTER {eventName}");
        }
    }

    private void LogCurrentState() {
        LogState("PERIODIC CHECK");
    }

    private Image GetStatusIndicatorImage() {
        if (widget == null) return null;

        // Use reflection to get the private statusIndicator field
        var field = typeof(MetronomeWidgetDisplay).GetField("statusIndicator",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field != null) {
            return field.GetValue(widget) as Image;
        }

        // Fallback: try to find by common names
        var images = widget.GetComponentsInChildren<Image>();
        foreach (var img in images) {
            if (img.gameObject.name.Contains("Status") ||
                img.gameObject.name.Contains("Indicator") ||
                img.gameObject.name.Contains("Metronome")) {
                return img;
            }
        }

        return null;
    }

    private string ColorToName(Color color) {
        // Convert color to approximate name
        if (ColorDistance(color, Color.red) < 0.1f) return "Red";
        if (ColorDistance(color, Color.green) < 0.1f) return "Green";
        if (ColorDistance(color, Color.yellow) < 0.1f) return "Yellow";
        if (ColorDistance(color, Color.gray) < 0.1f) return "Gray";
        if (ColorDistance(color, Color.white) < 0.1f) return "White";
        if (ColorDistance(color, Color.black) < 0.1f) return "Black";

        return "Unknown";
    }

    private float ColorDistance(Color a, Color b) {
        return Vector3.Distance(new Vector3(a.r, a.g, a.b), new Vector3(b.r, b.g, b.b));
    }

    // Public methods for manual testing

    [ContextMenu("Log Current State")]
    public void DebugCurrentState() {
        LogState("MANUAL DEBUG");
    }

    [ContextMenu("Compare With Other Metronomes")]
    public void CompareWithOthers() {
        var allDebuggers = FindObjectsOfType<MetronomeStateDebugger>();

        Debug.Log("=== METRONOME COMPARISON ===");
        foreach (var debugger in allDebuggers) {
            if (debugger.metronome != null) {
                Debug.Log($"{debugger.gameObject.name}: " +
                         $"Playing={debugger.metronome.IsPlaying}, " +
                         $"Muted={debugger.metronome.IsAudioMuted}, " +
                         $"Stopped={debugger.metronome.IsStopped}, " +
                         $"Color={ColorToName(debugger.currentIndicatorColor)}");
            }
        }
    }

    [ContextMenu("Force Widget Update")]
    public void ForceWidgetUpdate() {
        if (widget != null) {
            widget.UpdateDisplay();
            LogState("AFTER FORCE UPDATE");
        }
    }

    [ContextMenu("Test State Transitions")]
    public void TestStateTransitions() {
        if (metronome == null) return;

        Debug.Log($"[{gameObject.name}] Testing state transitions...");

        StartCoroutine(TestSequence());
    }

    private System.Collections.IEnumerator TestSequence() {
        LogState("Before Start");
        metronome.StartMetronome();
        yield return new WaitForSeconds(0.1f);

        LogState("After Start");
        metronome.PauseMetronome();
        yield return new WaitForSeconds(0.1f);

        LogState("After Pause");
        metronome.ResetMetronome();
        yield return new WaitForSeconds(0.1f);

        LogState("After Reset");
    }

    // === GLOBAL DIAGNOSTIC METHODS ===

    [ContextMenu("Check All Metronome States")]
    public void CheckAllMetronomeStates() {
        Debug.Log("=== METRONOME STATE CHECK ===");

        // Find all PrecisionMetronome components
        var metronomes = FindObjectsOfType<PrecisionMetronome>();

        for (int i = 0; i < metronomes.Length; i++) {
            var metronome = metronomes[i];
            Debug.Log($"Metronome {i + 1} ({metronome.gameObject.name}):");
            Debug.Log($"  IsPlaying: {metronome.IsPlaying}");
            Debug.Log($"  IsAudioMuted: {metronome.IsAudioMuted}");
            Debug.Log($"  IsStopped: {metronome.IsStopped}");
            Debug.Log($"  CurrentMeasure: {metronome.CurrentMeasure}");
            Debug.Log($"  BPM: {metronome.Bpm}");

            // Check the widget display
            var widget = metronome.GetComponent<MetronomeWidgetDisplay>();
            if (widget != null) {
                Debug.Log($"  Has Widget: YES");
                widget.UpdateDisplay(); // Force an update
            } else {
                Debug.Log($"  Has Widget: NO");
            }

            Debug.Log(""); // Empty line for readability
        }

        // Also check MetronomeWidgetDisplay components separately
        Debug.Log("=== WIDGET DISPLAY CHECK ===");
        var widgets = FindObjectsOfType<MetronomeWidgetDisplay>();

        for (int i = 0; i < widgets.Length; i++) {
            var widget = widgets[i];
            var widgetMetronome = widget.GetMetronome(); // Using the getter from MetronomeWidgetDisplay

            Debug.Log($"Widget {i + 1} ({widget.gameObject.name}):");
            if (widgetMetronome != null) {
                Debug.Log($"  Connected to: {widgetMetronome.gameObject.name}");
                Debug.Log($"  Metronome IsPlaying: {widgetMetronome.IsPlaying}");
                Debug.Log($"  Metronome IsStopped: {widgetMetronome.IsStopped}");
                Debug.Log($"  Metronome IsAudioMuted: {widgetMetronome.IsAudioMuted}");

                // Check what the widget thinks the color should be
                string expectedColor = "Unknown";
                if (widgetMetronome.IsStopped) expectedColor = "Red (Stopped)";
                else if (widgetMetronome.IsAudioMuted) expectedColor = "Gray (Muted)";
                else if (widgetMetronome.IsPlaying) expectedColor = "Green (Playing)";
                else expectedColor = "Yellow (Paused)";

                Debug.Log($"  Expected Color: {expectedColor}");
            } else {
                Debug.Log($"  Connected to: NULL - This is the problem!");
            }
            Debug.Log("");
        }
    }

    [ContextMenu("Force Update All Widgets")]
    public void ForceUpdateAllWidgets() {
        var widgets = FindObjectsOfType<MetronomeWidgetDisplay>();
        Debug.Log($"Forcing update on {widgets.Length} widgets...");

        foreach (var widget in widgets) {
            widget.UpdateDisplay();
            Debug.Log($"Updated widget on {widget.gameObject.name}");
        }
    }
}