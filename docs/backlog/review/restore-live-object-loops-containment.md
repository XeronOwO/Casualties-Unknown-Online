# The restore's remaining row loops: the live-object tables, the recipe table and the item reconcile

- Status: Review — landed 2026-09-19 (the restore loops decision 183's cycle left; decision 196).
  Awaiting the final unified acceptance pass.
- Priority: Low
- Category: Sync / restore accounting
- Source: split out of `review/restored-entity-row-containment.md` (2026-09-17), whose independent
  adversarial pass found the family wider than the three appliers that ticket named
- Related: `docs/decisions/active.md` 175, 183, 196,
  `docs/backlog/review/restored-entity-row-containment.md`,
  `src/CasualtiesUnknownOnline.Runtime/Session/World/ContainedRowLoop.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/World/KeypadCodeTable.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/World/GeyserStateTable.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/World/RecipeUnlockTable.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/WorldGen/GeneratedItemReconcile.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/World/GameRestoredWorldFactSink.cs`

## The gap (as it was split out)

`ContainedRowLoop` (decision 183) contained the three world-entity appliers AND the two world-fact
tables that iterate the RESTORED rows into the world's block/damage API (`WorldBlockStateTable.Apply`,
`GameBlockDamageTable.Apply`). Three loops of the same restore were left, and the reason each one was
left is recorded rather than folded into that change:

| loop | its shape | why it was not converted then |
|---|---|---|
| `KeypadCodeTable.Apply` / `ApplyAbsolute` | `foreach (var openable in Object.FindObjectsOfType<Openable>())`, counting MATCHED rows | it iterates the LIVE world, not the restored rows: a row has no position in the loop, so "one row costs itself" would have to key on the live object, and the count it would refine is "matched rows", not "written rows" |
| `GeyserStateTable.Apply` | the same shape over `Object.FindObjectsOfType<GeyserScript>()` | same |
| `RecipeUnlockTable.Apply` | `foreach (var row in rows)` writing `hasMadeBefore` / `INT` at an index the loop just validated | the least plausible throw in the family (pure field writes on a validated, non-null recipe), and the record it fed (`RecipeUnlockApplyResult.RefusedIndexes`) names INDICES — a throwing row would have been reported as "this table has no recipe at this index", the wrong reason, and would have had to grow the report's vocabulary |

The blast radius of all three was decision 175's: the throw escaped into the seam that called the
applier, so the rest of that applier's rows and every applier after it were lost, and the account
could say no more than "the live-world write threw".

## What landed

Every loop of the restore's live-world write path is now contained, and each keeps the count it can
honestly report:

- **The two live-object tables** (`KeypadCodeTable.Apply`, reached by both `ApplyAbsolute` — the
  restore — and `ApplyWhereUnset` — the guest's apply of the host's broadcast; `GeyserStateTable.Apply`)
  run through a third shape of the Runtime rule, `ContainedRowLoop.RunLiveWorld`: every live object is
  attempted on its own, a throwing one is named at ERROR level with its position, and the first five
  failures plus one summary line are bounded exactly like the row shapes. A matched row is counted
  only once its write path COMPLETED, so a throwing object is simply absent from `applied` and the
  caller's `rows - applied` names it refused. "Matched rows" stays the count these tables report, and
  a matched row whose value already equals the restored one still counts (the rule the pre-change code
  documented: matched is "the world has a home for this row", not "the value changed").
- **`RecipeUnlockTable.Apply`** iterates the cut's own rows and runs them through
  `ContainedRowLoop.RunContained`, keeping its written / missing-index accounting. A row that THREW
  is refused as its OWN class (`RecipeUnlockApplyResult.RefusedByThrow`) rather than as a missing
  index: its index EXISTS in the live table and the write threw, so calling it "this table has no
  recipe at this index" would name the wrong reason. The row itself is named by the containment's
  error line, whose identity is its index — the only key a recipe row has.
