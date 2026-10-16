# Item and entity data commands: give, spawn and a property editor

- Status: Todo — the read half landed 2026-10-14 as `review/item-data-read-commands.md`, and stage B's first
  verb (`/data set`, with the local-scene port) the same day as `done/item-data-write-commands.md`; the
  stages below are what remains.
- Priority: Medium-High
- Category: Command surface / item and entity state
- Source: the user's 2026-10-07 backlog request — a Minecraft-style command family for CUO: item-specific
  properties (battery charge, liquid and millilitres, condition/durability and the rest) must be gettable,
  settable, mergeable and removable, and the family also covers handing an item out and creating an entity.
- Related: `review/item-data-read-commands.md` (the landed read half, and the survey these stages build on),
  `done/item-data-write-commands.md` (the landed first write verb and the local-scene port),
  `todo/mod-content-ceiling.md` (the property surface this family writes through: item qualities,
  `useLimbAction` and the wearable set),
  `review/in-game-command-console-interactive.md` (the console the verbs land in),
  `todo/give-item-to-another-player.md` (the gesture half of `give`; its finding — no path carries an item
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

Two gaps the read half found and deliberately did NOT close, both of which stage B meets first:

- **The host's own untouched carried items are not in the record — and have no instance id at all.** A member's
  client registers its carried inventory (starting supplies and worn items included) at the generation-finished
  edge; the host's supplies are its local authority, whose ids are minted lazily on first domain entry —
  `CarriedInventoryReporter` states exactly that as its reason for being guest-side. Every verb in this family
  addresses an instance id, so the gap has to be settled before a write or the cursor target can reach an item
  the player is actually holding at world entry: either the host registers its own carried inventory the way a
  member does, or the family gains a read that can name an item before it has an id.
- **No verb can address the cursor.** The console reads the kernel, which has no cursor, so "the item nearest
  the mouse" needs a new GameAdapter capability port. It belongs with stage B because the write half needs
  local-scene access anyway.

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
   what they still owe). **Still open in this stage:** the cursor target (which owns the id-less
   generation-time ground item question); and the first gap above — the host's own untouched carried items
   still have no id and no row, so no verb can name them until the host registers its carried inventory at the
   generation-finished edge the way a member's client does (a cross-player change: those ids ride the 1 Hz
   character snapshot, so the members' clones, their remote inventory view and the divergence monitor all begin
   to see addressable rows for the host's items — its own verification run, not a ride on a verb).
2. **Stage C — `give`**: put an item into another player's inventory with its properties already set. The
   missing half is materialization on the receiving side — today an item reaches another player only by the
   receiver taking it or by dropping it for a pickup — so this stage owns that carrier, and it shares the
   finding with `todo/give-item-to-another-player.md`.
3. **Stage D — `spawn`**: create an entity in the world through the accepted-creation path, with the
   late-joiner question answered before the verb lands.
4. **The permission story, inside every stage.** Reads are `Anyone` today (the read half's own reasoning is in
   its ticket). Writes are the host's call, and the family expresses that as a host rule following the
   existing `HostRulesOptions` flags — enforced on the HOST side, because a member's write reaches the kernel
   as a wire command and the member's own console cannot be the gate. **Landed for `/data set` 2026-10-14
   (S1b of `todo/validation-and-source-checks.md`):** `HostRules.AllowMemberItemDataWrite`, off by default,
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

**What is left after the two verbs**, unchanged from the stage text above: the cursor target and the host's own
id-less carried items (both named as their own work), Stage C's `give` (which faces the same missing carrier the
give gesture ticket found) and Stage D's `spawn` (which owes the late-joiner answer first).

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

- **Neither new verb has a console-level case.** The parser and the value layer are pinned, but no test runs
  `/data merge …` or `/data remove …` through the console, so the shared guards, the single commit and the
  multi-property answer line are unobserved. The console suite is at 36 of its 40-case cap, so two cases fit.
- **The atomicity claim has no test at all.** It is stated in a comment and verified by reading only; a test
  that applies one property successfully, fails the next, and then asserts the scene and the record are
  untouched would close it. It has to be a console-level case: at the value layer the capture is mutated in
  memory by design, so that layer cannot observe whether anything was committed.
- **`set`'s answer changed for malformed input.** Parsing now happens before the session and announced-rule
  checks, so a malformed `/data set` with no session (or from a member under a closed rule) is answered with
  the usage line instead of the session/rule sentence. The new order is arguably better — a static error is
  more specific — but it is a real change to a shipped verb's answer and no test observes it.
- **`merge`'s property-level refusals quote the SET usage line**, because the property parse is shared and its
  messages interpolate the set grammar. Its own "no property at all" refusal quotes the merge grammar, so the
  two are inconsistent; the shared vocabulary is right, the printed grammar line is not.
- Unfixed and named: the duplicated `<summary>` left on the write method, and two nits about `WroteMany`
  taking two index-aligned lists and assuming a non-empty one.