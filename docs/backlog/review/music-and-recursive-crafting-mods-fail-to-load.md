# The music and recursive-crafting mods fail to load against the current Abstractions

- Status: Review — fixed 2026-10-25 and judged 2026-10-26 — opened 2026-10-25 from the client logs of both peers, after the user corrected the first report ("the pinyin mod is fine; the other
  two blew up — fix them together").
- Priority: High — two shipped satellite mods are dead on every client, and the reason is a stale deployed binary, not their source.
- Category: Satellite mods / deployment
- Source: user report (2026-10-25) plus the evidence below.

## Design

The satellite mods register through `docs/design/mechanics/console.md` (a mod's console commands and completion stage) and
`docs/architecture/surfaces.md` names the mod-loading surface. The mod contract itself is `CasualtiesUnknownOnline.Abstractions`, whose
`ModConsoleCommand` is the type both broken mods construct.

## What the machine says

Both clients, same frame, one day apart from their binaries:

```
22:54:47.409 [ERR] [Mods] cuo.music failed to load — skipped, the other mods continue.
System.MissingMethodException: Method not found: void CasualtiesUnknownOnline.Abstractions.ModConsoleCommand..ctor(string,string,string,
CasualtiesUnknownOnline.Abstractions.CommandPermission…
22:54:47.410 [ERR] [Mods] cuo.recursivecrafting failed to load — skipped, the other mods continue.
System.MissingMethodException: Method not found: void CasualtiesUnknownOnline.Abstractions.ModConsoleCommand..ctor(…)
```

`cuo.pinyinsearch` loads fine in the same frame (`console completion stage registered`, `initialized`), which is why the first report pointed at the
wrong mod. The deployed `CasualtiesUnknownOnline.Music.dll` and `CasualtiesUnknownOnline.RecursiveCrafting*.dll` carried `10-24 07:24/08:17`
timestamps while `CasualtiesUnknownOnline.Abstractions.dll` was deployed `10-25 22:48`: the mods were built against an older `ModConsoleCommand`
parameter list, so the CLR refuses to bind the constructor.

## The fix, in two parts

1. **The immediate one (done in this cycle)**: rebuild both mods from this tree and put their assemblies back into their folders
   (`BepInEx/plugins/MusicShare/`, `BepInEx/plugins/RecursiveCrafting/`). Verified by the load lines in the next batch.
2. **The reason it could drift (this cycle)**: `tools/deploy.ps1` deployed the main plug-in and the packed single file only — it never touched the
   satellite mod folders, so a mod could sit a day behind the contract it compiles against and nothing said so. The script now deploys each satellite
   mod's built assembly into its folder as part of the same run, with a line per mod, so "deployed CUO" means the mods too.

## Withdrawn part

The earlier instruction in this ticket's first form — merge the pinyin search's two projects into one that hard-depends on CUO — is **withdrawn by the
user** in the same message that corrected the diagnosis ("the pinyin mod is fine"). Nothing of it is implemented, and the pinyin mod keeps its
`PinyinSearch` + `PinyinSearch.Core` split.

## Acceptance rows

| Row | The check | State |
|---|---|---|
| 1 | Both clients load `cuo.music` and `cuo.recursivecrafting` with no `failed to load` line in a fresh session | **judged 2026-10-26**: host `18:52:41` and guest `18:53:10` both log `cuo.music loaded`, `cuo.recursivecrafting loaded` and `cuo.pinyinsearch loaded`, and both `latest.log`s hold ZERO `failed to load` lines |
| 2 | `tools/deploy.ps1` copies the satellite mods and prints one line per mod, and a second deploy leaves the folders at the same version as the plug-in | **judged**: the deploy printed `MusicShare/CasualtiesUnknownOnline.Music.dll (10-25 23:42)` and the two RecursiveCrafting assemblies with the same timestamp, and `verify-deploy` then reported the install matches this tree |
| 3 | A mod built against an older contract cannot ship unnoticed: the deploy prints the copied assembly's timestamp per mod | **judged by construction**: that timestamp line is the instrument, and it is the line that shows 10-25 23:42 for all three assemblies; an older copy would print its older date |

## Limits

- The row 1 check needs one fresh session after this cycle's redeploy; the rebuild and copy are already on the machine's game folder.
- Who built and copied the stale mods originally is not recorded anywhere; the deploy change is what makes that question moot rather than a suspect.

## Judged (2026-10-26)

Both clients, one fresh session started after a machine restart: `cuo.music loaded`, `cuo.recursivecrafting loaded`, `cuo.pinyinsearch loaded`, and zero
`failed to load` lines on either side. The deploy side was read back with the deploy log (one line per satellite assembly, all stamped `10-25 23:42`) and
`verify-deploy` ("Deployment matches this tree's build output").

## One thing this batch found about the launcher (not this ticket)

`tools/acceptance/launch-clients.ps1` started the host and the Steam1 guest, then failed because the Steam2 alt box could not come up in time, and its
non-zero exit made the whole launch look failed even though two usable clients were listening. Recorded here because it is how this batch started; the
launcher has its own ticket (`docs/backlog/todo/launcher-fails-when-the-alt-box-cannot-start.md`).