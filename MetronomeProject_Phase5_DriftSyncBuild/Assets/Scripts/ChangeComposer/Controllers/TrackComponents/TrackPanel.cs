using UnityEngine;
using UnityEngine.UI;
using ChangeComposer.Data;
using System.Collections.Generic;

namespace ChangeComposer.Controllers {
    /// <summary>
    /// Streamlined TrackPanel - Core parsing and validation only
    /// File operations moved to TrackFileManager
    /// UI management moved to TrackUIManager
    /// Metronome operations moved to TrackMetronomeManager
    /// </summary>
    public class TrackPanel : MonoBehaviour {
        [Header("Input Fields")]
        [SerializeField] private InputField musicalInput;
        [SerializeField] private InputField controlInput;

        [Header("Parse Buttons")]
        [SerializeField] private Button parseMusicalButton;
        [SerializeField] private Button clearMusicalButton;
        [SerializeField] private Button parseControlButton;
        [SerializeField] private Button clearControlButton;

        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugLogs = true;

        // Core data - parsed changes
        private List<MetronomeChange> musicalChanges = new List<MetronomeChange>();
        private List<MetronomeChange> controlChanges = new List<MetronomeChange>();

        // Component references (auto-found)
        private TrackUIManager uiManager;
        private TrackFileManager fileManager;
        private TrackMetronomeManager metronomeManager;

        private void Start() {
            SetupButtons();
            FindComponents();
            LogDebug("TrackPanel initialized - core parsing only");
        }

        private void SetupButtons() {
            if (parseMusicalButton != null)
                parseMusicalButton.onClick.AddListener(ParseMusicalContent);

            if (clearMusicalButton != null)
                clearMusicalButton.onClick.AddListener(ClearMusicalContent);

            if (parseControlButton != null)
                parseControlButton.onClick.AddListener(ParseControlContent);

            if (clearControlButton != null)
                clearControlButton.onClick.AddListener(ClearControlContent);
        }

        private void FindComponents() {
            // Auto-find companion components
            uiManager = GetComponent<TrackUIManager>();
            fileManager = GetComponent<TrackFileManager>();
            metronomeManager = GetComponent<TrackMetronomeManager>();

            if (uiManager == null)
                LogDebug("No TrackUIManager found - UI feedback will be limited");

            if (fileManager == null)
                LogDebug("No TrackFileManager found - file operations disabled");

            if (metronomeManager == null)
                LogDebug("No TrackMetronomeManager found - metronome loading disabled");
        }

        // === CORE PARSING METHODS ===

        /// <summary>
        /// Parse musical content (tempo/time signature changes)
        /// </summary>
        public void ParseMusicalContent() {
            if (musicalInput == null) {
                LogError("No musical input field assigned");
                return;
            }

            string[] lines = musicalInput.text.Split('\n');
            musicalChanges.Clear();

            int successCount = 0;
            int errorCount = 0;
            string lastError = "";

            LogDebug($"Parsing {lines.Length} musical lines...");

            foreach (string line in lines) {
                var result = MusicalParser.ParseMusical(line);

                if (result.isValid && result.parsedChange != null) {
                    musicalChanges.Add(result.parsedChange);
                    successCount++;
                } else if (!string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("//")) {
                    errorCount++;
                    lastError = result.GetErrorSummary();
                }
            }

            // Notify UI manager of results
            if (uiManager != null) {
                if (errorCount == 0 && successCount > 0)
                    uiManager.ShowMusicalSuccess(successCount);
                else if (errorCount > 0)
                    uiManager.ShowMusicalError(errorCount, lastError);
                else
                    uiManager.ShowMusicalPending("No content to parse");
            }

            LogDebug($"Musical parsing complete: {successCount} success, {errorCount} errors");
        }

        /// <summary>
        /// Parse control content (mute/visual/stop events)
        /// </summary>
        public void ParseControlContent() {
            if (controlInput == null) {
                LogError("No control input field assigned");
                return;
            }

            string[] lines = controlInput.text.Split('\n');
            controlChanges.Clear();

            int successCount = 0;
            int errorCount = 0;
            string lastError = "";

            LogDebug($"Parsing {lines.Length} control lines...");

            foreach (string line in lines) {
                var result = ControlParser.ParseControl(line);

                if (result.isValid && result.parsedChange != null) {
                    controlChanges.Add(result.parsedChange);
                    successCount++;
                } else if (!string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("//")) {
                    errorCount++;
                    lastError = result.GetErrorSummary();
                }
            }

            // Notify UI manager of results
            if (uiManager != null) {
                if (errorCount == 0 && successCount > 0)
                    uiManager.ShowControlSuccess(successCount);
                else if (errorCount > 0)
                    uiManager.ShowControlError(errorCount, lastError);
                else
                    uiManager.ShowControlPending("No content to parse");
            }

            LogDebug($"Control parsing complete: {successCount} success, {errorCount} errors");
        }

