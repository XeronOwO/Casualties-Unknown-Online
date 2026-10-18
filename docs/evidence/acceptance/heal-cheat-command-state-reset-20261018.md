# Acceptance record — the `/heal` cheat reset

- Ticket: `heal-cheat-command-state-reset` — verdict: **moved to `done/`**. All six rows pass on
  `0.1.0+a3dff483`; the record is this page, and the batch's shared setup is
  [`20261018-u-scope.md`](20261018-u-scope.md).
- Batch: `20261018-u` — tickets `heal-cheat-command-state-reset`, `item-identity-across-transforms`.
- Commit: `a3dff483` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+a3dff4838f2b8abab19d2b56e66906148acbba65` (`tools/verify-deploy.ps1`, exit 0 before the clients
  started).
- Run: 2026-10-18 12:57 → 13:17 (+08:00), by the clients' own log clock · Host: physical machine · Member:
  `Steam1` sandbox · Bystander: `Steam2` sandbox.
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `sandboxie`, `sandbox-alt`, `hotrepl` (three endpoints),
  `input`, `logs`, `artifacts` (preflight eleven present; the alternate third client configured).
- Artifacts: the `a*`-`k*` readings, `f0*.png` frames, `m-*.txt` marks and `x-*.txt` extracts in the batch
  directory named by `acceptance-artifacts-dir`; ids are written `<host>`/`<member>`/`<bystander>` here.

The session: lobby created by the host, both members joined and the run started; host, member and bystander
all read `inWorld: true` and both character restores landed before the first row (`prelude.log`,
`prelude-host-state.json`, `prelude-guest-state.json`, `prelude-alt-state.json`, batch body-ready reads).

| # | Row (the ticket's six) | Class | Verdict | Evidence |
|---|---|---|---|---|
| H1 | Host `/heal <member>`: the member's health, every limb field, the severed limbs, the hollow plus overlay, and the body's need/vital fields come back to the healthy values, on the member's own client and in the host's copy; the member's console reads what happened | machine (+supporting frames) | **pass** | staged on the member: `UpArmB` amputated (the upper arm, forearm and hand — 3 limbs), limb 10 skin 12/muscle 25/bleed 0.6/pain 70/shrapnel 2, brainHealth 2, consciousness 0, shock 100, a planted splint/tourniquet/chilled component, the three medication components and a mindwipe (`c01`-`c07`, `f06`); the host's selector `@a` (the command's grammar has no bare SteamId; a first attempt with one answered `No players match selector '<member>'`) answered `Reset <member>: sent to that player's own client…` (`c08`); the member's console: `<host> fully reset your state (3 severed limb(s) restored; hollow cleared)` (`c09`); after: `alive:true conscious:true brainHealth:100 shock:0 bloodVolume:100 mindwipePresent:false`, medication amounts 0 (`c10`); all fifteen limbs read `components:"" dismembered:false splinted:false skinHealth:100 muscleHealth:100 bleedAmount:0 pain:0` with the topology intact — limb 0 `isHead:true`, limb 1 `isVital:true`, and the other limbs' `distanceToHeart` unchanged (`c11-guest-limb0..14-after.json`); `medication-component-probe read`: `false/false/false`, `mindwipe:false` (`c12`); the overlay objects: `vignette count=0` (`c13`, before `a16`: `count=1 MindwipeVignette(Clone)`); the host's stored copy: `alive:true conscious:true brainHealth:100 mindwipePresent:false`, amounts 0 (`c14`); frame pair `f06` → `f07` (the member's world goes from the blacked-out state to rendered) |
| H2 | Host `/heal @s` (a selector resolving the host): the host's own body resets; the old self-target silent no-op is gone | machine | **pass** | the host staged the same injury set (`b01`-`b05`: `DownArmB` amputated, limb 10 damaged, splint/tourniquet/chilled components, medications, mindwipe; `b07`: `alive:true conscious:false brainHealth:1.798 shock:100 mindwipePresent:true`); the command ran while the host was **unconscious** and answered `Reset <host>: full heal applied (2 severed limb(s) restored; hollow cleared)` (`b08`); after: `alive:true conscious:true brainHealth:100 shock:0 bloodVolume:100 mindwipePresent:false`, amounts 0 (`b10`); the read limbs are healthy and the head/vital fields are preserved (`b11`, `limb0 isHead:true`, `limb1 isVital:true`); the hollow overlay `count=0` (`b13`); the carried-item count is 3 before and 3 after (`b06`, `b14`); the member's console history shows the answer the host's own command produced (`b09`); frames `f04` → `f05` |
| H3 | A dead member `/heal <member>`: alive again **in place**, both clients read the same outcome | machine | **pass** | the member was dead before the command: `alive:false conscious:false brainHealth:0 mindwipePresent:true` on its own client (`a40`), `alive:false brainHealth:0` in the host's copy (`a42`), and the body lying at `(-0.584, 503.496)` (its pre-death standing position was `(0, 505.615)`, `a03`); the host's answer names the revive: `Reset <member>: sent to that player's own client… — it was dead, so it revives.` (`a43`); the member's console: `<host> fully reset your state (revived from death; 2 severed limb(s) restored; hollow cleared)` (`a44`); after: `alive:true conscious:true brainHealth:100 shock:0 bloodVolume:100 mindwipePresent:false` (`a45`), the read limbs healthy and restored (`a46`), the host's copy `alive:true conscious:true brainHealth:100 mindwipePresent:false` (`a47`), the overlay `count=0` (`a49`), and the body stands at `(-0.584, 505.616)` — the same x, the y of a standing body at that spot, no teleport (`a41` dead pose, `a50` revived pose); frame `f03` |
| H4 | Cheat permission, two-sided: switch on plus roster ⇒ the member's `/heal <target>` works; switch off or absent from the roster ⇒ a local refusal naming the reason and no request in the host's log window | machine | **pass** | the member's rules read announced the switch (`announced:true allowMemberCheatVerbs:true`, `e01`); with the switch on and no roster entry its `/heal @a` was refused locally by the roster's sentence (`e02`) and the host's log window after the byte mark carries **no** `[HealReset]` line (`x-h4a-host-no-request.txt`); the host's `/op <member>` answered `Granted the cheat verbs to …` (`e04`), and the member's `/heal @a` then reached the host: the host log has `<member> reset the host's own body (… applied=True)` and `instructed <bystander> …` (`x-h4b-heal-lines.txt`), the member's console history carries the host's answer `Reset <host>: full heal applied. Reset <bystander>: sent…` (`e08`), and the host's own body (injured, unconscious) reads `alive:true conscious:true brainHealth:100 shock:0` after (`e09`); the host's `/deop <member>` (`e11`) made the member's next `/heal @a` refuse by the roster (`e12`) with no host `[HealReset]` line (`x-h4c-host-no-request.txt`); with the switch turned off through the Admin page control `admin.rule_allow_member_cheat_verbs` (`e16`), the member's rules read `allowMemberCheatVerbs:false forbidsMemberCheatVerbs:true` (`e18`), its `/heal @a` refused by the switch's sentence (`e19`) and the host's window again carries no `[HealReset]` line (`x-h4d-host-no-request.txt`) |
| H5 | The native `heal` and the remote-medical item path behave as before; `/heal` consumes no item and needs no conscious caster | machine | **pass** | native `heal` on the member with a freshly amputated forearm (2 limbs severed), the medications and the hollow (`d02`): after the command the member reads `brainHealth:100 mindwipePresent:false`, the severed limbs `dismembered:false`, the medication components gone and the overlay `count=0` (`d07`-`d10`) — the landed limb/hollow extension behaves as before — while `conscious` stays false and `shock` 100 (`d07`), the state `/heal` clears and stands (H2); the item path is the applied splint round trip judged in the sibling record's I2/I3 (the item is consumed into the limb and handed back with its id); `/heal` consumed no item (host carried-item count 3 → 3, `b06`/`b14`) and ran with an unconscious caster (H2's `b07` `conscious:false`) |
| H6 | The reset reaches the peers and survives a save/reconnect (or the row names the part that does not) | machine | **pass** | the peer copies: the host's stored character reads the reset state (`c14`; after the member's reconnect `j26`), and the member's own client reads it directly (`c10`, `j20`); the save: `/save` committed a mid-run snapshot (`[SaveArchiveWriter] Committed mid-run snapshot…`, `[SaveSeam] world … at revision 759 …`, `x-save-lines.txt`), and the archive's `characters/steam-<member>.json` carries `brainHealth=100, alive/conscious, mindwipeScriptPresent=false, shock=0`, every limb `dismembered=false` and limb 14 `splinted=true` (`x-save-character.txt`); the reconnect: the member quit, relaunched, rejoined the still-running lobby and re-entered the world (`j11`-`j15`), and after the entry restore its own client reads `alive:true conscious:true brainHealth:100 mindwipePresent:false` (`j20`), limbs 7/8/12/13 restored and healthy (`j21`), and the body at `(-5.835, 505.619)` against `(-5.817, 505.619)` before the save (`j08`, `j28`) — in place; the host's stored copy after the rejoin reads the same vitals (`j26`) |

## What the run established beside the rows

- **The row's selector is the command family's selector, not a raw id.** The first host attempt `/heal
  <steamId>` answered `No players match selector '<steamId>'` (`a30`): the resolver's grammar is the
  `@s/@p/@a/@r/@e` base with bracketed filters (`CommandSelectorResolver`), so the row's `<member>` target is
  reached through `@p`/`@a` (this run used `@a`, which also reset the bystander harmlessly). Recording the
  fact so the next batch's plan does not spend a step on it.
- **The reset is genuinely full, on the body and on its components.** The 15-limb read after H1 is the row
  the blocker in the ticket's review needed: the topology fields (`isHead`/`isVital`/`distanceToHeart`) came
  back intact on every limb, the head is still the head and the torso still vital, and the two severed limbs
  plus the hand were re-activated with `dismembered:false`. The three medication components, the splint, the
  tourniquet and the chilled component are all absent, and the mindwipe component and its overlay object are
  gone (`c11`-`c13`).
- **The reset survives the save at the character-record level.** The archive's character file is the fact
  the reconnect path restores from; it carries the healthy state and the restored limbs, and the rejoined
  member reads it back.
- **The native heal is a different surface, and the difference is visible.** The native command (with the
  landed limb/hollow extension) restored the same limbs and cleared the same hollow, but did not clear shock
  or lift the body (`d07`); `/heal` clears the revive baseline and stands the body (H2/H3). The two commands
  therefore remain distinguishable in a live session, which is what the ticket's row 5 asked to see.

## Residuals for the user

None. Every row is a console answer, a client read, a log excerpt or the archive's own file text.

## Limits

- **The raw-SteamId selector refusal is a grammar fact, not a defect**; the ticket's rows use the selector
  vocabulary the command family declares.
- **A member's `health-read source=saved` on a guest is empty by design**: the character store is the host's
  ledger, so the third client's saved-copy read answers `no-saved-character` (`a34`). The peer evidence here
  is the host's stored copy and the member's own local read; no third-peer saved-copy claim is made.
- **The swallowed-announcement staging was not run.** H4 judges the member's local refusals, the granted
  request's both-sided execution and the host's absent request lines; the case where a member holds a stale
  closed roster and its request still reaches the host is the unit-tested shape
  (`CommandConsoleMemberHealResetTests`), not re-staged here.
- **The frame pair is supporting, not the moodle read.** The member's Online UI window was open (the join
  flow leaves it open), and the pre-reset frame's black world is the unconscious state; the hollow claim is
  the component/overlay machine read, and the frames show the world rendering again after the reset.
- **The save/reconnect is a rejoin of the running session**, and the archive was read directly rather than
  reloaded by a Continue; the archive's content and the rejoin's restored state are both read.
