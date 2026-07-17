using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using ChangeComposer.Data;
using ChangeComposer.Coordination;
using ChangeComposer.Parsing;
using ChangeComposer.Serialization;

/// <summary>
/// CompositionEditorController
/// Version: 2026-03-20 v1
///
/// PURPOSE:
///   Main controller for the editor scene. Drives all three track columns
///   and their SYNC sections. No metronome references — editor only reads
///   and writes JSON files. Metronome widgets in the scene are wired
///   independently for in-editor testing.
///
/// WIRING (one GameObject, all slots in Inspector):
///   Three track columns — each has:
///     trackEventsInput, trackEventsStatus, parseTrackButton, clearTrackButton
///     syncEventsInput,  syncEventsStatus,  parseSyncButton,  clearSyncButton
///     trackFilenameInput, saveTrackButton, loadTrackButton
///     syncFilenameInput,  saveSyncButton,  loadSyncButton
///
/// SAVE BEHAVIOUR:
///   Save Track  — saves that column's track events to its track filename
///   Save SYNC   — saves ALL SYNC events from all three columns combined
///                 into the coordination JSON, using the clicked column's
///                 sync filename. The coordination JSON also includes all
///                 three track references (built from track filenames).
///
/// LOAD BEHAVIOUR:
///   Load Track  — reads track JSON, populates textarea via FormatChange()
///   Load SYNC   — reads coordination JSON, distributes SYNC lines to the
///                 correct column based on referenceTrack field
///
/// FILE LOCATION:
///   Application.persistentDataPath (same location as before).
///   .json extension is added automatically if not present.
/// </summary>
public class CompositionEditorController : MonoBehaviour {

    // =========================================================
    // INSPECTOR — TRACK 1
    // =========================================================

    [Header("═══ TRACK 1 ═══")]
    [SerializeField] private InputField track1EventsInput;
    [SerializeField] private Text track1EventsStatus;
    [SerializeField] private Button track1ParseButton;
    [SerializeField] private Button track1ClearButton;

    [SerializeField] private InputField track1SyncInput;
    [SerializeField] private Text track1SyncStatus;
    [SerializeField] private Button track1ParseSyncButton;
    [SerializeField] private Button track1ClearSyncButton;

    [SerializeField] private InputField track1FilenameInput;
    [SerializeField] private Button track1SaveButton;
    [SerializeField] private Button track1LoadButton;

    [SerializeField] private InputField track1SyncFilenameInput;
    [SerializeField] private Button track1SaveSyncButton;
    [SerializeField] private Button track1LoadSyncButton;

    // =========================================================
    // INSPECTOR — TRACK 2
    // =========================================================

    [Header("═══ TRACK 2 ═══")]
    [SerializeField] private InputField track2EventsInput;
    [SerializeField] private Text track2EventsStatus;
    [SerializeField] private Button track2ParseButton;
    [SerializeField] private Button track2ClearButton;

    [SerializeField] private InputField track2SyncInput;
    [SerializeField] private Text track2SyncStatus;
    [SerializeField] private Button track2ParseSyncButton;
    [SerializeField] private Button track2ClearSyncButton;

    [SerializeField] private InputField track2FilenameInput;
    [SerializeField] private Button track2SaveButton;
    [SerializeField] private Button track2LoadButton;

    [SerializeField] private InputField track2SyncFilenameInput;
    [SerializeField] private Button track2SaveSyncButton;
    [SerializeField] private Button track2LoadSyncButton;

    // =========================================================
    // INSPECTOR — TRACK 3
    // =========================================================

    [Header("═══ TRACK 3 ═══")]
    [SerializeField] private InputField track3EventsInput;
    [SerializeField] private Text track3EventsStatus;
    [SerializeField] private Button track3ParseButton;
    [SerializeField] private Button track3ClearButton;

    [SerializeField] private InputField track3SyncInput;
    [SerializeField] private Text track3SyncStatus;
    [SerializeField] private Button track3ParseSyncButton;
    [SerializeField] private Button track3ClearSyncButton;

    [SerializeField] private InputField track3FilenameInput;
    [SerializeField] private Button track3SaveButton;
    [SerializeField] private Button track3LoadButton;

    [SerializeField] private InputField track3SyncFilenameInput;
    [SerializeField] private Button track3SaveSyncButton;
    [SerializeField] private Button track3LoadSyncButton;

    // =========================================================
    // INSPECTOR — SCENE NAVIGATION
    // =========================================================

    [Header("═══ SCENE NAVIGATION ═══")]
    [SerializeField] private Button performanceSceneButton;
    [SerializeField] private string performanceSceneName = "PerformanceScene";

