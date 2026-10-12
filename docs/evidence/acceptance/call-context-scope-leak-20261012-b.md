# Acceptance record — A game-side throw inside a scope-entering patched frame latches CallContext off

- Ticket: `call-context-scope-leak` — verdict: **moved to `done/`**
- Batch: `20261012-b` — tickets: `call-context-scope-leak`, `mod-item-with-no-declared-tags-throws`
  (plan: `20261012-b-scope.md`)
- Commit: `ab246540` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+ab2465409c69fe7a88a03a789def56506e34980d` (`b4-verify-deploy.txt`: "Deployment matches this tree's
  build output.")
- Run: 2026-10-12 22:33:55 → 22:38:18, read from each client's own log's first and last line
  (`i6-run-window.txt`) · Host: physical machine, launched through Steam · Guest: primary sandbox
- Dependencies: the eleven ids `tools/acceptance/preflight.ps1` reports present — its own last line reads
  `RESULT: OK - a full two-client run is possible` (`b0-preflight.txt`) — and `session-environment.ps1 -Mode
  status` reported the CUO tree active with no game process before anything started (`b1-tree-status.txt`)
- Artifacts: the batch directory under the local artifact area, cited by file name only

## What the rows read

The probe is the committed `tools/acceptance/recipes/call-context-scope.cs`, run once per client in the
same world. Its `break` mode nulls the hand item's derived tag array (`ItemInfo.actualTags`, the private
field the game's own table pass fills) through reflection — the state a tagless declaration reached by
declaring no tags, where `Body.HandleVisuals` asks `HasTag("gun")` of the hand slot once per frame and the
game's own `Enumerable.Contains` throws inside CUO's scope around that method. `restore` puts the array
back through the game's own `ItemInfo.SetTags()`. Both clients carried the same hand item
(`emergencylight`, tags `belttool,backflip`), and the mark for each client was taken before its own first
`read` (host 22:35:47.912, guest 22:36:03.509).

The sequence per client, in order: `read` → `break` → `read` → `pant-sound kind=bark` → `restore` → `read`.
Every one answered `ok`:

| Step | Host artifact | Guest artifact | Reading |
|---|---|---|---|
| `read` before | `d1-host-read-before.json` | `e1-guest-read-before.json` | `depth 0`, `current LocalAction` |
| `break` | `d2-host-break.json` | `e2-guest-break.json` | `emergencylight`, `slot 0`, array → null, `applied true` |
| `read` after the break | `d3-host-read-after-break.json` | `e3-guest-read-after-break.json` | `depth 0` |
| a later scope-entering path | `d4-host-pant-bark.json` | `e4-guest-pant-bark.json` | `kind bark`, `applied true` |
| `restore` | `d5-host-restore.json` | `e5-guest-restore.json` | null → array, `applied true` |
| `read` after the restore | `d6-host-read-after-restore.json` | `e6-guest-read-after-restore.json` | `depth 0` |

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A probe makes a game-side frame throw while its CUO scope is open, and the process keeps working: later `CallContext.Enter` calls still succeed and no depth-exceeded line appears | `machine` | **pass** | The window from each client's own mark holds the game's own throw: host 267 `ArgumentNullException` lines spanning 22:35:49.142 → 22:35:50.995, guest 384 spanning 22:36:04.730 → 22:36:06.496 (`i4-injection-window-span.txt` — a count and a span, not two counts of one thing: the `[Unity:Exception]` marker and the `ItemInfo.HasTag` stack frame are part of those same lines). **Zero** `depth 16 exceeded` on either. The later scope-entering path answered `applied true` on both (`d4`/`e4`), and both `read`s inside the injected window report `depth 0` (`d3`/`e3`) |
| 2 | The same probe names the frame that leaked the scope, so the fix is aimed at a measured site rather than a candidate | `machine` | **pass** | The one recovery line per client names the origin: `[CallContext] frame boundary: 1 leaked scope(s) released: CharacterBurp x1` — host 22:35:49.179, guest 22:36:04.760 (`d7-host-log-window.txt`, `e7-guest-log-window.txt`). The logged stack beside it names the frame the ticket could only read before: `Body.DMD<Body::HandleVisuals>` |
| 3 | A legitimate deep stack (2–3 nested scopes) still classifies correctly — the recovery does not fire, and `Current` / `IsWithin` answer what they answered before | `machine` | **pass** | Whole-run census per client — each rolling log read from byte 0, so the window is the whole process life: exactly ONE `[CallContext]` line each (`g10-host-callcontext-whole-run.txt`, `g11-guest-callcontext-whole-run.txt`), i.e. the recovery fired only in the frame the probe injected and never on the nesting ordinary play produces every frame. The one scope-entering family this run drove directly is `PantSound.Bark`, which answered `applied true` on both clients (`d4`/`e4`); the rest of the 26-site family was not driven in this session and is not claimed. `CallContextRecoveryTests`' four cases, including `LegitimateNesting_IsUntouched`, are green in the gate run archived as `i5-gate-run.txt` |
| 4 | After a recovered leak the client stays usable on both peers for the rest of the session, with the recovery reported once rather than per frame | `machine` | **pass** | ONE recovery line per client for the whole episode, not per frame (census above). After both clients had leaked and recovered, the session carried a full cross-peer item handover — the tagless fixture created on the host, taken by the guest, dropped, and taken back on the host (`f4`, `g1`, `g4`, `g5`) — and the final reads are `depth 0` on both (`g8`, `g9`) with both endpoints still `active true, inWorld true` in the same lobby (`g6`, `g7`) |

## Residuals for the user

None. Every row is a machine read this run produced, and no row asks for a person's judgment.

## Limits

- **The leak is injected, not encountered.** The probe creates the game-side throw through the game's own
  frame path (`HandleVisuals` asking `HasTag` of a hand item whose array is absent); the run does not claim
  this shape occurs unprompted. What it proves is the behaviour under the injection, on both peers.
- **One world, two clients, one session.** Row 4's "the rest of the session" is this session's remainder —
  the cross-peer handover above, after the last recovery — not a second session and not a long soak; the
  whole run is the 4m23s of `i6-run-window.txt`. A leak that only latches under a rarer frame shape is not
  excluded by this run.
- **Row 3 is judged by the recovery's silence, not by reading `Current` / `IsWithin` from inside a nested
  scope.** A probe that opened a scope and read its own chain was not written; what the run shows is that
  the nesting ordinary play produces (present on every frame of this window) never triggered the recovery,
  plus the gate's own four behavioural cases. The specific families the run drove by name are named above.
- **The two remaining limits of the fix are unchanged and are the ticket's own**: a leak mis-classifies for
  the remainder of the frame it happened in, and the display-body window a throwing original can strand is
  not released by this boundary. Neither is exercised here.
- **The window's throw count is the game's own, not CUO's**: the 267 host / 384 guest `ArgumentNullException`
  lines are `Enumerable.Contains(null, tag)`, i.e. the injection working; they are evidence for rows 1–2 and
  are named as the injection, never as a product defect.

## Closing state

Both clients quit through the game, and no game process was left behind. The fixture DLL this batch placed
in the install's `plugins` tree was removed and the removal re-read. The batch's own log helper, probe
output and log windows stayed in the batch directory; the reusable probe half already lives under
`tools/acceptance/recipes/` (`call-context-scope.cs`, from the fixing cycle) and the batch's new
`item-has-tag.cs` was promoted there by this batch.
