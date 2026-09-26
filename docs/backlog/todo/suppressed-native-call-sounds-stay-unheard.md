# Sounds whose native call is suppressed never reach the peers

- Status: Todo
- Priority: Low
- Category: Audio sync / report coverage
- Source: the 2026-09-26 census of `review/unhooked-item-and-body-sound-families.md` — closing that family's routing left two rows whose clip is not merely unreported: the native call that would play it is suppressed or blocked on every side.
- Related: `review/unhooked-item-and-body-sound-families.md` (the census these rows come from), `review/remote-medical-treatment-operations.md`, `docs/architecture/remote-inventory-native-parity.md` (§3.4)

## The gap

Both rows share one shape: a native call that WOULD play the clip does not run on any side, so the
character-sound capture — which reports only a sound the native call actually played, and never
invents one — has nothing to carry.

| Row | What happens today | Why it is not just a missing scope |
|---|---|---|
| Remote limb treatment | The operator drops a medical item on the displayed limb → `PlayerCamera.TryPerformSpecialUIAction` → `TryHandleRemoteMedicalLimbUse` → the host-authoritative operation session. While the remote medical view is open the native `PlayerCamera.ApplyWoundItem` is BLOCKED (`RemoteMedicalBlockApplyWoundItemPatch`), so the item's own `useLimbAction` never runs and its clip (`syringe` / `splint` / …) plays on NO side: the operator, the patient and every third peer hear nothing. | Carrying it means the medical domain deciding to PLAY a clip the native path never played, and deciding where (the treated limb on the displayed body, or the patient's own limb). That is a new decision inside that domain, not a routing fix — inventing a sound is the character-sound family's stated non-goal. |
| World-item ground impact | `Item.OnCollisionEnter2D` (Item.cs:238-247) plays `drop` plus a block step sound and spawns dust. On a guest, `NonAuthoritativeItemImpactPolicy` suppresses the whole collision effect on the guest's non-authoritative copy, so a guest hears nothing when a world item lands — its own drops included — while the host hears its own authoritative copy. | There is no acting player to attribute the sound to, and the item-impact domain owns who simulates a landing. Relaying it needs a world-event carrier (an impact with a position), not a call-identity character scope. |

## What done looks like

1. Each row is either carried by a decided mechanism, or recorded as deliberately silent with the
   reason on this ticket — for the treatment row that means the user accepting "the operation is
   silent for everyone".
2. The treatment row is decided WITH the remote-medical domain: which client plays the clip, at which
   position, and whether the patient's client would then double-play it.
3. Any wire change bumps `ProtocolVersion.Current` in the same change (a fact of the change, not a
   constraint on it).

## Non-goals

- Not re-deriving the capture chain: `review/unhooked-item-and-body-sound-families.md` landed the
  routing, the scope guards and the census gate.
- Not a general "every game sound becomes a message" sweep: each row needs its own decision, and
  "the sound stays silent, because …" is a valid outcome that must be written down.