    [Header("═══ DEBUG ═══")]
    [SerializeField] private bool debugLogging = true;

    // =========================================================
    // INTERNAL STATE
    // Parsed changes are cached after PARSE so Save can use them
    // without re-parsing. Reset on Clear.
    // =========================================================

    private List<MetronomeChange_v2> track1Changes = new List<MetronomeChange_v2>();
    private List<MetronomeChange_v2> track2Changes = new List<MetronomeChange_v2>();
    private List<MetronomeChange_v2> track3Changes = new List<MetronomeChange_v2>();

    private List<BeatLevelAlignment> track1SyncAlignments = new List<BeatLevelAlignment>();
    private List<BeatLevelAlignment> track2SyncAlignments = new List<BeatLevelAlignment>();
    private List<BeatLevelAlignment> track3SyncAlignments = new List<BeatLevelAlignment>();

    // =========================================================
    // UNITY LIFECYCLE
    // =========================================================

    private void Start() {
        WireButtons();
        Log("CompositionEditorController ready.");
    }

    // =========================================================
    // BUTTON WIRING
    // =========================================================

    private void WireButtons() {
        // Track 1
        track1ParseButton?.onClick.AddListener(() => ParseTrackEvents(1));
        track1ClearButton?.onClick.AddListener(() => ClearTrackEvents(1));
        track1ParseSyncButton?.onClick.AddListener(() => ParseSyncEvents(1));
        track1ClearSyncButton?.onClick.AddListener(() => ClearSyncEvents(1));
        track1SaveButton?.onClick.AddListener(() => SaveTrack(1));
        track1LoadButton?.onClick.AddListener(() => LoadTrack(1));
        track1SaveSyncButton?.onClick.AddListener(() => SaveSync(1));
        track1LoadSyncButton?.onClick.AddListener(() => LoadSync(1));

        // Track 2
        track2ParseButton?.onClick.AddListener(() => ParseTrackEvents(2));
        track2ClearButton?.onClick.AddListener(() => ClearTrackEvents(2));
        track2ParseSyncButton?.onClick.AddListener(() => ParseSyncEvents(2));
        track2ClearSyncButton?.onClick.AddListener(() => ClearSyncEvents(2));
        track2SaveButton?.onClick.AddListener(() => SaveTrack(2));
        track2LoadButton?.onClick.AddListener(() => LoadTrack(2));
        track2SaveSyncButton?.onClick.AddListener(() => SaveSync(2));
        track2LoadSyncButton?.onClick.AddListener(() => LoadSync(2));

        // Track 3
        track3ParseButton?.onClick.AddListener(() => ParseTrackEvents(3));
        track3ClearButton?.onClick.AddListener(() => ClearTrackEvents(3));
        track3ParseSyncButton?.onClick.AddListener(() => ParseSyncEvents(3));
        track3ClearSyncButton?.onClick.AddListener(() => ClearSyncEvents(3));
        track3SaveButton?.onClick.AddListener(() => SaveTrack(3));
        track3LoadButton?.onClick.AddListener(() => LoadTrack(3));
        track3SaveSyncButton?.onClick.AddListener(() => SaveSync(3));
        track3LoadSyncButton?.onClick.AddListener(() => LoadSync(3));

        // Scene navigation
        performanceSceneButton?.onClick.AddListener(GoToPerformanceScene);
    }

    // =========================================================
    // PARSE — TRACK EVENTS
    // =========================================================

    private void ParseTrackEvents(int track) {
        string text = GetTrackEventsInput(track)?.text ?? "";

        var result = TextParser_v2.ParseAll(text);

        // Filter to track events only (ignore any SYNC lines typed here)
        var changes = new List<MetronomeChange_v2>();
        foreach (var e in result.trackEvents) {
            if (e.isValid)
                changes.Add(e.change);
        }

        SetTrackChanges(track, changes);

        int total = result.trackEvents.Count;
        int success = changes.Count;
        int errors = result.errorCount + (total - success);

        if (success > 0 && errors == 0)
            SetTrackEventsStatus(track, $"✓ {success} event{Plural(success)} parsed");
        else if (success > 0)
            SetTrackEventsStatus(track, $"✓ {success} parsed  ✗ {errors} error{Plural(errors)}");
        else if (errors > 0)
            SetTrackEventsStatus(track, $"✗ {result.errorSummary ?? $"{errors} error{Plural(errors)}"}");
        else
            SetTrackEventsStatus(track, "No events found");

        Log($"Track {track} parse: {success} ok, {errors} errors");
    }

    // =========================================================
    // PARSE — SYNC EVENTS
    // =========================================================

