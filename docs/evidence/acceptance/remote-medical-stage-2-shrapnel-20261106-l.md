# Remote medical Stage 2 — shrapnel, batch `20261106-l`

> Superseded for row 4 (2026-11-06): the bystander screen this record read was an invention of mine, not a requirement.
> The user rejected it ("不该看到") and the copy is removed; row 4 is re-read as non-interference (the bystander opens
> nothing and stays consistent). Everything else in this record — the two-operator fixture, the evaluator lesson, the
> restarts — stands.

Artifact `0.1.0+f9bee6a1f2fea72ab8b4278ed4b873f18cb00710` (acceptance build, verified against this tree).
Three clients in one world: host `76561198281246659` on `18590`, guest (patient) `76561198863287957` on `18591`,
bystander `76561199526807662` on `18592`.

## Row 4, first half: a bystander sees the observer screen

Batch `20261106-l`, after the `TryStartObserver` fix: the bystander opened the wound view on the patient
(`remote-wound-special mode=open target=76561198863287957 limb=5` → `opened:true, viewOpen:true`), the host started the
shrapnel session, and the bystander's `minigame-live` read

```
{"ok":true,"hasMain":true,"kind":"ShrapnelMinigame","handClicking":false,"handStarted":false,
 "handMagnitude":644.361,"handPosY":524}
```

where the same read on `0.1.0+c93f074b` returned `hasMain:false` for a bystander with the view open.

## Row 4, second half: the observer's own rows follow the authority

The host started session 6 (`21:35:14.342 session 6 created … (3 pieces)`; log line
`[Shrapnel] operator 76561198281246659 joined session 6.`). The bystander's own screen, read through the client's
`objects` list, showed three live rows and the two hidden rows the native constructor parks at 8000 + index:

```
before: 0:-316.9, 1:-316.9, 2:-316.9, 3:8003.0, 4:8004.0
```

The host then removed piece 0 (`shrapnel-break6 count=1`; host log `21:35:18.562 session 6 piece 0 removed by
76561198281246659.`), and the same read on the bystander showed

```
after:  0:8000.0, 1:-316.9, 2:-316.9, 3:8003.0, 4:8004.0
```

Piece 0 moved to the host's removal marker (8000 + index) on the watching client, the two live pieces stayed put, and
the rows the constructor had hidden kept their 8003/8004. That is the third party rendering the authority's piece
positions, not a local guess.

## Client restarts in this batch

The bystander client was restarted three times. Each restart was forced by the in-process evaluator, not by a fixture:
a declaration load or a recipe compile that fails leaves the evaluator's compilation unit poisoned, and from then on
every eval on that client (including `-Action state`) fails with the same stale error. The user approved restarting
this client. Recorded here so the cost is visible: a failed eval costs a client restart, so an instrument is written
against a known-good shape before it is declared.

## Limits

- One removal was driven; the row's "positions follow" claim is read on that single removal, on one bystander.
- The session ended by the 15 s idle timeout after the removal (`21:35:33.586 session 6 terminal TimedOut.`), which is
  the harness's timeout, not a product failure: a full removal would end it `Completed` (row 7).
- Row 9 (each operator's own tweezers condition) is still unread.
