# Acceptance — `/data merge` and `/data remove` on a real session

- Date: 2026-10-18
- Ticket: `docs/backlog/todo/item-and-entity-data-commands.md`
- Batch: `.acceptance/20261017-n/run.ps1` (two clients: host + one sandboxed member)
- Delivered artifact: `0.1.0+b1cc399ceaaad6ff77d0c7b4379115c62b3f8c96` — the tree's own HEAD, confirmed by
  `verify-deploy.ps1` before the clients started, in all three runs
- Logs: `.acceptance/20261017-n/run1.log`, `run2.log`, `run3.log`. Every answer quoted below is the
  console's own line, read back through the driver's `console` action (`lastLine`), and **run 3** is the
  one that judged everything; runs 1 and 2 are kept because what they failed on is worth recording.

## Judged — the host side

Every line below is the answer to a real command against an item the host really holds
(`item-provide type=bandage,mode=create` handed it one, carried in slot 0).

| # | Command | The answer | Verdict |
|---|---|---|---|
| 1 | `/data get id <item>` | `condition 1`, `favourited no` | the state before any write |
| 2 | `/data merge id <item> condition 0.5 favourited yes` | `condition 1 -> 0.5; favourited no -> yes — written and reported as this client's own item state.` | **every named property landed**, each with its own before/after, in the order written |
| 3 | `/data get id <item>` | `condition 0.5`, `favourited yes`, `revision 4` | the record followed the write |
| 4 | `/data merge id <item> condition 0.9 liquid petrol 100` | `carries no 'petrol' — its liquids are: none (an item with no liquid container reads the same way).` | refused by name |
| 5 | `/data get id <item>` | `condition 0.5`, `favourited yes`, `revision 4` | **the refused merge changed nothing** |

Rows 4–5 together are the atomicity claim, observed: the later property cannot be applied and the earlier,
LEGAL one is not in the record afterwards. A commit inside the loop would have left `condition 0.9` in row 5.
The same five rows came out identically in all three runs.

One more host row, in every run:

- `/data remove id <item> condition` → `'condition' has no absent state to remove — it is a value with a
  range, so give it one with the set verb. /data remove accepts liquid and component. Usage: /data remove
  [<selector>] slot <n> | id <n> — one or more of: liquid <liquidId> | component <TypeName> <field>`. The
  bounded property is refused **by name**, the refusal says which properties the verb does accept, and the
  grammar line is **remove's own**.

## Judged — the member side (run 3)

| # | Command | The answer | Verdict |
|---|---|---|---|
| 6 | the host allows it: `/hostrules {"AllowMemberItemDataWrite":true}` then `/data merge id <item> condition 0.4` on the MEMBER | `condition 1 -> 0.4 — written here and reported to the host, which judges a member's write by its own rule; a refusal puts the old value back and says so in this console.` | a member's write lands, and the answer says its verdict is still the host's |
| 7 | `/data get id <item>` on the member | `condition 0.4`, `revision 3` | the member's own record followed it |
| 8 | the host announces the closed rule, then the member writes again | `Refused locally: the host's rule AllowMemberItemDataWrite is off, so a member's item-data write does not land (the host announces its rules to this client). Nothing was written here.` | **refused on the member's OWN client** |
| 9 | `/data get id <item>` on the member | `condition 0.4`, `revision 3` | the refused write changed nothing |

Rows 8–9 are the announced-rule interception, which no unit test drives: the member refuses before applying
because the host told it the rule is closed, rather than applying, reporting and being put back.

## Judged — the clear path, on items that really carry the property

The nine rows above ran on a bandage, which carries neither a liquid stack nor a component, so the clear
path itself needed an item that does. A waterbottle does:

| # | Command | The answer | Verdict |
|---|---|---|---|
| 10 | `/data get id <bottle>` | `condition 1`, `favourited no`, `liquid water 500 ml` | the container as it arrives, full |
| 11 | `/data remove id <bottle> liquid water` | `liquid water 500 ml -> 0 ml — written and reported as this client's own item state.` | the liquid goes to zero, and the answer names the value it moved from and to |
| 12 | `/data get id <bottle>` | `liquid water 0 ml`, `revision 4` | the record followed it, and the **container is still there** — removal empties a stack rather than taking the property off the item |

This repeated identically in every run that reached it (4, 5 and 6).

## Still owed

**The component half of the clear path.** The unit cases cover all six field kinds (`TryResetField` writes
each kind's own zero), but no run has reset a real component field, because no run could name an item that
carries one: `type=battery` is not an item id the game grants (`item-provide` answers `eval-error`), and the
probe meant to find one asked the battery-tagged census for a field its answer does not carry (see below).
Reaching it needs an item whose prefab really loads a `BatteryItem`, named by the id the game's own item
table uses — the census's `mode=tag:battery` already computes that list.

## What the runs cost, and the one mistake they all made

**Run 1 — the member never entered the world.** Its own state reading said why: `steamInit: false`,
`role: None`, `gateText: "Waiting for the host to load…"`. The batch had waited for a second `steam`
PROCESS and taken that for readiness, then created a lobby and handed the member an id it could not use.
**Fixed in the batch**: a preflight row that waits on the member's own `steamInit` reading and throws with
that sentence. It reads `true` in every run since.

**Run 2 — the batch did not state its own starting condition.** The member's first write came back refused
before the step that closes the rule had run, because the rule is PERSISTED in the host's config and run 1
had left it closed. The refusal was correct — it is the same interception row 8 judges — but step 6 could
not be judged. **Fixed in the batch**: step 6 now sets the rule ON itself and waits for the announcement.

**Runs 5 and 6 — the component probe read a field the answer does not carry.** The step meant to find a
battery-tagged item parsed the census answer for an `"ids"` array; the answer is a LISTING with different
field names, so the parse failed and the step reported *"the game names no item tagged battery"*. The game
may well name several. Run 6 then asked for `type=battery` directly and got `eval-error`, which is at least
an honest failure rather than a fabricated absence.

**These three are the same mistake, and it is the one worth carrying forward**: each read a PROXY for the
fact it needed — a process count for a transport, an assumed default for a persisted setting, a field name
assumed rather than read. A verification step that cannot tell "the fact is absent" from "I failed to read
it" produces a false verdict in whichever direction is convenient, and two of these three very nearly did.
The fix is structural rather than local: **a step that reports an absence must first have demonstrated that
it can see the thing it is looking for.**

