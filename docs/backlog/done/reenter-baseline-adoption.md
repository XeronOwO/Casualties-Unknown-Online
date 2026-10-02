# A member that never left the session generates before the host's restored run baseline arrives

- Status: Done
- Priority: Medium-High
- Category: Network / sync (world entry / restore ordering)
- Source: agent attribution during the `layer-mod-baseline-divergence-on-continue` fix cycle (2026-10-02),
  from batch `20261002-k`'s F re-entry logs; not a user report
- Related: `done/layer-mod-baseline-divergence-on-continue.md` (the fixed root cause of that batch's
  divergence), `done/enemy-snapshot-binding-recovery.md` (rows 3/4/8 judge the re-entry in the same
  window), `src/CasualtiesUnknownOnline.Runtime/Session/World/WorldStateMessageService.cs`
- Acceptance: pending — the three-client staging runs against the fix commit's deployed artifact and is
  recorded under `docs/evidence/acceptance/`; this ticket moves to `done/` in that change.

## Problem (evidence)

Batch `20261002-k`, F scenario: both members left the world but stayed in the lobby, the host left and
Continued, then the host's world-entry edge invited them back. The members regenerated their world from
their own last run baseline before the host's restored state reached them:

| Time (+08) | Host | Member |
|---|---|---|
| 16:59:12 | Continue click; the restored baseline is applied | — |
| 16:59:13 | generation stream reset to the restored baseline | — |
| 16:59:28.7 | world-entry edge → invite `WorldJoin` to members not in world | — |
| 16:59:29.5 | — | world params applied; generation stream reset to the member's own last baseline, generation runs (`[LayerMod] guest replay index=-1 depth=0`) |
| ~16:59:44 | (its generation finished) | generation finished |
| 16:59:45.9 | — | first `Projected kernel run baseline (run 1, layer 1)` + `Restored kernel checkpoint` — the entry group's checkpoint arrived only now |

The ordering is structural, not a one-off race: the host sends the entry group — which carries the run
baseline FIRST (`WorldEntryFanout.Send`) — only when it observes the member's InWorld edge
(`SceneStateHandler`), i.e. after that member's generation. The member's generation boundary waits for
params only while it holds none (`WorldParamsService.EnsureGuestApplied`), and a member that was already
in the session holds its previous run's params. The handshake path does send the entry group before the
join (`HandshakeHandler`), which is why a *reconnect* (a member that also left the lobby) does adopt the
restored baseline; the gap is the member that stayed connected.

Consequence: whenever the host's restored state differs from the member's last baseline — a restore of
another world or archive chosen in the Worlds page, or a layer advance the member missed — the member
regenerates its own world and no snapshot repair can reconcile the two. The layer-modifier baseline
warning is the visible symptom. In batch `20261002-k` the Continue-pointer fix made the two baselines
coincide again, so this did not fire after it; the general guarantee is still missing.

## Fix (landed 2026-10-02)

The enter-the-world instruction now carries its own baseline delivery:

- **The invite edge sends the carrier.** `WorldStateMessageService.SendWorldJoin` / `SendWorldJoinTo`
  send the `WorldJoin` instruction first and, when the host holds a run
  (`KernelWorldGenerationSource.Current`), the run-baseline checkpoint set immediately after it, once
  per invited member. `WorldJoinMsg.RunBaselineFollows` (protocol 44 → 45) is the promise on the wire.
  The order is load-bearing: a set may only follow the instruction that announced its run, or
  `GuestCheckpointReceiver` refuses it whenever the run epoch changed.
- **The member waits for the promised set.** On the promise the world surface drops the `WorldParams`
  it still holds (`WorldStateMessageService.FireWorldJoinReceived`), so the existing params wait in
  `WorldParamsService.EnsureGuestApplied` — polled by `WorldGenRandomIsolation.Wrap` before the
  generation coroutine may consume any Random — holds the generation until the checkpoint restore
  republishes the host's baseline. No new interface member and no parallel state machine.
- **The other join paths keep their contracts.** The handshake path still sends its entry group
  (checkpoint included) BEFORE its join and promises nothing: the baseline is already in hand. The click
  path now also delivers the checkpoint (uniform, idempotent).

Red observed first:
`ReenterBaselineAdoptionTests.WorldJoinAtTheInviteEdge_DeliversTheRestoredRunBaselineToAStayedMember` and
`ReenterBaselineAdoptionTests.WorldJoinAnnouncesTheRunBeforeTheCheckpointThatCarriesIt` fail on the
pre-fix tree (2 failed / 0 passed — the member's kernel stays on run 1 and lacks the restored run-2
state) and pass after; the class's other tests pin the dropped-params wait, a repeated invite while the
member is loading, the targeted (respawn) invite, the host's InWorld filter and the host-side role
guard. Focused 77/77; full suite 4606 + 315 gates with build.

## Acceptance

Judged in batch `20261002-m` against commit `6dc751bc` and its deployed artifact
(`0.1.0+6dc751bc…`); record: `docs/evidence/acceptance/reenter-baseline-adoption-20261002-m.md`. Rows:
the invite edge sends the baseline itself (pass); a member that stayed connected adopts the host's
restored baseline before its generation consumes anything (pass); its generation matches the host's byte
for byte (pass); the Continue re-delivers that baseline to members that are out of world (pass); the
layer-mod entry states agree with zero `baseline divergence` in a clean window (pass); the run's own
entry path is not regressed (pass). The record also carries one observation outside these rows (the
enemy census/health difference read inside the 60 s repair window) and the first pass's staging anomaly.

## Limits

- A promised set lost before the member reaches InWorld leaves its generation holding: nothing re-sends
  to a member the host does not yet count as InWorld, and both transports are reliable and ordered, so
  the promise is healed only by its own set or by the session ending.
- The host's world-entry edge invites on `InWorld` alone, so a member that is still loading can receive
  a second promise: it drops the params again and the follow-up set restores (pinned by the repeated
  invite test). The click path's extra checkpoint only moves timing, not the gate logic.
- Restoring the promised set projects the checkpoint's live-world facts while the member's world does
  not exist yet — the same shape a reconnect's pre-join entry group already has. The rows the empty
  scene refuses are re-projected by the world-entry restore after the generation; a guard that skipped
  those projections was tried and reverted because the established guest contract projects them
  immediately.
- Only the Runtime seam is unit-pinned. Whether the invite really precedes the member's `GenerateWorld`,
  whether the generation is held and then adopts the restored baseline, and the three-client behaviour
  are judged by the acceptance run (the adapter's `RunCoordinator` is Unity-side).
- The acceptance run's first pass also exposed a separate generation-accounting gap (a member's
  generation that spans the host's absence counts host-wait yields as segments); it is recorded as
  `done/guest-generation-segments-over-host-absence.md` and is not part of this fix.
- The pre-fix evidence is the batch's own logs (member window `k-F-baseline-guest.log`, host window
  `k-F-world-host.log`), attributed on 2026-10-02.
