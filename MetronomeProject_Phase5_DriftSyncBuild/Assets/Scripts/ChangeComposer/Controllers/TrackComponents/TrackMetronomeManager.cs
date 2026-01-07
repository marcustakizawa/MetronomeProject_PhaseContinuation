using UnityEngine;
using UnityEngine.UI;
using ChangeComposer.Data;
using System.Collections.Generic;

namespace ChangeComposer.Controllers {
    /// <summary>
    /// Handles metronome integration for a track panel
    /// Manages target selection, loading changes, and coordination
    /// </summary>
    public class TrackMetronomeManager : MonoBehaviour {
        [Header("UI References")]
        [SerializeField] private Dropdown targetMetronomeDropdown;
        [SerializeField] private Button loadToMetronomeButton;

        [Header("Metronome References")]
        [SerializeField] private PrecisionMetronome metronome1;
        [SerializeField] private PrecisionMetronome metronome2;
        [SerializeField] private PrecisionMetronome metronome3;

        [Header("Settings")]
        [SerializeField] private bool clearExistingChanges = true;
        [SerializeField] private bool autoValidateBeforeLoad = true;
        [SerializeField] private bool enableDebugLogs = true;

        // Component references
        private TrackPanel trackPanel;
        private TrackUIManager uiManager;

        // Current target
        private PrecisionMetronome currentTarget;

        private void Start() {
            SetupDropdown();
            SetupButtons();
            FindComponents();
            UpdateTargetMetronome();

            LogDebug("TrackMetronomeManager initialized");
        }

        private void SetupDropdown() {
            if (targetMetronomeDropdown != null) {
                targetMetronomeDropdown.ClearOptions();
                targetMetronomeDropdown.AddOptions(new List<string>
                {
                    "Metronome 1",
                    "Metronome 2",
                    "Metronome 3"
                });
                targetMetronomeDropdown.value = 0;
                targetMetronomeDropdown.onValueChanged.AddListener(OnDropdownChanged);
            }
        }

        private void SetupButtons() {
            if (loadToMetronomeButton != null)
                loadToMetronomeButton.onClick.AddListener(LoadToMetronome);
        }

        private void FindComponents() {
            trackPanel = GetComponent<TrackPanel>();
            uiManager = GetComponent<TrackUIManager>();

            if (trackPanel == null)
                Debug.LogWarning("TrackMetronomeManager: No TrackPanel found");
        }

        private void OnDropdownChanged(int value) {
            UpdateTargetMetronome();

            if (uiManager != null)
                uiManager.UpdateUI();
        }

        private void UpdateTargetMetronome() {
            if (targetMetronomeDropdown == null) return;

            int selectedIndex = targetMetronomeDropdown.value;

            switch (selectedIndex) {
                case 0: currentTarget = metronome1; break;
                case 1: currentTarget = metronome2; break;
                case 2: currentTarget = metronome3; break;
                default: currentTarget = null; break;
            }

            LogDebug($"Target updated to: {GetTargetName()}");
        }

