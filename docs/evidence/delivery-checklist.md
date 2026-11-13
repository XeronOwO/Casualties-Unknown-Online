# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the heal row must know whether the body it reads is the body the reset wrote
- [ ] Native parity — no game content; the case reads the game's own Body instance
- [ ] Native parity probe — two readers on the member's client name the same instance when the heal lands
- [ ] Entry mapping — the reset path is the product's `LocalBodyReset.Reset`, called from the heal reset handler
- [ ] Mechanism inventory — the limb read covers local/saved/display sources and now names its body
- [ ] Whole-family audit — the same instance identity question applies to every body-side report; only the heal row reads a body back today
- [ ] Self-check table — two files changed, both instrumentation
- [ ] Verification design — build, unit, gates, format, then the pair
- [ ] Plan approved by the user — the goal's main line (finish the backlog, make the suite event-driven)
- [ ] Structure review done — the id travels as a log field and a recipe field, no new type
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: nothing deployed this cycle

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
