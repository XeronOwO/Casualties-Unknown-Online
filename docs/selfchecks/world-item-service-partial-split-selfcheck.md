# WorldService / ItemService partial split — self-check

Owner cycle: backlog architecture watchlist (files at/near the 600-line gate).
Decision: split the two largest runtime service cursors into focused message-flow
partials, without changing any behavior, domain ownership, DI registration,
wire surface or protocol. This is the same shape already used for
`GameAdapter.Construction.cs` and the existing `WorldService.Channels.cs` /
`ItemService.PendingPickups.cs` partials.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| `WorldService.cs` | Previously exactly 600 lines; kept only the world-defining state, constructor and start-gate lifecycle (233 lines). |
| `WorldService.MessageFlow.cs` | New partial (385 lines) owns block/building/world-state events, report/send/broadcast plumbing and the late-joiner block-difference snapshot. |
| `ItemService.cs` | Previously 597 lines; kept constructor, events, position stream, carried facts, host-only snapshot/lifecycle, crafting seams and interface forwards (309 lines). |
| `ItemService.ReportReceive.cs` | New partial (306 lines) owns the report/receive message-flow surface: spawn/drop/use/cook/destroy sends, wire receive events, block-drop registration, corrections and action/snapshot receive forwarding. |
| Partial consistency | Both services were already `sealed partial`; the new files restate the partial type without duplicating fields, constructors, base lists or existing partials. |
| No behavior change | No method bodies were rewritten; the moved blocks were copied verbatim, only relocated into the same partial type. No new class, no new state bool, no new message id. |

## 2. Whole-family audit

- `WorldService` already had `WorldService.Channels.cs`, `WorldService.BlockDamage.cs`
  and `WorldService.SessionState.cs`; this cycle adds `MessageFlow.cs` for the
  remaining world-state message surface.
- `ItemService` already had `ItemService.PendingPickups.cs`,
  `ItemService.PlayerInteraction.cs` and `ItemService.Traffic.cs`; this cycle adds
  `ReportReceive.cs` for the report/receive flow.
- No domain moved out of its owner: all state remains in the service classes and
  their existing sub-services; the new files are pure cursor/plumbing surfaces.
- No dead mechanism: nothing was replaced, duplicated or deleted.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| World cursor | 600 → 233 lines | `WorldService.cs` |
| World flow cursor | New 385-line partial | `WorldService.MessageFlow.cs` |
| Item cursor | 597 → 309 lines | `ItemService.cs` |
| Item flow cursor | New 306-line partial | `ItemService.ReportReceive.cs` |
| One top-level type per file | New files contain exactly one partial class | `check-architecture.ps1` |
| Readonly / DI wiring | Unchanged — constructors and field assignments untouched | Source diff; build |
| Wire/protocol | Unchanged — no NetMsg, no ProtocolVersion, no direction row | Full suite + gate |
| Runtime behavior | No semantic change expected | Full suite |

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1191 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | source clean; verify-no-changes flags only the gitignored generated `obj/.../MyPluginInfo.cs` |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` | passed (32 events) |
| `tools/check-entity-event-dispatch.ps1` | passed (32 kinds × 3 tables) |
| `tools/deploy.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Casualties Unknown Demo"` | deployed to the real game dir only (see §5) |
| Protocol | unchanged (32) |

## 5. Verification design (development-period, no manual acceptance)

- L0: full build + full test suite + architecture/event gates.
- Static: source diff is pure relocation; no new behavior path.
- Runtime: real-game-dir deploy only; no manual dual-side acceptance
  (user rule 2026-08-16).

## 6. Plan approval

The user instructed this session to pick a backlog item autonomously and
complete it ("由你来自主挑选一个并完成"), so this cycle's plan is approved
without a separate interactive approval step.

## 7. Structure review

- All touched files are under the 600-line gate.
- One top-level type per file.
- No new expression-state bool fields.
- State remains owned by the same service/sub-service owners; the new partials
  are cursor/plumbing only.
- Backlog watchlist updated: `WorldService.cs` and `ItemService.cs` are no
  longer listed as at/near the gate.
