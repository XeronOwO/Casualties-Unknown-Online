# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the chain is written with file:line before the edit
- [ ] Native parity — the write seam is the game's own `SetBlock(Vector2Int, ushort)`
- [ ] Native parity probe — the stamper patch cannot install (`HarmonyException: IL Compile Error`), so the chain comes from the stack
- [ ] Entry mapping — one seam, one recorder
- [ ] Mechanism inventory — the trigger is the frame above `GenerateObjectAtPos`
- [ ] Whole-family audit — the pod and three clearing circles are the same generation family
- [ ] Self-check table — instrumentation only, no behaviour change
- [ ] Verification design — the line names writer, stamper, coroutine and its caller
- [ ] Plan approved by the user — "locate the root cause and fix it"
- [ ] Structure review done — the recorder stays acceptance-only
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: the deploy that reads the new chain follows with the next build

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
