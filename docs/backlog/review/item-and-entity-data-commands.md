# Item and entity data commands: give, spawn and a property editor

- Status: Review — **stage D (`spawn`) landed 2026-10-18**, so every stage of this ticket is built; its acceptance
  rows (`What the acceptance batch owes`, under *Stage D* below) are what a real run still owes. History: the read
  half landed 2026-10-14 as `review/item-data-read-commands.md`, and stage B's first
  verb (`/data set`, with the local-scene port) the same day as `done/item-data-write-commands.md`; `merge` and
  `remove` landed 2026-10-17, and stage B's two remainders — the cursor target and the host's own carried
  registration — 2026-10-18, so **stage B is complete**; batch `20261018-q` re-read its read/write rows under
  the SNBT renderer on `0.1.0+30dbb252` (record
  `docs/evidence/acceptance/item-and-entity-data-commands-20261018-q.md`). **Stage C (`give`) landed 2026-10-17
  and moved out** to `done/give-item-to-a-player-command.md`, together with
  `review/cheat-permission-and-op-commands.md`'s permission half.
- Priority: Medium-High
- Category: Command surface / item and entity state
- Source: the user's 2026-10-07 backlog request — a Minecraft-style command family for CUO: item-specific
  properties (battery charge, liquid and millilitres, condition/durability and the rest) must be gettable,
  settable, mergeable and removable, and the family also covers handing an item out and creating an entity.
- Related: `review/item-data-read-commands.md` (the landed read half, and the survey these stages build on),
  `done/item-data-write-commands.md` (the landed first write verb and the local-scene port),
  `done/give-item-to-a-player-command.md` (stage C, landed and moved out with its design and acceptance rows),
  `review/cheat-permission-and-op-commands.md` (the cheat permission and the two-sided check every cheat verb
  rides), `done/mod-content-ceiling.md` (the property surface this family writes through: item qualities,
  `useLimbAction` and the wearable set),
  `review/in-game-command-console-interactive.md` (the console the verbs land in),
  `review/give-item-to-another-player.md` (the gesture half of `give`; its finding — no path carries an item
  INTO another player's inventory — is what this family's `give` faced too),
  `done/id-system-namespaced-ids.md` (how a mod-authored item id is addressed),
  `done/runtime-entity-creation-rejection.md`, `done/runtime-entity-spawn-backfill.md` (the admission path a
  spawned entity must go through), `docs/en/reference/mod-api.md`

## What is asked

Four capabilities over the same data model, driven from CUO's console:

