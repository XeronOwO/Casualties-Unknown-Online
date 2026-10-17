# Item and entity data commands: give, spawn and a property editor

- Status: Todo — the read half landed 2026-10-14 as `review/item-data-read-commands.md`, and stage B's first
  verb (`/data set`, with the local-scene port) the same day as `done/item-data-write-commands.md`; `merge` and
  `remove` landed 2026-10-17, and stage B's two remainders — the cursor target and the host's own carried
  registration — 2026-10-18, so **stage B is complete**. Stage C's `give` is surveyed; its permission is the
  combined ruling: it ships for BOTH roles together with `todo/cheat-permission-and-op-commands.md`, every cheat
  verb checked on both sides (the client refuses locally, the host arbitrates). Stage C and Stage D (`spawn`) are
  what remains.
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

The read half left two questions behind, and both are answered now:

- **The host's own carried items are in the record (2026-10-18).** A member's client registers its carried
  inventory (starting supplies and worn items included) at the generation-finished edge; the host's supplies
  used to get an id only when a native act minted one, so no verb could name an item the host was merely
  holding. The host now registers its own carried inventory at the same edges — a local write on the ordinary
  kernel batch (no new frame kind) — so those items have ids and rows from world entry; *The host's own carried
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
   *The cursor target landed* carries its mapping, its evidence and its limits; and the host's own carried
   inventory, which now registers at world entry — the last section carries it. **Stage B is complete.**
2. **Stage C — `give`**: put an item into another player's inventory with its properties already set. The
   missing half is materialization on the receiving side — today an item reaches another player only by the
   receiver taking it or by dropping it for a pickup — so this stage owns that carrier, and it shares the
   finding with `review/give-item-to-another-player.md`. **Designed 2026-10-18** — the carrier turned out to be
   the giver-initiated push the give ticket landed, and the user's combined ruling ships the verb for BOTH roles
   together with `todo/cheat-permission-and-op-commands.md`: see *Stage C's design, surveyed* below for the
   carrier, the open pieces and the two-sided permission check.
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

**What is left after stage B**, unchanged from the stage text above: Stage C's `give` (which faces the same
missing carrier the give gesture ticket found) and Stage D's `spawn` (which owes the late-joiner answer first).
The three remainders stage B carried — the host's own id-less carried items, the cursor target and the two
verbs — all landed by 2026-10-18 (the sections below).

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

## The host's own carried inventory is registered (2026-10-18)

Stage B's last gap is closed: the host's own carried items — the starting supplies and the worn items — no
longer wait for a native act to mint their id. They are registered at world entry with the SAME write a native
act of the host takes, so `/data get`, `/data set` and the members' record can name them from the moment the
world opens.

**Entry mapping.** No native call site, and that is the point: this is CUO's own record bookkeeping, not a
gesture mirroring the game's action — the ticket's read half found the gap and the stage text froze the direction
("the host registers its carried inventory at the generation-finished edge the way a member's client does"). What
it adds that a member's client cannot state is the host's own authority rows; no player-visible gesture changed.

**Mechanism inventory.**

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The capture | `CarriedInventoryReporter` — the same capture a member's client sends (body slots plus worn-limb children, `EnsureId` per item); the host now runs it at the same edge, so the host's supplies are minted in the host's own id space |
| 2 | The arming edges | the generation-finished edge (both sides) and the host-session rising edge (`_hostSessionActive`), which covers a world entered before the session existed (a solo world turned lobby) |
| 3 | The registration write | `ItemOwnCarriedRegistration.Register` → `ItemService.SendItemCarriedSync` → `ItemKernelAuthority.TrySpawnCarried` / `TryUpdateState`: the write a native act of the host takes, not a second route |
| 4 | The members' record | the kernel commit is the batch a member's replay reads (`ItemKernelAuthority.TryExecute` → `BatchCommitted` → `KernelProtocolService.BroadcastCommittedBatch`); the ids the registration's `EnsureId` mints then ride the host's 1 Hz character snapshot to the members' view, and that snapshot could not carry them before — its capture reads `ItemInstanceId` and the host's items had none (`ItemStateCodec.CaptureItem`: `InstanceId = item.GetComponent<ItemInstanceId>()?.Id ?? 0`) |
| 5 | The guest's path | unchanged: the guest's report, its cadence/window and the host's judging of that statement are untouched; the only shared surface is the role branch in `SendCarriedInventory` |
| 6 | One registration, not a cadence | the host's write is local, so it cannot be lost on the wire; a repeat would re-commit every item — the debt is armed at world entry and spent by the first capture that can read the body |
| 7 | No wire change (a fact, not a design input) | no frame, kind or protocol number moved; the row travels on the ordinary kernel batch |

**Self-check table.**

