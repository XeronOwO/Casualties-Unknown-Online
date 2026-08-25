# Character attack-anim sync + in-world right-click player menu — self-check (2026-08-23)

This cycle closes two user-visible gaps found during play:

1. The player attack claw/swing/laser one-shot visual (`Body.Attack`'s
   `attackAnim` prefab) was still not appearing on peer clones.
2. Right-clicking another player in the world did nothing, because CUO had no
   in-world player interaction menu and remote render clones intentionally have
   no colliders.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| `Body.Attack` one-shot visual | `Body.cs:1913-1920` instantiates `atk.attackAnim` and parents it to the body. |
| Existing attack-swing path | `ArmsSwing` rides `PlayerEntity.IsAttacking` / `SwingSeq` through the 20 Hz entity stream; swing audio rides `CharacterSoundMsg`. |
| Remote clone colliders | `RemoteBodyFactory.CreateRemoteBody` disables all clone colliders (`RemoteBodyFactory.cs:82-90`) — the game's `Physics2D.OverlapPoint` player-hover path cannot see a remote. |
| Existing direct interactions | Take/Carry/Heal/Recruit already have host-authoritative Runtime domains and are projected by `OnlineUiMemberProjection` for the Players page. |
| Wire registry | Every new NetMsg must be explicitly classified in `DirectionTests` (NetMsg direction fail-closed). |

## 2. Changes

- `CharacterAttackAnimMsg` — protocol message (NetMsg 113, ProtocolVersion 41):
  owner id, prefab name, anchor position, normalized direction, facing sign.
- `CharacterAttackAnimHandler` + `CharacterDataStore` transport/relay — star
  semantics, same shape as `CharacterSound`.
- `CharacterAttackAnimSync` — adapter replay: loads the same Resources prefab,
  parents it to the owner render clone, mirrors the source rotation/scale and
  destroys after 5 s (mirrors Body.cs:1915-1920).
- `BodyAttackPatch` — captures the non-null prefab name, reports only for a
  local body, and uses the post-attack `isRight`/`targetLookPos`/arm to match
  the native visual.
- `OnlineUiPlayerContextMenu` — right-click near a remote player's stream
  position opens a context menu with the exact same eligibility rows/actions as
  the Players page, plus an always-available "View items" fallback that opens
  the standalone `OnlineUiQuickPanel` pinned to that member and expands its
  inventory (never the full Online window). The menu measures each row with
  `GUIStyle.CalcHeight` and uses a zero-margin menu button style so its height
  adapts to the action list instead of overflowing the panel.
- `OnlineUiOverlay` — right-click hit-testing against authoritative remote
  positions (no collider dependency) and context-menu drawing.

## 3. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx --no-restore` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-restore` | 1304 passed / 0 failed (full suite) |
| `dotnet format CasualtiesUnknownOnline.slnx --no-restore` | passed |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` | passed (33 events) |
| `tools/check-entity-event-dispatch.ps1` | passed (33 kinds × 3 tables) |
| `DirectionTests.EveryNetMsg_IsExplicitlyClassified` | pass after adding `CharacterAttackAnim` to the bidirectional classification |
| `CharacterAttackAnimSyncTests` | 4 tests: round-trip, guest→host relay, host broadcast, guest relay fire |

## 4. L0 proof

- `CharacterAttackAnimSyncTests` exercise the runtime transport/relay path in
  full containers (guest report reaches host event + other guest; host broadcast
  reaches both guests; relay reaches the other guest).
- The context menu reuses `OnlineUiMemberProjection`, which is already locked by
  `OnlineUiMemberProjectionTests` for carry/drop/heal/recruit/take eligibility;
  no new eligibility logic was introduced.
- No manual dual-side acceptance (user rule 2026-08-16).

## 5. Structure review

- `CharacterAttackAnimSync` is one deep module with one responsibility
  (replay owner attack-anim visuals), no cross-call mutable state, and no new DI
  service beyond the existing `CharacterDataStore` surface.
- `OnlineUiPlayerContextMenu` is a UI-only state owner; it holds only the
  currently targeted SteamID and screen position, and closes when the target
  leaves the world.
- No dead mechanism left behind: the old missing path was simply absent; the new
  dedicated visual event does not co-exist with any duplicate snapshot field.
