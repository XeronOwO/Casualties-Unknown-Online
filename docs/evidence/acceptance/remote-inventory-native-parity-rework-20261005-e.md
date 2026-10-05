# Acceptance record — Remote inventory native parity rework (batch `20261005-e`)

- Ticket: `remote-inventory-native-parity-rework` — verdict: **stays in `review/`** (rows 4 and the
  battery-unload half pass; row 8, the combine and favourite halves and the battery-load half stay
  open)
- Batch: `20261005-e` — tickets `container-move-snapshot-only-sync`,
  `remote-inventory-native-parity-rework`
- Commit: `cdd93044` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+cdd93044b027327fd2ec2b4269b90e43b56999db`
- Run, dependencies and artifact directory: the same three-client session as
  `container-move-snapshot-only-sync-20261005-e.md`, whose header carries them in full
- Why these rows ride that batch: the same native gesture primitive, the same trash-bag fixture and the
  same three clients serve both tickets

## The rows this batch judges

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 4 | Take an item back out of the nested container | machine | **pass** | Driven in both owner directions through the container's own contents window. Host owner: the operator opened the bag (`container-panel mode=remote` → `contentsCount: 1`), dragged the contained child out and released it over empty space (recipe answer `itemParentContainer: true`) → the owner logs `[ItemTrace] op=17 … origin=OnItemUnloadedFromContainer result=Committed(1) events=[Unload]` + `[RemoteIntent] replayed native TakeOutOfContainer on item 1172847052739 (container 1168552085443, slot -1).`; the child then reads in the world on the third peer's own view (`cm17-alt-world-after-drop.json`, `dogfood id 1172847052739 x=4.323 y=482.894`) — it came out and stayed out. Guest owner: the host drove the same gesture on the guest's bag (`cm14-host-takeout-guestdogfood.json`) → owner `OnItemUnloadedFromContainer … events=[Unload]` + `replayed native TakeOutOfContainer`, third peer `dogfood id 13787924117 x=3.086 y=483.575` |
| 7 — battery **unload** half | `UnloadBattery` from the remote view | machine | **pass** | Guest `[RemoteIntent] UnloadBattery captured for item 1026818164675 of 76561198281246659` → owner `[PickUpResult] mediumbattery → slot (slot 1) at (7.6,437.5).` + `[RemoteIntent] the native unload ejected item aed's battery onto the owner's body.` + `replayed native UnloadBattery`; both viewers show the new carried item arriving as its own fact (`[CarriedSync] added mediumbattery (id 1207206791107) to …'s snapshot — re-rendering the clone.`) with no divergence |
| 7 — battery **load** half | `LoadBattery` from the remote view | machine | **unproven** | Not driven: it needs a battery item in the owner's ring AND a receiver item with a battery slot, and this session's fixture budget went to the unload half (whose first two attempts the native guard refused because a `Utils.Create`d copy carries the battery component without `hasBattery`) |
| 14 — monitor half | Four items into the remote trash bag | machine | **partially re-read** | The dog food insert of this batch was read twice (before the take-out and as the re-insert) and is silent on both viewers (`marks-cm-takeout2.txt`); the four-item series itself was not re-driven, so the ticket's earlier reading stands for water bottle, lantern and metal scrap |
| 8 | Held remote item, backpack closed, used from the medical panel | machine | **not judged** | Needs the treatable-limb fixture and the wound-view staging; unchanged from batch `20261005-d` |

## Notes this run adds to the ticket

- The take-out row's earlier `unproven` verdict is closed by the container's own contents window: the
  child proxy only exists in the operator's scene once `PlayerCamera.OpenContainer(proxy)` has run, which
  is what `container-panel mode=remote` stages. A take-out released onto an empty ring slot is classified
  `PickUpToSlot` (the owner's replay then unloads the child through `Body.PickUpItem`, so the child does
  leave the container — the run read `OnItemUnloadedFromContainer` there too); the classification the
  ticket's row 4 is about (`TakeOutOfContainer`) needs the release **into the world**.
- The battery fixture is narrower than it looks: world-generated items carry an installed battery
  (`battery.hasBattery`), `Utils.Create`d copies do not, and only the installed one satisfies the
  native R2 guard. Bringing a world item to the body is what made the row drivable
  (`probe-battery-bring2.cs`).
