using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ChangeComposer.Data;
using ChangeComposer.Coordination;
using ChangeComposer.Indexing;

/// <summary>
/// CompositionCoordinator_v2 - JSON-Driven Beat-Level Coordination
/// Version: 2026-03-31 v3 (Added stopAll SYNC action)
///
/// ALL FOUR BUGS FIXED:
///   Bug 1 (Step 4): LoadTrack() dispatches all change types via ScheduleChange().
///                   targetBeat < 1 fixup applied on load.
///   Bug 2 (Step 2): Hardcoded 2-metronome limit — metronome3/scheduler3 added.
///   Bug 3 (Step 2): Event subscription leak — delegates stored and unsubscribed explicitly.
///   Bug 4 (Step 3): Pickup beat computed internally from lockAtMeasure/lockAtBeat
///                   via CompositionIndex. JSON authors specify lock point only.
///
/// POST-STEP-5 FIX:
///   LoadTrack() now uses ChangeSequence_v2.FromJSON() instead of ChangeSequence.FromJSON(),
///   so the loaded sequence holds MetronomeChange_v2 objects throughout.
///
/// PURPOSE:
///   Loads a coordination JSON, loads each track's tempo JSON, schedules changes
///   into the appropriate metronome schedulers, and executes beat-level alignments.
///
/// SETUP IN INSPECTOR:
///   1. Assign coordinationJsonFile
///   2. Assign trackJsonBindings[] to match track IDs in coordination JSON
///   3. Assign metronome1/2/3 (PrecisionMetronome_v5_BeatLevel)
///   4. Assign scheduler1/2/3 (MetronomeScheduler_v2)
///   5. Press Play, then click "Load and Start"
/// </summary>
public class CompositionCoordinator_v2 : MonoBehaviour {

    // =========================================================
    // INSPECTOR FIELDS
    // =========================================================

    [Header("═══ COORDINATION JSON ═══")]
    [Tooltip("The coordination JSON describing tracks and alignments")]
    [SerializeField] private TextAsset coordinationJsonFile;

    [Header("═══ TRACK JSON FILES ═══")]
    [Tooltip("Map track IDs to their tempo JSON TextAssets")]
    [SerializeField] private List<TrackJsonBinding> trackJsonBindings;

    [Header("═══ METRONOMES (v5 BeatLevel) ═══")]
    [SerializeField] private PrecisionMetronome_v5_BeatLevel metronome1;
    [SerializeField] private PrecisionMetronome_v5_BeatLevel metronome2;
    [SerializeField] private PrecisionMetronome_v5_BeatLevel metronome3;

    [Header("═══ SCHEDULERS (v2) ═══")]
    [SerializeField] private MetronomeScheduler_v2 scheduler1;
    [SerializeField] private MetronomeScheduler_v2 scheduler2;
    [SerializeField] private MetronomeScheduler_v2 scheduler3;

    [Header("═══ DEBUG ═══")]
    [SerializeField] private bool debugLogging = true;
    [TextArea(6, 12)]
    [SerializeField] private string statusLog = "Not loaded.";

    // =========================================================
    // PRIVATE STATE
    // =========================================================

    private CompositionCoordination coordination;
    private Dictionary<string, PrecisionMetronome_v5_BeatLevel> metronomeMap;
    private Dictionary<string, MetronomeScheduler_v2> schedulerMap;
    private Dictionary<string, TextAsset> trackJsonMap;
    private Dictionary<string, ChangeSequence_v2> sequenceMap;
    private Dictionary<string, CompositionIndex> indexMap;
    private List<AlignmentTrigger> alignmentTriggers;
    private HashSet<int> firedAlignments = new HashSet<int>();
    private Dictionary<string, Action<int>> beatEventHandlers = new Dictionary<string, Action<int>>();
    private bool isLoaded = false;

    // =========================================================
    // ALIGNMENT TRIGGER
    // =========================================================

