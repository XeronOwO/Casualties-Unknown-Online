# The item state language: SNBT text, NBT paths and adapter-schema completion

- Status: Done — the language and the two commands that speak it (`/data`, `/give`) landed in this
  cycle, and batch `20261018-q` judged every acceptance row it owes on the deployed `30dbb252` (record
  `docs/evidence/acceptance/snbt-state-language-20261018-q.md`). Decision 264 ruled the syntax; this ticket
  owned the parser, the writer, the path resolver, the console completion and the rewiring of both verbs onto
  them. The two consumer tickets re-derived their acceptance rows here (`todo/item-and-entity-data-commands.md`,
  `todo/give-item-to-a-player-command.md`).
- Priority: High — it is the language decision 264 made mandatory, and both item commands ship it.
- Category: Command surface / item state / data language
- Source: decision 264 (`docs/decisions/active.md`), which supersedes the `condition` / `favourited` /
  `liquid` / `component` token vocabulary and orders "the parser, the writer, the path resolver and the
  console's SNBT-aware completion" as a ticket of their own.
- Related: `docs/decisions/active.md` 264, `todo/item-and-entity-data-commands.md` (*The data family's
  syntax*), `todo/give-item-to-a-player-command.md` (*The syntax decision*),
  `done/item-data-write-commands.md` (the token grammar this replaces), `review/item-data-read-commands.md`
  (the read half the new renderer keeps), `docs/architecture/member-report-validation.md` (the give
  request's census row), `docs/evidence/sync-coverage-matrix.md` row I11.

## What is asked

One data language across the item-data command family:

```text
/data get    <target> [<path>]                       # the whole state, or one path, as SNBT
/data merge  <target> <snbt>                         # merge keys into the item's state
/data modify <target> <path> <set|merge|append|prepend|insert <n>> <snbt>
/data remove <target> <path>
/give        <selector> <itemId>{<snbt>}             # the created item's state, attached to its id
```

`<target>` stays the family's own: `slot <n>`, `cursor`, `id <n>`, or a player selector (the selector's
own `slot <n>` narrowing included). `<path>` is the NBT path subset below. `/data set` is superseded:
`modify … set` is the same operation with an explicit path, and keeping both would be two spellings of one
verb.

## The dialect, read off the latest Minecraft rather than from memory (2026-10-17)

