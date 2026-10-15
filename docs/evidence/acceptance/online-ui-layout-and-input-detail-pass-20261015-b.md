# The Online UI's layout and input detail pass — batch `20261015-b`

- Ticket: `online-ui-layout-and-input-detail-pass` — verdict: **`review/`** (row 11's English half passes; the
  rows that remain are setups no run has had, and row 11's window half still needs a session)
- Batch: `20261015-b` — tickets `online-ui-layout-and-input-detail-pass` (the only ticket this batch judges)
- Commit: `78a86961` · Deployed artifact: `0.1.0+78a869618a6c6047aedf87a39395195ee3045f2f` (`verify-deploy.ps1`
  exit 0) · Host: one client on the physical machine, no guest
- Scope: `docs/evidence/acceptance/20261015-b-scope.md` · Artifacts: the `20261015-b` directory of the local
  artifact root

## The row this batch exists for

**Row 11 — no regression, the English half.** Batch `20261015-a` failed it: the strip read
`Tabs rect=1038.7x36.0` against a 984-unit page, with the last tab ending 55 units past the window's right
edge. Read live on this build, in the window the run opened and left on the Admin page:

| Reading | Chinese | English |
|---|---|---|
| The band | `Tabs rect=984.0x36.0` | `Tabs rect=984.0x36.0` |
| The six tabs | 112 / 112 / 112 / 112 / 112 / 112 | 112 / 132.1 / 133.8 / 112 / 119.5 / 191.2 |
| Tabs plus the five 4-unit gaps | 692 | **820.6** |
| Against the page | 984 | 984 |

Verdict: **pass.** The strip is inside its band in both languages, and in English it is 164 units clear of the
window's right edge where it used to overshoot it by 55. Evidence: `d2-admin-zh-dump.txt`, `d5-admin-en-dump.txt`
(the band lines and the six tab buttons), and the frames `d3-admin-zh.png`, `d6-admin-en.png` — in the English
frame every caption is whole and the last one ends well inside the frame.

## The share rule, exercised on purpose

At the shipped size the tabs fit their band, so the rule that shares it has nothing to do — reading only the
shipped window would prove the room was counted once and nothing about the share itself. The run therefore
narrowed the WINDOW's own rect to 800 units (inner width 784) and read the strip again:

| Reading | Value |
|---|---|
| The band | `Tabs rect=784.0x36.0` |
| The six tabs | 112 / 121.3 / 123 / 112 / 115.4 / 180.3 |
| Tabs plus gaps | **784.0** — exactly the budget |

Verdict: **pass, and it is the half the independent review's finding was about.** The tabs gave up 36.6 units
between them in proportion to the room each had above its own floor, every caption stayed whole (the frame
`e3-narrow-800.png` shows `Preferences` complete at the strip's right edge), and the sum lands exactly on the
budget. That last number is also what separates a budget read off the WINDOW from one read off the band: the
band is as wide as its own children's minimums, so a strip budgeted from the band would have kept the tabs at
820.6 and the band at 820.6, overhanging an 800-unit window. Evidence: `e1-narrow-800.json`,
`e2-narrow-dump.txt`, `e3-narrow-800.png`, `e4-restore-width.json` (the window put back to 1000).

## The rows this batch did NOT judge

| Row | State | Why |
|---|---|---|
| 8 — a canvas smaller than the frame | `unproven` | still the setup the run cannot make (a canvas below the frame) |
| 9 — an OS-level drag to the edge | `unproven` | a real pointer drag is outside the in-process driver by design |
| 10 — the launcher over the medical panel | `unproven` | needs a world and a session |
| 11's window half — the quick panel, the context menu, the overlays | `unproven` | needs a session; this batch judged the strip, which is the half that failed |

## The one visible consequence, read in the frames

Every caption-sized control is narrower than batch `20261015-a`'s frames show, because that surplus WAS the
room counted twice: exactly 40 units where the caption decides the width, less where the tab model's 112-unit
floor takes over (the Chinese tabs, 130 → 112; `Home` and `Admin` in English, 138.6 → 112 and 143.6 → 112).
Read from `d6-admin-en.png` and `d3-admin-zh.png`, the captions are whole, and no control reads as cramped at
the shipped window size. Whether it reads as a control at the player's own canvas scale stays the user's
judgement, as the ticket records.

## What this run leaves behind

- The lesson the batch taught is a second sighting of the container-measurement lesson of 2026-10-14, and this
  run's shape of it is its own entry (`docs/acceptance/lessons.md`, 2026-10-15): a budget read off the container
  it is shared out among agrees with itself and never diverges.
- The probes worth reusing are `narrow.cs` (write the window's own rect), `ui-dump.cs` and the language probe,
  all in the local artifact directory; the batch's own JSON answers are named in the rows above.
