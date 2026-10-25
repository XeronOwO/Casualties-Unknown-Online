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
| 7 | `mechanics/medical-view.md` | "only the two families where the TARGET consumes the item need a conscious target" | `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/ItemUseTargetPolicy.cs`: `RequiresAConsciousTarget` is true for `Drink` and `SolidFood` only, and a dead body is refused for every family | `agrees`; a code-only detail added | the page now states that the split mirrors `MedicalTargetBodyValidator`, which requires a responsive patient only for `Injection` and `Shrapnel`, and that no item family is the defibrillator |
| 8 | `mechanics/item-use.md` | "the family decides which native action runs… one item can satisfy several" | `ItemUseFamily` (Runtime) and `LocalUseItemEligibility` / `CarriedItemUseTree` ask `ItemUseFamilyClassifier` once per entry | `agrees` | none |
| 9 | `items/food.md` | "the family is decided from the game's own data" | `src/CasualtiesUnknownOnline.GameAdapter/Content/GameSolidFoodFacts.cs` classifies by reading the item's own action through `UseActionInstructions` and answering a `SolidFoodVerdict` | `agrees` | none |
| 10 | `items/container.md` | "a container's contents are not a body slot" | `StandingItemMaterializer` and `CarriedItemUseTree` resolve a CARRIED item by slot; a contained child is reached through the container, not a slot | `agrees` | none |
| 11 | `mechanics/cpr-and-revival.md` | "the operator's own body is asked FIRST… each press costs stamina" | `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/CprButtonPolicy.cs` decides `Hidden`/`Start`/`Compress` and floors the interval at `0.01` s; `CprButtonCoordinator` calls it before sending | `agrees`; a code-only detail added | the page now states the three states and the arrest condition (`Body.inCardiacArrest`, `heartRate < 20`), plus the never-zero interval |
| 12 | `surfaces/status-line.md` | "a refusal is accompanied by the game's own deny sound" | `GameAdapterSessionBinding` wires `UseRefused` to `OnPlayerItemUseRefused`, whose comment states "the game's own deny sound beside it… NO extra success sound, because the drag already has the game's own cloth rustle" | `agrees`; a code-only detail added | the page now quotes the pairing and names the wiring |
| 13 | `mechanics/give-and-take.md` | "the two destinations are mutually exclusive" | `RemotePlacementService.HandleRemoteGiveToBody` and `PlayerInventoryTakeService.HandleTakeCore` take a slot OR a container, never both | `agrees` | none |
| 14 | `surfaces/drag-cue.md` | "the label names the player the release would pick — the one whose range the cursor entered last" | `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/DragTargetSelection.cs` is the single resolution both the cue and the release ask | `agrees` | none |
| 15 | `cross-cutting/permissions-and-commands.md` | "`@a` is every player including the executor" | `CommandSelectorResolver.SelectBase` answers `"a" => targets` only after the fix; today it answers the REMOTE set, which is `todo/selectors-exclude-the-executor.md` | `code-drift` (already ticketed) | no new ticket: the row points at the existing one, and the page states the target design rather than the current code, which is what the ticket exists to correct |
| … | the remaining pages (mechanics 10, items 5, surfaces 8, cross-cutting 3) | | | | |
