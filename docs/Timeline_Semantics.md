# Timeline Semantics (D12): timing rules of the Phase 7 live scheduler

Written November 1, 2026 from `main` at `86eb639` (tag `reference-timing-log-verified`). Decisions on the open items added the same day (section 5).

Source versions read: PrecisionMetronome_v5_BeatLevel v5.3 (2026-02-18), MetronomeScheduler_v2 v2 (2025-01-10), MetronomeStateManager_v1 v1 (2025-08-23), CompositionCoordinator_v2 v4 (2026-09-03), MetronomeChange_v2 v2 (2025-01-10), CompositionCoordination v2 (2026-03-20), CompositionJsonSerializer_v2 v2 (2026-03-20).

The build scene (`Phase7_Rebuild_Aug_03_Nov28_2025_08032026.unity`) contains three v5 metronomes and one coordinator and none of the drift, subdivision or debug scripts, so nothing outside these files changes a beat time or a track's state at runtime.

Short names used in citations:

| Short | File |
|---|---|
| PM | `Assets/Scripts/Metronome/PrecisionMetronomeRefactor/PrecisionMetronome_v5_BeatLevel.cs` |
| SCH | `Assets/Scripts/Metronome/PrecisionMetronomeRefactor/MetronomeScheduler_v2.cs` |
| SM | `Assets/Scripts/Metronome/PrecisionMetronomeRefactor/MetronomeStateManager_v1.cs` |
| CC | `Assets/Scripts/Coordination/CompositionCoordinator_v2.cs` |
| CO | `Assets/Scripts/Coordination/CompositionCoordination.cs` |
| MC | `Assets/Scripts/ChangeComposer/Data/MetronomeChange_v2.cs` |
| SER | `Assets/Scripts/ChangeComposer/Indexing/CompositionJsonSerializer_v2.cs` |
| TC | `Assets/Scripts/Coordination/TransportControls.cs` |

Verdicts:

- **KEEP**: intended musical behavior the new timeline must reproduce.
- **DROP**: artifact of live scheduling or a bug; the new timeline must not copy it.
- **DECIDED**: intent was not decidable from code; the owner's decision is recorded in section 5 and overrides the old behavior described in the rule.

## 1. Rules

### Clock and grid

- **R1 KEEP.** Each track holds one running beat time; after processing a beat it adds exactly 60/BPM seconds, using the BPM in effect after that beat's changes were applied. No re-anchoring to the audio clock ever happens during normal playback. PM:309, PM:128.
- **R2 DROP.** A beat is processed on the first frame whose `AudioSettings.dspTime` is at or past the beat time, and the click is then scheduled at that already-past time, so audio starts late by up to one DSP buffer. Measured 0.22 to 21.22 ms across the three October 31 logs; the intended time is the grid time, not the audio time. PM:265, PM:420.
- **R3 DROP.** BPM is stored as a float and the interval is computed as 60.0 / (double)(float)BPM, so a non-integer BPM is rounded to float precision first. Integer BPMs are exact. The new timeline should use the exact value. PM:69, PM:128, PM:796.

### Start

- **R4 KEEP the shared start, DROP the offset.** All tracks started by the coordinator in one frame get the identical first-beat time, frame dspTime + 0.1 s, because `StartAtMeasure` always overwrites the beat time. The 0.1 s is a scheduling lead and means nothing musically. CC:707-717, PM:607.
- **R5 DROP.** With `startAll.enabled`, the start is armed for dspTime + `delaySeconds` (0.5) and fires on the first `Update` at or after that deadline, so M1 B1 sits at load time + 0.5 s + up to one frame + 0.1 s. The delay exists to "give Unity time to settle". CC:731-750, CO:117.
- **R6 DROP.** The first beat is scheduled twice: once at start with the pre-change tempo (logged as 120 BPM) and again in the first `Update` after the M1 B1 changes were applied. Both calls target the same dsp time; the second restarts the clip already playing, about 7 ms in. This happens at every start, including start-at-measure. PM:589, PM:608, PM:300.
- **R7 KEEP.** Initial state is applied in this order: inspector default, then on load the JSON `initialBpm` and `initialBeatsPerMeasure` (the serializer substitutes 120 and 4 for missing or non-positive values), then at the first beat any change targeting that beat. A change at M1 B1 is applied before the first click and before the first interval, so it wins over the initial values for everything audible. `initialBpm` governs playback only when no change sits on the start beat. CC:397-404, SER:106-107, PM:288-290, SCH:121-125, PM:494.

