# Remote container content view — Online UI projection (no protocol bump)

> **HISTORICAL** — This selfcheck describes a superseded/removed wire path or
> an intermediate architecture slice. It is retained for audit history, not as
> current evidence. Check `docs/evidence/selfchecks/MANIFEST.md` and
> `docs/architecture/protocol.md` before citing.

Owner cycle: backlog "Open another player's inventory/container — content sync
and clone fact tables are correct, but the renderer does not display a remote
player's container contents; a remote inventory UI remains." Decision for this
cycle: close the **view** half by projecting the already-wire-carried recursive
`CharacterItemMsg.Contents` into the read-only remote-inventory snapshot and
rendering the nested container lines in the Online UI. No wire change, no
ProtocolVersion bump.

Decision summary:

- `RemoteInventoryEntry` now carries a recursive `IReadOnlyList<RemoteInventoryEntry> Contents`
  instead of only a count. `ContentsCount` remains as a derived convenience for
  the compact top-level line.
- `RemoteInventorySnapshot.From` projects the recursive `CharacterItemMsg.Contents`
  tree; `ToDisplayLines()` renders each container child indented beneath its
  parent with a `↳` marker.
- `OnlineUiOverlay` renders the same nested lines in the member status list.
  Container contents are display-only; taking a nested item is not part of this
  slice (the existing Take operation remains top-level slot items only).
- The 1 Hz character-data stream already carried the nested facts, so this is
  purely a UI/render projection — the same pattern as the earlier
  remote-inventory-view slice.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Character snapshots already carry recursive contents | `CharacterItemMsg.Contents` (`CharacterItemMsg.cs:38`, recursive `[ProtoMember(7)]`); `RemoteInventoryServiceTests` and `CharacterDataFileStoreTests` already round-trip nested items |
| 2 | Remote-inventory cache exists | `RemoteInventoryService` fills from `CharacterDataReceived` / `HostCharacterDataReceived` and already clears on world leave / session end |
| 3 | Projection was collapsing contents to a count | Old `RemoteInventorySnapshot.From` only called `item.Contents.Count` (`RemoteInventorySnapshot.cs` before this cycle) |
| 4 | UI already rendered the member inventory list | `OnlineUiOverlay.DrawMemberStatus` printed top-level lines with `(+N inside)` but no child rows |
| 5 | No new wire contract | The same `CharacterItemMsg.Contents` data used by `CloneFactTable` / `ItemStateCodec.RestoreContents` is projected read-only for UI |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `RemoteInventoryEntry` | Add recursive `Contents`; keep `ContentsCount` as derived property; still immutable |
| `RemoteInventorySnapshot` | Recursive `Project`, recursive `ToDisplayLines` |
| `OnlineUiOverlay` | Render nested container rows via a recursive `DrawContainerContents` helper |
| Existing item/content channels | Unchanged — no new NetMsg, no changes to `ContainerItemSync`, `CloneFactTable`, or `ItemStateCodec` |
| Existing take/carry/heal UI | Unchanged — nested container items remain non-takeable; the top-level Take button logic is untouched |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Recursive contents projected | `RemoteInventorySnapshot.From` builds nested `RemoteInventoryEntry.Contents` | `Snapshot_ProjectsRecursiveContainerContents` (levels: backpack → inner → deep) |
| Display lines include nested rows | `ToDisplayLines` adds indented `↳` children | `Snapshot_ProjectsItemsAndFormats` + `Snapshot_ProjectsRecursiveContainerContents` assert child lines |
| `ContentsCount` remains stable | Derived from `Contents.Count`; top-level compact line unchanged | Existing `(+N inside)` assertions still pass |
| Online UI renders nested rows | `DrawContainerContents` walks `entry.Contents` recursively | Static UI code; pure projection has L0 test face; UI itself is display-only |
| No protocol change | No NetMsg / message / ProtocolVersion edits | `git diff` contains only Runtime/Plugin/test/docs files |
| No stale cross-session data | Service lifecycle/cache-clearing behavior unchanged | Existing `RemoteLeavingWorld_ClearsThatPlayersInventory` / `SessionEnd_ClearsTheCache` still pass |

## 4. Verification design (development-period, no manual acceptance)

- **L0 service tests** (`RemoteInventoryServiceTests`): recursive projection,
  nested display formatting, derived count; 9 tests in this class.
- **Full regression**: `dotnet test CasualtiesUnknownOnline.slnx --no-build` —
  **1134 passed / 0 failed**.
- **Gates**: `dotnet format`, `check-architecture.ps1`,
  `check-event-replay.ps1`, `check-entity-event-dispatch.ps1` all pass.
- **Runtime evidence**: development-period rule — L0 simulation + static
  evidence + real-game-dir deploy; **no manual acceptance** (user 2026-08-16).

## 5. Plan approval

The user instructed this session to pick one backlog item autonomously and
complete it, then write the result back into `../backlog.md`
("由你来自主挑选一个并完成，记得在完成之后回写 backlog"). That instruction is
the plan approval for this cycle; no further interactive approval is required.

## 6. Verification results (2026-08-22)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1134 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean on source |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | all passed |
| `tools/deploy.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Casualties Unknown Demo"` | deployed to the real game dir only |
| Protocol | unchanged (no bump) |

## 7. Structure review

- `RemoteInventoryEntry.cs` remains a one-line immutable record plus one
  derived property; `RemoteInventorySnapshot.cs` ~104 lines;
  `OnlineUiOverlay.cs` remains under the 600-line gate.
- One top-level type per file; no new expression-state bools; the recursive
  contents state stays inside the immutable snapshot, not a shared mutable
  service.
- Dead mechanisms: none. The projection is a new read-only consumer of the
  existing character-data stream, not a duplicate item channel.

## 8. Accepted boundaries

- Nested container items are **view-only**; the existing Take button is for
  top-level slot items only.
- No open/close/collapse UI, no remote container mutation, no world-container
  UI (only the remote player's carried/worn container items are projected).
- The rendered child line does not include condition/slot (the child's slot is
  the parent's slot in the wire shape, so it is not a meaningful independent
  position); item id + nested count + favourite flag are enough for a status
  view.
