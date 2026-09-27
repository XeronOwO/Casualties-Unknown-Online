# Carry rider acceptance readiness — self-check (2026-09-27)

Ticket: `docs/backlog/todo/carry-piggyback-rider-position-smoothing.md` (Critical).

Cycle scope: make the ticket's one remaining action — the physical-machine dual-client run — answer its
code-visible questions in a DEFAULT session, and turn the reported symptom into a reading a log can carry.
No placement change, no authority change, and no fix built on the mechanism the ticket itself records as
unverified. The independent review's fix round is folded in (§6).

## 1. What the run has to answer, and what each answer is worth

| Question | Where the answer comes from | What it does NOT answer |
|---|---|---|
| Was the structural fix engaged on this view (rider clone mounted to the local carrier, and pinned at all)? | the 1 Hz clone line's `mounted-to-local-carrier` tag and its `pinned-to-carrier` tag (a pin was written in this window); a live relation with NO pin in the whole window is a WARNING | whether the rendered picture matched |
| Did an exact limb pose travel with the body root the ride pose pinned (the ticket's 2026-09-07 question)? | `limbSeparation` on the same line, plus a WARNING above the tolerance | the conscious-rider population: its clone is animator-driven and is not measured |
| Was the rider RENDERED away from the position its carry pin wrote? | `riderDrift`, read from the state the frame that rendered left behind, plus a WARNING above the tolerance | the pair being drawn together: a carrier moved AFTER its own pin takes a mounted rider with it, so both sides stay stale together and that frame reads zero by construction. A zero clears "a writer moved the rider after the pin", never the symptom |
| Does it look right on the screen? | the user's own two-client run — unchanged, and nothing in this cycle claims it | — |

## 2. What landed

- `CarryPresentationReading` (Runtime, pure) now owns every DECISION: `ReportThreshold` (a tolerance of
  half a percent of a body height, two orders above the transform arithmetic's own scatter),
  `IsReportable` (fails closed on a negative or not-a-number reading), `IsCarryParticipant` (the clone set
  whose 1 Hz line is default-visible), `Anomalies` (what a window must report) and the offset/drift
  arithmetic itself (`Offset`, `Drift`).
- `CarryAnomalies` (Runtime): the three reports of one window — limb separation, rider drift, a live
  relation with no carry pin in the whole window.
- `CarryPresentationProbe` (GameAdapter) OBSERVES and RENDERS: stores the pin's rider-minus-anchor
  reference after the ride pose wrote the root, counts the window's pins, records the drift reading,
  describes the window's readings zero included, and turns the Runtime decision into warnings. It places
  nothing (pinned by a test).
- `RemoteBodyDriver`: the pin reference (carrier, anchor kind, offset), `PinCountInWindow` and
  `PinDriftWindowMax` — on the clone's own component, so nothing outlives the clone it describes.
- `CarriedRiderPresenter` (the renderer until the 2026-09-27 split): the drift is read at the top of the per-clone pass, BEFORE
  `SessionStatePump.Apply`; both carry views store their reference after their own ride pose; the
  participant line is Information and every other clone stays on the Debug position line; the window's
  three readings are read and reset once per second.

Two semantics the review sharpened, both now pinned: a RELEASE drops the reference but never the window's
reading (a drift measured before a release is still reported in that window), and an anchor that is merely
unavailable this frame keeps the reference — the frames that follow measure what accumulated meanwhile.

## 3. Verification

| Claim | How it was checked | Result |
|---|---|---|
| The tolerance is a matrix, and a visible reading cannot hide under it | `CarryPresentationReadingTests` (zero, inside, above, NaN, negative, the threshold's own bound) | pass |
| Who counts as a carry participant, and what a window reports, are decisions rather than adapter conditions | `CarryPresentationReadingTests` — 5 participant facts, the ordinary clone, the quiet window, the pinned rider that must never report "no pin" (the third-party case), the live relation with no pin, a drift without a pin | pass |
| The drift arithmetic is RELATIVE and an offset from an earlier frame is caught | `CarryPresentationReadingTests` — nothing moved, the pair moved together by (100, −7), only the rider moved, a stale offset | pass |
| The wiring: read before the stream write, both stores after their ride pose, the release keeping the window, the read-only reader, the level branch, the warning branches, the window reset | `CarryPresentationProbePinTests` (8 facts) | pass |
| The pin surface exists on the built adapter | `CarriedRiderMountTests.CarryPinReadingSurface_IsOnTheDriver` | pass |
| The pins are load-bearing, not decorative | 9 real-source mutations run by this cycle, all red: 4 in the first round (`%TEMP%/cuo-carry-mut/m1..m4.txt`) and 5 in the fix round (`%TEMP%/cuo-carry-mut2/m6,m7,m11,m12,m14.txt`); sources byte-identical after every restore (`md5sum -c`). The review's own matrix (`%TEMP%/cuo-review-carry-readiness.md` §4.1, 16 rows) had caught 11 and left 5 standing — the inverted level branch, a forced participant fact, a `Clear` that destroys the window, an absolute drift comparison, a store before the ride pose — and all 5 are red here | pass |
| Nothing in the carry family regressed; the build and the repository hold | build 0 warnings / 0 errors (`%TEMP%/cuo-carry-build3.txt`); focused carry/render-proxy filter (`cuo-carry-focus3.txt`, 57/57); `dotnet format` exit 0 (`cuo-carry-format2.txt`); full suite 4438 + 288 gates, exit 0 (`cuo-carry-full2.txt`) | pass |

## 4. The acceptance run this prepares

Precondition: this cycle's readings are in THIS commit. The build on the physical machine is HEAD, which
carries the limb-anchor instrumentation, and the readings reach it through the release-cycle action that
precedes the run — `tools/deploy.ps1 -GameDir "<game-dir>"` followed by `tools/verify-deploy.ps1`. No
`Logging.MinimumLevel` change is needed: a carry participant's line is Information.

1. Carry in both directions (host on guest, guest on host) and walk, turn and crouch while carried.
2. In `BepInEx\LogOutput.log`, find the 1 Hz `Clone …` lines of the clones tagged `carried-rider-clone`.
   `mounted-to-local-carrier` (carrier's own view) and `pinned-to-carrier` (every view) say the pin was
   engaged, and `riderDrift=0` says the pin's placement survived to the frames that rendered.
3. A `Carried rider clone …` WARNING is the reading being reportable, and each one names what it is:
   `limbSeparation` (an exact limb pose left behind its root), `riderDrift` (the clone rendered away from
   the pin's placement, in world units), or a whole window with no carry pin at all. A zero `riderDrift`
   does NOT clear the reported teleport — see §1.
4. Separately, with a limp (dead or unconscious) carried rider, the same line carries
   `limbSeparation=<value>`; zero retires the ticket's 2026-09-07 mechanism note, a non-zero reading is
   the separation a re-anchor would have to remove.

## 5. Limits

- Nothing here verifies the picture. The readings bound what the carry presentation DID — where the clone
  was placed, where the limbs ended up — never what the player saw.
- `riderDrift` cannot see a carrier that moves after its own last pin: the mounted rider travels with it
  through the transform hierarchy, so both sides of the comparison stay together and the reading is zero
  by construction. That window is precisely what the ticket's own root-cause sentence describes, and only
  the two-client run can judge it.
- A frame with no pin is not measured: the first frame of a relation, and any frame whose anchor is
  unavailable, contribute nothing to the window. A window with no pin at all is reported as exactly that
  (the no-pin warning), never as a zero reading.
- `limbSeparation` covers a clone rendering EXACT limb poses (a dead or unconscious rider). A conscious
  rider's clone is animator-driven and is not measured, and the carried local rider needs no anchor
  because the carry follow rather than this pin places it.
- The warnings are per 1 Hz window (the window keeps each reading's maximum), so a separation that lives
  for a few frames is reported, but its exact frame is not.
- No wire member, no protocol change, no carry authority, no release semantics, no placement change:
  recorded as facts.

## 6. Independent review and its disposition

Review: fresh context, frozen tree, report `%TEMP%/cuo-review-carry-readiness.md` — 0 blocker / 4 major /
3 minor / 2 nit, with the eleven reviewed files hashed in its §6 and 15 mechanical mutations run against
its own replication of the pin matchers (11 caught, 4 surviving).

| # | Severity | Finding | Disposition |
|---|---|---|---|
| 1 | major | the `!pinned && !mounted` guard warns "no pin in force" on a third-party view, where mounting is deliberately off, and a cleared pin for one frame repeats it once per second | fixed: the report is decided by `CarryPresentationReading.Anomalies` from the WINDOW's pin count, `mounted` is gone from the decision, and the matrix carries the third-party case (`APinnedRiderIsNeverReportedAsUnpinned`) |
| 2 | minor→major | `Clear` zeroed `PinDriftWindowMax`, erasing a real drift measured just before a release | fixed: `Clear` drops the reference only; the window is reset by the diagnostic alone, and `ReleasedRelation_DropsTheReference_ButKeepsTheWindowsReading` pins it |
| 3 | major | the level pin compared source ORDER, so an inverted branch or a forced participant fact survived | fixed: the level comes from the Runtime `IsCarryParticipant` (matrix + a pin on the five observed facts), and the pin now asserts the Information call's branch and the Debug call's branch |
| 4 | major | the warning pin counted substrings; a warning not conditioned on the rule survived | fixed: the reports are branches of `Anomalies` (Runtime matrix) and the pin asserts each report sits inside its own branch |
| 5 | major | the "RELATIVE on purpose" claim had no test; an absolute comparison and a stale offset were green | fixed: `Offset`/`Drift` are Runtime arithmetic with a four-case matrix, and the store ordering is pinned per carry view |
| 6 | minor | the first, unpinned frame is not measured, and the log did not distinguish it | documented in §1/§5; the window's pin count makes "no pin in the window" a distinct report rather than a zero |
| 7 | minor | "no deploy step and no config edit" was true of the previous cycle only | fixed: the ticket and §4 state the deploy precondition |
| 8 | nit | "presented from the state stream alone" was an inference the probe cannot make | fixed: the warning states the fact ("no carry pin was in force for the whole window") |
| 9 | nit | the threshold was described as float noise while sitting 100× above it | fixed: renamed `ReportThreshold` and documented as a tolerance (half a percent of a body height, two orders above the arithmetic's scatter) |
