# GameAdapter split — self-check

Owner cycle: backlog architecture & quality debt — "Large logical class debt
flattening" (continued). Decision: split the recorded 1397-line logical
`GameAdapter` (fourteen physical partials) into a thin facade plus real
top-level responsibility classes. No behavior, DI wiring, wire or protocol
change.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| `GameAdapter.cs` | Was a 481-line partial plus thirteen more partials (1397 aggregate); now a 299-line normal facade implementing `IGameAdapter`, `ICuoService`, `IModEntitySpawner`, `IModNativeApiProvider`. It keeps the lifecycle (probe/install/uninstall), the Update pump, the Runtime boundary methods and the mod native-API adapter. |
| `GameAdapterDomains.cs` | New 179-line top-level class owns the deep sync module set and constructor wiring (all domain fields, no behavior). It is internal owned state, not a DI service. |
| `GameAdapterBridge.cs` | New 277-line top-level class implements all of `IPatchBridge`; `GameAdapter` binds this object into `PatchBridge` instead of binding `this`. |
| `GameAdapterSessionBinding.cs` | New 117-line top-level class owns session bind/unbind, session-led item events and session-ended resets. |
| `PlayerInteractionApply.cs` | New 392-line top-level class owns cross-player inventory transfer, carry/release and heal application plus the Online UI heal-item projection. |
| Deleted partials | `GameAdapter.Construction.cs`, `GameAdapter.PlayerInteraction.cs`, `GameAdapter.CarryInteraction.cs`, `GameAdapter.HealInteraction.cs`, `GameAdapter.NativeApi.cs`, `GameAdapter.Enemy.cs`, `GameAdapter.SessionEvents.cs`, `GameAdapter.SessionEnded.cs`, `GameAdapter.CharacterSound.cs`, `GameAdapter.Heater.cs`, `GameAdapter.Limb.cs`, `GameAdapter.Time.cs`, `GameAdapter.ModEntitySpawn.cs`, `GameAdapter.TraderRecruit.cs` removed. |
| DI registration | Unchanged — `GameAdapter` remains the single `IGameAdapter` / `ICuoService` / `IModEntitySpawner` / `IModNativeApiProvider` implementation; the new classes are internal owned dependencies. |
| Wire/protocol | No NetMsg, no ProtocolVersion, no direction row, no payload change. |

## 2. Whole-family audit

- The old logical class mixed construction, patch-bridge forwarding, session
  wiring and direct-player-interaction apply sides in one partial family. The
  new split gives each responsibility a first-class real top-level owner.
- State ownership: the domain module set lives in `GameAdapterDomains`; the
  carry pending state lives in `PlayerInteractionApply`; the Harmony patch
  bridge is bound to `GameAdapterBridge`.
- Event wiring is explicit: `GameAdapterSessionBinding` subscribes/unsubscribes
  the runtime item and player-interaction events and calls
  `PlayerInteractionApply`.
- No dead mechanism: all moved method bodies are verbatim or explicit
  delegation; `PatchBridge.Bind(_bridge)` keeps the same static seam.
- No new expression-state bool fields.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Logical `GameAdapter` | 1397 aggregate → 299-line facade | `GameAdapter.cs` |
| Domain wiring | New 179-line top-level class | `GameAdapterDomains.cs` |
| Patch bridge | New 277-line top-level class | `GameAdapterBridge.cs` |
| Session wiring | New 117-line top-level class | `GameAdapterSessionBinding.cs` |
| Player interaction apply | New 392-line top-level class | `PlayerInteractionApply.cs` |
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
| `tools/check-architecture.ps1` | passed; `GameAdapter` removed from debt ledger |
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

The user instructed this session to continue the autonomous architecture
backlog item. This cycle's plan is approved without a separate interactive
approval step.

## 7. Structure review

- All touched files are under the 600-line gate; `GameAdapter` is no longer in
  `docs/architecture-debt.json`.
- One top-level type per file.
- No new expression-state bool fields.
- The new classes are internal owned dependencies, not DI-visible shared state.
- Backlog updated: the large logical class debt flattening list is now empty.
