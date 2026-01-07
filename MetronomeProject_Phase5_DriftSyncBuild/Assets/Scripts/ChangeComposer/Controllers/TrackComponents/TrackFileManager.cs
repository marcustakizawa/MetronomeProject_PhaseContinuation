using UnityEngine;
using UnityEngine.UI;
using ChangeComposer.Data;
using System.Collections.Generic;

namespace ChangeComposer.Controllers {
    /// <summary>
    /// Handles file save/load operations for track panels
    /// Separate component for easier debugging and reusability
    /// </summary>
    public class TrackFileManager : MonoBehaviour {
        [Header("UI References")]
        [SerializeField] private InputField filenameInput;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;

        [Header("Target Input Fields")]
        [SerializeField] private InputField musicalInput;
        [SerializeField] private InputField controlInput;

        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool prettyPrintJSON = true;

        // Reference to the track panel for getting current changes
        private TrackPanel trackPanel;

        private void Start() {
            SetupButtons();
            trackPanel = GetComponent<TrackPanel>();

            if (trackPanel == null) {
                Debug.LogWarning("TrackFileManager: No TrackPanel found on same GameObject");
            }

            LogDebug("TrackFileManager initialized");
        }

        private void SetupButtons() {
            if (saveButton != null)
                saveButton.onClick.AddListener(SaveTrack);

            if (loadButton != null)
                loadButton.onClick.AddListener(LoadTrack);
        }

