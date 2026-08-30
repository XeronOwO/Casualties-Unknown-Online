# CrystalTeleport Sync — Repeatable Teleport Crystal Event (ProtocolVersion 34)

## Mechanism inventory

| # | Mechanism | Vanilla behaviour | CUO change | Evidence |
|---|---|---|---|---|
| 1 | Crystal assignment | `CrystalBehaviour.possibleEffects` includes `CrystalTeleport` (weight 10) and both sides generate deterministically from the isolated generation stream | no change — the crystal type already matches on every side | `CrystalBehaviour.cs:185-206` |
| 2 | Teleport touch | `CrystalTeleport.Touched` finds a body, searches up to 1000 random points, and on the first ground hit plays `observerlaugh` (2D/global), `FlashBrief`, and teleports the body (`consciousness=100`, `shock=0`, `velocity=0`, `Stand(false)`, position = hit + up * 3) | the trigger side still runs this full path locally; the event carries only the shared laugh/flash | `CrystalTeleport.cs:14-38` |
| 3 | Body result | the body's new position/stats are per-player local simulation state | already rides the existing 20 Hz player entity state stream (`EntityStateMsg` / character authority) — no new body transport | existing body stream |
| 4 | Observability | the 2D `observerlaugh` and `FlashBrief` were trigger-side only, so peers saw the remote player blink away with no laugh/flash | new repeatable entity event replays both on host and guests | this delivery |
| 5 | Repeatability | no latch — every body touch may teleport again | event is classified repeatable; not in one-shot snapshot | `EntityEventArchives` / `EntityEventProfiles` |

## Change

- **New event kind**: `EntityEventKind.CrystalTeleportTriggered = 33`.
- **Report**: dynamic Harmony prefix/postfix on the internal
  `CrystalTeleport.Touched` (the override cannot be intercepted through the
  public base). The prefix captures the touching body's position; the postfix
  reports only when the body actually moved (>1 unit), so a failed 1000-point
  search (no ground hit) does not emit a false event.
- **Replay**: `CrystalStateActions.ApplyCrystalTeleport` plays the exact
  trigger-side calls (`observerlaugh` 2D with the same flags, then
  `FlashBrief`). Both `TrapEffectApplier` (host executor for guest-triggered
  events) and `TrapVisualReplay` (guest replay for host- or other-guest events)
  route through this shared action.
- **Body state is deliberately NOT applied by the event**: each side simulates
  its own body; the teleporting player's body already moved on its own side and
  the 20 Hz player stream carries the new position/stat state to every peer.
- **Repeatable / no snapshot**: no crystal latch exists, so this is not a
  one-shot consumption; a late joiner must not replay an old laugh/flash.

## Dispatch tables

All three GameAdapter tables reference `CrystalTeleportTriggered`:
`TrapEntityScan` (`"CrystalTeleport" => [CrystalTeleportTriggered]`),
`TrapEffectApplier.ApplyEvent` (host executor),
`TrapVisualReplay.Replay` (guest replay).

## Protocol

`ProtocolVersion.Current` 33 → 34: a v33 peer would receive the new enum
value on the existing entity event message and silently drop the presentation.

## Verification

| Mechanism | Change | Evidence |
|---|---|---|
| Report edge = real body teleport | dynamic prefix/postfix on `CrystalTeleport.Touched` | `DynamicPatchInstaller.cs`; `PatchInventory` dynamic contract `CrystalTeleport.Touched` with `touched` param; `PatchContractTests.CrystalTeleportPatchSet_IsComplete` |
| Replay = trigger-side laugh/flash | shared `CrystalStateActions.ApplyCrystalTeleport` | `TrapEffectApplier` / `TrapVisualReplay` cases |
| Repeatable not in snapshot | archive row `(CrystalTeleportTriggered, false)` | `EntityEventArchives`; `EntityEventSimulationTests.CrystalTeleportTriggered_RepeatableEvent_NotInLateJoinerSnapshot` |
| Entity matrix | new CrystalTeleport row covered + doc | `tools/entity-features.ps1 validate`; `EntityFeaturesDocConsistencyTests` |
| Event-replay audit | new row covered | `tools/check-event-replay.ps1` |
| Full suite | 1200 green | `dotnet test` |
| Repo gates | architecture + event-replay + entity-event-dispatch + entity-features validate | all pass |

**L0 simulation + static evidence, no manual acceptance**
(development-period no-manual-acceptance rule).
