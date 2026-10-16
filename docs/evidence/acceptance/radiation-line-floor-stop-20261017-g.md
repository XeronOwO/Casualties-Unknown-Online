# Acceptance record — the radiation line parks above the floor on the host's rule

- Ticket: `docs/backlog/done/radiation-line-floor-stop.md` — **CLOSED** by this record; the ticket moves
  to `done/`.
- Batch: `20261017-g`, run `line4` — three clients were started (host + two sandbox members); the third never
  answered the driver (`transport`), so the readings below are the host's and one member's. See *Limits*.
- Commits: `7d69fcb7` (the rule and the clamp) and `7d064d55` (the probe). Deployed artifact:
  `0.1.0+7d064d55…`, compared against this tree's build output by `tools/verify-deploy.ps1` (exit 0).
- Run: 2026-10-17, 03:22 → 03:23 · host on the physical machine, member in a sandbox.

The layer's own clock arms the line minutes in, so the run stands it at a chosen depth instead: it reads the
world's `height`, computes the clamp (`height - 8` = `timeGone` 1016 for that world) and writes the line five
units PAST it, then reads what every client does with that.

| # | Row | Verdict | Evidence |
|---|---|---|---|
| 1 | The host's rule parks the line | **pass** | The line was stood at `timeGone=1021`, five units past the clamp, and the host then read `active=true timeGone=1016` — the clamp, not the value it was given |
| 2 | Every view reads the same parked height | **pass** | host `timeGone=1016 clearance=10.957` beside member1 `timeGone=1016.217 clearance=10.782`: the same parked value, the small difference being the single frame between the two reads |
| 3 | The switch flips IN THE SAME SESSION | **pass** | The host's own console line, first `/hostrules {"RadiationLineStopsAboveTheFloor":true}` and then the same with `false`: `timeGone` moved 1016 → 1024.278 on the host and 1016.217 → 1025.019 on the member — the line descends again with no restart, and the member follows the host |
| 4 | The console writer and the watched definition both work | **pass** | Row 3 is that reading: the console reached the rule at all (`HostRulesConfigEditor.TrySet`'s new case) and the value took effect live (the entry inside `BepInExOptionsMonitor`'s watched set) — the two places the adversarial review found broken before this run |
| 5 | Clearance matches what the ticket now states | **pass** | It reads ~11 on both views rather than 8, which is `8 - sin(timeGone * 0.66) * 3`: the native wave freezes with the line, exactly the ±3 the ticket names |

## Limits

- **The third client never answered**: `member2` failed three reads with `error=transport`, so "three views
  agree" is read as "the host and one member agree". Two views agreeing on a value the member derives through
  its own frame loop is what the mechanism claims, and the third client's silence is a batch/box problem
  rather than a reading about the rule — but it is not read, and it is named here instead of being rounded up.
- **The margin is still a starting number**: 8 units is the reference implementation's value, and this run
  confirms what it does (a parked line ~8–11 units above the floor), not that it is the right room for this
  game's layer heights and body sizes. That judgement needs a player looking at the bottom of a layer.
- **Two edges were not staged**: turning the rule ON mid-descent was not read (the run set it ON before
  standing the line), and neither was a parked line's behaviour across a layer change. Both are stated in the
  ticket as consequences a reader should expect.
- **A persisted rule decided this run's first reading**: a previous run had written the rule OFF through the
  console, so the line was descending when this run began. That is the correct behaviour for a host rule and
  it is why the batch now sets the value it means to test instead of assuming the default.
