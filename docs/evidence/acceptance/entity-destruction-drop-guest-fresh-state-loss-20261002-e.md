# Acceptance record — Entity destruction drops lose fresh-drop presentation/initial motion on the guest view

- Ticket: `entity-destruction-drop-guest-fresh-state-loss` — verdict: **stays in `review/`** (rows 1-4
  `unproven`: the peers' copies provably carry the fresh presentation inside its window in both
  directions, but the run's frames cannot be tied to the drop sprites at the shipped zoom; row 5 passes)
- Batch: `20261002-e` — ticket `entity-destruction-drop-guest-fresh-state-loss` (batch scope page:
  `docs/evidence/acceptance/20261002-e-scope.md`)
- Commit: `83ab1272` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+83ab1272e95c1de065842597e80d05c556e4b483` (`tools/verify-deploy.ps1` exit 0)
- Run: 2026-10-02, 09:14-09:28 +08:00 · Host: physical machine · Guest: Steam1 sandbox · Third client:
  Steam2 sandbox; one world, one session (lobby `109775243622860862`, members 3)
- Dependencies used: `steam`, `game`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `logs`, `artifacts`,
  `deploy`
- Artifacts (in the directory named by `acceptance-artifacts-dir`): `e-verify-deploy.log`,
  `e-build.log`, `e-full-test.log`, `e-format.log`, `e-floating-{host,guest,alt}.json`,
  `e-site-find-{host,guest}.json`, `e-place4-*.json`, `e-place5-*.json`, `e-gate-host-4.json`,
  `e-gate-guest-5.json`, `e-break-host-3.json`, `e-break-guest.json`,
  `e-c-{host,guest,alt}-fresh.json`, `e-c-{host,guest,alt}.png`, `e-c-{host,guest,alt}-after.png`,
  `e-c-guest-marked-zoom.png`, `e-d-{host,guest,alt}-fresh.json`, `e-d-{host,guest,alt}.png`,
  `e-chain-{host,guest,alt}.log`, `rehearsal-y-drop6-tight.png`, `rehearsal-y-drop6-after-tight.png`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Host destroys an entity: the guest view shows the same fresh-drop highlight/floating presentation | visual | **unproven** | machine half passes: host break at 09:23:32.8 (pad cell (302,154), destroyer 6.91 units, support 2 → 0) produced `[ItemBuildingDeathDrop] local scrapmetal/scrapmetal/circuitboard …` + `Committed(0+3) events=[Break, Drop, BuildingDrop]`; the guest's census at 09:23:38.38 reads `fresh:3` with the same three ids and world positions within 0.05 units (`1134192347075`, `1129897379779`, `1125602412483`) and velocities ~0, and its own log carries `[ItemSpawn] materializing … vel (-4.2,-3.1) / (3.6,5.0) / (4.4,-0.5)` at 09:23:32.894-.905. Visual half not judged: the guest frame `e-c-guest.png` was captured at 09:23:38.5 (inside the window) but no drop sits under any census-projected mark (`e-c-guest-marked-zoom.png`) |
| 2 | Guest destroys an entity: the host view shows the same | visual | **unproven** | machine half passes: the guest broke the support of pad cell (918,624) at 09:27:01.4 (destroyer 5.99 units, support 1 → 0) and captured `[ItemBuildingDeathDrop] local scrapmetal (id 10156476526) at (406.7,112.0) …` + `Committed(0+1) events=[Break, Drop, BuildingDrop]`; the HOST materialized the same id 86 ms later with the guest's velocity (`[ItemSpawn] materializing scrapmetal (id 10156476526) at (406.7,112.0), vel (-0.3,4.1)`, 09:27:01.470) and its census at 09:27:05.67 reads `fresh:1` for it. Visual half not judged (same projection limit; frame `e-d-host.png`) |
| 3 | Third party view | visual | **unproven** | machine half passes in both directions: the alt's censuses read `fresh:3` (09:23:40.12, the host's three ids) and `fresh:1` (09:27:10.40, the guest's id), and its log carries the matching `[ItemSpawn] materializing …` lines at 09:23:32.902-.912 and 09:27:01.492; frames `e-c-alt.png` and `e-d-alt.png` were captured inside the window, the pixels are not resolvable |
| 4 | The drops do not fall-then-pull-back; they start at the host's phase | feel | **unproven** | machine half passes: each peer materializes from the breaker's own state with its velocity (log pair above), the censused velocity stays ~0 through the window (host `av` 0.164/0.029/-0.031, `vy` 0/0.001/0) and the fall appears only after it (alt `vy=-5.299` at 09:27:10.40, past `FreshItemDrop`'s 8 s gravity ramp). The visual half needs a frame burst that resolves the item; this run's frames do not |
| 5 | The regression half (existing sync tests and gates stay green) | machine | **pass** | this run's own chain: `dotnet build` 0 warnings / 0 errors, full suite 4577 + 315 passed, `dotnet format` exit 0 with a clean tree afterwards |

## Where the visual half stands

The family's presentation mechanism is the game's own `FreshItemDrop` component
(`reversing/Assembly-CSharp/Assembly-CSharp/FreshItemDrop.cs`): `Start` parents a copy of the item's own
sprite at scale 1.125 with `Special/ItemOutline` one sorting order behind the item, `Update` holds the
outline at full alpha for the first 7.5 s, and `FixedUpdate` keeps `gravityScale` at 0 until the last
2 s. This run proves the peers' copies carry that component inside its window in BOTH directions, with
ids and world positions identical to the breaker's — the strongest machine half this ticket has had.
What it does not have is a readable frame: the run's `screen → bitmap` mapping places the census-named
drop tens of pixels away from the frame content at these sites, so no crop can be said to show the drop,
and the row is not judged.

## Limits

- The mapping and its failure are recorded in the batch scope page; a rehearsal crop of batch-y's own
  frames at its recorded point is pure black in both its fresh and control frame.
- The sites are unlit cave rooms: the drop sprites render dark there, a second independent reason the
  crops cannot settle the row.
- One session is not a race proof; the machine halves are read from the state the ends converged to.
- The destroying bodies were moved to scanned standing spots next to the chosen pad (`body-place`), a
  declared substitution: the spawn area is 100+ units from the nearest pad.

## Residuals for the user

- None from this batch: every unjudged row is a capability limit of the run's frames, not a judgement a
  person must make about feel.
