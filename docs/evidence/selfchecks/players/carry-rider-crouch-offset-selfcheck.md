# Carry rider crouch-offset continuity — self-check (2026-09-27)

Ticket: `docs/backlog/todo/carry-piggyback-rider-position-smoothing.md` (Critical).

Cycle scope: one placement defect of the carried rider's presentation, found by reading the carry chain
rather than by a run — the rider's height above the carrier was read from the carrier's crouch FLAG while
the game eases the crouch POSE, so one crouch or stand-up stepped the rider 0.4 world units inside a
single frame on every view. No wire change, no authority change, no carry-relation change.

## 1. The defect

`CarriedBodyPlacement.BackOffset` placed the rider with `var up = carrierCrouching ? 0.5f : 0.9f;`, and
all three views fed it the flag:

| View | Anchor | Fact it read |
|---|---|---|
| carrier's own view | the local Body (`CarriedRiderPresenter.AttachAll`) | `localBody.crouching` |
| rider's own view | the carrier render clone (`PlayerInteractionApply.UpdateCarriedBody`) | `carrierBody.crouching` |
| third-party view | the carrier render clone (`CarriedRiderPresenter.AttachAll`) | `carrierClone.crouching` |

The flag is a step; the pose is not. The game flips `crouching` inside one frame while `crouchAmount`
eases (`Body.cs:3095-3099`), and this repository had already fixed that same flag-step once, for the
clone's crouch ANIMATION (`BodyUpdatePatch.UpdateCrouchAmount`). The rider's offset was left on the flag,
so a crouch or a stand-up moved the rider 0.4 world units — about 40% of a body height — in one frame, on
every view: the instant displacement the ticket's Goal and its "crouch changes" acceptance row name, and
the same family as the reported teleport.

## 2. What landed

- `BackOffset(carrierPosition, carrierIsRight, float carrierCrouchAmount)` interpolates the height between
  `BackOffsetCrouchingHeight` (0.5) and `BackOffsetStandingHeight` (0.9) by the eased amount. The lateral
  side and both endpoints are unchanged, an out-of-range amount clamps, and a not-a-number amount falls
  back to upright instead of writing NaN into the rider's transform.
- `ApplyRidePose` / `ApplyLocalRiderPose` / `ApplyCarrierFollow` thread the amount BESIDE the flag:
  `body.crouching` still receives the carrier's flag (it drives the clone's own crouch pose), and only the
  offset reads the pose.
- The two clone-backed views pass the eased value (`localBody.crouchAmount`, `carrierClone.crouchAmount`,
  `carrierBody.crouchAmount`). The one remaining flag read is the pre-clone entity-buffer fallback in
  `PlayerInteractionApply`: it runs for the handful of frames before the carrier's render clone exists and
  has no eased value to read. It is named in the source.
