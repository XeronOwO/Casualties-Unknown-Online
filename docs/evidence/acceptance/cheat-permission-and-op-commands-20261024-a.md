# Acceptance record — cheat permission row 6 (the IP-direct session) on the current artifact

- Ticket: `cheat-permission-and-op-commands` — row 6 passes here; rows 1-5 were judged in batch
  `20261017-q` on `0.1.0+bb2350e7…` (its record is
  `docs/evidence/acceptance/cheat-permission-and-op-commands-20261017-q.md`), and this batch re-reads the
  two mechanisms those rows bind on the CURRENT artifact (grant refusal, member gate) rather than
  re-running the `/data set` wording of rows 1-5.
- Batch: `20261024-a` — a two-client IP-direct probe through the Online UI's own transport switch.
- Commit: `2dfe2071` · Deployed artifact: `0.1.0+2dfe207183e1e880fe9feb59b510d2960510ab1e` —
  `tools/verify-deploy.ps1` exit 0 (`ipdirect-row6-verify.txt`).
- Run: 2026-10-24 02:31 → 02:32, this session's clock · Host: physical machine · Member: first sandbox.
- Artifacts: `ipdirect-row6.log`, `ipdirect-row6-console.log`, `ipdirect-row6-*.json`,
  `ipdirect-row6-verify.txt`, `ipdirect-row6-session-environment.txt` in the directory named by
  `acceptance-artifacts-dir`.
- Method: no Steam lobby. Host: Online UI → `home.transport.ip` → `ip.create_host` (role=Host,
  active=true). Member: `home.transport.ip`, the loopback address (the default `[IpDirect] JoinAddress`)
  and the configured listen port, `ip.join` (session active). Then the host's console and the member's
  console were read through the driver.

| # | Row | Verdict | Evidence |
|---|---|---|---|
| 6 | IP-direct: the host's `/op` is refused with the IP-direct sentence, and the member's cheat verb is refused too | **pass** | host `/op <member>` (`ipdirect-row6-console-host-op.json`, line read by `console-read`): `This session is IP-direct: a peer is known by the logical id its own connection registered, not by an account, so a grant keyed on it would follow a connection rather than a person. Cheat permission is not granted in an IP-direct session.` — the member's transport-assigned logical id came from `give-probe`; member `/gamerule pvp_enabled false`: `Refused locally: the host's cheat roster (announced to this client) does not name you, so /gamerule cannot land. The host grants it with /op. Nothing was done here.`; control: the host's own `/gamerule pvp_enabled` answered `Game rule pvp_enabled is currently set to: false.`, so the verb itself works and only the member is refused |

## Rows 1-5 (carried from `20261017-q`, not re-worded here)

The earlier batch judged them on the deployed `bb2350e7`: the switch's sentence with a grant present,
the roster's sentence with the switch open and no grant, a `/op` that lands and both peers reading the
written value, a `/deop` that refuses the same write on the member's own console, and the self/unknown
target sentences. That record is the evidence for those five; this batch's member line above re-reads
the roster ground on the current artifact, and the host's query re-reads the host's own standing.

## Limits

- **Rows 1-5 were not re-driven** with `/data set`: they were accepted on `bb2350e7` and their wording
  names the item-data verb. Re-reading their exact lines on the current artifact would need a granted
  member holding a known item and a three-client world; this batch bound the ROW-6 mechanism and the
  two rulings' current sentences instead, and the ticket records the difference rather than claiming a
  re-run of rows it did not perform.
- **The IP-direct member's identity is the transport's synthetic id**, not a Steam account; that is the
  condition the row exists for (no account to key a grant on), not a limitation of the run.
- **No world was started**: `/op` and `/gamerule` are session-level, and the row is about the grant's
  standing; the host's own rule query is the control that the console path is live.
- **Steam clients were left running**; both game clients were closed by the driver and the process list
  was empty afterwards.
