# The host decides enemy hits on remote players

- Status: Review
- Priority: High
- Category: Network / sync coverage / enemies (hit-determination authority)
- Source: User ruling 2026-09-18 (design alignment session): hit determination must run on the victim's client. A host-side verdict punishes high-latency players — damage arrives after the victim's own screen showed a dodge, and several hits can land inside one round trip. Gameplay experience is the highest standard. Supersedes `resolved/enemy-attack-delivery-recovery.md`.
- Related: `review/enemy-snapshot-binding-recovery.md` (the binding the victim's own judgment depends on), `review/remote-interaction-local-gating.md` (the same authority rule for interaction gates), `todo/world-time-local-initiation.md` (the same rule for a shared world clock)

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

## Landed (2026-09-18)

- **The host announces, the victim judges.** `EnemyAttackMsg` is now
  `{EnemyId, Kind, AttackSeq}` — the host-chosen `VictimSteamId` and `LimbIndex` are gone —
  and `EnemySyncService.SendEnemyAttack(NetworkEntityId enemyId, EnemyAttackKind kind)`
  broadcasts it to every in-world guest (`PacketSender.SendToAll` via
  `InWorldGuestSteamIds`). The host still owns WHEN the enemy acts; it no longer answers
  who was hit or with which limb, so one attack reaching two remote players is two local
  judgments, and a spider lunging at air is a legal outcome.
- **One attack, one application, without a window.** The host stamps a per-enemy monotonic
  `AttackSeq` (a counter per enemy id, cleared with the session) and the judging client
  keeps the highest judged sequence per enemy in `EnemyAttackLedger`; a repeat, a reorder
  and a malformed `0` all fail closed. No latency value and no tolerance window enter the
  decision — the identity does the deduplication.
- **The judgment is the local picture.** `EnemyAttackLocalProbe` observes what THIS screen
  shows — the frozen enemy copy's colliders against this body's limb colliders, and the
  crystal's own ray — and `EnemyAttackJudgment` (Runtime, pure) answers:
  - a bite needs a real collider contact plus the game's facing gate
    (`SpiderHandler.minVectorDotToBite`), with the limb resolved the way
    `Body.LimbFromObject` does it (a Limb collider maps to itself, the Body collider to its
    closest limb — Body.cs:3688) and the gate applied once to that limb
    (`SpiderHandler.CheckForLimbDamage`, SpiderHandler.cs:180-210);
  - a lunge runs the game's own ray, `Physics2D.RaycastAll(crystal.position, crystal.up)`:
    the FIRST body it meets takes the damage and the first ground hit stops it
    (`CrystalEnemy.Lunge`, CrystalEnemy.cs:133-165), and the limb is the native random
    non-dismembered pick (CrystalEnemy.cs:141) instead of the host's index.
- **The host's own body is untouched — and it never vetoes a guest.** The local body keeps
  the native collision path (the game damages it and writes its own cooldown), and that
  branch mirrors nothing. The announcement is edge-triggered per spider
  (`EnemyBiteAnnouncementState`): it fires once while the game's own gate holds (cooldown
  open, no stun, a player inside the bite range) and the latch clears when that gate closes,
  so the cadence follows the enemy's own cooldown instead of the frame rate. Announcing is
  never conditional on the host's own collider: "the nearest candidate is the host's body"
  says nothing about whether a guest's screen shows the bite connecting, so the announcement
  goes out either way. `EnemyCombatDirector` still mirrors `CheckForLimbDamage`'s retreat +
  cooldown for a remote candidate, so the host's spider backs off exactly as before.
- **Dead host-side verdict machinery deleted in the same round.**
  `EnemyCombatArbitration.SelectLungeVictim`, `EnemyCombatOrderPolicy.DecideSpiderBite` /
  `DecideCrystalLunge` (+ `ApplyPath.RemoteOrder`),
  `EnemyTargetResolver.SelectLimbIndex` / `BodyLimbIndex`,
  `EnemyCombatPolicy.CrystalRayLength` / `CrystalRayTolerance` and the director's
  `FirstGroundDistance` helper are gone; the target resolver now owns only the host's view
  of who is where.
- **Wire identity.** `ProtocolVersion.Current` 23 → 24 (with its version list), and the
  `NetMsg.EnemyAttack` / handler / message docs now say announcement, not order.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | High-latency victim dodges on its own screen | No damage (the local judgment sees no connection) |
| 2 | The victim's screen shows the hit connect | Damage lands, even when the host's view says the enemy has already moved past |
| 3 | One attack, notification repeated/reordered | Applied at most once |
| 4 | Host's own body is the victim | Native path unchanged |
| 5 | Third-party view | Every peer agrees on the victim's post-attack limb state (the existing report/broadcast chain) |
| 6 | The enemy's action itself | Still host-simulated and animated; a guest never announces an attack |
| 7 | Two remote victims in one attack | Each victim judges its own hit |
| 8 | Proximity-effect family (thornback / septic / grabber) | Unchanged (already locally applied and reported) |
| 9 | The host's own body is the nearest in-range candidate but is NOT in contact | Every guest still receives the announcement and judges its own screen; only the local body's native path stays untouched |

## Verification

**Rule-level (L0).** `EnemyAttackJudgmentTests` pins the bite rule (a non-touching limb is
never bitten, the nearest touching limb wins, the facing gate blocks a limb the spider has
its back to, the gate is strict at equality, a limb exactly on the enemy counts as faced)
and the lunge rule (the first body wins, the ground in front ends it, another body first is
a miss, no body is a miss). `EnemyAttackLedgerTests` pins first/repeat/stale/newer
sequences, per-enemy independence, the epoch-bearing id and the fail-closed `0`.

