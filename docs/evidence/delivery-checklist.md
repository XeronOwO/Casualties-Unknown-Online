# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — a relay that reaches a generating guest is dropped with no recovery path
- [ ] Native parity — the branch is the product's own guard, quoted verbatim
- [ ] Native parity probe — the suite's break lands while the member's world still generates
- [ ] Entry mapping — the entry is `WorldEventSync.BindToSession` → `OnRemoteBlockDamaged`
- [ ] Mechanism inventory — only a breaker keeps a pending entry; a guest has none
- [ ] Whole-family audit — the finding is on the divergence ticket with three options and a recommendation
- [ ] Self-check table — the drop leaves no warning line on the member
- [ ] Verification design — the fix buffers the relay and applies it when the world is whole
- [ ] Plan approved by the user — night mode: the question is written into the ticket with options; the queue continues
- [ ] Structure review done — no code changed this cycle
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
