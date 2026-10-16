# Acceptance — `/data merge` and `/data remove` on a real session

- Date: 2026-10-18 (run 1)
- Ticket: `docs/backlog/todo/item-and-entity-data-commands.md`
- Batch: `.acceptance/20261017-n/run.ps1` (two clients: host + one sandboxed member)
- Delivered artifact: `0.1.0+b1cc399ceaaad6ff77d0c7b4379115c62b3f8c96` — the tree's own HEAD, confirmed by
  `verify-deploy.ps1` before the clients started
- Log: `.acceptance/20261017-n/run1.log` (the answers quoted below are the console's own lines, read back
  through the driver's `console` action)

## Judged — the host side, five rows

Every line below is the console's own answer to a real command against an item the host really holds
(`item-provide type=bandage,mode=create` gave it instance `1181436987331`, carried in slot 0).

| # | Command | The answer | Verdict |
|---|---|---|---|
| 1 | `/data get id <item>` | `condition 1`, `favourited no` | the state before any write |
| 2 | `/data merge id <item> condition 0.5 favourited yes` | `condition 1 -> 0.5; favourited no -> yes — written and reported as this client's own item state.` | **every named property landed**, and the answer names each with its own before/after |
| 3 | `/data get id <item>` | `condition 0.5`, `favourited yes`, `revision 4` | the record followed the write |
| 4 | `/data merge id <item> condition 0.9 liquid petrol 100` | `carries no 'petrol' — its liquids are: none (an item with no liquid container reads the same way).` | refused by name |
| 5 | `/data get id <item>` | `condition 0.5`, `favourited yes`, `revision 4` | **the refused merge changed nothing** — the first property did not land on its own |

Row 4–5 together are the atomicity claim, observed: the later property cannot be applied, and the earlier
one — which was legal — is not in the record afterwards. A commit inside the loop would have left
`condition 0.9` in row 5.

Two more rows from the same run:

- `/data remove id <item> condition` → `'condition' has no absent state to remove — it is a value with a
  range, so give it one with the set verb. /data remove accepts liquid and component. Usage: /data remove
  [<selector>] slot <n> | id <n> — one or more of: liquid <liquidId> | component <TypeName> <field>`. The
  bounded property is refused **by name**, the refusal says which properties the verb does accept, and the
  grammar line it prints is **remove's own** — which is what the third commit of this ticket fixed.
- The merge answer above carries both properties with their own before/after in the order they were written,
  so `WroteMany` is what an operator reads on a multi-property write.

## Not judged — the member side, and why

The member never entered the world, so nothing about a member's write was observed. Its own state reading
says why, and it is the environment rather than the mod:

```
"transport": "Steam",  "steamInit": false,  "role": "None",  "lobby": "0",
"gateText": "Waiting for the host to load…"
```

`join-lobby` answered `join-not-entered` ("the join was applied but the client did not settle into lobby
…"), and `steamInit` was already **false** at that point — the sandboxed Steam the batch starts for the
member was not up, so the Steam transport the lobby id travels over was never available. The host side of
the same batch used the same lobby id successfully.

**Residual for the next run** (the same batch script reaches all three, they are simply later in it):

1. A member's own write while the host allows it: `/data merge` on an item the member holds, then
   `/data get` on the member to read the new values back.
2. The same write refused **on the member's own client** after the host announces
   `AllowMemberItemDataWrite=false` — the announced-rule interception, which no unit test drives.
3. `remove` on a real container (a liquid going to zero millilitres) and on a real component (a field
   returning to its kind's zero), across a save/restore round trip.

What the member needs before those rows can be judged: the sandbox's Steam running and logged in when the
batch starts. The batch already waits for a second `steam` process, but it does not verify that the member's
own Steam transport initialised — a preflight row that reads `steamInit` on the member endpoint before
`create-lobby` would turn this failure into a named refusal instead of a join that silently does not settle.
