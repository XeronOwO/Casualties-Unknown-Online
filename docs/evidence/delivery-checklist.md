# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — before the first code edit the ticket states, in its own section, the surface being changed
- [ ] Native parity — the three lines (player's action and expectation / the native chain the game itself runs / how the clone reproduces it)
- [ ] Native parity probe
- [ ] Entry mapping — the entry is the game's own item customizer; the wire path is the carried-inventory message, not a CUO control
- [ ] Mechanism inventory — every touched mechanism has evidence
- [ ] Whole-family audit: the two aliases of one item state — `CharacterItemMsg` (carried sync/digest) and `WireItemData` (kernel checkpoint) — both carry the pair, and every mapper between them (`KernelWireMapper`, `KernelPlayerInteractionWireMapper`, `ItemKernelCodec`) maps both directions; `ItemStateEquality.TopLevelMatches` compares both
- [ ] Self-check table: mechanism x change x evidence
- [ ] Verification design
- [ ] Plan approved by the user
- [ ] Structure review done
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
