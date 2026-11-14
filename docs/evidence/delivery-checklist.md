# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the box keeps its own plug-in copy, so the launcher must sync it before the box starts
- [ ] Native parity — tooling only; no product code
- [ ] Native parity probe — the launch now prints `synced guest plugins from the desktop build` and the pair joins
- [ ] Entry mapping — one entry, `Start-BoxGame`
- [ ] Mechanism inventory — a stale box build reads as a product defect (the join was refused)
- [ ] Whole-family audit — the desktop-path branch is skipped when the box root equals the game dir
- [ ] Self-check table — one tooling file changed
- [ ] Verification design — a launch prints the sync line and the join row passes
- [ ] Plan approved by the user — the goal's main line
- [ ] Structure review done — the sync lives in one place (`Start-BoxGame`), not two
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: the deployed build was already verified; this cycle only fixes the launcher

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