### Beat processing order

- **R8 KEEP.** At each beat (m, b) the metronome does, in order:
  1. if b = 1 and this is not the run's first beat, advance the measure counter and apply changes targeting (m, 1);
  2. apply changes targeting exactly (m, b);
  3. run the coordinator's alignment handlers;
  4. schedule the click for (m, b) unless muted;
  5. draw the visual for (m, b);
  6. increment the beat counter using the current beats-per-measure;
  7. set the next beat time = this beat time + 60/current BPM.

  Consequence: a change at (m, b) changes the state of beat (m, b) itself (mute, visual, stop, meter) and the interval that follows it (tempo), and never moves beat (m, b). Beat 1 and mid-measure changes follow the same rule; steps 1 and 2 both test exact equality. PM:265-310, SCH:231-239.
- **R9 KEEP.** A tempo change at (m, b), whether type Tempo, Both or Combined with tempo, leaves beat (m, b) on the old grid and spaces the following beats at the new tempo. Measured: track 2 M4 B4 to M5 B1 = 60/85, M5 B1 to M5 B2 = 60/90. PM:501, PM:516, PM:552, PM:309.
- **R10 DECIDED (see 5.2).** Old behavior: changes fire only on exact (measure, beat) equality. A change on a beat that never occurs, such as B4 in a 3-beat measure or any beat before the start position, stays pending forever and is silently never applied. SCH:231-239.
- **R11 KEEP.** A target beat below 1 is clamped to 1 at load. SER:183, CC:411-412.
- **R12 DECIDED (see 5.3).** Old behavior: several changes on the same beat are all applied, in list order after an unstable sort, so two tempo changes on one beat have an undefined winner. SCH:282-291.

### Meter

- **R13 KEEP.** A meter change (TimeSignature, Both, or Combined with meter) sets beats-per-measure immediately, before the beat counter increments for that beat. On beat 1 the whole measure has the new length. PM:506, PM:517, PM:553, PM:308.
- **R14 DECIDED (see 5.5).** Old behavior for a mid-measure meter change: the measure ends after the first beat whose number is at or above the new count. So 4/4 to 3 at M10 B2 gives M10 three beats (B1 old meter, B2, B3); 4/4 to 3 at M10 B4 gives M10 four beats; 4/4 to 6 at M10 B4 gives M10 six beats. PM:425-430.
- **R15 DECIDED (see 5.4).** Old behavior: beats-per-measure has a floor of 1, so a `Both` change whose JSON `beatsPerMeasure` is 0 produces one-beat measures. Tempo-type changes carry 0 harmlessly because they never apply it. PM:136, SER:141.
- **R16 KEEP for now.** The denominator is stored but never read for timing; the JSON has no denominator field; the interval is 60/BPM per counted beat whatever the meter. Denominator semantics belong to the separate design in `TimeSignature_Denominator_Design_Handoff_August2026.md`. PM:71, PM:141, PM:846, SER:244-258.

### Mute and visuals