    private struct AlignmentTrigger {
        public int triggerMeasure;
        public int triggerBeat;
        public bool isAtLockPoint;
    }

    // =========================================================
    // UNITY LIFECYCLE
    // =========================================================

    private void Start() {
        Log("CompositionCoordinator_v2 ready. Call LoadAndStart() or click the button.");
    }

    private void OnDestroy() {
        UnsubscribeAll();
    }

    // =========================================================
    // PUBLIC API
    // =========================================================

    [ContextMenu("▶ Load and Start")]
    public void LoadAndStart() {
        ClearLog();
        Log("═══ COMPOSITION COORDINATOR v2 ═══");
        Log($"Loading: {(coordinationJsonFile ? coordinationJsonFile.name : "⚠ No file assigned")}");

        UnsubscribeAll();
        firedAlignments.Clear();

        if (!ValidateInspectorAssignments()) return;
        if (!LoadCoordinationJson()) return;
        if (!BuildMaps()) return;
        if (!LoadAllTracks()) return;

        BuildCompositionIndexes();
        SetupBeatAlignments();

        if (coordination.startAll != null && coordination.startAll.enabled)
            StartCoroutine(StartAllWithDelay(coordination.startAll));
        else
            StartAllMetronomes(coordination.startAll?.atMeasure ?? 1);

        isLoaded = true;
        Log("");
        Log("✅ Composition loaded and running!");
        Log($"Title: {coordination.compositionTitle}");
        Log($"Tracks: {coordination.tracks?.Count ?? 0}");
        Log($"Beat alignments: {coordination.beatLevelAlignments?.Count ?? 0}");
    }

    [ContextMenu("■ Stop All")]
    public void StopAll() {
        UnsubscribeAll();
        metronome1?.StopMetronome();
        metronome2?.StopMetronome();
        metronome3?.StopMetronome();
        isLoaded = false;
        Log("■ Stopped all metronomes.");
    }

    [ContextMenu("↺ Reset")]
    public void Reset() {
        StopAll();

        ResetMetronomeAndScheduler(metronome1, scheduler1);
        ResetMetronomeAndScheduler(metronome2, scheduler2);
        ResetMetronomeAndScheduler(metronome3, scheduler3);

        firedAlignments.Clear();
        coordination = null;
        sequenceMap = null;
        indexMap = null;
        alignmentTriggers = null;
        Log("↺ Reset complete. Ready to load.");
    }

    // =========================================================
    // STEP 1: LOAD COORDINATION JSON
    // =========================================================

    private bool LoadCoordinationJson() {
        try {
            coordination = CompositionCoordination.FromJSON(coordinationJsonFile.text);

            if (coordination == null) {
                LogError("Failed to parse coordination JSON (null result).");
                return false;
            }

            Log($"✔ Coordination JSON parsed: '{coordination.compositionTitle}'");
            return true;
        } catch (Exception ex) {
            LogError($"Exception parsing coordination JSON: {ex.Message}");
            return false;
        }
    }

    // =========================================================
    // STEP 2: BUILD MAPS
    // =========================================================

    private bool BuildMaps() {
        metronomeMap = new Dictionary<string, PrecisionMetronome_v5_BeatLevel>();
        schedulerMap = new Dictionary<string, MetronomeScheduler_v2>();
        trackJsonMap = new Dictionary<string, TextAsset>();
        sequenceMap = new Dictionary<string, ChangeSequence_v2>();

        if (trackJsonBindings != null) {
            foreach (var binding in trackJsonBindings) {
                if (binding.trackId != null && binding.jsonFile != null)
                    trackJsonMap[binding.trackId] = binding.jsonFile;
            }
        }

        if (coordination.tracks == null) {
            LogError("Coordination JSON has no 'tracks' array.");
            return false;
        }

        foreach (var track in coordination.tracks) {
            PrecisionMetronome_v5_BeatLevel metro = null;
            MetronomeScheduler_v2 sched = null;

            switch (track.metronomeReference) {
                case "metronome1": metro = metronome1; sched = scheduler1; break;
                case "metronome2": metro = metronome2; sched = scheduler2; break;
                case "metronome3": metro = metronome3; sched = scheduler3; break;
                default:
                    LogError($"Unknown metronomeReference '{track.metronomeReference}' for " +
                             $"track '{track.id}'. Supported: metronome1, metronome2, metronome3.");
                    return false;
            }

            if (metro == null) {
                LogError($"Inspector slot '{track.metronomeReference}' is not assigned " +
                         $"(needed by track '{track.id}').");
                return false;
            }

            metronomeMap[track.id] = metro;
            schedulerMap[track.id] = sched;
            Log($"  Mapped: '{track.id}' → {track.metronomeReference}");
        }

        return true;
    }

