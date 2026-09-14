# Remote container content view — Online UI projection (no protocol bump)

> **HISTORICAL** — This selfcheck describes a superseded/removed wire path or
> an intermediate architecture slice. It is retained for audit history, not as
> current evidence. Check Rdocs/evidence/selfchecks/MANIFEST.mdR and
> Rdocs/architecture/protocol.mdR before citing.

Owner cycle: backlog "Open another player's inventory/container — content sync
and clone fact tables are correct, but the renderer does not display a remote
player's container contents; a remote inventory UI remains." Decision for this
cycle: close the **view** half by projecting the already-wire-carried recursive
RCharacterItemMsg.ContentsR into the read-only remote-inventory snapshot and
rendering the nested container lines in the Online UI. No wire change, no
ProtocolVersion bump.

Decision summary:

- RRemoteInventoryEntryR now carries a recursive RIReadOnlyList<RemoteInventoryEntry> ContentsR
  instead of only a count. RContentsCountR remains as a derived convenience for
  the compact top-level line.
- RRemoteInventorySnapshot.FromR projects the recursive RCharacterItemMsg.ContentsR
  tree; RToDisplayLines()R renders each container child indented beneath its
  parent with a R↳R marker.
- ROnlineUiOverlayR renders the same nested lines in the member status list.
  Container contents are display-only; taking a nested item is not part of this
  slice (the existing Take operation remains top-level slot items only).
- The 1 Hz character-data stream already carried the nested facts, so this is
  purely a UI/render projection — the same pattern as the earlier
  remote-inventory-view slice.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Character snapshots already carry recursive contents | RCharacterItemMsg.ContentsR (RCharacterItemMsg.cs:38R, recursive R[ProtoMember(7)]R); RRemoteInventoryServiceTestsR and RNetPacketTests.CharacterData_EveryFieldFamily_RoundTripsR (the wire codec) already round-trip nested items |
| 2 | Remote-inventory cache exists | RRemoteInventoryServiceR fills from RCharacterDataReceivedR / RHostCharacterDataReceivedR and already clears on world leave / session end |
| 3 | Projection was collapsing contents to a count | Old RRemoteInventorySnapshot.FromR only called Ritem.Contents.CountR (RRemoteInventorySnapshot.csR before this cycle) |
| 4 | UI already rendered the member inventory list | ROnlineUiOverlay.DrawMemberStatusR printed top-level lines with R(+N inside)R but no child rows |
| 5 | No new wire contract | The same RCharacterItemMsg.ContentsR data used by RCloneFactTableR / RItemStateCodec.RestoreContentsR is projected read-only for UI |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| RRemoteInventoryEntryR | Add recursive RContentsR; keep RContentsCountR as derived property; still immutable |
| RRemoteInventorySnapshotR | Recursive RProjectR, recursive RToDisplayLinesR |
| ROnlineUiOverlayR | Render nested container rows via a recursive RDrawContainerContentsR helper |
| Existing item/content channels | Unchanged — no new NetMsg, no changes to RContainerItemSyncR, RCloneFactTableR, or RItemStateCodecR |
| Existing take/carry/heal UI | Unchanged — nested container items remain non-takeable; the top-level Take button logic is untouched |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Recursive contents projected | RRemoteInventorySnapshot.FromR builds nested RRemoteInventoryEntry.ContentsR | RSnapshot_ProjectsRecursiveContainerContentsR (levels: backpack → inner → deep) |
| Display lines include nested rows | RToDisplayLinesR adds indented R↳R children | RSnapshot_ProjectsItemsAndFormatsR + RSnapshot_ProjectsRecursiveContainerContentsR assert child lines |
| RContentsCountR remains stable | Derived from RContents.CountR; top-level compact line unchanged | Existing R(+N inside)R assertions still pass |
| Online UI renders nested rows | RDrawContainerContentsR walks Rentry.ContentsR recursively | Static UI code; pure projection has L0 test face; UI itself is display-only |
| No protocol change | No NetMsg / message / ProtocolVersion edits | Rgit diffR contains only Runtime/Plugin/test/docs files |
| No stale cross-session data | Service lifecycle/cache-clearing behavior unchanged | Existing RRemoteLeavingWorld_ClearsThatPlayersInventoryR / RSessionEnd_ClearsTheCacheR still pass |

## 4. Verification design (development-period, no manual acceptance)

- **L0 service tests** (RRemoteInventoryServiceTestsR): recursive projection,
  nested display formatting, derived count; 9 tests in this class.
- **Full regression**: Rdotnet test CasualtiesUnknownOnline.slnx --no-buildR —
  **1134 passed / 0 failed**.
- **Gates**: Rdotnet formatR, Rcheck-architecture.ps1R,
  Rcheck-event-replay.ps1R, Rcheck-entity-event-dispatch.ps1R all pass.
- **Runtime evidence**: development-period rule — L0 simulation + static
  evidence + real-game-dir deploy; **no manual acceptance** (user 2026-08-16).

## 5. Plan approval

The user instructed this session to pick one backlog item autonomously and
complete it, then write the result back into R../backlog.mdR
("由你来自主挑选一个并完成，记得在完成之后回写 backlog"). That instruction is
the plan approval for this cycle; no further interactive approval is required.

## 6. Verification results (2026-08-22)

| Evidence | Result |
|---|---|
| Rdotnet build CasualtiesUnknownOnline.slnxR | 0 warnings / 0 errors |
| Rdotnet test CasualtiesUnknownOnline.slnx --no-buildR | 1134 passed / 0 failed |
| Rdotnet format CasualtiesUnknownOnline.slnxR | clean on source |
| Rcheck-architecture.ps1R / Rcheck-event-replay.ps1R / Rcheck-entity-event-dispatch.ps1R | all passed |
| Rtools/deploy.ps1 -GameDir "<game-dir>"R | deployed to the real game dir only |
| Protocol | unchanged (no bump) |

## 7. Structure review

- RRemoteInventoryEntry.csR remains a one-line immutable record plus one
  derived property; RRemoteInventorySnapshot.csR ~104 lines;
  ROnlineUiOverlay.csR remains under the 600-line gate.
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
