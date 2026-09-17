# Layer time accounting is not carried by the archive

- Status: Todo — split out of `review/save-native-run-field-parity.md` (recorded gap 2 of that ticket's
  S3.4a adversarial pass, kept there as "decide with S3.5" — the S4 stage closed without an S3.5)
- Priority: Low-Medium
- Category: Persistence / save system (game state no CUO domain owns)
- Source: the S3.4a independent adversarial pass; pre-existing and native-parity-consistent
- Related: `docs/architecture/save-archive-format.md` §3.4, `docs/decisions/active.md` 166/169/171,
  `src/CasualtiesUnknownOnline.Runtime/Persistence/SaveNativeRunFields.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/Patches/WorldGenerationUpdatePatch.cs`

## The gap

`layerTimeSpent` and `maxTimePerLayer` are per-layer game state that no CUO domain owns and no archive
row carries. Continuing into the SAME layer therefore restarts the radiation line's timer and hands the
player a fresh `timelimit`.

The NATIVE save does not carry them either: the native write puts only
`saveInfo.runTime = SaveSystem.savedRunTime + WorldGeneration.world.realTimeElapsed;` into its run
record (`reversing/Assembly-CSharp/Assembly-CSharp/SaveSystem.cs:165`), so a native continue restarts
the same timer. That is why this is a parity/design question rather than a defect CUO introduced.

The only place CUO touches the pair today is the generation update patch, which documents the single
reader and clamps the value:
`WorldGenerationUpdatePatch.cs` — "layerTimeSpent is otherwise consumed only by the line condition" and
`__instance.layerTimeSpent = Mathf.Min(__instance.layerTimeSpent, __instance.maxTimePerLayer);`.

## The decision to make (user-facing)

Two defensible behaviors, and they differ in what the player gets:

- **Resume** — the restored layer keeps the time it had already spent, so the radiation line's warning
  and the remaining `timelimit` continue from where the interrupted run stood. Better than native, and
  the reason to archive the pair at all.
- **Restart** — exactly what a native continue does today. Nothing new is archived.

Because this changes what the player experiences in a continued run (a shorter or longer window before
the line activates), the choice is asked, not assumed.

## Decided home

`run.json`'s `native-run-fields` row — the same row the clock base and the recipe table already use,
because this is game state no CUO domain owns. The pair is written/read at the cut and at the restore,
never as a generation input.

## Scope

1. Get the user's decision on resume vs. restart (one question, with the native behavior stated).
2. If resume: add the two values to the native-run-fields row (additive archive member + the "the row
   carries none" restore-report entry), and write them at the world-entry seam where a live world
   exists — the same seam the recipe unlocks use, because the world must be generated before it can take
   a layer timer.
3. If restart: record the decision here and close the ticket; the restore report then names the pair as
   deliberately NOT carried rather than leaving it unnamed.

## Acceptance

| # | Scenario | Expected (resume) |
|---|---|---|
| 1 | Continue into the same layer with 6 of 10 minutes already spent | The layer's timer starts at 6 minutes, not 0 |
| 2 | Continue a cut whose archive carries no layer-time row | The restore report names the field; the timer restarts, and the log says why |
| 3 | Layer advance after a restore | The next layer keeps the game's own accounting (the pair is per layer, not per run) |

## Verification limits

Rows 1 and 3 read the in-game radiation line and timer, so they need the user's pass. Machine-checkable:
the archive round trip (including the absent row) and the seam ordering.