    // =========================================================
    // STEP 3: LOAD TRACK JSONS AND SCHEDULE CHANGES
    // =========================================================

    private bool LoadAllTracks() {
        Log("Loading track change sequences...");

        foreach (var track in coordination.tracks) {
            if (!LoadTrack(track)) return false;
        }

        return true;
    }

    private bool LoadTrack(TrackReference track) {
        TextAsset jsonAsset = null;

        if (trackJsonMap.TryGetValue(track.id, out jsonAsset) && jsonAsset != null) {
            // found by track ID
        } else if (trackJsonBindings != null) {
            foreach (var binding in trackJsonBindings) {
                if (binding.jsonFile != null &&
                    (binding.jsonFile.name == track.jsonFile ||
                     binding.jsonFile.name == System.IO.Path.GetFileNameWithoutExtension(track.jsonFile))) {
                    jsonAsset = binding.jsonFile;
                    break;
                }
            }
        }

        if (jsonAsset == null) {
            LogError($"No TextAsset found for track '{track.id}' (jsonFile: '{track.jsonFile}'). " +
                     $"Add it to Track Json Bindings in the inspector.");
            return false;
        }

        // POST-STEP-5 FIX: use ChangeSequence_v2.FromJSON() so the sequence
        // holds MetronomeChange_v2 objects compatible with ScheduleChange().
        ChangeSequence_v2 sequence;
        try {
            sequence = ChangeSequence_v2.FromJSON(jsonAsset.text);
        } catch (Exception ex) {
            LogError($"Failed to parse JSON for track '{track.id}': {ex.Message}");
            return false;
        }

        if (sequence == null) {
            LogError($"Track '{track.id}' JSON parsed to null. Check JSON format.");
            return false;
        }

        sequenceMap[track.id] = sequence;

        var metro = metronomeMap[track.id];
        var sched = schedulerMap[track.id];

        if (sequence.initialBpm > 0)
            metro.SetTempo(sequence.initialBpm);

        if (sequence.initialBeatsPerMeasure > 0)
            metro.BeatsPerMeasure = sequence.initialBeatsPerMeasure;


        int scheduled = 0;
        if (sequence.changes != null) {
            foreach (var change in sequence.changes) {
                if (change.targetBeat < 1)
                    change.targetBeat = 1;

                if (sched != null)
                    sched.ScheduleChange(change);
                else
                    metro.ScheduleChange(change);

                scheduled++;

                if (debugLogging)
                    Log($"    Scheduled: M{change.targetMeasure} B{change.targetBeat} " +
                        $"— {change.GetChangeDescription()}");
            }
        }

        Log($"  ✔ Track '{track.id}': {scheduled} changes scheduled " +
            $"(initial BPM: {sequence.initialBpm})");
        return true;
    }

    // =========================================================
    // BUILD COMPOSITION INDEXES
    // =========================================================

