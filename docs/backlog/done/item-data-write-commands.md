# The item data write verb: set one property on the item this client holds

- Status: Done — the verb, the member route and the host's rule for it landed 2026-10-14 (S1b of
  `todo/validation-and-source-checks.md`).
  **Accepted 2026-10-14 in batch `20261014-a`** (`docs/evidence/acceptance/item-data-write-commands-20261014-a.md`): every row passes — the host's own writes on a carried item, a component field, a liquid stack and a world item, both sides of the host's rule for a member's write (refused and settled back / admitted and recorded), the record-gap refusal, the four refused targets, and the written values surviving a save, a restore and a rejoin.
- Priority: Medium-High
- Category: Command surface / item state
- Source: Stage B of `todo/item-and-entity-data-commands.md`, cut off 2026-10-14 — the first write verb
  (`/data set`) plus the local-scene port every later write and the cursor target need. The stage's other
  halves (`merge`, `remove`, the cursor target and the record gap the read half found) are still open there,
  each with its own verification.
- Related: `review/item-data-read-commands.md` (the read half, and the property survey this verb writes
  through), `todo/item-and-entity-data-commands.md` (the family's remaining stages),
  `todo/validation-and-source-checks.md` (S1b: the console write's own wire kind, the host's rule and the
  refusal's way back), `todo/host-rules-replication.md` (what would let a member's console know the rule
  before it writes), `review/in-game-command-console-interactive.md` (the console the verb lands in),
  `docs/architecture/domains.md` (the item kernel domain), `docs/contracts/item-features-matrix.csv`

## What landed

`/data set` writes ONE property of ONE item, and the record follows the write: the command computes the
value against the item's LOCAL capture, applies it to the local object, and reports the result through the
same two calls a native use's host branch reports with. The row a command writes is therefore the row the
game would have written — condition, the favourite flag, one liquid stack and one typed component field.

| File | What it is |
|---|---|
| `src/CasualtiesUnknownOnline.Runtime/GameAdapter/ILocalItemAccess.cs` + `LocalItemWriteResult.cs` | The port: the item this client holds under an id, its captured state, and one write into it |
| `src/CasualtiesUnknownOnline.Runtime/GameAdapter/UnavailableLocalItemAccess.cs` | The build without an adapter: reads answer "not held", writes are refused — never a scene that looks empty |
| `src/CasualtiesUnknownOnline.GameAdapter/Items/LocalItemAccess.cs` | The adapter's implementation: the id's own O(1) scene index, the codec's capture, and the save restore's three write steps |
| `src/CasualtiesUnknownOnline.Runtime/Session/Commands/ItemDataWriteParser.cs` + `ItemDataWrite.cs` + `ItemDataProperty.cs` | The write grammar: the read verb's target shapes, then one closed property keyword and its value |
| `src/CasualtiesUnknownOnline.Runtime/Session/Commands/ItemDataWriteValues.cs` | The value half: what the item's own capture admits, and the application to it |
| `src/CasualtiesUnknownOnline.Runtime/Session/Commands/ItemDataQueryParser.cs` | The target grammar gained a prefix form (`TryParseTarget`), so the write verb reuses it instead of growing a second selector vocabulary |
| `src/CasualtiesUnknownOnline.Runtime/Session/Commands/ItemDataCommands.cs` | The handler: the one-item rule, the refusals by name, the report route, the answer |
| `src/CasualtiesUnknownOnline.Runtime/Session/Commands/ItemDataRenderer.cs` | `Wrote` (the answer line) and the captured-shape value formatters |

## Entry mapping (what this mirrors, one line each)

| CUO entry | The game's own call site it is the counterpart of |
|---|---|
| `/data set` (the gesture: typing a value) | **None, deliberately** — a CUO operator surface, asked for by the user on 2026-10-07, not a mirror of a native gesture. Its scope is argued there rather than inherited from an older CUO table. |
| the STATE it writes | the game's own save restore (`item.condition = …`, the `[Saveable]` fields by reflection — the liquids ride that same reflection pass there, `SaveSystem`'s `List<LiquidStack>` case), which is why `LocalItemAccess` calls the codec's restore halves instead of inventing a setter: `condition`/`favourited` assigned directly, then `RestoreLiquids`, then `RestoreComponentStates`. `SetCondition` is NOT used: it drains the container's water by ratio. |
| the REPORT it makes | `ItemUseSync.OnItemUsed`'s host branch (itself the postfix of the game's own use: `Body.UseItem` / `Body.UseItemInHand`): a world row's local copy is the fact and goes out as the correction, anything else as the carried fact. |
| the TARGET it names | the read verb's own grammar (a selector, a body slot, an instance id), so an operator addresses an item the way they read it. |

