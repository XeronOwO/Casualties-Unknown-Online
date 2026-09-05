# Carry/piggyback riding movement teleport and rider/carrier position mismatch

- Status: Todo (rejected in review)
- Priority: Medium
- Category: Player interaction / movement sync / carry-piggyback presentation
- Source: User report (2026-09-04); rejected by user (2026-09-05) — the fix only covers half of the carry presentation family.

## Rejected

User reports the fix is only half done:

- When the carrier moves, the carrier still sees the rider misaligned/teleporting;
  the rider's own view is normal.
- The reverse direction also shows the same class of problem.
- In short, the carried/ridden presentation still has a visible mismatch on the
  side that should be showing the carried player; the fix did not cover every
  participant/third-party view.

This ticket is re-opened as TODO. The next implementation must satisfy the
acceptance criteria from all relevant views, not just the rider's own client.

## Landed

Implemented the shared carried-ride presentation path:

- The rider's own client now follows the remote carrier's RENDER clone (already
  smoothed by `SessionStatePump`) instead of the raw 20 Hz entity buffer.
- `GameAdapter.Update` runs `UpdateCarriedBody` after `RemotePlayerRenderer.Update`,
  so the carrier clone receives this frame's interpolation before the rider uses it as the anchor.
- One shared `CarriedBodyPlacement.ApplyRidePose` replaces the duplicated
  field-by-field placement in both participant paths, so position, velocity,
  facing, crouch, standing/move-dir and look target cannot diverge.
- The rider's stream now publishes the body root while carried (not the
  ragdoll torso anchor), so third-party peers use the same reference point as
  the two participant views.
- After all clones are interpolated each frame, `RemotePlayerRenderer` pins
  every remote rider clone to its carrier's visual position — not just on the
  local-carrier screen, but also on third-party screens. Therefore the pair is
  rigidly attached everywhere and interpolation cannot introduce visible
  detachment.
- 1 Hz clone diagnostics tag carried-rider/carrier clones.

No carry authority, wire protocol, release semantics, or host rules changed.

Selfcheck: `docs/evidence/selfchecks/players/carried-rider-placement-smoothing-selfcheck.md`.

## Goal

Make carried/piggyback rider movement visually stable on both participant sides. The rider should stay continuously attached at the expected back offset while the carrier moves; neither participant should see frame-level teleporting or a noticeable mismatch between the carrier and rider positions.

## Current implementation

- Carry/piggyback is a host-authoritative relation; each client still simulates only its own body. The carried player's body is moved by its own client; peers see it through the normal 20 Hz player stream.
- The rider's own client marks its local body with `CarriedBodyDriver`, skips its normal physics/simulation, and each frame sets the body transform directly from the carrier's remote entity buffer:

  - `src/CasualtiesUnknownOnline.GameAdapter/PlayerInteractionApply.cs:69-101` — `UpdateCarriedBody` reads `carrier.Position` / `carrier.Velocity` / `carrier.IsRight` / `carrier.Crouching` directly and writes the body to `CarriedBodyPlacement.BackOffset(...)`.
  - `src/CasualtiesUnknownOnline.GameAdapter/Character/CarriedBodyPlacement.cs:20-25` — the back-offset placement rule.

- Remote render clones use a separate smoothing path:

  - `src/CasualtiesUnknownOnline.GameAdapter/Character/SessionStatePump.cs:15-66` — renders remote clones by interpolating between `PrevPosition` and `Position` using an EMA-averaged snapshot interval.
  - `src/CasualtiesUnknownOnline.GameAdapter/Character/RemotePlayerRenderer.cs:157-192` — applies `SessionStatePump` to every remote clone, then `ApplyLocalCarrierFollow` overrides the clone for the player the local player is carrying.