    private void BuildCompositionIndexes() {
        indexMap = new Dictionary<string, CompositionIndex>();

        foreach (var kvp in sequenceMap) {
            string trackId = kvp.Key;
            ChangeSequence_v2 seq = kvp.Value;

            // CompositionIndexGenerator still takes a ChangeSequence (v1).
            // TODO: update CompositionIndexGenerator to accept ChangeSequence_v2
            // once the index system is extended in a future step.
            // For now, convert via the existing path.
            var index = CompositionIndexGenerator.GenerateIndex(ConvertToV1Sequence(seq));
            index.BuildLookupTable();
            indexMap[trackId] = index;

            Log($"  ✔ CompositionIndex built for '{trackId}': " +
                $"{index.measureStates.Count} measures");
        }
    }

    /// <summary>
    /// Temporary bridge: converts a ChangeSequence_v2 to the v1 ChangeSequence
    /// so CompositionIndexGenerator (which hasn't been updated yet) can consume it.
    /// Remove once CompositionIndexGenerator is updated to accept ChangeSequence_v2.
    /// </summary>
    private ChangeSequence ConvertToV1Sequence(ChangeSequence_v2 v2) {
        var v1 = ScriptableObject.CreateInstance<ChangeSequence>();
        v1.title = v2.title;
        v1.initialBpm = v2.initialBpm;
        v1.initialBeatsPerMeasure = v2.initialBeatsPerMeasure;

        if (v2.changes != null) {
            foreach (var c in v2.changes) {
                // Map the fields that CompositionIndex cares about
                var v1Change = new MetronomeChange(c.targetMeasure, c.newBpm, c.description);
                v1Change.targetMeasure = c.targetMeasure;
                v1Change.newBpm = c.newBpm;
                v1Change.newBeatsPerMeasure = c.newBeatsPerMeasure;
                v1Change.hasTempo = c.hasTempo;
                v1Change.hasTimeSignature = c.hasTimeSignature;
                v1Change.hasAudioEvent = c.hasAudioEvent;
                v1Change.hasVisualEvent = c.hasVisualEvent;
                v1Change.muteAudio = c.muteAudio;
                v1Change.hideVisual = c.hideVisual;
                v1.AddChange(v1Change);
            }
        }

        return v1;
    }

    // =========================================================
    // SETUP BEAT ALIGNMENTS
    // =========================================================

    private void SetupBeatAlignments() {
        if (coordination.beatLevelAlignments == null ||
            coordination.beatLevelAlignments.Count == 0) {
            Log("  (No beat alignments defined)");
            return;
        }

        Log($"Setting up {coordination.beatLevelAlignments.Count} beat-level alignment(s)...");
        alignmentTriggers = new List<AlignmentTrigger>();

        for (int i = 0; i < coordination.beatLevelAlignments.Count; i++) {
            var alignment = coordination.beatLevelAlignments[i];

            var refTrack = coordination.GetTrack(alignment.referenceTrack);
            if (refTrack == null) {
                LogError($"  Alignment {i}: references unknown track '{alignment.referenceTrack}'");
                alignmentTriggers.Add(new AlignmentTrigger {
                    triggerMeasure = alignment.lockAtMeasure,
                    triggerBeat = alignment.lockAtBeat,
                    isAtLockPoint = true
                });
                continue;
            }

            // stopAll fires exactly at the lock point — no pickup needed.
            // resetBeat needs a pickup beat to prepare the sync one beat in advance.
            AlignmentTrigger trigger = (alignment.action == "stopAll")
                ? new AlignmentTrigger {
                    triggerMeasure = alignment.lockAtMeasure,
                    triggerBeat = alignment.lockAtBeat,
                    isAtLockPoint = true
                }
                : ComputePickupBeat(
                    alignment.lockAtMeasure,
                    alignment.lockAtBeat,
                    alignment.referenceTrack);

            alignmentTriggers.Add(trigger);

            if (!beatEventHandlers.ContainsKey(alignment.referenceTrack)) {
                string refTrackId = alignment.referenceTrack;
                var refMetronome = metronomeMap[refTrackId];

                Action<int> handler = (beat) => HandleBeatEvent(refTrackId, beat);
                beatEventHandlers[refTrackId] = handler;
                refMetronome.OnBeatTriggered += handler;

                Log($"  ✔ Subscribed to OnBeatTriggered on '{refTrackId}'");
            }

            string pickupNote = (alignment.action == "stopAll")
                ? " (no pickup — stopAll fires at lock point)"
                : trigger.isAtLockPoint
                    ? " (no pickup — lock is at M1 B1)"
                    : $" (fires at M{trigger.triggerMeasure} B{trigger.triggerBeat})";

            Log($"  ✔ Alignment {i}: lock '{alignment.actionTrack}' at " +
                $"M{alignment.lockAtMeasure} B{alignment.lockAtBeat}{pickupNote}" +
                (!string.IsNullOrEmpty(alignment.description)
                    ? $" — {alignment.description}" : ""));
        }
    }

