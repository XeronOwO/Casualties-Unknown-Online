# mod-cross-player-native-semantics — rows (a) and (b): the world drop and the medical-view dose (20261107-o)

Batch on `0.1.0+3005409c`, host 18590 + Steam1 18591 in lobby `109775243733797288`.

The topical's entry is the patient's medical view, and only it: `docs/design/items/README.md:16-23`
puts the topical family in the medical view, `docs/design/items/topical.md:7-20` names the wound
view's release as its only entry, and `docs/design/mechanics/medical-view.md:7-16` makes that release
the native call site. The ticket's §What runs where says the same: a world drag is the cross-player
form of USING from the inventory, so a topical carrier released in the world falls through to the
native drop and applies nothing.

The ticket's Part B rows: (a) a topical container released on a teammate in the world drops instead
of applying anything; (b) a wound-view release applies the dose to the limb the operator picked, on
the patient's own screen. This record covers both and marks each half.

## The fixture

Host at `(0, 500.615)`; Steam1 teleported to `(1, 501.9419)` (the game snapped it from
`(1.803524, 500.6154)`); Steam1 wound-seeded so a dose would show:

- `wound-seed limb=5 force=300 infection=0` → `skinHealth=-112.2, bleedAmount=18.52, pain=222.004`
  before the first medical-view attempt; a second seed read
  `skinHealth=-79.2, bleedAmount=49.36, pain=169.988` before the accepted release.
- `screen-point2 x=1 y=501.9419` → the guest's ground point at `(688, 559)` in the host's pixels.
- `ring-buttons` with Steam1's displayed backpack open → six buttons; their average is the ring's
  centre: `(660, 535)`, and after the display moved with the focus, `(823, 44.5)`.
- The host carried a `/give @p paincream` (`#1387595417539`, condition 0 — the finding below) and,
  after dropping it, a native `item-provide mode=create` cream (`#1396185352131`, condition 1).

## Readings

### Row (a) — a world release of the cream, backpack closed

Release 1, on the ground beside the teammate (pixel `(688, 559)`):

    casts="" hadItem=paincream(Clone) after=none mode=World backpackOpen=False
    focusedSteamId=0 centreUnderPointer=False centreDistance=180.735 centreRadius=135
    [ItemDropped] paincream (id 1357530646467) at (3.0,498.1), vel (-0.7,-0.7) — container contents 0.

Release 2, 180 px up the body's screen column (pixel `(688, 378)`):

    casts="" hadItem=paincream(Clone) after=none mode=World backpackOpen=False
    focusedSteamId=0 centreUnderPointer=False centreDistance=129.306 centreRadius=135
    [ItemDropped] paincream (id 1361825613763) at (3.0,498.1), vel (0.0,-0.0) — container contents 0.

Both fall through to the native drop and apply nothing. The cast can never hit the remote body — the
clones' colliders are disabled by design — so in the world the drop is the only outcome.

Release 3, on the displayed backpack's USE centre while it was open (pixel `(823, 44)`):

    casts=RadialCenter hadItem=paincream(Clone) after=none mode=DisplayedBackpack
    backpackOpen=True focusedSteamId=76561198863287957 centreUnderPointer=True
    centreDistance=82.609 centreRadius=135

The item returned to the sender: the host's `body-items` still read
`0:paincream|1:trashbag|3:lantern|4:dogfood|5:waterbottle`, no `[ItemDropped]` names
`#1366120581059`, and the guest's inventory stayed `3:lantern|4:dogfood|5:waterbottle`. The cast is
the displayed backpack's own radial centre, not the inventory use's world drop; nothing was applied.

### Row (b) — a release through the patient's medical view

First attempt with the `/give` cream (`#1387595417539`):

    remote-medical-release item=paincream limb=5
    → ok=true, slot=0, viewWasOpen=true, dragAfter="none"
    [MedicalView] refused limb use: paincream is not a supported remote medical/limb-treatment item.

The refusal was correct: `use-eligibility mode=one type=paincream` read
`found=true, condition=0, isUseItem=false`. The give-path cream is unusable (see the finding); the
limb afterwards (`skinHealth=0, bleedAmount=17.916, pain=8.906`) is natural healing, not a dose.

Second attempt with the native cream (`#1396185352131`, condition 1), the medical view opened on
Steam1's limb 5 (`remote-wound-special mode=open target=76561198863287957 limb=5` → `opened=true,
viewOpen=true, displayBodyName="MedicalDisplay_110000135D30695"`):

    remote-medical-release item=paincream limb=5
    → ok=true, slot=1, limb=5, limbLookingAt=5, viewWasOpen=true, viewOpen=true,
      dragAfter="none", calls="none"
    [MedicalView] measured 10.00 ml from paincream's own native limb action for
    76561198863287957 limb 5 (id 1396185352131).

## What this settles

- **Row (a):** the no-application half holds in both shapes — a world release drops the carrier
  natively (two `[ItemDropped]` lines), and a release on the displayed backpack's use centre returns
  it without applying anything. The row's "drops instead of applying" is observed; the return shape
  is the display's own affordance, not an application.
- **Row (b):** the operator half is measured — the release measured `10.00` ml from the item's own
  native limb action and the host put `limb 5` on the request. The patient half is open: the guest
  log carries no application line, and the limb afterwards (`skinHealth=0, bleedAmount=48.99,
  pain=90.112` against the seed's `-79.2/49.36/169.988`) is not attribution against natural healing.

## The give-path finding

`/give @p paincream` creates `condition=0` with container contents 0 (`[ItemDropped] … container
contents 0`), while `item-provide mode=create` creates condition 1; the guest materialized the give
cream as `condition 1.00`. The host-side give item is unusable, and the native admission gate
(`ItemInfo.ActuallyUsableOnLimb`) refuses it. This is a defect of the give path for liquid containers,
not of the topical entry, and needs its own ticket.

## Open

- Row (b)'s patient half: prove the patient's client ran the applied dose — a patient-side log line
  or a controlled before/after that isolates the cream from natural healing.
- The give-path condition-0 defect: file a ticket.
- The display's use-centre return: decide whether "return to the sender" is the intended refusal
  shape, by reading `ItemUseFamilyClassifier` and `PlayerCamera.TryPerformRadialAction`.
