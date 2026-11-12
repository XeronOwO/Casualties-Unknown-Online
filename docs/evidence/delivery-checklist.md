# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — `DamageBlock` dereferences `GetBlockInfo` before writing damage
- [ ] Native parity — the reading is the game's own code, no native change
- [ ] Native parity probe — `block-hit dmg=0` failed at stage `damage`
- [ ] Entry mapping — the entry is `WorldGeneration.DamageBlock(Vector2Int, float, bool, bool, bool)`
- [ ] Mechanism inventory — block 14 has no `BlockInfo`, so a hit there cannot land
- [ ] Whole-family audit — the row stays parked with the cause, not deleted
- [ ] Self-check table — the stage name from the recipe's own wrapper
- [ ] Verification design — the next fixture names a real terrain cell
- [ ] Plan approved by the user — night mode: a park with its cause, no product decision
- [ ] Structure review done — no code changed, the reading sits beside the parked row
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
