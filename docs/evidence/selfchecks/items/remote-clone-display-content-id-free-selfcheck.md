# Remote clone display content is id-free

Owner cycle: backlog "Guest carried container contents periodically appear as
world drops on the host view (dog food in trash bag)". The report is fixed at
the display/domain boundary: remote clone inventory children are presentation
proxies, so they must never carry authoritative item-domain instance ids.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Remote clone renders are display proxies | `RemoteBodyFactory.cs:65-75` strips ids from the cloned body; `CloneInventoryRenderer.cs:22-23` / `RemoteCloneRender.cs` mark every rendered item as display-only |
| 2 | Nested container display restore used the authoritative restore helper | `CloneInventoryRenderer.RestoreRemoteContents` called `ItemStateCodec.RestoreContents` directly; `ItemStateCodec.RestoreContent` stamps `ItemInstanceId` (`ItemStateCodec.cs:473-476`) |
| 3 | Domain lookup scans all `Item`s and did not exclude display proxies | `RemoteItemSceneOps.FindWorldItem` iterated `Item.allItems` / `Object.FindObjectsOfType<Item>()` and returned the first id match |
| 4 | A domain event addressing the same id can act on the wrong object | `ItemApplication.OnRemoteItemDropped` unparents the found item and places it in the world; a proxy match therefore becomes a ghost world drop on the host view |
| 5 | The host displays the guest's carried bag contents through this same clone path | `CloneFactTable` + `CharacterDataSync` feed the recursive snapshot to `CloneInventoryRenderer` |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `CloneInventoryContentSanitizer` | New pure display-domain sanitizer: recursively zeroes `InstanceId` on clone snapshot contents before display restore |
| `CloneInventoryRenderer.RestoreRemoteContents` | Uses the sanitizer before `ItemStateCodec.RestoreContents` |
| `RemoteItemSceneOps.FindWorldItem` | Skips any item under a `RemoteCloneRender`, even if an id leaks into a proxy |
| `RemoteItemSceneOps.FindExistingAt` | Skips any item under a `RemoteCloneRender` before generation-time binding |
| Authoritative restore paths | Unchanged: local body/world restores still attach domain ids (`ItemStateCodec.RestoreItem`, `RemoteItemSceneOps.SpawnWorldItem`) |

No protocol/wire change, no new message, no kernel model change. The fix is
local to the display boundary and the domain lookup defense.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Display content ids removed before restore | `CloneInventoryContentSanitizer.WithoutInstanceIds` is called from `RestoreRemoteContents` | `CloneInventoryContentSanitizerTests` assert nested ids are zero and source data is not mutated |
| Domain lookup cannot address display proxies | `FindWorldItem`/`FindExistingAt` skip `RemoteCloneRender` items | Static code path audit; covered by the same contract family (proxies are not domain objects) |
| Authoritative restores keep ids | No change to `ItemStateCodec.RestoreContents` default behavior | Existing restore/container tests still pass |
| No protocol/architecture expansion | No NetMsg/wire/kernel edits | `git diff` contains only GameAdapter + test + docs |

## 4. Verification design (development-period, no manual acceptance)

- **Red**: before the fix, `CloneInventoryContentSanitizerTests` failed with
  `TypeLoadException` because the sanitizer type did not exist.
- **Green**: after the fix, the focused tests pass.
- **Full regression**: `dotnet test CasualtiesUnknownOnline.slnx --no-build` — 2226 passed / 0 failed.
- **Gates**: `dotnet format`, `check-architecture.ps1`, `check-event-replay.ps1`,
  `check-entity-event-dispatch.ps1`, `check-delivery.ps1` pass.
- **Runtime evidence**: development-period rule — L0 simulation + static
  evidence + real-game-dir deploy; no manual acceptance this cycle.

## 5. Structure review

- `CloneInventoryContentSanitizer` is a small pure type; no new mutable state.
- `CloneInventoryRenderer` remains under the type-size gate.
- `RemoteItemSceneOps` additions are narrow lookup filters, not new responsibilities.
- No dead mechanisms were left behind; the sanitizer is the single display-boundary ownership point.

## 6. Accepted boundaries

- This cycle does not add new remote backpack interactions; it fixes the proxy/domain
  identity ambiguity that can show carried container contents as world drops.
- If a real domain drop/correction legitimately targets an id, the lookup will
  now find only authoritative objects; display proxies are ignored by design.
