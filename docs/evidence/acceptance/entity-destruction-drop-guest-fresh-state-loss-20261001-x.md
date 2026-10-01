# Acceptance record — Entity destruction drops lose fresh-drop presentation/initial motion on the guest view

- Ticket: `entity-destruction-drop-guest-fresh-state-loss` — verdict: **stays in `review/`** (rows 1–4 unproven, row 5 named; the visual judgement could not be carried by this run's frames)
- Batch: `20261001-x` — tickets `block-break-first-writer-wins`, `guest-block-mutation-re-report`, `guest-partial-block-damage-re-report`, `block-damage-table-capacity-alignment`, `trap-destruction-drop-quantity-desync`, `entity-destruction-drop-guest-fresh-state-loss`, `guest-break-drops-recovery`, `unhooked-damage-block-callers`
- Commit: `725fe0f4` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+725fe0f4de9887015b1c56a19209f600fbe1ee39`
- Run: 2026-10-01, 20:51–21:35 +08:00 · Host: physical machine (Steam app id 4576510) · Guest: Steam1 sandbox · Third client: Steam2 sandbox
- Dependencies used: `steam`, `game`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `logs`, `artifacts`, `deploy`
- Artifacts: `s2-guest-baseline.png`, `s2-guest-after-break.png`, `s2-alt-after-break.png`, `s2-host-items-after.json`, `s2-guest-items-after.json`, `s2-guest-breakB.json`, `s2-host-watch.json`, `s2-fresh-round.json`

The ticket states its acceptance criteria in prose; the run writes the rows first and judges them.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Host destroys an entity: the guest view shows the same fresh-drop highlight/floating presentation | visual | **unproven** | the guest window was captured before and after the pad's destruction (`s2-guest-baseline.png` → `s2-guest-after-break.png`) and shows the destroyed pad's crater and the HUD, but at the game's own zoom the drops themselves are not resolvable enough to judge a highlight — the independent review read the same frame and reported the same limit; the fresh-drop component was not caught live either (row 4). |
| 2 | Guest destroys an entity: the host view shows the same | visual | **unproven** | the second pad was broken by the guest, but by then the host had performed a leave→continue recovery and the item set differed; no host frame was captured for that half. |
| 3 | Third party view | visual | **unproven** | the third client's frame after the first destruction (`s2-alt-after-break.png`) has the same resolution limit as row 1. |
| 4 | The drops do not fall-then-pull-back; they start at the host's phase | feel | **unproven** | no frame sequence was captured, and the fresh flag was never observed as `true`: this pad's two drops read `fresh:false` on both clients ≈20 s after the break (`s2-host-items-after.json`, `s2-guest-items-after.json`, initial spin `av=-0.156` present), and three attempts to catch the flag inside its short window observed no new item at all — a same-eval 40 × 100 ms poll on the breaker (`s2-guest-breakB.json`), the observer's 4 s watch whose window had closed before the break it waited for (`s2-host-watch.json`), and the last round's 4 s watch (`s2-fresh-round.json`: `no-new-item`, listing two unrelated items). |
| 5 | The regression half (existing sync tests and gates stay green) | machine | **unproven** | the change set under acceptance is documentation-only (no `src/`, `tests/` or `tools/` modification), so this batch ran the gates project only — `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests` → 300/300, reproduced twice by the independent review — and skipped build, the full suite and `dotnet format` per `AGENTS.md`. The engine-side regression half of this row is not read here. |

## Limits

- Frames are read, not measured: the world frames at this zoom cannot resolve a drop's highlight, so the visual rows are `unproven`, not passed and not failed.
- The drops that were observed on both clients carried an initial spin (`av=-0.156`) but never a `fresh` flag in this session's reads.
