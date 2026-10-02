# A member's layer generation that spans the host's absence counts host-wait yields as segments

- Status: Todo
- Priority: High
- Category: Network / sync (world generation, layer-modifier identity)
- Source: agent attribution during batch `20261002-m`'s acceptance run of `reenter-baseline-adoption`
  (2026-10-02); not a user report
- Related: `done/layer-mod-baseline-divergence-on-continue.md` (the same warning line, a different
  cause), `todo/world-determinism-world-fingerprint.md` (the comparison that would catch the effect),
  `src/CasualtiesUnknownOnline.GameAdapter/WorldGen/WorldGenRandomIsolation.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/WorldGen/LayerModifierSync.cs`

## Problem (evidence)

Batch `20261002-m` left the host out of the world while both members were still generating their layer
(host `leave-world` at 19:03:05; the members' generation finished 19:03:11.478). The members' generation
wrappers kept draining the game's host-wait yields as generation segments after the world was complete:

| Client | Generation | Segments | Last recorded segment start |
|---|---|---|---|
| host | layer 1, 19:02:43.700 → 19:02:56.990 | 19 | `692FA3173CE7973949303E6E7C7FD415` |
| guest | layer 1, 19:02:58.341 → 19:03:11.478 | 51 | `D6848B6D0B46332A4A24DBC1E3CB786B` |
| alt | layer 1, 19:02:58.341 → 19:03:11.410 | 43 | (same shape) |

The real segments matched the host's byte for byte through `692FA317…` — the world itself was the host's —
but the empty tail's last segment start replaced the recorded one, so the layer-modifier replay decided
from a different state: the members' `[LayerMod] guest replay index=-1 … entryState=D6848B6D…` against
the host's `[LayerMod] enter state=692FA317… chance=40 depth=1`, `picked=none` against the host's
`picked=3 prefix=寒冷`, and both members warned `[LayerMod] baseline divergence — local segment start
D6848B6D… vs host's 692FA317… (world effects may diverge)` from 19:03:12 (repeating on the 10 s snapshot
cadence). The host's snapshot then applied the modifier authoritatively (`[LayerMod] applied host
modifier 3`), but the layer's world effects had already been generated without it on the members.

A second pass with the host present for the whole member generation reproduced `19/19/19` segments,
`entryState=E5D3D723B117268DC2F862A15C15068F` on all three clients and zero divergence — the tail is
what moves the decision.

## Fix direction (to be designed in its own cycle)

- Decide what a generation segment IS when the game's coroutine yields while waiting for the host: the
  recorded start must be the last segment that consumed randomness (or the wrapper must stop recording
  once the world is complete), not whichever yield came last.
- Fix the whole family at the wrapper: the same accounting feeds the `[GenStream]` fingerprint, the
  layer-modifier replay and every later determinism comparison, so re-derive the consumers rather than
  patching the warning.
- Its own red: a member whose generation spans the host's absence must reach the same layer-modifier
  decision state as the host, without the host's snapshot correcting it.

## Limits

- Observed once, on one staging (the host leaves while the members load). Whether a member's generation
  can span a host *reconnect* the same way is not staged.
- The census/health difference the same session showed is the enemy-binding family
  (`todo/enemy-snapshot-binding-recovery.md`); this ticket does not claim it as its own effect.
