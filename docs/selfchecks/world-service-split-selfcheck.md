# WorldService message-flow split — self-check

Owner cycle: backlog architecture & quality debt — "Large logical class debt
flattening" (continued). Decision: split the 899-line logical `WorldService`
into a thin `IWorldControl` facade plus two real top-level responsibility
classes: `WorldStateMessageService` (world block/building/state message flow)
and `WorldChannelRelay` (entity/trader/speech/chat channel forwarding). No
behavior, DI, wire or protocol change.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| `WorldService.cs` | Was 235-line main partial + four more partials (899 aggregate); now a 423-line normal facade implementing `IWorldControl`, keeping only the start-gate lifecycle, session reset and delegation. |
| `WorldStateMessageService.cs` | New 422-line top-level class owns block/building/world-state message flow: block-damage backfill, block-state table, radiation-line snapshot source, world-start params, world join, earthquake, keypad/geyser and block damaged send/receive. |
| `WorldChannelRelay.cs` | New 143-line top-level class owns the pure channel-forwarding surface for entity events, trap/opened/health/layout/fluid, trader, speech and chat. |
| Deleted partials | `WorldService.Channels.cs`, `WorldService.BlockDamage.cs`, `WorldService.MessageFlow.cs`, `WorldService.SessionState.cs` removed. |
| DI registration | Unchanged — `WorldService` remains the single `IWorldControl` implementation registered in `CuoBootstrap`; the two new classes are internal owned dependencies. |
| Wire/protocol | No NetMsg, no ProtocolVersion, no direction row, no payload change. |

## 2. Whole-family audit

- The old logical class had three distinct responsibilities: start-gate
  lifecycle, block/building/world message flow, and pure channel forwarding.
  The new split gives the latter two first-class owners while the facade keeps
  the lifecycle.
- Mutable state ownership: `_damagedBlocks`, `WorldParams` and
  `RadiationLineState` moved with the message-flow owner; start-gate state stays
  in `WorldService`.
- Event forwarding is explicit through the facade; handlers and the Game
  Adapter still see the same `IWorldControl` surface.
- No dead mechanism: all methods/events were moved verbatim and delegated.
- No new expression-state bool fields.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Logical `WorldService` | 899 aggregate → 423-line facade | `WorldService.cs` |
| World message flow | New 422-line top-level class | `WorldStateMessageService.cs` |
| Channel relay | New 143-line top-level class | `WorldChannelRelay.cs` |
| Old partials | Removed | `git status` / deleted files |
| One top-level type per file | New files contain exactly one top-level type | `check-architecture.ps1` |
| DI / readonly wiring | Unchanged | Source diff; build |
| Wire/protocol | Unchanged | Full suite + gates |
| Runtime behavior | No semantic change expected | Full suite |

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1250 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | source clean |
| `tools/check-architecture.ps1` | passed; `WorldService` removed from debt ledger |
| `tools/check-event-replay.ps1` | passed (33 events) |
| `tools/check-entity-event-dispatch.ps1` | passed (33 kinds × 3 tables) |
| `tools/deploy.ps1 -GameDir "<real game dir>"` | deployed to the real game dir only |
| `tools/check-delivery.ps1 -Check` | passed (9 boxes checked) |
| Protocol | unchanged (38) |

## 5. Verification design (development-period, no manual acceptance)

- L0: full build + full test suite + architecture/event gates.
- Static: source diff is a responsibility split; the moved method bodies are
  verbatim in the new classes, and the facade is explicit delegation.
- Runtime: no manual dual-side acceptance (user rule 2026-08-16); no game
  behavior path changed.

## 6. Plan approval

The user instructed this session to continue the autonomous backlog item and
complete adjacent architecture work. This cycle's plan is approved without a
separate interactive approval step.

## 7. Structure review

- All touched files are under the 600-line gate; `WorldService` is no longer in
  `docs/architecture-debt.json`.
- One top-level type per file.
- No new expression-state bool fields.
- The new classes are internal owned dependencies, not DI-visible shared state.
- Backlog updated: `WorldService` removed from the remaining flattening list
  and the architecture watchlist (the old main file no longer exists).
