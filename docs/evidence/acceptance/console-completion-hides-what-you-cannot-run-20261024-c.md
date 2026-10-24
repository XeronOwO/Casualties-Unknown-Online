# Acceptance record — the console completes only what you may run

- Ticket: `console-completion-hides-what-you-cannot-run` — verdict: **pass**, moved to `done/`.
- Batch: `20261024-c` — three clients (host, granted member, plain member), this ticket's seven rows plus
  the visual row.
- Commit: `77510106` · Deployed artifact: `CasualtiesUnknownOnline.dll`, `ProductVersion`
  `0.1.0+77510106c0860116d68c3a48bae321a3f86dcbb6` — `tools/verify-deploy.ps1` exit 0 before the clients
  started (`verify.log`), and the prelude re-read the same version (`prelude.log`).
- Run: 2026-10-24 01:56 → 01:59, this session's clock · Host: physical machine · Granted member: first
  sandbox · Plain member: second sandbox.
- Dependencies: `tools/acceptance/preflight.ps1` at the cycle's start (`RESULT: OK`, `preflight.log`),
  `session-environment.ps1 -Mode ensure-cuo` (`active=cuo`), the in-process driver on all three endpoints,
  and the committed `console-suggest`, `console-read` and `host-rules-read` recipes.
- Artifacts: `r1-*` … `r8-*`, `quit-*.json`, `prelude*.json`, `prelude.log`, `deploy.log`, `verify.log`,
  `preflight.log`, and the frame `r8-alt-suggestions.png` in the directory named by
  `acceptance-artifacts-dir`; players are written `<host>`/`<member>`/`<bystander>`. The run's starting
  values were `remote_backpack_access=allow`, `allow_member_cheat_verbs=true`,
  `native_binding_parity=warn`; every one of them was restored before the batch closed.
- The recipe's `trail=space` affordance was smoke-tested first (`r1-smoke-host-rules.json`: 16 rule names
  with their values), because the driver trims a `-RecipeArg` pair and a trailing space cannot otherwise
  reach the client.

| # | Row | Verdict | Evidence |
|---|---|---|---|
| 1 | A plain member's live suggestions for `/ga` (and `/k`, `/gi`) offer nothing for the authority commands, while the host's and the granted member's offer them | **pass** | plain member (`r1-alt-*.json`): `/ga` 0, `/k` 0, `/gi` 0, `/op` 0, `/trader` 0, while `/pl` → `players`; host (`r1-host-*.json`): `/ga` → `gamerule`, `/k` → `kick`, `/gi` → `give`; granted member (`r1-guest-*.json`, after `r1-host-op.json`): `/gi` → `give`, `/ga` → `gamerule`, `/k` 0 |
| 2 | The same member's `/help` does not list them; `/help gamerule` answers the permission refusal, not "unknown command" | **pass** | `r2-alt-help-console.json` (470-char list): contains `/data —` and `/players —`, contains none of `/give —`, `/gamerule —`, `/kick —`, `/op —`, `/traderreport —`; `r2-alt-help-gamerule-console.json`: `Refused locally: the host's cheat roster (announced to this client) does not name you, so /gamerule cannot land. …`; `r2-alt-help-kick-console.json`: `Permission denied: this command is host-only.` |
| 3 | The host's `/gamerule ` completes the 16 rule names; `remote_backpack_access ` the three values; `native_binding_parity ` the three; the weight rule offers no value | **pass** | `r3-host-rules.json`: 16 names in census order (`pvp_enabled` … `spectate_while_waiting_to_join`), `remote_backpack_access` described `Currently allow`; `r3-host-access.json`: `allow,unconscious_or_dead,deny`; `r3-host-parity.json`: `allow,warn,require`; `r3-host-weight.json`: 0 |
| 4 | Every offered value is accepted by the command when entered | **pass** | `r4-set-1..6.json`: all six values entered on the host answered `… is now set to: <the value>.` (`allow`, `unconscious_or_dead`, `deny`, `allow`, `warn`, `require`); `r4-host-rules-before.json` read `allow`/`Warn` and `r4-restore-access.json`/`r4-restore-parity.json` returned to them |
| 5 | `/data` still offers its read verbs to a plain member, and its write verbs' completions only to an authorised caller | **pass** | `r5-alt-data.json`: `1: get`; `r5-guest-data.json` (granted): `4: get,merge,modify,remove`; `r5-host-data.json`: `4: get,merge,modify,remove`; `r5-alt-dat.json`: the word `data` is still completed for the plain member |
| 6 | With the switch off (or the roster revoked) the granted member's suggestions drop back to the plain member's set | **pass** | `r6-host-switch-off.json` + `r6-guest-rules-off.json` (`allowMemberCheatVerbs=false`): `r6-guest-gi-off.json` 0; `r6-host-deop.json` (`Revoked the cheat verbs from <member>`): `r6-guest-gi-deop.json` 0; `r6-host-reop.json` + `r6-guest-gi-restored.json` → 1 (`give`) |
| 7 | A hidden command typed directly is still refused by name (hiding is not the enforcement) | **pass** | `r7-alt-gamerule-console.json`: `Refused locally: … cheat roster … does not name you …`; `r7-alt-kick-console.json`: the host-only sentence (the console refused it, `r7-alt-kick.json` has no `applied`); `r7-alt-give-console.json`: the same gate sentence for `/give`; `r7-host-query.json` still `allow` |
| visual | The suggestion list a player sees offers only what that player may run | **pass** | `r8-alt-suggestions.png` — the plain member's open console at `/` lists `clear, data, help, players, rtt` (the machine read `r8-alt-open4.json` names the same 8 candidates, including the ones below the visible fold: `save, spectate, whoami`), and no hidden verb is there |

## Limits

- **One repetition per row.** Each call was driven once; a race between the announcement and a completion
  read is not excluded by one repetition.
- **The visual frame is one role's.** The granted member's and the host's lists are machine reads only; the
  frame records the plain member's list (the row the ruling is about).
- **The plain member's ground was the roster** in this install (the switch was already on), matching row 2's
  and row 6's live readings; the switch's own ground was driven in row 6.
- **No frame was taken of a `/gamerule` value list.** Row 3 is a machine read; the list is rendered by the
  same overlay the frame shows.
- **A hidden command typed in full still shows its grammar refusal** (the rule names, the accepted values):
  the parse precedes the gate by design, and the ticket's Limits record it.
- **`/help <hidden>` renders green** (a handler return); the tracked console-wide defect
  `review/console-command-refusals-render-green.md` names this surface, and the typed-answer fix it asked for has
  landed since (see that ticket's acceptance).
- **The sandbox Steam clients were left running**, per this machine's convention; all three game clients
  were closed with the driver's `quit` and the process list was empty afterwards.
