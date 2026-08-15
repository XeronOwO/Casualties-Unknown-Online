# Enemy Targeting & Host-Ordered Attacks — Self-Check Table

Delivery-cycle fact sheet for the enemy-targeting fix (route A). Every touched mechanism is listed
with the change and its evidence; verification is L0/contract/simulation/smoke, not manual
dual-open acceptance.

## Mechanism × change × evidence

| # | Mechanism | Change | Evidence (file:line / test / runtime) |
|---|-----------|--------|----------------------------------------|
| 1 | SpiderHandler target discovery | Replace the host-only `Physics2D.OverlapCircle` result with the nearest in-world player on the game's own `moveTime` expiry edge | `SpiderHandler.cs:71/95`; `EnemyTargetingPatches.SpiderHandlerTargetPatch`; `EnemyCombatArbitration.SelectNearest` + `EnemyCombatArbitrationTests` |
| 2 | CrystalEnemy target discovery | Resolve the private `body` getter to the nearest in-world player body inside the game's 64-unit `close` radius | `CrystalEnemy.cs:15/25`; `EnemyTargetingPatches.CrystalEnemyBodyPatch`; `EnemyCombatArbitrationTests` |
| 3 | Spider bite on a remote victim | Host orders `EnemyAttack` (NetMsg 83) inside the 1.5-unit chase-stop radius, then mirrors the post-bite retreat/cooldown | `SpiderHandler.cs:125/146-151`; `EnemyCombatDirector.TryOrderSpiderBite`; `EnemyCombatArbitration.SelectBiteVictim` tests; `EnemyAttackSyncTests` |
| 4 | Spider bite local application | Victim applies the frozen copy's own `DamageLimb` (virtual — TBE included) plus `CheckForLimbDamage`'s non-collision side effects, then reports `EnemyBite` | `SpiderHandler.cs:148-160`; `EnemySyncCoordinator.ApplyHostSpiderBite`; `EnemyBitePatches` (base + TBE) |
| 5 | Frozen spider collision callbacks | Skip `OnCollisionStay2D`/`OnCollisionEnter2D` on guest copies — the old local bite path would race the host command and double-apply | `EnemyPatches.SpiderHandlerCollisionStayPatch/EnterPatch`; `PatchContractTests.EnemyCombatPatchSet_IsComplete` |
| 6 | Crystal lunge on a remote victim | Host orders `EnemyAttack` when the player is first along the lunge ray before the first ground hit | `CrystalEnemy.cs:133-168`; `EnemyCombatDirector.OnCrystalLunge`; `EnemyCombatArbitration.SelectLungeVictim` tests |
| 7 | Crystal lunge local application | Victim applies the exact armor-reduced damage constants + body reactions, then reports `EnemyLunge` (NetMsg 84) | `CrystalEnemy.cs:143-156`; `EnemySyncCoordinator.ApplyHostCrystalLunge`; `EnemyAttackSyncTests` |
| 8 | EnemyLunge clone fact | Apply the post-lunge limb + adrenaline/stamina to the victim's clone fact table and re-render | `CloneFactTable.ApplyEnemyLunge`; `EnemyLungeHandler` relay tests |
| 9 | Wire direction / protocol version | `EnemyAttack` = host→guest one-way; `EnemyLunge` = bidirectional; ProtocolVersion 7 | `PacketReceiver.IsValidDirection`; `DirectionTests`; `ProtocolVersion.Current` |
| 10 | Patch installability | All 101 patch targets install and verify at startup (incl. the `CrystalEnemy.get_body` getter) | Runtime smoke: `Game Adapter patches installed and verified (101 targets)` |

## Explicitly out of scope (recorded, not silent)

- `LookTarget` local gaze/scare and the `Heater` temperature field on `xaloris` remain local
  presentation — tracked in `docs/backlog.md`.
- The previously out-of-scope enemy-proximity effects, the host-local `CrystalEnemy` lunge report
  and the prefab script-mapping runtime check were closed in the next delivery cycle
  (`docs/enemy-effects-selfcheck.md`).

## Verification design

- **L0 arbitration**: 10 cases — nearest/tie/out-of-range, bite cooldown+stun+range+local-victim,
  lunge ray order/ground/off-ray/behind-origin.
- **Protocol**: `EnemyAttackMsg`/`EnemyLungeMsg` roundtrip incl. limb 0 and -1 sentinels.
- **Wire simulation**: command reaches only the victim, fires `EnemyAttackReceived`, is dropped
  for a not-in-world victim; `EnemyLunge` report relays to the other guest.
- **Patch contract**: every hook in the enemy-combat set must exist, and `SpiderHandler.Update`
  must carry both the freeze and the target-guidance patch classes.
- **Runtime smoke**: deploy + start the real game once; `LogOutput.log` must show all 101 patch
  targets installed and no CUO patch error.

## Closeout record

- Code gates: `dotnet build` 0 warnings/0 errors; `dotnet test` **636/636 green**;
  `dotnet format` clean; check-architecture / check-event-replay /
  check-entity-event-dispatch all passed.
- Deploy smoke (real game dir, post-deploy): `Game Adapter patches installed and verified
  (101 targets)` in `BepInEx/LogOutput.log` — the new SpiderHandler/CrystalEnemy target and
  attack-command hooks installed with the rest of the patch set.
- Delivery checklist: all real boxes checked line-by-line; the forbidden box remains unchecked.