- **The restore's ITEM half** (`GeneratedItemReconcile.Apply`, the reconcile
  `GeneratedItemAuthority.ReconcileRestoredItems` runs against the cut's world items) had the same
  shape and is contained too: its entry loop runs through `RunContained`, with `bound` /
  `materialized` taken only after the entry's write path completed, so a throwing entry is counted
  nowhere and the loss is named in the refusal the item half reports; and its leftover-destruction
  loop — which iterates the LIVE world — is contained per object, so a leftover the cut never
  described that SURVIVES is reported instead of diverging silently. This site was found by the
  cycle's independent adversarial pass; the ticket's own first word is "remaining", so it belongs to
  this cycle rather than to a follow-up.
- **The identity used by the error line is now read defensively** (`ContainedRowLoop.Describe`). The
  row shapes name rows from message data, but a live object's identity is its position — read off the
  very object whose engine call just threw. An identity that cannot be produced is reported as
  `<identity unavailable>` instead of throwing out of the catch block, which would have discarded the
  original exception and escaped the containment (the exact outcome the rule exists to prevent).
- **The counts stay exact**: `RunLiveWorld` returns NOTHING on purpose — the callers that report
  refusals compute them as `rows - applied`, so a throwing object is already inside that count, and a
  returned count added there would count it twice (the two callers that report no refusal, the live
  broadcasts, are covered by the same rule). The recipe table's refused total is `missing + thrown`,
  and the item reconcile's `Applied` is `bound + materialized`, which excludes a throwing entry.
- **No wire member changed** (protocol stays 34) — a fact about the mechanism, never a design input
  (decisions 137/188).

## Acceptance

| # | Scenario | Evidence |
|---|---|---|
| 1 | A keypad or geyser write throws mid-loop: the rows after it still land, and the refused count names the one that did not | `ContainedRowLoopTests.RunLiveWorld_AThrowingObjectCostsOnlyItselfAndIsNamed` (the rule: every object attempted once, the throwing one absent from the count, its position in the error line) + `RestoredWorldFactReplayTests.ApplyIfPending_AThrowingNativeRowCostsOnlyItself` (the seam reports exactly `1 keypad code(s)` and `1 geyser type(s)`, names each object by its position, and the rows behind them land) |
| 2 | A recipe row throws: the rows after it still land, and the report names the row and calls it refused | the same seam test (`1 recipe unlock row(s)` with `index 2` in the error line and the two rows behind it applied). The adapter table's own arithmetic is read-only reviewed — `Recipes.recipes` is game-typed |
| 3 | No throw anywhere: every count is identical to today's | `ContainedRowLoopTests.RunLiveWorld_NoThrow_AppliesEveryObjectOnceAndWritesNothing` pins the rule's shape; the three game-typed tables' and the reconcile's arithmetic is compared against the pre-change tree read-only (`git show HEAD:<path>`), because only a game can execute those bodies — the same limit recorded below |
| 4 | A systematic failure stays readable, and the count stays exact | `ContainedRowLoopTests.RunLiveWorld_ASystematicFailureIsBoundedLikeTheRowRule` (five named + one summary naming the remaining four and the total nine) |
| 5 | Naming a refusing unit cannot itself throw | `ContainedRowLoopTests.RunContained_AnIdentityThatThrowsCannotReplaceTheOriginalFailure` (the loop still attempts every row, every failure still produces its error line, the unnamed one reads `<identity unavailable>`) |

## Family sweep

Every loop the restore's live-world write path owns, and its verdict after this cycle:

| site | loop | verdict |
|---|---|---|
| `WorldBlockStateTable.Apply` | restored block cells | contained in the parent cycle (`RunContained`) |
| `GameBlockDamageTable.Apply` | partial-damage rows | contained in the parent cycle (`RunContained`) |
| `EntityEventSync.OnTrapStateProjected` | trap rows | contained in the parent cycle (`Run`) |
| `WorldBuildingEntitySync.OnOpenedEntitiesProjected` / `OnBuildingHealthProjected` | world-entity rows | contained in the parent cycle (`Run`) |
| `KeypadCodeTable.Apply` | LIVE `Openable`s | contained here (`RunLiveWorld`) |
| `GeyserStateTable.Apply` | LIVE `GeyserScript`s | contained here (`RunLiveWorld`) |
| `RecipeUnlockTable.Apply` | restored recipe rows | contained here (`RunContained` + its own refusal class) |
| `GeneratedItemReconcile.Apply` (entry loop) | the cut's world-item rows | contained here (`RunContained`; a throwing entry lands in the item half's refusal) |
| `GeneratedItemReconcile.Apply` (leftover destruction) | LIVE `Item`s no entry claimed | contained here (`RunContained`; a leftover that survived is reported) |
| `GameRestoredWorldFactSink.ApplyRadiationLine` | one value, not a loop | unchanged — a single `RadiationLineTable.Apply` whose failure is the existing boolean refusal |
| `GameRestoredWorldFactSink.RefreshCrackSprites` | the written rows' crack sprites | already guarded before this cycle (one `try` per row at warn level) — presentation only, never part of the account |
| `NativeWorldFacts.Capture*` and the tables' `Capture()` readers | the CUT side | out of scope, and NOT a read-only path: `KeypadCodeTable.Capture` generates a missing code host-side (`EnsureCode` writes it), so a throw there fails the whole cut rather than losing rows. That outcome belongs to the cut's own contract (`null` / `Unreadable` refusals); this ticket neither contains it nor claims it |

