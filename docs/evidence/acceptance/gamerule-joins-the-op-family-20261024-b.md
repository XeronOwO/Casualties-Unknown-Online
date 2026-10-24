# Acceptance record — `/gamerule` joins the op family

- Ticket: `gamerule-joins-the-op-family` — verdict: **pass**, moved to `done/`.
- Batch: `20261024-b` — three clients (host, granted member, plain member), this ticket's seven rows.
- Commit: `f9052945` · Deployed artifact: `CasualtiesUnknownOnline.dll`, `ProductVersion`
  `0.1.0+f90529458c42804b31cb7e80cc0b37c0907b6b0e` — `tools/verify-deploy.ps1` exit 0 before the clients
  started (`prelude-verify-deploy.txt`), and the preflight re-read the same version and the same HEAD
  (`preflight.log`).
- Run: 2026-10-24 01:16 → 01:19, this session's clock · Host: physical machine · Granted member: first
  sandbox · Plain member: second sandbox.
- Dependencies: `session-environment.ps1 -Mode ensure-cuo` (`active=cuo`, `game-running=false`), the
  in-process driver on all three endpoints, and the committed `host-rules-read`/`console-read` recipes named
  below. `preflight.ps1` reported `RESULT: OK - a full two-client run is possible; the alternate third
  client is configured` — run at the END of the batch rather than before it (process deviation: the
  start-of-run checks were the prelude's `session-environment` and `verify-deploy`; the preflight would
  have caught the same dependencies and should run first next time).
- Artifacts: `r1-*`, `r2-*`, `r3-*`, `r4-*`, `r5-*`, `r5b-*`, `r6-*`, `r7-*`, `quit-*.json`,
  `prelude*.json`, `prelude.log`, `deploy.log`, `verify.log`, `preflight.log` in the directory named by
  `acceptance-artifacts-dir`; players are written `<host>`/`<member>`/`<bystander>`. The run's starting
  values were `remote_backpack_access=allow`, `allow_member_cheat_verbs=true`,
  `piggyback_weight_multiplier=0.65` (this install's config, left by earlier batches); row 5 turned the
  switch off and back on, and row 6 returned `remote_backpack_access` to `allow`, so the install ends
  where it started.
- The member's console answer is read from `console-read` (`CommandConsoleService.Lines`), not from the
  console call's `lastLine`: a starting-supplies notification landed after the answer in `r3-guest-set.json`
  and made `lastLine` the notification (the console history carries both lines in order).

| # | Row | Verdict | Evidence |
|---|---|---|---|
| 1 | The host's own `/gamerule` set/query still works exactly as in the accepted batch | **pass** | `r1-host-query.json`: `Game rule remote_backpack_access is currently set to: allow.`; `r1-host-set.json`: `… is now set to: unconscious_or_dead.`; `r1-host-query2.json`: `… is currently set to: unconscious_or_dead.`; `r1-host-restore.json`: `… is now set to: allow.` |
| 2 | The plain member's `/gamerule …` is refused by the gate, naming the switch/roster ground, and nothing changes on any client | **pass** | `r2-alt-gamerule.json`: `Refused locally: the host's cheat roster (announced to this client) does not name you, so /gamerule cannot land. The host grants it with /op. Nothing was done here.` (this install had the switch on, so the ROSTER is the ground here; the switch ground is row 5a); `r2-host-query.json` still `allow`; `r2-host-rules.json`/`r2-guest-rules.json`/`r2-alt-rules.json` all `remote=allow, announced=true` for both members |
| 3 | The granted member's `/gamerule remote_backpack_access unconscious_or_dead` answers on its own console with the host's answer; the host's config entry holds the value; the plain member's announced set reads it on the next pump | **pass** | `r3-host-op.json`: `Granted the cheat verbs to <member> …`; `r3-guest-console.json` line 3: `Game rule remote_backpack_access is now set to: unconscious_or_dead.`; `r3-host-query.json`: `… is currently set to: unconscious_or_dead.`; `r3-alt-rules.json`: `remoteBackpackAccess=unconscious_or_dead, announced=true` (the plain member's own client, on the change edge); host log `[Rules] <member> set remote_backpack_access = unconscious_or_dead; the entry now holds unconscious_or_dead.`; member log `[Rules] <member> asked the host to set remote_backpack_access = unconscious_or_dead; the answer will print where the verb was typed.` |
| 4 | The granted member's invalid value is refused locally, no request is sent, and the host's value is unchanged | **pass** | `r4-guest-console.json`: `Game rule remote_backpack_access: 'maybe' is not one of allow, unconscious_or_dead, deny.`; `r4-host-query.json` still `unconscious_or_dead`; the host log read since the row's mark (`m-r4.txt`) printed `NO MATCHING LINES` for `[Rules]` — no request reached the host |
| 5 | With the switch off (or the roster revoked) the same granted member is refused and the rule stays | **pass** | 5a: `r5-host-switch-off.json` `… is now set to: false.`; `r5-guest-console.json`: `Refused locally: the host's cheat switch (AllowMemberCheatVerbs) is off …`; `r5-host-query.json` still `unconscious_or_dead`. 5b: `r5-host-deop.json` `Revoked the cheat verbs from <member> …`; `r5b-guest-console.json`: `Refused locally: the host's cheat roster (announced to this client) does not name you …`; `r5b-host-query.json` still `unconscious_or_dead` |
| 6 | After a granted member's change, the member's own config file is unchanged and the host's holds the new value | **pass** | `r6-guest-console.json`: `Game rule remote_backpack_access is now set to: allow.`; the member's `CasualtiesUnknownOnline.cfg` hashed `SHA256` identically before and after the write AND kept its `LastWriteTimeUtc` (`guest-config unchanged=True mtime-same=True`), still reading `RemoteBackpackAccess = allow`; the host's own file reads `RemoteBackpackAccess = allow`; `r6-host-query.json` agrees |
| 7 | The rest of the cheat family's refusals are unchanged (one `/give` or `/heal` probe as the control) | **pass** | `r7-alt-console.json`: `Refused locally: the host's cheat roster (announced to this client) does not name you, so /give cannot land. The host grants it with /op. Nothing was done here.`; and the granted member's `/give` still lands: `r7-guest-console.json`: `Gave bandage (#…) to <host> in slot 0.` |

## Limits

- **One repetition per row.** Each invocation was driven once; a race between the write, the config
  monitor, the announcement and the console print is not excluded by one repetition.
- **The plain member's ground was the roster in this install.** The switch was already `true` when the run
  started (the install's config from earlier batches), so row 2's local refusal named the roster; the
  switch's own ground was driven in row 5a.
- **Row 6 is the member's own sandbox config.** "Unchanged" is the file's SHA-256 and its mtime across the
  write; the host's copy is a separate file read on the physical machine, which is the write the host
  performed on the member's request.
- **The member's permission refusal renders green.** The console adds every handler return as a success
  line; the member's gate refusal is a handler return now (the old host-only pre-check was an error line).
  The text names the refusal; the kind is `review/console-command-refusals-render-green.md`, which this cycle
  updated to carry the gate instance, and whose typed-answer fix has since made that refusal an error line.
- **No frame was captured.** Every row is a machine reading (console history, config file, host/member
  logs); nothing in the rows is a visual judgement.
- **The three-client setup came from the `20261024-a` prelude**, copied into this batch, which starts the
  clients serially and refuses to run while a game process exists.
- **The sandbox Steam clients were left running**, per this machine's convention; all three game clients
  were closed with the driver's `quit` and the process list was empty afterwards.
