# The spectator's three superseded host rules are still wired

- Status: Done — accepted 2026-10-24. The three rules left the wire (`HostRulesMsg` fields 14-16), the rule surface,
  the config, the Admin page, both localization catalogues and the config reference; the dead `SpectatorPolicy` and
  its test are deleted, and the entry-repair pass's reproducible size moved from 2462 to 2460 bytes with its comment
  updated. `780` gates and `6398` behaviour tests green after the removal (`519d8191`, `61456b37`).
- Priority: Low
- Category: Host rules / dead config

## What is asked

The spectator pivot turned the mode into a cheat-gated verb (`/gamemode spectator`) plus the death receipt's button,
and the user's ruling of 2026-10-24 says the mode is the player's own choice with **no host rule over it**. The three
rules the earlier design added are therefore superseded:

| Rule | Why it is dead |
|---|---|
| `SpectatorFreeFly` | The free camera is the mode's own capability now; nothing reads the rule. |
| `SpectateWhileUnconscious` | The mode's gate is the verb, not the death state. |
| `SpectateWhileWaitingToJoin` | Same: the mode is entered by choice, never by waiting. |

They still exist end to end — id, census row, rule set, the rules message's fields, the config entries, the Admin
page rows and both localization keys — so a host can set a switch that changes nothing, and a reader of the wire
sees three fields no code consumes.

## Required work

1. Remove the three rules from the host-rules surface: `HostRuleId`, `HostRuleCensus`, `HostRuleSet`, `IHostRules`,
   `HostRulesService`, `HostRulesOptions`, and the `HostRulesMsg` fields (pre-release, so the wire drops them with no
   compatibility layer — decision 241).
2. Remove their config entries (`PluginDependencyRegistrar`/`PluginHost`), the Admin page rows
   (`OnlineUiAdminDrawer`, `HostRulesConfigEditor`) and the two localization keys per rule.
3. Delete the dead code the rules kept alive in `SpectatorCameraService.MayWatch` (the mode's own gate decides now),
   and update `docs/en/reference/configuration.md` + `docs/zh/reference/configuration.md`.
4. The tests that census the rules (`HostRuleCensusTests`, `HostRuleAnnouncementTests`, `HostRuleValueTextTests`,
   `CommandConsoleTestSession`) move with the removal.

## Non-goals

- No behaviour change beyond deleting the rules: the mode's gates stay exactly as
  `dead-player-spectating.md` records them.
