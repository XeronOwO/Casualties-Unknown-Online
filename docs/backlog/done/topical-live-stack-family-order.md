# A health-usable liquid in a drinkable container was refused by the world drag

- Status: Done — both acceptance rows pass in batch `20261020-b` on the deployed artifact: the inventory entry drinks the dual-class bag (production gate `true`, family `Drink`, 100 ml measured, the host commits the drain and the member's client runs the liquid's `onDrink`), while the limb entry does not take the inventory use (no drink, the bag unchanged). Record: `docs/evidence/acceptance/topical-live-stack-family-order-20261020-b.md`.
- Priority: Low-Medium
- Category: Item use / family classification (the world drag versus the wound view)
- Source: the independent review of the topical-entry correction (2026-10-08), its finding m2's
  second half — the live-stack shape the game's own liquid transfer can build. Filed rather than
  fixed because resolving it is a decision about the FAMILY ORDER, not part of taking the topical
  family off the world drag.
- Related: `docs/decisions/active.md` row 246, `docs/evidence/selfchecks/players/topical-entry-and-named-limb-selfcheck.md` §5, `docs/evidence/selfchecks/players/topical-live-stack-family-order-selfcheck.md`, `docs/backlog/review/mod-cross-player-native-semantics.md`
- Landed: 2026-10-20

## The gap (as filed)

`FamilyOf` asked the topical rule BEFORE the drink rule and read the item's LIVE stacks
(`TopicalAdmission.IsTopicalContainer` needs a `healthUsable` stack). The game's own liquid-transfer
gesture (`LiquidTransfer.Finish` → `Body.CombineLiquids`) lets a player pour a health-usable liquid
(`disinfectant`, `alcohol`, `reliefcream`, `woundglue`, …) into a container whose item DATA is
`usable = true` — a `saline` bag, whose own inventory use is a DRINK (`useAction` → `Drink(body, 100f,
"drink")`, `Item.cs:1867-1870`). That container was classified Topical, so the world drag refused it
and the release fell through to the native drop, although the item's own data says the gesture would
drink from it.

The reading in the filing record (2026-10-18) showed why this is a cross-layer question rather than a
local fix: the two limb rules and the drink rule answer different questions and the container is
legitimately both, so the missing input is **which gesture the player made** — the world drag is the
inventory use (`useAction`), and a drag onto a wound-view limb is the limb use (`useLimbAction`).
Decision 246 states the same rule for the entries.

## What landed (2026-10-20)

The isolation rule decision 246 already states was applied to the family ORDER itself:

- **One entry-aware classifier.** `ItemUseFamilyClassifier` (Runtime) is now the only family order.
  `ItemUseEntry.Inventory` — the item's own `useAction`, whose cross-player form is the world drag and
  the one-shot UI use — asks wear, the injection refusal, drink and solid food, and never a limb rule.
  `ItemUseEntry.Limb` — the wound view's release (`PlayerCamera.ApplyWoundItem`) — asks wear, the
  injection refusal, topical, drink, solid food and the limb tool, in the host chain's dispatch order.
  Within an entry the injection refusal is still first on both entries, so an injectable container
  never reaches a measurement.
- **Every one-shot routing site asks it.** The host's dispatch and auto-select
  (`PlayerItemUseExecution`, `CarriedItemUseTree`), the world-drag gate and its cue
  (`LocalUseItemEligibility.IsUseItem`) and the medical view's dose measurement
  (`MeasureRemoteTopicalDose`/`MeasureRemoteDrinkDose`) read the same classifier, so the operator's
  verdict and the host's branch cannot disagree. `PlayerItemUseService` was split at the architecture
  line gate: its endpoints and the affected side's outcome report stay, and `PlayerItemUseExecution`
  owns the use's family, branch, commit and result.