    // =========================================================
    // PICKUP BEAT COMPUTATION
    // =========================================================

    private AlignmentTrigger ComputePickupBeat(
            int lockAtMeasure,
            int lockAtBeat,
            string referenceTrackId) {

        if (lockAtMeasure <= 1 && lockAtBeat <= 1) {
            LogWarning("Lock point is at M1 B1 — no pickup beat possible. " +
                       "Alignment will fire at the lock point itself.");
            return new AlignmentTrigger {
                triggerMeasure = lockAtMeasure,
                triggerBeat = lockAtBeat,
                isAtLockPoint = true
            };
        }

        if (lockAtBeat > 1) {
            return new AlignmentTrigger {
                triggerMeasure = lockAtMeasure,
                triggerBeat = lockAtBeat - 1,
                isAtLockPoint = false
            };
        }

        int prevMeasure = lockAtMeasure - 1;
        int prevBeatsPerMeasure = GetBeatsPerMeasureAt(prevMeasure, referenceTrackId);

        return new AlignmentTrigger {
            triggerMeasure = prevMeasure,
            triggerBeat = prevBeatsPerMeasure,
            isAtLockPoint = false
        };
    }

    private int GetBeatsPerMeasureAt(int measure, string trackId) {
        if (indexMap != null && indexMap.TryGetValue(trackId, out var index)) {
            var state = index.GetStateAtMeasure(measure);
            if (state != null)
                return state.beatsPerMeasure;

            LogWarning($"CompositionIndex for '{trackId}' has no entry for M{measure}. " +
                       "Defaulting to 4 beats.");
        } else {
            LogWarning($"No CompositionIndex found for '{trackId}'. Defaulting to 4 beats.");
        }

        return 4;
    }

    // =========================================================
    // BEAT EVENT HANDLER
    // =========================================================

    private void HandleBeatEvent(string referenceTrackId, int beat) {
        if (coordination?.beatLevelAlignments == null) return;

        var refMetronome = metronomeMap[referenceTrackId];
        int currentMeasure = refMetronome.CurrentMeasure;

        for (int i = 0; i < coordination.beatLevelAlignments.Count; i++) {
            var alignment = coordination.beatLevelAlignments[i];

            if (alignment.referenceTrack != referenceTrackId) continue;
            if (firedAlignments.Contains(i)) continue;

            var trigger = alignmentTriggers[i];
            if (currentMeasure == trigger.triggerMeasure && beat == trigger.triggerBeat)
                ExecuteAlignment(i, alignment);
        }
    }