**Wire simulation.** `EnemyAttackSyncTests` pins the roundtrip (`EnemyId`, `Kind`,
`AttackSeq`) and the broadcast itself: both in-world guests receive every announcement with
the same per-enemy identity sequence (1, 2 for one enemy, 1 for the next), and a guest that
is not in world receives nothing. `EnemyCombatArbitrationTests` keeps the host-side
targeting/bite-action gates that still exist; `EnemyCombatOrderPolicyTests` keeps the
item-hit fallback.

**Independent adversarial review (fresh context, FULL tier, against this frozen tree).**
See the cycle's review report; findings fixed in the same commit.

**Gates and suite.** Normative gates 56/56; whole solution green in a building run
(`dotnet test CasualtiesUnknownOnline.slnx`, never `--no-build`); `dotnet format` clean;
build 0 warnings / 0 errors.

**Evidence.** `docs/evidence/sync-coverage-matrix.md` row N1 rewritten for the landed
attack half; `docs/evidence/sync-coverage-evidence.json` quotes re-pointed at the code
that replaced them.

### Acceptance matrix coverage

| # | Scenario | Covered by |
|---|---|---|
| 1 | High-latency victim dodges on its own screen | NOT machine-provable here: the judgment is geometric against the client's own displayed copy, so a dodge that clears the contact is a miss by construction (`SelectBittenLimb` requires touching; `LungeHitsLocalBody` requires the ray to reach the body). The frame-level feel is the user's dual-client pass |
| 2 | The victim's screen shows the hit connect | `SelectBittenLimb_TouchingLimbInFront_IsBitten`; the host's own view no longer participates in the verdict (no `VictimSteamId` on the wire) |
| 3 | One attack, notification repeated/reordered | `EnemyAttackLedgerTests` (repeat, stale reorder, fail-closed zero) |
| 4 | Host's own body is the victim | Native path unchanged: `TryAnnounceSpiderBite` announces without touching the bite cooldown/retreat the game writes itself, and the director's local-branch guard only fires while the host's own contact is showing |
| 5 | Third-party view | Unchanged chain: the victim's `EnemyBite` / `EnemyLunge` terminal report rides the existing kernel projection to every peer |
| 6 | The enemy's action itself | `EnemyCombatArbitrationTests` + the director: aiming still runs on the host (`SelectNearest`, `CrystalCloseRange`), and a guest can never announce (broadcast is host-only, `FireEnemyAttackReceived` is guest-only) |
| 7 | Two remote victims in one attack | `Announcement_ReachesEveryInWorldGuest_WithAPerEnemyIdentity` — the broadcast means every guest judges its own screen; the host names none of them |
| 8 | Proximity-effect family (thornback / septic / grabber) | Untouched by this change (`EnemyProximitySync` / `EnemyEffectMsg` path) |
| 9 | The host's own body is the nearest in-range candidate but not in contact | Pinned by removing the probe gate from the announcement path (the interim adversarial review's M1 finding: it made the announcement conditional on the host's own contact and dropped it for every guest). The branch is Unity-side, so this row rests on the code read plus the acceptance pass; the broadcast that carries it is wire-tested |

## Known limitations (recorded, not hidden)

- The judgment runs against the client's own displayed (interpolated) copy of the enemy and
  its own body. Two lags therefore meet — the host's picture of a remote player's position
  and the guest's interpolation buffer — so a bite lands when the copy is on top of the
  victim on the victim's own screen. That is the ruling's semantics, and its frame-level
  feel is observable only in the dual-client acceptance pass.
- The probe reads live physics (collider distance, raycast); the L0 tests cover the RULE,
  not the probe. The Unity boundary stays behind the existing patch/field contracts.
- "The guest's frozen enemy copy keeps queryable colliders" is taken from code evidence
  (`EnemySyncCoordinator.RuntimeSpawns.Freeze` sets only `RigidbodyType2D.Static`, unlike
  `RemoteBodyFactory`, which explicitly disables the player clones' colliders); in-game
  confirmation belongs to the acceptance pass.
- `EnemyAttackLedger` never evicts: safe by construction — the enemy id carries the session's
  epoch and the id counter never resets inside a session, so an id can only be reused across
  sessions, and a new session starts with a fresh ledger.
- With no in-world guest, `SendEnemyAttack` sends nothing and stamps no identity — nobody was
  there to judge, which is the same no-divergence reasoning as a lost announcement.
- `EnemyAttackHandler` keeps the accept-first pattern of its neighbours: it checks the local
  role and not the sender, which the star topology makes sufficient (guests never address each
  other, and the direction table rejects this message on a host).
- `EnemyAttackLocalProbe` resolves the bitten limb from EVERY limb and lets the rule pick the
  nearest touching one, while the game resolves it from the single collision object Unity hands
  it; with several limbs in simultaneous contact the two can pick different limbs of the same
  body (recorded in the probe's own doc).
- `EnemyAttack` still has no re-issue path: a lost announcement costs one attack. Nothing
  diverges (the local screen is the only judge and the terminal report is unchanged), so the
  sync-coverage matrix records it as an accepted loss — row N1's verdict is `OK` and its gap-list
  entry is CLOSED 2026-09-18 — rather than as an open gap.
