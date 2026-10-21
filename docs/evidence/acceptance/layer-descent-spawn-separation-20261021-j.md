# Acceptance record — Members do not arrive together when the group descends a layer

- Ticket: `layer-descent-spawn-separation` — verdict: **moved to `done/`** (all four staged rows pass; the
  status field carries the batch).
- Batch: `20261021-j` — this ticket (four sessions: a normal descent; the descent edge dropped on one
  member; a dead member at the descent; a member reconnecting during the descent).
- Commit: `7b9ddfe0` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+7b9ddfe01f55380394ce37295deda9f05db1cfdb` (`prelude-verify-deploy.txt`; `tools/verify-deploy.ps1`
  exit 0).
- Run: 2026-10-21 23:20 → 23:27 local · Host: physical machine · Guests: sandbox `<sandbox-guest>` and
  `<sandbox-alt>` (three clients).
- Dependencies: the preflight read `11 present`; the prelude's `session-environment (ensure-cuo) exit=0`
  and the deploy verification above.
- Artifacts: `20261021-j/` under the directory named by `acceptance-artifacts-dir` — the prelude, the
  session logs (`run.log`, `dead.log`, `reconnect.log`), the per-client scene probes (`a-*`, `b-*`,
  `c-*`, `r-*-read.json`), the generation probes (`a-*-gen.json`), the frames (`a-after-*.png`,
  `c-after-*.png`), the drop answers (`b-drop-{arm,off}.json`), the pre- and post-descent health reads
  (`c-guest-health-*.json`), the current body read (`z-guest-body.json`), the reconnect's guest log lines
  (`r-guest-reconnect-lines.txt`), and the log windows (`run-*-log.txt`, `dead-*-log.txt`).

## Session A — the normal descent (layer 0 → 1)

Before: host, guest and alt all read `depth:0, x:0, y:500.62` (`a-before-*-read.json`). The host's
`[LayerDescent]` edge at 23:20:50.875 told 2 members; each member ran its own generation and reported
`World ready — start playing (the descent's new layer was already generated)` at 23:20:59.30. The host's
gate read `Start gate expects 2 member(s) back from the layer descent` and then
`No confirmed members waiting — releasing the start gate immediately` at 23:20:59.293 — both members had
already reported InWorld before the host's own entry, so no InWorld handback was attempted. After:
host, guest and alt all read `depth:1, x:0, y:488.62` (`a-after-*-read.json`), and each
`a-after-*-gen.json` reads `layerIndex:1, inWorld:true`. No member logged a menu return and no probe read
`PreGen`.

## Session B — the descent edge dropped on the guest (layer 1 → 2)

`b-drop-arm.json` reads `armed:true, msg:LayerDescent`; `b-drop-off.json` reads `dropped:1` (exactly one
frame lost). The guest went `Leaving the world to the main menu`, then `World join received — starting a
run to follow`, `Received character restore (1 items)`, and at 23:24:20.055:
`[Character] the restore's saved position belongs to layer 1 while this client is in layer 2 — it is not
applied; the body stays at this layer's own spawn.` No `Character restore position applied at spawn` line
appears in the guest's window. The host armed the gate for the one out-of-world member
(`Start gate armed — waiting for 1 member(s)`), sent the saved character
(`Sent saved character data to <member> (1 items).`) and released the group
(`Start gate released — everyone is in the world.`) at 23:24:18.982. After: host, guest and alt all read
`depth:2, x:0, y:505.62` (`b-after-*-read.json`, `b-after-*-gen.json` layer 2).

## Session C — a dead member at the descent (layer 2 → 3)

The guest's own body was killed before the descent (`c-guest-health-before.json`: `alive:false`). It
followed the descent (its log carries `host descent accepted` and `World ready`), and the host's
next-level respawn revived it:
`[Respawn] revived <member> (inWorld=True, items=1, skillsReset=False).` and
`[Respawn] next-level respawn revived 1 player(s) (keepInventory=True, keepSkills=True).` at
23:25:01.776. The guest's restore arrived at 23:25:02.655 with `Respawn` set (no position), and
`c-guest-health-after.json` reads `alive:true, conscious:true`. After: host and alt read
`depth:3, x:0, y:499.62`; the guest reads `depth:3, x:4.0, y:487.62` and `z-guest-body.json` reads
`standing:true, animatorClip:ExperimentIdle, positionY:487.615` — it is alive, standing and in the new
layer, on the floor 12 units below the entry ledge the other two stand on.

## Session R — a member reconnecting during the descent (layer 3 → 4)

Before: host `(0, 499.62, depth 3)`, guest `(4.04, 487.62, depth 3)`. The host started a descent and the
guest was quit at +0.6 s (process gone at +6.3 s), relaunched in the sandbox and rejoined the lobby
(`active:true, inWorld:true` at +31.9 s). The reconnected client's log shows the ordering the fix turns
on: `Received character restore (1 items).` at 23:26:19.368, **before** `World join received — starting a
run to follow.` at 23:26:19.540; at apply time it reads
`the restore's saved position belongs to layer 3 while this client is in layer 4 — it is not applied; the
body stays at this layer's own spawn.` at 23:26:26.750, with no `Character restore position applied at
spawn` line. After: host `(0, 501.62, depth 4)`, guest `(0, 501.6, depth 4)`, alt `(0, 501.62, depth 4)`.

## The rows

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | After a normal descent the host, guest and alt read the same new-layer entry (x equal within 0.01, `abs(dy) <= 1` block), and all three layer indices advanced | machine | **pass** | `a-before-*-read.json` all `(0, 500.62, 0)`; `a-after-*-read.json` all `(0, 488.62, 1)`; `a-after-*-gen.json` layer 1; `run-a-*-log.txt` (`[LayerDescent]`, `World ready`, `Start gate …`). |
| 2 | With the `LayerDescent` edge dropped on one member, that member re-enters through the ordinary `WorldJoin` and lands at the new layer's entry, and its restore applies no previous-layer position | machine | **pass** | `b-drop-arm.json` `armed:true`; `b-drop-off.json` `dropped:1`; `run-b-guest-log.txt` layer-refusal line, no `position applied`; `b-after-*-read.json` all `(0, 505.62, 2)`; `run-b-host-log.txt` gate armed/released. |
| 3 | A member reconnecting during a descent lands at the new layer's entry, with the saved position refused at apply time (the restore arrives before the run-baseline checkpoint) | machine | **pass** | `r-before-*` and `r-after-*-read.json`: guest `(0, 501.6, 4)` vs host/alt `(0, 501.62, 4)`; `r-guest-reconnect-lines.txt`: restore before `World join`, the layer 3 → 4 refusal at 23:26:26.750, no `position applied`. |
| 4 | A dead member at the descent is revived into the new layer (alive/conscious, not left in the old layer's geometry) | machine | **pass** | `c-guest-health-before.json` `alive:false`; `dead-host-log.txt` `[Respawn] next-level respawn revived 1 player(s)`; `c-guest-health-after.json` `alive:true, conscious:true`; `c-after-*-read.json` all `depth:3` (guest `x:4.0, y:487.62`). |

## Residuals for the user

None: every row is machine-read. The dead member's 12-unit settle below the entry ledge is named in
Limits, not left as a judgement call.

## Limits

- **The follower's ordinary InWorld handback was not staged.** In session A both members reported InWorld
  before the host's own entry, so the host never sent the follower handback (`SendSavedCharacter` requires
  the host in world). The one-shot refusal is unit-tested (`DescentBodyPreservationTests`), and the
  previous ticket's batch covered the follower's carried items; this run does not re-prove the refusal.
- **The dead member's body settled 12 units below the entry ledge.** It is alive, conscious and standing
  on the floor under the ledge (`z-guest-body.json`), not at the other clients' `(0, 499.62)` point. The
  revive handback carries no position (by design); the settle is the native physics of the revived body,
  not a restore teleport. Whether the body should be held at the ledge is a separate shape.
- **The raw `Character restore position applied at spawn: (-1.5,506.6)` pre-fix line** is an observation
  from the sandbox client's `BepInEx\LogOutput.log` during batch `20261021-i`, not a tracked reading; this
  batch's tracked evidence is the post-fix layer-refusal line above.
- **The blocked-entry, carried-member and brand-new-mid-layer-joiner shapes are not staged** (the same
  Limits as the ticket): a blocked layer has no standable gap and is the game's own failure; a carried
  body is re-pinned by its carry driver; a new joiner has no saved position.
- **The slow-report residual remains**: a report received after the host's new baseline commit while its
  body is still in the old world carries the new layer's stamp with an old position, and the stamp cannot
  separate the two. Not measured.
- **A respawn handback re-sent before the member's next report applies twice** (the flag stays on the
  stored snapshot); the content is the same snapshot, so the result is the same character. Not measured.
- The batch's own driver/scene probes are gitignored artifacts cited by id; no machine path, account id
  or room id is recorded here (the log lines' peer ids are replaced by `<member>` / `<host>`).
