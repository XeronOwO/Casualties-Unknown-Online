# A mod item that declares no tags throws inside the game's own frames

- Status: Todo — **found 2026-10-11 by acceptance batch `20261011-k`** while driving the mod-item rows of
  `docs/backlog/done/mod-item-authored-actions.md`; recorded with this run's evidence and left for its own
  cycle, because an acceptance batch validates and does not change code
  (`docs/evidence/acceptance/mod-item-authored-actions-20261011-k.md`). Attribution checked against this
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
The factory sets its own `fullName` / `description` from the definition, and it populates the tags array
only when the definition's `Tags` is not blank:

```text
info.tags = definition.Tags ?? string.Empty;
...
if (!string.IsNullOrWhiteSpace(info.tags))
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
source`, and `HasTag` is asked all over the game's own item paths. The run's own two stacks name
`Item.Start` (`cangetwet`) and `Body.HandleVisuals` (on the host: `PlayerCamera.HandleGunMenu` (`gun`) too),
and the paths this ticket's family census read are the drag pair (`tool`, `battery`) and the placeable check.
The list is NOT exhaustive — the game asks `HasTag` from other places as well (pickup tags, heaters,
traders) — and nothing here needs it to be: the fix is that the array exists at all.

## What the run measured

Both sessions of batch `20261011-k` reproduced it, on both peers, without any forced state:

- **On creation, once per item**: `[Unity:Exception] ArgumentNullException … ItemInfo.HasTag … Item.DMD<Item::Start>`
  on the creating client (host, 22:27:20.548 and 22:33:20.608) and on the peer that received the replicated
  item (guest, 22:27:20.952 and 22:33:21.154). The item is still created and picked up — `item-use` found it
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

- Populate the derived array unconditionally in `ModItemInfoFactory` (the factory's own `ApplyTags` already
  splits `tags ?? string.Empty`, so the fix is the guard, not the writer) and state on `Tags` what an empty
  declaration means.
- **Audit the family**: every other derived member the game's own table pass writes (`rotSpeed` from
  `decayMinutes` is the second one that pass sets), and every sibling mapping in the content providers that
  leaves a game field at its default because a declaration was blank. The sweep in this batch found no
  second instance of the exact shape — `info.tags` itself is always assigned, and the other blank-guards
  cover names, paths and categories — but the rule, not the sweep, is what has to hold.
- Case it: a definition with `Tags` left empty builds an `ItemInfo` whose `HasTag` answers instead of
  throwing, over the real build site; and a case that the game's own `HasTag` call sites are reached with a
  non-null array for every item the factory builds.
- Decide and case the amplifier half (`CallContext` scope disposal under a throwing original), or record
  why it is deferred.

## Acceptance rows

| # | Row | Class |
|---|---|---|
| 1 | A mod item whose definition declares no tags is created, dropped, replicated to the peer and held in the hand slot with no `ArgumentNullException` in either client's log over the run | `machine` |
| 2 | The same item's `Item.GlobalItems` entry answers `HasTag` for a tag it does not carry (false) and for one it does (true) — the derived array is present and correct | `machine` |
| 3 | The amplifier half's own row, once decided | `machine` |

## Non-goals

- Not the item-action surface (`docs/backlog/done/mod-item-authored-actions.md`), which is accepted and
  whose fixture merely carried the missing tag declaration.
- Not the wearable, recognition-gate or misc-read entries of `mod-content-ceiling.md` Part 3.A, each of
  which keeps its own consumer rule.
- Not a change to what `Tags` MEANS for a mod item: this ticket makes the existing field legal when empty.