- Update order is relevant:

  - `src/CasualtiesUnknownOnline.GameAdapter/GameAdapter.cs:166-192` — `Run.Update` (which publishes the local body state) runs before `PlayerInteractionApply.UpdateCarriedBody` (which moves a carried local body), and `Renderer.Update` runs later in the same frame.
  - `src/CasualtiesUnknownOnline.GameAdapter/Run/RunCoordinator.cs:144-149` — `PublishBodyState` captures the local body transform at the beginning of the frame, before the carried-follow adjustment.

- Existing design statements:

  - `docs/evidence/selfchecks/players/carry-interaction-selfcheck.md:27-30` — the carried body reports its own position through the ordinary streams because peers do not need a carry-specific render network.
  - `docs/evidence/selfchecks/players/player-interaction-followups-selfcheck.md:35-39` — carrier-side real-time follow pins the carried remote clone to the local body; the rider still reports its own authoritative position through the stream.

## Reported symptom

- Host rides guest, or guest rides host, while moving:
  - both participants see a degree of teleporting / frame snap;
  - the two players' positions and the riding position do not match at some frames;
  - the mismatch is visible from both sides of the relation.

## Likely causes to verify during implementation

1. **Rider uses raw network state, not the render-smoothing path.** `UpdateCarriedBody` writes the local body from `carrier.Position` (latest 20 Hz buffer), while the same client's visible carrier clone is interpolated by `SessionStatePump`. The rider body can therefore step/jump and appear offset from the smoothly moving carrier on the same screen.
2. **Publish/apply order adds one-frame staleness.** `Run.Update` publishes local body state before `UpdateCarriedBody` moves it. While carried, the outgoing position may lag the visible rider placement by one frame or more, which can contribute to peers seeing snap/mismatch.
3. **Carrier-side override is local only.** The carrier client pins the rider clone to its own body, but other peers only have the rider's independently reported stream. If the rider's client steps or lags, a third party may see the rider detached/teleporting relative to the carrier.
4. **Back-offset transitions** (facing flip, crouch) are written directly to a frozen body/clone rather than through the same interpolation, so any facing/crouch change can cause an instantaneous one-frame offset.
5. There is no current runtime/L0 coverage for riding-motion smoothness or for comparing the carried body placement against the carrier's visual transform.

## Required design direction (for the implementation cycle)

- Define one consistent rider-placement path used by:
  - the rider's own client (place its frozen local body),
  - the carrier's client (pin the remote rider clone),
  - third-party peers (attach or phase-align the rider presentation to the carrier in a deterministic way).
- Decide whether the rider should follow the same interpolation target used by `SessionStatePump` (e.g. the carrier's interpolated clone transform or an equivalent computed `Lerp(PrevPosition, Position, alpha)`), rather than the raw latest buffer.
- Ensure the fix preserves the architecture rule: each player only simulates its own body; no remote-carrier motion simulation or client prediction is added.
- Keep the carry relation authority unchanged unless a wire change is genuinely necessary (e.g. a deterministic offset/phase field). Prefer presentation/ordering-only fixes first.
- Add observable logging/diagnostics for the carried-body position vs carrier position during movement so runtime verification can show whether the mismatch is eliminated.

## Acceptance criteria (for the later implementation cycle)

- Host-on-guest and guest-on-host movement no longer shows frame teleport on the two participant views.
- The rider remains at the expected back offset relative to the carrier during movement, facing changes, and crouch changes.
- Any third-party view of the same carry relation also keeps the rider visually attached within the same tolerance as normal remote-player smoothing.
- No change to carry authority or the carry relation invariants.
- Existing carry/release/UI tests, build, format, and repo gates pass.
- New tests or runtime evidence cover the placement/smoothing behavior (including at least the two movement directions and a facing/crouch-change edge case).

## Non-goals

- Not adding client prediction or remote-side simulation of the carrier.
- Not changing carry/release authority, host rules, or the carry relation lifecycle.
- The presentation/order-only fix landed in this cycle; see the selfcheck linked above.
