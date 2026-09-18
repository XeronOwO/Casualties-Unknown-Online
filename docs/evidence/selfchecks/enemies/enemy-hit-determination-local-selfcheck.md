# Enemy hit determination — victim-side judgment self-check

Cycle: 2026-09-18 (ticket `docs/backlog/review/enemy-hit-determination-local.md`, decision 185).
Scope: the enemy ATTACK path only (`EnemyAttackMsg`, `EnemyCombatDirector`, `EnemyCombatReplay`);
the enemy stream, the enemy snapshot binding and the proximity-effect family are untouched.

| Mechanism | Change | Evidence |
|---|---|---|
| Attack delivery | `EnemyAttackMsg` is an announcement `{EnemyId, Kind, AttackSeq}` broadcast to every in-world guest; the host names no victim and no limb | `EnemySyncService.SendEnemyAttack` + `PacketSender.SendToAll`; `EnemyAttackSyncTests.Announcement_ReachesEveryInWorldGuest_WithAPerEnemyIdentity` (seq 1, 2 for one enemy, 1 for the next; both guests) |
| Identity / dedup | a per-enemy monotonic `AttackSeq` stamped by the host; the judging client keeps the highest judged value and fails closed on 0 | `EnemyAttackLedger`; `EnemyAttackLedgerTests` (first / repeat / stale / newer / zero / per-enemy / epoch) |
| Bite judgment | a real collider contact between the frozen copy and this body's limb colliders, plus the game's facing gate, with the limb resolved the way `Body.LimbFromObject` resolves it | `EnemyAttackJudgment.SelectBittenLimb` + `EnemyAttackLocalProbe.ProbeBittenLimb`; `EnemyAttackJudgmentTests` (6 rows) |
| Lunge judgment | the game's own ray `Physics2D.RaycastAll(pos, up)`: the first body wins and the ground stops it; the limb is the native random non-dismembered pick | `EnemyAttackJudgment.LungeHitsLocalBody` + the probe; `EnemyAttackJudgmentTests` (4 rows) |
| Host action kept | aiming (`SelectNearest`, `CrystalCloseRange`), the bite-action gates (cooldown / stun / range) and the post-bite retreat mirror stay; the host's own body keeps the native collision path; the bite announcement is edge-triggered per spider (`EnemyBiteAnnouncementState`) and is NEVER conditional on the host's own collider | `EnemyCombatDirector.TryAnnounceSpiderBite` / `OnCrystalLungeBegin`; `EnemyCombatArbitrationTests` |
| Deleted verdict machinery | `SelectLungeVictim`, `DecideSpiderBite`, `DecideCrystalLunge`, `ApplyPath.RemoteOrder`, `SelectLimbIndex`, `BodyLimbIndex`, `CrystalRayLength`, `CrystalRayTolerance`, `FirstGroundDistance` | the cycle's diff; `EnemyCombatPolicyTests` and `EnemyTargetResolverContractTests` updated in the same round |
| Protocol | `ProtocolVersion.Current` 23 → 24 with the version list updated; `NetMsg.EnemyAttack` and the handler/message docs say announcement | `src/CasualtiesUnknownOnline.Runtime/Protocol/ProtocolVersion.cs`; `EnemyAttack_RoundTripsTheAnnouncement` |

Verification runs (this tree, building runs, never `--no-build`):

- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings, 0 errors.
- `dotnet format CasualtiesUnknownOnline.slnx` — exit 0.
- `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName!~DeliveryChecklist"` — 3265 main + 55 gate tests green (the checklist gate is the 56th and needs this checklist complete, so the unfiltered building run is the one that proves 56/56).
- Focused family: `--filter "FullyQualifiedName~Enemy"` — 162 green.

What only the user's dual-client pass can show:

- the bite/lunge feel at the frame level. The judgment runs against the client's own interpolated
  copy of the enemy and its own body (that IS the ruling's semantics), but how it feels in a real
  two-client fight is not machine-provable here;
- that the frozen enemy copy keeps queryable colliders at runtime. That is taken from code evidence —
  `EnemySyncCoordinator.RuntimeSpawns.Freeze` sets only `RigidbodyType2D.Static`, unlike
  `RemoteBodyFactory`, which explicitly disables the player clones' colliders — and the acceptance
  pass is where it is confirmed in-game.