| # | Claim | Evidence |
|---|---|---|
| 1 | A host's own carried item has a row at world entry, owned by the host | `HostRegistration_WritesTheHostsOwnCarriedRowAndTheMembersReplayIt` — the row exists, is `Carried`, and its owner is the host |
| 2 | The member's replay receives that row | the same case, after a tick: the member's `ItemKernelAuthority.FindItem` holds it with the host as owner |
| 3 | The registration is owed only after an arm | `HostRegistration_WithoutAnArm_WritesNothing` |
| 4 | It is spent once, so a later capture cannot rewrite the row | `HostRegistration_IsOwedOnce_SoALaterSendCannotRewriteTheRow` |
| 5 | The guest's registration is untouched | the class's own suite: `dotnet test … --filter "FullyQualifiedName~CarriedInventoryRegistrationTests"` — 16/16, the first thirteen of them the existing guest/arbitration facts |
| 6 | The service stayed under the gate | `ItemService` 578 lines (548 at HEAD), the registration in its own `ItemOwnCarriedRegistration` (85) |
| 7 | Not covered here, and the batch's | the live world-entry run (rows 1-5 below); the ID MINTING itself (`EnsureId`) is the adapter's and has no unit test — only the acceptance run can prove a host's untouched item appears with an id |

**What the acceptance batch owes.**

1. A host and a member in a session's world: the host runs `/data get` and its starting supplies and worn items
   are listed with instance ids — the state that used to be empty for a host who had not used anything yet.
2. The host runs `/data set id <one of them> condition 0.5` WITHOUT having used, moved or dropped that item —
   the write lands and `/data get` reads it back on both peers.
3. The member runs `/data get id <the same id>` and reads the host's row — the member's replay really received
   the registration.
4. The member's own supplies still register the way they did (a regression row, not a new claim).
5. The host takes the next-level transition (the auto-revive path): after the layer is entered, `/data get`
   lists the host's restored carried items with ids — the registration waited for the restore instead of
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

## Stage C's design, surveyed (2026-10-18 — the permission ruling and the carrier)

`/give` creates one item in another player's inventory with its properties already set. Two things are settled and
some are open; the next cycle implements it.

**Who may run it: the host AND its members, gated by the cheat permission** — the user's 2026-10-18 combined
ruling (`todo/cheat-permission-and-op-commands.md`, *The combined ruling*), which SUPERSEDES the earlier "host-only
first" answer recorded here. The verb therefore ships TOGETHER with that ticket's `/op`/`/deop` whitelist and its
two-sided check: the acting client refuses a `/give` locally, by name, when it holds the announced permission and
its own entry is not on it (front end = the game instance), and the host judges every request that does reach it
against its own authoritative list and answers a refusal (back end = the host's arbitration). The same two-sided
shape applies to the rest of the cheat family (`/data`'s write verbs, `/spawn` when it lands, `/heal`).

**The delivery carrier already exists**: the giver-initiated push landed in
`review/give-item-to-another-player.md`. The host writes the destination's character snapshot and publishes ONE
message (`PlayerInventoryTakeService.HandleTakeCore` → `PublishTransfer(new PlayerInventoryTransferMsg {
FromSteamId, ToSteamId, Item, TargetParentItemId })`), and the receiving client's own applier adds it to its local
body (`PlayerInteractionApply.OnPlayerInventoryTransfer`: `ToSteamId == local` → the slot or container branch).
`/give` mints the item and rides that message: the TARGET's client is the one that puts it in its body.

**What is still open, and belongs to the implementation cycle:**

- **Minting the id in the Runtime.** The instance id is allocated adapter-side (`ItemIdAllocator`), and the console
  lives in the Runtime: the verb needs one narrow port member ("allocate an id in this host's own space") rather
  than reaching into the adapter, and the answer must be an id that space really owns.
- **Validating the definition.** The verb names a definition id (`bandage`, `largebattery`); an id the game does not
  know would be refused by the receiver's own creation, a round trip too late. Either the verb asks an existing
  adapter facts port, or the gap is named as a limit of this stage.
- **The destination.** One body (the family's one-item rule: a selector matching several bodies is refused by
  name), a named free slot or the first free slot, and the "no free slot" refusal. The container destination is the
  give ticket's own open gap, so `/give` starts with body slots and records that.
- **Properties**: the same property grammar `/data set` uses (`condition`, `favourited`, `liquid`, `component`),
  applied to the created item BEFORE it is published, so the receiver materializes it with the values already set.
- **The sender's own console** prints the answer (item, id, destination slot), and the host's log carries one line.

**Limits this verb will inherit, to be recorded rather than hidden**: the receiving client's native slot, weight
and tag rules do not currently decide placement (the give ticket's open gap — the host computes the destination);
a definition the game does not know; a target with no free slot; and a member's `/give` is only as trustworthy as
the permission list that admitted it — the host still arbitrates every request, and an IP-direct peer's identity
is the permission ticket's open question, not this verb's.