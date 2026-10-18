# The item data read verb: what CUO records about one item

- Status: Review — code-complete 2026-10-14; batch `20261018-q` judged rows 1-4 and 5a/5c on the deployed
  `0.1.0+30dbb252`, leaving row 5b (two members besides the caller) and row 6 (an item applied to a limb).
- Priority: Medium-High
- Category: Command surface / item state
- Source: Stage A of `done/item-and-entity-data-commands.md` (the user's 2026-10-07 request for a
  Minecraft-style command family), cut out on 2026-10-14. That ticket's own rule is that each stage is its own
  deliverable with its own verification, and its first required job was the attribute: write down what is
  addressable today before designing a verb that writes it.
- Related: `done/item-and-entity-data-commands.md` (the family's remaining stages),
  `done/in-game-command-console-interactive.md` (the console these verbs land in),
  `done/in-game-command-console.md` (the console itself), `done/id-system-namespaced-ids.md` (the instance id a
  target names), `docs/architecture/domains.md` (the item kernel domain), `docs/contracts/item-features-matrix.csv`

## What landed

`/data get` reads the KERNEL row for one item — the host's authority on the host, a guest's replay of the
committed batches elsewhere — and prints every save-shaped property it holds. It is the first verb of the
family the user asked for, and it is deliberately the read half: it answers "what does the session think this
item is" without touching who may write what.

| File | What it is |
|---|---|
| `src/CasualtiesUnknownOnline.Runtime/Session/Commands/ItemDataCommands.cs` | The command group: target resolution, the kernel query, the refusals, the audit lines |
| `src/CasualtiesUnknownOnline.Runtime/Session/Commands/ItemDataQueryParser.cs` | The target grammar, parsed with no session so the vocabulary is pinned by unit tests |
| `src/CasualtiesUnknownOnline.Runtime/Session/Commands/ItemDataQuery.cs` + `ItemDataQueryKind.cs` | One parsed target (bodies, a slot of those bodies, the pointer, or one instance id — the pointer shape landed with the write half's port) |
| `src/CasualtiesUnknownOnline.Runtime/Session/Commands/ItemDataRenderer.cs` | The wording: one line per item when listing, one property per line when reading |
| `src/CasualtiesUnknownOnline.Runtime/Session/Commands/CommandPlayerTargets.cs` | The one selector expansion both selector-consuming command groups now share |
| `src/CasualtiesUnknownOnline.Application/Kernel/IKernelItemFacts.cs` | The read model gained `QueryItems()` — the same rows `FindItem` answers one at a time, for a listing |

## The property surface it reads (the attribute, answered)

An item's persistent state is one kernel payload, `ItemData`: condition, favourited flag, the owner-local slot
index, the liquid stacks and the typed component fields. Its own doc says what it is for — "the save-shaped
item facts that must travel with identity and location". What gets INTO it, and what therefore crosses players,
is decided by one capture path and one restore path:

| Property | Decided by | What a mod or a game update can change under it |
|---|---|---|
| condition, favourited | the game's `SavedItem` fields, read at capture (`ItemStateCodec.CaptureItem`) | nothing — two floats/bools with no vocabulary |
| slot | the owner's own body, read at capture (`ItemStateCodec.SlotOf`) | nothing — the owner's local fact, which the host adopts |
| liquids | `WaterContainerItem.stack`, read and rebuilt whole (`CaptureLiquids` / `RestoreLiquids`) | the liquid ids and amounts; the stack is replaced, not merged |
| component fields | every `[Saveable]` component's simple-typed fields, plus two whitelists: `CustomItemBehaviour` and `GrapplingHook`'s fired/latched/pulling | a new `[Saveable]` field joins by itself; anything else needs a whitelist entry |
| `CustomItemBehaviour.data` | two explicit wire faces (the liquidcentrifuge cooldown, the dynamite fuse) | a new payload in that array needs its own face — it is `object[]`, not a saveable field |
| container contents | separate item rows joined by a `Contained` location, never nested copies | nothing — nesting is the model |

Two properties the family will want are NOT here, and the survey says why: the item's definition data
(`ItemInfo`'s static table) is content, not per-instance state, and anything the codecs do not whitelist (a
Unity-reference component field) never leaves the process that holds it.

**Where a write would go.** The same kernel row is what a write has to change, and the path exists:
`ItemKernelAuthority.TryUpdateState` is the host's own entry (owner-predicted, host-validated), and a guest's
report of its own item rides `ItemActionSync` as one `ItemUpdateState` wire command — the exact path a native
item use already takes. What does NOT exist is a host-side edit of another player's CARRIED item: a carried
item's state is its owner's own report, and the correction machinery resolves world items only
(`ItemApplication` looks the instance up in the world table and ignores what it cannot find). That is why the
write stages cannot promise remote edits without a new mechanism, and it is recorded in the umbrella ticket.

## The target grammar

```text
/data get                                  the items CUO records for the caller's own body
/data get <selector>                        the items of the bodies that selector names
/data get slot <n>                          the caller's own item in body slot n
/data get <selector> slot <n>               that body's item in slot n
/data get id <instanceId>                   one item wherever it is, world rows included
```

`<selector>` is CUO's existing Minecraft-style player vocabulary (`@s`, `@a`, `@p`, `@r`, and the bracketed
`name`/`type`/`distance`/`limit`/`sort` filters), expanding over the same entity table `/heal` uses. Every
other shape is refused with the usage line, including a bare number: reading `4101` as an instance id would let
a typo address an item the player never named.

Answering one item prints its id, definition, location, revision and each property; answering a body prints one
line per item in slot order, so the listing reads like the inventory it describes. A target that matches nobody
is refused by name ("no players match selector", "no item is carried by X in slot N"), so an empty answer is
never mistaken for a silent one. A body that answers empty is the one place the reason matters, and the
caller's own body gets it in the answer: an item joins this record on the holding client's first report of it,
which is also when it gets the instance id a target can name.

A verb that names ONE item needs one body to name it on. That verdict is taken AT EXECUTION, on the resolved
set, and answered by name — `@a slot 0` says how many bodies matched and what to write instead (`@p`, a
selector with `[limit=1]`, or the item's own id), and it takes THREE members in the session to see it, because
`@a` names the remote bodies and never the caller's own. The grammar stays as wide as the selector vocabulary on
purpose: the player writes whatever selector it likes, and the ambiguity is settled where it can be seen rather
than by making a single-target parameter mandatory (user ruling 2026-10-14). The listing form is the other shape
and stands: several bodies are answered separately, each block its own answer.

## Self-check (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| The kernel item read model | `IKernelItemFacts.QueryItems()` added; the authority already implemented it, so no behaviour moved | the interface's implementors: the authority resolves it, the admission seam's test fake gained the member in the same change |
| The console's selector expansion | one expansion extracted (`CommandPlayerTargets`); `/heal` now calls it instead of its own copy | the parse/resolve path is unchanged for `/heal` — the family's existing selector cases stay green |
| The target grammar | a pure parser: the shapes it accepts, and every shape it refuses | `ItemDataQueryParserTests` — the refusal cases plus the accepted shapes |
| The wording | a pure renderer: every property, every component field kind, every location family | `ItemDataRendererTests` — including the container-contents case and the enum/string/string-list field kinds |
| The wiring | console → kernel → render, plus every refusal | `CommandConsoleItemDataTests` — 19 integration cases through the real console and the real kernel |

## Acceptance (the rows a run must read)

One two-client session, host and guest both in a world:

| # | Scenario | Expected |
|---|---|---|
| 1 | The guest runs `/data get` once the world is entered | Its carried items, starting supplies included, one line each in slot order with instance id, definition, slot and condition — a member's client registers its carried inventory at the generation-finished edge |
| 2 | The host picks an item up (or moves one in its inventory) and then runs `/data get` | That item is listed; an item the host has never touched is absent, and the empty answer names why — the host registers a carried item on its first domain entry |
| 3 | The host runs `/data get id <id>` on an item whose state it knows (a partly used container with liquid, or an item with a charge field) | Every property printed matches the item: condition, favourited, each liquid stack with its millilitres, each component field with its kind, and the contents by id |
| 4 | The guest runs `/data get id <the same id>` for an item the host registered | The same rows the host's own read printed — the replay kernel agrees with the authority |
| 5 | `/data get id 999999`, `/data get slot <empty>`, `/data get @a slot 0` in a session with two members BESIDES the caller, `/data get @x`, `/data get 4101`, `/data set ...` | One explicit line each — "no item is recorded", "no item is carried by … in slot …", the count of bodies the ambiguous selector matched together with what to write instead, "no players match selector", the usage line, and the not-implemented answer for the write verb — with no silent no-op anywhere |
| 6 | An item that IS recorded and then applied to a limb (a splint or tourniquet) | Listed by the body form as `limb N`, and read by its id as `applied to <owner>, limb N` |

**Batch `20261018-q` judged rows 1, 2, 3, 4 and the 5a/5c halves on the deployed `30dbb252`, against the SNBT
renderer** (record `docs/evidence/acceptance/item-data-read-commands-20261018-q.md`): both roles list their
one carried item at world entry, an item's whole document and a single path print the same values on both
peers, and the unknown-id, empty-slot, bad-selector and retired-verb answers are each one explicit line.
Row 5b (the ambiguous selector, which needs two members besides the caller) and row 6 (an item applied to a
limb) were not staged, so the ticket stays in `review/` on them. The row-2 wording above predates the landed
host registration: the host's untouched supplies are listed at world entry now, and the record reads that
behaviour.

## Limits, recorded rather than hidden

- **Reads only, and the write half has since landed its first verb.** `set` is
  `done/item-data-write-commands.md` now; `merge` and `remove` still answer "not implemented yet", and their
  authority model is the finding above, not a detail to be improvised.
- **`slot` is a body slot.** The negative encodings the wire uses for worn items are a transport detail and are
  not accepted; a limb-installed item is reached through the body listing and read by its id.
- **The cursor target has landed (2026-10-18), in the umbrella ticket.** The user asked for a Minecraft-style
  selector including "the item nearest the mouse"; that needed a new GameAdapter capability port (the console
  reads the kernel, which has no cursor), and it landed with the write half's port rather than here
  (`done/item-and-entity-data-commands.md`, *The cursor target landed*). This ticket's rows do not read it.
- **Reads the session's record, not the local scene — and having no record is a real answer.** The verb
  answers what CUO records, and the record is filled by the holding client's report. A member's client registers
  its carried inventory (starting supplies and worn items included) at the generation-finished edge, so a guest's
  own body reads completely; the HOST's own untouched items have no row and no instance id at all — the host's
  supplies are its local authority and "their ids are assigned lazily on first domain entry", which
  `CarriedInventoryReporter` states as its own reason for being guest-side. The verb therefore lists what the
  session knows, and says so when the answer is empty; closing that gap (a host-side registration path, or a
  local-scene read that can name an id-less item) is stage B's first question, recorded in the umbrella ticket.
  A row is also the client's own last report, so a value the scene changed a frame ago appears when its report
  lands.
- **No console completion for this verb.** It declares one free-text argument, and the console's suggestion
  sources are keyed by argument kind, so `/data get @<Tab>` suggests nothing; the selector vocabulary itself is
  the one `/heal` completes. A grammar that mixes keywords with a selector needs a per-argument completion
  source, which is its own change rather than a fix here.
- **Permission is `Anyone` for this verb.** Another player's carried items are already visible in the remote
  inventory view, and the verb only reads. The host's call over the family belongs to the write stages, which
  the umbrella ticket carries.

## Independent review (fresh context, against the frozen tree, before the commit)

One blocker, two majors and six minors. Every one is dispositioned here, because a finding and its answer belong
where the next reader meets them rather than in a report nothing opens:

| Finding | Disposition |
|---|---|
| **Blocker — the own-body claim was false.** The verb claimed to list "what its own body holds"; the host's untouched carried items are not in the record at all and have no instance id, because a member's client registers its carried inventory while the host's supplies wait for their first domain entry (`CarriedInventoryReporter` is guest-side for exactly that reason). The integration cases could not see it: they seed the kernel directly. | The claim is corrected everywhere it appeared — the grammar table, the answer's own wording (a new case pins the reason an empty own-body answer is empty), and acceptance rows 1 and 2, which now read a guest's registered inventory and a host item that HAS entered the domain. The gap itself is carried as stage B's first question in the umbrella ticket, with the code evidence. |
| **Major — the read model's reader list was understated**: the protocol command handler reads the same rows by id. | The interface's own documentation now names every consumer. |
| **Major — the delivery checklist carried the PREVIOUS cycle's evidence** on the two boxes this cycle had reset. | Both boxes were rewritten with this cycle's measured numbers before the commit. |
| **Minor — the audit line counted the whole kernel**, not the rows the answer listed. | It counts the rows the answer listed. |
| **Minor — the line counts used a non-blank-line measure** the architecture gate does not use. | The gate's own measure (every line) is used: 584 → 571, and the watchlist entry for the class is refreshed. |
| **Minor — the case counts were wrong** (15 where the classes run 16), and the obvious test filter also matches unrelated classes. | The counts are per class and measured: 25 parser, 10 renderer, 19 integration. |
| **Minor — the verb offers no console completion**, which the "one selector vocabulary" wording implied. | Recorded as a limit: the grammar mixes keywords with a selector, so per-argument completion is its own change. |
| **Minor — `/data SET` was refused as an unknown verb** while `/data GET` worked. | The reserved-verb check is case-insensitive now. |
| **Nits — a trailing space, an LF-only ticket, the survey restated in two tickets.** | Removed, normalized to the tree's line endings, and the umbrella ticket now points at this survey instead of repeating it. |

**After the commit — a user ruling and the delta it produced.** Asked whether a selector that matches several
bodies breaks the one-target idea, the user ruled on 2026-10-14 that a target's uniqueness is decided when the
command RUNS, not by narrowing the grammar or making a single-target parameter mandatory. `ReadSlot` therefore
refuses a multi-body resolution by name, and the write stage inherits the rule (recorded in the umbrella
ticket). A narrowed second review of that delta found the guard sound and four defects in its RECORDS — a
two-member session leaves only ONE remote, so the acceptance row needed a third member; the checklist numbers
had not actually been refreshed; the count in the table above was left at 16; and the refusal wrote no log line.
Each is fixed before the delta's commit.

## Non-goals

- Not the write verbs, `give` or `spawn` — each is its own stage in `done/item-and-entity-data-commands.md`.
- Not a raw property editor: only the properties the game's own data model carries are addressable, and a
  property the codecs do not whitelist is out of reach by construction (the survey above says where).
- Not a new player-facing documentation page: the reading surface for a console verb is the console's own
  `/help`, which is generated from the command registration, so the page a player opens is already the one
  that answers.