        /// <summary>
        /// Load all parsed changes to the selected metronome
        /// </summary>
        public void LoadToMetronome() {
            if (currentTarget == null) {
                LogError("No target metronome selected");
                return;
            }

            if (trackPanel == null) {
                LogError("No TrackPanel component found");
                return;
            }

            var allChanges = trackPanel.GetAllChanges();

            if (allChanges.Count == 0) {
                LogError("No changes to load - parse content first");
                return;
            }

            try {
                // Optional validation before loading
                if (autoValidateBeforeLoad) {
                    var validation = trackPanel.ValidateAllChanges();
                    if (validation.errors > 0) {
                        LogError($"Cannot load: {validation.errors} validation errors found");
                        return;
                    }
                }

                // Clear existing changes if requested
                if (clearExistingChanges) {
                    currentTarget.ClearPendingChanges();
                    LogDebug("Cleared existing pending changes");
                }

                // Load all changes
                int loadedCount = 0;
                foreach (var change in allChanges) {
                    try {
                        currentTarget.ScheduleChange(change);
                        loadedCount++;
                    } catch (System.Exception ex) {
                        LogError($"Failed to schedule change at M{change.targetMeasure}: {ex.Message}");
                    }
                }

                LogDebug($"✅ Successfully loaded {loadedCount}/{allChanges.Count} changes to {GetTargetName()}");

                // Show success feedback
                if (uiManager != null)
                    uiManager.ShowTemporaryMessage($"Loaded {loadedCount} changes to {GetTargetName()}");

                // Log pending changes for verification
                LogPendingChanges();
            } catch (System.Exception ex) {
                LogError($"Load to metronome failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get changes currently pending in the target metronome
        /// </summary>
        public List<MetronomeChange> GetPendingChanges() {
            if (currentTarget == null) return new List<MetronomeChange>();
            return currentTarget.GetPendingChanges();
        }

        /// <summary>
        /// Clear all pending changes from target metronome
        /// </summary>
        public void ClearPendingChanges() {
            if (currentTarget == null) {
                LogError("No target metronome selected");
                return;
            }

            currentTarget.ClearPendingChanges();
            LogDebug($"Cleared pending changes from {GetTargetName()}");
        }

        /// <summary>
        /// Check if target metronome is currently playing
        /// </summary>
        public bool IsTargetPlaying() {
            return currentTarget != null && currentTarget.IsPlaying;
        }

        /// <summary>
        /// Get current target metronome status
        /// </summary>
        public string GetTargetStatus() {
            if (currentTarget == null) return "No Target";

            string status = $"{GetTargetName()}: ";
            status += $"M{currentTarget.CurrentMeasure} ";
            status += $"{currentTarget.Bpm:F0}BPM ";
            status += currentTarget.IsPlaying ? "Playing" : "Stopped";

            return status;
        }

        // === PUBLIC CONFIGURATION ===

        /// <summary>
        /// Set metronome references (useful for dynamic setup)
        /// </summary>
        public void SetMetronomes(PrecisionMetronome metro1, PrecisionMetronome metro2, PrecisionMetronome metro3) {
            metronome1 = metro1;
            metronome2 = metro2;
            metronome3 = metro3;
            UpdateTargetMetronome();
        }

        /// <summary>
        /// Set specific target by index
        /// </summary>
        public void SetTarget(int metronomeIndex) {
            if (targetMetronomeDropdown != null && metronomeIndex >= 0 && metronomeIndex < 3) {
                targetMetronomeDropdown.value = metronomeIndex;
                UpdateTargetMetronome();
            }
        }

        /// <summary>
        /// Check if we have a valid target metronome
        /// </summary>
        public bool HasTargetMetronome() => currentTarget != null;

        /// <summary>
        /// Get current target metronome
        /// </summary>
        public PrecisionMetronome GetTargetMetronome() => currentTarget;

        // === UTILITY METHODS ===

        private string GetTargetName() {
            if (currentTarget == null) return "None";

            if (currentTarget == metronome1) return "Metronome 1";
            if (currentTarget == metronome2) return "Metronome 2";
            if (currentTarget == metronome3) return "Metronome 3";

            return "Unknown";
        }

        private void LogPendingChanges() {
            if (currentTarget == null) return;

            var pending = currentTarget.GetPendingChanges();
            LogDebug($"Pending changes in {GetTargetName()}:");

            if (pending.Count == 0) {
                LogDebug("  (none)");
                return;
            }

            foreach (var change in pending) {
                LogDebug($"  {change}");
            }
        }

        private void LogDebug(string message) {
            if (enableDebugLogs)
                Debug.Log($"[TrackMetronomeManager] {message}");
        }

        private void LogError(string message) {
            Debug.LogError($"[TrackMetronomeManager] ❌ {message}");
        }

        // === DEBUG METHODS ===

        [ContextMenu("Debug Metronome State")]
        public void DebugMetronomeState() {
            Debug.Log($"=== METRONOME MANAGER DEBUG ===");
            Debug.Log($"Current target: {GetTargetName()}");
            Debug.Log($"Target status: {GetTargetStatus()}");
            Debug.Log($"Has target: {HasTargetMetronome()}");
            Debug.Log($"Target playing: {IsTargetPlaying()}");
            Debug.Log($"Pending changes: {GetPendingChanges().Count}");
        }

        [ContextMenu("Test Load to All Metronomes")]
        public void TestLoadToAllMetronomes() {
            if (trackPanel == null || !trackPanel.HasParsedContent()) {
                LogError("No content to test with");
                return;
            }

            var allChanges = trackPanel.GetAllChanges();
            LogDebug($"Testing load of {allChanges.Count} changes to all metronomes...");

            // Test load to each metronome
            for (int i = 0; i < 3; i++) {
                SetTarget(i);
                LoadToMetronome();
            }

            LogDebug("Test load to all metronomes complete");
        }
    }
}