    private void ParseSyncEvents(int track) {
        string text = GetSyncInput(track)?.text ?? "";

        var result = TextParser_v2.ParseAll(text);

        var alignments = new List<BeatLevelAlignment>();
        foreach (var e in result.syncEvents) {
            if (e.isValid)
                alignments.Add(e.alignment);
        }

        SetSyncAlignments(track, alignments);

        int success = alignments.Count;
        int errors = result.errorCount + (result.syncEvents.Count - success);

        if (success > 0 && errors == 0)
            SetSyncEventsStatus(track, $"✓ {success} SYNC event{Plural(success)} parsed");
        else if (success > 0)
            SetSyncEventsStatus(track, $"✓ {success} parsed  ✗ {errors} error{Plural(errors)}");
        else if (errors > 0)
            SetSyncEventsStatus(track, $"✗ {result.errorSummary ?? $"{errors} error{Plural(errors)}"}");
        else
            SetSyncEventsStatus(track, "No SYNC events found");

        Log($"Track {track} SYNC parse: {success} ok, {errors} errors");
    }

    // =========================================================
    // CLEAR
    // =========================================================

    private void ClearTrackEvents(int track) {
        var input = GetTrackEventsInput(track);
        if (input != null) input.text = "";
        SetTrackChanges(track, new List<MetronomeChange_v2>());
        SetTrackEventsStatus(track, "Cleared");
    }

    private void ClearSyncEvents(int track) {
        var input = GetSyncInput(track);
        if (input != null) input.text = "";
        SetSyncAlignments(track, new List<BeatLevelAlignment>());
        SetSyncEventsStatus(track, "Cleared");
    }

    // =========================================================
    // SAVE TRACK
    // =========================================================

    private void SaveTrack(int track) {
        string filename = GetTrackFilename(track);
        if (string.IsNullOrWhiteSpace(filename)) {
            SetTrackEventsStatus(track, "✗ Enter a filename first");
            return;
        }

        var changes = GetTrackChanges(track);
        if (changes.Count == 0) {
            SetTrackEventsStatus(track, "✗ Parse events first");
            return;
        }

        // Build a ChangeSequence_v2 from the cached changes
        var sequence = ScriptableObject.CreateInstance<ChangeSequence_v2>();
        sequence.title = Path.GetFileNameWithoutExtension(filename);
        sequence.changes = new List<MetronomeChange_v2>(changes);

        string json = CompositionJsonSerializer_v2.Save(sequence);
        if (json == null) {
            SetTrackEventsStatus(track, "✗ Serialization failed");
            return;
        }

        string path = BuildFilePath(filename);
        try {
            File.WriteAllText(path, json);
            SetTrackEventsStatus(track, $"✓ Saved to {Path.GetFileName(path)}");
            Log($"Track {track} saved: {path}");
        } catch (Exception ex) {
            SetTrackEventsStatus(track, $"✗ Save failed: {ex.Message}");
            LogError($"Track {track} save error: {ex.Message}");
        }
    }

    // =========================================================
    // LOAD TRACK
    // =========================================================

    private void LoadTrack(int track) {
        string filename = GetTrackFilename(track);
        if (string.IsNullOrWhiteSpace(filename)) {
            SetTrackEventsStatus(track, "✗ Enter a filename first");
            return;
        }

        string path = BuildFilePath(filename);
        if (!File.Exists(path)) {
            SetTrackEventsStatus(track, $"✗ File not found: {Path.GetFileName(path)}");
            return;
        }

        try {
            string json = File.ReadAllText(path);
            var sequence = CompositionJsonSerializer_v2.Load(json);

            if (sequence == null) {
                SetTrackEventsStatus(track, "✗ Failed to parse file");
                return;
            }

            // Populate textarea using round-trip formatter
            var lines = new List<string>();
            foreach (var change in sequence.GetSortedChanges())
                lines.Add(TextParser_v2.FormatChange(change));

            var input = GetTrackEventsInput(track);
            if (input != null)
                input.text = string.Join("\n", lines);

            // Cache the loaded changes
            SetTrackChanges(track, sequence.changes);
            SetTrackEventsStatus(track,
                $"✓ Loaded {sequence.changes.Count} event{Plural(sequence.changes.Count)}");

            Log($"Track {track} loaded: {path}");
        } catch (Exception ex) {
            SetTrackEventsStatus(track, $"✗ Load failed: {ex.Message}");
            LogError($"Track {track} load error: {ex.Message}");
        }
    }

    // =========================================================
    // SAVE SYNC (coordination JSON)
    // Collects all SYNC events from all three columns and writes
    // one coordination JSON using the clicked column's filename.
    // =========================================================

