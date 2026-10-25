# Carry: picking a player up and putting them down

Carrying a teammate on your back, and being carried.

## What the player does

Asks the game to carry a teammate (or be carried), lifts them, walks with them and puts them down. The player expects the
carried body to ride along, to sit in the ride pose, and to stop being carried when the carrier is no longer able to carry
— not to keep hanging in the air or to drop into an unrelated animation.

## What the game itself does

Carrying is the game's own carry relation between two bodies; the carried body is carried by a live carrier and both
bodies' presentation follows from that relation.

## Rules and states

- A carry relation requires a LIVE carrier: a carrier who dies, is knocked down or leaves the world ends the relation, and
  the carried body is put down rather than left attached.
- The carried body rides in the ride presentation. The accepted record fixes the whole family: "The rider's own client
  never publishes `Sitting=true` while carried", "the carrier-side rider clone is marked as a carried rider and never
  replays the native sit clips from the entity stream", "a body that was already in `ExperimentSit`/`ArmsSit` when carry
  began is actively returned to the `Grounded`/ride presentation instead of lingering", and "the carried-ride `idleTime` is
  held at zero every frame, so the native sit condition cannot" fire.
- The carry is a body pose fact, not a seating: being carried never turns into the native idle-sit behaviour no matter how
  long the rider is without input.
- **The carrier cannot sit while carrying.** The carrier-side half of the same family was its own defect: "while the guest
  carries the host on their back, the guest (carrier) is still able to sit on the ground".
- **The rider's own body keeps simulating.** The carried body is driven by a carry driver whose presence used to skip the
  body's own per-frame simulation — "While this component is present the body's own per-frame simulation is skipped
  (`BodyPatches` treats it like a render proxy) and `GameAdapter` drives its transform from the carrier's entity state each
  frame" — and the accepted goal is the opposite: "A carried rider's own client keeps its body's native per-frame simulation
  running; the carry relation drives position and pose through the game's own mechanisms instead of freezing the body." A
  frozen body showed as a flat ECG and twitching limbs on the rider's own medical panel, because a skipped simulation leaves
  the vitals at their last values.
- The same rule covers a DEAD or unconscious carried body: it keeps its own per-frame simulation too, since and the
  presentation of a body that stopped simulating is wrong for the party.
- **The rider sits at the same height on every screen.** An earlier asymmetry put the rider above the carrier on one side and below on the other: "when the host rides on a guest's back, the host's own view shows the host body higher than the guest, while …".
- **The rider's position follows smoothly.** A rider that teleports to the carrier's position is the defect the smoothing
  family fixed: the accepted reading for the third-party view leaves a remainder of "≤1 px interpolation".
- The relation is one carrier and one carried body at a time, and the menu's carry action is only offered where the
  relation can exist.

## Refusals

| Condition | The player reads |
|---|---|
| the target is already carried, or the carrier is already carrying | the action is not offered |
| the carrier cannot carry (dead, downed, left the world) | the carry ends; nothing is left attached |

## Who judges

Each client presents the relation from the body state it holds: the rider's own client suppresses the sit presentation, and
the carrier's clone replays the ride rather than the native sit clips. Whether the relation holds is the host's body-state
fact.

## What travels

The carry relation as body state: who carries whom, and the pose the carried body is in. No presentation travels.

## What the player sees and hears

A teammate on the carrier's back, in the ride pose, moving with the carrier — and put down cleanly when the carry ends.

## Records

Decision 155 ("Carry relations require a live carrier; kernel invariant"). Accepted:
`../../backlog/done/carried-player-idle-sit-suppression.md` — its accepted cycle introduced "a single pure carried-ride pose
rule (`CarriedBodyPose`)" applied across the presentation family.

## Gaps

- Whether a carried player can act (use an item, be treated) while carried is not recorded here yet.
- The item-transfer path between a carrier and a carried body is not recorded here yet.
