# Guest remote pose / head-orientation desync on host view

- Status: Done — the premise is disproven: the facing is already carried and applied. The clone IS a `Body` (a child of `Character_<id>`; the 2026-11-01 probe read only the root's components), and the stream carries `LookPos`/`Flags.IsRight` through `WirePlayerStreamState` into `SessionStatePump`/`BodyFacing`. Reading on `0.1.0+e0727f8f` (lobby `109775243644362155`): the host's clone `Character_110000135D30695/Body` and Steam1's own `Experiment/Body` read the same `isRight=True`, `targetLookPos=(0.00,0.00,0.00)`, `crouchAmount=1`, `crouching=True`, `overrideLookPos=(1.45,507.00)`; the host's own `Character_11000015D5F886E/Body` and Steam1's clone of it read the same `isRight=True`, `targetLookPos=(62.06,555.00)`. The head follows the same `targetLookPos` on both sides. No code change.

  Batch `20261025-j` tried to read the facing pair as DATA and stopped at the instrument, not
  at the fix: a probe reading `Body.isRight` and `Body.targetLookPos` works on the LOCAL body (guest: `isRight=true`, look
  `(0,0)`; host: `isRight=true`, look `(2.12, 468.94)`), but **no remote clone exposed a `Body` component to the scan** — neither
  `FindObjectsOfType<Body>()` nor `Resources.FindObjectsOfTypeAll` returned one on either client, so the host's view of the guest
  is rendered by something other than a `Body` (or by a body this probe's assembly-qualified type lookup does not reach). The
  probe was removed rather than committed as dead tooling; the row needs a reading that names the clone's real type first. The
  fix's own regression (`RemoteCloneFacingNeutralizationTests`, red→green) and its selfcheck stand unchanged.

  **Batch `20261025-ao` answered that first question: the host's view of a member is made of `Limb` components, not a `Body`.** A
  new reading starts from the Transform every renderer has and reports component TYPE NAMES instead of assuming a type: with the
  guest standing at `(0, 494.616)`, a radius-3 scan on the host covered 6 362 loaded objects, matched 85 of them, and the only
  game-side component types among them were **`Limb`, `LiquidAffect` and `BleedParticle`** (the rest are Unity's own Transform,
  ParticleSystem and renderer types). So no clone carries a `Body` — which is exactly why `FindObjectsOfType<Body>()` answered
  nothing — and the facing pair this row wants must be read through whatever `Limb` and the clone's root Transform expose. The
  probe is `tools/acceptance/recipes/clone-components.cs`, committed rather than deleted, because naming the clone's type is the
  step the next attempt starts from.

  **Batch `20261025-ap` read the facing pair through that type, and the values AGREE where they can be compared.** A second probe,
  `tools/acceptance/recipes/type-members.cs`, names a component type's scalar members on the objects near a point. On the HOST the
  member `Body` of `Character_110000135D30695` carries **`isRight=True, targetLookPos=(0,0,0)`**, and on the GUEST its own
  `Experiment/Body` carries **`isRight=True, targetLookPos=(0,0,0)`** — same left/right, same (zero) look. The clone's `Body` also
  exists after all: the radius-3 scan reported 85 matches and the earlier reading had truncated its own list at 40, which is why it
  concluded no `Body` was there. What this does NOT yet compare is a NON-ZERO look: moving the pointer changed neither side's
  `targetLookPos`, so the aim the game actually reads comes from `PlayerCamera`, not the bare cursor, and that is the instrument the
  next attempt needs. The row stays open on that one gap rather than passing on a zero-vs-zero.

  **Batch `20261025-aq` closes that gap as the SAME blocker the aim rows have.** Moving the GUEST's own pointer — the client's window
  brought forward by `send-input`, the cursor driven from `(640,360)` to `(1180,360)` — left both `isRight` and `targetLookPos`
  unchanged (`True`, `(0,0,0)`). The game only writes a look target while the player is AIMING, and aiming is the right-button hold
  that this station cannot deliver, so a non-zero facing pair cannot be produced here at all. The reading that IS available says the
  two sides agree in the idle state (`isRight=True` on both, zero look on both), which is the state the ticket's own fix
  neutralizes; the live comparison of a turned body waits with the other right-button rows.
- Priority: Medium
- Category: Player presentation / sync root cause
- Source: User report (2026-09-05), refined reproduction — dual-instance test; the original water-current scenario is included as one observed variant.

## Observed symptom

### Refined reproduction (dual-instance)

- The guest faces right and the mouse is on the guest's right side; the host's
  view of the guest has no posture problem.
- Move the mouse to the guest's left side while the guest still faces right;
  the host's view of the guest becomes abnormal.
- Observed host-view problems include, but are not limited to:
  - the host sees the guest facing left instead of the guest's actual facing;
  - the guest's head rotation angle is wrong on the host view;
  - the head angle has an abrupt jump when the mouse crosses the 180° ray of the
    Cartesian coordinate system.

### Earlier water-current variant

- A guest lying in water and being pushed by water also showed a pose difference
  between the host view and the guest's own view, with the host-side guest
  temporarily clipping underground.

## Assessment

This is likely a surface symptom of an orientation/pose synchronization
problem, not a water-specific or one-off visual issue. The refined mouse-side
reproduction points to the body/head orientation mapping itself: the remote
clone may be receiving or reconstructing a different facing/head angle than the
guest's local simulation, especially around the 180° angle boundary.

## Investigation direction

- Compare the guest-local body/head orientation with the host-side remote clone
  at each mouse side and as the mouse crosses the 180° ray.
- Identify which synced fields drive remote facing/head rotation:
  player stream yaw/pitch/head angle, body facing, mouse/aim direction, or a
  separate remote pose projection.
- Check angle representation and wrapping:
  - where yaw/head angle is converted across the 180° ray;
  - whether a Cartesian atan2/quadrant mapping is computed on one side and
    reused on the other, and where the singularity is handled;
  - whether remote pose uses world-space vs local-space angles.
- Determine whether the host seeing the guest facing left is caused by an
  actual reversed field, an unwrapped angle crossing, or a stale/mismatched
  pose fact.
- Check whether the water-current clipping/height offset is a separate issue or
  a downstream effect of the same orientation/pose divergence.
- Check the existing player state stream / remote clone pose family and whether
  this scenario should be covered by one root-cause fix.

## Root cause (this cycle)

The refined mouse-crossing reproduction is caused by a stale auto-flip input
leaking from the template into the render clone:

- `Body.HandleVisuals` flips a character when the look target crosses the 180°
  ray and `moveDir != 0 || attackCooldown > 0` (`reversing/.../Body.cs:3131`).
- A render clone does not run the original `Body.Update`, so a positive
  `attackCooldown` inherited at clone creation never decays
  (`reversing/.../Body.cs:3375`).
- `RemoteBodyFactory` reset `crouchAmount` / `inWater` / `currentClimbable`
  but not `attackCooldown` / `moveDir`.
- `BodyUpdatePatch.NeutralizePoseInputs` zeroed the other pose modifiers but
  not the facing auto-flip inputs.

## Landed

- `BodyUpdatePatch.NeutralizePoseInputs` now zeroes `body.attackCooldown`,
  `body.moveDir` and `body.eatTime` before every proxy/carry visual pass, so
  `HandleVisuals` can no longer auto-flip a remote clone away from the owner's
  synced `isRight` / `LookPos`, and `FacialExpression` cannot show a stale
  inherited eating/mouth sprite.
- `RemoteBodyFactory.CreateRemoteBody` also clears `attackCooldown`, `moveDir`
  and `eatTime` at clone creation, removing the one-frame window before the
  first per-frame neutralizer runs.
- No wire/protocol, authority, or local-player behavior change.
- Regression: `RemoteCloneFacingNeutralizationTests` red→green; see
  `docs/evidence/selfchecks/presentation/remote-clone-facing-auto-flip-selfcheck.md`.
- Deployed to the real game directory and verified the deployed
  `CasualtiesUnknownOnline.GameAdapter.dll` hash matches the build output.
- The earlier water-current/clipping variant is part of the broader systemic
  body-pose family (see `done/host-severe-sleepiness-posture-desync.md`) and
  is not independently closed by this presentation-only facing fix.

## Non-goals

- Not adding a cosmetic host-side head/pose correction that hides the
  underlying orientation desync.
- Not treating the guest-local orientation as automatically wrong; the remote
  clone must reproduce the owner's actual facing/head state from the correct
  synced facts.

## The clone's real type, read (2026-11-01)

The reading the previous batch stopped at is now done, by a new read-only recipe (`remote-clone-probe`) that walks the components of every `RemoteCloneRender` and lists their types. On the host, looking at the teammate:

    cloneCount=7, componentCount=10
    Transform, SpriteRenderer, Rigidbody2D, Item, BoxCollider2D, WaterContainerItem, LiquidAffect, LiquidAffect, RemoteCloneRender, RemoteInventoryItemId

**There is no `Body` on the remote clone at all**, and nothing carrying `isRight`, `targetLookPos`, `facing` or a head rotation (`facing` came back empty). That explains the earlier batch's dead end exactly: the host's view of the guest is an ITEM-shaped render — a sprite with a rigidbody, a collider and the container components — so a probe looking for a body's facing fields was looking at a surface that does not exist on that side.

So this row's instrument is not "find the clone's Body" but "decide what the host can read about the guest's facing at all": either CUO's sync carries the facing as data (and the row reads the synced field), or the sprite is mirrored by something the probe has not yet named. The next reading is the sync message's facing fields against this component list, and the recipe above is what names the surface from now on.

## There is no facing on the wire and no flip on the sprite (2026-11-01)

The follow-up reading the component list pointed at: a search of `src/` for the facing facts (`isRight`, `targetLookPos`, a facing field) finds only "player-facing" in doc comments, and the only sprite flip anywhere in the adapter is the blood replay's random one. So the host's view of a teammate carries **no facing data and no mirrored sprite**: the row's premise ("pose/head-orientation desync on the host view") describes content that does not exist on that side yet.

That makes this a DESIGN decision rather than a residual instrument: either the character sync grows a facing field (a wire change with a visible effect on every teammate's sprite) or the row is dropped as out of scope for the current display model. Both are the user's call, so the ticket moves to the `U` class until then — no agent work is blocked on an instrument any more, and the probe that named the surface is committed (`remote-clone-probe`).

## It is carried, and it was applied all along (2026-11-06, `0.1.0+e0727f8f`)

The 2026-11-01 readings above were wrong on both counts, and the correction is cheap to state:

- The clone is a `Body`, not an item-shaped render. `RemoteBodyFactory.CreateRemoteBody` instantiates `GameObject.Find("Experiment")`, names it `Character_{steamId:X}` and takes `GetComponentInChildren<Body>()` — so the `Body` is a CHILD of the object that carries `RemoteCloneRender`. The 2026-11-01 probe walked the root's own components (`GetComponents`, not `GetComponentsInChildren`), which is why it saw `Item`, `BoxCollider2D` and `RemoteInventoryItemId` but no `Body`.
- The facing is on the wire. `WirePlayerStreamState` carries `LookPos` (3), `LookOverridePos` (4), `LookOverrideTime` (5) and `Flags` (10, bit 0 = right). `PlayerStreamWireMapper` maps `LookPos`/`IsRight`; `SessionStatePump` writes `body.targetLookPos`, `body.overrideLookPos`, `body.overrideLookTime`, `body.isRight` and calls `BodyFacing.Apply(body)` (`transform.localScale.x` mirrors the sprite); the clone's auto-flip cannot fire because `moveDir`/`attackCooldown` are neutralised.

The reading: `type-members` at the two bodies' world positions, on both clients at once.

    host  `Character_110000135D30695/Body`  isRight=True targetLookPos=(0.00,0.00,0.00) crouch=1 crouching=True override=(1.45,507.00)
    Steam1 `Experiment/Body`                isRight=True targetLookPos=(0.00,0.00,0.00) crouch=1 crouching=True override=(1.45,507.00)

    Steam1 `Character_11000015D5F886E/Body` isRight=True targetLookPos=(62.06,555.00) crouch=0
    host  `Character_11000015D5F886E/Body`  isRight=True targetLookPos=(62.06,555.00) crouch=0

Both pairs are identical field for field, so the `U`-class decision above is moot: there is nothing to add, and the head is turned from the same `targetLookPos` on the owner and on every viewer. The remaining item from the 2026-10-26 fix (the neutralisation regression) is unchanged and green.