The `FindObjectsOfType` scans that a CONVERTED row's own action performs (the trap replay's
nearest-entity scan, the trap effect appliers) run inside that one row and are therefore already
inside the row's containment, not loops of their own.

## Limits

- The converted sites are game-typed (`Object.FindObjectsOfType`, `Traverse`, `Recipes.recipes`,
  `ItemApplication`), so only the part that lives in `ContainedRowLoop` is machine-checked; the
  adapter call sites are read-only reviewed. The seam suite drives a fake sink that performs the
  adapter's arithmetic, so it proves the RULE and the accounting path, not that the production call
  sites are wired to it — and the reconcile's containment has no test at all beyond the rule's own.
- The guarantee covers the rows HANDED to the applier: the enumeration/table read that produces them
  (`Object.FindObjectsOfType`, `Recipes.recipes`, `Item.allItems.ToList()`) is evaluated before the
  containment starts, so a throw there still fails that whole applier and is handled by decision 175's
  half scoping rather than by this rule.
- The live-object loops assume one live home per restored row. The 3 m position tolerance can in
  principle match two live objects to one row, which makes `applied` exceed the row count and its
  `rows - applied` refusal negative — the pre-change arithmetic had the same property, and it is
  recorded here rather than papered over.
- `ApplyWhereUnset` and `GeyserStateTable.Apply` are also the guest's LIVE broadcast path, so those
  handlers now contain (and log) an exception that used to escape into the frame/packet path, and the
  keypad broadcast's "applied" log line stops counting an object whose field threw. Both differences
  appear only in the failure case.
- No unguarded engine call in these loops is demonstrated to throw today: this ticket is about the
  blast radius when one does, so it was written as behaviour tests rather than from a recorded red —
  with no pre-existing defect there is nothing to see fail, and a missing type is not a red.

## Evidence

| claim | how it is proven | where |
|---|---|---|
| one throwing live object costs only itself, and the caller's count names it | the rule's own contract: every object attempted exactly once, the throwing one absent from the count, its position in an ERROR line | `ContainedRowLoopTests` |
| a refusing unit whose identity cannot be read does not escape the containment | the rule's test: every row still attempted, one error line each, the unnameable one reading `<identity unavailable>` | `ContainedRowLoopTests` |
| the seam turns a throwing keypad object, a throwing geyser object and a throwing recipe row into exact refused counts | Runtime-seam test: a sink whose three native loops each throw once reports exactly `1 keypad code(s)`, `1 geyser type(s)` and `1 recipe unlock row(s)` — the whole refused list asserted — and never "the write threw" | `RestoredWorldFactReplayTests.ApplyIfPending_AThrowingNativeRowCostsOnlyItself` |
| the throwing rows are NAMED and the rows behind them land | the same seam test asserts the three error lines' identities and the applied counts | `FakeRestoredWorldFactSink`, the same test |
| no-throw behaviour is unchanged | the matched-count rule is untouched, and the rule's own test asserts every object applied once with a silent log | `ContainedRowLoopTests` |
| the production call sites really use the rule | read-only review of their bodies (game-typed, not bindable by the test host) | `KeypadCodeTable.cs`, `GeyserStateTable.cs`, `RecipeUnlockTable.cs`, `GeneratedItemReconcile.cs`, `GameRestoredWorldFactSink.cs` |