Nothing here needs information its gesture cannot carry: the item comes from the target, the property and
its value from the operator, and a target that names several items is refused at execution rather than
resolved by a rule (the one-item ruling of 2026-10-14). No entry falls back to a default — "the most
injured limb" and "the first match" have no counterpart in this verb.

## The write path (why it is the holder's own report)

The read half reads the kernel row; a write cannot go that way. A carried item's state IS its holder's
report — the host's authority row is what the holder's client last stated, and the client's local object is
the fact the next report re-states. So the command:

1. resolves ONE item (the read half's targets, minus the listing shape),
2. takes the item's LOCAL capture through `ILocalItemAccess.ReadLocalItem`,
3. checks the value against that capture and applies it to it (`ItemDataWriteValues`),
4. hands the computed state back through `ILocalItemAccess.WriteLocalItem`, which writes the local object,
5. reports it: a WORLD row's local copy is the fact and every peer's copy adopts it
   (`SendWorldItemCorrection`, the #194 ground-canister path); anything else is this client's carried fact
   (`SendItemCarriedSync`, which commits `TrySpawnCarried`/`TryUpdateState` and broadcasts).

Steps 3–5 are the same calls a native use's host branch makes (`ItemUseSync.OnItemUsed`), which is the point:
the record cannot tell a command write from a played one. The route is chosen the way the native path chooses
it — by whether the item's location CHAIN is rooted in the world (`ItemLocationChain.IsWorldRooted`, the
kernel's own container walk that the layer reset and the archive read the same way), not by the item's own
row kind: a child of a dropped backpack is world-rooted even though its row says "contained", and it takes the
correction the native path gives it, while a child of a carried container takes the carried-fact broadcast,
whose receiver merges nested facts by id (the tested nested-container case).

**Who may write.** Every client, and the HOST decides. A write is the holder's own report, so the verb is
open to every console and the host's own rule judges a member's write: `HostRules`'
`AllowMemberItemDataWrite` (off by default, the family's "not a cheating surface by default") is read at the
admission seam, so the host may open it, close it, or close it mid-session. What made this possible is the
member's write having a wire kind of its own (`ItemDataWrite`, the same `UpdateItemStateCommand` a native use
maps to — the row it writes is the row the game would have written): before it, a member's write was
byte-identical to a native use and the host had nothing to grant or refuse separately. The two halves of a
refusal are in place with it — the seam ANSWERS this kind (the writer already applied the value to its own
object), the answer names the report it judged (`WireCommand.RefusedKind`), and the member's client puts the
session's recorded state back and prints the refusal where the operator typed the command. A host whose rule
is closed therefore refuses a member's write by name and both sides keep agreeing.

**What a write cannot reach.** An item this client does not hold is refused by name, and the refusal says
where the row puts it. That is the model, not a shortcut: another member's carried item's local form is a
render proxy, and there is no mechanism today that edits a remote player's carried item (the umbrella
ticket's finding). A limb-installed item has no local `Item` object at all (its own use action destroyed it),
so it is refused as not held rather than written into a component this verb does not model.

## The grammar

```text
/data set <selector> slot <n> condition <n>              the SavedItem scalar
/data set <selector> slot <n> favourited <bool>          the SavedItem flag (yes/no/true/false/on/off)
/data set <selector> slot <n> liquid <liquidId> <ml>     one stack of the item's own container
/data set <selector> slot <n> component <Type> <field> <value>
/data set id <instanceId> <the same property shapes>     one item wherever it is
```

The target half is the read half's own grammar (one parser, two verbs), so every selector shape the read
verb accepts is accepted here, including the shapes that name several items — the verdict on those is taken
at execution, on the resolved set, per the user ruling of 2026-10-14. What a write adds is the closed
property vocabulary: `condition`, `favourited`, `liquid`, `component` are the words the read half PRINTS, so
an operator sets what the verb just showed. An unknown word is refused with the usage line rather than
guessed at.

A component field travels as TEXT until execution because its kind is a fact about the target: the value is
parsed against the kind the item's own capture tagged the field with (`SaveableFieldKind` — the same table
the capture classifies with), and an enum is written as its underlying int, which is the shape it crosses and
is boxed back in.

## Refusals (each answered by name)

| Refusal | The answer's own words (fragments; the line continues) |
|---|---|
| The target names no single item | `a write names ONE item: add 'slot <n>', or use 'id <n>'` |
| A selector matched several bodies | the count and what to write instead (`@p`, `[limit=1]`, the id) |
| The item is another member's | `That item is carried by <id> — a write is the holder's own act, and no route edits another player's carried item yet.` |
| The record has no row for that slot/id | `No item is recorded for <owner> in slot <n> yet — …` / `No item #<id> is recorded in this session.` |
| The row exists, this client holds nothing | `#<id> is recorded as <location> but this client does not hold it — …` |
| A property the item does not have | the component/field/liquid it DOES have is listed |
| A value that is not that field's kind | `'fast' is not a float for BatteryItem.charge.` |
| The scene refuses the write | its own reason (a proxy or a standing incarnation, or nothing held) |
| A member's write, with the host's rule closed | the verb's own line first (`… written here and reported to the host, which judges a member's write by its own rule; a refusal puts the old value back and says so in this console`), then the host's answer as its own console line: `The host refused the write to #<id> <definition> (this host's rule does not open the item-data write verbs to members) — the recorded value is back on this client.` |
| No active session | `No active session — a write reports into the session's record, so it needs one …` |

## Self-check (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| The local-scene port | new, with a null-object default the adapter replaces | `CommandConsoleItemDataTests` drives the real console and the real kernel over a fake scene; the adapter's own half is verified by the acceptance batch (below) |
| The write grammar | new parser over the read verb's own target prefix | `ItemDataWriteParserTests` — 33 cases: 6 accepted shapes, 7 flag words, 2 keyword-casing cases, 17 refused shapes, the finite-number rule and the named unknown-property refusal |
| The value check | new pure class over the captured shape | `ItemDataWriteValuesTests` — every field kind, the liquid/liquid-id, the negative-value refusals |
| The report route | two existing `IItemControl` calls, chosen by the kernel's own world-rooted walk | the two `Set_On…` route cases assert the EVENTS each route raises (a carried fact vs a correction) and that the other route stayed silent — the row changes on both, so the row is not the discriminator — plus `Set_OnAContainedChildOfAWorldContainer_TakesTheWorldCorrectionRoute` for the chain walk |
| The value's source | computed against the local capture | `Set_ComputesTheValueAgainstTheLocalCapture_NotTheRecord` seeds a local value the row disagrees with and asserts the answer's "before" is the local one |
| The target grammar | the read parser exposes a prefix form; the read verb's own behaviour is unchanged | `ItemDataQueryParserTests` (25 cases) still green, and the write parser's target cases run through the same function |
| A member's write route | the report travels under `WireCommandKind.ItemDataWrite` so the host judges the VERB, not the state (S1b) | `ItemActionSyncTests`; end to end `CommandConsoleMemberItemDataTests.Set_OnAMembersConsole_IsReportedAndTheHostRecordsIt` (the host's own kernel row moves) |
| The host's rule for the verb | new flag `HostRulesOptions.AllowMemberItemDataWrite` (off), read at the admission seam through the Application-declared `IMemberCommandRules` port | `KernelCommandGatewayTests.ConsoleVerbWithTheHostRuleClosed_IsRefusedAndAnswered` (RED with the rule check disabled; its open-rule control stayed green, which is what makes the rule the difference) / `.ConsoleVerbWithTheHostRuleOpen_IsAdmitted`; the end-to-end pair `CommandAdmissionIntegrationTests.AConsoleWriteFromAMember_IsRefusedByTheHostRuleAndAnswered` (RED with the rule check disabled) / `.CommitsWhenTheHostRuleIsOpen` |
| The way back from a refusal | the seam answers this kind and `WireCommand.RefusedKind` names the judged report; the member's client settles the recorded state back (`ItemDataWriteSettlement`) and the console prints it | `CommandConsoleMemberItemDataTests.Set_OnAMembersConsole_WhenTheHostRuleIsClosed_IsRefusedAndTheValueGoesBack` (RED with the settle disabled: the local value stayed at the written one) and `KernelItemReportEligibilityTests.ConsoleWriteForAnotherMembersItem_IsAnsweredRatherThanIgnored` (RED with the answer narrowed to the native shape) |
| The refusal keeps its own family | a refused console write never becomes the pickup/block-drop rejection the adapter rolls back | `Set_OnAMembersConsole_WhenRefused_NeverBecameAPickupRollback` asserts the item control's rejection event stayed silent, and the member's local value went back rather than the item leaving its hands |
| The re-report window (found by the independent review, fixed in the same commit) | the console write is re-reported like the other member actions whose local half already happened, and a refusal closes the newest report OF THE KIND it names | `GuestCommandReconciliationTests.SwallowedConsoleWrite_IsReReportedAndTheHostCommitsIt` (RED with the kind absent from the re-reportable set: an empty window where `[ItemDataWrite]` was expected) and `.RefusalClosesOnlyTheKindItNames` (RED with the item-scoped pop restored: an empty window where `[ItemPickup]` was expected) |

