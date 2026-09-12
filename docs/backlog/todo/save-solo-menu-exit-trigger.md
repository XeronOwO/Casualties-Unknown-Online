# S3.6 — Solo menu-exit trigger for the mid-run cut

- Status: Todo (found by the S3.3 adversarial pass; split out of S3's scope list on 2026-09-12 so
  stage 3's scope holds only what stage 3 does)
- Priority: Medium
- Category: Persistence / save system
- Source: `docs/backlog/todo/save-mid-run-consistent-cut.md` → scope 9 ("solo menu-exit trigger")
- Related: `docs/architecture/save-archive-format.md` §4 (cut phases), decision 167 (the cut seam),
  `docs/backlog/in-progress/save-system-mid-run-and-layer-end.md` (stage table)

## The gap

The deliberate menu return is the second mid-run trigger (decision 167). It is requested from
session-teardown events and decided by `RunMenuReturnPolicy.Decide(role, inWorld)`
(`src/CasualtiesUnknownOnline.Runtime/Session/RunMenuReturnPolicy.cs:11-20`), which returns
`SaveAndMenu` only for `SessionRole.Host`. Solo play has NO session role (`SessionRole.None` — the
enum's own contract), so a solo player leaving to the menu gets no menu-return cut at all: `/save` is
the only mid-run trigger there.

The S3.3 adversarial pass already fixed the sibling half of this family: the `/save` command is open
to anyone and the SAVE LAYER owns the authority rule (`WorldSaveService.WriteCut` refuses a guest),
precisely because solo has no role. The trigger edge was left for whoever owns the solo surface.

## The fix (as scoped)

One in-world → menu transition edge in the run coordinator that requests the SAME frame-end seam cut
(`IWorldSaveControl.TryRequestCut(WorldCutReason.MenuReturn, out refusal)`, taken by
`SaveCutSeam`/`WorldSaveService.TryCaptureArmedCut`), never a second cut path: decision 167 makes
that seam the only place a mid-run cut may be taken, and a solo player's leave has to write the same
snapshot a host's leave does. The cut is requested by whoever observes the transition; the leave
itself stays the adapter's business (`RunMenuReturnCoordinator.Leave`).

## Acceptance

| # | Scenario | Expected |
|---|---|---|
| 1 | Solo play, in world, leave to the menu | The same `MenuReturn` cut is armed and taken at the frame-end seam, and `WorldCutReport` names it |
| 2 | Solo play, not in world (menu/generation) | No cut is requested; nothing is written |
| 3 | Host with guests, deliberate leave | Unchanged: the session-teardown request keeps taking the same cut |
| 4 | Guest leaves | No cut is requested at all (the save layer refuses a guest — decision 164) |
| 5 | Cut refused (no repository, no run baseline, a stuck transient past the deferral bound) | The leave still happens; the report names the refusal and the previous snapshot stays |

## Verification limits

The Runtime suites can pin the request wiring, the refuse-a-guest rule and the deferral path; the
real in-world → menu transition is a game-side edge (scene state + the local body), so the actual
solo leave needs the user's in-game pass.