    private void SaveSync(int fromTrack) {
        string filename = GetSyncFilename(fromTrack);
        if (string.IsNullOrWhiteSpace(filename)) {
            SetSyncEventsStatus(fromTrack, "✗ Enter a SYNC filename first");
            return;
        }

        // Collect all alignments across all three columns
        var allAlignments = new List<BeatLevelAlignment>();
        allAlignments.AddRange(track1SyncAlignments);
        allAlignments.AddRange(track2SyncAlignments);
        allAlignments.AddRange(track3SyncAlignments);

        if (allAlignments.Count == 0) {
            SetSyncEventsStatus(fromTrack, "✗ Parse SYNC events first");
            return;
        }

        // Build track references from the track filename fields
        var tracks = new List<TrackReference> {
            BuildTrackReference("track1", track1FilenameInput?.text, "metronome1"),
            BuildTrackReference("track2", track2FilenameInput?.text, "metronome2"),
            BuildTrackReference("track3", track3FilenameInput?.text, "metronome3")
        };

        var coord = new CompositionCoordination {
            compositionTitle = Path.GetFileNameWithoutExtension(filename),
            tracks = tracks,
            beatLevelAlignments = allAlignments,
            startAll = new StartAllConfig {
                enabled = true,
                atMeasure = 1,
                delaySeconds = 0.5f
            }
        };

        string json = JsonUtility.ToJson(coord, true);
        string path = BuildFilePath(filename);

        try {
            File.WriteAllText(path, json);
            int total = allAlignments.Count;
            SetSyncEventsStatus(fromTrack,
                $"✓ Saved {total} alignment{Plural(total)} to {Path.GetFileName(path)}");
            Log($"Coordination JSON saved: {path} ({total} alignments)");
        } catch (Exception ex) {
            SetSyncEventsStatus(fromTrack, $"✗ Save failed: {ex.Message}");
            LogError($"Sync save error: {ex.Message}");
        }
    }

    // =========================================================
    // LOAD SYNC (coordination JSON)
    // Reads coordination JSON and distributes SYNC lines back to
    // the correct column based on referenceTrack.
    // =========================================================

    private void LoadSync(int fromTrack) {
        string filename = GetSyncFilename(fromTrack);
        if (string.IsNullOrWhiteSpace(filename)) {
            SetSyncEventsStatus(fromTrack, "✗ Enter a SYNC filename first");
            return;
        }

        string path = BuildFilePath(filename);
        if (!File.Exists(path)) {
            SetSyncEventsStatus(fromTrack, $"✗ File not found: {Path.GetFileName(path)}");
            return;
        }

        try {
            string json = File.ReadAllText(path);
            var coord = CompositionCoordination.FromJSON(json);

            if (coord == null) {
                SetSyncEventsStatus(fromTrack, "✗ Failed to parse coordination JSON");
                return;
            }

            // Clear existing sync state
            track1SyncAlignments.Clear();
            track2SyncAlignments.Clear();
            track3SyncAlignments.Clear();

            var t1Lines = new List<string>();
            var t2Lines = new List<string>();
            var t3Lines = new List<string>();

            if (coord.beatLevelAlignments != null) {
                foreach (var alignment in coord.beatLevelAlignments) {
                    string line = TextParser_v2.FormatAlignment(alignment);

                    // Distribute to column based on referenceTrack
                    switch (alignment.referenceTrack) {
                        case "track1":
                            track1SyncAlignments.Add(alignment);
                            t1Lines.Add(line);
                            break;
                        case "track2":
                            track2SyncAlignments.Add(alignment);
                            t2Lines.Add(line);
                            break;
                        case "track3":
                            track3SyncAlignments.Add(alignment);
                            t3Lines.Add(line);
                            break;
                        default:
                            LogError($"Unknown referenceTrack '{alignment.referenceTrack}' in coordination JSON");
                            break;
                    }
                }
            }

            // Populate SYNC textareas
            if (track1SyncInput != null) track1SyncInput.text = string.Join("\n", t1Lines);
            if (track2SyncInput != null) track2SyncInput.text = string.Join("\n", t2Lines);
            if (track3SyncInput != null) track3SyncInput.text = string.Join("\n", t3Lines);

            // Update status on the column that triggered the load
            int total = (coord.beatLevelAlignments?.Count ?? 0);
            SetSyncEventsStatus(fromTrack,
                $"✓ Loaded {total} alignment{Plural(total)}");

            // Also update status on other columns if they received alignments
            if (t1Lines.Count > 0 && fromTrack != 1)
                SetSyncEventsStatus(1, $"✓ {t1Lines.Count} loaded from file");
            if (t2Lines.Count > 0 && fromTrack != 2)
                SetSyncEventsStatus(2, $"✓ {t2Lines.Count} loaded from file");
            if (t3Lines.Count > 0 && fromTrack != 3)
                SetSyncEventsStatus(3, $"✓ {t3Lines.Count} loaded from file");

            Log($"Coordination JSON loaded: {path} ({total} alignments)");
        } catch (Exception ex) {
            SetSyncEventsStatus(fromTrack, $"✗ Load failed: {ex.Message}");
            LogError($"Sync load error: {ex.Message}");
        }
    }

