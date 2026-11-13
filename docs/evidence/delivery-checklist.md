# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — four tickets filed from the reference comparison, each naming the design page that owns its surface: character spawn (`docs/design/game/README.md` + `saves-and-recovery.md`), consent (`permissions-and-commands.md` + `items/README.md`), rule toggles (`permissions-and-commands.md` + `game/README.md`), death stats (`observability.md` + `console-and-save.md`)
- [ ] Native parity — every ticket quotes the game's own mechanism it sits on: `PreRunScript.characterSelected`/`PlayerCamera.SpawnCharacter` for the character, `ItemUseTargetPolicy` for the target gate, `PlayerPushService`/`CarriagePatchBridge` for the toggles, the revive log line for the counter
- [ ] Native parity probe — the probe for the comparison was the ripgrep pass over `src/` with the game's own names (`characterSelected`, `SpawnCharacter`, `NapVariant`, `Nameplate`, `RemoteBackpackAccess`): the first two are absent, the last three present
- [ ] Entry mapping — each ticket names its entry path and file:line: `RemoteBodyFactory.cs:54-69` (clone template), `PlayerItemUseExecution`/`NativeSolidFoodEat` (the hand-feed path), `HostRuleSet.cs` (the rule set the toggles join), the respawn path (`RespawnCoordinator`)
- [ ] Mechanism inventory — the comparison was re-run with ripgrep after the first pass was wrong: `grep Skill`, `grep Nap`, `grep Nameplate`, `grep RemoteBackpackAccess`, `grep Consent` — the first four exist, the fifth finds only comments
- [ ] Whole-family audit — the tickets that touch the rule surface (toggles, consent) say they join `IHostRules`/`HostRuleSet` and are announced like every rule; the character ticket says the id rides the character snapshot instead
- [ ] Self-check table — gates 834/0 after the four tickets, their index rows and census rows landed; the build was untouched (documentation only)
- [ ] Verification design — each ticket carries acceptance rows with a reading that fails today (the clone's source object, the consent pending state, the refused push, the count after two deaths)
- [ ] Plan approved by the user — the user ordered the tickets and the work ("file the tickets, then start" - translated) after the corrected comparison
- [ ] Structure review done — no code changed; the four tickets add one surface each and name the existing owner, so no new type or module is introduced by filing
- [ ] Build + dotnet format + dotnet test normative gates pass
- [ ] Release-cycle deployment/acceptance: not applicable — this cycle changed no deployable artifact

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