- **R17 KEEP.** Mute at (m, b) suppresses the click of (m, b) and every later click; the grid, counters, measure advancement, events and visuals keep running. Unmute at (m, b) sounds (m, b) and onward. The Combined type with `hasAudioEvent` takes the same path. PM:412, PM:531-539, PM:554, PM:802-814.
- **R18 KEEP.** VisualOff at (m, b) disables the visual for (m, b) onward and resets the indicators; VisualOn re-enables from (m, b). State only, no effect on timing. PM:541-549, PM:555, PM:821-837, PM:303-305.
- **R19 DROP.** The Combined type with `hasTimeSignature` does not tell the time-signature visual controller about the new meter and fires no settings-changed event, unlike TimeSignature and Both. Display bug; the new display reads meter from the timeline. PM:551-557, PM:565-569, PM:507-511.

### Stop

- **R20 DECIDED the click (see 5.1), KEEP the freeze.** Old behavior: a Stop change at (m, b) stops the track at that beat: playing flag off, audio source stopped, visuals reset. Because it is applied in step 2 and the click is scheduled in step 4, the click for (m, b) still sounds; the counters then increment once more and freeze. Nothing continues for visuals or anything else; `Update` returns at its first line. PM:526-528, PM:624-653, PM:300, PM:308-309, PM:261.
- **R21 KEEP.** A Stop change stops only its own track; only the coordinator's `stopAll` stops all tracks. CC:205-218.
- **R22 KEEP the end time, DECIDED the click (see 5.1), DROP the frame dependence.** Old behavior: `stopAll` with lock (M, B) on the reference track fires inside the reference's beat handler for (M, B), before that click is scheduled, so the reference's lock-beat click sounds and all other tracks are stopped wherever they are in their interval; their next beat never happens. A click another track scheduled earlier in the same frame could be cut by the stop. The end time for every track is the reference's (M, B) time. CC:527-533, CC:647-648, CC:658-665, CC:213-215, PM:632.
- **R23 Finding.** The extra click after a stop (R20, R22) is confirmed from code but cannot be seen in the October 31 CSVs: track 1 muted at M18 B2, track 2 at M19 B2, track 3 at M20 B2, and the stop fired at track 1 M19 B1 = 50.824 s, when track 2 was between M19 B4 and M20 B1 and track 3 between M20 B4 and M21 B1. Every track was silent before the stop, so no stop-beat click was logged.

### Beat-level alignment (resetBeat)

- **R24 DROP.** For `resetBeat` the coordinator fires one reference beat before the lock point, the "pickup": (M, B-1) when B > 1, else (M-1, last beat of M-1) read from the reference's CompositionIndex with a fallback of 4. This exists only so a live metronome can be retargeted before the lock; it also depends on the known-buggy index generator. The new timeline can act at the lock point directly. CC:570-607, CC:609-626.
- **R25 KEEP.** At the pickup the action track's beat counter is forced to `resetToBeat` (clamped to its beats-per-measure), its pending next beat time is overwritten with the reference's lock-beat time, and if `targetBpm` > 0 its BPM is set. So alignment does change a beat time: the action track's pending beat is replaced by a beat at the reference's lock time, labeled `resetToBeat`, and the action track's grid continues from there at its own or the new BPM. If the action track's pending beat was due between the pickup and the lock it is lost; if due after the lock it is pulled earlier. CC:676-681, PM:784-792, PM:794-799, PM:143, PM:309.
- **R26 DECIDED (see 5.6).** Old behavior: alignment does not touch the action track's measure number; if `resetToBeat` = 1 the action track's measure counter simply increments at the lock. PM:267-269.
- **R27 DROP.** `SetBpm` from an alignment bypasses the `Bpm` property, so the BPM input field and widgets are not updated. Timing is identical. PM:794-799 vs PM:124-131.
- **R28 DROP.** `firedAlignments.Add(index)` runs before the action-track and action-name guards, so a misconfigured `resetBeat` errors once and is then dead for the run. Harmless for `stopAll`. Also, an alignment naming an unknown reference track gets a trigger but no subscription, so it never fires. CC:654 vs CC:668-672, CC:693-695, CC:512-523.
- **R29 DROP.** A `resetBeat` lock at M1 B1 has no pickup, so it fires at M1 B1 itself, but the handler still syncs the action track to the reference's next beat, which is M1 B2. The action locks one beat late. Irrelevant in the new system since all tracks share t0. CC:576-586, CC:679, PM:143.
- **R30 DROP.** If the action track also has a beat due in the same frame as the reference's pickup beat, whether that click sounds before being overwritten depends on script execution order.
- **R31 KEEP.** Each alignment fires at most once per run; the fired set is cleared on load and on reset. CC:131, CC:182, CC:237, CC:644.

