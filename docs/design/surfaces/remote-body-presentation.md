# Remote body presentation: what another player's body shows

The face, the posture and the pose of a teammate's body on your screen — and the sounds their body makes.

## What the player does

Looks at a teammate and reads their state from the body: a squint when they are desperately sleepy, a slouched posture
when they cannot stand straight, a mouth that opens when they are hurt, the pose they are in while carried or sitting, and
the sounds the body makes — eating, drinking, a heartbeat.

## What it is

A projection: every client animates the other players' bodies from the state it holds. The face-driving and posture-driving
values ride the character data (the 1 Hz stream), and one-shot sounds ride their own relay.

## Rules and states

- **A player picks a colour, and the head tag is only the colour.** Names above heads are colour-only tags by design ("Player-selectable colors and color-only head name tags"), so a tag is not a text label with a colour attached; the colour is the player's choice and the tag shows it.
- **A ragdoll is a body state, not a local animation.** A body that has gone limp shows as limp to the others ("Remote ragdoll state not visible" and "Ragdoll limb pose not synced" are its two records), so the ragdoll state and its limb pose travel as body state rather than being played only where the body fell.
- **A body's state reaches every client's view, not only its owner's.** The family's own records name each case as it was
  found: "High sleepiness squint not visible remotely", "Host severe sleepiness posture not synced to guest" (a body that
  "cannot stand straight (posture is visibly slouched/bent)"), and "Host fall injury mouth-expression desync" where "on the
  guest's view the host's mouth opens, but on the host's own view" it did not.
- **The values that drive the face and the posture are character data**, delivered at the character-data rate rather than
  per frame; a presentation that needs a finer cadence names it.
- **One-shot character sounds are relayed with their clip identity.** A consumption plays the game's own clips — the
  accepted reading names them: "the guest and a third client each replayed the exact `Consume` clips (`eatCrunch`,
  `drink`)".
- **A carried body's pose is a pose rule, not a seating** (see `../mechanics/carry.md`): the ride presentation replaces the
  native idle-sit family on both the rider's and the carrier's side.
- A presentation never decides anything: it shows the state the owner produces.

## Refusals

None of its own: a presentation cannot refuse. A body whose state has not arrived shows the last state it had, and the
ticket that finds a stale presentation names the value that never arrived.

## Who judges

The body's owner produces the state; every other client draws it. A presentation claim of the owner's (a sound, an
expression) travels like any other one-shot fact.

## What travels

The face-driving and posture-driving values in the character data; one-shot sounds with their clip identity and their
position.

## What the player sees and hears

A teammate's face, posture and pose matching their state, and the sounds their body makes at the moment it makes them.

## Records

Accepted: `../../backlog/done/high-sleepiness-squint-not-visible-remotely.md`,
`done/host-severe-sleepiness-posture-desync.md`, `done/host-fall-injury-mouth-expression-desync.md`,
`done/host-eating-sound-not-heard-on-guest.md`, `done/carried-player-idle-sit-suppression.md`. Open:
`../../backlog/review/sync-player-pain-vocalizations-and-bark.md` (the one-shot relay the eating sound extended).

## Gaps

- The heartbeat's own case is recorded as CLOSED BY INSTRUCTION rather than fixed: its ticket says it was "closed at the
  user's instruction ('just consider it done'), not by a fix", with the finding that more than one source plays the same
  per-beat clip (`heartthump-heavy` / `-monitor` at `time 0 / 0.769 s`). That is a known, accepted limitation.
- Which presentation values are per-frame and which are character-data rate is not tabulated here yet.
