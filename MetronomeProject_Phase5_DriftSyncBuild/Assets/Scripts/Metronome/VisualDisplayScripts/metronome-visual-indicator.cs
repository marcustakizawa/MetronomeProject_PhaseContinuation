using UnityEngine;
using UnityEngine.UI;

public class MetronomeVisualIndicator : MonoBehaviour {
    [Header("Visual Settings")]
    [SerializeField] private Image indicatorImage;
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color regularBeatColor = Color.red;
    [SerializeField] private Color strongBeatColor = Color.blue;  // Different color for first beat
    [SerializeField] private float flashDuration = 0.1f;

    // State tracking
    private bool isFlashing = false;
    private float flashTimer = 0f;
    private bool isStrongBeat = false;

    private void Start() {
        // Initialize with default color if not already set
        if (indicatorImage != null && defaultColor == Color.white) {
            defaultColor = indicatorImage.color;
        }
    }

    private void Update() {
        // Handle flash timing
        if (isFlashing) {
            flashTimer += Time.deltaTime;

            if (flashTimer >= flashDuration) {
                isFlashing = false;
                if (indicatorImage != null) {
                    indicatorImage.color = defaultColor;
                }
            }
        }
    }

    // Trigger a flash (called by the metronome)
    public void Flash(bool strongBeat = false) {
        if (indicatorImage == null) return;

        isFlashing = true;
        isStrongBeat = strongBeat;
        flashTimer = 0f;

        // Use different color based on beat type
        indicatorImage.color = strongBeat ? strongBeatColor : regularBeatColor;
    }

    // Reset indicator to default state
    public void Reset() {
        isFlashing = false;
        if (indicatorImage != null) {
            indicatorImage.color = defaultColor;
        }
    }

    // Configure visual properties at runtime
    public void Configure(Color newDefaultColor, Color newRegularColor, Color newStrongColor, float newFlashDuration) {
        defaultColor = newDefaultColor;
        regularBeatColor = newRegularColor;
        strongBeatColor = newStrongColor;
        flashDuration = newFlashDuration;

        // Apply default color immediately
        if (indicatorImage != null) {
            indicatorImage.color = defaultColor;
        }
    }
}
