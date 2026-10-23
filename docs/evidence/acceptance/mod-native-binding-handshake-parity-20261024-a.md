# Acceptance record — the Online UI's native-binding-parity row

- Ticket: `mod-native-binding-handshake-parity` — the last unjudged row, now **pass**; moved to `done/`.
  The other three rows were judged in batch `20261012-c`
  (`docs/evidence/acceptance/mod-native-binding-handshake-parity-20261012-c.md`).
- Batch: `20261024-a` — two Steam clients in one world.
- Commit: `2dfe2071` · Deployed artifact: `0.1.0+2dfe207183e1e880fe9feb59b510d2960510ab1e` —
  `tools/verify-deploy.ps1` exit 0 (`parity-ui-verify.txt`).
- Run: 2026-10-24 02:34 → 02:35, this session's clock · Host: physical machine · Member: first sandbox.
- Artifacts: `parity-ui.log`, `parity-ui-*.json`, `parity-ui-verify.txt`, `parity-ui-session-environment.txt`
  and the frame `parity-ui-admin.png` in the directory named by `acceptance-artifacts-dir`.
- Method: the host created a Steam lobby, the member joined, the host started the run (both in world), the
  host opened the Online UI and switched to its Admin page; one frame was captured with the window open.
  Both clients were closed by the driver afterwards.

| # | Row | Verdict | Evidence |
|---|---|---|---|
| 1 | The Online UI renders the native-binding-parity row | **pass** | `parity-ui-admin.png`: the Admin page's host-rules block carries the row `原生绑定一致性` with the value in force `警告` (= `warn`, the default); `parity-ui-host-admin-state.json` lists the offered control id `admin.rule_native_binding_parity` on that page, so the row is a live control rather than static text |

## Limits

- **One frame, one role.** The host's Admin page is the editable surface; the member's read-only summary
  was not captured, and the frame was taken with the host in a real two-client world (the gate line
  `Waiting for <member> to load…` is visible), not in the menu.
- **The row's edit path was not driven** in this batch: the value was left at its default `warn`. The
  rule's three levels are covered by `HostRulesPolicyTests` and the console's `/gamerule` acceptance; the
  UI's selector is the presentation of the same rule, and this row judged its presence and value.
- **No mod declared a native binding in this session**: the row is the RULE's surface, not a per-mod
  mismatch list, which is what the ticket's unjudged row named.
- The Steam clients were left running; both game clients were closed and the process list was empty.
