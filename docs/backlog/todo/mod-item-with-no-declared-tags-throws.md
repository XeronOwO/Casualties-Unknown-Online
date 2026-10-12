# A mod item that declares no tags throws inside the game's own frames

- Status: Todo — **stage 1 (the content defect) is FIXED 2026-10-12** and its cases were observed red first;
  the ticket stays open, and stays in `todo/`, because stage 2 (the amplifier) is still unbuilt and, before it
  can be built, still unmeasured. Found 2026-10-11 by acceptance batch
  `20261011-k` while driving the mod-item rows of `docs/backlog/done/mod-item-authored-actions.md`; recorded
  with this run's evidence and left for its own cycle, because an acceptance batch validates and does not change
  code (`docs/evidence/acceptance/mod-item-authored-actions-20261011-k.md`). Attribution checked against this
  repository's own history: the blank-tags guard was written by `a981a93d` (2026-09-03, the advanced item
  behaviour slice) inside the item content provider, and `8de7f5ed` (2026-10-08, the declared-behaviour
  cycle) moved it verbatim into the new `ModItemInfoFactory`; the item-action cycle did not touch those lines.
- Priority: High — every mod item whose definition leaves `Tags` empty (the DTO's own default) throws when
  it is created, and throws once per frame while it is the item in hand.
- Category: Mod platform / content binding
- Parent: `docs/backlog/todo/mod-content-ceiling.md` (Part 3.A's field gaps, the same build site)
- Related: `docs/backlog/done/mod-item-authored-actions.md` (the cycle whose fixture carried the missing tag
  declaration), `docs/backlog/done/mod-item-declaration-parity.md` (the same factory, the "every field the
  game reads" rule this ticket completes),
  `docs/acceptance/lessons.md` (the run's own lesson),
  `docs/en/reference/mod-api.md` (the `Tags` row this ticket makes honest)
- Source: acceptance batch `20261011-k`'s fixture, the example mod's `example.placebo` — the definition the
  same cycle added to exercise "a declared flag with no function". It declares `Usable` and no `Tags`, and
  the game's own frames threw on it the moment it existed.

## Why this ticket exists

`ModItemInfoFactory` is the only place under `src/` that constructs the game's item type, and the game's own
item table builds a DERIVED field on every entry it ships: `Item.cs`'s table pass ends with

```text
foreach (KeyValuePair<string, ItemInfo> keyValuePair in Item.GlobalItems)
{
    keyValuePair.Value.fullName = Locale.GetItem(keyValuePair.Key);
    keyValuePair.Value.description = Locale.GetItem(keyValuePair.Key + "dsc");
    keyValuePair.Value.SetTags();
    ...
}
```

CUO injects its definitions into `Item.GlobalItems` AFTER that pass, so those writes are CUO's to make.
The factory set its own `fullName` / `description` from the definition, and populated the tags array only
when the definition's `Tags` was not blank — the guard this cycle removed:

```text
info.tags = definition.Tags ?? string.Empty;
...
if (!string.IsNullOrWhiteSpace(info.tags))     // removed 2026-10-12: ApplyTags now runs for every definition
{
    ApplyTags(info);
}
```

`ModItemDefinition.Tags` defaults to `""`, so a definition that declares no tags — the common case, and the
fixture's own — leaves the game's derived field at its CLR default. The game then dereferences it:

```text
private string[] actualTags;                      // ItemInfo.cs — no initializer
public bool HasTag(string tag) => Enumerable.Contains<string>(this.actualTags, tag);   // ItemInfo.cs:82
```

`Enumerable.Contains(null, tag)` throws `ArgumentNullException: Value cannot be null. Parameter name:
source`, and `HasTag` is asked all over the game's own item paths. What the RETAINED artifacts name is
`Item.DMD<Item::Start>` and `PlayerCamera.HandleGunMenu` (`gun`), plus a victim frame
`PlayerCamera.HandleBlockPlaceGhost` in the storm census — a corrected attribution: an earlier draft of this
ticket also listed `Body.HandleVisuals`, which the batch's artifacts never name (its `HasTag("gun")` is true
of the SOURCE, `Body.cs`'s `HandleVisuals`, but the raw log was rotated away and no kept stack shows it), and
the independent review of this cycle's fix caught that. The paths this ticket's family census read are
the drag pair (`tool`, `battery`) and the placeable check. The list is NOT exhaustive — the game asks
`HasTag` from other places as well (pickup tags, heaters, traders) — and nothing here needs it to be:
the fix is that the array exists at all.

## What the run measured
Both sessions of batch `20261011-k` reproduced it, on both peers, without any forced state:

- **On creation, once per item**: `[Unity:Exception] ArgumentNullException … ItemInfo.HasTag … Item.DMD<Item::Start>`
  on the creating client (host, 22:27:20.548 and 22:33:20.608) and on the peer that received the replicated
  item (guest). Three of those four timestamps are in the kept artifacts; the guest's session-1 one
  (22:27:20.952) is NOT — the kept excerpt for that side is the aggregate `guest HasTag frames: 2338`
  (`f2-guest-session1.txt`), so the substance is supported and that one number is unverifiable from the tree
  after the log rotation. The item is still created and picked up — `item-use` found it
  in its slot afterwards — so the game carries on with a broken entry.
- **Every frame while it is the item in hand**: `PlayerCamera.HandleGunMenu` asks `HasTag("gun")` on the hand
  slot's item each frame. In the first session the fixture item landed in the hand slot, and that session's
  host log — re-derived 2026-10-11 from the rotated log into the batch's `f3-host-session1-storm-census.txt`,
  which names the source file — holds 136,445 lines and 15,706,048 bytes, with 2,174 `ItemInfo.HasTag` frames
  and 1,166 `Item.DMD<Item::Update>` frames.
- **And it does not stay local**: the same census counts **19,973**
  `InvalidOperationException: CallContext.Enter depth 16 exceeded — an Enter was never disposed (leaked
  scope)` lines, the first at 22:27:20.779 and the last at 22:28:52.907 — 92 seconds in which every
  scope-entering path in that process threw on its own, so the session was unusable and the client had to be
  restarted. What the evidence shows, and what it does not, is worth stating exactly: the depth guard's own
  message proves a scope was entered and never disposed, and the window is the one in which the game's own
  `HasTag` throw fired every frame; the captured stacks keep two frames each, so **no artifact names the
  frame that entered the scope**. Reading the source after the run: `Body.HandleVisuals` IS wrapped in a
  `CallContext` scope (`Patches/BurpSoundPatches.cs`) and asks `HasTag("gun")` on the per-frame path, which is
  why it is the candidate that fits — while `PlayerCamera.HandleGunMenu` carries no CUO patch at all and
  `Item.Start`'s patch (`Patches/ItemPatches.cs`) is a postfix with no scope, so a first draft of this ticket
  that named those two was wrong and the batch's independent review corrected it. What the census DOES land
  is the storm's visible shape: the sample depth-exceeded stack is a scope-entering prefix throwing on its
  own account (`PantSoundPatches.PantSoundUpdatePatch.Prefix` → `PantSound.Update`), i.e. an unrelated path
  paying for the exhausted depth. In the second session the
  fixture item sat in a non-hand slot, the per-frame half never fired, no scope leaked, and the only
  exception was the single `Item.Start` one.
- The definition WITH tags is unaffected: `example.dressing` (`Tags => "dressing,medicine"`) threw in
  neither session, on either client.

Raw log excerpts are in the batch artifact directory (`f1-host-fixture-and-storm.txt`,
`f3-host-session1-storm-census.txt`, `p1-host-session2.txt`, `p2-guest-session2.txt`, cited by name in the
acceptance record).

## The two halves, and why the second is its own question

1. **`actualTags` is never populated for a tagless definition** — the defect this ticket fixes. The shape is
   the one `mod-item-declaration-parity` established: an injected definition must carry every field the
   game's own table pass would have given it, and a derived collection must exist even when it is empty.
   The `Tags` documentation and the `mod-api.md` DTO row say what a mod gets; today "no tags" is not a
   supported value.
2. **A game-side throw inside a scope-entering patched frame leaks CUO's `CallContext` scope, and sixteen
   leaks brick every later `CallContext.Enter` in the process** — the amplifier, and a product robustness
   question rather than a content one. The depth guard's own message is the evidence that a scope was entered
   and never disposed; the mechanism that FITS (a throwing original body skips the postfix that owns the
   disposal) is a reading of `CallContext`'s design plus the frame that carries a scope on this path
   (`Body.HandleVisuals`), NOT an artifact: no captured stack shows the entering frame, so whoever takes this
   half starts by producing that stack (a probe that throws inside each candidate frame, one at a time). The
   fix direction (disposal that survives the original's exception, or a guard that recovers the depth rather
   than latching) is this ticket's to decide, with its own cases.

## Required work

- DONE (2026-10-12) — Populate the derived array unconditionally in `ModItemInfoFactory`: the blank guard is
  gone and `ApplyTags` runs for every definition, so an empty `Tags` reaches the game's own split (one empty
  element) instead of leaving the array null. `Tags` now states on the contract what an empty declaration
  means (`IModItemDefinition.Tags`) and the DTO row in `docs/en/reference/mod-api.md` carries the same
  sentence, so a mod author reads the legal-empty rule where they declare it.
- DONE (2026-10-12) — **Family audit, read at the source rather than swept**: `src/` constructs the game's item
  type in exactly ONE place (`ModItemInfoFactory`; `grep 'new ItemInfo|new LiquidItemInfo'` over `src/` answers
  only that site — a mod liquid has no item type of its own, and `LiquidItemInfo` is only ever type-tested),
  and the game's own table pass writes exactly four derived members: `fullName`, `description`, `SetTags()`'s
  array, and `rotSpeed` when `decayMinutes > 0` (`Item.cs`'s pass over `Item.GlobalItems`). Three are set by the
  factory's own construction and `rotSpeed` by the `DecayMinutes` branch, which applies the table's own rule, so
  the array was the only unpopulated one. `ItemInfo` declares exactly ONE private field (`actualTags`) and it is
  the only member of its type read through an accessor instead of a field — pinned by
  `TheDerivedTagArray_IsTheOnlyFieldTheTypeKeepsPrivate_AndTheFactoryFillsIt`, which fails if a game update adds
  a second. The sibling object builders (`CustomItemTemplateFactory`, `CustomBuildingTemplateFactory`,
  `CustomTileFactory`) all return null WITH a warning when a blank declaration leaves them uninformed, so none
  of them can hand the game a half-built object (the failure mode this ticket fixes), and `BlockInfo` carries
  public fields only. No second instance of the shape exists under `src/Content`.
- DONE (2026-10-12) — **Cased, red observed first**: `Item_DeclaringNoTags_StillAnswersTheGamesOwnHasTag`
  failed on the unfixed tree with `System.ArgumentNullException` out of `System.Linq.Enumerable.Contains` —
  the same frame the in-game storm showed — and passes now; `Item_DeclaringTags_AnswersTheGamesOwnHasTag` pins
  the declared half, and the census case above pins the derived array's shape through the game's own
  `GetTags`/`HasTag` rather than through the field.
- DONE (2026-10-12) — **Independently reviewed against the frozen tree, and every finding dispositioned in
  the same commit**: the reviewer reproduced the game's own table pass and confirmed the fix restores the
  game's own write (unconditional `SetTags()` per entry) rather than inventing a CUO shape, confirmed the
  split shapes match, and confirmed the fixed line is reached by every definition — including the `Gun` branch
  that appends a tag — before the delegates are installed. Verdict: no blocker; 2 major, 5 minor, 4 nit, all
  in the documentation and evidence layer. Fixed here: a paragraph that broke the DTO table rendering in
  `docs/en/reference/mod-api.md` (moved above the table, with the zh mirror's equivalent sentence added and
  both pair hashes re-recorded); "answers `false` for every tag" narrowed to "every tag a caller asks for"
  (the empty tag IS answered true, and the same change's own case pins that); this ticket's stack attribution
  corrected — the retained artifacts name `Item.DMD<Item::Start>`, `PlayerCamera.HandleGunMenu` and a victim
  `PlayerCamera.HandleBlockPlaceGhost`, and never `Body.HandleVisuals`, whose `HasTag("gun")` is a SOURCE fact
  rather than a kept stack; the guest's session-1 timestamp marked unverifiable from the tree; the checklist's
  own numbers corrected (the line count the first draft measured was two LOW, because a plain `Get-Content` on
  this host decodes UTF-8 as ANSI — the corrected figure is in the delivery checklist, which does not edit
  itself); and the census
  case's comment rewritten to claim only what its assertion reaches (private FIELDS — a private property,
  method or non-private field would still pass). The reviewer's one process finding against this cycle is
  recorded in the delivery checklist: a ticket file created for a separate directive AFTER the freeze turned
  `BacklogIntegrityGateTests` red until it was indexed, and that is a frozen-tree violation, not a detail.
- **Stage 2 — the amplifier, still TODO and still unmeasured**: the `CallContext` scope half. A game-side
  throw inside a scope-entering patched frame leaves the `CallContext` scope open (`BurpSoundPatches`'
  `Body.HandleVisuals` pair is the fitting shape: a `Prefix` that enters, a `Postfix` that disposes, and no
  postfix when the original throws), sixteen leaks latch `Enter` into permanent refusal, and every later
  scope-entering path in that process throws — 19,973 lines and 92 seconds in batch `20261011-k`. The
  mechanism is READ, not observed: no captured stack names the entering frame, so the first step of this stage
  is producing that stack (a probe that throws inside each candidate frame, one at a time) rather than
  choosing a fix for a frame nobody has shown. It is its own cycle because it touches call-identity
  architecture, not content binding, and because a wrong recovery rule there can mis-classify mutations
  process-wide.

## Acceptance rows

| # | Row | Class |
|---|---|---|
| 1 | A mod item whose definition declares no tags is created, dropped, replicated to the peer and held in the hand slot with no `ArgumentNullException` in either client's log over the run | `machine` |
| 2 | The same item's `Item.GlobalItems` entry answers `HasTag` for a tag it does not carry (false) and for one it does (true) — the derived array is present and correct | `machine` |
| 3 | The amplifier half's own row, once decided | `machine` |

## Limits, stated rather than implied

- Rows 1 and 2 are `machine` rows and stay unjudged here: what this cycle proves is the build site's own
  behaviour over the real factory and the real game type (`ItemDeclarationParityTests`, 16 cases green, after
  the tagless case was first OBSERVED FAILING with `Enumerable.Contains`'s `ArgumentNullException`). That the
  same defect no longer throws on a live client — created, replicated, held in the hand slot, on both peers —
  is an acceptance batch's row (`docs/acceptance/`), not a unit test's claim, and the batch that measured the
  defect (`20261011-k`) measured it through the example mod's own fixture.
- The example mod's `example.placebo` KEEPS its empty `Tags` deliberately: it is now the live regression
  fixture for this shape, and its own comment says so. Nothing in this cycle needed to change it, because the
  fix is on the framework's side of the boundary.
- Stage 2 (the amplifier) is neither fixed nor measured. The candidate frame that enters the scope is a
  reading of the source; the run's captured stacks keep two frames each and name no entering frame, so the
  ticket above records a candidate, not a conclusion, and stage 2 starts by producing that stack.
- Nothing here is claimed about a game-side `HasTag` caller this repository has not read: the census in
  *Why this ticket exists* lists the paths that WERE read and says the list is not exhaustive — the fix makes
  the array exist for every caller rather than enumerating them.

## Non-goals

- Not the item-action surface (`docs/backlog/done/mod-item-authored-actions.md`), which is accepted and
  whose fixture merely carried the missing tag declaration.
- Not the wearable, recognition-gate or misc-read entries of `mod-content-ceiling.md` Part 3.A, each of
  which keeps its own consumer rule.
- Not a change to what `Tags` MEANS for a mod item: this ticket makes the existing field legal when empty.
