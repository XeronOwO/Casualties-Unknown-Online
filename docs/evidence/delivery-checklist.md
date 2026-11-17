# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops.
- [ ] Design comprehension
      section and its "rules replicate at admission and are enforced by both sides" bullet),
      `docs/design/cross-cutting/observability.md` (a player-facing refusal is a named line, never silence) and
      `docs/design/game/README.md` (the body-action families); the ticket records the chain by execution.
- [ ] Native parity
      in `PlayerPushService` (`MaxPushDistance = 9f * 1.2f`, `PushCooldownMs = 1000`, `1 + (strength - 10) * 0.1`);
      the carry rule the switch lifts is the game's own `Body.conscious`/`alive` gate, not a CUO invention.
- [ ] Native parity probe
      the real-game `host-rule-toggles` row (push refused, carry through) rather than a native probe.
- [ ] Entry mapping
      `Plugin/OnlineUiMemberListDrawer.cs:243` (Push) → `IPlayerInteractionControl.SendPushRequest`; the carry buttons →
      `SendCarryStartRequest`; the rule write → `/gamerule` → `HostRuleChangeService` → `IHostRulesEditor` →
      `HostRulesConfigEditor`; the announcement → `HostRulesMsg` → `AnnouncedHostRules` → `HostRulesService.Current`.
- [ ] Mechanism inventory
      `PlayerPushService.HandlePushRequest`; `AllowAlwaysCarry` lifts the carried-side state rule in
      `PlayerCarryService.HandleCarryStartRequest`; `PiggybackMaxBodies` has no model to limit and is parked on the
      ticket with three options.
- [ ] Whole-family audit
      invariant is the kernel's (`GameState/Domains/Players/PlayerDomainModule.cs` `AssertCarryFields`) and the
      switch stops there; heal/use are outside this decision and keep their existing gates.
- [ ] Self-check table
      refusals and the carry rule each have their own unit row, including the host-authority arm.
- [ ] Verification design
      `/gamerule` verb, drives `push-player` and `carry-start` through the same runtime entry the UI uses, reads the
      member's `host-rules-read`, and checks the target's `body-read` position before and after.
- [ ] Plan approved by the user
      three switches"), with the third parked by evidence rather than silently dropped.
- [ ] Structure review done
      chain; the push service took `IHostRules` + notice/localization through its existing composition, and the carry
      service took the same `IHostRules` reference.
- [ ] Build + dotnet format + dotnet test normative gates pass
      836/836, format exit 0 on this tree.

- [ ] Release-cycle deployment/acceptance: deploy and read a passing pod-team-wait with zero member errors.
- [ ] FORBIDDEN — never check this box
