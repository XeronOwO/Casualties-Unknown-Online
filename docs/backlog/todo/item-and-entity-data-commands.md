# Item and entity data commands: give, spawn and a property editor

- Status: Todo — the read half landed 2026-10-14 as `review/item-data-read-commands.md`, and stage B's first
  verb (`/data set`, with the local-scene port) the same day as `done/item-data-write-commands.md`; `merge` and
  `remove` landed 2026-10-17, and the cursor target 2026-10-18, both in this ticket. The stages below are what
  remains.
- Priority: Medium-High
- Category: Command surface / item and entity state
- Source: the user's 2026-10-07 backlog request — a Minecraft-style command family for CUO: item-specific
  properties (battery charge, liquid and millilitres, condition/durability and the rest) must be gettable,
  settable, mergeable and removable, and the family also covers handing an item out and creating an entity.
- Related: `review/item-data-read-commands.md` (the landed read half, and the survey these stages build on),
  `done/item-data-write-commands.md` (the landed first write verb and the local-scene port),
  `done/mod-content-ceiling.md` (the property surface this family writes through: item qualities,
  `useLimbAction` and the wearable set),
  `review/in-game-command-console-interactive.md` (the console the verbs land in),
  `review/give-item-to-another-player.md` (the gesture half of `give`; its finding — no path carries an item
  INTO another player's inventory today — is what this family's `give` faces too),
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
container's liquid and its millilitres, and condition/durability. The stated intent is open-ended — an
operator or a mod author should reach the properties the game itself keeps, not a curated subset.

## What the attribute answered (it came first, as this ticket required)

`review/item-data-read-commands.md` is the read half, and the property-by-property survey lives there. The three
conclusions every stage below depends on:

- **What is addressable at all** is one kernel payload per item — `ItemData`'s condition, favourited, slot,
  liquid stacks and typed component fields — captured from the game's own `SavedItem` fields plus the codecs'
  whitelists, and restored the same way. Outside it there is nothing for a verb to address: an item's definition
  data is content rather than per-instance state, and a component field the codec does not carry never leaves
  its process.
- **Where a write goes** is that same row. The host writes its own authority
  (`ItemKernelAuthority.TryUpdateState`); a member's write of its OWN item rides the existing
  `ItemUpdateState` report path, which is the one a native item use already takes. What does not exist is a
  host-side edit of another player's CARRIED item: a carried item's state is its owner's report, and the
  correction machinery resolves world items only. A remote edit therefore needs its own mechanism — a carrier
  that tells the owner to apply the change — and that is a decision to take, not a detail to improvise.
- **Racing the host** is answered by the same model: a member's report is owner-predicted and host-validated,
  the kernel keeps one revision per item, and a command write is expected to take the identical path rather
  than a second one beside it.

The read half left two questions behind: the gap this stage still owns, and the cursor, which landed on
2026-10-18 as the same family's target:

- **The host's own untouched carried items are not in the record — and have no instance id at all.** A member's
  client registers its carried inventory (starting supplies and worn items included) at the generation-finished
  edge; the host's supplies are its local authority, whose ids are minted lazily on first domain entry —
  `CarriedInventoryReporter` states exactly that as its reason for being guest-side. Every verb in this family
  addresses an instance id, so the gap has to be settled before a write or the cursor target can reach an item
  the player is actually holding at world entry: either the host registers its own carried inventory the way a
  member does, or the family gains a read that can name an item before it has an id.
