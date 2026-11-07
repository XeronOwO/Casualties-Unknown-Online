# mod-cross-player-native-semantics — the topical row's release shapes (20261107-o)

Batch on `0.1.0+3005409c`, host 18590 + Steam1 18591 in lobby `109775243733797288`. The ticket's Part B
"Topical entry correction" names two acceptance rows: (a) a topical container released on a teammate in
the world drops instead of applying anything; (b) a wound-view release applies the dose to the limb the
operator picked, on the patient's own screen. This record covers the shapes attempt `20261107-n` could
not reach, and says plainly which row each reading does and does not settle.

## The fixture

The host held a fresh `paincream` (topical carrier, `usableOnLimb`); Steam1 was wound-seeded so the
dose, if one landed, would show:

- `wound-seed limb=5 force=300 infection=0` → `skinHealth=-78.867, bleedAmount=46.048, pain=113.667`
- Steam1 teleported to `(1, 501.9419)` (the game snapped it from `(1.803524, 500.6154)`); the host stood
  at `(0, 500.615)`.
- `screen-point2 x=1 y=501.9419` → the guest's ground point at `(688, 559)` in the host's pixels.
- `ring-buttons` with Steam1's displayed backpack open → six buttons; the ring's centre is their
  average, read twice: `(660, 535)` and, after the display moved with the focus, `(823, 44.5)`.

## Readings

Release 1 — on the ground beside the teammate (pixel `(688, 559)`), backpack closed:

    casts="" hadItem=paincream(Clone) after=none mode=World backpackOpen=False
    focusedSteamId=0 centreUnderPointer=False centreDistance=180.735 centreRadius=135
    [ItemDropped] paincream (id 1357530646467) at (3.0,498.1), vel (-0.7,-0.7) — container contents 0.

Release 2 — 180 px up the body's screen column (pixel `(688, 378)`):

    casts="" hadItem=paincream(Clone) after=none mode=World backpackOpen=False
    focusedSteamId=0 centreUnderPointer=False centreDistance=129.306 centreRadius=135
    [ItemDropped] paincream (id 1361825613763) at (3.0,498.1), vel (0.0,-0.0) — container contents 0.

Release 3 — on the displayed backpack's USE centre while it was open (pixel `(823, 44)`, the ring's own
average), item `#1366120581059`:

    casts=RadialCenter hadItem=paincream(Clone) after=none mode=DisplayedBackpack
    backpackOpen=True focusedSteamId=76561198863287957 centreUnderPointer=True
    centreDistance=82.609 centreRadius=135

Release 3's item did not leave the host: `body-items` afterwards still read
`0:paincream|1:trashbag|3:lantern|4:dogfood|5:waterbottle`, and the log carries no `[ItemDropped]` for
`#1366120581059`. The guest's inventory stayed `3:lantern|4:dogfood|5:waterbottle` (no take). The
patient's limb afterwards read `skinHealth=0, bleedAmount=45.215, pain=50.284` — three minutes of
natural healing, not a dose: nothing in the log names an application for any of the three ids.

## What this settles

- **Release 1 and 2 are ordinary world drops**, not "released on a teammate": `casts` is empty, so the
  release never reached the remote body (the clones' colliders are disabled by design — the native
  cast cannot hit them). They show a topical carrier falling through to the native drop with no dose,
  which is the same shape as the design's "falls through to the native drop" — but they are not the
  teammate-shaped reading row (a) asks for, and this record does not claim they are.
- **Release 3 is the teammate-shaped reading** (the display names Steam1, the pointer is on the USE
  centre, `centreUnderPointer=True`): the cast is `RadialCenter`, the item is neither applied nor
  dropped — it returns to the host's slot. The design's row (a) says "drops instead of applying
  anything"; the no-application half holds, the drop half does not.
- **Row (b) is unmeasured.** `remote-gesture mode=medical` answered `no-proxy`; the wound view needs a
  proxy body, and this batch did not build one.

## Open

- Decide whether release 3's "return to the sender" is the intended refusal shape or a defect: the
  topical family was supposed to fall through to the native drop, and the displayed backpack's centre
  is the use/wear affordance, not the inventory use's drop. Until that is read against
  `ItemUseFamilyClassifier` and `PlayerCamera.TryPerformRadialAction`, the ticket cannot mark row (a).
- Row (b) needs the medical view driven: a proxy body (the `no-proxy` reading) or the wound-view
  release on the patient's own screen.
