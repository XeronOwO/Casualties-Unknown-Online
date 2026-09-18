# The host decides enemy hits on remote players

- Status: Todo
- Priority: High
- Category: Network / sync coverage / enemies (hit-determination authority)
- Source: User ruling 2026-09-18 (design alignment session): hit determination must run on the victim's client. A host-side verdict punishes high-latency players — damage arrives after the victim's own screen showed a dodge, and several hits can land inside one round trip. Gameplay experience is the highest standard. Supersedes `resolved/enemy-attack-delivery-recovery.md`.
- Related: `review/enemy-snapshot-binding-recovery.md` (the binding the victim's own judgment depends on), `todo/remote-interaction-local-gating.md` (the same authority rule for interaction gates), `todo/world-time-local-initiation.md` (the same rule for a shared world clock)

## Problem (evidence)

The victim's body is already locally authoritative — the damage is computed on the
victim's client and reported as a terminal fact — but the VERDICT that an attack
connects is the host's, taken on the host's own timeline and with the host's own view
of the victim's body:

- Host: `src/CasualtiesUnknownOnline.GameAdapter/Character/EnemyCombatDirector.cs`
  `TryOrderSpiderBite` selects the victim AND the limb from `EnemyTargetResolver`'s
  candidate set (the entity-stream positions, the host's stale picture of a remote
  player) and mirrors `CheckForLimbDamage`'s post-bite retreat + cooldown in the same
  frame; `OnCrystalLungeBegin` selects the victim along the host's own lunge ray and
  sends the order inside the native `Lunge` prefix. The command is documented as the
  host's decision: `src/CasualtiesUnknownOnline.Runtime/Protocol/NetMsg.cs`
  "EnemyAttack = 83, host -> guest: the host's enemy simulation decided an attack on a
  remote player".
- Victim: `src/CasualtiesUnknownOnline.GameAdapter/Character/EnemyCombatReplay.cs`
  applies the order verbatim (`ApplyHostSpiderBite` / `ApplyHostCrystalLunge`), including
  the host-chosen `msg.LimbIndex`. The victim never judges whether the attack connected
  with anything it can see; it only computes the damage of a hit someone else decided.
- The result is latency-as-a-judge: a player who dodged on their own screen still takes
  the hit, and a burst of host decisions arrives as several wounds in one round trip.

Three facts make this an OUTLIER rather than the design:

- The enemy simulation itself is host-authoritative and stays so:
  `src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/EnemySyncService.cs` "the host
  simulates the enemies (AI + physics) … never the AI internal state".
- Everything else that lands on a player's own body is already resolved locally:
  `src/CasualtiesUnknownOnline.GameAdapter/World/ExplosionBodyEffect.cs` ("a replayed
  explosion … must hit the replaying side's real body the same way", rolls included),
  `src/CasualtiesUnknownOnline.GameAdapter/Patches/TrapBarbedFencePatch.cs` (the damage
  happens on the triggering side's limb), `src/CasualtiesUnknownOnline.GameAdapter/World/RadiationLineSync.cs`
  ("body radiation/eye effects stay local per side"), and the proximity-effect family
  (`EnemySyncService.SendEnemyEffect`: the affected player's local body already applied it).
- The victim's report is already the shared truth: `RecordEnemyBiteCommand` /
  `RecordEnemyLungeCommand` carry the post-attack terminal state and every peer projects
  it, so a locally judged hit already has a correct path to the third-party view.

## Goal

An enemy attack on a remote player is judged by THAT player's client, on its own view and
its own timeline: what the victim's screen shows is what the victim's body takes. The host
keeps the enemy's action (AI, targeting intent, animation, timing) and never decides
whether the attack connected with a remote body.

## Design direction (decide at implementation)

1. The host stops sending a verdict. `EnemyAttackMsg` becomes an ATTACK notification
   (which enemy, which kind, when) or is replaced by the attack state the stream already
   carries; the host-chosen `LimbIndex` goes away — the victim picks the limb on its own
   body, exactly as the local path already does (`SelectLimb`'s fallback).
2. The victim judges the connection itself, using its own interpolated view of the enemy
   and its own body, then applies the game's own damage locally and reports the terminal
   state through the existing kernel chain.
3. One attack applies at most once: the notification carries an attack identity (or the
   judgment is edge-triggered on the attack state), so a reordered or repeated
   notification cannot double-apply.
4. An attack the victim's view never shows connecting does NOT land — and that is the
   intended semantics, not a lost command: the host's spider lunging at air is a legal
   outcome.
5. Superseded by this direction: the whole "the host consumed the attack, so re-issue or
   compensate the drop" family (`resolved/enemy-attack-delivery-recovery.md`).

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | High-latency victim dodges on its own screen | No damage (the local judgment sees no connection) |
| 2 | The victim's screen shows the hit connect | Damage lands, even when the host's view says the enemy has already moved past |
| 3 | One attack, notification repeated/reordered | Applied at most once |
| 4 | Host's own body is the victim | Native path unchanged |
| 5 | Third-party view | Every peer agrees on the victim's post-attack limb state (the existing report/broadcast chain) |
| 6 | The enemy's action itself | Still host-simulated and animated; a guest never invents an attack |
| 7 | Two remote victims in one attack | Each victim judges its own hit |
| 8 | Proximity-effect family (thornback / septic / grabber) | Unchanged (already locally applied and reported) |

## Non-goals

- Enemy AI, targeting policy, or combat thresholds.
- The enemy binding recovery (`review/enemy-snapshot-binding-recovery.md`, landed).
- Guest-side enemy simulation or any transfer of the enemy's own state authority.
