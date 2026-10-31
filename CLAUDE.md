# Claude Session Rules — ChangeComposer / Metronome Project

**Last updated:** September 3, 2026 (status note added October 31, 2026)
**Replaces:** `DEPENDENCY_CHECK_REMINDER.md` (delete that file — its class list was stale and actively misleading)

> **Status note for Claude Code (October 31, 2026).** This file was written for claude.ai chat sessions, where files came from a project-knowledge snapshot (`/mnt/project/`). In Claude Code you work on the **live repo**, so the files you read are current; rule 1.1's provenance check applies to any copy the user pastes in from elsewhere. Two open items in §5 are now closed: the `feature/textparser-uncommitted-work` branch was identified as `TextParser_v2` v4, merged to `main` and tagged `v4-textparser-selftest-pass` (October 27); the reset fix was verified in Play mode and tagged `v4-reset-verified` (October 28). Every change is a diff the user reviews and approves before it is applied.

This file exists because of two real incidents, seven weeks apart, with the same root cause: **a stale copy of a live file in project knowledge was treated as current.** The second incident destroyed working code.

---

## 1. Rules for Claude

### 1.1 Before editing any project file — check provenance

Project knowledge is a reference shelf, and shelves go stale. Before modifying a file from `/mnt/project/`:

- Read its version header and date.
- Cross-check that against the newest handoff document that mentions the file.
- **If a handoff describes a version newer than the header says, STOP.** Do not proceed. Ask for the current file to be uploaded.

Concrete example of the check working: `Phase7_StudioBuild_Handoff_August2026.md` §7 states the coordinator is v3 with 7 `stopAll` references. If the project copy's header reads `2026-03-20 v2` and contains zero, the copy is stale. That single comparison would have prevented the September 3 incident.

### 1.2 Never hand over a full-file replacement for a file not seen in its live form

A refactor delivered as a whole file silently reverts anything the source copy was missing. Before telling the user to paste over a live file:

- Confirm the source is current (rule 1.1), **or**
- Deliver targeted edits (specific `str_replace`-style changes) instead of a whole file, **or**
- Say plainly that the base may be stale and ask for the live file first.

Whole-file replacement is the highest-risk delivery format. Prefer targeted edits when provenance is uncertain.

### 1.3 After delivering any modified file — prompt for a commit

When a `.cs`, `.json`, or scene file has been changed, end the response with a concrete commit suggestion:

```
git add <specific paths>
git commit -m "<type>: <description>"
git push origin main
```

Use the commit types from `GIT_WORKFLOW.md`: `feat:`, `fix:`, `test:`, `checkpoint:`, `refactor:`, `docs:`, `chore:`.

Do not suggest committing until the change has been verified in Unity. "Compiles" is not "works" — the September 3 file compiled fine and still had destroyed a feature.

### 1.4 Suggest a tag after a verified fix

When something meaningful has been confirmed working in Play mode:

```
git tag -a <name> -m "<what was verified>"
git push origin <name>
```

Tag names should state what was verified, not just a version number.

### 1.5 Recommend branching for uncertain work

Verified work goes to `main`. Untested work, unknown-provenance work, and experiments go to a branch. Commit the safe work to `main` first — uncommitted changes follow across branch switches, so branching later loses nothing.

---

## 2. Rules for the user

- **Ask for git instructions whenever a file changes.** The habit costs seconds and makes every mistake reversible.
- **Refresh project knowledge after a session that changes a live file.** Replace the copy; don't add a second one.
- **One copy per live file in project knowledge.** It's a reference shelf, not an archive. Git is the archive.

---

## 3. Current class names — verified September 3, 2026

Never assume or abbreviate. These were read from source, not memory.

### MonoBehaviours

| Class | File |
|---|---|
| `PrecisionMetronome_v5_BeatLevel` | `PrecisionMetronome_v5_BeatLevel.cs` |
| `MetronomeScheduler_v2` | `MetronomeScheduler_v2.cs` |
| `CompositionCoordinator_v2` | `CompositionCoordinator_v2.cs` |
| `TransportControls` | `TransportControls.cs` |

Note: `CompositionCoordinator_v2` is the class name even at file version v4. The `_v2` suffix is frozen — renaming a MonoBehaviour file creates a new `.meta` GUID and the scene loses every inspector reference. See `Phase7_StudioBuild_Handoff_August2026.md` §6.

