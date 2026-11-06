# The remote bandage: the minigame ends when the bandage is spent

- Date: 2026-11-06, 19:50–19:53 (Asia/Shanghai)
- Artifact: `0.1.0+0a1764f04c3f895a600c970051dccad622887439` (acceptance build; `tools/verify-deploy.ps1` exit 0)
- Session: host on 18590 on the desktop (Chinese UI); guest on 18591 in the Steam1 box; alt up but quit, not joined
- Batch dir: `.acceptance/20261106-e/`
- Defect: the user reported that a bandage whose durability fell below 1% kept the minigame turning with no further effect, where it should leave the screen once the item is used up.
- Instrument: the host's game log; the gesture itself was run by the user's hand.

## Scene

| step | reading |
|---|---|
| guest `wound-seed limb=5 force=300 infection=0` | `skinHealth=-12.2`, `bleedAmount=14.239`, `pain=234.923` on the hand |
| host `/give @s bandage` twice | two bandages, in the backpack; `/give @s splint` put a splint in the carried trashbag |
| both clients | `inWorld=true`, `gateWaiting=false`, same lobby |

## Readings (host log)

- 19:52:19–19:52:20 the item's condition walks down through `0.05 … 0.01`.
- 19:52:20.574 `[MedicalOps] local item 1366120581059 updated to condition 0.00.`
- 19:52:20.623 `[MedicalOps] local item 1366120581059 consumed by the remote operation — destroyed.`
- 19:52:20.629 `[ItemTrace] op=4 … result=Committed(1) events=[Destroyed]`
- 19:52:20.643 `[MedicalOps3] operation 1 (Bandage) ended with progress 1.000.`
- 19:52:20.644 `[MedicalOps3] operation 1 terminal Completed: progress 1.000.`
- 19:52:27.450 `[ItemTraffic] … Destroy=1`

Two follow-on lines are log noise, not defects: the kernel refused a second destroy of the same already-terminal item
(`InvalidTransition`) and the authoritative item update was skipped because the local item was already gone.

Verdict: PASS — the minigame closed by itself the moment the bandage was spent, and the operation ended Completed.
