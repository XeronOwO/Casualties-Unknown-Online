# RadiationLine Straggler Pressure — Self-Check (2026-08-23)

Delivery fact sheet for the host-side co-op straggler rule (backlog:
Radiation line / straggler pressure, exploration §2.3). No wire/protocol
change — it completes the already-landed `RadiationLineState` world-state sync
with the missing multiplayer activation rule.

## Mechanism inventory (complete side-effect table)

| # | Mechanism | Vanilla behaviour | CUO change | Evidence |
|---|---|---|---|---|
| 1 | Layer-timer activation | `WorldGeneration.Update`: `layerTimeSpent > maxTimePerLayer` → `RadiationLine.line.Activate()` (WorldGeneration.cs:859-863) | unchanged for solo; in an active host session `RadiationLineSync` may also activate earlier through the straggler rule. Guest local activation remains suppressed by the existing `layerTimeSpent` cap | `WorldGenerationUpdatePatch.cs`, `RadiationLineSync.cs` |
| 2 | Straggler detection | single-player has no concept of other players | host gathers the local entity + `EntitySyncService.RemotePlayers`, checks the vanilla layer-bottom boundary (`y < -halfHeight + 3.1f`, WorldGeneration.cs:979) and asks the pure policy | `RadiationStragglerPolicy.cs`, `RadiationLineSync.TryActivateForStragglers` |
| 3 | Activation | `RadiationLine.Activate()` sets `active = true`; the line then descends and applies local body effects | host calls the same `Activate()` when the policy fires; the existing `RadiationLineSync` broadcast propagates it to every guest | `RadiationLineSync.cs` |
| 4 | One-way semantics | the line stays active until the layer is regenerated / `Deactivate()` | unchanged — the policy only activates, never deactivates (a later layer regeneration resets it through the existing world state path) | `RadiationLine.cs` |
| 5 | Per-player body pressure | `RadiationLine.Update` applies `radiationSickness`, `eyeScareTime`, `SetIrradiateIntensity` on the local body above the line | unchanged — each side still applies its own local body effects (local-compute mandate); the host only owns the world-state boundary | `RadiationLineSync`, `RadiationLine.cs:49-70` |
| 6 | Dead / absent players | n/a | ignored: dead players are not stragglers and there is no pressure for a body that is no longer a controllable world player | `RadiationStragglerPolicy.ShouldActivateLine` |

## Design

- **Pure decision**: `RadiationStragglerPolicy.ShouldActivateLine` lives in
  `Runtime.Session.EntitySync` and takes engine-agnostic
  `RadiationPlayerProgress` facts (`Y`, `Alive`). Rule: **at least one living
  player has reached the layer bottom AND at least one other living player is
  still above it**.
- **Integration**: `RadiationLineSync` receives `EntitySyncService` and calls
  the policy on the host before publishing. It only acts while the line is
  still inactive, so the vanilla timer remains the fallback.
- **Boundary**: `bottomY = -halfHeight + 3.1f`, the exact condition the game
  uses to open the next-layer save/continue panel (WorldGeneration.cs:979).
  Strict `<` keeps the straggler rule on the same boundary as the game.
- **No wire/protocol change**: the activation is a local-world mutation that
  the existing `RadiationLineState` message (NetMsg 106) already broadcasts;
  `ProtocolVersion` remains 34.

## Verification design

1. L0 (pure decision): `RadiationStragglerPolicyTests` — 8 cases cover:
   leader + straggler activates, everyone at bottom, everyone above, no living
   players, dead players ignored, exact-threshold boundary, one-leader/many-
   stragglers, empty roster.
2. Full suite: `dotnet test CasualtiesUnknownOnline.slnx` — **1208 green**.
3. Static/build gates: build 0 warnings/0 errors, `dotnet format`,
   `tools/check-architecture.ps1`, `tools/check-event-replay.ps1` pass.
4. Runtime (final acceptance only): with two or more players, one reaches the
   layer bottom while another is still above → the host log
   `[RadiationLine] host activated the line for straggler pressure` appears and
   both sides receive the existing `RadiationLineState` active broadcast. Host
   timer remains the fallback if nobody is below. No manual acceptance during
   the dev cycle; evidence is L0 + static.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Activation decision | pure host-side rule | `RadiationStragglerPolicy.cs` |
| Hud/entity input | local + remote entity-stream positions | `RadiationLineSync.TryActivateForStragglers` |
| World boundary | vanilla layer-bottom formula | `WorldGeneration.cs:979` |
| Existing wire path | reuse NetMsg 106 broadcast | `RadiationLineSync.Publish` |
| Protocol | unchanged (34) | `ProtocolVersion.cs` |
| Solo behaviour | unchanged | `RadiationLineSync.Update` only calls the policy in an active host session |
| Structure | new small owners under 600-line gate | `tools/check-architecture.ps1` passed |
