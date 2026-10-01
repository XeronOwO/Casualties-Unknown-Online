# Acceptance record — Entity destruction drops lose fresh-drop presentation/initial motion on the guest view

- Ticket: `entity-destruction-drop-guest-fresh-state-loss` — verdict: **stays in `review/`** (rows 1–4 unproven, row 5 passes; the visual judgement still cannot be carried at the game's world zoom)
- Batch: `20261001-y` — tickets `block-damage-table-capacity-alignment`, `guest-partial-block-damage-re-report`, `unhooked-damage-block-callers`, `trap-destruction-drop-quantity-desync`, `entity-destruction-drop-guest-fresh-state-loss`, `guest-block-mutation-re-report`
- Commit: `4b28a64d` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+4b28a64ddbdbb9068379db2f534f9ede0f472c00`
- Run: 2026-10-01, 22:12–22:26 +08:00 · Host: physical machine · Guest: Steam1 sandbox · Third client: Steam2 sandbox
- Dependencies used: `steam`, `game`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `logs`, `artifacts`, `deploy`
- Artifacts: `y-drop3-{host,guest,alt}-read.json`, `y-fresh-screen.cs`, `y-drop6-guest.png`, `y-drop6-guest-zoom.png`, `y-drop6-guest-after.png`, `y-drop6-guest-after-zoom.png`, `y-drop4-guest-f01..f08.png`, `y-drop4-alt-f01..f04.png`, `y-full-2.log`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Host destroys an entity: the guest view shows the same fresh-drop highlight/floating presentation | visual | **unproven** | the guest's own copies carried the component inside the window (`y-fresh-screen.cs` on the guest: `fresh: 3`, e.g. `circuitboard` world (-204.44,357.099) → screen (819,439.6) → bitmap (662,254)), and `y-drop6-guest.png` was captured then and crop-zoomed at that point (`y-drop6-guest-zoom.png`); the control frame `y-drop6-guest-after.png` (+zoom) is the same crop after the 10 s window (probe: `fresh: 0`). The pair shows a white-outlined object in the fresh frame and none in the control, but at the game's fixed world zoom that object cannot be identified with certainty AS the dropped item — recorded as a limit, not passed. |
| 2 | Guest destroys an entity: the host view shows the same | visual | **unproven** | not driven: every destruction this run was host-triggered. |
| 3 | Third party view | visual | **unproven** | the alt's copies carry the component (`y-drop3-alt-read.json`: `scrapmetal 1297401104323`, `fresh=true`), but no alt frame was captured inside the window. |
| 4 | The drops do not fall-then-pull-back; they start at the host's phase | feel | **unproven** | machine half holds — both peers materialize from the host's own state: `[ItemSpawn] materializing … vel (-1.9,-5.4) / (-4.3,-2.8) / (3.0,0.6)` at 22:18:26.403–.413, then `[ItemPhysics] play … (from freeze)` at .417; the visual half (no visible yank) needs a settled frame sequence inside the window, which this run's bursts did not resolve. |
| 5 | The regression half (existing sync tests and gates stay green) | machine | **pass** | the change set under acceptance adds acceptance recipes only; this cycle ran focus 4/4 (`y-focus-gate-3.log`), the gate project 300/300 (`y-gates-2.log`), `dotnet format` exit 0 (`y-format-2.log`) and the full suite with build 4573 + 300 (`y-full-2.log`). |

## Limits

- The fresh presentation is gated by the game itself at 8 world units from the destroying body
  (`BuildingEntity.cs:74`); inside that radius the peer's copy provably carries `FreshItemDrop`
  (`fresh=true` read on all three clients for the same id), which is the strongest machine half this
  ticket has had — the highlight's pixels remain unresolvable at the shipped zoom.
- `y-drop4-guest-f01..f08.png` and `y-drop4-alt-f01..f04.png` are the burst around another destruction;
  they bracket the 10 s window but the drops are a few pixels tall.