The rule is "SNBT as Minecraft means it today", so the dialect was checked against the current wiki and
narrowed where the item state has no use for a feature. Sources: [NBT format](https://minecraft.wiki/w/NBT_format)
(its *SNBT format* section), [NBT path](https://minecraft.wiki/w/NBT_path_format) and
[the `/data` command](https://minecraft.wiki/w/Commands/data).

What the current format carries, and what this language does with it:

| Minecraft today | This language | Why |
|---|---|---|
| Compounds `{key:value,…}`, keys unquoted when `[A-Za-z0-9_\-.+]` and not starting with digit/`-`/`.`/`+`, else single- or double-quoted | accepted, written back with the same rule | the item state's keys are plain names, so the readable form is the common one |
| Strings quoted with `"` or `'`, escapes `\\`, `\"`, `\'`, `\n`, `\t`, `\r`, `\b`, `\f`, `\s`, `\xhh`, `\uhhhh`, `\Uhhhhhhhh` | accepted; written with double quotes | an operator pastes the `/data get` line back; the writer must be readable and lossless |
| Unquoted strings when they match the key rule | accepted as `SnbtString` | `LiquidId:water` is a legal shorthand; the writer still quotes, so the canonical form is unambiguous |
| Numbers with `b`/`s`/`i`/`L`/`f`/`d` suffixes, plain integers and decimals, hex/binary, `_` groups, and the `s`/`u` signedness suffixes (25w09a) | accepted; typed as integer or floating. A literal with no suffix is an int without a decimal point and a double with one, exactly as Minecraft resolves it — so `/data get` prints a float as `0.8f` and a plain decimal the operator wrote as `0.8d` | the state has `int` fields (component ints/enums) and `float` fields (condition, amounts), and the writer must say which one it printed |
| Heterogeneous lists (1.21.5) and trailing commas (25w09a) | accepted | they cost nothing to accept and keep pasted SNBT working |
| Typed arrays `[B;…]`, `[I;…]`, `[L;…]` | **refused by name** | the item state carries no typed array; accepting one would name a value the adapter cannot bind, so the parser says so instead of guessing |
| `bool(arg)` / `uuid(arg)` operations (25w10a) | **refused by name** | same reason: no field in the state takes an operation |
| List indices, negative indices (`[-1]` is the last), `[]` all-elements, `[{…}]` filters, `{}` root filters, quoted keys | this language accepts **keys, quoted keys and `[index]` only**, negative index included; wildcards and filters are refused by name | every verb here addresses ONE item and one value; a multi-selection would need Minecraft's "must select exactly one / may select many" split, and no CUO verb has the second half |
| `modify` operations `set`, `merge`, `append`, `prepend`, `insert <index>`, each with a `value`/`from`/`string` source | the four operations, each with the SNBT value directly (`value` is not spelled) | there is no second source in CUO: the operator's value is the only source, so the keyword would be noise |

Two deviations are deliberate and are recorded where the commands are described:

- **`/data merge` is Minecraft's shallow merge** — it replaces a top-level key wholesale; changing one
  field of a component is `/data modify … merge`. This matches vanilla's own worked example pair (vanilla
  tells the reader to use `modify` for the nested field rather than `merge`).
- **`/give`'s attached state is an overlay, not a document**: it is merged onto the definition's captured
  state, and compounds overlay recursively, so `{Components:{BatteryItem:{charge:0.9}}}` changes the
  charge and keeps the captured `on`. A give has no document to start from — the operator names only what
  they want to be different — while `/data merge` edits a document the operator just read.

The model is a small immutable tree: compound, list, string, integer, float, boolean. No external NBT
library is taken: the project targets net48 and an NBT dependency would buy typed arrays, binary tags and
a second data model this surface never uses.

## The layer split (decision 264, restated as the implementation's contract)

- **Runtime** owns the text: `SnbtParser`, `SnbtWriter`, `NbtPath` + the tree operations, and the sentence
  the operator reads. It judges SHAPE: malformed text, a path that does not resolve, an operation whose
  target is not a compound/list, a value the operation cannot take.
- **Game Adapter** owns the SCHEMA and the legality:
  - which top-level keys an item's state carries (`Condition`, `Favourited`, `Liquids`, `Components`),
  - which component type names and fields a given definition or live item carries,
  - the liquid stack's shape (`LiquidId`, `Amount`), whether the item is a liquid container and how much
    it holds (the game's `WaterContainerItem.Capacity`), and the value ranges a field's kind admits,
  - the projection from the save-shaped state to the tree and the binding back to a capture.
  It refuses by name, before anything is written or created: an unknown key, a key the definition does not
  carry, a component the item does not carry, a field its component does not carry, a kind mismatch, two
  stacks of one liquid, a negative amount, more liquid than the container holds.
- **Completion** asks the adapter for the schema of the item or definition at hand, so the keys offered
  are the keys the capture really carries — never a hand-kept list in the console.

This is why the schema is a port and not a table in the Runtime: the component types and fields come from
the game's own captured components, and the capacity is the game's own rule.

## The ports

Two adapter ports change, and one is added. All three live in `Runtime/GameAdapter/`; the adapter
implements them and the Runtime registers an unavailable default for a build without an adapter.

- `IItemStateLanguage` (new): the state's tree projection and schema.
  - `bool TryDescribe(ItemData state, out SnbtValue document)` — the kernel row's document; `/data get`
    prints it on every peer, including one that does not hold the item.
  - `bool TryDescribe(CharacterItemMsg capture, out SnbtValue document)` — the same projection for a
    local capture (the write verbs' "before" value).
  - `ItemStateSchema? DescribeSchema(ItemData state)` — the completion schema for a kernel row.
  - `ItemStateSchema? DescribeDefinitionSchema(string definitionId)` — the completion schema for a
    definition, read from its prefab; null when the build does not know it.
- `ILocalItemAccess.WriteLocalItem(ulong instanceId, SnbtValue desired)` returns a
  `LocalItemWriteOutcome`: `Applied` with the re-read capture, `NotHeld`, `NotWritable`, or `Refused` with
  the adapter's sentence. The write validates the whole document first and mutates second, so a refused
  document changes nothing; the re-read capture is what the report carries, never the caller's guess.
- `IItemGrantSource.TryCreate(string definitionId, SnbtValue? state, …)`: the state is overlaid onto the
  prefab's captured state and bound before the instance id is allocated, so a state the definition cannot
  carry is refused before anything exists.

The console's completion is one new seam, `ItemStateCompletion`, consulted by
`CommandConsoleService.Suggest` before the argument-kind fallback. It parses the current token's SNBT
prefix, walks the schema to the position being typed, and returns whole-token replacements (the console
replaces one token per suggestion): top-level keys, component type names, component field names, booleans
and the liquid ids. `CommandSuggestion` gains `Verbatim`, because a completed state is already a command
line token and must be inserted without the console's token quoting.

## What the paths and operations mean

- A path is `key`, `"quoted key"` or `[index]` nodes separated by dots (a dot before `[` is optional):
  `Liquids[0].Amount`, `Components.BatteryItem.charge`, `Components."X"`. A negative index counts from
  the end (`Liquids[-1]`).
- `get` resolves one path and prints its value; `set` replaces the value at an EXISTING path (it does not
  create keys — `merge` is how keys are added); `merge` merges a compound into the compound at the path,
  shallow; `append`, `prepend` and `insert <n>` add to the list at the path, with `insert` taking
  `0..count` and no negative index; `remove` deletes the node at an existing path.
- The document is the item's OWN state. `Contents` is not part of it: each content is its own item with
  its own id and its own report route, so a path into a child is refused by name with the child's own ids
  named (the read half already lists them), rather than edited through the parent. This narrows the
  example in `item-and-entity-data-commands.md`'s syntax section, which showed `Contents[1].Condition`.
- `remove` of a top-level key or of a component field produces a document the item cannot carry
  (the state is complete: every top-level key and every captured field is required), so the adapter
  refuses it by name; the values that CAN be removed are the list elements, a liquid stack above all.
  The old `remove component <Type> <field>` "reset to the kind's zero" is gone; the explicit
  `modify … set 0` / `set false` / `set ""` says what it means.

## What the commands replace

- `ItemDataWriteParser`, `ItemDataWrite` and `ItemDataWriteValues` (the token grammar and its value
  checks) are deleted with the vocabulary they parse. The old usage lines, the property-name answer
  wording and the per-property self-check rows are replaced by path-and-value rows.
- `ItemDataCommands` gains a verb parser for the new forms, keeps the target grammar and the one-item
  verdict, and renders with the writer. `/data get` prints the document (or the path's value) and keeps
  the location/revision/contents lines the read half prints around it.
- `ItemGiveParser` splits `<itemId>{<snbt>}` at the first `{`, parses the state as a compound and keeps
  the optional `slot <n>` after it. `PlayerInventoryGiveRequestMsg.Properties` becomes `State` (the
  canonical SNBT text, length-bounded and re-parsed by the host); the host overlays it onto its own
  capture through `IItemGrantSource`, so no schema crosses the wire and a member cannot hand over a state
  the definition does not carry.
- `ItemStateSettlement` settles a refused member write by describing the kernel row and writing that
  document through the same port, so the refused-write path also speaks one form.

## Mechanism inventory

| # | Mechanism | Named consumer |
|---|---|---|
| 1 | `SnbtParser` / `SnbtWriter` | `/data` and `/give` parse the operator's text; `get` and both answers print it |
| 2 | `NbtPath` + tree operations | `/data get|merge|modify|remove` |
| 3 | `IItemStateLanguage` | `/data` (describe, schema), `/give` (definition schema) and the console completion |
| 4 | `ILocalItemAccess`'s document write | `/data`'s three write verbs and `ItemStateSettlement` |
| 5 | `IItemGrantSource`'s state argument | `/give` (host side) |
| 6 | `ItemStateCompletion` | the console's `Suggest` for `/data` and `/give` |
| 7 | `PlayerInventoryGiveRequestMsg.State` | a member's `/give` request and the host's re-parse |

## Self-check table

| # | Claim | Evidence |
|---|---|---|
| 1 | The parser accepts the dialect above and refuses what this language excludes, by name, with a position | `SnbtParserTests` (structure, escapes, heterogeneous lists, trailing commas, typed arrays, operations, depth, truncation) and `SnbtNumberTests` (suffixes, bases, digit groups, signedness, range and finiteness) |
| 2 | The writer's output parses back to the same tree and is the form an operator can paste | the round-trip rows in `SnbtParserTests` (keys that need quoting, escapes, floats with `f`, negative numbers, empty compounds/lists) |
| 3 | Paths resolve keys, quoted keys and indices, including negative indices, and refuse wildcards/filters by name | `NbtPathTests` |
| 4 | The five modify operations and remove have the semantics stated above, with their refusals | `SnbtTreeTests` (set on a missing path, merge on a non-compound, append on a non-list, insert out of range, remove on the root) |
| 5 | `/data get` prints the document from the KERNEL row on both roles, and a path prints just that value | `CommandConsoleItemDataTests` (single item, body listing, slot, cursor, id, path, missing path) |
| 6 | `/data merge` is shallow and atomic; `/data modify` and `/data remove` commit once through the local scene and report the re-read capture | the item-data console suites (each verb's applied and refused cases; a refused second key leaves the first unwritten) |
| 7 | The document the adapter binds is complete and legal, and every refusal names the key/path | the adapter's apply path is exercised by the give/data acceptance run (a unit test host has no game assembly); the local-scene fake pins the Runtime's half |
| 8 | `/give @p waterbottle{Liquids:[…],Condition:0.8}` creates the item with that state, and an unknown key/component/field refuses by name before anything exists | give console suites (host/member) plus the acceptance batch's row |
| 9 | A member's give request carries the SNBT state text and the host re-parses it; a property token is no longer accepted | member give suites, the message's own bound case, sync-coverage row I11 |
| 10 | Completion offers the adapter's keys at the root, component types under `Components`, fields under a component, booleans after `Favourited:` and liquid ids after `LiquidId:`, and nothing where the state has no choice | `ItemStateCompletionTests` |
| 11 | The old token vocabulary is gone from the tree and from every usage/answer | the deleted `ItemDataWriteParser`/`ItemDataWrite`/`ItemDataWriteValues`/`ItemDataProperty` files and the `SourceShape`/census gates that pass without them |
| 12 | Not covered here | the live two-client run (the acceptance batch), and the adapter's game reads (capacity, component presence) that only a real client can prove |

## Acceptance the batch owes

1. Host: `/give @p bandage` — the answer names id and slot; `/data get id <id>` prints the same document on
   BOTH peers.
2. Host: `/give @p waterbottle{Liquids:[{LiquidId:"water",Amount:500}]}` — a fresh empty container is
   filled, and `/data get id <id> Liquids[0].Amount` reads `500f` on the member's side.
3. Host: `/give @p waterbottle{Condition:0.5,Liquids:[{LiquidId:"water",Amount:300},{LiquidId:"blood",Amount:100}]}`
   — both stacks land (`500` is the fixture capacity; row 2 fills it exactly); a state over capacity is refused
   by name and nothing is created.
4. `/give @p largebattery{Components:{BatteryItem:{charge:0.9}}}` on a definition whose capture carries the
   component — the field changes and the sibling fields keep their captured values; on one that does not,
   the named refusal and nothing created.
5. `/data modify id <id> Liquids[0].Amount set 200` — the answer shows `500f -> 200f`, and the next `/data
   get` on both peers agrees.
6. `/data modify id <id> Components.BatteryItem.charge set 0.5` — the same read-back.
7. `/data merge id <id> {Condition:0.8,Favourited:true}` — both keys change; a merge whose second key is
   unknown is refused whole and the first key is unchanged (atomicity).
8. `/data remove id <id> Liquids[0]` — the stack is gone; `/data remove id <id> Condition` is refused by
   name and nothing changes.
9. A path that does not resolve (`Liquids[3]`, `Components.Nope.charge`) is refused with the path and the
   list/component it does carry.
10. Member, switch open + grant: the same `/data` write and `/give` from the member's console; the answers
    print on the member's console and the host's `/data get` reads them.

**Batch `20261018-q` judged all ten on the deployed `30dbb252`** (record
`docs/evidence/acceptance/snbt-state-language-20261018-q.md`): rows 1-3 and 5-10 pass as written (row 5's
"`500f -> 200f`" was read as this run's `300f -> 200f`, the same operation on the amount the first bottle
carried), and row 4's captured-component half is exercised on the emergencylight's `CustomItemBehaviour` —
no live definition carries a `BatteryItem` line — with the battery's named refusal read as the row's second
half.

## Limits, recorded rather than hidden

- **`Contents` is not addressable through a parent's document.** Each content is its own row; use its own
  `id` (the read half lists the ids). A path into `Contents` is refused, not silently ignored.
- **No path completion.** Completion covers the SNBT token (keys, component types, fields, booleans, liquid
  ids) and not the bare `<path>` token of `get`/`modify`/`remove`; the schema walk is the same, and this is
  the next small unit if the operators ask for it.
- **No definition completion** for `/give`: the item id before `{` stays free text, exactly as the read half
  recorded, because the resource-location catalog is not wired into the verb.
- **The document is the save-shaped state**, not a rendering of every field the game's components have:
  a field the capture does not carry is not in the document, and a value outside it is refused rather than
  written. This is the capture whitelist's boundary, not a second one.
- **No typed arrays, no NBT operations, no wildcards/filters**: refused by name with the accepted form.
- **The old token syntax is not kept beside the new one** (decision 264, pre-release): a `/data set` line
  and a `liquid water 500` argument are answered with the new usage, not translated.

## What landed

**2026-10-17, `docs/backlog/review/snbt-state-language.md`'s cycle.** `src/CasualtiesUnknownOnline.Runtime/Data/`
holds the immutable value tree (`SnbtValue` and its six kinds), `SnbtParser`, `SnbtNumber`, `SnbtText`,
`SnbtWriter`, `NbtPath`/`NbtPathParser` and `SnbtTree`; `Runtime/GameAdapter/` holds `IItemStateLanguage` and
the recursive `ItemStateShape` schema; the Game Adapter's `ItemStateDocument` owns the four keys, the stack and
component shapes, the liquid registry choices and the two legality rules it alone can read (a container's
presence and its `Capacity`), and `ItemStateLanguage` registers that over the unavailable default. The write
ports changed with the language: `ILocalItemAccess.WriteLocalItem` takes the document and returns the re-read
capture, a refusal sentence and an adapter note (the condition-at-zero consequence), and
`IItemGrantSource.TryCreate` takes the give state and binds it before allocating an id.

`/data` is the four verbs over the target grammar it already had (the `set` verb is gone — `modify … set` is
the same operation with a path): `get` prints the document from the kernel row (or the one path's value),
`merge` is Minecraft's shallow merge at the root, `modify` runs `set`/`merge`/`append`/`prepend`/`insert <n>`,
and `remove` deletes an existing node — a top-level key or a component field the item always carries is
refused by the adapter's bind, and a liquid stack is the ordinary removable node. The answer's clauses are
built from the tree before and the tree the adapter re-read, so a value the bind dropped cannot be reported as
written. `ItemDataWriteParser`, `ItemDataWrite`, `ItemDataWriteValues` and `ItemDataProperty` are deleted with
the vocabulary they parsed.

`/give` takes the id with its state attached (`/give @p waterbottle{Liquids:[…],Condition:0.8}`); the state is
overlaid recursively onto the definition's capture, bound before an id exists, and the answer names the state
it gave. The member's request carries the canonical state text and its length bound; the host re-parses and
re-binds it. The console completes inside the state from the adapter's schema — top-level keys, component
types, fields, booleans and liquid ids — and `CommandSuggestion.Verbatim` keeps the inserted token's own
quotes intact; path completion is not part of this cycle (recorded above as a limit).

Tests: `SnbtParserTests`/`SnbtNumberTests`/`NbtPathTests` (the language and its refusals),
`ItemStateCompletionTests` (the schema walk and both commands' wiring), `ItemDataVerbParserTests` (the verb
grammar and the cursor target), `ItemDataRendererTests` (the printed form), and the migrated item-data and
give console suites. A test host has no game assembly, so the adapter's own `ItemStateDocument` is the
acceptance batch's evidence, exactly as `ItemStateCodec` was.

**Recorded deviations from the first design note.** A plain decimal is a double, not a float (Minecraft's
resolution rule); the writer therefore prints `f` only for floats the adapter projected. `Contents` is refused
by name rather than addressed through the parent (each content is its own row).
