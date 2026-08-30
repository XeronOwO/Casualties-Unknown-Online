# GrapplingHook presentation sync and remote clone owner-local script isolation — mechanism inventory and self-check

Owner cycle: autonomous backlog selection (user instruction: "由你来自主挑选一个并完成") —
the remaining native-content state gap with the highest mechanical value was
GrapplingHook's local-only fired/latched/pulling visual. The adjacent low-risk
WatchScript/AutoPump local-only states were closed in the same pass by
documenting them as owner-local by design and disabling those scripts on render
clones.

Decision: no new wire message or protocol bump. The three grapple bools ride
the existing `CharacterItemMsg.Components` digest (`ItemStateCodec`), and the
clone renderer applies the fired/normal sprite directly while disabling the
owner-local scripts on clone items.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Native grapple state fields | `GrapplingHook.cs:105-129` (`hook`, `barrel`, `hookPrefab` public refs; `fired`/`hookLatched`/`pulling` private bools; `normSprite`/`firedSprite` public sprites) |
| 2 | Native use transitions | `GrapplingHook.cs:20-51` (`Use`: fire → `fired=true`, sprite=fire, drain; second use → `pulling=true`) |
| 3 | Native return/latch transitions | `GrapplingHook.cs:63-87` (`Update` return dims; `HookHit` latches) |
| 4 | Existing item-state capture path | `ItemStateCodec.CaptureSaveableComponents` carries `[Saveable]` component states plus the `CustomItemBehaviour` whitelist on every item path |
| 5 | Missing piece before this cycle | `fired`/`hookLatched`/`pulling` were private and not `[SerializeField]`, so they never entered the digest; a remote clone saw only the normal sprite |
| 6 | Clone rendering path | `CloneInventoryRenderer.RenderItemInto` instantiates/keeps clone item prefabs and applies `RestoreComponentStates`; it is the display-domain owner |
| 7 | Owner-local script danger | A clone has no live `hook` Rigidbody2D; enabling the original `GrapplingHook.Update` after restoring `fired=true` would NRE. `WatchScript`/`AutoPump` read `PlayerCamera.main.body` and must not act on the local player from a clone copy |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ItemStateCodec` | Added `MultiplayerStateFields` whitelist for `GrapplingHook`; field eligibility now also admits those explicitly declared private bools |
| `CloneInventoryRenderer` | Calls `RemoteItemPresentation.Apply` on every clone render (matching and new); adds `RemoteCloneRender` marker to all clone items, not only worn limbs |
| `RemoteItemPresentation` (new) | Disables `GrapplingHook`/`WatchScript`/`AutoPump` on clone items; sets the grapple sprite from the wire state; keeps line renderer disabled |
| WatchScript / AutoPump | No state sync (owner-local by design); render-clone scripts disabled — the low-risk local-only gap is closed as excluded, not piecemeal sync |
| Protocol | No change: `ComponentStateMsg` already carries name/kind/value; no `NetMsg` or ProtocolVersion change |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Grapple bools captured | `MultiplayerStateFields` + private-field eligibility in `ItemStateCodec` | `GrapplingHookComponentSyncContractTests` reflects the table and game fields |
| Clone fired sprite decision | `RemoteItemPresentation.IsGrapplingHookFired` reads the component wire state | `RemoteItemPresentationTests` (3 cases: true / false / missing component) |
| Clone-safe script disabling | `RemoteItemPresentation.Apply` disables GrapplingHook/WatchScript/AutoPump | Code path; Unity `== null` guards for destroyed objects |
| Clone renderer calls presentation | `CloneInventoryRenderer.RenderItemInto` (matching + new render) | Code path; no new patch contract needed |
| No wire/protocol break | No new NetMsg or ProtocolVersion change | `ProtocolVersion` unchanged; build/test pass |
| Game-update guard | GrapplingHook fields and codec table are contract-tested | `dotnet test` contract tests pass |

## 4. Verification design (development-period, no manual acceptance)

- L0 tests: `RemoteItemPresentationTests` exercises the fired-sprite decision;
  `GrapplingHookComponentSyncContractTests` exercises the codec table and game
  field shapes.
- Full suite: 1133 passed / 0 failed.
- Static evidence: the display-only path is inside `CloneInventoryRenderer`;
  the owner's real grapple simulation remains untouched; the clone scripts are
  disabled before any `Update` can read a null hook.
- Runtime verification box for this development-period cycle: **L0 simulation +
  static evidence, no manual acceptance** (user rule 2026-08-16).

## 5. Plan approval

The user instructed this session to pick one backlog item autonomously and
complete it ("由你来自主挑选一个并完成"), and set approval policy to "never".
That instruction is the plan approval for this cycle; no further interactive
approval is required.

## 6. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1133 passed / 0 failed |
| `dotnet format` (changed source files) | clean |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | pass |
| Protocol version | unchanged (no new message) |
| Native evidence | `reversing/Assembly-CSharp/Assembly-CSharp/GrapplingHook.cs:105-129` |

## 7. Structure review

- Touched classes remain under the 600-line gate: `ItemStateCodec.cs` (422),
  `CloneInventoryRenderer.cs` (205), `RemoteItemPresentation.cs` (~75).
- No new expression-state bool fields: the presentation is stateless; the
  codec table is a readonly dictionary.
- Dead mechanisms: none. The rope/hook projectile remains the documented
  local-projection residual; only the fired/normal sprite state is now synced.
