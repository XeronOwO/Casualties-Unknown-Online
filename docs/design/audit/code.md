# Code audit: the corpus against the code, both directions

The ticket audit (`README.md`) checks every ticket against the code and the corpus. This ledger is the other axis: it takes
the CORPUS page by page and checks its rules against the code, in both directions —

- **code → page**: a detail the code has and the page does not state (a threshold, a state, a field's meaning) is added to
  the page;
- **page → code**: every rule the page states is found in the code, or the disagreement is judged: `page-wrong` (the page is
  corrected), `code-drift` (a ticket is filed, with the rule, the code and the difference), or `needs-decision` (the
  question goes to the user with both sides quoted).

## Verdicts

| Verdict | Meaning |
|---|---|
| `agrees` | the code implements the rule as the page states it |
| `page-wrong` | the page overstates, understates or misnames what the code does; the page is corrected in this cycle |
| `code-drift` | the code contradicts a rule the page holds from a record; a ticket is filed rather than the code changed |
| `needs-decision` | the records do not settle which side is right; the user is asked with both sides quoted |

## The ledger

| # | Page | Rule (quoted) | Code landing | Verdict | Action |
|---|---|---|---|---|---|
| 1 | `model/authority-and-judgement.md` | "the kernel declares FOUR kinds: `HostOnly`, `OwnerPredictedHostValidated`, `TriggerObservedHostCommitted`, `PresentationOnly`" | `src/CasualtiesUnknownOnline.GameState/AuthorityKind.cs` declares exactly those four; `KernelCommandGateway.cs` switches on `HostOnly` and `PresentationOnly` | `page-wrong` (fixed) | the page had abstracted three kinds; it now names the code's four and cites decision 157 for why the two predicted ones are declared without a current user |
| 2 | `model/authority-and-judgement.md` | "the host checks a report against a fact it already holds" | `KernelCommandGateway.cs`: "a `HostOnly` fact is authored by the host's own simulation and `PresentationOnly` is never a" report | `agrees` | none |
| 3 | `model/item-custody.md` | "a favourite mark… is saved with that player's character record, and never reaches the owner's item" | `src/CasualtiesUnknownOnline.GameState/Domains/Items/ItemData.cs` declares `bool Favourited` on the kernel item row, and `KernelWireMapper` carries it | `page-wrong` (fixed) | the page now says what decision 258 means (the OTHER player's item is never reached) and that the kernel records the flag on the item once it changes hands, quoting the accepted ticket |
| 4 | `model/item-custody.md` | "an id is never re-issued to a live item" | `src/CasualtiesUnknownOnline.Runtime/Session/Items/ItemIdCoordinator.cs` owns the allocator and the restore seeds it | `agrees` | none |
| 5 | `model/session-lifecycle.md` | "a player the world has no character for is supplied as a NEW player" | `StartingSupplyCoordinator` and `StartingSupplyPolicy` own the new-player path | `agrees` | none |
| 6 | `cross-cutting/permissions-and-commands.md` | "a host rule is decided by the host and reaches every member that reads it" | `src/CasualtiesUnknownOnline.Runtime/Configuration/HostRulesOptions.cs` declares the rule set: `PvpEnabled`, `AutoContinue`, `AllowLateJoin`, `RemoteBackpackAccess`, `WidenRunSettings`, `PiggybackWeightMultiplier`, `AllowMemberCheatVerbs`, `NativeBindingParity` | `agrees`; a detail the page lacked | the page gains the rule list, because "a host rule" was abstract without it |
| … | the remaining pages (mechanics 15, items 8, surfaces 9, cross-cutting 3) | | | | |