- `RemoteBodyFactory` seeds a fresh clone's `crouchAmount` from the owner's reported crouch instead of
  zero (the independent review's F1). Zeroing it — which the factory did — put a crouching owner's
  rider 0.4 world units too high until the clone's local easing caught up (about 0.37 s), and on the
  rider's own view the fallback-to-clone hand-over stepped the rider inside one frame: the same defect
  this cycle removes, and one `riderDrift` cannot see, because a pin re-derives its offset after every
  placement. The template's stale value is still replaced rather than kept.
- `RemotePlayerRenderer.Update` is split into a READ pass (clone ensure + `MarkCarryRole` +
  `MeasurePinDrift` for every remote member) and a WRITE pass (`SessionStatePump.Apply`), with clone
  creation extracted into a private `EnsureClone`. A drift anchor is another clone's transform, so inside
  one interleaved per-clone loop a rider could be measured against a carrier the same frame had already
  moved: drift for a pair that never separated, a false WARNING in the acceptance run's evidence.

## 3. Verification

| Claim | How it was checked | Result |
|---|---|---|
| The height follows the crouch POSE across its range, and both endpoints are the pre-change values | `CarriedRiderBackOffsetTests.Height_FollowsTheCarrierCrouchPose` — 7 rows: 0 / 0.25 / 0.5 / 1, both facings, out of range in both directions | pass |
| The rider no longer steps: a 2% crouch-pose step moves it by less than 0.02 units instead of 0.4 | `Height_IsContinuousInTheCrouchPose` | pass |
| An unset pose cannot put NaN into the rider's transform | `Height_FallsBackToUprightWhenThePoseIsNotSet` | pass |
| The flag cannot come back as the input | `BackOffset_TakesTheCrouchPose_NotTheCrouchFlag` (the built signature) and `EveryCloneBackedView_PlacesTheRiderFromTheCrouchPose` (the three call sites, by source) | pass |
| Every drift reading precedes the frame's first state write | `CarryPresentationProbePinTests.EveryDriftReading_IsTakenBeforeTheFirstStateWrite` | pass |
| A fresh clone starts at the owner's crouch pose, not upright | `ACloneIsSeededWithTheOwnersCrouchPose` (the factory's seed, by source) | pass |
| Each clone-backed view passes the value itself, not a wrapper | `EveryCloneBackedView_PlacesTheRiderFromTheCrouchPose`, line-anchored, plus a census of the family's flag-derived amounts (exactly one, the pre-clone fallback) | pass |
| The new cases carry the change | mutation: the amount read as a step again (`crouch >= 0.5f ? 0.5f : 0.9f`) → 3 of 13 red, the continuity case reporting "a 2% crouch-pose step moved the rider 0.4 world units"; the source restored byte-identical (`md5 c0002ed50741e7276ddbb2f4680162d9`). Round 2 (the review's F1/F3): the clone seed back to `0f` and one call site wrapped (`1f - localBody.crouchAmount,`) → 2 of 14 red (`ACloneIsSeededWithTheOwnersCrouchPose`, `EveryCloneBackedView_PlacesTheRiderFromTheCrouchPose`); sources restored byte-identical (`md5 0eb4506e8cd2870b6d23aa87d3d66c05`, `7779f5d51b617c2ed040426bf18339ad`) | pass |
| Nothing else moved | build 0 warnings / 0 errors; focused carry filter 108/108; normative gates 288/288; `dotnet format` leaves this change's files as the only modified ones | pass |

## 4. Limits

- Nothing here verifies the picture. What is proven is a placement discontinuity in arithmetic — 0.4
  world units inside one frame — not what the player saw.
- The three views read the eased amount from three different bodies: the carrier's own `crouchAmount` is
  the game's own easing, while a render clone's value is eased locally at `Time.deltaTime * 6f`
  (`BodyUpdatePatch.UpdateCrouchAmount`). The views can therefore differ by a fraction of the transition
  for a few frames — a presentation timing difference, not a difference in the placement rule.
- The pre-clone fallback still reads the flag: it exists for the frames before the carrier's render clone
  is created, which is the one place no eased value is available. With the clone seeded from the owner's
  pose, that hand-over is continuous again.
- A carrier clone destroyed and re-created inside a live relation can still anchor a pin stored before it
  for one frame of false `riderDrift` (the review's F8, pre-existing): the anchor is resolved by SteamId,
  which is the relation's own grain, and "a missing anchor keeps the reference" is a pinned semantic of
  the reading. Recorded here for a readings cycle rather than changed under a placement fix.
- The ticket's acceptance criteria — the teleport on the participant views, the `limbSeparation` reading
  for a limp rider, the third-party view — still await the user's dual-client run on the deployed build.

## 5. Independent review and its disposition

Review: fresh context, read-only, frozen code (the reviewer hashed every reviewed file twice, minutes
apart and identical; the three `docs/` entries that appeared during its window are this change's own
records), report `%TEMP%/cuo-review-carry-crouch-offset.md` — 0 blocker / 1 major / 2 minor / 5 nit,
with HEAD, the working-tree status and the hashes recorded in its own §0. The reviewer ran no build and
no test, so this sheet's numbers were reasoned about rather than re-executed by it.

| # | Severity | Finding | Disposition |
|---|---|---|---|
| F1 | major | a fresh clone seeded `crouchAmount = 0` put a crouching owner's rider 0.4 world units too high until the clone's local easing caught up (about 0.37 s), and on the rider's own view the fallback-to-clone hand-over stepped the rider inside one frame — the defect this cycle removes, and one `riderDrift` cannot see | fixed: the factory seeds the clone's pose from the owner's reported crouch, pinned by `ACloneIsSeededWithTheOwnersCrouchPose` (mutation-red) |
| F2 | minor | the write pass had dropped the old loop's explicit in-world gate, so it wrote stream state to any clone still in the table | fixed: the gate is back in the write pass |
| F3 | minor | the three call-site pins were unanchored substring checks, so a wrapper around the value (`1f - amount`, a rounding) survived the whole matrix | fixed: the pins are line-anchored and the family's flag-derived amounts are censused (exactly one, the pre-clone fallback); mutation-red against a wrapped call site |
| F4 | nit | pinning the fallback's exact expression locked a known limitation into a test contract | fixed: the pin states the reason (one flag-derived amount in the family) rather than the expression |
| F5 | nit | `Mathf.Clamp01` is redundant beside `Mathf.Lerp`'s own clamp of `t` | accepted: kept as the stated contract, and the comment now says which guard does what |
| F6 | nit | the NaN guard covers the amount, not `carrierPosition` | accepted: the carrier's own transform is the game's own, and hiding a NaN there is not this rule's job; the comment now claims only the amount |
| F7 | nit | the change's summary said "before any clone is written" while the read pass creates clones | fixed: the claim is the code's own — before the frame's first state write — in the pin's name, the ticket and this sheet |
| F8 | nit | a carrier clone destroyed and re-created can anchor a pin stored before it, for one frame of false `riderDrift` | recorded, not changed: pre-existing, the anchor is resolved by SteamId by design, and the keep-the-reference-on-a-missing-anchor semantic is pinned; a readings cycle's job |