- **The cursor is addressable now (2026-10-18).** The console still reads the kernel, which has no cursor, so
  the pointer is a Game Adapter answer (`ILocalItemAccess.ResolveCursorItem`, the game's own pickup query) and
  the target is `cursor`; see *The cursor target landed* below for the mapping, the id rule and what the
  acceptance batch owes. It does not close the gap above: an item the record has no id for is still refused by
  name, whichever way the operator named it.

Still open, and belonging to the stage that meets it: whether a command-written property survives a
reconnect the way a played one does (stage B's acceptance), and whether a command-spawned entity can reuse the
accepted-creation path unchanged or needs its own admission, including what a later joiner sees of it
(stage D's first question).

## The remaining stages

1. **Stage B — the write verbs** (`data set` / `merge` / `remove`) over the properties the survey lists, and the
   local-scene answer the two gaps above need. The write is the owner's own: apply it to the local item and
   report it exactly as a native use does, so the row a command writes is indistinguishable from a row the game
   wrote. Every write inherits the read half's one-item rule: a resolved selector that names more than one body
   is refused at execution, by name, rather than by narrowing the grammar (user ruling 2026-10-14) — and for a
   write that rule is what keeps one typo from editing a whole team's items.
   Acceptance: a set value survives the next snapshot, a save/restore and a rejoin on both peers, and a
   refused write is answered by name. The CURSOR target the user asked for lands in the same stage, on the same
   new capability port.
   **Landed 2026-10-14:** `/data set` over condition, favourited, one liquid stack and one component field, on
   the item THIS client holds, plus the port (`ILocalItemAccess`) and the report route that makes the row the
   game's own — `done/item-data-write-commands.md` carries its rows and its limits.
   **Landed with them 2026-10-17:** `merge` and `remove` (the landing section below carries what they do and
   what they still owe). **Landed 2026-10-18:** the cursor target, on the port the write added — the section
   *The cursor target landed* below carries its mapping, its evidence and its limits. **Still open in this
   stage:** the first gap above — the host's own untouched carried items
   still have no id and no row, so no verb can name them until the host registers its carried inventory at the
   generation-finished edge the way a member's client does (a cross-player change: those ids ride the 1 Hz
   character snapshot, so the members' clones, their remote inventory view and the divergence monitor all begin
   to see addressable rows for the host's items — its own verification run, not a ride on a verb).
2. **Stage C — `give`**: put an item into another player's inventory with its properties already set. The
   missing half is materialization on the receiving side — today an item reaches another player only by the
   receiver taking it or by dropping it for a pickup — so this stage owns that carrier, and it shares the
   finding with `review/give-item-to-another-player.md`.
3. **Stage D — `spawn`**: create an entity in the world through the accepted-creation path, with the
   late-joiner question answered before the verb lands.
4. **The permission story, inside every stage.** Reads are `Anyone` today (the read half's own reasoning is in
   its ticket). Writes are the host's call, and the family expresses that as a host rule following the
   existing `HostRulesOptions` flags — enforced on the HOST side, because a member's write reaches the kernel
   as a wire command and the member's own console cannot be the gate. **Landed for `/data set` 2026-10-14
   (S1b of `done/validation-and-source-checks.md`):** `HostRules.AllowMemberItemDataWrite`, off by default,
   judged at the admission seam on the write's own wire kind (`ItemDataWrite`); a refusal is answered, and the
   member's client puts the value it had already applied back onto the session's record
   (`done/item-data-write-commands.md`). Every later write verb rides the same kind and the same rule — a
   new verb does not need a new gate, and it must not grow one. What the rule could not do when this was
   written — reach the member BEFORE the write, so a closed rule was a value that appeared and went back — is
   closed (`done/host-rules-replication.md`): a member holding the announced rule is refused on its own
   client before anything is applied, and the verbs that follow `/data set` inherit it.

## Stage B's remaining two verbs — the design, settled by reading the landed one (2026-10-17)

`ItemDataCommands` already carries the constants `MergeVerb` and `RemoveVerb` and answers both with "not implemented
yet", so the two verbs are additive work on a path that exists rather than a new surface. What the reading settled:

- **`merge` is `set` with more than one property.** The landed `Write` does five things before the value is applied
  — session check, the host's announced rule, target parse, target resolution, the local item — and `merge` needs
  all five once, not per property. The target is therefore named once and the properties follow:
  `merge [<selector>] slot <n> | id <n> — condition <n> favourited <yes|no> liquid <liquidId> <ml> component
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
  "set it to zero" — which would silently mean something different for a condition than for a battery.
- **The permission story is inherited, not rebuilt.** All three verbs are one wire kind (`ItemDataWrite`) under
  `HostRules.AllowMemberItemDataWrite`, judged at the admission seam, refused on the member's own client when the
  announced rule forbids it. The ticket's own line is that "a new verb does not need a new gate, and it must not
  grow one", so `merge` and `remove` ride the landed rule and add no flag.

**What is left after the two verbs**, unchanged from the stage text above: the host's own id-less carried items
(named as its own work), Stage C's `give` (which faces the same missing carrier the give gesture ticket found)
and Stage D's `spawn` (which owes the late-joiner answer first). The cursor target that used to head this list
landed 2026-10-18 (the last section).

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
the same one-item resolution, and a command commits once — which is what makes a merge atomic rather than
half-applied.

**An adversarial review of the frozen change ran and found no blocker: 2 major, 8 minor, 5 nits.** The two
majors were both about evidence rather than behaviour, and one of them was the kind that flatters a change:
the two single-property answer renderers had been kept "so the verbs that already ship print what they printed
before", but by then nothing in `src/` called either of them — they were dead code, and the tests asserting
their EXACT text were defending a method no operator could reach. The exact-text cases now point at the live
renderer and the dead pair is deleted. Fixed with it: the success log had lost the property name (a merge logged
three values with no way to attribute them), and a bare `/data` never mentioned the two new verbs existed.

**What the review could not get from this change, and this ticket still owes:**

- **Both of the review's evidence gaps are closed (second commit, same day).** `/data merge` and `/data remove`
  now run through the real console, and one of the three cases is the atomicity proof: a merge whose first
  property is legal and whose second is not leaves the record untouched, which is the case a per-property commit
  would fail. The assertions read the kernel row rather than the local object, because the report happens after
  the commit — so the row is what proves nothing landed.
- **`set`'s answer changed for malformed input, and it is now pinned.** Parsing happens before the session and
  announced-rule checks, so a malformed `/data set` with no session (or from a member under a closed rule) is
  answered with the usage line instead of the session/rule sentence. The new order is better — a static error is
  more specific than a state complaint about a line that could never have run — and the existing parser case
  `AnythingElse_IsRefusedWithTheUsage("slot 0")` already observes it, so a later reorder has to be deliberate
  rather than incidental. Recorded here because it is a shipped verb's printed answer.
- **`merge`'s refusals now quote the merge grammar** (third commit, same day). The property parse is shared, so
  its messages interpolated the set usage line — telling a merge operator to write a shape merge does not accept,
  while its own "no property at all" refusal quoted the right one. The verb's usage is now passed into the shared
  parse, and the case that asserted the set line asserts the merge line instead (and asserts the set line is
  absent), so the fix is pinned by the very test that caught it.
**A real two-client run judged what it could (2026-10-18)**: the record is
`docs/evidence/acceptance/item-data-write-verbs-20261018.md`. The host side passed every row it reached —
a merge landing both named properties and reading back, a refused merge leaving the earlier property
unlanded (the atomicity claim, observed), and a bounded property refused by name with the REMOVE grammar.
The member side passed too, once the batch declared its own starting conditions (third run): a member's
write lands while the host allows it and its record follows, and the SAME write is refused on the member's
own client once the host announces the closed rule - the interception no unit test drives. The clear path is judged
too on a waterbottle (a full stack goes to `500 ml -> 0 ml` and the container stays). What the record still
owes is the COMPONENT half of that path, and the run that looked for it came back with a finding rather than a
gap: the tag scan names `largebattery` and the item is granted, but an item straight from the game's grant
carries NO component line at all — a battery's component is brought into being by `BatteryItem.LoadBattery`,
so judging that row needs a fixture that loads one first. `TryResetField` is unit-covered for all six kinds and
runtime-covered for none. The record also keeps the three runs' failures, which are all one
mistake: a step that reported an absence from a reading it had not established it could make.

- **Since then:** the duplicated `<summary>` on the write method is deleted. Two nits remain unfixed and are
  recorded rather than forgotten: `WroteMany` takes two index-aligned lists, and it assumes a non-empty one.

## The cursor target landed (2026-10-18)

`cursor` is a target shape now: `/data get cursor` reads the item the pointer is over, and `/data set|merge|remove
cursor …` writes it. The read still answers from the kernel row — the scene contributes only the ID, which is the
one thing a command cannot get from the record.

**Entry mapping.** The CUO target is the item the game's own pickup QUERY selects:
`Physics2D.OverlapPoint(Camera.main.ScreenToWorldPoint(Input.mousePosition), LayerMask.GetMask(new string[] { "Item" }))`
and then `TryGetComponent<Item>` — `PlayerCamera.TryPickupFromWorld`, `reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:1423`
and `:1437`. The wider hover query (`:1997`, masks `Item`, `Ground`, `Descriptor`, `Limb`) is deliberately NOT
the mapping: it answers blocks and limbs too, so it is the hover machinery's question, not "which item". The
pickup GESTURE's own gates are not mirrored either — it runs only while unpaused, conscious and allowed to use an
item, within ten units (`:1355`, `Body.cs:1364`-`:1375`) — because those gates judge the holding player's action,
not "which item is under the pointer"; a target that reproduced them would refuse to name an item the operator
can plainly see. The gesture carries the pointer and nothing else; where the pointer cannot carry an identity the
request is refused BY NAME — no fallback supplies a nearby item.

**Mechanism inventory.**

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The pointer → item query | `PlayerCamera.cs:1423` (the `Item` layer) and `:1437` (`TryGetComponent<Item>`); replicated in `LocalItemAccess.ResolveCursorItem` with the same call and the same mask |
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
| 7 | The grammar is one grammar | `ItemDataQueryParserTests.Cursor_AddressesTheItemUnderThePointer`, `CursorKeyword_IsCaseInsensitive`, the `cursor 5` / `@a cursor` refusals, `ItemDataWriteParserTests.CursorTarget_IsAdmittedLikeEveryOtherTarget`, `Merge_WithACursorTarget_ParsesEveryProperty` |
| 8 | Every pointer refusal is a named answer, never a silent no-op | the five refusal cases (read: nothing / no id / not recorded; write: nothing / not recorded), each asserting the sentence it prints |
| 9 | What did not change | the write's wire kind, the host rule (`AllowMemberItemDataWrite`), the report routes and the protocol number; the verdict's move shrank `ItemDataCommands` 549 → 460 lines and `ItemDataTargets` is new at 204, both under the 600-line gate |
| 10 | Not covered here | the real pointer under a real mouse — the acceptance batch's rows below; the driver's ban on pointer input (decision 237) is why it cannot be driven from a test |

**Whole-family audit.** The family is every item-data target (selector, slot, cursor, id). What moved is the
one-item verdict and the body/slot resolution (`ItemDataTargets`, shared by the read's slot path and the write's
verdict); the REPORT routes (`ItemDataCommands.Report`'s world-versus-carried choice), the read refusals
(`RefuseNotHeld`, `RefuseWrite`, `RefuseAmbiguousBody`, `EmptyBodyAnswer`) and the renderer stay where they were,
so the cursor can only reach the same verdict and the same routes — and the world-row case now proves it takes
the correction route the row's location chooses. The other pointer-driven CUO gesture, the cross-player drag
use, resolves PLAYERS by world-space geometry of its own (`CrossPlayerDragUse`) rather than the item layer, so it
shares nothing with this query and was checked rather than aligned.

**What the acceptance batch owes.**

1. The host drops an item (so it has an id), points at it and runs `/data get cursor` — the row of the item
   under the pointer, not a neighbour's.
2. `/data set cursor …` on that WORLD item, read back with `/data get id <id>` on BOTH peers — the world
   correction route, and the record agrees on both sides. A world row is the pointer's own shape: a carried
   item's world object stops simulating when it is picked up.
3. An id the pointer names but the record has no row for (a guest's just-dropped item) — the named refusal and
   nothing written.
4. The pointer over ground, a body or a building — "nothing is under the pointer", and nothing is written.
5. The id-less row: a guest in the window between world entry and the host's item snapshot, pointing at a
   generation-time ground item — the named refusal. This is the row most likely to need a held window; if it
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
- Another member's item cannot be named by the pointer at all — its local forms are non-physical — so `id` and
  the holder's slot remain the way to reach it. This is not a refusal the target prints; the pointer simply never
  returns one.
- An item this client has no id for yet stays unaddressable by the pointer until the id arrives (the host's
  generation publish, its item snapshot on a guest, a runtime item's entry). Minting one on a write would be an
  identity nothing reports.
- The pointer is read when the command runs, so the answer belongs to that moment; nothing remembers a previous
  pointer position.
- The read answers the record: the pointer can name an item whose row has not arrived yet, and then the answer
  is the record's ("no item recorded"), not the scene's.
- No tab completion for the target keywords — the verb still declares one free-text argument, exactly as the
  read half recorded.