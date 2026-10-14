# Acceptance record — the item-data write verb (`/data set`)

- Ticket: `item-data-write-commands` — verdict: **moved to `done/`** (all eight acceptance rows pass).
- Batch: `20261014-a` — tickets `item-data-write-commands`, `validation-and-source-checks`,
  `online-ui-art-and-controls-overhaul`, `online-ui-layout-and-input-detail-pass`
- Commit: `16c0d603` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+16c0d6034766394af57fa88106a185f2e4f47fbf`
- Run: 2026-10-14 22:2x → 23:0x · Host: physical machine · Guest: `Steam1` sandbox
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `sandboxie`, `hotrepl`, `capture`, `input`, `logs`,
  `artifacts` (preflight exit 0)
- Artifacts: the `20261014-a` subdirectory of the directory named by `acceptance-artifacts-dir`

Item ids in the evidence are the session's own (`#1202911823811` emergencylight and `#1207206791107`
trashbag are the host's, `#5197989525` is the member's); the clients' Steam ids appear in the captured
console lines but are not repeated here.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The host holds an item it has used or moved once and writes `condition 0.5` | machine | pass | `n2-host-write-condition.json` — `#1202911823811 emergencylight: condition 1 -> 0.5 — written and reported as this client's own item state`; `n3-host-get.json` and `n4-guest-get.json` — BOTH peers read `condition 0.5` at `revision 4` |
| 2 | The host writes a component field and a liquid stack | machine | pass | `o1-host-component.json` — `component CustomItemBehaviour state 0 -> 7`; `o2-host-liquid.json` — `liquid water 500 ml -> 500 ml`; `o3`/`o4` read the bottle on both peers as `liquid water 500 ml`; `o5`/`o6` read the light on both peers as `component CustomItemBehaviour: int state=7` |
| 3 | The host writes a WORLD item (a container it dropped) | machine | pass | `q1-host-drop-trashbag.json` (the native drop that made it world content); `q2-host-write-world.json`; `q3`/`q4` — both peers read `#1207206791107 trashbag [world (4.84, 501.028)] revision 5`, `condition 0.5` |
| 4 | A member's console write with the host's rule CLOSED (its default) | machine | pass | `h1-host-rule-closed.json` — `/hostrules {"AllowMemberItemDataWrite":false}` → `Updated 1 host rule(s).`; `h2-guest-write-refused.json` — the verb's own line, `condition 1 -> 0.5 … reported to the host, which judges a member's write by its own rule`; `h3-host-rule-refusal.txt` — the seam's line, `(answered): UpdateItemStateCommand … HostRuleDenied: the host's rule does not open the item-data write verbs to members`; `h4-guest-settle-lines.txt` — awaits the verdict → refused → `[ItemWrite] the host refused the console write to #5197989525 … the recorded state is back on the local object (condition 1)`; `h5-guest-console-history.json` — the operator reads the refusal line on its own console; `h6`/`h7` — both peers read `condition 1`, `revision 1` |
| 4b | The same write with the host's rule OPEN | machine | pass | `i1-host-rule-open.json` — `Updated 1 host rule(s).`; `i2-guest-write-accepted.json`; `i5-host-accepted-lines.txt` — ZERO admission-refusal lines in the window; `i6-guest-accepted-lines.txt` — `converged on its first report`; `i3`/`i4` — both peers read `revision 2`, `condition 0.25`; `i7-host-row-accepted.json` — the host's own kernel row: `condition 0.25`, `Carried`, owner = the member |
| 5 | The host writes a slot the record has no row for | machine | pass | The fixture: `r1-host-inject-unregistered.json` (a geigercounter placed in slot 1 without the pickup report), `r3-host-body-after-inject.json` (the body census shows it carried in slot 1), `r4-host-record-after-inject.json` (the record lists slots 0 and 2 only). The row: `r5-host-slot1-gap.json` — `No item is recorded for … in slot 1 yet — a carried item joins the record on the holding client's first report of it (a use, a move or a drop).` |
| 6 | Four refused targets | machine | pass | `p1-host-unknown-id.json` — `No item #999999 is recorded in this session.`; `p2-host-all-members.json` — the `@a slot 0` target answers `That item is carried by … — a write is the holder's own act, and no route edits another player's carried item yet.`; `p3-host-no-slot.json` — `That target names every item the body carries — a write names ONE item: add 'slot <n>', or use 'id <n>'.`; `p4-host-wrong-component.json` — `#1202911823811 has no component 'Radio' — it carries: CustomItemBehaviour.` |
| 7 | `/save`, continue (restore) and a rejoin after the written values | machine | pass | `s1-host-save.json` (queued), `s2-host-save-result.json` (`cut written … at revision 721`); the host left the world, restored it through the game's own Continue entry, and the guest rejoined the same lobby (`t1-guest-rejoin.json`); after the restore BOTH peers read the written values — `t4`/`t5`: `#1211501758403 waterbottle … liquid water 500 ml`; `t6`/`t7`: `#1202911823811 … condition 0.5`, `component CustomItemBehaviour: int state=7`; `t3-host-member-value.json`/`t3-guest-member-value.json`: the member's own row still reads `condition 0.25` |

## Residuals for the user

None. Every row of this ticket is a state read the run produced itself.

## Limits

- The rows were read in one two-client session with the host as the writer and one member as the other
  side. The row set is the ticket's own; nothing here says anything about a third peer.
- Row 2's "the container's fill follows on the guest's clone" half was read as the record's own values on
  both peers (`/data get`); this run did not separately photograph a clone's rendering.
- Row 5's fixture is a declared setup substitution (a local probe placed the item the way the game's carry
  does, deliberately without the reporting call site); the state it produces is the one the row names, and
  the artifact set shows both halves — the item is carried, the record has no row for it.
