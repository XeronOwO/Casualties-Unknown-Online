# Carried clone root/limb separation — self-check (2026-09-25)

Ticket: `docs/backlog/todo/carry-piggyback-rider-position-smoothing.md` (Critical).
Cycle scope: settle whether a carried rider clone's exact limb poses are left behind when the ride
pose re-pins its body root, and ship the instrumentation that answers it in a real session. The
cycle began as a re-anchor fix and ended as a measurement: the mechanism it was built on is not
supported by the current code, and the independent review deleted the fix before the commit.

## 1. Mechanism inventory — the hypothesis against the code

| # | Claim / mechanism | Evidence | Verdict |
|---|---|---|---|
| 1 | Hypothesis (the ticket's 2026-09-07 note, step 4): with an exact pose active, `HandleVisuals` no longer re-attaches the limbs, so a root re-pinned every frame leaves "the visible limbs … at the previous world coordinates until the next pose tick" | the ticket's own record; the same note states the teleport was never re-tested after that cycle | **not supported**, and never verified at runtime |
| 2 | The visible limb transforms are children of the body hierarchy, so a root write carries them | `reversing/Assembly-CSharp/Assembly-CSharp/Body.cs`: `HandleVisuals` places a limb with a LOCAL write derived from the animator node (`limb.transform.localPosition = this.bodyAnimator.transform.InverseTransformPoint(limb.animLimb.transform.position) …`), and `Body.Stand` moves the root and then counter-moves every limb by the same amount (`base.transform.position += Vector3.up * num2;` then `array[i].transform.position -= Vector3.up * num2;`) | contradicts #1 |
| 3 | A remote clone's limb rigidbodies are frozen, so the transform hierarchy is that clone's only driver | `RemoteBodyFactory.CreateRemoteBody` freezes every `Rigidbody2D` on the clone; `BodyUpdatePatch` re-freezes them every frame | contradicts #1 |
| 4 | The carried LOCAL rider is the control case: `CarriedBodyPlacement.ApplyLocalRiderPose` writes its root on every carried frame with the root and every limb rigidbody frozen and nothing re-anchoring them | the method itself; no report describes the rider's own body coming apart | contradicts #1 |
| 5 | The population the ticket names is real at the code level: a dead or unconscious carried body still publishes exact limb poses and its clone renders them as exact transforms | `CarriedBodyPose.ShouldPublishExactLimbPoses(isCarried, alive, conscious) => !isCarried \|\| !alive \|\| !conscious`; `LimbPoseCapture.Capture` guards only on standing/sleeping; `RagdollPoseApplication.Apply` writes them and sets `RagdollPoseActive` | confirmed — which is why the question is worth measuring |
| 6 | The root of a carried clone has more than one writer per frame | `SessionStatePump.Apply` writes `body.transform.position = position;` every frame, then the ride pose writes it again (`ApplyRemoteCarrierAttachAll`, from `Renderer.Update`, the post-`Body.Update` re-pin and `Plugin.LateUpdate`) | confirmed — the anchor's capture root is the stream root, not a ride-pose root |

Conclusion: the shipped hierarchy gives the opposite answer to #1, so no placement change may be
built on it. What the cycle ships is the measurement that settles it on a screen.

## 2. What landed

- `CarriedLimbAnchor` (Runtime, pure): the reference shape of the applied exact poses as
  root-relative offsets in application (parents-first) order, the gate
  `ShouldMeasureAgainstPinnedRoot(isCarriedRider, hasExactLimbPose)`, and `TryTarget`.
- `RagdollPoseApplication` captures that shape as it applies the poses, and
  `MeasurePinnedRootSeparation` reads every rendered limb against it. **It writes nothing**, and
  `CarriedLimbAnchorTests.PinnedRootMeasurement_DoesNotMoveAnything` is the pin that keeps it that way.
- `CarriedBodyPlacement.ApplyRidePose` takes the reading right after it writes the root —
  `CarriedLimbAnchorTests.RidePose_MeasuresAfterWritingTheRoot` pins that ordering.
- The 1 Hz clone diagnostics print `limbSeparation=<largest reading in the window>` for every clone
  rendering exact poses, zero included (so "measured zero" and "not measured" cannot be confused),
  and reset the window unconditionally. It is a Debug line: a session that wants it sets
  `Logging.MinimumLevel=Debug`, the same prerequisite `CarrySimulationTrace` documents.
- Release and detach clear the reference shape and the window.

Why the reading discriminates: the reference target is `offset + pinned root`, while a limb that did
not follow the root still sits where the pose stream wrote it, so the reading is the distance between
the two — zero if the hierarchy carried the limb (expected), and non-zero, in game units, if it did
not. That is the number a re-anchor would have to remove.

## 3. What was deleted, and why

The first cut of this cycle shipped `FollowPinnedRoot`: it translated every captured limb to
`offset + pinned root` on every rendered frame and reported the root travel it removed as
`limbReAnchor`. The independent review (`%TEMP%/cuo-review-carry-limb-anchor.md`, 1 blocker /
4 major / 6 minor / 4 nit) showed that this is idempotent by construction and therefore writes what
the transform hierarchy has already written — the change's own "safe whatever the hierarchy does"
wording was the same fact stated positively — and that it rested on the unverified step 4 above. The
write path, its call site, the rule `ShouldRidePinnedRoot`, `RootTravel` and the `limbReAnchor` field
were deleted in the same commit before any of it was committed; this file and the ticket record the
finding instead. Deleting rather than keeping a harmless guard is deliberate: the repository's rule is
that an unverified mechanism is not implemented, and the measurement is what turns it into a verified
one.

## 4. Verification

| Claim | How it was checked | Result |
|---|---|---|
| The gate is a matrix, not a buried condition | `CarriedLimbAnchorTests` — carried rider with exact poses measured, non-carried clone and pose-less rider not | passed |
| The reference shape is captured, ordered and cleared correctly | `CarriedLimbAnchorTests` — offsets, parents-first order, tick replacement, clearing, out-of-range order | passed |
| The ride pose reads after writing the root, and the probe moves nothing | source pins in the same file | passed |
| The built adapter exposes the measurement surface | `CarriedRiderMountTests.CarriedLimbMeasurementSurface_IsWiredIntoTheRidePose` (reflection: static, `void`, `Body`, plus `LimbAnchor`/`LimbSeparationWindowMax`) | passed |
| Nothing in the carry family regressed | `dotnet test tests/CasualtiesUnknownOnline.Tests --filter "FullyQualifiedName~Carr\|FullyQualifiedName~RenderProxy\|FullyQualifiedName~Ragdoll"` | 257 passed / 0 failed |
| The build is clean and the whole suite holds | `dotnet test CasualtiesUnknownOnline.slnx` (with build) | 3918 tests passed / 0 failed, 0 warnings; gates 149/149 |
| The runtime half is readable in a session | the 1 Hz clone diagnostic's `limbSeparation` field at Debug level | log-text review; the session is the run's |

## 5. Limits

- The hypothesis is neither confirmed nor refuted here. Zero `limbSeparation` in a session means the
  hierarchy carried the limbs and the ticket's step 4 should be corrected in place; a non-zero value
  is the separation, in game units, and makes the re-anchor the fix to land then.
- Nothing in this cycle changes a rendered frame: it adds a read and a log line. The reported teleport
  itself is still unverified — the ticket records that it has not been re-tested since 2026-09-07.
- The measurement covers a carried rider clone that renders exact poses (a dead or unconscious rider).
  A conscious carried rider's clone is animator-driven and is not measured; the carried local rider is
  not measured because no anchor is needed for it (the control case in §1 row 4).
- The parent chain of the visible limbs was read from decompiled `Body.cs`, `RemoteBodyFactory` and
  CUO's own comments; the shipped prefab was not opened, and no frame was rendered.
- `limbSeparation` is a Debug line: an acceptance session must raise `Logging.MinimumLevel` to Debug,
  or the field is not written at all.
