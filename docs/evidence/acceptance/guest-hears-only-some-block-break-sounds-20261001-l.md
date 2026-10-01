# Acceptance record — Guest hears only some of the host's block hit/break sounds

- Ticket: `guest-hears-only-some-block-break-sounds` — verdict: **moved to `done/`** (every row judged; the
  audible half is a residual for the user)
- Batch: `20261001-l` — tickets `host-eating-sound-not-heard-on-guest`,
  `host-metal-scrap-block-place-sound-not-synced-to-guest`, `guest-hears-only-some-block-break-sounds`,
  `sync-player-pain-vocalizations-and-bark`, `unhooked-item-and-body-sound-families`,
  `suppressed-native-call-sounds-stay-unheard`
- Commit: `a91ca24a` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+a91ca24a04deaa0aa6994abec495651c810eaaff` (`deploy.ps1` then `verify-deploy.ps1`, exit 0)
- Run: 2026-10-01 09:46 → 09:59 (+08:00) · Host: physical machine (18590) · Guests: primary sandbox
  (18591) and alternate sandbox (18592), one world
- Dependencies: preflight `11 present`, exit 0
- Artifacts: the `batch-l` directory under `acceptance-artifacts-dir`; the run log is `l-run-log.txt`

## How the rows are decided

A block hit/break is not a `CharacterSound` event: the side that rolls the damage plays the block's own
hit or break presentation locally, and a receiving side replays it by running its own
`WorldGeneration.DamageBlock` on the reported cell. The machine evidence for "the other side presented the
same event" is therefore the receiver's own world state: a damage row exists there only because its local
roll ran (the roll is where the hit sound lives), and a break leaves the cell air on both sides. Probes:
`block-hit` (one roll per invocation, the cell relative to the local body) and `block-read` (the cell's
block id and damage row, with the returned cell coordinates checked against the intended absolute cell).

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Every hit sound the host plays plays on the guest | machine + residual | **pass** | five non-lethal hits (`dmg=1`) on the rock at (511,1008): the host's damage row grew 1 → 55 across `l-C1-host-hit1..6-lethal.json` / `l-C1-host-cell-after.json`, and the guest's row for the same absolute cell read `damage=55` (`l-C1-guest-cell-after.json`; the first hit's `damage=1` is `l-C1-guest-hostcell.json`) — every report ran the guest's own roll |
| 2 | The break plays exactly once | machine + residual | **pass** | the placed block at (516,1011) broken with one lethal hit (`l-C2b-host-break.json`); both sides then read `block=0, damage=-1` (`l-C2b-host-cell.json`, `l-C2b-guest-cell.json`) — one air write, one break presentation per side, no double |
| 3 | The guest mines, the host listens | machine + residual | **pass** | the guest's lethal hit on (512,1009) (`l-C3b-guest-break.json`); the host's cell read `block=0` (`l-C3b-host-cell.json`), and the guest's own follow-up read shows it standing in the hole it made (`l-C3b-guest-cell.json` reads block `8` one cell lower) |
| 4 | A third peer hears the same cadence | machine + residual | **pass** | the alternate client's reads of the same absolute cells: (516,1011) and (512,1009) both `block=0` (`l-C4-alt-cell-516_1011-broken.json`, `l-C4-alt-cell-512_1009-broken.json`), and one absolute row of the layer read **identical** on all three clients (`l-world-row-host.json`, `l-world-row-guest.json`, `l-world-row-alt.json`: `504:0,505:6,506:10,507:0,508:0,509:0,510:8,511:0,512:0,513:0,514:8,515:8,516:5,517:10,518:0,519:3,520:0`) |
| 5 | Fast consecutive hits are not coalesced | machine + residual | **pass** | four back-to-back hits at 09:52:43 (`l-C1-host-hit2..5.json`, all landed in the same second) plus the earlier one: the guest's damage row accumulated to 55, i.e. every report applied; `l-C1-hits.txt` lists the six calls |
| 6 | Two players hit the same block | machine + residual | **pass** | the host and the guest each rolled one hit at the same absolute cell (515,1008) from their own positions (`l-C6-host-hit.json`, `l-C6-guest-hit.json`), and both clients then read `damage=2` there (`l-C6-host-cell.json`, `l-C6-guest-cell.json`) — both contributions accounted on both sides |

## Residuals for the user

- The audible half of every row: the presentation call is a Unity icall this run cannot hear. What is
  proven is that each receiving client's own damage roll ran for every report (the state rows above) and
  that a break leaves both worlds air with no second presentation.

## Limits

- The two players' positions drift independently; each `block-read`/`block-hit` was computed from the
  local body and its returned cell coordinates were checked against the intended absolute cell before the
  values were compared, so the pairs above are the same cell on both sides.
- The rock used for rows 1 and 5 has more health than 55, so the "break" of row 2 was driven on a placed
  block (id 3) with a lethal hit; the break path is the same one.
- A single session proves the events observed; it does not disprove a rare loss between reports.
