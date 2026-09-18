# A dropped enemy attack is never re-issued

- Status: Todo
- Priority: Medium
- Category: Network / sync coverage / enemies (host-ordered attack delivery)
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` row N1, verdict `Event-only gap`); split from the former `enemy-snapshot-and-attack-recovery` umbrella — the binding half is `review/enemy-snapshot-binding-recovery.md`
- Related: `review/enemy-snapshot-binding-recovery.md`, `review/runtime-entity-spawn-backfill.md`

## Problem (evidence)

An attack on a remote victim is a host-ordered one-shot: the host's enemy simulation decides, the
victim's client applies it to its own body and reports the terminal state back (the `EnemyBite` /
`EnemyLunge` kernel events). The order is dropped on two paths and nothing re-issues it.

- Host side, the victim is not an in-world member:
  `src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/EnemySyncService.cs` `SendEnemyAttack`
  logs "victim {Victim} is not an in-world member — command dropped." and returns. The decision is
  discarded; no retry record exists.
- Victim side, the enemy id is not bound:
  `src/CasualtiesUnknownOnline.GameAdapter/Character/EnemyCombatReplay.cs`
  `OnEnemyAttackReceived` logs "attack {Kind} arrived for unknown enemy {Enemy} — the snapshot
  binding may not have arrived yet; command dropped." (the binding half of this dependency is
  `review/enemy-snapshot-binding-recovery.md`: while the binding is missing, EVERY ordered attack is
  dropped, which is what makes the loss permanent rather than occasional).
- The message is documented as final: `EnemyAttackMsg` — "Reliable — the command is one-shot" —
  and the host consumes the enemy's attack immediately after ordering it:
  `src/CasualtiesUnknownOnline.GameAdapter/Character/EnemyCombatDirector.cs` `TryOrderSpiderBite`
  mirrors `CheckForLimbDamage`'s post-bite retreat + cooldown write "so the host spider backs off
  exactly like after a native bite and cannot double-order during the retreat". So the host's
  simulation believes the bite happened while the victim's body never took it.
- The crystal lunge is the sharpest case: `OnCrystalLungeBegin`'s `ApplyPath.RemoteOrder` branch
  orders the lunge and returns; the enemy lunges once, so a dropped order is permanently lost —
  there is no second decision to re-issue it.

## Goal

A dropped `EnemyAttack` does not leave the host's simulation and the victim's body permanently
disagreeing. Either the order is re-issued, or the loss is explicitly accepted with a recorded
reason and a bounded consequence in the matrix row.

## Design direction (decide at implementation)

1. **Do not consume an undelivered order.** Make the send's outcome explicit and mirror the
   retreat/cooldown (spider) or the wind-up commitment (crystal) only when the order actually
   left; the next AI decision then re-issues it. This is the no-wire-change option the audit
   prefers. It must be checked against the per-frame re-order / log-spam risk while a victim is out
   of world or still unbound.
2. **Or make the command idempotent with an ack/retry.** The victim's bite report already exists as
   a natural ack, but the command carries no attempt identity, so a blind retry could double-apply
   a bite; that needs a wire member.
3. **Or accept the loss explicitly.** If a case is accepted, record the concrete bound in the
   matrix row instead of leaving it implicit.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 7 | `EnemyAttack` dropped (victim not in world / enemy id unbound) | The victim does not stay permanently unbitten; either re-issued or explicitly accepted with a recorded reason |
| 7b | Third-party view | All peers agree on the victim's post-attack state |

## Non-goals

- Enemy AI / combat policy changes.
- The enemy binding half (`review/enemy-snapshot-binding-recovery.md`).
