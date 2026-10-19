# Acceptance record — a member's recipe unlock is bounded to the host's own table

- Ticket: `docs/backlog/done/member-report-bounds.md` — **section B's `NetMsg.RecipeUnlock` row is CLOSED**
  (one of the section's five). The ticket does NOT move: four section-B rows and both section-C rows are
  still undeveloped code.
- Batch: `20261017-b`, run 1 — three clients in one world (host on the physical machine, members in the
  `Steam1` and `Steam2` sandboxes). Three clients, because the refusal's whole point is that the OTHER
  member is not written to.
- Commit: `333d3a1a` — Deployed artifact: CasualtiesUnknownOnline.dll, ProductVersion
  `0.1.0+333d3a1a97561c0da4a367c832c5b28d80631398`, verified against this tree by
  `tools/verify-deploy.ps1` (exit 0; `b1-verify-deploy.txt`). The probe the run drives
  (`tools/acceptance/recipes/recipe-unlock-forge.cs`) is committed with that commit and read from the
  tree by the driver, so it is not part of the deployed artifact.
- Run: 2026-10-17, 01:41:19 → 01:42:15 · Host: physical machine · Members: `<member-a>` (the forger,
  `Steam1`) and `<member-b>` (`Steam2`, the side that must stay untouched), all three reading the
  physical install.
- Dependencies used: `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (all three endpoints), `capture`,
  `input` (the in-process driver and the scenario recipes), `logs`, `artifacts` — the preflight reported 9
  present before the run.
- Artifacts (batch `20261017-b/`): `run1.ps1`/`run1.log` (the script and its own trace), `b0`–`b1`
  (preflight, environment, deployed-artifact check), `b2`–`b4b` (the three endpoints coming up),
  `b5`–`b11` (lobby, the three clients in world), `b12`–`b14` (the three host windows and their two probe
  answers), `b15`–`b17` (teardown).

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A member reports a blueprint unlock for an index the host's own recipe table does not have | machine | **pass** | `b13-host-bad-index.txt` — `[WRN] [CraftSyncService] [Crafting] refused <member-a>'s recipe 9999: this host's table has no such recipe (132 entries) — nothing applied, nothing relayed.` The refusal names the sender, the index and the TABLE'S OWN SIZE (132 entries, the live `Recipes.recipes`), and it is the only line the window carries: the frame was neither applied on the host nor relayed to `<member-b>`, so the third client never receives the index |
| 2 | The same path with an index the host's table holds | machine | **pass** | `b14-host-good-index.txt` — the control run in the SAME session: `[INF] [RecipeUnlockApply] [Crafting] recipe 0 unlocked (<sprite index=25>…)` and `[Crafting] showed recipe-unlock popup (…)`, with NO refusal line in that window. So the bound is a comparison and not a blanket rejection of whatever a member reports, and an honest unlock still lands and still reaches the member's own screen |
| 3 | An unmodified stretch of the same session meets no refusal | machine | **pass** | `b12-host-crafting-baseline.txt` — the host's own `[Crafting]` window over the entry phase carries two lines, both `sent 2 unlocked recipe(s) to <member>`, i.e. the world-entry backfill of the absolute set, and zero refusals, while all three clients were in the world (`b9`–`b11`: `inWorld=true` on each) |

Judged from this run's own readings against the deployed artifact named above.

## Residuals for the user

None open. Every row is a host log line, a probe's own answer or a client state, so nothing here waits on
an eye.

## Limits

- **What "nothing relayed" rests on.** The host's own line says it, and `<member-b>` was in the world
  throughout — but the third client's recipe table cannot be READ by any probe in this tree (there is no
  read verb for it), so the far half of the claim is the code path plus the host's log rather than a
  client-side reading. What the run does show from the far side is that the member stayed in the world.
- **The `132 entries` figure is the LIVE table's own count**, printed by the refusal itself, not a number
  this record carries from elsewhere.
- **What the direction leaves open** is named in the census row and the ticket rather than here: a session
  whose two recipe tables differ in LENGTH (the index space is per-process), the window in which a host's
  table exists but its mod recipes are not appended yet, the fact that the refusal is unanswered and the
  reporter converges on its own bounded re-report budget, and the guest-side half every `Bidirectional`
  row of the census carries.
- **The probe reports an index that no table has (9999) and the control reports a real one (0)**; a probe
  cannot ask for "an index the HOST lacks but this member has", which is the length-difference case above.
