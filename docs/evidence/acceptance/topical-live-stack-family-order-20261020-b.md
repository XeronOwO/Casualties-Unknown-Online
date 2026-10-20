# Acceptance record — the live-stack family order

- Ticket: `topical-live-stack-family-order` — verdict: **moved to `done/`** (both rows pass)
- Batch: `20261020-b` — this run is the batch's whole scope
- Commit: `b8628640` · Deployed artifact: `CasualtiesUnknownOnline.dll`, `0.1.0+b8628640f769ea7f79e36ce1e3aa45cf34eef08c` (`prelude.log`: verify-deploy exit 0 before the clients started)
- Run: 2026-10-20 19:48 → 19:56 · Host: physical machine · Guest: sandbox 1
- Dependencies: full preflight (`RESULT: OK`, 11 present), two clients, in-process probes and recipes, per-client logs — no frame capture and no OS input (both rows are value rows)
- Artifacts: `.acceptance/20261020-b/` — `preflight-t1.log`, `prelude.log`, the `r1*`/`r2*`/`r3*` probe and recipe answers, `m-*.txt` log marks; the cycle's build evidence is `format-t1.log`, `gates-final2.log`, `full-final1.log`, and the red is `red-t1.log`

## The run's setup

The host gave itself a saline bag with a health-usable liquid through the product's own state command:
`/give @s saline{Liquids:[{LiquidId:"disinfectant",Amount:750}]}` answered `Gave saline
(#1121307445187) ... Liquids:[{LiquidId:"disinfectant",Amount:750f}]` (`r1-host-give.json`), confirmed
by `/data get id` (`r1b-host-item.json`: `{Condition:1f,...,Liquids:[{disinfectant:750f}]}`). The
guest's own body read `sickness 0` (`r1b-guest-health.json`).

## What the run judged

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A saline bag holding a health-usable liquid, dragged onto a teammate in the world, drinks through the drink family's own path: the host commits the drained dose, the affected client runs the liquid's own `onDrink`, the bag's liquid and condition drop, and the target's body shows that liquid's effect. | machine | **pass** | The operator-side production calls on the live bag (`r2c-host-drag-row.json`): `gate=true`, `family=Drink`, `dose=100`, `sentDose=100`. The host log: `[ItemUse] measured 100.000 ml from saline's own native use action.` 19:55:08.370, `[ItemUse] <host> used saline (id 1121307445187) on <member>; destroyed=False.` 19:55:08.373, `[ItemUse] local item 1121307445187 condition set to 0.87` 19:55:08.376. The bag after the use reads `{Condition:0.8666667f,...,Liquids:[{LiquidId:"disinfectant",Amount:650f}]}` (`r2d-host-item-after.json`). The guest's own client ran the liquid's body: `[ItemUse] local body drank 1 stack(s) from <host>.` 19:55:08.391, and its body moved by the liquid's own `onDrink` — sickness 0 → 14.709 (`r2e-guest-health.json`) |
| 2 | The SAME bag, released on a limb through the remote wound view, does not take the inventory use: no drink dose is committed and the bag is not drunk. | machine | **pass** | The remote medical view opened on the member through production's own entry (`r3a-host-open.json`: `viewOpen=true`, `displayBodyName=MedicalDisplay_...`), the wound view pointed at limb 0 (`r3b-host-wound-view.json`: `limbLookingAt=0`), and the release consumed the drag without a drink: `r3c-host-release.json` (`dragAfter=none`, `calls=none`). The host log's only effect line is the existing topical-measurement refusal `[ItemUse] refused topical use: saline's own native limb action applied nothing to the treated limb.` 19:55:23.369 — no `used saline` and no member drink follows it — and the bag is unchanged (`r3d-host-item-after.json`, still 650 ml) with the member's limb 0 unchanged (`r3e-guest-limb-after.json`: pain 0, skin 100, no shrapnel) |

## What the run also verified

- The pre-fix verdict is this cycle's red (`red-t1.log`: the host refused the same request and the
  focused unit case failed on the pre-fix tree).
- The entry split holds in both directions: the inventory entry reads `Drink` and measures 100 ml
  (row 1), while the limb entry's own topical measurement refuses the same item instead of drinking it
  (row 2).
- The wound-view refusal is the shape filed as `todo/topical-limb-action-shape.md`: the container's own
  limb action is the syringe path, so the topical measurement captures no `ApplyToLimb`. The run records
  it rather than presenting it as the row's expected success.

## Residuals for the user

None: both rows are value rows judged from the deployed artifact.

## Limits

- **The world-drag row did not drive the pointer's own target resolution.** The run may not move the OS
  pointer; on this machine the pointer is parked outside the client's window (the probe read
  `Input.mousePosition` at (2067,-521), whose world point sits about 90 world units from the group
  entry and below the surface). Moving the client window under the pointer was attempted and restored
  (the window's rect read back as its original `-8,0`), but it could not place the pointer over a world
  position the release resolves. The row therefore drives the three production facts the release itself
  calls — `IsUseItem`, `FamilyOf(Inventory)` and `RemoteDrinkUseHandler.TryMeasure` — plus the production
  request entry (`SendUseRequest`), which is the call the release makes once its target resolves. The
  fixed 1.5-unit target circle is `todo/cross-player-drag-use-model-bounds.md`'s, not this ticket's.
- The rows are judged on values read in-process, not pixels; no frame was captured (both rows are
  `machine`).
- The wound-view row reads the refusal and the untouched state only; the started syringe delegate is
  not otherwise observed (no UI read). That is the filed sibling's evidence, not this ticket's row.
