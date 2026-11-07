# A given cream arrives full and usable — batch 20261107-q

- Build: `0.1.0+8a44059903d39eeb8213c864e29f2ea962096063` (deployed, `verify-deploy` exit 0)
- Session: three clients launched 18:52:05-35, lobby `109775243745428409`, all in world 18:52:35
- Ticket: `docs/backlog/done/give-creates-empty-liquid-containers.md`

## Row 1 — a given cream reads condition 1 and the wound view accepts it

```
18:53:16 host console `/give @p paincream` → the cream lands in slot 0 as #1340350777283
18:53:16.598 [ItemCreate] prepared paincream (id 1340350777283) from a fresh instance of its prefab.
host use-eligibility type=paincream predicate=medical → found=true, condition=1, isEligible=true
```

The wound view's own admission (`LocalUseItemEligibility.IsMedicalLimbUseItem`) accepts the given cream. The
instrument was extended this batch (`use-eligibility` gained `predicate=use|medical`) because the old reading only
asked the world-drag classifier (`IsUseItem`), which a limb-use item never answers.

## Row 2 — the host and guest copies agree

```
18:53:16.612 guest [StandingItem] materialized paincream (id 1340350777283) for 76561198281246659, data parent 0, condition 1.00.
18:55:37.512 host [MedicalView] measured 10.00 ml from paincream's own native limb action for 76561198863287957 limb 5 (id 1340350777283).
18:55:37 patient limb 5 disinfectionTime 0 → 299.445
```

The guest's materialized copy reads the same condition 1; the dose measured 10 ml from the cream's own contents, and
the patient's `disinfectionTime` went 0 → 299.445 (reliefcream's `SetDisinfect(300 * clamp01(ml * 0.1))`), so both
copies carry the default contents.

## What the batch settles

Both acceptance rows are judged on the deployed build. The defect's mechanism — the grant captured the prefab
ASSET's state (condition 0, no `Awake`-filled stack) — is gone, and `ItemGrantGateTests` pins the rule.

## Limits

- The acceptance read a cream created by `/give`; the `/summon` item path shares the same grant seam and was not
  re-read here.
- The guest's copy was read from its materialized standing fact; the display clone's own contents are a render, not
  a domain fact.
