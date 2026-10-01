# Layer time accounting is not carried by the archive

- Status: Done
- Acceptance (20261001-n): all three rows judged on the fixed seam (commit `9f79f25a`) — the timer
  resumed at `364.8` of the cut's value (host live read `364.77`; the guest converged to `364.8` through
  the in-world message path), an archive whose optional `layerTimeSpent` property was dropped decoded as
  `layer time absent` and the continued layer restarted from zero without inventing a value, and a layer
  advance kept the game's own accounting (`0.65 → 36.57`, limit `3600`) — record
  `../evidence/acceptance/save-layer-time-not-carried-20261001-n.md`
- Priority: Low-Medium
- Category: Persistence / save system (game state no CUO domain owns)
- Source: the S3.4a independent adversarial pass; pre-existing and native-parity-consistent. Landed in
  the clock/layer-time cycle (2026-09-19) after the user ruled RESUME (2026-09-19)
- Related: `docs/architecture/save-archive-format.md` §3.4, `docs/decisions/active.md` 166/169/171/193,
  `src/CasualtiesUnknownOnline.Runtime/Persistence/SaveNativeRunFields.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Persistence/WorldSnapshotDecoder.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/Patches/WorldGenerationUpdatePatch.cs`

## The gap

`layerTimeSpent` and `maxTimePerLayer` are per-layer game state that no CUO domain owns and no archive
row carried. Continuing into the SAME layer therefore restarted the radiation line's timer and handed
the player a fresh `timelimit`.

The NATIVE save does not carry them either: the native write puts only
`saveInfo.runTime = SaveSystem.savedRunTime + WorldGeneration.world.realTimeElapsed;` into its run
record (`reversing/Assembly-CSharp/Assembly-CSharp/SaveSystem.cs:165`), so a native continue restarts
the same timer. That is why this was a parity/design question rather than a defect CUO introduced.

The only place CUO touched the pair was the generation update patch, which documents the single
reader and clamps the value:
`WorldGenerationUpdatePatch.cs` — "layerTimeSpent is otherwise consumed only by the line condition" and
`__instance.layerTimeSpent = Mathf.Min(__instance.layerTimeSpent, __instance.maxTimePerLayer);`.

## The decision (asked, then ruled)

Two defensible behaviors, and they differ in what the player gets:

- **Resume** — the restored layer keeps the time it had already spent, so the radiation line continues
  from where the interrupted run stood. Better than native, and the reason to archive the value at all.
- **Restart** — exactly what a native continue does today. Nothing new is archived.

The user ruled **RESUME** on 2026-09-19 (asked with the native behavior stated, per `AGENTS.md` rule 9).

## What landed

`run.json`'s **`native-run-fields`** row carries an OPTIONAL `layerTimeSpent` (`SaveNativeRunFields` /
`NativeRunFields`). A MID-RUN cut records it; a LAYER-END cut records nothing, because the layer it
names is regenerated and the timer belongs to the new layer — the encoder drops it with the same rule
it drops every other in-layer fact. The property is ABSENT rather than null when nothing was recorded
(`SaveArchiveJson` ignores null members), so the decoder tells "the cut recorded no layer timer" from
"it recorded one" by the property's presence and the live timer is left alone; the restore then names
the gap instead of inventing a zero. The LIMIT is deliberately not carried: `WorldGeneration.Start`
derives `maxTimePerLayer` from the restored run settings.

The write does NOT ride the restored clock base's seam: the native save slot runs before the generation
coroutine, and the game zeroes `layerTimeSpent` on the first line of `FinishWorldGeneration`
(`WorldGeneration.cs:3609`), so a timer written there is erased (acceptance 20261001-m Run A). The cut
arms a pending value through `INativeWorldFacts.ApplyCutRunFields(savedRunTime, layerTimeSpent)`, and
`TryWritePendingLayerTimer` lands it at the first seam after the generation — the world-entry edge, or
the apply of a message that reaches a member already in the world; the member-side handover travels on
`RunFacts` (see `review/save-run-clock-not-sent.md`).

| # | Acceptance row (resume) | What pins it |
|---|---|---|
| 1 | Continue into the same layer with 6 of 10 minutes already spent; the timer starts at 6 | `WorldRunFieldTests.MidRunCut_WritesTheRunBaselineAndTheNativeRunFields` (the row carries it) + `Continue_CarriesTheLayerTimerBackToTheNativeApplier` (the handover and the world-entry flush write it); the in-game timer itself was judged by the agent-run acceptance (batch 20261001-n) |
| 2 | Continue a cut whose archive carries no layer-time row; the timer restarts and the log says why | `WorldRunFieldTests.Continue_FromAnArchiveWithoutTheLayerTime_KeepsTheLiveTimer`, `MidRunCut_WithNoLayerTimerRead_WritesNoLayerTimeProperty`, `WorldSnapshotCodecTests.Decode_NativeRunFieldsRow_WithoutALayerTimer_KeepsTheAbsence` |
| 3 | Layer advance after a restore; the next layer keeps the game's own accounting | `WorldSnapshotCodecTests.Decode_NativeRunFieldsRow_CarriesTheLayerTimerWhenTheRowHasOne` + `WorldRunFieldTests.LayerEndCut_CarriesTheRunFieldsAndStampsTheBaseline` (a layer-end cut carries no timer) |

## Verification (machine-checked)

- Focused suites green: `WorldRunFieldTests` (12 cases — the seam fix added
  `Continue_DoesNotWriteTheLayerTimerBeforeTheWorldFinishedGenerating` and
  `Continue_NeverMovesTheLayerTimerBackwards`), `WorldSnapshotCodecTests`, `RunClockFactsTests`,
  `NetPacketTests`, `WorldEntrySnapshotTests`.
- Normative gates 69/69 with this cycle's delivery checklist filled (matrix row R9 + six anchors; the two
  `ProtocolVersion` anchors moved 32 → 33). While the checklist is reset the gates read 68/69, that one
  red being `DeliveryChecklist_NoIncompleteRequiredBoxes`.
- Archive round trip: the encoder writes the property only when the reader returned one, and the
  decoder reads it back (or keeps the absence) — both directions pinned by tests.

## Limits

- Rows 1 and 3 read the live timer; they were judged by the agent-run acceptance batch `20261001-n`
  (record above), not by a person.
- The engine write (`WorldGeneration.world.layerTimeSpent = ...`) is adapter code with a live scene
  dependency, so it is not executed by a unit test; the seam ordering is asserted through the fake's
  recorded writes, and the real write was observed in batch `20261001-n` (`the layer timer 364.8s
  (written) — the world finished generating`).
