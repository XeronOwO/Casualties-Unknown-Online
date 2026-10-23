# Acceptance record — the `/gamerule` host-rule command

- Ticket: `gamerule-host-rule-command` — verdict: **pass**, moved to `done/`.
- Batch: `20261023-j` — this ticket's seven rows and
  `give-gesture-own-ui-and-center-use`'s row 7 (record
  `give-gesture-own-ui-and-center-use-20261023-j.md`).
- Commit: `a2ba208d` · Deployed artifact: `CasualtiesUnknownOnline.dll`, `ProductVersion`
  `0.1.0+a2ba208dd0a275130175161090467de5e7a4258c` — `tools/verify-deploy.ps1` exit 0 before the
  clients started (`prelude-verify-deploy.txt`).
- Run: 2026-10-24 00:20 → 00:24, this session's clock · Host: physical machine · Guest: first sandbox.
- Dependencies: `preflight.ps1` (`RESULT: OK - a full two-client run is possible`),
  `session-environment.ps1 -Mode ensure-cuo` (`active=cuo`, `game-running=false`), the in-process driver on
  both endpoints, and the committed recipes named below. No cursor or keyboard API was used in this batch.
- Artifacts: `j-r1-query.json`, `j-r2-set.json`, `j-r2-query.json`, `j-r2-host-rules.json`,
  `j-r2-guest-rules.json`, `j-r3-invalid.json`, `j-r3-query.json`, `j-r4-unknown.json`, `j-r5-guest.json`,
  `j-r5-guest-rules.json`, `j-r6-range.json`, `j-r6-set.json`, `j-r6-query.json`, `j-r6-guest-rules.json`,
  `j-r7-restore-access.json`, `j-r7-restore-weight.json`, `j-r7-guest-rules.json`, `j-b1-item.json`,
  `j-b2-open.json`, `j-b3-release.json`, `j-b4-find.json`, `m-give.txt` in the directory named by
  `acceptance-artifacts-dir`; players are written `<host>`/`<member>`. The pre-run values were
  `remote_backpack_access=allow` and `piggyback_weight_multiplier=0.65` (this install's config), and row 7
  restored exactly those.

| # | Row | Verdict | Evidence |
|---|---|---|---|
| 1 | `/gamerule remote_backpack_access` on the host queries the value in force | **pass** | `j-r1-query.json` `lastLine`: `Game rule remote_backpack_access is currently set to: allow.` — the value the install carried before the run |
| 2 | `/gamerule remote_backpack_access unconscious_or_dead` sets it; the answer names the new value; a second query reads it; the member's announced rule becomes `unconscious_or_dead` on the next pump | **pass** | `j-r2-set.json` `Game rule remote_backpack_access is now set to: unconscious_or_dead.`; `j-r2-query.json` `… is currently set to: unconscious_or_dead.`; `j-r2-host-rules.json` `remoteBackpackAccess=unconscious_or_dead, announced=false`; `j-r2-guest-rules.json` `remoteBackpackAccess=unconscious_or_dead, announced=true` — the member's own client, on the change edge |
| 3 | `/gamerule remote_backpack_access maybe` is refused, names `allow`, `unconscious_or_dead` and `deny`, and the value stays | **pass** | `j-r3-invalid.json` `Game rule remote_backpack_access: 'maybe' is not one of allow, unconscious_or_dead, deny.`; `j-r3-query.json` still `unconscious_or_dead` |
| 4 | `/gamerule nope` is refused and prints the known rule names | **pass** | `j-r4-unknown.json` `Unknown game rule 'nope'. Known rules: ` + the 16 names, alphabetically (`allow_late_join` … `widen_run_settings`) |
| 5 | A member's `/gamerule remote_backpack_access deny` answers the host-only refusal and the member's announced set is unchanged | **pass** | `j-r5-guest.json`: `applied=false`, `last.lastLine` `Permission denied: this command is host-only.` (the driver reports `console-refused`, which is the gate); `j-r5-guest-rules.json` still `unconscious_or_dead` |
| 6 | `/gamerule piggyback_weight_multiplier 5` is refused with the 0–3 range; `… 0.5` sets it and the member reads 0.5 | **pass** | `j-r6-range.json` `'5' is not a number between 0 and 3.`; `j-r6-set.json` `… is now set to: 0.5.`; `j-r6-query.json` `0.5`; `j-r6-guest-rules.json` `piggybackWeightMultiplier=0.5` |
| 7 | The rules are restored (`allow`, `0.65`) and the member reads both back | **pass** | `j-r7-restore-access.json` `… is now set to: allow.`; `j-r7-restore-weight.json` `… is now set to: 0.65.`; `j-r7-guest-rules.json` `remoteBackpackAccess=allow, piggybackWeightMultiplier=0.65, announced=true` |

## Limits

- **One repetition per row.** Each invocation was driven once; a race between the write, the monitor and
  the announcement is not excluded by one repetition.
- **The member's value is the one its own client acts on.** `host-rules-read` reads
  `HostRulesService.Current` and the announced-set flag on the member, which is what the member's screens
  and checks read; it is not a read of the host's config file.
- **The refusal line is a console line, not an error line.** The console adds a handler's return as a
  success line (its colours), so a refusal reads green; the text names the refusal. Filed as
  `todo/console-command-refusals-render-green.md`; the ticket's Limits record it.
- **No completion.** `/gamerule ` suggests nothing; rows 3 and 4's refusals are the discovery surface.
- The guest's row-5 answer is the driver's `console-refused` wrapper with the console line inside
  `last.lastLine`; the driver treats a `false` `TryExecute` as a refusal, which is exactly the host-only
  gate.