        /// <summary>
        /// Save current track changes to JSON file
        /// </summary>
        public void SaveTrack() {
            string filename = GetFilename();
            if (string.IsNullOrWhiteSpace(filename)) {
                LogError("No filename provided for save");
                return;
            }

            if (trackPanel == null) {
                LogError("No TrackPanel component found");
                return;
            }

            try {
                // Get current changes from track panel
                var musicalChanges = trackPanel.GetMusicalChanges();
                var controlChanges = trackPanel.GetControlChanges();

                if (musicalChanges.Count == 0 && controlChanges.Count == 0) {
                    LogError("No changes to save - parse content first");
                    return;
                }

                // Create sequence
                var sequence = CreateSequenceFromChanges(filename, musicalChanges, controlChanges);

                // Save to file
                string filePath = SaveSequenceToFile(sequence, filename);

                LogDebug($"✅ Successfully saved track");
                LogDebug($"📁 File: {filename}.json");
                LogDebug($"📍 Path: {filePath}");
                LogDebug($"💾 Changes: {musicalChanges.Count} musical, {controlChanges.Count} control");
            } catch (System.Exception ex) {
                LogError($"Save failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Load track from JSON file into input fields
        /// </summary>
        public void LoadTrack() {
            string filename = GetFilename();
            if (string.IsNullOrWhiteSpace(filename)) {
                LogError("No filename provided for load");
                return;
            }

            try {
                // Load sequence from file
                var sequence = LoadSequenceFromFile(filename);

                if (sequence == null) {
                    LogError("Failed to load sequence from file");
                    return;
                }

                // Populate input fields
                PopulateInputFields(sequence);

                // Trigger parsing on the track panel
                if (trackPanel != null) {
                    trackPanel.ParseMusicalContent();
                    trackPanel.ParseControlContent();
                }

                LogDebug($"✅ Successfully loaded track");
                LogDebug($"📄 Title: {sequence.title}");
                LogDebug($"🎵 Changes: {sequence.changes.Count}");
                LogDebug($"⚡ Initial: {sequence.initialBpm} BPM, {sequence.initialBeatsPerMeasure}/4");
            } catch (System.Exception ex) {
                LogError($"Load failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Create a ChangeSequence from the provided changes
        /// </summary>
        private ChangeSequence CreateSequenceFromChanges(string title, List<MetronomeChange> musicalChanges, List<MetronomeChange> controlChanges) {
            var sequence = ScriptableObject.CreateInstance<ChangeSequence>();

            // Set basic info (only what streamlined ChangeSequence has)
            sequence.title = title;

            // Add all changes
            foreach (var change in musicalChanges)
                sequence.AddChange(change);

            foreach (var change in controlChanges)
                sequence.AddChange(change);

            return sequence;
        }

        /// <summary>
        /// Save sequence to JSON file
        /// </summary>
        private string SaveSequenceToFile(ChangeSequence sequence, string filename) {
            string json = sequence.ToJSON();
            string fileName = filename + ".json";
            string path = Application.persistentDataPath + "/" + fileName;

            System.IO.File.WriteAllText(path, json);

            return path;
        }

        /// <summary>
        /// Load sequence from JSON file
        /// </summary>
        private ChangeSequence LoadSequenceFromFile(string filename) {
            string fileName = filename + ".json";
            string path = Application.persistentDataPath + "/" + fileName;

            if (!System.IO.File.Exists(path)) {
                LogError($"File not found: {fileName}");
                return null;
            }

            string json = System.IO.File.ReadAllText(path);

            if (string.IsNullOrWhiteSpace(json)) {
                LogError("File is empty or unreadable");
                return null;
            }

            var sequence = ChangeSequence.FromJSON(json);

            if (sequence == null) {
                LogError("Failed to parse JSON content");
                return null;
            }

            return sequence;
        }

        /// <summary>
        /// Populate input fields with sequence content
        /// </summary>
        private void PopulateInputFields(ChangeSequence sequence) {
            var musicalLines = new List<string>();
            var controlLines = new List<string>();

            // Add headers (using only available properties)
            musicalLines.Add($"// {sequence.title} - Musical Events");
            musicalLines.Add($"// Loaded from file");
            musicalLines.Add("");

            controlLines.Add($"// {sequence.title} - Control Events");
            controlLines.Add($"// Loaded from file");
            controlLines.Add("");

            // Sort and categorize changes
            var sortedChanges = sequence.GetSortedChanges();

            foreach (var change in sortedChanges) {
                switch (change.type) {
                    case MetronomeChange.ChangeType.Tempo:
                    case MetronomeChange.ChangeType.TimeSignature:
                    case MetronomeChange.ChangeType.Both:
                        string musicalText = MusicalParser.FormatMusical(change);
                        if (!string.IsNullOrEmpty(musicalText))
                            musicalLines.Add(musicalText);
                        break;

                    default: // All control events
                        string controlText = ControlParser.FormatControl(change);
                        if (!string.IsNullOrEmpty(controlText))
                            controlLines.Add(controlText);
                        break;
                }
            }

            // Set input field content
            if (musicalInput != null)
                musicalInput.text = string.Join("\n", musicalLines);

            if (controlInput != null)
                controlInput.text = string.Join("\n", controlLines);
        }

        /// <summary>
        /// Get filename from input field
        /// </summary>
        private string GetFilename() {
            return filenameInput?.text?.Trim() ?? "";
        }

        /// <summary>
        /// Debug logging with toggle
        /// </summary>
        private void LogDebug(string message) {
            if (enableDebugLogs)
                Debug.Log($"[TrackFileManager] {message}");
        }

        /// <summary>
        /// Error logging
        /// </summary>
        private void LogError(string message) {
            Debug.LogError($"[TrackFileManager] ❌ {message}");
        }

        // === PUBLIC UTILITY METHODS ===

        /// <summary>
        /// Check if a file exists
        /// </summary>
        public bool FileExists(string filename) {
            if (string.IsNullOrWhiteSpace(filename)) return false;

            string fileName = filename + ".json";
            string path = Application.persistentDataPath + "/" + fileName;
            return System.IO.File.Exists(path);
        }

        /// <summary>
        /// Get list of available save files
        /// </summary>
        public string[] GetAvailableFiles() {
            try {
                string[] files = System.IO.Directory.GetFiles(Application.persistentDataPath, "*.json");
                var fileNames = new List<string>();

                foreach (string file in files) {
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(file);
                    fileNames.Add(fileName);
                }

                return fileNames.ToArray();
            } catch {
                return new string[0];
            }
        }

        /// <summary>
        /// Show current save path in console
        /// </summary>
        [ContextMenu("Show Save Path")]
        public void ShowSavePath() {
            Debug.Log($"💾 Save Path: {Application.persistentDataPath}");
        }

        /// <summary>
        /// List all available files in console
        /// </summary>
        [ContextMenu("List Available Files")]
        public void ListAvailableFiles() {
            var files = GetAvailableFiles();
            Debug.Log($"📁 Found {files.Length} files:");
            foreach (string file in files) {
                Debug.Log($"  - {file}.json");
            }
        }
    }
}