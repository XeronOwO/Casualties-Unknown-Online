# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops.

- [ ] Design comprehension
      `docs/design/cross-cutting/saves-and-recovery.md` and `docs/architecture/save-archive-format.md`, with the chain
      by execution: `WorldCutWriter` → `RestoredWorldFactReplay` → `WorldBuildingEntitySync.ApplyRemoteBuildingEntityHealth`
      (`:242`) → `BuildingEntityLookup.At` (`:18`).
- [ ] Native parity
      (`buildingEntity.health -= param.structuralDamage * Random.Range(0f, 2f);`) reports the blast, and a building whose
      support the blast removed is then felled by the game's support-loss path, which reports nothing.
- [ ] Native parity probe
      world after a continue, not a native call.
- [ ] Entry mapping
      `BuildingEntityLookup.Nearest(x, y)`, which scans `Object.FindObjectsOfType<BuildingEntity>(true)` so a building in
      a deactivated chunk is still found.
- [ ] Mechanism inventory
      nearest fallback second; `ApplyRemoteBuildingEntityHealth` keeps its destroyed-fact rule (health below 0.5 with no
      entity returns true); `Nearest` is diagnostic only and returns text, never a decision.
- [ ] Whole-family audit
      one-to-one within 6 m, `GeyserFillWait`, the keypad table), and this ticket owns only the building-health rows; the
      log now names the nearest building for every refusal in this family.
- [ ] Self-check table
      fixed on the way: the localization census rejects `string.Format(` as an unresolved accessor, and the FQN gate
      rejects qualified `UnityEngine.Object` where a using exists), `dotnet format --no-restore` exit 0.
- [ ] Verification design
      nearest building's distance, deciding A (settle before the cut) for "no building within 25 m", B (restore rule) for
      "a building 3–10 m away", and the measurement-only path for no refusal in five runs.
- [ ] Plan approved by the user
      standing instruction is that technical trade-offs are decided from evidence, so this cycle takes C (measure first).
- [ ] Structure review done
      unchanged apart from the appended sentence.
- [ ] Build + dotnet format + dotnet test normative gates pass
      (2 pre-existing MSB3270 warnings), unit 6747/6747, gates 836/836, `dotnet format --no-restore` exit 0 on this tree.

- [ ] Release-cycle deployment/acceptance: deploy and read a passing pod-team-wait with zero member errors.
- [ ] FORBIDDEN — never check this box
