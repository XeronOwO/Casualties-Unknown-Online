# A member that never left the session generates before the host's restored run baseline arrives

- Status: Todo
- Priority: Medium-High
- Category: Network / sync (world entry / restore ordering)
- Source: agent attribution during the `layer-mod-baseline-divergence-on-continue` fix cycle (2026-10-02),
  from batch `20261002-k`'s F re-entry logs; not a user report
- Related: `review/layer-mod-baseline-divergence-on-continue.md` (the fixed root cause of that batch's
  divergence), `review/enemy-snapshot-binding-recovery.md` (rows 3/4/8 judge the re-entry in the same
  window), `src/CasualtiesUnknownOnline.Runtime/Session/World/WorldEntryFanout.cs`

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

## Fix direction (to be designed in its own cycle)

- Make the run-baseline carrier reach the member before its generation consumes randomness: at the
  host's re-invite edge, send the entry group (or at least the kernel checkpoint, as
  `WorldEntryFanout.Send` already orders it first) before/with the `WorldJoin`, mirroring the handshake
  path's documented order ("a reconnect's entry group precedes the join"). The member's next generation
  boundary then applies the restored params, exactly as it already does for a reconnect.
- An announcement/barrier alone (e.g. the `WorldJoin` carrying the host's revision and the member holding
  until its kernel reaches it) cannot close this by itself: the host must send the state the member waits
  for, so the send-order half is required either way.
- Whichever shape lands needs its own red: a member that stayed in the lobby while the host restored a
  state it never had must generate the host's world. The runtime staging is the F shape with the member
  staying connected across a host layer advance or a page-chosen restore.

## Limits

- No code yet; the evidence above is the batch's own logs (member window `k-F-baseline-guest.log`, host
  window `k-F-world-host.log`), attributed on 2026-10-02.
- The adapter's invite edge (`RunCoordinator.UpdateSceneState`) is Unity-side, so a red test for the
  ordering half rests on the runtime acceptance run; a Runtime-level test can only pin the carrier's
  membership and order inside the fan-out itself.