## Acceptance (the rows a run must read)

One two-client session, host and guest both in a world:

| # | Scenario | Expected |
|---|---|---|
| 1 | The host holds an item it has used or moved once, and runs `/data set id <id> condition 0.5` | The answer names the item and `condition <old> -> 0.5`; `/data get id <id>` on BOTH peers reads 0.5 after the next snapshot |
| 2 | The host writes a component field (a battery's charge) and a liquid stack (`liquid water 500`) | Both peers' `/data get id <id>` print the written values; the container's fill follows on the guest's clone |
| 3 | The host writes a WORLD item (a dropped container it holds) | The guest's copy adopts the value (the correction route), and `/data get` on both peers agrees |
| 4 | `/data set …` on the guest's console, with the host's rule closed (its default) | The verb's own line, then the host's refusal line naming the rule, and the item reads back at its recorded value on both peers |
| 4b | The same write with a host whose rule is open | The host records the value, `/data get id <id>` on both peers reads it after the next snapshot, and no refusal line appears |
| 5 | The host runs `/data set @s slot <n> …` for a carried item the record has no row for (a fresh world's starting supplies) | One line naming why: the item joins the record on its first report — the record gap this stage did NOT close (see Limits) |
| 6 | `/data set id 999999 …`, `/data set @a slot 0 …` (two remote members), `/data set @s …` (no slot), `/data set id <id> component Radio gain 1` | One explicit line each: no such item, the body count with what to write instead, "a write names ONE item", the component it does have |
| 7 | `/save`, continue (restore) and a rejoin after row 1's write | The written value is still what both peers read |

## Limits, recorded rather than hidden

- **`merge` and `remove` are not implemented.** They need the additive and the entry-removal semantics over
  the same two property families that carry more than one value (`liquid`, `component`), and their answers
  have to name the delta. `/data set` is the verb that landed.
- **No cursor target yet.** The user asked for "the item nearest the mouse"; the port this stage adds has no
  cursor lookup, and the lookup itself has an open question that belongs with it: a world-generation ground
  item deliberately carries NO instance id (`ItemInstanceId`'s own rule: "Generation-time items never carry
  one — world-gen determinism covers them"), so the pointer is over an id-less item in the ordinary case, and
  what a write should do then (mint an id and hope the peers can bind it by position, or refuse) is a
  decision with its own evidence.
- **The record does not cover the host's own untouched carried items, and this stage did not close it.** The
  host's starting supplies get an id and a row on their first USE, move or drop (the same lazy path every
  native action takes), so a write to one of them is refused by name until then. Closing it means the host's
  client registering its carried inventory at the generation-finished edge the way a member's does — a
  CROSS-PLAYER change (those ids travel in the 1 Hz character snapshot, so members' clones, their remote
  inventory view and the divergence monitor all start seeing addressable rows for the host's items), which
  needs its own verification run rather than a ride on this verb. The read half's ticket carries the same gap
  from the read side.
- **A contained item's write is reported as that item's own carried fact**, which the receivers merge by id
  (the nested-container case is covered for the wire shape); a two-client run has not yet read it.
- **`condition 0` is written as asked.** The game destroys what reaches zero on the ITEM's own rule
  (`destroyAtZeroCondition`), so the answer may be followed by the item vanishing; the verb does not second-guess
  the data model. A negative condition is refused.
- **A slot MOVE is not a property write.** `slot` names an item in the target; moving one item to another
  body slot is a native gesture with its own report chain (`ItemSlotSync`), not a value this verb writes.
- **The rule gates the VERB, not the state.** A modified client can report the same item state under the
  native `ItemUpdateState` kind, which the host admits for a member's own item by design (the pre-release
  trust model; the validation ticket's acceptance row 6 says the same) — what `AllowMemberItemDataWrite`
  decides is whether an operator's verb runs, never whether a member can put a number into the record. Stating
  it here because the opposite reading is the natural one: the rule is a host's call over its own console
  surface, not a boundary a determined client meets. Questioning the STATE is S3
  (`todo/validation-and-source-checks.md`) — an item use must name an item the host records in a state that
  permits that use — and the client-side half that stage needs is the same one this stage built: something a
  client can do locally when the host says "that did not happen".
- **A member's write is applied before the host judges it, so a closed host rule is visible for one round
  trip.** The verb refuses nothing locally — only the host knows its own rule — so a member's `/data set`
  writes the value, reports it, and a refusal puts the recorded value back a moment later (S1b). The two
  console lines say both halves; the value never stays wrong. What that costs is the flicker: on a host
  whose rule is closed, the item briefly reads the value the member typed. The alternative (the member's
  console refusing what the host would accept, or promising what it may refuse) would put a rule this side
  does not hold in front of the operator. Removing it the honest way means telling the member the rule —
  `todo/host-rules-replication.md`, recorded at the user's direction when this stage closed, which also owns
  the mid-session change (an event, not just a world-entry fact) and the both-sides check the user asked for.
- **Permission is a HOST rule for the verb family**, `HostRules`' `AllowMemberItemDataWrite`, off by
  default; the host's own console is unaffected by it (its own writes never pass the admission seam). The
  permission is per COMMAND and the read verb's `Anyone` is what keeps a member's `/data get` open, so a
  member's `/help` does list the write grammar and typing it is refused by name when the host has not
  opened it — the refusal is the offer's only gate on the member's side.
- **A write needs an ACTIVE session.** Both report routes are guarded on one (`ItemService`), so without a
  session the local object would change and the record would not; the verb refuses by name instead
  (`No active session …`). A solo or offline world therefore has no item record for this verb to change, which
  is also why the permission reads as host-only there.
- **The port's registration order is load-bearing and unguarded.** The Runtime registers
  `UnavailableLocalItemAccess` and the adapter's composition replaces it; the plugin's composition runs the
  adapter last, so the real one wins — but a build whose order changed would silently write nowhere visible
  (the answers read as "not held"). A mis-wiring check is not part of this stage.

## Non-goals

- Not `give` or `spawn` — each is its own stage in the umbrella ticket.
- Not a raw memory editor: only the properties the capture carries are addressable, and a field the codec
  does not whitelist never left its process to be written.
- Not a new player-facing page: the surface is the console's own `/help`, generated from the registration.
