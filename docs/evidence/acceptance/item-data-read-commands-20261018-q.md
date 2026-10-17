# Acceptance record — the item data read verb, on the SNBT renderer

- Ticket: `item-data-read-commands` — verdict: **stays in `review/`**: rows 1-5 were judged here, row 5's
  two-member ambiguity sub-case and row 6 (an item applied to a limb) remain, so the ticket is not closed.
- Batch: `20261018-q` — tickets `give-item-to-a-player-command`, `snbt-state-language`,
  `item-and-entity-data-commands`.
- Commit: `30dbb252` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+30dbb2525ccd07d72ddd810c1781d068de2959a2` (verified by `tools/verify-deploy.ps1`, exit 0 before the
  clients started).
- Run: 2026-10-17 23:42:24 → 23:50:37 by the clients' own log clock · Host: physical machine · Member:
  `Steam1` sandbox.
- Dependencies: `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (both endpoints), `input`, `logs`,
  `artifacts`.
- Artifacts: the `g0*`, `g1*`, `g2*`, `d1*`, `r*` and `s*` readings in the directory named by
  `acceptance-artifacts-dir`; Steam IDs are written `<host>`/`<member>`.

| # | Row (the ticket's own) | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The member runs `/data get` once the world is entered: its carried items, starting supplies included, one line each in slot order with instance id, definition, slot and condition | machine | **pass** | `g0-guest-body.json`: `Items recorded for <member> (1):` then `#5197989525 emergencylight [slot 3] {Condition:1f,Favourited:false,Liquids:[],Components:{CustomItemBehaviour:{state:0}}}` |
| 2 | The host runs `/data get` and its own carried items are listed (the ticket's pre-registration wording — "an item the host has never touched is absent" — is superseded by the landed host registration, which is the behaviour read here) | machine | **pass** | `g0-host-body.json`: `Items recorded for <host> (1): #1190026921923 emergencylight [slot 3] {Condition:1f,…}` — the host had not used, moved or dropped that item |
| 3 | The host reads an item whose state it knows (a partly used container with liquid, or an item with a charge field): every property printed matches the item — condition, favourited, each liquid stack with its millilitres, each component field with its kind | machine | **pass** | `d1-host-read.json`: `#1207206791107 waterbottle [carried by <host>, slot 0] revision 1` / `{Condition:0.8f,Favourited:false,Liquids:[{LiquidId:"water",Amount:300f},{LiquidId:"blood",Amount:100f}],Components:{}}`; `d10-host-read.json` reads `Components:{CustomItemBehaviour:{state:3}}` |
| 4 | The member runs `/data get id <the same id>`: the same rows the host's read printed | machine | **pass** | `g1-guest-read.json` carries the same answer text as the host's read for `#1194321889219`; `d1-guest-amount.json` reads the host's `Liquids[1].Amount` as `100f`; `d4-guest-read.json` reads the merged bandage |
| 5a | `/data get id 999999`, `/data get slot <empty>`, `/data get @x`: one explicit line each, no silent no-op | machine | **pass** | `r1-unknown-id.json`: `No item #999999 is recorded in this session.`; `r2-empty-slot.json`: `No item is carried by <host> in slot 5.`; `r3-bad-selector.json`: `No players match selector '@x'.` |
| 5b | `/data get @a slot 0` in a session with two members BESIDES the caller: the ambiguity answer with what to write instead | machine | **unproven** | the session had one member; the answer's wording is unit-pinned, but this run cannot stage the match |
| 5c | `/data set …`: the retired write verb's answer | machine | **pass** | `r4-old-verb.json`: `Unknown /data verb 'set'. The verbs are /data get | /data merge | /data modify | /data remove.` with the new usage line |
| 6 | An item that IS recorded and then applied to a limb (a splint or tourniquet): listed by the body form as `limb N`, and read by its id as `applied to <owner>, limb N` | machine | **unproven** | not staged — the native splint application needs an injured limb and the game's own use gesture; this batch did not attempt it |

## What the run adds beyond the rows

- **The rendered form is the SNBT document**, not the old property lines: a single item's answer is the
  header line plus `{Condition:…,Favourited:…,Liquids:[…],Components:{…}}`, and a path answer is
  `#<id> <definition>: <path> = <value>` (`d3-guest-amount.json`, `d10-guest-component.json`).
- **An empty body is answerable on both roles**: the host and the member each list one item at world entry,
  so the "whose record is this" question is answered by the owner named in the header.

## Residuals for the user

None.

## Limits

- Row 5b and row 6 are named as `unproven`; the ticket stays in `review/` on them.
- The `4101` target case the ticket's row 5 also lists was not driven; the usage line's target grammar is
  covered by the retired-verb answer, but this record does not claim that case.
- One member, no third client; no reconnect.
