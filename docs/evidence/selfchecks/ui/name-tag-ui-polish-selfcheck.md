# Name tag font, head position, and off-screen edge padding — self-check

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| Nameplate/off-screen path | `OnlineUiOverlay.DrawNameplatesAndArrows` iterates `EntitySyncService.RemotePlayers`, projects each authoritative position and routes through `OffScreenArrowGeometry.Place`. |
| Player head position | `RemotePlayerRenderer.TryGetRemoteHeadPosition` returns `Body.limbs[0].transform.position` of the live render clone; the game uses `limbs[0]` as the head (`NetBody.GetHeadPos`, reversing/KrokMP/NetBody.cs:934). |
| Head query boundary | `IGameAdapter.TryGetRemoteHeadPosition` exposes the Game-Adapter-only head lookup to the plugin UI; falls back to the body root when no clone exists. |
| Nameplate layout | `NameplateLayout.AboveHead` is pure screen-space geometry (head-anchored, centered, with a head gap) and is L0-tested. |
| Edge padding | `OffScreenArrowGeometry.Place` is driven with a larger `ScreenEdgeMargin` so arrows/nameplates stay inside UI-safe margins. |

## 2. Changes

- **Head-anchored markers** — the overlay now asks the Game Adapter for the
  remote render clone's head limb position, so on-screen nameplates and
  off-screen arrows point at the visible head instead of the body-root/center.
  It falls back to the authoritative body position until the clone exists.
- **Larger on-screen nameplate** — `NameplateLayout` (180×24, 8 px head gap)
  replaces the previous 160×20 box; font size is raised from 12 to 15.
- **Larger off-screen markers** — arrow font is raised from 18 to 22 with a
  32 px hit box; the name/distance label is raised from 10 to 13 and widened.
- **Screen-edge inner padding** — the marker margin is raised from 28 px to
  52 px so off-screen indicators no longer sit flush against game UI edges.

## 3. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 1847 passed |
| `NameplateLayoutTests` | 3 passed |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` | passed (33 events) |
| `tools/check-entity-event-dispatch.ps1` | passed (33 kinds x 3 tables) |
| `tools/check-delivery.ps1` | passed (7 boxes checked) |
| `dotnet format CasualtiesUnknownOnline.slnx` | run |
| Manual acceptance | Visual confirmation remains with the user; no dual-client acceptance required during the developer cycle by the repo rule. |

## 4. Structure review

- `NameplateRect` and `NameplateLayout` are tiny pure Runtime types, one
  type/file, no UnityEngine dependency.
- `OnlineUiOverlay` gains only presentation wiring plus named style constants;
  it remains well below the 600-line gate.
- `IGameAdapter` gains one narrow read-only UI query; the Game Adapter
  implementation is a one-line delegation to the existing render clone table.
- No event/protocol machinery touched; no matrix row changes.