### Start at measure

- **R32 DROP the mechanism, DECIDED the meaning (see 5.7).** Old behavior: `StartAtMeasure(n)` sets the counters to (n, 1), schedules the first beat at dspTime + 0.1, and moves the scheduler's position to (n, 1). Changes before measure n are never applied because matching is exact, so the track starts at `initialBpm` / `initialBeatsPerMeasure` plus whatever change sits exactly at (n, 1); the tempo and meter accumulated over measures 1 to n-1 are not reconstructed. All tracks also start their own measure n at the same instant, even though in a multi-tempo piece those downbeats fall at different absolute times. PM:594-611, CC:703-720, SCH:232-234.

### Lifecycle (nothing to port, listed for completeness)

- **R33 DROP.** Stop does not clear pending changes or rewind the scheduler's position; only Reset does. A Load-and-Start after Stop without Reset schedules changes while the scheduler still sits at the last played position, so every change at or before that position is rejected as "in the past", and leftovers from the previous run persist. `TransportControls.PlayFromTop()` takes exactly that path. Not verified in Play mode; the documented flow is Reset, then Load and Start. CC:205-218, CC:774-793, SCH:70-74, TC:62-68.
- **R34 DROP.** Pause and resume: `StartMetronome` snaps a past beat time to dspTime + 0.1 and sets the first-beat flag, which skips one measure advance if the resume lands on beat 1, so the measure number lags by one. Not used by the coordinator. PM:585-587, PM:582, PM:267.
- **R35 No effect.** The advance-notification system only emits log messages and events. SCH:222-228, MC:301-306.

### Measured facts (Task 1 CSVs, October 31, 2026), confirmed against code

| Fact | Verdict | When it would not hold |
|---|---|---|
| 1. Shared start, M1 B1 = dspTime + 0.1 | Confirmed (R4) | Tracks started in different frames, or via `StartMetronome()` with a stale future beat time (PM:585). |
| 2. First interval is 60/85 | Confirmed (R7, R8) | If the start-beat change were rejected (R33), or if the first change were on M1 B2; then `initialBpm` governs the first interval. |
| 3. Tempo change leaves its beat on the old grid | Confirmed (R9) | Always holds, every beat and every tempo-bearing type. |
| 4. Times are t0 + sum of 60/BPM, no drift | Confirmed (R1); CSV intervals are exactly 60/85, 60/90, 60/96 | `resetBeat` alignment re-anchors (R25); float BPM rounding (R3); pause/resume (R34). |
| 5. Clicks 0 to 21 ms late, M1 B1 scheduled twice | Confirmed (R2, R6); measured 0.22 to 21.22 ms, matching a 1024-sample buffer at 48 kHz | Always holds. |
| 6. Mute skips the click, grid keeps running | Confirmed (R17) | Always holds. |
| 7. Stopping track schedules one more click | Confirmed from code (R20, R22), not visible in these CSVs (R23) | Other tracks under `stopAll` get no extra click; a same-frame click could even be cut. The new timeline drops this click (5.1). |

## 2. Formulas

These describe the **new** timeline: the KEEP rules plus the decisions in section 5. For one track, number the beats k = 0, 1, 2, ... from M1 B1. Each beat has a label (m_k, b_k), a time t_k, and the state in effect after its changes: BPM_k, N_k (beats per measure), muted_k, visible_k.

