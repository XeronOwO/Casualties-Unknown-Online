# Online UI player awareness — self-check (2026-08-24)

Closes the KrokMP-inspired nameplate/off-screen indicator row and the
overlapping remote-player target disambiguation row. The existing overlay
already drew nameplates and off-screen arrows; this cycle adds distance
readouts, local deterministic per-player marker colors, and an explicit target
selector when several remotes overlap at the right-click point.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| Existing nameplate/arrow path | `OnlineUiOverlay.DrawNameplatesAndArrows` iterates `EntitySyncService.RemotePlayers`, projects each authoritative position and routes through `OffScreenArrowGeometry.Place`. |
| Local position source | `EntitySyncService.LocalPlayer.Position` is the local player's authoritative world position (already published by the adapter). |
| Distance units | The game uses world units as metres for movement/speed fields (e.g. the world-time policy's `0.5 m/s` gate), so the UI displays world delta directly as metres. |
| Color source | `PlayerColorResolver` maps SteamId → one of eight high-contrast palette entries; the mapping is deterministic and local-only. |
| Overlap hit-test | `RemoteTargetPicker.Find` returns every `RemoteScreenTarget` inside the click radius, ordered by squared distance with SteamId tie-break. |

## 2. Changes

- **Distance** — `OnlineUiOverlay` computes `sqrt(dx²+dy²)` from local to
  remote and passes the localized distance string to `DrawOffScreenArrow`;
  on-screen nameplates keep the existing compact layout (vitals text stays
  white).
- **Colors** — `PlayerColorValue` + `PlayerColorResolver` are pure Runtime
  types (no UnityEngine dependency). The overlay converts the resolved value
  to a Unity `Color` and uses it for the nameplate name and the off-screen
  arrow + name. No color sync: every peer derives the same color from the same
  SteamId.
- **Overlap selection** — the right-click handler now collects all projected
  remotes and asks `RemoteTargetPicker`; `OnlineUiPlayerContextMenu` stores the
  candidate list and, when more than one candidate exists, draws a compact
  "Target:" selector row above the action buttons.
- **No protocol change** — no new `NetMsg`, no `ProtocolVersion` bump, no
  entity-event or item/entity matrix row touched.

## 3. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 1366 passed |
| `PlayerColorResolverTests` | 3 passed |
| `RemoteTargetPickerTests` | 5 passed |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` / `tools/check-entity-event-dispatch.ps1` | passed (no event mechanism touched) |
| `dotnet format` | run |
| Manual acceptance | Not required by the developer-cycle rule; L0 + static evidence, no manual acceptance. |

## 4. L0 proof

- `PlayerColorResolverTests` locks stable resolution, valid RGBA range and a
  useful palette spread.
- `RemoteTargetPickerTests` locks radius filtering, nearest-first ordering,
  SteamId tie-breaking, empty input, negative radius and zero-radius exact
  hits.
- The existing `OffScreenArrowGeometryTests` continue to cover the arrow
  geometry; the new code only consumes that existing pure surface.
- The overlay path remains the only Unity-facing consumer; all decision logic
  is in the Runtime and unit-tested.

## 5. Structure review

- `PlayerColorValue` is a tiny immutable value type, one file/type.
- `PlayerColorResolver` is stateless; palette data is a private static array.
- `RemoteScreenTarget` and `RemoteTargetPicker` are pure and testable.
- `OnlineUiOverlay` gains only presentation wiring (distance + color) and a
  candidate collector; `OnlineUiPlayerContextMenu` gains a small selector
  renderer with no business logic.
- No class approached the 600-line gate as a result of this change.

## 6. Plan approval

The user instructed this session to pick a backlog item autonomously and
complete it ("由你来自主挑选并完成"), so this cycle's plan is approved
without a separate interactive approval step.
