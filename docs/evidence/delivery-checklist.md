# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops.

- [ ] Design comprehension
      cut-time bullet it refines, and the ticket `docs/backlog/in-progress/1.0-cut-outlives-a-felled-building.md` carries
      the chain with file:line (`WorldCutWriter` → `RestoredWorldFactReplay` → `ApplyRemoteBuildingEntityHealth:242` →
      `BuildingEntityLookup.At:18`).
- [ ] Native parity
      `WorldGeneration.cs:5449` damage (`buildingEntity.health -= param.structuralDamage * Random.Range(0f, 2f);`), which
      reports nothing; the restore now records what that pass leaves behind.
- [ ] Native parity probe
      on fresh worlds rather than a native call.
- [ ] Entry mapping
      `src/CasualtiesUnknownOnline.GameAdapter/World/WorldBuildingEntitySync.cs` `ApplyRemoteBuildingEntityHealth`, the
      only site that applies a restored building-health row.
- [ ] Mechanism inventory
      (`AnyWithin`, `NeighbourhoodRadius`) and the diagnostic sentence (`Nearest`); the destroyed-fact rule for health
      below 0.5 is unchanged.
- [ ] Whole-family audit
      one-to-one, `GeyserFillWait` growth hold, keypad table), and this rule touches only building-health rows; an area
      the layer has not produced still refuses.
- [ ] Self-check table
      `dotnet format --no-restore` exit 0; the five-run measurement shows one refusal in five before the change.
- [ ] Verification design
      the pass condition is zero refused world-entity rows and `restore complete` in the audit.
- [ ] Plan approved by the user
      instruction is that technical trade-offs are decided from evidence, and C's reading chose B with the neighbourhood
      gate.
- [ ] Structure review done
      and the design page names the two questions (is the building there, is the area there).
- [ ] Build + dotnet format + dotnet test normative gates pass
      warnings), unit 6747/6747, gates 836/836, format exit 0 on this tree.

- [ ] Release-cycle deployment/acceptance: deploy and read a passing pod-team-wait with zero member errors.
- [ ] FORBIDDEN — never check this box
