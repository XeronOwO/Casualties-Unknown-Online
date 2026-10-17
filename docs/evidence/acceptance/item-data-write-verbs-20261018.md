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

## Still owed

`remove` against a **real container** (a liquid going to zero millilitres) and a **real component** (a field
returning to its kind's zero), across a save/restore round trip. The two verbs were judged on a bandage,
which has neither a liquid stack nor a component, so the clear path itself is unobserved at runtime — the
unit cases cover it but the record should not claim more than a run showed. Reaching it needs an item that
carries both, which is a `type=` away in the same batch.

## What the first two runs cost, and what each one taught

**Run 1 — the member never entered the world.** Its own state reading said why: `steamInit: false`,
`role: None`, `gateText: "Waiting for the host to load…"`. The batch had waited for a second `steam`
PROCESS and taken that for readiness, then created a lobby and handed the member an id it could not use.
Every member-side row was lost to a failure the batch could have refused by name. **Fixed in the batch**: a
preflight row that waits on the member's own `steamInit` reading and throws with that sentence. It reads
`true` in runs 2 and 3, which is the row doing its job.

**Run 2 — the batch did not state its own starting condition.** The member's first write came back refused
before the step that closes the rule had run, because the rule is PERSISTED in the host's BepInEx config and
run 1 had left it closed. The refusal was correct — it was the same interception row 8 judges — but step 6
could not be judged at all. **Fixed in the batch**: step 6 now sets the rule ON itself and waits for the
announcement, and 3b/row 8 turns it off again. That the rule survives a restart is itself a reading this
batch now demonstrates instead of tripping over.

Neither failure was a defect in the verbs. Both were the batch assuming something it could have read, which
is the same mistake in two places and is why the preflight row and the explicit starting condition both
live in the script rather than in this note.
