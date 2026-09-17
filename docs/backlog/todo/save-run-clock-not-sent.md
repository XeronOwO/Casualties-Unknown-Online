# The archived run clock base never reaches a player who joins mid-run

- Status: Todo — split out of `review/save-native-run-field-parity.md` (recorded gap 1 of that ticket's
  S3.4a adversarial pass); pre-existing, not user-reported
- Priority: Low-Medium
- Category: Persistence / save system (wire)
- Source: the S3.4a independent adversarial pass, kept as a recorded gap until the S3.4c hardening
  cycle split it out (2026-09-17)
- Related: `docs/architecture/save-archive-format.md` §3.4, `docs/decisions/active.md` 166/169/171,
  `src/CasualtiesUnknownOnline.Runtime/Persistence/SaveNativeRunFields.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/World/INativeWorldFacts.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/World/WorldStartParams.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/World/NativeWorldFacts.cs`

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

## The effect

The game has exactly one reader:

- `WorldGeneration.TotalRunTime()` returns `SaveSystem.savedRunTime + Time.timeSinceLevelLoad`
  (`reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs:177-179`);
- its only caller is the END SCREEN's run-time readout, the death-stats text
  (`reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:2324`:
  `TimeSpan.FromSeconds((double)WorldGeneration.TotalRunTime()).ToString("hh\\:mm\\:ss")`).

So a guest that joins a run already in progress reads only the time since it joined, while the host
reads the run's total. (The S3.4a note said "pause/tooltip/death-stat clock"; the end screen is the one
the decompiled tree actually shows — the correction is evidence-based, not a behavior change.)

## Decided home (recorded by the S3.4a pass)

The run baseline, or a small absolute message at the world-entry fan-out. It is deliberately NOT in
`WorldStartParams`' generation group in the sense that matters: nothing GENERATES from the clock, so a
carrier that only exists to inform has to be chosen rather than appended to the fields a peer derives a
layer from. Both options are open; the decision belongs to the implementation that lands it, and the
generation inputs must not be re-purposed as a general side channel.

## Scope

1. Choose the carrier (a run-baseline member that is written at the same seam as the other run values,
   or one small absolute world-entry message) and record why in the ticket that lands it.
2. Additive wire member plus a `ProtocolVersion` bump — the repo's rule for a new wire field.
3. Write it at the seam where a live world exists (the same slot the restore uses today: the write must
   land before the first `TotalRunTime()` read derives from it).
4. Degradation: a sender that carries none leaves today's behavior (the guest keeps its own value), and
   the absence is named in the log once — never a guessed clock.

## Acceptance

| # | Scenario | Expected |
|---|---|---|
| 1 | Host is 40 minutes into a run when a guest joins | The guest's end screen shows the same total run time (within the join latency) |
| 2 | The guest and host advance a layer together | The clock keeps counting from the run's total, and never restarts per layer |
| 3 | An older sender that carries no clock | Today's behavior, with the absence named once in the log |

## Verification limits

Rows 1-2 read a game UI value, so they need the user's dual-client pass. Machine-checkable: the wire
round trip, the "absent stays absent" degradation, and the seam ordering (the value is written before
anything derives from it).
