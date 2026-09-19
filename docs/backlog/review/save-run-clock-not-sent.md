# The archived run clock base never reaches a player who joins mid-run

- Status: Review — landed 2026-09-19 with `RunFacts` (protocol 33); the end-screen rows await the
  user's dual-client pass
- Priority: Low-Medium
- Category: Persistence / save system (wire)
- Source: the S3.4a independent adversarial pass, kept as a recorded gap until the S3.4c hardening
  cycle split it out (2026-09-17); landed in the clock/layer-time cycle (2026-09-19)
- Related: `docs/architecture/save-archive-format.md` §3.4, `docs/decisions/active.md` 166/169/171/193,
  `docs/evidence/sync-coverage-matrix.md` row R9,
  `src/CasualtiesUnknownOnline.Runtime/Persistence/SaveNativeRunFields.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/World/INativeWorldFacts.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Protocol/Messages/RunFactsMsg.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/World/RunClockFactsSync.cs`

## The gap

The run clock base is captured per cut (`SaveNativeRunFields.SavedRunTime`: "the run clock base at the
cut instant (seconds), exactly the game's own `runTime` value"), and it is written back on the Continue
path only, and only by the HOST: `INativeWorldFacts.ApplyCutRunFields` is documented as "Host only: a
RESTORED cut's run clock base is waiting for the world", and the adapter's pending-run-field write ends
in `SaveSystem.savedRunTime = runTime.Value;`.

No PEER ever receives it. The wire carrier a joining guest gets is `WorldStartParams` — the RNG state,
the run settings, the world-defining fields (`BiomeOverride`, `BiomeDepth`, `TotalTraveled`) and the two
rarity multipliers — and it has no clock member, so a guest that joins a run in progress (or follows a
layer switch) keeps whatever `SaveSystem.savedRunTime` its own process held: 0 on a fresh launch.

## What landed

The carrier is a dedicated absolute message, `RunFacts` (`NetMsg.RunFacts = 140`, protocol 33), sent
in the world-entry group and the 60 s in-session repair group right after the kernel checkpoint
(`WorldEntryFanout.Send` / `SendInSessionRepair`), and stamped with the kernel run baseline's
generation — the same identity every layer-relative world report carries. `WorldStateMessageService`
stamps it from `KernelWorldGenerationSource` and refuses to send at all when no run baseline exists
(an unstamped absolute clock is exactly the value that could land on the wrong layer).

The reading half is `RunClockFactsSync`, owned by the adapter: the generation boundary (and the
world-entry edge) capture the live world through `INativeWorldFacts.CaptureRunClockFacts`, publish it
with `IWorldControl.PublishRunFacts`, and a member's own value is applied through
`INativeWorldFacts.ApplyRunFacts` — at the save slot when a world exists, otherwise held for
`TryWritePendingRunFields` at the world-entry edge.

Why NOT `WorldStartParams`/`RunState`: nothing about a layer's shape is generated from a clock, so
appending it to the generation baseline would have re-purposed that carrier as a side channel, and on
the RESTORE path the projected baseline would have carried a kernel value that disagrees with the
archive's own clock base — two carriers for one fact (decision 193).

| # | Acceptance row | What pins it |
|---|---|---|
| 1 | Host is 40 minutes into a run when a guest joins; the guest's end screen shows the run total | `RunClockFactsTests.MemberEntersWorld_ReceivesTheRunClocks` (value + stamp) and `InSessionRepair_AlsoCarriesTheRunClocks` (the swallowed-send recovery half), `NetPacketTests.RunFacts_RoundTripsTheClocksAndTheGenerationStamp`; the UI read itself is the user's dual-client pass |
| 2 | The clock keeps counting from the run's total across a layer change | `RunClockFactsSync.SettleAtGenerationBoundary` re-arms the per-world write marker and takes the boundary's new base; `RunClockFactsTests` pins the stamp the value must match |
| 3 | A sender that carries no clock leaves today's behaviour and names the absence | `RunClockFactsTests.NoCapturedClocks_SendsNothing` / `NoCommittedRun_SendsNothing`; the adapter logs the absence and writes nothing |

## Verification (machine-checked)

- Focused suites: `RunClockFactsTests` (7), `WorldRunFieldTests` (10), `WorldSnapshotCodecTests`,
  `NetPacketTests`, `WorldEntrySnapshotTests` — green.
- Normative gates 69/69 with this cycle's delivery checklist filled, after the matrix row R9 (`RunFacts`
  in the wire vocabulary index) and the six R9 evidence anchors; the two pre-existing `ProtocolVersion`
  anchors moved 32 → 33. The gates are 68/69 while the checklist is reset — that single red is
  `DeliveryChecklist_NoIncompleteRequiredBoxes`, by design.
- The monotone write rule (`SaveSystem.savedRunTime` written at most once per world, layer timer only
  when it advances) lives in the GAME layer, so it is read-only reviewed; the Runtime seam (stamp,
  send, generation relation) is machine-checked by the suite above, including the `Unknown`-stamp case
  (`RunClockFactsTests.UnknownStamp_AppliesTheLayerTimer`). Its re-arm half is HOST-only (the generation
  boundary); a guest never re-arms it and does not need to — every clock it receives is the host's own
  increasing total.

## Limits

- The engine half has no executable evidence in this repo: the capture reads a live
  `WorldGeneration.world`, so the adapter's write path is reviewed, not executed by a test.
- Row 1's UI value (the end screen's death-stats clock) needs the user's dual-client pass.
- A message whose stamp names ANOTHER layer of a run the receiver already knows still contributes its
  CLOCK (run-scoped, monotone) while its LAYER TIMER and LIMIT are dropped together (layer-scoped): the
  clock is never held hostage by a layer mismatch, and the next repair send carries the timer again. A
  stamp that cannot be compared yet (no kernel run baseline on this side) applies all three — dropping
  them would leave a joining member's radiation timer at zero until its own baseline arrives; the
  monotone write guard is the real protection, and it is not stamp-dependent.
