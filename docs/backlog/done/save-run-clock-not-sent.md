# The archived run clock base never reaches a player who joins mid-run

- Status: Done
- Acceptance (20261001-o): row 1 FAILED the live comparison the batch plan names, row 2 passed and the
  suite row 3 passed — record `../evidence/acceptance/save-run-clock-not-sent-20261001-o.md`. The
  diagnosis was the publish POINT, not the value: a mid-world joiner took the host's last published base
  while its own counter starts at its own entry, and the repair group re-sent that same absolute value.
  The fix landed in this cycle: the value is re-read at every send and mapped onto the receiving world's
  epoch, so a joiner reads the host's total as of its own entry and a repair re-send is
  idempotent up to the two sends' transport jitter; row 1 awaits the re-run.
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
| 1 | Host is 40 minutes into a run when a guest joins; the guest's end screen shows the run total | `RunClockSendPointFreshnessTests.WorldEntry_ReReadsTheLiveClock_SoAMidRunJoinerGetsTheCurrentTotal`, `.InSessionRepair_ReReadsTheLiveClock_TheSameWay`, `.AReadThatFails_SendsNothingRatherThanAStaleTotal`, `RunClockFactsTests.MemberEntersWorld_ReceivesTheRunClocks` (value + stamp) and `.InSessionRepair_AlsoCarriesTheRunClocks`, `NetPacketTests.RunFacts_RoundTripsTheClocksAndTheGenerationStamp`; the UI read itself is the user's dual-client pass |
| 2 | The clock keeps counting from the run's total across a layer change | `RunClockFactsSync.SettleAtGenerationBoundary` re-arms the per-world write marker and takes the boundary's new base; `RunClockFactsTests` pins the stamp the value must match |
| 3 | A sender that carries no clock leaves today's behaviour and names the absence | `RunClockFactsTests.NoCapturedClocks_SendsNothing` / `NoCommittedRun_SendsNothing`; the adapter logs the absence and writes nothing |

## The row-1 fix (send-point read + receiver mapping)

- **The value is read at the SEND point.** `WorldStateMessageService.SendRunFacts` re-reads the live
  world through the existing `INativeWorldFacts.CaptureRunClockFacts` port on every send — the entry
  group and the 60 s repair group alike — instead of sending the value captured at the last generation
  boundary/world entry. A read that fails sends NOTHING (the receiver maps the total onto its own world
  epoch, so a stale total would be mapped as if it described the current moment); a Runtime-only
  composition with no native port keeps sending its published value, as before.
- **The receiver maps the total onto its own world epoch.** The write in `NativeWorldFacts` takes
  `sentTotal - world.realTimeElapsed`, because the game's display derives
  `SaveSystem.savedRunTime + realTimeElapsed` and the receiving world started its counter at its own
  entry. A member that enters mid-run therefore writes the host's total as of its own entry; a repair
  re-send of a LATER total maps back onto the same base up to the two sends' transport jitter
  (sub-second), so the monotone guard keeps what is there and a re-send can never jump the clock by the
  interval it used to — instead of either being stuck (the old equal value) or making a forward jump (a
  fresh absolute value written verbatim would overcount by the member's own elapsed).
- `ProtocolVersion.Current` is bumped in the same change with its per-number log entry; decision 193,
  the sync-coverage matrix's R9 row and both protocol-message reference rows follow.

## Verification (machine-checked)

- Row 1's fix: the three `RunClockSendPointFreshnessTests` cases were RED against HEAD's send path (the
  entry and repair groups carried the last published 100 s while the live world held 130 s, and a failed
  live read still sent the stale value) and green after it, with the existing clock suite unchanged.
  This cycle's focused run, the source for its own count:
  `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~StartingSupply|FullyQualifiedName~RunClock|FullyQualifiedName~WorldRunFieldTests|FullyQualifiedName~WorldEntrySnapshot|FullyQualifiedName~ReconnectWorldSnapshot|FullyQualifiedName~TrapLayoutEntryFreshnessTests|FullyQualifiedName~CommandConsoleSaveTests"`
  → 112 passed, 0 failed (the clock classes in it are `RunClockFactsTests` 7 + `RunClockSendPointFreshnessTests` 3;
  the rest are the sibling supplies fix and the entry/console neighbours it shares a seam with).
- Full solution run the same cycle, WITH build: `dotnet test CasualtiesUnknownOnline.slnx` → 4554 passed,
  0 failed (the NormativeGates project is 300 of that total), up from the previous cycle's 4548 by exactly
  the six cases this cycle adds.
- Focused suites: `RunClockFactsTests` (7), `WorldRunFieldTests` (12 — the layer-timer seam added
  `Continue_DoesNotWriteTheLayerTimerBeforeTheWorldFinishedGenerating` and
  `Continue_NeverMovesTheLayerTimerBackwards`), `WorldSnapshotCodecTests`,
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

- The engine half has no executable evidence in this repo: the capture and the receiver's epoch mapping
  both read the live `WorldGeneration.world`, so that half of the adapter is reviewed, not executed by a
  test — the dual-client re-run is its proof. The Runtime half (the send-point read, the wire value, its
  stamp and the failure paths) is machine-checked by the suite above.
- Row 1's UI value (the end screen's death-stats clock) needs the user's dual-client pass.
- The send-point read refreshes the LAYER timer with the same capture, so a member entering a new layer
  is now sent the host's CURRENT world's timer instead of the boundary capture's old-layer value; the
  receiver's advance-only write makes that the host's own accounting, but the layer-change half was NOT
  measured live in this cycle — the dual-client run should read both sides' radiation timer across one
  descent as well as the clock.
- A message whose stamp names ANOTHER layer of a run the receiver already knows still contributes its
  CLOCK (run-scoped, monotone) while its LAYER TIMER and LIMIT are dropped together (layer-scoped): the
  clock is never held hostage by a layer mismatch, and the next repair send carries the timer again. A
  stamp that cannot be compared yet (no kernel run baseline on this side) applies all three — dropping
  them would leave a joining member's radiation timer at zero until its own baseline arrives; the
  monotone write guard is the real protection, and it is not stamp-dependent.

## Acceptance — batch 20261001-q (Run E2 host + guest)

Run record:
[save-run-clock-not-sent-20261001-q.md](../../evidence/acceptance/save-run-clock-not-sent-20261001-q.md).
The mid-run join was driven for real (the guest left the lobby and rejoined the host's in-progress run,
host total ≈ 76 s at its entry): before the entry group's `RunFacts` the joiner sat on its own counter
(host `76.11` vs guest `10.64`), the entry-group send mapped the host's live total onto the receiver's
epoch (`the live world took the run clock base 66.0s (written)`), and the settled pairs read within
**+3.4 s** (three samples over 55 s, constant). The layer-change half then paired **Δ0.067 s** (host
`215.1655` vs guest `215.2321`) with the layer timers `5.194`/`5.103` — the member holds the host's
current layer's timer, not the replaced layer's `137.0 s`. Row 3 stays suite-pinned. Residual: the
end-screen death-stats text is for a person to read.
