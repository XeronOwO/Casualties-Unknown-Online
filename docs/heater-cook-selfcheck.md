# Heater cooker meat→steak sync — mechanism inventory and self-check

Owner cycle: backlog item-domain TODO "Heater cooker meat→steak conversion".
Decision: complete it as ONE dedicated host→guest event (`ItemCook`, NetMsg 92,
ProtocolVersion 15) — the conversion is a discrete game trigger, so it must
travel as one operation = one message (AGENTS.md sync-chain rules 10/11),
never as an accidental pair of `ItemDestroy` + `ItemSpawn` messages.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The game's conversion code | `Heater.OnCollisionEnter2D` (`reversing/.../Heater.cs:41-49`): when `cooker` is true and the colliding GameObject has an `Item` whose `Stats.HasTag("meat")` and whose id is not `"steak"`, the game instantiates `Resources.Load("steak")` at the raw item's transform position/rotation, sets the steak condition to `item.condition * 0.3f`, destroys the raw item and plays `Sound.Play("Scald", ...)` |
| 2 | Trigger side | `OnCollisionEnter2D` is physics-collision driven. On the host (the only full-physics side for world items) the conversion runs natively. A guest's world items are isolated to `Item` layer 7 × `Ground` layer 6 only (`ItemPositionFollow.cs:33-36,186-198`), so a guest item **cannot** collide with the Heater building while the session is active — the host's copy is the only copy that can cook |
| 3 | Why the current generic path is not enough | If the guest could ever run the native conversion, its `Item.OnDestroy`/`Item.Start` hooks (`ItemPatches.cs:17,29-34`) would report a second `ItemDestroy` + a second self-allocated `ItemSpawn`; the host would register the guest's duplicate steak beside its own. Today the guest copy is collision-isolated, but the patch must make that boundary explicit and one-message, not accidental |
| 4 | Raw-item identity | The colliding raw meat is a standalone world item with an instance id (dropped loot was reported by `ItemWorldSync.OnItemInstantiated`, `ItemWorldSync.cs:176-233`). A hypothetical generation-time meat entering the domain gets its id before the native conversion in the patch prefix via `ItemIdAllocator.EnsureId` |
| 5 | New steak identity | The native `Instantiate` returns the new steak immediately; its `Item.Start` has not run yet in the same physics callback, so it is not yet in `Item.allItems` (`Item.cs:112-118`) — that is the unambiguous marker used to find the created steak in the patch postfix |
| 6 | Source-destroy suppression | The raw meat's `OnDestroy` runs at end-of-frame after `Object.Destroy` (`ItemPatches.cs:20-38`). The postfix claims the source id before that hook, and `ShouldSuppressDestroy` consumes the claim exactly once — no duplicate `ItemDestroy` |
| 7 | New-steak spawn suppression | The domain stamps the steak with `ItemInstanceId` in the same postfix; `ItemWorldSync.OnItemInstantiated` already skips an item that carries an id attached by a remote/domain application (`ItemWorldSync.cs:184-190`) — no duplicate `ItemSpawn` |
| 8 | Authoritative table | `ItemService.SendItemDestroyed` removes the source and `SendItemSpawned` registers the product (`ItemService.cs:150-168,245-266`). The new `SendItemCooked` must do both atomically in one table transition |
| 9 | Late joiner / reconnect | The cooked steak is a runtime world item: it rides the existing `SendItemSnapshot` on world entry (`HandlerContext.SendWorldStateToMember`, `HandlerContext.cs:60-71`) and the 5 s periodic keyframe (`ItemPositionAuthority.cs:23-31`). No new late-joiner snapshot is needed |
| 10 | Position authority | The host's copy of the cooked steak simulates natively and feeds the existing 10 Hz `ItemMove` stream (`ItemPositionAuthority.cs:38-108`); the guest materializes the steak from the `ItemCook` payload and its existing `ItemPositionFollow` takes over on the first stream tick |
| 11 | Guest apply | `ItemApplication` already owns remote item materialization/kill (`ItemApplication.cs:454-563,347-383`). The guest applies the conversion atomically inside one `RemoteApply` scope: kill source id (idempotent), then spawn the cooked steak (idempotent by new id) |
| 12 | Sound | The native conversion plays `Sound.Play("Scald", item.transform.position, false, true, null, 1f, 1f, false, false)` (`Heater.cs:48`). The guest-side layer isolation guarantees the guest never plays it natively, so the remote apply replays the same call once — no double sound |
| 13 | Wire/protocol | New `ItemCookMsg` (source id, cooked id, full cooked-item capture, position/velocity/rotation/angular velocity) + `NetMsg.ItemCook = 92`; one-way host→guest (`PacketReceiver.IsValidDirection`). ProtocolVersion 14 → 15 because a v14 peer would not understand the conversion event |
| 14 | Idempotency | The guest apply kills a source only when present and skips spawning when the cooked id already exists. The reliable channel's duplicate delivery therefore re-applies harmlessly (same shape as the existing item handlers) |

## 2. Whole-family audit

