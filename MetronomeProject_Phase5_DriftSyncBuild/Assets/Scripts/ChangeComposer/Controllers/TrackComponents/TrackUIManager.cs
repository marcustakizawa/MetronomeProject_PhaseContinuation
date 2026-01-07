using UnityEngine;
using UnityEngine.UI;

namespace ChangeComposer.Controllers {
    /// <summary>
    /// Simplified TrackUIManager - removed all validation color complexity
    /// Just handles button states and text-based status feedback
    /// No more mysterious color issues to debug!
    /// </summary>
    public class TrackUIManager : MonoBehaviour {
        [Header("Status Text (Optional)")]
        [SerializeField] private Text musicalStatusText;
        [SerializeField] private Text controlStatusText;

        [Header("Action Buttons")]
        [SerializeField] private Button loadToMetronomeButton;
        [SerializeField] private Button saveTrackButton;
        [SerializeField] private Button loadTrackButton;

        // Component references
        private TrackPanel trackPanel;
        private TrackMetronomeManager metronomeManager;

        private void Start() {
            trackPanel = GetComponent<TrackPanel>();
            metronomeManager = GetComponent<TrackMetronomeManager>();

            UpdateUI();

            Debug.Log("[TrackUIManager] Initialized - simple text-only feedback");
        }

        // === MUSICAL CONTENT UI FEEDBACK (Text Only) ===

        public void ShowMusicalSuccess(int count) {
            SetStatusText(musicalStatusText, $"✓ {count} events parsed");
            UpdateUI();
        }

        public void ShowMusicalError(int count, string error) {
            SetStatusText(musicalStatusText, $"✗ {count} error{(count != 1 ? "s" : "")}");
            Debug.LogWarning($"[TrackUIManager] Musical parsing error: {error}");
            UpdateUI();
        }

        public void ShowMusicalWarning(int count, string warning) {
            SetStatusText(musicalStatusText, $"⚠ {count} warning{(count != 1 ? "s" : "")}");
            UpdateUI();
        }

        public void ShowMusicalPending(string message) {
            SetStatusText(musicalStatusText, message);
            UpdateUI();
        }

        // === CONTROL CONTENT UI FEEDBACK (Text Only) ===

        public void ShowControlSuccess(int count) {
            SetStatusText(controlStatusText, $"✓ {count} events parsed");
            UpdateUI();
        }

        public void ShowControlError(int count, string error) {
            SetStatusText(controlStatusText, $"✗ {count} error{(count != 1 ? "s" : "")}");
            Debug.LogWarning($"[TrackUIManager] Control parsing error: {error}");
            UpdateUI();
        }

        public void ShowControlWarning(int count, string warning) {
            SetStatusText(controlStatusText, $"⚠ {count} warning{(count != 1 ? "s" : "")}");
            UpdateUI();
        }

        public void ShowControlPending(string message) {
            SetStatusText(controlStatusText, message);
            UpdateUI();
        }

        // === BUTTON STATE MANAGEMENT ===

        public void UpdateUI() {
            if (trackPanel == null) return;

            bool hasContent = trackPanel.HasParsedContent();
            bool hasMetronome = metronomeManager != null && metronomeManager.HasTargetMetronome();

            // Enable/disable buttons based on state
            SetButtonInteractable(loadToMetronomeButton, hasContent && hasMetronome);
            SetButtonInteractable(saveTrackButton, hasContent);
            // Load button is always enabled (to load files)

            // Update button text based on state
            UpdateButtonTexts(hasContent, hasMetronome);
        }

        private void UpdateButtonTexts(bool hasContent, bool hasMetronome) {
            // Update button text to show change count
            if (loadToMetronomeButton != null && hasContent) {
                int count = trackPanel.GetTotalChangeCount();
                var buttonText = loadToMetronomeButton.GetComponentInChildren<Text>();
                if (buttonText != null)
                    buttonText.text = $"Apply ({count})";
            }
        }

        // === SIMPLE FEEDBACK METHODS ===

        public void ShowTemporaryMessage(string message, float duration = 2f) {
            Debug.Log($"[TrackUIManager] {message}");
            // Could implement simple text-based temporary messages here if needed
        }

        public void ResetAllVisuals() {
            SetStatusText(musicalStatusText, "Ready");
            SetStatusText(controlStatusText, "Ready");
            UpdateUI();
        }

        // === UTILITY METHODS ===

        private void SetStatusText(Text textComponent, string text) {
            if (textComponent != null) {
                textComponent.text = text;
                // No color changes - just plain text
            }
        }

        private void SetButtonInteractable(Button button, bool interactable) {
            if (button != null)
                button.interactable = interactable;
        }

        // === DEBUG AND TESTING ===

        [ContextMenu("Test Status Messages")]
        public void TestStatusMessages() {
            StartCoroutine(TestSequence());
        }

        private System.Collections.IEnumerator TestSequence() {
            ShowMusicalPending("Parsing...");
            yield return new UnityEngine.WaitForSeconds(0.5f);

            ShowMusicalSuccess(3);
            yield return new UnityEngine.WaitForSeconds(0.5f);

            ShowControlError(1, "Test error");
            yield return new UnityEngine.WaitForSeconds(0.5f);

            ShowControlSuccess(5);
            yield return new UnityEngine.WaitForSeconds(0.5f);

            ResetAllVisuals();
        }

        [ContextMenu("Debug UI State")]
        public void DebugUIState() {
            Debug.Log($"=== TRACK UI MANAGER DEBUG ===");
            Debug.Log($"Has TrackPanel: {trackPanel != null}");
            Debug.Log($"Has MetronomeManager: {metronomeManager != null}");
            Debug.Log($"Has Content: {(trackPanel != null ? trackPanel.HasParsedContent() : false)}");
            Debug.Log($"Has Target Metronome: {(metronomeManager != null ? metronomeManager.HasTargetMetronome() : false)}");
            Debug.Log($"Musical Status: {(musicalStatusText != null ? musicalStatusText.text : "No text component")}");
            Debug.Log($"Control Status: {(controlStatusText != null ? controlStatusText.text : "No text component")}");
        }
    }
}