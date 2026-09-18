# A dropped enemy attack is never re-issued

- Status: Resolved
- Category: Network / sync coverage / enemies (host-ordered attack delivery)
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` row N1, verdict `Event-only gap`); split from the former `enemy-snapshot-and-attack-recovery` umbrella — the binding half landed as `review/enemy-snapshot-binding-recovery.md`
- Superseded by: `todo/enemy-hit-determination-local.md` (user ruling 2026-09-18)
- Related: `review/enemy-snapshot-binding-recovery.md`, `review/runtime-entity-spawn-backfill.md`

## Why this record is closed without code

This ticket assumed the host ORDERS the hit and the victim applies it, and asked how a
dropped order should be recovered (re-issue it, acknowledge it, or record the loss). The
user's 2026-09-18 ruling removes the premise: hit determination itself belongs on the
victim's client, because a host-side verdict punishes high-latency players (damage landing
after the victim's own screen showed a dodge, several hits inside one round trip). With the
victim judging its own hit there is no host verdict to drop, so the whole "re-issue or
compensate the lost order" family — including the design directions below — is moot.

What survives from this ticket is carried into `todo/enemy-hit-determination-local.md`:
the host keeps the enemy's action and its timing; the victim's report remains the shared
truth; the binding recovery that the victim's judgment depends on already landed
(`review/enemy-snapshot-binding-recovery.md`).

## Original problem (evidence, as recorded)

An attack on a remote victim is a host-ordered one-shot: the host's enemy simulation
decides, the victim's client applies it to its own body and reports the terminal state back
(the `EnemyBite` / `EnemyLunge` kernel events). The order is dropped on two paths and
nothing re-issues it.

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

## Original design directions (superseded)

1. Do not consume an undelivered order (the no-wire-change option the audit preferred).
2. Or make the command idempotent with an ack/retry.
3. Or accept the loss explicitly with the bound recorded in the matrix row.

## Original acceptance matrix (superseded)

| # | Scenario | Expected |
|---|---|---|
| 7 | `EnemyAttack` dropped (victim not in world / enemy id unbound) | The victim does not stay permanently unbitten; either re-issued or explicitly accepted with a recorded reason |
| 7b | Third-party view | All peers agree on the victim's post-attack state |