    private void ExecuteAlignment(int index, BeatLevelAlignment alignment) {
        firedAlignments.Add(index);

        // stopAll is coordinator-level — no actionTrack needed.
        // Check before the actionTrack lookup so the absent field is never read.
        if (alignment.action == "stopAll") {
            string desc = !string.IsNullOrEmpty(alignment.description)
                ? alignment.description : $"alignment[{index}]";
            Log($"✅ STOP ALL: triggered by '{alignment.referenceTrack}' " +
                $"at M{alignment.lockAtMeasure} B{alignment.lockAtBeat} — {desc}");
            StopAll();
            return;
        }

        if (!metronomeMap.TryGetValue(alignment.actionTrack, out var actionMetronome)) {
            LogError($"Alignment {index}: unknown action track '{alignment.actionTrack}'");
            return;
        }

        switch (alignment.action) {
            case "resetBeat":
                var refMetronome = metronomeMap[alignment.referenceTrack];
                actionMetronome.SetCurrentBeat(alignment.resetToBeat);
                actionMetronome.SyncBeatTiming(refMetronome.NextBeatTime);
                if (alignment.targetBpm > 0f)
                    actionMetronome.SetBpm(alignment.targetBpm);

                var trigger = alignmentTriggers[index];
                string desc = !string.IsNullOrEmpty(alignment.description)
                    ? alignment.description : $"alignment[{index}]";

                Log($"✅ BEAT ALIGNED: '{alignment.actionTrack}' reset to B{alignment.resetToBeat} " +
                    $"| lock: M{alignment.lockAtMeasure} B{alignment.lockAtBeat} " +
                    $"| fired: M{trigger.triggerMeasure} B{trigger.triggerBeat} " +
                    $"| {desc}");
                break;

            default:
                LogError($"Alignment {index}: unknown action '{alignment.action}'");
                break;
        }
    }

    // =========================================================
    // START ALL METRONOMES
    // =========================================================

    private void StartAllMetronomes(int fromMeasure) {
        Log($"Starting all metronomes from measure {fromMeasure}...");

        foreach (var kvp in metronomeMap) {
            kvp.Value.StartAtMeasure(fromMeasure);
            Log($"  ✔ Started '{kvp.Key}'");
        }
    }

    private IEnumerator StartAllWithDelay(StartAllConfig config) {
        Log($"  Waiting {config.delaySeconds}s before start...");
        yield return new WaitForSeconds(config.delaySeconds);
        StartAllMetronomes(config.atMeasure);
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void UnsubscribeAll() {
        if (beatEventHandlers != null && metronomeMap != null) {
            foreach (var kvp in beatEventHandlers) {
                if (metronomeMap.TryGetValue(kvp.Key, out var metro))
                    metro.OnBeatTriggered -= kvp.Value;
            }
        }

        beatEventHandlers.Clear();
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private void ResetMetronomeAndScheduler(
            PrecisionMetronome_v5_BeatLevel metro,
            MetronomeScheduler_v2 sched) {
        metro?.ClearPendingChanges();
        metro?.ResetMetronome();
        sched?.ClearPendingChanges();
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private bool ValidateInspectorAssignments() {
        bool ok = true;

        if (coordinationJsonFile == null) {
            LogError("coordinationJsonFile is not assigned!");
            ok = false;
        }
        if (metronome1 == null) {
            LogError("metronome1 is not assigned!");
            ok = false;
        }
        if (metronome2 == null) {
            LogError("metronome2 is not assigned!");
            ok = false;
        }

        return ok;
    }

    // =========================================================
    // LOGGING
    // =========================================================

    private void Log(string message) {
        statusLog += message + "\n";
        if (debugLogging) Debug.Log($"[CoordinatorV2] {message}");
    }

    private void LogWarning(string message) {
        statusLog += $"⚠ {message}\n";
        Debug.LogWarning($"[CoordinatorV2] {message}");
    }

    private void LogError(string message) {
        statusLog += $"❌ {message}\n";
        Debug.LogError($"[CoordinatorV2] {message}");
    }

    private void ClearLog() {
        statusLog = "";
    }
}

// =========================================================
// INSPECTOR HELPER
// =========================================================

[Serializable]
public class TrackJsonBinding {
    [Tooltip("Must match the 'id' field in the coordination JSON (e.g. 'track1')")]
    public string trackId;

    [Tooltip("The .json TextAsset for this track's tempo changes")]
    public TextAsset jsonFile;
}