# Remote medical Stage 2 — shrapnel, batch `20261106-n`

Artifact `0.1.0+e0727f8f61d8c88c5b59e15b51bb7adca0a56c73` (acceptance build, verified against this tree).

Three clients in one world: host `76561198281246659` on `18590`, guest (patient) `76561198863287957` on `18591`,
bystander `76561199526807662` on `18592`; lobby `109775243644362155`.

## Row 4, re-read: the bystander opens nothing

The row asks that a third client, with the patient's wound view open, is not disturbed by two operators working. It does
**not** ask for a screen on that client: the earlier record (`-l.md`) read a bystander copy that was my invention and was
removed in `e0727f8f`. This batch reads the corrected meaning.

1. The bystander opened the wound view on the guest:
   `remote-wound-special mode=open target=76561198863287957 limb=5` → `opened:true, viewOpen:true,
   displayBodyName:"MedicalDisplay_110000135D30695"`.
2. The guest's hand carried three pieces: `seed-shrapnel limb=5 count=3` → `{"shrapnel":3,"hasShrapnel":true}`.
3. The host started the session from its own view:
   `remote-wound-special mode=special target=76561198863287957 limb=5` → `started:true, shrapnel:3`.
4. `minigame-live` on both clients, same moment:

```
host:     {"hasMain":true,"kind":"ShrapnelMinigame","handClicking":false}
bystander {"hasMain":true,"kind":"none"}
```

The operator's client runs the native shrapnel screen; the bystander's client runs **no** minigame and continues to report
state normally (`minigame-live` itself is an in-process read on that client). Row 4 passes on the corrected meaning.

## Row 9, the isolation half (batch `20261106-m`)

The host used its own tweezers on the patient (host log `[MedicalView] started remote shrapnel minigame …
(tweezers=True)`), and `item-census mode=body` read the host's pair condition `1.00 → 0.99` while the bystander's own pair
stayed `1.00`. An operator's use drains only that operator's pair.

Unread: the symmetric half — the second operator's own drag draining its own pair. It needs the second operator's hand
(the sandbox window), or a seam that drives the wound-view drag in-process; neither exists today.