        // === CLEAR OPERATIONS ===

        public void ClearMusicalContent() {
            if (musicalInput != null)
                musicalInput.text = "";

            musicalChanges.Clear();

            if (uiManager != null)
                uiManager.ShowMusicalPending("Cleared");

            LogDebug("Musical content cleared");
        }

        public void ClearControlContent() {
            if (controlInput != null)
                controlInput.text = "";

            controlChanges.Clear();

            if (uiManager != null)
                uiManager.ShowControlPending("Cleared");

            LogDebug("Control content cleared");
        }

        /// <summary>
        /// Clear all content (both musical and control)
        /// </summary>
        public void ClearAllContent() {
            ClearMusicalContent();
            ClearControlContent();
        }

        // === PUBLIC DATA ACCESS ===

        /// <summary>
        /// Get current musical changes (for file manager)
        /// </summary>
        public List<MetronomeChange> GetMusicalChanges() => new List<MetronomeChange>(musicalChanges);

        /// <summary>
        /// Get current control changes (for file manager)
        /// </summary>
        public List<MetronomeChange> GetControlChanges() => new List<MetronomeChange>(controlChanges);

        /// <summary>
        /// Get all changes combined
        /// </summary>
        public List<MetronomeChange> GetAllChanges() {
            var allChanges = new List<MetronomeChange>();
            allChanges.AddRange(musicalChanges);
            allChanges.AddRange(controlChanges);
            return allChanges;
        }

        /// <summary>
        /// Get total number of parsed changes
        /// </summary>
        public int GetTotalChangeCount() => musicalChanges.Count + controlChanges.Count;

        /// <summary>
        /// Check if panel has any content to work with
        /// </summary>
        public bool HasParsedContent() => GetTotalChangeCount() > 0;

        // === INPUT FIELD ACCESS (for file manager) ===

        /// <summary>
        /// Set musical input text (for loading)
        /// </summary>
        public void SetMusicalInputText(string text) {
            if (musicalInput != null)
                musicalInput.text = text;
        }

        /// <summary>
        /// Set control input text (for loading)
        /// </summary>
        public void SetControlInputText(string text) {
            if (controlInput != null)
                controlInput.text = text;
        }

        /// <summary>
        /// Get musical input text
        /// </summary>
        public string GetMusicalInputText() => musicalInput?.text ?? "";

        /// <summary>
        /// Get control input text
        /// </summary>
        public string GetControlInputText() => controlInput?.text ?? "";

        // === VALIDATION METHODS ===

        /// <summary>
        /// Validate all current changes
        /// </summary>
        public (int errors, int warnings, int total) ValidateAllChanges() {
            int errors = 0, warnings = 0, total = 0;

            // Create temporary sequence for validation
            var sequence = ScriptableObject.CreateInstance<ChangeSequence>();
            foreach (var change in GetAllChanges())
                sequence.AddChange(change);

            // Use simple validation from streamlined ChangeSequence
            bool isValid = sequence.IsValid();

            if (!isValid) {
                errors = 1; // Simple error count for streamlined version
                total = 1;
            }

            return (errors, warnings, total);
        }

        // === DEBUG METHODS ===

        private void LogDebug(string message) {
            if (enableDebugLogs)
                Debug.Log($"[TrackPanel] {message}");
        }

        private void LogError(string message) {
            Debug.LogError($"[TrackPanel] ❌ {message}");
        }

        [ContextMenu("Debug Panel State")]
        public void DebugPanelState() {
            Debug.Log($"=== TRACK PANEL DEBUG ===");
            Debug.Log($"Musical changes: {musicalChanges.Count}");
            Debug.Log($"Control changes: {controlChanges.Count}");
            Debug.Log($"Total changes: {GetTotalChangeCount()}");
            Debug.Log($"Has content: {HasParsedContent()}");
            Debug.Log($"UI Manager: {(uiManager != null ? "✓" : "✗")}");
            Debug.Log($"File Manager: {(fileManager != null ? "✓" : "✗")}");
            Debug.Log($"Metronome Manager: {(metronomeManager != null ? "✓" : "✗")}");
        }
    }
}