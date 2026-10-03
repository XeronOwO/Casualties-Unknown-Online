# Acceptance record — Trap/entity action divergence

- Ticket: `trap-action-divergence-hardening` — verdict: **moved to `done/`** (both acceptance bullets pass;
  the ticket's own shy-crystal pairing residual is carried, see below)
- Batch: `20261003-f` — tickets `guest-partial-block-damage-re-report`, `partial-damage-delta-report-overlap`,
  `guest-break-drops-recovery`, `guest-command-loss-reconciliation`,
  `entity-destruction-drop-guest-fresh-state-loss`, `runtime-entity-markerless-bind-absorption`,
  `trap-layout-entry-snapshot-staleness`, `trap-layout-snapshot-recovery`, `turret-stray-fire-after-reload`,
  `trap-action-divergence-hardening`, `unhooked-damage-block-callers`, `session-control-convergence`
  (scope: `docs/evidence/acceptance/20261003-f-scope.md`)
- Commit: `76ef80c2` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+76ef80c28162de716c38626075b69a2032fc8206` (`tools/verify-deploy.ps1` exit 0 before the session)
- Run: 2026-10-03, from 10:19 +08:00 · Host: physical machine (launched through Steam) · Guests: the primary
  and the alternate sandboxes; three clients, one world, one lobby
- Dependencies used: the eleven ids `tools/acceptance/preflight.ps1` reports present (exit 0); the alternate
  sandbox supplied the third peer
- Artifacts: the `s7*` ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Each action's verdict is decided per case with the decompiled source as evidence, and the per-row containment question (does a throwing row abort the whole live-world half?) is answered with a test at the Runtime seam | `machine` | **pass** | re-run against the deployed tree in this session: `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~RestoredWorldFactReplayTests"` → **24 passed / 0 failed / exit 0** (the throw-scope cases `ApplyIfPending_WhenAWorldEntityRowThrows_DoesNotBlameTheHalvesThatLanded` and `ApplyIfPending_WhenTheWriteThrows_ReportsIncompleteAndReleasesBothHandovers` are in that class), and the combined filter `TrapActionClassificationTests\|RestoredWorldFactReplayTests` → **28 passed / 0 failed / exit 0** |
| 2 | Machine evidence: the classification half is read-only reviewed and the containment half is pinned at the Runtime seam, exceeded for three cases — `TrapActionClassificationTests` drives the production `ApplyShower`/`ApplyHeat` bodies and the extracted `CrystalEffectAccess.TryActivate` rule on never-initialized game components; the two crystal ACTION bodies are not host-drivable and the reason is recorded | `machine` | **pass** | the same session's run: `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~TrapActionClassificationTests"` → **4 passed / 0 failed / exit 0** (the two lifepod refusals plus the latch-rule and mimic-refusal cases); the ticket's own per-mechanism table names the read-only-reviewed rows (`BioTerminalScript.building`, the `CrystalShy` latch, `CrystalEMP.activated`, `CrystalMetamorphic` latch + death) and the recorded reason the crystal bodies are not host-drivable (`Component.transform` / `CrystalBehaviour.build` are members the test host cannot bind) |

## The live half this batch attempted (the ticket's own residual)

The ticket's `Residuals (recorded, not closed)` names the shy-crystal pairing: the replayed row's position is
post-swap, so a dual-client pass that trips a shy crystal and watches both worlds' crystal positions would
settle whether the pair converges.

- **Attempted, not stageable with the committed instruments.** `world-object-census` ran on all three clients
  (`s7-census-host.json`, `s7-census-guest.json`, `s7-census-alt.json`): every world holds the same total
  (1883 entities) and the same crystal populations — `BloodCrystal` 3, `ReliefCrystal` 7, `TurbulentCrystal` 2 —
  so the three worlds agree on the crystal set at that instant.
- The pairing is a **position swap** (`CrystalShy`'s scan → swap), and the census returns prefab counts only
  (its own contract: `ok, utc, total, groups`); `generation-read` returns the run epoch and layer index. No
  committed instrument reports a per-crystal position or latch, so "watch both worlds' crystal positions"
  cannot be read in this run.
- A trip was still attempted live: four `move-drive` slide nudges moved the host body (`s7-move-1..4.json`;
  position X 0 → 20.968, Y 497.615 → 481.814) and no `Crystal`/`shy` line appeared in the host's or the
  guest's log tail — and with no position readout a trip could not be confirmed even had one occurred.

## Limits

- The classification and containment halves are machine-verified by the re-run tests; the four
  read-only-reviewed rows rest on the decompiled sources, as the ticket records.
- One session, one world; the drive attempt is four nudges, not a search.

## Residuals for the user

- **Shy-crystal pairing (the ticket's own residual, carried):** a dual-client pass that trips a shy crystal and
  compares both worlds' crystal positions would settle whether the pair converges. The run could not stage it —
  no committed instrument exposes a per-crystal position or latch, and the pairing is a position swap the
  prefab census cannot see.
- `TrapEntityScan.CrystalKinds` being dead for crystals and the `PlayerCamera.main` dereferences recorded in the
  ticket are unchanged by this run and stay in the ticket's own residual list.