- **The entry reaches the host as the request's own limb selection.** A request that names a limb is
  the wound view's (that limb IS its native call site, decision 246); `-1` is the inventory use. The
  held-remote-item path (`ApplyToLimb` intent) passes `Limb` explicitly, because its own native
  convention lets an injection request omit the limb. No wire message, field or protocol number
  changed (decision 241's freeze stands).

So a `saline` bag holding a health-usable liquid, dragged onto a teammate in the world, now drinks
from the bag through the drink family's own path: the operator measures the 100 ml the bag's own
`useAction` passes to `WaterContainerItem.Drink`, the host commits the drain, and the affected
client runs the liquid's own `onDrink`. The same container released on a limb keeps the wound-view
entry, and that entry asks the topical rule first — but the topical half is not reachable for this
vanilla container (see `Limits`).

## What a fix had to decide (resolved)

- The world drag classifies by the item's own DATA (`usable` → `useAction` → the drink family) rather
  than by the live stacks' topical admissibility — the isolation rule's own answer.
- The wound view keeps its own entry: the same container is two actions and neither arbitrates the
  other.
- `FamilyOf` takes the entry as an input, and the order lives in ONE place shared by both ends.

## Acceptance

A two-client run on the deployed artifact:

| # | Row | Class |
|---|---|---|
| 1 | A `saline` bag holding a health-usable liquid, dragged onto a teammate in the world, drinks through the drink family's own path: the host commits the drained dose, the affected client runs the liquid's own `onDrink`, the bag's liquid and condition drop by the measured amount, and the target's body shows that liquid's own effect. | machine |
| 2 | The SAME bag, released on a limb through the remote wound view, does not take the inventory use: no drink dose is committed and the bag is not drunk. The topical half of this container is not reachable (its own limb action is the syringe path, `Item.cs:1859-1866`) and is filed as `todo/topical-limb-action-shape.md`; this row judges only that the two entries stayed apart. | machine |

## Limits

- **The wound-view topical half of this container is not reachable, and the ticket's original
  acceptance sentence was wrong.** `PlayerCamera.ApplyWoundItem` (`PlayerCamera.cs:739-763`) runs the
  item's own `useLimbAction` when `usableOnLimb` is set and reaches `ApplyToLimb` only on the
  `usableOnLimb == false` branch. Every vanilla container that is both `usable` and `usableOnLimb`
  (`bloodbag`, `saline`, `ringersolution`, `bloodbaghuman`) has an INJECTION limb action
  (`Item.cs:1756-1761`, `1859-1866`, `1891-1898`, `1922-1927`); none calls `ApplyToLimb`. CUO's
  `TopicalAdmission` requires `usableOnLimb`, so the dual-class container is admitted topical, the
  operator's measurement (`RemoteTopicalUseHandler.TryMeasure`) captures no `ApplyToLimb` and refuses,
  and the release is swallowed. The shape — and the mirror gap, a `usableOnLimb == false` container
  whose native `ApplyToLimb` branch CUO excludes — is filed as
  `todo/topical-limb-action-shape.md` with the native evidence. The host's dispatch for a limb request
  that really carries a measured topical dose is unit-pinned
  (`ItemUseTests.Use_TheDualClassContainerOnTheLimbEntry_AppliesTheTopicalHalfNotTheDrink`); it is the
  host contract, not a production-reachability claim.
- **The drink measurement can run a destructive use action before it can refuse.**
  `RemoteDrinkUseHandler.TryMeasure` runs the item's own `useAction` and only then checks whether the
  delegate reached `WaterContainerItem.Drink`. A blood bag (`usable = true`) refilled with a
  non-injectable liquid — water, or now a health-usable one — is admitted as a Drink, and
  `Item.DrawBlood` runs on the operator's client before the refusal. The pre-change topical-first
  order shielded the health-usable refill only; the water refill was already exposed. Filed as
  `done/drink-measurement-destructive-use-action.md` — **closed 2026-10-24**: the measurement now
  classifies the DEFINITION's compiled body before running anything and refuses a non-drink action
  unrun. The residual (a definition classified Drinks whose run skips the diverted call) is named in
  that ticket, so this record's warning still holds for it alone.
- The world drag's target resolution (the fixed 1.5-unit circle) belongs to
  `todo/cross-player-drag-use-model-bounds.md`; the acceptance drives the production release with the
  peer under the pointer.
- Auto-select (the Online UI's "let the host choose" shape) now skips topical, limb-tool and injection
  items on the inventory entry, which is the isolation rule's consequence; an injectable-only request
  is now refused by the named injection line instead of the generic "not a family" one. Neither has a
  production caller in this tree today.