    // =========================================================
    // SCENE NAVIGATION
    // =========================================================

    private void GoToPerformanceScene() {
        if (!string.IsNullOrEmpty(performanceSceneName))
            UnityEngine.SceneManagement.SceneManager.LoadScene(performanceSceneName);
        else
            LogError("Performance scene name not set in inspector.");
    }

    // =========================================================
    // HELPERS — per-track field accessors
    // =========================================================

    private InputField GetTrackEventsInput(int track) {
        switch (track) {
            case 1: return track1EventsInput;
            case 2: return track2EventsInput;
            case 3: return track3EventsInput;
            default: return null;
        }
    }

    private InputField GetSyncInput(int track) {
        switch (track) {
            case 1: return track1SyncInput;
            case 2: return track2SyncInput;
            case 3: return track3SyncInput;
            default: return null;
        }
    }

    private string GetTrackFilename(int track) {
        switch (track) {
            case 1: return track1FilenameInput?.text?.Trim() ?? "";
            case 2: return track2FilenameInput?.text?.Trim() ?? "";
            case 3: return track3FilenameInput?.text?.Trim() ?? "";
            default: return "";
        }
    }

    private string GetSyncFilename(int track) {
        switch (track) {
            case 1: return track1SyncFilenameInput?.text?.Trim() ?? "";
            case 2: return track2SyncFilenameInput?.text?.Trim() ?? "";
            case 3: return track3SyncFilenameInput?.text?.Trim() ?? "";
            default: return "";
        }
    }

    private void SetTrackEventsStatus(int track, string message) {
        switch (track) {
            case 1: if (track1EventsStatus != null) track1EventsStatus.text = message; break;
            case 2: if (track2EventsStatus != null) track2EventsStatus.text = message; break;
            case 3: if (track3EventsStatus != null) track3EventsStatus.text = message; break;
        }
    }

    private void SetSyncEventsStatus(int track, string message) {
        switch (track) {
            case 1: if (track1SyncStatus != null) track1SyncStatus.text = message; break;
            case 2: if (track2SyncStatus != null) track2SyncStatus.text = message; break;
            case 3: if (track3SyncStatus != null) track3SyncStatus.text = message; break;
        }
    }

    private List<MetronomeChange_v2> GetTrackChanges(int track) {
        switch (track) {
            case 1: return track1Changes;
            case 2: return track2Changes;
            case 3: return track3Changes;
            default: return new List<MetronomeChange_v2>();
        }
    }

    private void SetTrackChanges(int track, List<MetronomeChange_v2> changes) {
        switch (track) {
            case 1: track1Changes = changes; break;
            case 2: track2Changes = changes; break;
            case 3: track3Changes = changes; break;
        }
    }

    private void SetSyncAlignments(int track, List<BeatLevelAlignment> alignments) {
        switch (track) {
            case 1: track1SyncAlignments = alignments; break;
            case 2: track2SyncAlignments = alignments; break;
            case 3: track3SyncAlignments = alignments; break;
        }
    }

    // =========================================================
    // HELPERS — file and data utilities
    // =========================================================

    private static string BuildFilePath(string filename) {
        string name = filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? filename
            : filename + ".json";
        return Path.Combine(Application.persistentDataPath, name);
    }

    private static TrackReference BuildTrackReference(
            string id, string filenameField, string metronomeRef) {
        string jsonFile = string.IsNullOrWhiteSpace(filenameField)
            ? $"{id}.json"
            : filenameField.Trim().EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? filenameField.Trim()
                : filenameField.Trim() + ".json";

        return new TrackReference {
            id = id,
            jsonFile = jsonFile,
            metronomeReference = metronomeRef
        };
    }

    private static string Plural(int count) => count == 1 ? "" : "s";

    // =========================================================
    // LOGGING
    // =========================================================

    private void Log(string message) {
        if (debugLogging)
            Debug.Log($"[EditorController] {message}");
    }

    private void LogError(string message) {
        Debug.LogError($"[EditorController] ❌ {message}");
    }
}