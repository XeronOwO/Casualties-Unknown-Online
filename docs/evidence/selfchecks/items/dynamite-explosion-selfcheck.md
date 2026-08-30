# Dynamite detonation player-item explosion sync — mechanism inventory and self-check

Owner cycle: backlog item "Dynamite fuse — the only known gameplay-affecting item gap".
Decision: close it with a dedicated `DynamiteExplosionMsg` (NetMsg 105,
ProtocolVersion 30) carrying the one-shot item id + detonation position. The
native `CustomItemBehaviour.DynamiteExplode` already ran on the trigger side;
the new event lets the host apply the explosion to its own world and lets the
peers replay the body/visual segment exactly once. The 5-second lit-fuse visual
on remote clones is not a persistent state and stays an accepted local-only
presentation residual (same family as the crystal-unstable pre-explosion
ticking).

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Native detonation | `Item.cs:6671-6682` (dynamite use action sets `data[0]=true`, enables the child sprite, plays the fuse sound, invokes `DynamiteExplode` after 5 s); `CustomItemBehaviour.cs:563-572` (destroy + `CreateExplosion(new ExplosionParams { position, range = 18, structuralDamage = 2000 })`) |
| 2 | Trigger side local effects | The native explosion already damages the local terrain, buildings, items and body; those world consequences ride the existing block/building/item channels (`WorldGenerationSetBlockPatch`, `ExplosionBuildingSyncPatch`, `BlockBreakSync`, `ItemWorldSync`) |
| 3 | Missing piece before this cycle | The host/other guests never received a body/visual explosion event, so a remote blast could hurt only the trigger side's own body and never play on the other sides |
| 4 | One-shot identity | Each dynamite item can detonate at most once in the native game; the item's `ItemInstanceId` is the natural duplicate-suppression key on reliable retransmission. The patch reads it from the item's `ItemInstanceId` component at detonation |
| 5 | Star topology | Guest → host report, host applies to its own world inside `RemoteApply` (suppressing a second round of block/building reports, since the trigger side's own consequences already synced), then broadcasts source-excluded; guests replay under `RemoteApply` |
| 6 | Host apply | `WorldGeneration.CreateExplosion(DynamiteExplosionParams(pos))` inside `RemoteApply` — the host's real body, world items, and any already-surviving buildings receive the same native effect |
| 7 | Guest replay | `TrapVisualReplay.ReplayExplosion` (new shared method) → `ReplayExplosionVisual` + `ExplosionBodyEffect.ApplyToLocalBodies` — no local `CreateExplosion`, no double terrain carve |
| 8 | Dedup | `DynamiteExplosionSync` keeps a session-scoped `HashSet<ulong>` of seen item ids; a reliable-channel duplicate is dropped on the host and on every replaying guest |
| 9 | Wire/protocol | `DynamiteExplosionMsg` (item id + position), `NetMsg.DynamiteExplosion = 105`, bidirectional direction row, ProtocolVersion 29 → 30 because a v29 peer does not understand the event |
| 10 | Accepted residual | The 5 s lit-fuse visual (child sprite + fuse audio) on remote clones remains local-only; it is short-lived presentation, not persistent state. The authoritative detonation (body damage + world consequences) is now synced |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `CustomItemBehaviour.DynamiteExplode` | New postfix patch only reports the verified detonation; the native method is unchanged |
| Block damage / building damage / world-item condition channels | Unchanged; they already carry the trigger side's native explosion consequences and remain the world-state authority |
| `EntityEventChannel` | Gained the player-item explosion channel side (send/broadcast/fire/event), reusing the same star shape — no new channel class |
| `TrapVisualReplay` | Refactored a shared `ReplayExplosion(ExplosionParams)` so the dynamite replay uses the exact same visual/body segment as trap explosions |
| `PacketReceiver` / `DirectionTests` | New bidirectional row; role-direction table completeness guard passes |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Native detonation captured at the real edge | `DynamiteExplodePatch.Postfix` on `CustomItemBehaviour.DynamiteExplode` | Patch contract auto-added by `PatchInventory.BuildContracts`; full `PatchContractTests` pass |
| One-shot identity | `ItemInstanceId` read from the same GameObject; carried in `DynamiteExplosionMsg` | `NetPacketTests.DynamiteExplosion_Position_RoundTrips`; simulation asserts the id rides through |
| Star relay | `EntityEventChannel.SendDynamiteExplosion` / `BroadcastDynamiteExplosion` | `DynamiteExplosionSimulationTests`: guest report reaches host and relays to the other guest source-excluded; host report reaches both guests |
| Duplicate suppression | `HashSet<ulong>` in `DynamiteExplosionSync`, cleared on unbind | Code path; deterministic one-shot identity from the item id |
| Host apply without re-reporting | `WorldGeneration.CreateExplosion` called inside `CallContext.Origin.RemoteApply` | Code path (BlockBreakSync/WorldEventSync/ExplosionBuildingSync all gate on `RemoteApply`); static evidence in those classes |
| Guest replay without local explosion | `TrapVisualReplay.ReplayExplosion` (visual + body only) | Code path; the same method is already the trap replay's shape |
| Direction table | `NetMsg.DynamiteExplosion` added to `BidirectionalMessages` | `DirectionTests.EveryNetMsg_IsExplicitlyClassified` passes |
| Protocol version | 29 → 30 | `ProtocolVersion.Current` + handshake tests |

## 4. Verification design (development-period, no manual acceptance)

- L0 wire simulation: `DynamiteExplosionSimulationTests` (2 cases) plus
  `NetPacketTests` round-trip and `DirectionTests` classification.
- Patch contract: the new `[HarmonyPatch(typeof(CustomItemBehaviour), "DynamiteExplode")]` is
  picked up automatically by `PatchInventory.BuildContracts`; a game update
  that renames/retypes the method fails `dotnet test` before launch.
- Full suite: 1128 tests green (includes the new tests).
- Static evidence: the host apply/replay split is verified by the existing
  `RemoteApply` gates in `BlockBreakSync`, `WorldEventSync` and
  `ExplosionBuildingSync`; `TrapVisualReplay.ReplayExplosion` reuses the proven
  trap explosion replay path.
- Runtime verification box for this development-period cycle: **L0 simulation +
  static evidence, no manual acceptance** (user rule 2026-08-16).

## 5. Plan approval

The user instructed this session to pick one backlog item autonomously and
complete it ("由你来自主挑选一个并完成"). That instruction is the plan
approval for this cycle; no further interactive approval is required.

## 6. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1128 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean (run before final commit) |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | pass (run before final commit) |
| Patch contract | New `CustomItemBehaviour.DynamiteExplode` contract auto-generated and passed in `PatchContractTests` |
| Direction table | `NetMsg.DynamiteExplosion` explicitly classified; completeness guard passed |
| Native detonation evidence | `reversing/Assembly-CSharp/Assembly-CSharp/CustomItemBehaviour.cs:563-572` + `Item.cs:6671-6682` |

## 7. Structure review

- Touched classes stay under the 600-line gate: `GameAdapter.cs` remains below
  the gate; the new pieces are small owners (`DynamiteExplosionSync.cs` 103
  lines, `DynamiteExplodePatch.cs` 33 lines).
- No new expression-state bool fields: `DynamiteExplosionSync` owns one
  session-scoped `HashSet<ulong>` duplicate set, no bool flags.
- Dead mechanisms: none. The generic item hooks remain the fallback for
  non-dynamite explosions; the `CustomItemBehaviour.data` object array itself
  is deliberately still unsynced (only the dedicated explosion event is new).