| Capability | Meaning |
|---|---|
| give | put an item into a player's inventory with its properties already set |
| spawn | create an entity in the world (the game's own summon: an enemy, a trap, a container, a loose item) |
| data get | read the properties of a targeted item or entity |
| data set / merge / remove | change those properties |

The user's own examples of "properties" are the ones the game already stores per item: a battery's charge, a
container's liquid and its millilitres, and condition/durability. The stated intent is open-ended 鈥?an
operator or a mod author should reach the properties the game itself keeps, not a curated subset.

## The data family's syntax (2026-10-17, decision 264)

The family speaks ONE language: the item's own state as **SNBT**, and Minecraft's four `/data` verbs over a
PATH language. The `set` / `merge` / `remove` token vocabulary (`condition <n>`, `liquid <id> <ml>`,
`component <Type> <field> <value>`) is superseded and must not grow — it could not express nesting, several
liquids, contents or two fields of one component, and it made the Runtime know which properties exist.

```text
/data get  <target> [<path>]                     # whole item or one path, printed as SNBT
/data merge <target> <snbt>                      # merge keys into the item's state
/data modify <target> <path> <set|merge|append|prepend|insert <n>> <snbt>
/data remove <target> <path>
```

`<target>` stays the family's own: `id <n>`, `slot <n>`, `cursor`, or a player selector. `<path>` is dotted
keys with list indices (`Liquids[0].Amount`, `Components.BatteryItem.charge`).

Layer split, the same rule as everywhere else in this family: the Runtime owns the SNBT parse, the path
resolution and the sentence the operator reads; the ADAPTER owns the SCHEMA (which keys this item's state
carries, which component types and fields its capture has, whether it is a liquid container) and the legality
of the values. So no game rule and no definition constant ever enters the Runtime or the wire shape.

The parser, the writer, the path resolver and the console's SNBT-aware completion (keys, component types,
fields, enumerated values, from the adapter's schema) are their own ticket (`done/snbt-state-language.md`);
the read and write halves' rows, and every existing `/data` acceptance row, are re-derived in this syntax
there. `spawn` (stage D) inherits the same language for its own state argument.

`Contents` is not part of the state document: each content is its own item row with its own id and report
route, so a path into a child is refused by name and the child is addressed by its own `id` (the read half
lists the ids).

## What the attribute answered (it came first, as this ticket required)

`review/item-data-read-commands.md` is the read half, and the property-by-property survey lives there. The three
conclusions every stage below depends on:

- **What is addressable at all** is one kernel payload per item 鈥?`ItemData`'s condition, favourited, slot,
  liquid stacks and typed component fields 鈥?captured from the game's own `SavedItem` fields plus the codecs'
  whitelists, and restored the same way. Outside it there is nothing for a verb to address: an item's definition
  data is content rather than per-instance state, and a component field the codec does not carry never leaves
  its process.
- **Where a write goes** is that same row. The host writes its own authority
  (`ItemKernelAuthority.TryUpdateState`); a member's write of its OWN item rides the existing
  `ItemUpdateState` report path, which is the one a native item use already takes. What does not exist is a
  host-side edit of another player's CARRIED item: a carried item's state is its owner's report, and the
  correction machinery resolves world items only. A remote edit therefore needs its own mechanism 鈥?a carrier
  that tells the owner to apply the change 鈥?and that is a decision to take, not a detail to improvise.
- **Racing the host** is answered by the same model: a member's report is owner-predicted and host-validated,
  the kernel keeps one revision per item, and a command write is expected to take the identical path rather
  than a second one beside it.

The read half left two questions behind, and both are answered now:

- **The host's own carried items are in the record (2026-10-18).** A member's client registers its carried
  inventory (starting supplies and worn items included) at the generation-finished edge; the host's supplies
  used to get an id only when a native act minted one, so no verb could name an item the host was merely
  holding. The host now registers its own carried inventory at the same edges 鈥?a local write on the ordinary
  kernel batch (no new frame kind) 鈥?so those items have ids and rows from world entry; *The host's own carried
  inventory is registered* below carries the mechanism and what the acceptance batch owes.
- **The cursor is addressable now (2026-10-18).** The console still reads the kernel, which has no cursor, so
  the pointer is a Game Adapter answer (`ILocalItemAccess.ResolveCursorItem`, the game's own pickup query) and
  the target is `cursor`; see *The cursor target landed* below for the mapping, the id rule and what the
  acceptance batch owes. Neither answer weakens the other: an item the record has no id for is still refused by
  name, whichever way the operator named it.

Still open, and belonging to the stage that meets it: whether a command-written property survives a
reconnect the way a played one does (stage B's acceptance), and whether a command-spawned entity can reuse the
accepted-creation path unchanged or needs its own admission, including what a later joiner sees of it
(stage D's first question).

## The remaining stages

1. **Stage B 鈥?the write verbs** (`data set` / `merge` / `remove`) over the properties the survey lists, and the
   local-scene answer the two gaps above need. The write is the owner's own: apply it to the local item and
   report it exactly as a native use does, so the row a command writes is indistinguishable from a row the game
   wrote. Every write inherits the read half's one-item rule: a resolved selector that names more than one body
   is refused at execution, by name, rather than by narrowing the grammar (user ruling 2026-10-14) 鈥?and for a
   write that rule is what keeps one typo from editing a whole team's items.
   Acceptance: a set value survives the next snapshot, a save/restore and a rejoin on both peers, and a
   refused write is answered by name. The CURSOR target the user asked for lands in the same stage, on the same
   new capability port.
   **Landed 2026-10-14:** `/data set` over condition, favourited, one liquid stack and one component field, on
   the item THIS client holds, plus the port (`ILocalItemAccess`) and the report route that makes the row the
   game's own 鈥?`done/item-data-write-commands.md` carries its rows and its limits.
   **Landed with them 2026-10-17:** `merge` and `remove` (the landing section below carries what they do and
   what they still owe). **Landed 2026-10-18:** the cursor target, on the port the write added 鈥?the section
   *The cursor target landed* carries its mapping, its evidence and its limits; and the host's own carried
   inventory, which now registers at world entry 鈥?the last section carries it. **Stage B is complete.**
2. **Stage C — `give`**: put an item into a player's inventory with its properties already set. **Landed
   2026-10-17 and moved out** to `done/give-item-to-a-player-command.md`, which carries the design that was
   settled here, the mechanism inventory, the self-check table, the acceptance rows and the limits. The carrier
   is the giver-initiated push the give ticket landed, the permission is
   `review/cheat-permission-and-op-commands.md`'s two-sided check, and the creation rides a narrow adapter port.
3. **Stage D — `spawn`**: create an entity in the world through the accepted-creation path, with the
   late-joiner question answered before the verb lands. This is the only stage this ticket still owns.
4. **The permission story, inside every stage.** Reads are `Anyone` today (the read half's own reasoning is in
   its ticket). Writes are the host's call, and the family expresses that as a host rule following the
   existing `HostRulesOptions` flags 鈥?enforced on the HOST side, because a member's write reaches the kernel
   as a wire command and the member's own console cannot be the gate. **Landed for `/data set` 2026-10-14
   (S1b of `done/validation-and-source-checks.md`):** `HostRules.AllowMemberCheatVerbs`, off by default,
   judged at the admission seam on the write's own wire kind (`ItemDataWrite`); a refusal is answered, and the
   member's client puts the value it had already applied back onto the session's record
   (`done/item-data-write-commands.md`). Every later write verb rides the same kind and the same rule 鈥?a
   new verb does not need a new gate, and it must not grow one. What the rule could not do when this was
   written 鈥?reach the member BEFORE the write, so a closed rule was a value that appeared and went back 鈥?is
   closed (`done/host-rules-replication.md`): a member holding the announced rule is refused on its own
   client before anything is applied, and the verbs that follow `/data set` inherit it. **`/give` rides the
   same rulings on its own request frame** (it is not a kernel command, so the host judges it in its request
   handler rather than at the admission seam); the permission half is
   `review/cheat-permission-and-op-commands.md`, and the verb is `done/give-item-to-a-player-command.md`.

## Stage B's remaining two verbs 鈥?the design, settled by reading the landed one (2026-10-17)

> **Superseded syntax (decision 264).** The `condition` / `favourited` / `liquid` / `component` vocabulary
> this section's design was written against is replaced by the item's state as SNBT
> (`done/snbt-state-language.md`): the verbs, the target grammar and the permission story below stand; the
> property words and the per-property parse do not.

`ItemDataCommands` already carries the constants `MergeVerb` and `RemoveVerb` and answers both with "not implemented
yet", so the two verbs are additive work on a path that exists rather than a new surface. What the reading settled:

- **`merge` is `set` with more than one property.** The landed `Write` does five things before the value is applied
  鈥?session check, the host's announced rule, target parse, target resolution, the local item 鈥?and `merge` needs
  all five once, not per property. The target is therefore named once and the properties follow:
  `merge [<selector>] slot <n> | id <n> 鈥?condition <n> favourited <yes|no> liquid <liquidId> <ml> component
  <TypeName> <field> <value>` reads the properties until the tokens run out. The parser needs a repeat of its own
  body rather than a new grammar, and its refusal messages stay the ones an operator already reads.
- **`merge` is atomic for free, and that is worth keeping.** `ItemDataWriteValues.TryApply` mutates the in-memory
  local item and `WriteLocalItem` commits it once, so applying several properties before that one commit means a
  property that cannot be applied refuses the whole merge rather than leaving half of it applied. A per-property
  commit would be the wrong shape here and should not be introduced to make the loop simpler.
- **`remove` only means something for a property that HAS a default.** Clearing a battery, emptying a container's
  liquid and resetting a component field are all "remove"; `condition` and `favourited` are bounded values with no
  absent state, so `remove condition` has no meaning to execute. The rule that follows the family's own habit: the
  verbs that can be removed are `liquid` (zero millilitres) and `component <TypeName> <field>` (the field's
  default), and anything else is refused BY NAME with what the verb does accept, rather than being turned into
  "set it to zero" 鈥?which would silently mean something different for a condition than for a battery.
- **The permission story is inherited, not rebuilt.** All three verbs are one wire kind (`ItemDataWrite`) under
  `HostRules.AllowMemberCheatVerbs` plus the host's cheat roster (`/op`), judged at the admission seam, refused on the member's own client when the
  announced rule forbids it. The ticket's own line is that "a new verb does not need a new gate, and it must not
  grow one", so `merge` and `remove` ride the landed rule and add no flag.

**What is left after stage B**, unchanged from the stage text above: Stage D's `spawn` (which owes the
late-joiner answer first). Stage C landed and moved out (`done/give-item-to-a-player-command.md`). The three
remainders stage B carried — the host's own id-less carried items, the cursor target and the two verbs — all
landed by 2026-10-18 (the sections below).

## Non-goals

- Not a scripting language or a command language design: the family is property-level.
- Not a cheating surface by default: the permission story is part of the work, and it is the host's call who
  may run the family.
- Not a raw memory editor: only properties the game's own data model carries are in scope.
## `merge` and `remove` landed (2026-10-17)

Both verbs are real now, built on the design recorded above: `merge` takes ONE target and one or more
property/value pairs, `remove` takes one target and one or more properties with no value (a liquid goes to zero
millilitres, a component field returns to its kind's own zero, and a bounded property is refused by name). The
write path is shared, so all three verbs pass the same session check, the same announced-rule interception and
the same one-item resolution, and a command commits once 鈥?which is what makes a merge atomic rather than
half-applied.

**An adversarial review of the frozen change ran and found no blocker: 2 major, 8 minor, 5 nits.** The two
majors were both about evidence rather than behaviour, and one of them was the kind that flatters a change:
the two single-property answer renderers had been kept "so the verbs that already ship print what they printed
before", but by then nothing in `src/` called either of them 鈥?they were dead code, and the tests asserting
their EXACT text were defending a method no operator could reach. The exact-text cases now point at the live
renderer and the dead pair is deleted. Fixed with it: the success log had lost the property name (a merge logged
three values with no way to attribute them), and a bare `/data` never mentioned the two new verbs existed.

**What the review could not get from this change, and this ticket still owes:**

- **Both of the review's evidence gaps are closed (second commit, same day).** `/data merge` and `/data remove`
  now run through the real console, and one of the three cases is the atomicity proof: a merge whose first
  property is legal and whose second is not leaves the record untouched, which is the case a per-property commit
  would fail. The assertions read the kernel row rather than the local object, because the report happens after
  the commit 鈥?so the row is what proves nothing landed.
- **`set`'s answer changed for malformed input, and it is now pinned.** Parsing happens before the session and
  announced-rule checks, so a malformed `/data set` with no session (or from a member under a closed rule) is
  answered with the usage line instead of the session/rule sentence. The new order is better 鈥?a static error is
  more specific than a state complaint about a line that could never have run 鈥?and the existing parser case
  `AnythingElse_IsRefusedWithTheUsage("slot 0")` already observes it, so a later reorder has to be deliberate
  rather than incidental. Recorded here because it is a shipped verb's printed answer.
- **`merge`'s refusals now quote the merge grammar** (third commit, same day). The property parse is shared, so
  its messages interpolated the set usage line 鈥?telling a merge operator to write a shape merge does not accept,
  while its own "no property at all" refusal quoted the right one. The verb's usage is now passed into the shared
  parse, and the case that asserted the set line asserts the merge line instead (and asserts the set line is
  absent), so the fix is pinned by the very test that caught it.
**A real two-client run judged what it could (2026-10-18)**: the record is
`docs/evidence/acceptance/item-data-write-verbs-20261018.md`. The host side passed every row it reached 鈥?
a merge landing both named properties and reading back, a refused merge leaving the earlier property
unlanded (the atomicity claim, observed), and a bounded property refused by name with the REMOVE grammar.
The member side passed too, once the batch declared its own starting conditions (third run): a member's
write lands while the host allows it and its record follows, and the SAME write is refused on the member's
own client once the host announces the closed rule - the interception no unit test drives. The clear path is judged
too on a waterbottle (a full stack goes to `500 ml -> 0 ml` and the container stays). What the record still
owes is the COMPONENT half of that path, and the run that looked for it came back with a finding rather than a
gap: the tag scan names `largebattery` and the item is granted, but an item straight from the game's grant
carries NO component line at all 鈥?a battery's component is brought into being by `BatteryItem.LoadBattery`,
so judging that row needs a fixture that loads one first. `TryResetField` is unit-covered for all six kinds and
runtime-covered for none. The record also keeps the three runs' failures, which are all one
mistake: a step that reported an absence from a reading it had not established it could make.

- **Since then:** the duplicated `<summary>` on the write method is deleted. Two nits remain unfixed and are
  recorded rather than forgotten: `WroteMany` takes two index-aligned lists, and it assumes a non-empty one.

## The cursor target landed (2026-10-18)

`cursor` is a target shape now: `/data get cursor` reads the item the pointer is over, and `/data modify|merge|remove
cursor 鈥 writes it. The read still answers from the kernel row 鈥?the scene contributes only the ID, which is the
one thing a command cannot get from the record.

**Entry mapping.** The CUO target is the item the game's own pickup QUERY selects:
`Physics2D.OverlapPoint(Camera.main.ScreenToWorldPoint(Input.mousePosition), LayerMask.GetMask(new string[] { "Item" }))`
and then `TryGetComponent<Item>` 鈥?`PlayerCamera.TryPickupFromWorld`, `reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:1423`
and `:1437`. The wider hover query (`:1997`, masks `Item`, `Ground`, `Descriptor`, `Limb`) is deliberately NOT
the mapping: it answers blocks and limbs too, so it is the hover machinery's question, not "which item". The
pickup GESTURE's own gates are not mirrored either 鈥?it runs only while unpaused, conscious and allowed to use an
item, within ten units (`:1355`, `Body.cs:1364`-`:1375`) 鈥?because those gates judge the holding player's action,
not "which item is under the pointer"; a target that reproduced them would refuse to name an item the operator
can plainly see. The gesture carries the pointer and nothing else; where the pointer cannot carry an identity the
request is refused BY NAME 鈥?no fallback supplies a nearby item.

**Mechanism inventory.**

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The pointer 鈫?item query | `PlayerCamera.cs:1423` (the `Item` layer) and `:1437` (`TryGetComponent<Item>`); replicated in `LocalItemAccess.ResolveCursorItem` with the same call and the same mask |
| 2 | Instance identity | `ItemInstanceId` is READ, never minted. The current rule: the host assigns an id to every generation-time item when a generation finishes (`GeneratedItemAuthority`'s class doc; `Publish()`'s "every standalone world item without an id is a generation-time item"), its item snapshot carries them to a guest, and a runtime item gets one as it enters (`ItemIdAllocator.EnsureId`: "dropped from an inventory, unloaded from a container, picked up from the ground"). The `ItemInstanceId` comment that said generation-time items "never carry one" was stale and is corrected in this change |
| 3 | Another member's item is NOT pointer-addressable | its local forms are non-physical by construction: `CloneInventoryRenderer` (`item.rb.simulated = false; // pure display`, `col.enabled = false; // never pickable/blocking`) and `StandingItemRecipe.MakeInert` (`collider.enabled = false`). Both carry an id in their own shape (`RemoteInventoryItemId`, and the authoritative `ItemInstanceId` in the standing recipe), which is why `id`/slot name them and the pointer query never returns either |
| 4 | The port | `ILocalItemAccess.ResolveCursorItem` answers a classification and the ID only when it is `Resolved`; `UnavailableLocalItemAccess` answers `None`, so a build with no adapter answers the verb rather than throwing |
| 5 | The grammar | One grammar serves both halves: `ItemDataQueryParser` adds the `cursor` keyword and `ItemDataWriteParser` admits it unchanged, so a target cannot reach one verb and miss the other |
| 6 | The one-item verdict | Moved out of `ItemDataCommands` into `ItemDataTargets` (that class was at 549 lines; "which item did the operator mean" is its own responsibility) and the cursor joins the same verdict the `id` and `slot` targets take |
| 7 | The report route | Unchanged: the write still applies through `ILocalItemAccess.WriteLocalItem` and reports through the same two calls, so the cursor changes only HOW the ID was found |
| 8 | No wire change (a fact, not a design input) | No kind, field or protocol number moved; the cursor target rides the landed `ItemDataWrite` kind and the inherited host rule |

**Self-check table.**

| # | Claim | Evidence |
|---|---|---|
| 1 | The pointer names the item the game's own pickup query selects | `Get_Cursor_ReadsTheRecordedRowNotTheLocalCapture` stages a world row (the only shape the query can return) and the call and mask are the cited `:1423`/`:1437` |
| 2 | The READ answers the record, not the scene | the same case: the local capture says `condition 0.4` while the row says `0.8`, and the answer prints the row's value |
| 3 | An item with no id yet is refused, not given an invented id | `Get_Cursor_OnAnIdlessItem_IsRefusedByName`; the answer names the current mechanism (the host's publish / item snapshot) and no allocator is called |
| 4 | Nothing under the pointer is answered, and a write writes nothing | `Get_Cursor_OnNothingUnderThePointer_IsRefusedByName`, `Set_Cursor_OnNothingUnderThePointer_IsRefusedWithoutWriting` (the local capture, `Written` and the kernel row all unchanged) |
| 5 | The write takes the same path the `id` target takes | `Set_Cursor_WritesAWorldItemThroughTheCorrectionRoute` asserts the local capture, the kernel row, the world-correction route AND the absence of a carried report; `Merge_Cursor_ChangesEveryPropertyItNames` pins the multi-property tap |
| 6 | An id the pointer names that the record has no row for is refused by name | `Set_Cursor_OnAnIdTheRecordDoesNotName_IsRefusedByName` (nothing written, the capture unchanged) |
| 7 | The grammar is one grammar | `ItemDataQueryParserTests.Cursor_AddressesTheItemUnderThePointer`, `CursorKeyword_IsCaseInsensitive`, the `cursor 5` / `@a cursor` refusals, `ItemDataVerbParserTests.CursorTarget_IsAdmittedByEveryWriteVerb` |
| 8 | Every pointer refusal is a named answer, never a silent no-op | the five refusal cases (read: nothing / no id / not recorded; write: nothing / not recorded), each asserting the sentence it prints |
| 9 | What did not change | the write's wire kind, the host rule (`AllowMemberCheatVerbs`, then named `AllowMemberItemDataWrite`), the report routes and the protocol number; the verdict's move shrank `ItemDataCommands` 549 鈫?460 lines and `ItemDataTargets` is new at 204, both under the 600-line gate |
| 10 | Not covered here | the real pointer under a real mouse 鈥?the acceptance batch's rows below; the driver's ban on pointer input (decision 237) is why it cannot be driven from a test |

**Whole-family audit.** The family is every item-data target (selector, slot, cursor, id). What moved is the
one-item verdict and the body/slot resolution (`ItemDataTargets`, shared by the read's slot path and the write's
verdict); the REPORT routes (`ItemDataCommands.Report`'s world-versus-carried choice), the read refusals
(`RefuseNotHeld`, `RefuseWrite`, `RefuseAmbiguousBody`, `EmptyBodyAnswer`) and the renderer stay where they were,
so the cursor can only reach the same verdict and the same routes 鈥?and the world-row case now proves it takes
the correction route the row's location chooses. The other pointer-driven CUO gesture, the cross-player drag
use, resolves PLAYERS by world-space geometry of its own (`CrossPlayerDragUse`) rather than the item layer, so it
shares nothing with this query and was checked rather than aligned.

**What the acceptance batch owes.**

Batch `20261017-q` judged both landed sections on the deployed `bb2350e7` (record:
`docs/evidence/acceptance/item-and-entity-data-commands-20261017-q.md`): the host-registration rows 1-5 pass
(with the plain-layer-change substitution named in the record), and the cursor rows 1-5 are **unproven** for
one dependency — the machine's pointer was outside the host window (`mouseX 1265, mouseY -658, screen
1280x720`), so the run named that dependency instead of moving the pointer or weakening the check. The
ticket stays in `todo/` for stage D either way.

1. The host drops an item (so it has an id), points at it and runs `/data get cursor` 鈥?the row of the item
   under the pointer, not a neighbour's.
2. `/data modify cursor Condition set 0.5f` on that WORLD item, read back with `/data get id <id>` on BOTH peers 鈥?the world
   correction route, and the record agrees on both sides. A world row is the pointer's own shape: a carried
   item's world object stops simulating when it is picked up.
3. An id the pointer names but the record has no row for (a guest's just-dropped item) 鈥?the named refusal and
   nothing written.
4. The pointer over ground, a body or a building 鈥?"nothing is under the pointer", and nothing is written.
5. The id-less row: a guest in the window between world entry and the host's item snapshot, pointing at a
   generation-time ground item 鈥?the named refusal. This is the row most likely to need a held window; if it
   cannot be held, the batch records it `unproven` rather than weakening the check.

The pointer's world position is readable from the client (`Camera.main.ScreenToWorldPoint(Input.mousePosition)`),
so an item can be staged UNDER wherever the pointer already is instead of asking the run to move it; a pointer
left outside the client window makes the rows unstageable, and then the batch names that dependency rather than
replacing the check.

**Limits, recorded rather than hidden.**

- Two overlapping items answer what the game's own single-collider `OverlapPoint` answers. CUO does not
  disambiguate further: the native query has the same shape, and a second rule here would name a different item
  than the game's own hover shows.
- The pickup gesture's gates are not mirrored (paused, conscious, allowed to use an item, ten units): the pointer
  names what the query selects, which is the operator's question rather than the pickup action's.
- Another member's item cannot be named by the pointer at all 鈥?its local forms are non-physical 鈥?so `id` and
  the holder's slot remain the way to reach it. This is not a refusal the target prints; the pointer simply never
  returns one.
- An item this client has no id for yet stays unaddressable by the pointer until the id arrives (the host's
  generation publish, its item snapshot on a guest, a runtime item's entry). Minting one on a write would be an
  identity nothing reports.
- The pointer is read when the command runs, so the answer belongs to that moment; nothing remembers a previous
  pointer position.
- The read answers the record: the pointer can name an item whose row has not arrived yet, and then the answer
  is the record's ("no item recorded"), not the scene's.
- No tab completion for the target keywords 鈥?the verb still declares one free-text argument, exactly as the
  read half recorded.

## The host's own carried inventory is registered (2026-10-18)

Stage B's last gap is closed: the host's own carried items 鈥?the starting supplies and the worn items 鈥?no
longer wait for a native act to mint their id. They are registered at world entry with the SAME write a native
act of the host takes, so `/data get`, the write verbs and the members' record can name them from the moment the
world opens.

**Entry mapping.** No native call site, and that is the point: this is CUO's own record bookkeeping, not a
gesture mirroring the game's action 鈥?the ticket's read half found the gap and the stage text froze the direction
("the host registers its carried inventory at the generation-finished edge the way a member's client does"). What
it adds that a member's client cannot state is the host's own authority rows; no player-visible gesture changed.

**Mechanism inventory.**

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The capture | `CarriedInventoryReporter` 鈥?the same capture a member's client sends (body slots plus worn-limb children, `EnsureId` per item); the host now runs it at the same edge, so the host's supplies are minted in the host's own id space |
| 2 | The arming edges | the generation-finished edge (both sides) and the host-session rising edge (`_hostSessionActive`), which covers a world entered before the session existed (a solo world turned lobby) |
| 3 | The registration write | `ItemOwnCarriedRegistration.Register` 鈫?`ItemService.SendItemCarriedSync` 鈫?`ItemKernelAuthority.TrySummonCarried` / `TryUpdateState`: the write a native act of the host takes, not a second route |
| 4 | The members' record | the kernel commit is the batch a member's replay reads (`ItemKernelAuthority.TryExecute` 鈫?`BatchCommitted` 鈫?`KernelProtocolService.BroadcastCommittedBatch`); the ids the registration's `EnsureId` mints then ride the host's 1 Hz character snapshot to the members' view, and that snapshot could not carry them before 鈥?its capture reads `ItemInstanceId` and the host's items had none (`ItemStateCodec.CaptureItem`: `InstanceId = item.GetComponent<ItemInstanceId>()?.Id ?? 0`) |
| 5 | The guest's path | unchanged: the guest's report, its cadence/window and the host's judging of that statement are untouched; the only shared surface is the role branch in `SendCarriedInventory` |
| 6 | One registration, not a cadence | the host's write is local, so it cannot be lost on the wire; a repeat would re-commit every item 鈥?the debt is armed at world entry and spent by the first capture that can read the body |
| 7 | No wire change (a fact, not a design input) | no frame, kind or protocol number moved; the row travels on the ordinary kernel batch |

**Self-check table.**

| # | Claim | Evidence |
|---|---|---|
| 1 | A host's own carried item has a row at world entry, owned by the host | `HostRegistration_WritesTheHostsOwnCarriedRowAndTheMembersReplayIt` 鈥?the row exists, is `Carried`, and its owner is the host |
| 2 | The member's replay receives that row | the same case, after a tick: the member's `ItemKernelAuthority.FindItem` holds it with the host as owner |
| 3 | The registration is owed only after an arm | `HostRegistration_WithoutAnArm_WritesNothing` |
| 4 | It is spent once, so a later capture cannot rewrite the row | `HostRegistration_IsOwedOnce_SoALaterSendCannotRewriteTheRow` |
| 5 | The guest's registration is untouched | the class's own suite: `dotnet test 鈥?--filter "FullyQualifiedName~CarriedInventoryRegistrationTests"` 鈥?16/16, the first thirteen of them the existing guest/arbitration facts |
| 6 | The service stayed under the gate | `ItemService` 578 lines (548 at HEAD), the registration in its own `ItemOwnCarriedRegistration` (85) |
| 7 | Not covered here, and the batch's | the live world-entry run (rows 1-5 below); the ID MINTING itself (`EnsureId`) is the adapter's and has no unit test 鈥?only the acceptance run can prove a host's untouched item appears with an id |

**What the acceptance batch owes.**

1. A host and a member in a session's world: the host runs `/data get` and its starting supplies and worn items
   are listed with instance ids 鈥?the state that used to be empty for a host who had not used anything yet.
2. The host runs `/data modify id <one of them> Condition set 0.5f` WITHOUT having used, moved or dropped that item 鈥?
   the write lands and `/data get` reads it back on both peers.
3. The member runs `/data get id <the same id>` and reads the host's row 鈥?the member's replay really received
   the registration.
4. The member's own supplies still register the way they did (a regression row, not a new claim).
5. The host takes the next-level transition (the auto-revive path): after the layer is entered, `/data get`
   lists the host's restored carried items with ids 鈥?the registration waited for the restore instead of
   stating an empty set.

**Limits, recorded rather than hidden.**

- A solo world registers nothing: there is no session, and this family's write verbs need one anyway; the
  session's own start on that world is the arming edge once a lobby exists.
- A layer entered through a respawn holds the registration until the local restore lands: the adapter holds the
  due while a character restore is queued, because the respawn places its fresh body one frame after the
  generation edge and its items two frames later, and without the hold the host would capture that empty body
  and spend the one registration it does not get again. A plain layer change re-runs the generation edge and
  re-registers the same set once (the state is the host's own; no new frame kind is involved).
- The registration states the IDS and STATE of the items the body holds at that moment, at the top level: a
  carried container's child that has never been given an id (the capture does not mint inside `Contents`) stays
  unnamed until its own native act names it. The member's own registration has the same boundary, and the
  starting-supply plan hands out leaf items, so this is not reachable there.
- The host's allocation space is its own: the ids it mints here are what the host's snapshot and the kernel row
  carry, exactly as if a native act had minted them.

## Stage D — `summon`: what a later joiner sees, the entry mapping and the design (2026-10-18)

This ticket's stage D owed one answer before the verb could land: **what does a member that joins after a
command-spawned object see?** It is answered here from the tree, then the verb is designed on that answer.

### What a later joiner sees (answered 2026-10-18)

A spawn creates one of three classes of object, and each class already has an absolute table in the world-entry
group. The verb therefore needs **no admission of its own**: it creates on the HOST through the game's own
creation call, and each family's existing report path fills the table the later joiner reads.

| What the prefab is | The report a host creation triggers | The absolute table a later member receives | When |
|---|---|---|---|
| A world entity (`BuildingEntity`, not an animal) — a beartrap, a crate, a geyser, a turret | `BuildingEntity.Start` → `EntitySpawnSync.OnEntityInstantiated` → `RuntimeEntityChannel.SendEntitySpawned`; the host branch records it (`RuntimeEntityRegistry.Report`) and broadcasts | `RuntimeEntitySnapshot` — the accepted-creation table; the guest replays each entry through the LIVE creation path (`FireRuntimeEntitySnapshotReceived` → `FireEntitySpawnedReceived` → `OnRemoteEntitySpawned`) | world entry (`WorldEntryFanout.Send`'s `SendRuntimeEntitySnapshot`), the in-session repair group, and the 60 s host cycle |
| An animal (`BuildingEntity.animal`) — a spider, a thornback | The same Start report with `IsAnimal = true`; the host records it by KEY only (`RuntimeEntityRegistry.ReportAnimal`), never as a materializable entry | `EnemySnapshot.RuntimeSpawns` — the facts `EnemyStateCapture.Capture` builds with `RuntimeSpawned = _runtimeAnimals.Contains(entity)` and `PrefabId = entity.id`; the guest materializes them in `EnemySyncCoordinator.MaterializeRuntimeSpawns` | world entry and the in-session repair group (`SendEnemySnapshot`) |
| A loose item — a bandage, scrap metal, a waterbottle | `Item.Start` → `ItemWorldSync.OnItemInstantiated` (it allocates the instance id and reports) → `ItemMessageFlowService.SendItemSpawned`; the host's `ItemProjection.ApplySpawn` writes the kernel row and the world table | The item snapshot (`ItemSnapshotService.SendItemSnapshot`) plus the kernel checkpoint | world entry (one-shot, reliable), then the 5 s keyframe |

Three consequences the implementation takes from that table:

- **The verb creates on the HOST.** A member's own creation would ride the accepted-creation path as a REPORT —
  the host may reject it (a prefab the host lacks), the member's copy would die on that refusal, and the answer
  could not say what landed. The host-created object's record exists from the moment its creation reports, which
  is exactly what the later joiner reads. A member's verb is therefore a request (the `/give` shape), not a
  local creation.
- **No new table and no new creation frame.** The verb adds only its request/answer pair; the creation itself
  rides `EntitySpawned` (entity and animal) or the item domain's own `ItemSpawn` (item), so the three tables
  above stay the single answer to "what does a later joiner see".
- **The item half is not the runtime-entity channel.** A loose item is an item-domain row from its first
  report; putting it on the entity channel would create a second materialization path for the same object.

### Entry mapping (the native call site)

CUO's `/summon` is the console counterpart of the game's OWN console `spawn` command — not an invented gesture:

- The call site is `ConsoleScript.RegisterAllCommands`'s `spawn` command: `spawn <string id> [position] [float
  condition] [int amount]`. It creates `Utils.Create(id, position, 0f)` once per amount, throws *Could not find
  entity "<id>"* for an id that resolves to nothing, applies the optional item condition (and a magazine's
  rounds scaled by it), and **picks the item up when it lands within 2 units of the player**.
- Its position argument is `ConsoleScript.ParsePosition`: `cursor` = `Camera.main.ScreenToWorldPoint(
  Input.mousePosition)` (the default when the argument is omitted), `player` = `PlayerCamera.main.body.transform.
  position`, `random` = a uniform point in the layer rectangle (`±halfWidth`, `±halfHeight`), or a literal
  `<x>,<y>` pair.
- The creation call is `Utils.Create` — `Object.Instantiate(Resources.Load(id), pos, rot)`. CUO's
  `UtilsCreateCustomPrefabPatch` extends that same call to mod-registered templates, which is why the creation
  seam attempts the call instead of pre-checking `Resources.Load` (that pre-check is the round-3 finding that
  starved every mod template).
- Item and entity share that one call: the game's own command applies the condition only when the created object
  carries an `Item`, and CUO's two `Start` hooks are what report the object afterwards.

Deviations, each with its reason:

- **The `condition` argument is superseded by the family's state language.** The item state is an SNBT document
  (decision 264), bound by the same adapter schema and legality `/give` uses; `/summon` inherits that language
  rather than growing a second property vocabulary. An ENTITY prefab takes no state argument, because an entity
  has no state document in this tree: its creation-time values (a geyser's liquid type, a keypad's code, a
  crystal's tint) are the game's own, and a document nothing reads would be a mechanism with no consumer.
- **The `amount` argument is not mirrored.** One invocation creates one object, the same rule as `/give`; a
  second copy is a second verb. Recorded under *Limits*.
- **The within-2-units auto-pickup is not mirrored.** `/summon` is the WORLD verb and `/give` is the BODY verb;
  a member's spawn is created by the host at that member's position, so the host's own body would be the one
  picking the item up — the native convenience would become a host-local side effect nobody asked for. Recorded
  under *Limits*.

### Grammar

```text
/summon <prefabId> [<position>] [<snbt-state>]           an entity or an animal
/summon item <itemId> [<position>] [<snbt-state>]       a loose item entity
<position> = cursor | player | random | <x>,<y>          (default: cursor)
```

The user's 2026-10-18 ruling renamed the stage's verb from `spawn` to `summon`: Minecraft's command template is the
reference for a console verb (now a standing rule, `AGENTS.md` Engineering Conventions 17), and the template is
`/summon <entity> [<pos>] [<nbt>]` — the type first, the position second, the SNBT last. The type word `item`
follows that shape for a loose item entity, and the item's SNBT is the SAME document `/give` speaks, which the
user required in the same ruling: the summoned item entity must be equivalent to the item entity a give drops
when the body cannot hold it. Both verbs hand the state text to the same creation port (`IItemGrantSource`) and
the same world-item creation (`WorldItemCreation`), so the equivalence is structural and is pinned by
`ASummonedItemEntity_AndAGivenDrop_CarryTheSameStateDocument`.

The template was checked against the live wiki on 2026-10-18 rather than remembered
([`/summon`](https://minecraft.wiki/w/Commands/summon)): Java Edition's form is `<entity> [<pos>] [<nbt>]`, and its
item entity names the item INSIDE the NBT (`{Item:{id:...,count:...}}`). CUO deviates in one place and records
why: the definition stays the type word's argument (`/summon item bandage {…}`) and the SNBT is the item's OWN
state document, because the user's ruling requires that document to equal the give drop's — nesting it under
Minecraft's `Item:` envelope would make it a different document for the same item. The position and state
arguments are Minecraft's order and shape.

### Design, as landed

- **The seam.** `IWorldSummon` (Runtime → Game Adapter): `TryResolveAnchor` answers the pointer / body / random
  point (the game's own `ScreenToWorldPoint`, `body.transform.position` and layer rectangle), and `TrySummon`
  materializes one prefab at a point and answers the created instance id (items) or zero (entities), or a
  refusal BY NAME. It is a STANDALONE adapter service, like the give seam: the console builds it at startup and
  the adapter aggregate must not enter that construction (the `20261017-q` startup cycle's lesson).
- **Classification is the adapter's, before anything is created.** An id in the game's own item table is an
  item (the `Item.GrantSource` fact path: prefab capture → state bind → instance id); otherwise the prefab is
  created through `Utils.Create` and must carry a `BuildingEntity` with a non-empty `id` (an id-less prefab
  could never be recreated by a peer). A prefab that is neither is refused by name and destroyed.
- **The item path reuses the give ladder's world creation.** The fact is created by `IItemGrantSource` (the
  id space, the state bind), then materialized at the named point and reported by the same code the give's drop
  rung uses — extracted into one `WorldItemCreation` collaborator so the two cannot drift.
- **The permission is the family's two-sided check, no new flag.** The acting client runs `CheatVerbGate`; the
  host judges its own switch and roster for every request (switch then roster, each its own sentence) and
  answers. The host's own verb needs neither.
- **The position travels as the requesting client's own fact.** The member resolves the keyword on its own
  scene (the pointer is its pointer, `player` is its body) and the request carries the resolved point; the host
  creates there. The claim is a creation cause, the same class `ItemSpawn` already is, and the census row says
  so. The request is stamped with the world/layer generation, so a request that outlived a layer boundary is
  refused by name instead of materializing at layer-relative coordinates in the next layer.
- **No new table, no new broadcast, no protocol bump.** The creation rides `EntitySpawned` / `ItemSpawn`; the
  two added `NetMsg` members are 149/150 and no existing member moves (decision 241).

### Landed (2026-10-18)

The verb is real now: `/summon` creates one world object through the game's own creation call, on the host, and
every family's existing report carries it to the session and to a later joiner. The console's cheat family is
complete — `/data`'s write verbs, `/give`, `/summon` — under one two-sided check and one gate.

**Mechanism inventory.**

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The native call site | the game console's own `spawn` command (`ConsoleScript.RegisterAllCommands`) and its `ParsePosition`: `cursor` = `Camera.main.ScreenToWorldPoint(Input.mousePosition)`, `player` = `PlayerCamera.main.body.transform.position`, `random` = a uniform point in the layer rectangle, or `<x>,<y>`; the creation call is `Utils.Create` (`Object.Instantiate(Resources.Load(id), pos, rot)`), extended to mod templates by `UtilsCreateCustomPrefabPatch` |
| 2 | The position words | `WorldSummoner.TryResolveAnchor` reads the same three places; `WorldSummonParser` owns the keywords and the literal pair, and a refusal quotes the vocabulary |
| 3 | The type word | `WorldSummoner.TrySummon`: `item` requires an id in `Item.GlobalItems` (the `IItemGrantSource` fact: prefab capture → state bind → instance id); the entity form refuses an item definition BY NAME with the spelling that works, and creates through `RuntimeEntityFactory.TryCreate`, requiring a `BuildingEntity` with a non-empty id (an id no peer could recreate is refused and destroyed) |
| 4 | The item entity is the give drop's fact | the same `IItemGrantSource` + `WorldItemCreation` the give ladder's drop rung uses — create at the point, stamp the host's id, restore the state, report through `IItemControl.SendItemSpawned` — so the two verbs cannot produce different item entities; pinned by `ASummonedItemEntity_AndAGivenDrop_CarryTheSameStateDocument` |
| 5 | The refusals | `WorldSummonOutcome.Refusal` names every ground: an unknown prefab, `item` with a non-item id, an item id without the word, an entity prefab with a state, a prefab with no entity id, a scene with no world/body/camera, an item state the definition does not carry, a request from another layer, the host's switch and roster |
| 6 | The permission | the family's landed two-sided check, no new flag: `CheatVerbGate` on the acting client before any scene work, `IMemberCommandRules` on the host for every request (switch then roster, each its own sentence) |
| 7 | The wire | `NetMsg.WorldSummonRequest` 149 (guest → host) and `NetMsg.WorldSummonResult` 150 (host → requester), each direction-locked by its handler; the request carries the prefab, the resolved point, the canonical SNBT state and the world/layer stamp |
| 8 | The layer stamp | `KernelWorldGenerationSource` + `WorldReportGenerationGate.Relate`, the same pair the runtime-entity channel uses; a stale request is refused by name and answered |
| 9 | The answer | `WorldSummonService.Describe` — what was created (the item's id included), the point the creation really used and the state the object really carries; one helper feeds the log and the sentence |
| 10 | No new creation path | the created entity/animal reports through `EntitySpawned` (row E3), the item through the item domain's own spawn row (row I1); the verb adds no table, no broadcast and no protocol bump (decision 241) |
| 11 | The shared state text | `ItemStateText` (extracted from `ItemGiveParser`): one parse, one canonical form, one length bound for `/give` and `/summon`, so the two creation verbs cannot disagree about what a state argument is |
| 12 | The seam's shape | `IWorldSummon` is a STANDALONE adapter service, pinned by `AdapterCapabilityPortShapeTests.WorldSummonSeam_IsStandalone`; the give seam's startup-cycle lesson is the reason |

**Self-check table.**

| # | Claim | Evidence |
|---|---|---|
| 1 | A host summon creates an entity at the point the position word names, and the creation records | `CommandConsoleWorldSummonTests.Host_SummonsAnEntity_ReportsTheCreationAndAnswersThePosition`, `.Host_SummonsWithNoPosition_AtThePointer`, `.Host_SummonsAtALiteralPoint` |
| 2 | An item entity is created with an instance id, its world row written and its state bound before the creation | `.Host_SummonsAnItemEntity_WritesTheWorldRowAndAnswersItsInstanceId`, `.Host_SummonsAnItemEntityWithState_AppliesItBeforeTheCreation` |
| 3 | A summoned item entity and a give drop carry the SAME state document for the same text | `.ASummonedItemEntity_AndAGivenDrop_CarryTheSameStateDocument` |
| 4 | An animal is acknowledged by key, never as a materializable creation | `.Host_SummonsAnAnimal_IsAcknowledgedByKeyAndNotRecordedAsAMaterializableCreation` |
| 5 | The type word is enforced, not guessed | `.Host_SummonsAnItemDefinitionWithoutTheItemWord_IsRefusedWithTheSpelling`, `.Host_SummonsAnEntityPrefabWithTheItemWord_IsRefusedByName` |
| 6 | Every other refusal is by name and creates nothing | `.Host_SummonsAPrefabTheBuildDoesNotKnow_IsRefusedByName`, `.Host_SummonsAnEntityWithAState_IsRefusedBeforeAnythingIsCreated`, `.Host_SummonWhenThePositionCannotBeResolved_IsRefusedByName`, `.MalformedSummon_IsRefusedWithItsOwnSentence` |
| 7 | The host's own verb needs neither cheat ruling | `.Host_SummonsWhileTheMemberSwitchIsClosed` |
| 8 | A member's verb is a request whose answer prints on the member's console, and the member's own scene resolved the point | `CommandConsoleMemberWorldSummonTests.Member_Summons_RequestsTheHostAndPrintsItsAnswer`, `.Member_SummonsAnItemEntity_IsCreatedByTheHost`, `.Member_SummonsWithState_IsBoundByTheHost` |
| 9 | Both cheat rulings refuse locally before any scene work, and the host is the back end when the announcement was swallowed | `.Member_Summons_WhenTheAnnouncedSwitchIsClosed_IsRefusedLocally`, `.Member_Summons_WhenTheSwitchIsOpenButTheMemberIsNotGranted_IsRefusedByTheRoster`, `.Member_Summons_WhenNoRosterWasAnnounced_IsSettledByTheHostsOwnRefusal` |
| 10 | A malformed state or an unanswerable position never leaves the member's client | `.Member_SummonsWithMalformedSnbt_IsRefusedLocallyWithoutARequest`, `.Member_SummonWhenItsOwnSceneCannotAnswerThePosition_IsRefusedLocallyWithoutARequest` |
| 11 | A request from another layer is refused by name and creates nothing | `.ARequestFromAnotherLayer_IsRefusedByTheHostAndCreatesNothing` |
| 12 | A forged answer from a non-host sender is dropped by name | `.AnAnswerFromANonHostSender_IsDropped` |
| 13 | The later joiner receives the entity through the accepted-creation table | `WorldSummonLateJoinerTests.AnEntitySummon_ReachesALaterJoinerThroughTheAcceptedCreationTable` |
| 14 | The later joiner receives the item entity through the item snapshot and the kernel row | `.AnItemEntitySummon_ReachesALaterJoinerThroughTheItemSnapshotAndTheKernelRow` |
| 15 | The animal's key reaches the later joiner's snapshot, with no materializable entry | `.AnAnimalSummon_IsAcknowledgedByKeyInTheSnapshotALaterJoinerReceives` |
| 16 | The three frames round-trip their fields, and an unset type word reads as the entity family | `WorldSummonProtocolTests.SummonRequest_RoundTripsTheTypePrefabPointStateAndGeneration`, `.SummonRequest_WithoutATypeWord_ReadsAsAnEntity`, `.SummonResult_RoundTripsTheAppliedIdentityAndAnswer` |
| 17 | The pair is direction-classified and the member-authored one is in the census | `GuestToHostDirectionTests`/`HostToGuestDirectionTests` rows, `docs/architecture/member-report-validation.md`, sync-coverage row I12 with 8 anchors |
| 18 | The grammar is Minecraft's template with the family's recorded deviation | `WorldSummonParserTests` (23 cases): the type word, the position vocabulary, the attached-state refusal and the item-form shape |
| 19 | The seam cannot reach the aggregate and the two verbs share one item materialization | `AdapterCapabilityPortShapeTests.WorldSummonSeam_IsStandalone`; `WorldItemCreation` is the one world-item creation used by the give ladder and the summon verb |
| 20 | Not covered here | the real instantiation, the real prefab tables, the real pointer and body — the Game Adapter's half, which a test host cannot host: code-reviewed, and the acceptance batch's rows below are its runtime proof |

**Whole-family audit.** The console's cheat family now reads: `/data`'s write verbs (gated at the admission
seam), `/give` (its own request handler), `/summon` (this verb, its own request handler), `/data get` (`Anyone`),
the host-admin verbs (`HostOnly`), `/heal` (excluded — it consumes the healer's own item). The three creation
families were checked against the verb: the runtime-entity channel (a non-animal `BuildingEntity`), the enemy
domain (an animal), the item domain (a loose item) — each already had its absolute table in the world-entry
group, so the verb adds none; and the world-item creation path the give ladder's drop rung used is now ONE
collaborator, so the two verbs cannot materialize an item differently.

**What the acceptance batch owes.**

A host + one member session on the deployed artifact:

| # | Scenario | Expected |
|---|---|---|
| 1 | Host `/summon beartrap player` | the answer names the point it used; the beartrap exists on both peers' scenes at that point |
| 2 | Host `/summon medcrate` with the pointer over the world | the answer names the POINTER's world point (the native default) and the crate appears there |
| 3 | Host `/summon bandage player {Condition:0.4f}` | the answer carries the state the item really holds; `/data get id <id>` on BOTH peers reads `0.4f`, and the item lies where the host stood |
| 4 | Host `/summon spider player` | a spider appears on both clients, and the host's enemy snapshot log names it as a runtime spawn |
| 5 | Member (switch open + grant) `/summon bandage player` | the answer prints on the member's console, and both peers see the item where the MEMBER stood — the member's own scene resolved the point |
| 6 | Member, switch closed (announced), the same verb | refused locally, nothing created on either peer |
| 7 | Member, switch open, no grant (announced) | the roster's refusal, nothing created |
| 8 | `/summon notanprefab` and `/summon beartrap player {Condition:0.5f}` | each is refused by name, nothing created on either peer |
| 9 | A later joiner: the member leaves the world and re-enters (or a third client joins) | the beartrap from row 1 materializes again at its creation point, exactly once — the runtime-entity table |
| 10 | The layer-boundary request (a request stamped with the previous layer) | refused by name; if the live window cannot be held, the row records `unproven` rather than weakening the check (the decision is pinned by `ARequestFromAnotherLayer_IsRefusedByTheHostAndCreatesNothing`) |

**Limits, recorded rather than hidden.**

- **The native console's `amount` argument is not mirrored.** One invocation creates one object, the same rule as
  `/give`; a second copy is a second verb. The native count was a loop around the same creation call.
- **The native console's within-2-units auto-pickup is not mirrored.** `/summon` is the WORLD verb and `/give` is
  the BODY verb; a member's spawn is created by the host at that member's position, so the host's own body would
  be the one picking the item up — the native convenience would become a host-local side effect. A player who
  wants an item in hand uses `/give`.
- **An entity prefab takes no state argument.** No entity state document exists in this tree: a geyser's liquid
  type is rolled at its child's `Start`, a keypad's code is host authority, and a crystal's tint comes from its
  trigger. The `/data` verbs address items, and an entity state language would need a reader before it is a
  mechanism; a state on an entity prefab is refused by name instead.
- **The position is the operator's claim.** The member's own client resolves it (its pointer, its body, its
  layer) and the host creates there; it is not bounded by a data anchor, by design — the permission covers the
  verb, exactly as it covers `/give`'s state values. A client that lies about the point can put a spawned object
  anywhere in the layer; it cannot make the host create something the host's own content lacks.
- **A spawn needs a session.** A solo world refuses the verb, the family's cheat standing (`/give`'s rule).
- **No console completion.** The verb declares one free-text argument, like `/give`; the native console's id
  autofill is not ported.
- **A spawned object is a session/layer fact.** The game's own save holds blocks, the body and saved items, not
  runtime-created entities: a host reload regenerates the layer and the object is gone, and every member's own
  generation agrees. A dropped item is the same kind of fact.
- **The item half's row is the host's.** The instance id is the host's allocation and the world row is the
  host's kernel row (the creation report); a member's `/data get id <id>` reads its replay.
- **The answer prints with two console kinds.** The host's own verb returns a success line; a member's answer
  arrives through the answer event and prints with the verdict's kind. The words are identical — the console-wide
  return contract the give answer already has, not this verb's to change.
- **The scene seam has no test host.** The real instantiation, the prefab tables and the pointer/body reads are
  adapter shell: code review plus the acceptance batch. `FakeWorldSummon` replaces exactly that half and makes
  the report the real hooks make.

**Verification.** `dotnet build` exit 0 (0 warnings), `dotnet format` exit 0, the behavioural suite 5798/5798 and
the normative gates 695/695 on the frozen tree (`.acceptance/test-full-3.log`, `.acceptance/gates-4.log`). The 59
new cases are the parser's 23, the host console's 18, the member console's 10, the late-joiner table's 3, the
protocol round trips' 3, the two direction rows and the standalone-seam pin. Two shipped lines changed on the
way and are recorded rather than hidden: the give ladder's drop rung now logs through the shared
`WorldItemCreation` (`[Give] created <id> (id <n>) at (<x>,<y>) and reported its creation` — the same tag, the
same facts, the position named as coordinates instead of "the local body's position"), and `ItemGrantSource`'s
preparation line is tagged `[ItemCreate]` because two verbs now ask it for a fact.

