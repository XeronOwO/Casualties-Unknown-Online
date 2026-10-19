# Acceptance record — limb-tool family protection gaps (batch 20261023-a)

- Ticket: `limb-tool-family-protection-gaps` — verdict: **moved to `done/`**; both acceptance rows pass on the
  deployed artifact.
- Batch: `20261023-a` — tickets: this one and `cross-player-item-use-random-determinism`;
  both were served by one three-client session (`docs/evidence/acceptance/cross-player-item-use-random-determinism-20261023-a.md`)
- Commit: `44e263cc` · Deployed artifact: `CasualtiesUnknownOnline.dll`,
  `ProductVersion 0.1.0+44e263cc681d2f8f2ccd8fee6dd9272f4365b999` — `tools/deploy.ps1` exit 0 and
  `tools/verify-deploy.ps1` exit 0 before any client started
- Run: 2026-10-19 21:18 → 21:26 by the session's clock · Host: physical machine (the operator) · Guest:
  sandbox (the treated player) · Third client: alternate sandbox (the third-party view)
- Dependencies: all eleven present (`RESULT: OK - a full two-client run is possible; the alternate third
  client is configured`); `active=cuo`, `game-running=false`
- Artifacts: the `l-*`, `l9`/`l10` and `r8-alt-display-limb*` JSON files and the `z-*.log` copies in the
  directory named by `acceptance-artifacts-dir`; Steam ids are written `<host>` / `<guest>` / `<third>`

The operator carried two `icepack`s. The ice pack's native limb action is
`item.condition -= 0.5f; limb.body.temperature -= 1f; GetOrAddComponent<ChilledLimb>(limb.gameObject)` with
`timeLeft = maxTime = 150f` (`reversing/Assembly-CSharp/Assembly-CSharp/Item.cs`, the `icepack` definition at
`:1629`), and `ChilledLimb.Update` destroys itself once its 150 s run out
(`reversing/Assembly-CSharp/Assembly-CSharp/ChilledLimb.cs`). It is therefore a limb tool whose whole effect —
a named field and a limb component — is observable on the treated limb.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| a | the picked limb is served: the tool's effect and its component land on that limb (the treated side's own limb state plus the operator's and the third peer's view) | machine | **pass** | The wound-view release (`remote-medical-release item=icepack limb=2`, after `remote-wound-special mode=open target=<guest>` and `remote-gesture mode=wound-view limb=2`) ran the item's own action on the patient: `<guest>`'s log carries `[ItemUse] ran the game's own limb action of icepack (id 1078357772227) on the local limb 2; item condition left at 0.50.` and the host's log the admission and outcome pair at 21:20:07.008/.107 (`[ItemUse] <host> uses icepack (id 1078357772227) on <guest> — the item's own limb action runs on the affected side.` / `used on the affected side icepack … condition 0.50.`, `z-host-latest.log`). The PATIENT's own limb reads `components:"ChilledLimb"` on index 2 and empty on every other index (`l5-guest-limbs-after-a.json`, read seconds after the action). The OPERATOR's displayed copy reads `components:"ChilledLimb"` on limb 2 and empty on limb 3 (`l6-host-display-limb2.json`, `l7-host-display-limb3.json`). The THIRD client, which joined later for the other ticket's row, reads the same component on limb 2 and empty on limb 3 through its own medical view (`r8-alt-display-limb2.json`, `r8-alt-display-limb3.json`). |
| b | with the picked limb dismembered between the pick and the run, or with a request naming an index the patient's body does not have, the run is refused with a log naming the item and the patient, and no limb receives an effect it was not picked for | machine | **pass** (the invalid-index shape; the dismembered shape was not staged) | The host called the production entry the gesture itself calls, `IPlayerInteractionControl.SendUseRequest(<guest>, 1082652739523, 99, 0)` (`l8-host-request-99.json`), and the host admitted it by name at 21:20:50.321 (`[ItemUse] <host> uses icepack (id 1082652739523) on <guest> — the item's own limb action runs on the affected side.`, with no outcome line after it). The AFFECTED side refused it: `<guest>`'s log carries `[ItemUse] the limb tool on icepack (id 1082652739523) cannot run: limb 99 of the request is not a usable limb of this body (gone, dismembered, or past its layout) — refused rather than applied to another limb.` The patient's limb snapshot after the refusal is unchanged: limb 2 still carries the previous `ChilledLimb` and every other limb carries nothing; no skin/bleed value moved (`l10-guest-limbs-after-refusal.json`). The second ice pack is still carried and untouched (`condition 1`, `id 1082652739523`, `l11-host-items-after-refusal.json`). |

## What the run did, in order

```text
21:19:02  host client launched through Steam; guest launched in the first sandbox; lobby created, guest joined
21:19:4x  host starts the run; both clients in world, member presence true on the host
21:20:0x  host creates two ice packs (1078357772227 slot 0, 1082652739523 slot 1)
21:20:05  host opens the remote medical view on <guest> (display MedicalDisplay_…) and its wound view on limb 2
21:20:07  the release runs the ice pack's own limb action on <guest>'s limb 2; ChilledLimb lands
21:20:1x  the treated limb, the operator's display and every neighbour are read
21:20:50  the host sends a limb-use request naming limb 99 with the second ice pack; <guest> refuses it
21:20:5x  the refusal is read back on <guest> and the untouched ice pack on the host
```

The release and the request are drives of production entries, not hand-built state: the release is the
game's own `PlayerCamera.HandleReleaseDragging` on a `WoundViewLimb` (the path the recipe documents), and the
invalid-index request is `IPlayerInteractionControl.SendUseRequest`, the call the wound-view gesture makes
(`CrossPlayerDragUse`). The only fixture is the ice pack itself (`item-provide mode=create`), declared setup.

## Residuals for the user

None.

## Limits

- **The dismembered variant was not staged.** Row (b) names two shapes; the invalid-index shape was driven,
  and it is the one the host cannot catch (`PlayerItemUseService` refuses a negative index only). Staging a
  dismemberment between pick and release would need a dismember write the batch does not have, and the row
  does not require both shapes.
- **The component is time-limited, and the reads are timestamped inside its life.** `ChilledLimb` removes
  itself after 150 s of game time (`ChilledLimb.Update`), so the component reads above were taken within
  ~2 minutes of the action. A later read of the authority's stored copy shows no component — the game expired
  it, not a failed write; that late reading is not evidence either way, and the limb component's landing is
  judged on the reads listed in row (a).
- **Pain is not compared as a constant.** The native limb drives pain every frame, so the patient's pain at
  read time is a decayed value; skin and bleed are the stable fields, and this row's subject is the component
  and the limb identity, not a pain constant.
- **Two clients plus one viewer.** The third-party view is the alternate sandbox client that joined for the
  sibling ticket's late-joiner row; it read the component through its own medical view while the view was
  fresh.