1. **Start.** t_0 = T0 (use 0 in the hand table), (m_0, b_0) = (1, 1). The timeline is always computed from M1 B1; "start at measure n" is a playhead seek into it (5.7), not a different starting state. State before any beat: BPM = `initialBpm`, N = `initialBeatsPerMeasure`, unmuted, visible.
2. **Changes.** At beat k, apply every change whose (measure, beat) equals (m_k, b_k), in this order (5.3): meter sets N_k (only allowed when b_k = 1, 5.5), tempo sets BPM_k, Mute or Unmute sets muted_k, VisualOff or VisualOn sets visible_k, Stop ends the track at k. Otherwise state carries over from k-1.
3. **Next time.** t_{k+1} = t_k + 60 / BPM_k.
4. **Next label.** If b_k + 1 > N_k then (m_k + 1, 1), else (m_k, b_k + 1).
5. **Click.** The click at t_k sounds iff not muted_k and the track has not stopped at or before k; it is the strong click iff b_k = 1.
6. **Stop.** A Stop at beat k (label (m, b)) is silent on its own beat (5.1): the track's last possible click is at t_{k-1}, and t_k is the track's end time. Beat k and every later beat produce no click and no visual.
7. **stopAll.** A `stopAll` locked at reference beat (M, B), whose time on the reference track is T_stop, silences every track from T_stop on (5.1): for each track, every beat with t_k >= T_stop is silent, including the reference's (M, B) and any other track's beat that falls exactly on T_stop. Each track's last possible click is its last t_k < T_stop. T_stop is the end time for all tracks.
8. **resetBeat.** With reference lock time T_L: let j be the action track's last beat with t_j < T_L. Replace the action's next beat: t_{j+1} = T_L, b_{j+1} = clamp(`resetToBeat`, 1, N_j), m_{j+1} = m_j (then rule 4 applies from there, so a reset to beat 1 increments the action track's own measure number, 5.6), and BPM_{j+1} = `targetBpm` if > 0 else BPM_j. Continue with rule 3. (The old app chose j relative to the pickup beat, R24; with the pickup dropped, j is relative to the lock time.)
9. **Spreadsheet note.** For integer BPMs 60/BPM is exact; for fractional BPMs the old app used the float-rounded BPM (R3), and the new timeline uses the exact value. Beat index inside constant 4/4: k = 4(m-1) + (b-1).

## 3. Ambiguities and disagreements

- **Stop-beat click** (R20, R22): the code sounds it only because `StopMetronome()` runs before `ScheduleNextBeat()`; the comment at PM:313-315 treats this as a logging detail, not a musical choice. Decided: silent (5.1).
- **Three copies of position.** The metronome's own beat counter, the state manager's, and the scheduler's are updated at different points in the frame (PM:308, PM:295, SCH:122-123). Only the metronome's drives timing; the display reads the state manager, which lags one step during each interval. Coordinator alignment checks read the metronome's measure (CC:637) after the advance, which is why lock checks on beat 1 work.
- **`NextBeatTime` is only correct inside the beat handler.** The property adds one interval to the pending beat (PM:143); it equals the lock time only because the handler runs before PM:309. Any other caller would get the wrong beat.
- **Lock at M1 B1** (R29): the code comment says the alignment "will fire at the lock point itself", but the sync lands on M1 B2.
- **`CompositionCoordination.cs` says only `resetBeat` is supported** (CO:91-94) while the coordinator also implements `stopAll`. Comment is stale.
- **Combined type and the visual meter** (R19): TimeSignature and Both update the visual controller; Combined does not.
- **Frame-dependent behavior**: click lateness (R2), start delay (R5), same-frame ordering at `stopAll` (R22) and at the pickup (R30), pause measure lag (R34).
- **Reload without Reset** (R33): code says changes are rejected; not verified in Play mode.
- **Duplicate changes on one beat** (R12) and **mid-measure meter changes** (R14) had definite but probably unintended behavior. Decided: load errors (5.3, 5.5).
- **Start at measure n** (R32) was a counter reset, not a seek, with no defined meaning across tracks with different tempi. Decided: seek to the reference track's Mn B1 (5.7).

## 4. Worked example: track 2, M4 B3 to M5 B3

Track 2 JSON (`Track2_Demo_Intro_Viola_BPM90_withSyncFile_08262026.json`): initial 120 BPM 4/4; `Both` 85 BPM 4/4 at M1 B1; `Tempo` 90 at M5 B1; `Mute` at M19 B2.

Zero = M1 B1. With constant 4/4, k = 4(m-1) + (b-1). BPM_k = 85 for k <= 15, so t_k = k x 60/85 for k <= 16 (the beat at k = 16, M5 B1, is still placed by the 85 interval, rule 3 with BPM_15). From k = 16 on, BPM_k = 90.

| Beat | k | Formula | Intended (s) | Interval from previous | CSV (dsp_time - 602.681333) |
|---|---|---|---|---|---|
| M4 B3 | 14 | 14 x 60/85 | 9.882353 | 0.705882 | 9.882353 |
| M4 B4 | 15 | 15 x 60/85 | 10.588235 | 0.705882 | 10.588235 |
| M5 B1 | 16 | 16 x 60/85 | 11.294118 | 0.705882 | 11.294118 |
| M5 B2 | 17 | 16 x 60/85 + 1 x 60/90 | 11.960784 | 0.666667 | 11.960784 |
| M5 B3 | 18 | 16 x 60/85 + 2 x 60/90 | 12.627451 | 0.666667 | 12.627451 |

All five match `ReferenceTiming/Metronome2_20261031_164615.csv` with a difference below one microsecond. Track 2's last logged beat, M19 B1 (k = 72) = 16 x 60/85 + 56 x 60/90 = 48.627451 s, also matches. The second run (`_164510`) has identical row counts and was not analyzed separately.

## 5. Decisions (November 1, 2026)

Owner decisions on the items the code could not settle. Where a decision differs from the old app's behavior, the new timeline follows the decision, and the hand table (D8) must be computed from the decision, not from the old CSVs.

- **5.1 Stop is silent on its own beat (R20, R22).** A Stop at (m, b), or a `stopAll` locked at (M, B), means "from this beat on, nothing": beat (m, b) does not click, for any track. This matches every other change type, where a change at (m, b) applies to beat (m, b) itself. Formulas 6 and 7 implement this. Note for D8: the old app sounded the stop beat, so a stop-beat row present in an old CSV is expected to be absent from the new timeline.
- **5.2 A change on a beat that does not exist is a load error (R10).** The loader rejects any change whose (measure, beat) the track never reaches, for example beat 4 in a 3-beat measure. It is not silently ignored.
- **5.3 Duplicate kinds on one beat are a load error; mixed kinds have a fixed order (R12).** Two changes of the same kind on one beat (two tempos, two meters) are a load error. Different kinds on one beat are allowed and are applied in this order: meter, tempo, mute/unmute, visual, stop.
- **5.4 Beats per measure below 1 is a load error (R15).** Any meter-bearing change (TimeSignature, Both, or Combined with meter) with beats per measure < 1 is rejected at load.
- **5.5 Meter changes only on beat 1 (R14).** A meter-bearing change must target beat 1. A mid-measure meter change is a load error.
- **5.6 resetBeat keeps the action track's measure numbering (R26).** After a `resetBeat`, the action track keeps counting its own measures; it does not adopt the reference track's measure number.
- **5.7 Start at measure n seeks on a reference track (R32).** "Start at measure n" means measure n of a reference track (track 1 by default). The playhead seeks to that track's Mn B1 time; every other track is wherever the timeline puts it at that time.
