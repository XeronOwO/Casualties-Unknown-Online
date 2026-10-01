# Acceptance record — Steam send-limit refusal handling (batch 20261001-s)

- Ticket: `steam-transport-send-limit-runaway` — verdict: **stays in `review/`** (row 5 remains
  `unproven`; this run narrowed the trigger gap and re-observed row 6 live)
- Batch: `20261001-s` (one host + one guest, two client launches on the same deployment)
- Commit: `1b088511` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+1b088511d555cf09ca19b336c2f3715f94c7df4e` (`tools/verify-deploy.ps1` exit 0, "Deployment
  matches this tree's build output"; tree `6fa991a8` is the docs-only commit after the build)
- Run: 2026-10-01 17:17 → 17:35 · Host: physical machine (Steam) · Guest: the primary sandbox
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `logs`, `artifacts` (preflight:
  11 present, exit 0); the third client was not needed
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 5 | Host escalation | machine + residual | **unproven** | none of the three live injections below produced a ≥ 30 s continuous refusal episode; the policy half stays unit-pinned and the session half integration-tested |
| 6 | Guest convergence | machine | **pass**, re-observed | the guest's own log carries `No frame from the host <host-id> for 15031 ms — ending the session locally.` 15 s after the pump-stop injection, and the guest's session then read `active: false` while the host's stayed active |

Rows 1–4 and 7 were judged in the ticket's first record (`…-20261001-r.md`) and are not re-run here;
this run's pre-injection phase again showed zero refusals on an ordinary session.

## What this run tried — load first, then three reversible injections

The starting point was batch r's own next step: reproduce Run E's dense shape with the game's own
entity factory (`Utils.Create` = `Object.Instantiate(Resources.Load(id))`, `reversing/` `Utils.cs`) and
then repeat the non-draining-peer injection. The load used an animal prefab the world itself
distributes (`shadecrawler`), spawned in rings around the host body by the in-process evaluator.

- **Load**: five spawn batches (500 objects) brought the host's animal census to **589**
  (`BuildingEntity.animal`, the product's own enumeration predicate). The host's `[NetworkTraffic]`
  window then read **≈197–248 kB/s** send (enemy stream p50 ≈ 46–70 kB per frame) and the guest's own
  window read **≈209–242 kB/s** receive — the guest drained everything the host sent.
- **Injection 1 — organic**: 60 s of dense load with both clients in the world: **0** refusals.
- **Injection 2 — the guest's transport pump stopped** (`SteamService.IsInitialized` flipped through
  its backing field, the batch-r probe pair): the host went on sending (≈231 kB/s; the peer's receive
  counter read 0) for **100 s** with **0** `k_EResultLimitExceeded`. The only observable effect was
  the guest's silence watchdog ending the guest's session after 15 s (row 6's line above); the host
  never learned the guest had left and kept sending to it.
- **Injection 3 — guest CPU starvation**: the guest's process was pinned to two, then one, of the
  machine's twenty logical cores while the dense load ran. The guest stayed `active: true` /
  `inWorld: true`, kept receiving ≈209 kB/s, and the host again saw **0** refusals over ~2 minutes.

The first session (482 animals) additionally reproduced the known rejoin wedge: after the pump-stop
ended the guest's session, `join-lobby` returned a session that never re-activated, so the run closed
both clients and relaunched a clean session for injection 3.

Every step was single-stepped with a checkpoint (deployment identity, both ends in world, load census,
send/receive rate, host log growth, refusal count); the host log grew from 68 kB at baseline to
≈369 kB, far below the 50 MB abort card. All injections were restored (pump on, affinity back to every
core) and both clients left through the driver's `quit` with no orphan processes.

## What the run proved about the trigger (why row 5 stayed open)

1. **A peer that stops draining does not back-pressure a healthy Steam link.** With the guest's
   transport pump off the host pushed >20 MB into that peer with no refusal at all — the send path
   accepted every message — and the guest's own 15 s silence watchdog ended that session first. The
   state row 5 needs (a peer still connected while its queue stays full for 30 s) is not produced by an
   application that stops reading.
2. **The host's own send rate is bounded below the Run E condition.** The adaptive profiles cap the
   streams that carry the load (`EnemyStateBroadcast` at 256 kB/s), so a 589-animal world plateaus at
   ≈200–250 kB/s — an order of magnitude under the ≈1.5 MB/s the ticket's Run E condition names — and
   that rate sits inside the guest's drain capacity.
3. **CPU starvation is not the lever either**: one logical core was still enough for the guest to
   drain ≈200 kB/s while staying in the world.

## Limits

- Row 5's trigger was not produced: the evidence names why these three injections fail on this
  machine, not that the escalation is unreachable everywhere. A peer on a genuinely saturated link, or
  one blocked at the transport level, could still reach it.
- The pump-stop injection also stops the guest's own sends, so its own stall path is not separable
  from its silence path in that observation.
- A rare race is not excluded by two sessions; rows are judged only from this run's evidence.