The conversion touches the world-item family (spawn/destroy/report/receive/table).
Every sibling is aligned in the same round:

| Family member | Change |
|---|---|
| `ItemSpawnMsg` / `ItemDestroyMsg` paths | Unchanged; their hooks remain the fallback only when the postfix cannot identify the created steak (the patch then deliberately does NOT claim the source, so the existing two-message path still self-heals) |
| `ItemWorldSync.OnItemInstantiated` | Already skips id-stamped steaks — verified, not modified |
| `ItemPatches.OnDestroy` | Already routes through `ShouldSuppressDestroy` — extended to the heater claim set, no new hook order assumption |
| `ItemService` world table | New atomic `SendItemCooked` transition; no separate remove/set call sites |
| `ItemApplication` | New one-scope guest apply; reuses `FindWorldItem`, `KillRemoteItem`, `SpawnWorldItem` — no new materialization semantics |
| Position stream / item snapshot / reconnect | Unchanged; the cooked item is an ordinary table entry after the event |
| Late-joiner generation snapshot | Unchanged; generation-time raw meat/steak still belongs to world-gen determinism until one of them enters the runtime domain |
| Guest layer isolation | Unchanged and now the patch's authority gate (`IsHeaterCookAuthority`) makes the boundary explicit instead of relying only on the physics matrix |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Cook candidate predicate | Extracted into pure `HeaterCookRule.IsCookCandidate` (same predicate as Heater.cs:44) | L0 reflective tests over the rule; patch contract points at `Heater.OnCollisionEnter2D` |
| Condition multiplier 0.3 | `HeaterCookRule.CookedCondition` | L0 reflective test |
| Guest must not cook locally | `HeaterCookPatch.Prefix` returns false when `IPatchBridge.IsHeaterCookAuthority` is false (guest + active session) | Patch-surface reflection test + `ItemPositionFollow` static evidence |
| Raw id before native destroy | Prefix calls `HeaterCookSync.OnCookCandidate` → `ItemIdAllocator.EnsureId` | Code path; id-stamping behavior covered by existing item-id tests |
| Created-steak identification | Postfix finds the only nearby steak not yet in `Item.allItems` with condition equal to source×0.3 | Pure `HeaterCookRule.IsCookedSpawn`/`IsCookedCondition` tests; Unity-scene part is static-evidence only (no game runtime in L0) |
| One message, one table transition | `ItemService.SendItemCooked` removes source + sets cooked + broadcasts `ItemCook` once | `ItemCookSimulationTests` over the real wire stack |
| Guest atomic apply | `ItemApplication.OnRemoteItemCooked` kill + spawn in one `RemoteApply` scope | Code path + reflection contract (adapter is compile-excluded); wire event delivery covered by simulation |
| Source destroy silence | Claim set consumed by `ShouldSuppressDestroy` before end-of-frame OnDestroy | Code path; claim pattern mirrors `CraftingSync.ShouldSuppressDestroy` (proven pattern) |
| Steak spawn silence | Domain stamps `ItemInstanceId` before `Item.Start`; `OnItemInstantiated` skips already-stamped items | Existing `ItemWorldSync.cs:184-190` + code path |
| Sound once per side | Guest replays `Sound.Play("Scald", ...)` only in remote apply; host native plays it | Static evidence (Heater.cs:48 mirror); no L0 audio runtime |
| Late joiner | No new snapshot — cooked steak is in the world table and rides `ItemSnapshot` | Existing `ItemSnapshotSimulationTests` + `HandlerContext` code path |
| Protocol direction/version | `NetMsg.ItemCook` added to the host→guest switch, `DirectionTests` completeness guard, ProtocolVersion 15 | `DirectionTests` + `ProtocolVersionTests` (existing) |

## 4. Verification design (development-period, no manual acceptance)

- L0 wire simulation: `ItemCookSimulationTests` — host broadcasts one `ItemCook`,
  both guests receive exactly one wire frame, the guest event surfaces the full
  `WorldItem`, and the host table flips source→cooked atomically.
- Replay fossil: `heater-cook.replay` drives the same operation through
  `ItemSimWorld` and asserts the received-frame and table invariants; the
  emitted SimTrace stays diffable.
- Patch contract: `HeaterCookPatchTests` reflectively locks the patch shape
  (bool Prefix with `__instance`/`collision`/`__state`, postfix shape) and
  `PatchInventory.BuildContracts` automatically gains the `Heater.OnCollisionEnter2D`
  contract — a game update that renames/retypes the method fails `dotnet test`.
- Static evidence: guest layer isolation (`ItemPositionFollow.cs:186-198`) is
  the proof that only the host runs the native conversion; the guest-side
  sound replay is the exact `Heater.cs:48` call.
- Runtime verification box for this development-period cycle: **L0 simulation +
  static evidence, no manual acceptance** (user rule 2026-08-16).

## 5. Plan approval

The user instructed this session to pick one backlog item autonomously and
complete it ("由你来自主挑选一个并完成"). That instruction is the plan
approval for this cycle; no further interactive approval is required.