### Data classes

| Class | Namespace |
|---|---|
| `MetronomeChange_v2` | `ChangeComposer.Data` |
| `ChangeSequence_v2` | `ChangeComposer.Data` (ScriptableObject) |
| `TrackReference`, `BeatLevelAlignment`, `StartAllConfig` | `ChangeComposer.Coordination` |
| `ParsedTrackEvent` | `ChangeComposer.Parsing` |
| `CompositionIndex`, `MeasureState` | `ChangeComposer.Indexing` |

### Abandoned — do not reference

`ThreeMetronomeCoordinator` (all 11 versions), `PrecisionMetronome_v4_MultipleDisplays`, `MetronomeScheduler_v1`, un-suffixed `MetronomeChange` / `ChangeSequence`, `CompositionCoordinator` (no suffix), `RuntimeResetModule_v4`, `MusicalParser`, `ControlParser`.

These are plausible search hits that return outdated answers. The v11 architecture was explicitly abandoned in Phase 7.

---

## 4. Incident log

### September 3, 2026 — stale snapshot destroyed the `stopAll` feature

**What happened.** A genuine bug was reported: the Reset button didn't re-schedule changes on reload. Claude read `CompositionCoordinator_v2.cs` from project knowledge (header `2026-03-20 v2`), refactored it, and told the user to paste it over the live file. The live file was v3 (`2026-03-31`), which contained the `stopAll` SYNC action. The paste silently deleted that feature.

**Root cause.** The project-knowledge copy was seven weeks stale. `Phase7_StudioBuild_Handoff_August2026.md` §7 had documented this exact drift for this exact file and stated it was fixed — the refresh evidently didn't take.

**Why it wasn't caught.** Claude never compared the file header against the August handoff. The mistake was also self-concealing: the pasted file compiled cleanly and the reported bug appeared fixed. Only a separate feature failing, several exchanges later, revealed it.

**Recovery.** The `stopAll` implementation was reconstructed verbatim from the March 31 session transcript and verified two ways: reference count matched the August handoff's "7 `stopAll` references", and `git diff` showed the reconstructed lines as unchanged context against the committed v3. Confirmed working in Unity — `✅ STOP ALL` fired at M19 B1.

**What made recovery possible.** The session transcript, and git — which had held the intact v3 the entire time.

**Underlying real bug (genuine, now fixed in v4).** `MetronomeScheduler_v2` keeps private `currentMeasure`/`currentBeat` advanced only by `HandleMeasureChanged()`/`HandleBeatChanged()` during playback. Neither `ClearPendingChanges()` nor `ResetMetronome()` rewinds them, so after a run `ScheduleChange()` rejected every change as "in the past". Fix: `sched.SetCurrentPosition(1, 1)` in `ResetMetronomeAndScheduler()`.

### July 29, 2026 — same root cause, caught in time

An uploaded `CompositionCoordinator_v2.cs` was found to be newer than the project copy (`2026-03-31 v3` vs `2026-03-20 v2`). Caught during analysis, no damage. Logged in `Phase7_StudioBuild_Handoff_August2026.md` §7 — the warning that was not heeded on September 3.

---

## 5. Open items

- ~~**`feature/textparser-uncommitted-work` branch**~~ — **Closed October 27, 2026:** identified as `TextParser_v2` v4, merged to `main`, tagged `v4-textparser-selftest-pass`.
- **Initial time signature fix untested** — v4 applies `initialBeatsPerMeasure` on load, but all current JSONs are 4/4. Prerequisite for the denominator work in `TimeSignature_Denominator_Design_Handoff_August2026.md`. (Reset fix itself verified October 28, 2026, tag `v4-reset-verified`.)
- **`firedAlignments.Add(index)` runs before the guards** in `ExecuteAlignment()`, so a misconfigured alignment burns itself on the first attempt and fails silently once instead of loudly every bar. Harmless for `stopAll`; a latent trap for `resetBeat` typos.
- **Refresh project knowledge** — replace the stale `CompositionCoordinator_v2.cs` with the live v4 and delete `DEPENDENCY_CHECK_REMINDER.md`.
