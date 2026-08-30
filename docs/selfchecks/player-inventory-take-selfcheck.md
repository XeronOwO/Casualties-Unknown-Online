# Player inventory take — direct player interaction slice (ProtocolVersion 26)

> **HISTORICAL** — This selfcheck describes a superseded/removed wire path or
> an intermediate architecture slice. It is retained for audit history, not as
> current evidence. Check `docs/selfchecks/MANIFEST.md` and
> `docs/architecture-evolution/protocol.md` before citing.

Owner cycle: backlog "Direct player interaction (view/take items, carry, view
vitals, heal)" — the remaining take half. With view vitals + view items
already landed, this slice implements the **"take items from another player"**
operation. Carry and heal remain open.

Decision summary:

- The host is the cross-player authority for every take. It reads the
  authoritative per-player character snapshots (the host's own latest host
  snapshot + every guest's saved 1 Hz report), moves one carried item between
  them, updates the guest transfer table where a guest is a participant, and
  sends each participant one authoritative `PlayerInventoryTransferMsg`.
- The receiving Game Adapter mutates the local body inside a `RemoteApply`
  scope (no echo reports) and immediately re-reports the character snapshot,
  so the host save and every clone converge on the real local slot within the
  same run.
- The host picks a concrete empty slot from the latest target character
  snapshot (`CharacterDataMsg.SlotCount` + occupied slot indexes) before
  sending the transfer; the recipient's live body still verifies and falls back
  to `FirstEmptySlot()` if the snapshot is stale.
- Permission default follows the KrokMP-compatible cooperative rule: only an
  **unconscious or dead** remote body can be searched/taken from. The Online UI
  shows the Take button only in that state and the host re-checks the
  authoritative snapshot.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Local UI | `OnlineUiOverlay.DrawMemberStatus` renders one **Take** button per backpack/hand-slot item (`SlotIndex >= 0`) when the target is an in-world remote and its vitals show `!Conscious || !Alive`. The button calls `Plugin.TryTakeItemFromRemote` → `PlayerInteractionService.SendTakeRequest` |
| 2 | Wire request | `PlayerInventoryTakeRequestMsg` (NetMsg 97, guest → host) carries the owner SteamId + item instance id. `PacketReceiver` direction table locks it guest→host |
| 3 | Host authority | `PlayerInteractionService.HandleTakeRequest` resolves the item from the host's own cached snapshot (`CharacterDataStore.GetHostCharacterData` / `BroadcastHostCharacterData` cache) or a guest's saved report, and refuses when the target is conscious/alive, not in-world, unknown, or worn |
| 4 | Authority update | The service clones the source/target `CharacterDataMsg`, picks a concrete empty slot from `SlotCount` + occupied slot indexes, removes the item from source, adds it to target, saves both back, and moves the guest transfer-table record between participants (`ItemArbitration.AdoptTransferredItem` / `RemoveTransferredItem`) |
| 5 | Wire result | `PlayerInventoryTransferMsg` (NetMsg 98, host → participant) carries From/To + the full item fact. `PacketReceiver` locks host→guest; the host also fires the same event locally for its own participant half |
| 6 | Local apply | `GameAdapter.PlayerInteraction.cs`: source side destroys the item object by instance id (slots + worn), target side re-instantiates via `ItemStateCodec.RestoreItem` into `Body.FirstEmptySlot()`; both wrapped in `CallContext.Origin.RemoteApply` |
| 7 | Immediate re-report | After the local mutation `CharacterDataSync.ReportInventoryChanged(body)` sends the full character snapshot immediately — the real slot reaches the host/peers without waiting for the next 1 Hz tick |
| 8 | Protocol bump | New wire messages require protocol version 26 (`ProtocolVersion.Current`) |

## 2. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| UI button surface | Only unconscious/dead target + slot items shown | Vitals projection + `RemoteInventorySnapshot` now carries `InstanceId`; Online UI reads it |
| Host validates ownership | Source snapshot must contain the exact instance id | `PlayerInteractionService.HandleTakeRequest` + tests (`Take_UnknownItem_IsRefused`) |
| Host validates state | Conscious alive target refused | `source.Health.Conscious && source.Health.Alive` rule; test locks it |
| Host moves authority | Source snapshot loses item, target gains it, transfer table follows for guests | Tests assert both saved snapshots + `IItemControl.GetTransferredItems` |
| Both participants apply | Source destroys, target restores, no local report echo | `RemoteApply` scope + immediate re-report; static evidence in `GameAdapter.PlayerInteraction.cs` |
| Wire direction | TakeRequest only received by host; Transfer only received by guest | `PacketReceiver.IsValidDirection` updated + `DirectionTests` classification completeness |
| No protocol envelope | One operation = one transfer message; no per-item bulk correction | `PlayerInventoryTransferMsg` single payload |

## 3. Verification design (development-period, no manual acceptance)

- L0 Runtime wire tests (`PlayerInteractionServiceTests`): guest→host and
  host→guest takes, conscious refusal, unknown-item refusal, worn-item refusal,
  ownership record movement.
- `RemoteInventoryServiceTests` add the instance-id projection so the UI's
  Take button has a stable key.
- `DirectionTests` classify both new messages and keep the every-NetMsg
  completeness guard.
- Full suite: **1044 tests green** (L0 simulation + static evidence,
  no manual acceptance — user rule 2026-08-16).

## 4. Structure review

- `PlayerInteractionService` is one responsibility (host-authoritative
  cross-player transfer), no pump, no session-mutable state beyond the
  character-data/transfer-table owners.
- `GameAdapter.cs` stays under 600 lines after moving session-event forwards to
  `GameAdapter.SessionEvents.cs`; the local apply lives in
  `GameAdapter.PlayerInteraction.cs` (partial).
- `ItemService.cs` stays under 600 after the two transfer-table seams moved to
  `ItemService.PlayerInteraction.cs`.
- No new expression-state bool fields; no dead mechanisms added.

## 5. Accepted boundaries

- Worn items (`SlotIndex < 0`) are not takeable in this slice.
- The host chooses the target slot from the latest snapshot, but the recipient
  remains the live slot authority: if the snapshot is stale the local body uses
  `FirstEmptySlot()` and the immediate re-report corrects the host.
- Carry and heal remain separate open backlog items.
