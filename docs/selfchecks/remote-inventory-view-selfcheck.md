# Remote inventory view — mechanism inventory and self-check

Owner cycle: backlog "Direct player interaction (view/take items, carry, view
vitals, heal)". Decision for this cycle: close the **view items** slice by
showing each in-world remote player's carried/worn inventory in the Online UI
member status list. Taking a remote player's item, carrying and healing remain
open direct-interaction work.

No protocol change: the 1 Hz character-data stream already carries the full
`CharacterDataMsg.Items` list to every side (guest reports → host save/relay;
host broadcast → guests; cross-guest relay with `OwnerSteamId`), so the UI only
needs a read-only projection of data that is already arriving.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Character snapshots reach every side | `CharacterDataMsg.Items` (`CharacterDataMsg.cs`); host saves guest reports, fires `CharacterDataReceived`, then relays them to the other guests (`CharacterDataHandler.cs`); the host's own snapshot arrives via `HostCharacterDataHandler` |
| 2 | The item list is already wire-complete | `CharacterItemMsg` carries `ItemId`, `SlotIndex`, `Condition`, `Favourited`, `Components`, `Contents` (recursive) and `Liquids` |
| 3 | Existing Online UI has the remote-player projection point | `OnlineUiOverlay.DrawMemberStatus` already renders every lobby member with status/vitals; the inventory lines are a new indented projection under each in-world member |
| 4 | UI must not reach GameAdapter/Unity internals | The new cache lives in the Runtime and is fed by the public character-data events; the Plugin only calls `TryGet` and draws strings |
| 5 | Session scope must not leak between runs | `SessionService.SessionEnded` and `RemoteSceneChanged` are already broadcast; the cache clears on both |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `CharacterDataStore` | Unchanged — it remains the owner of saved/relayed character data |
| `CharacterDataHandler` | Unchanged — the existing report/relay/restore flow already delivers item data |
| `RemoteInventorySnapshot` | New pure projection: `From(CharacterDataMsg?)`, `ToShortString()`, `ToDisplayLines()` |
| `RemoteInventoryEntry` | New immutable display record (item id, slot, condition, favourite flag, nested-content count) |
| `RemoteInventoryService` | New read-only cache, subscribed to `CharacterDataReceived`, `HostCharacterDataReceived`, `RemoteSceneChanged`, `SessionEnded` |
| `OnlineUiOverlay` | Expands an in-world member's carried/worn inventory under its status row |
| `Plugin` | Resolves `RemoteInventoryService` and passes it to the overlay |
| Protocol / patches | Unchanged — no wire bump, no Harmony patch surface |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Host sees guest inventory | Cache `CharacterDataReceived(sender, data)` on host as sender | `RemoteInventoryService.OnCharacterDataReceived`; covered by `Host_CachesGuestInventoryBySender` |
| Guest sees host inventory | Cache `HostCharacterDataReceived(data)` as `HostSteamId` | `RemoteInventoryService.OnHostCharacterDataReceived`; covered by `Guest_CachesHostInventoryByHostSteamId` |
| Guest sees another guest's inventory | Use `OwnerSteamId` from the host relay, never the transport sender | `RemoteInventoryService.OnCharacterDataReceived`; covered by `Guest_CachesCrossGuestRelayByOwnerSteamId` |
| Local restore is not shown as a remote | A guest-side `CharacterData` with `OwnerSteamId == 0` is ignored | `Guest_IgnoresOwnRestoreOwnerZero` |
| No stale inventory after leaving world | `RemoteSceneChanged(false)` removes the player's entry | `RemoteLeavingWorld_ClearsThatPlayersInventory` |
| No cross-session leak | `SessionEnded` clears the cache | `SessionEnd_ClearsTheCache` |
| Compact + readable formatting | Pure `ToDisplayLines()` renders slot/worn labels, favourite and nested content counts | `Snapshot_ProjectsItemsAndFormats` |
| Empty inventory is a valid view | A snapshot with zero items caches as `(empty)`, not as "unknown" | `Snapshot_ProjectsItemsAndFormats` |

## 4. Verification design

- **L0 service tests:** `RemoteInventoryServiceTests` (7 tests) — host report,
  host broadcast, cross-guest relay, own-restore exclusion, world-leave clear,
  session-end clear, pure projection/formatting.
- **Full regression:** `dotnet test CasualtiesUnknownOnline.slnx` — the
  character-data flow, Online UI geometry and the existing 20 Hz/1 Hz domains
  stay untouched.
- **Static evidence:** the character-data stream already delivers
  `CharacterDataMsg.Items`; the UI only projects received data.
- **Runtime evidence:** development-period rule — L0 simulation + static
  evidence + real-game-dir deploy; **no manual acceptance** (user 2026-08-16).

## 5. Plan approval

The user instructed this session to pick one backlog item autonomously and
complete it, then write the result back into `../backlog.md`
("由你来自主挑选一个并完成，记得在完成之后回写 backlog"). That instruction is
the plan approval for this cycle; no further interactive approval is required.

## 6. Verification results (2026-08-21)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1025 passed / 0 failed |
| `RemoteInventoryServiceTests` focused filter | 7 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean on source (verify-no-changes flags only the gitignored generated `obj/.../MyPluginInfo.cs`) |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | all passed |
| `tools/deploy.ps1 -GameDir "<game-dir>"` | 26 files deployed to the real game dir only |
| Protocol | unchanged (no bump) |

## 7. Structure review

- `RemoteInventorySnapshot` ~75 lines, `RemoteInventoryService` ~100 lines,
  `RemoteInventoryEntry` 7 lines, all under the 600-line gate.
- One top-level type per file; no new expression-state bools; the cache is
  state owned by `RemoteInventoryService` with a read-only `TryGet` surface.
- Dead mechanisms: none. The existing character-data events are the single
  source; the UI projection is a new consumer, not a duplicate path.
