# Cross-player item-use random determinism — batch 20261011-a

- Ticket: `docs/backlog/review/cross-player-item-use-random-determinism.md`
- Commit under test: `c385f831`
- Deployed artifact: `0.1.0+7e9cac22107f623849682816b42751b5bcb88ee5`, verified against `<game-dir>`
  (`tools/verify-deploy.ps1`, exit 0). The plugin assemblies are unchanged since `7e9cac22`; `c385f831`
  adds only the acceptance recipe, which the driver reads from this tree.
- Date: 2026-10-11
- Dependencies: `tools/acceptance/preflight.ps1` — 11 present, "a full two-client run is possible";
  `session-environment.ps1 -Mode status` — `active=cuo`, `game-running=false`, `swap-needed=false`.

## What this run established

The environment was built end to end and is repeatable:

- two clients (host on the real install, guest in the sandbox box) handshook; the host created a lobby,
  the guest joined, the host started the run, and BOTH clients reported `inWorld: true`;
- the new `seed-shrapnel` recipe seeded live pieces into limb 1 of the GUEST's own body and reported
  `shrapnel: 3, hasShrapnel: true, dismembered: false, skinHealth: 100, bleedAmount: 0, pain: 0` — a clean
  baseline for the rows below;
- both clients were stopped through the driver's own `quit`; no game process was left running.

What was NOT driven is the whole of this ticket's subject: no grip slip was produced (see the row
verdicts), so none of the behavioural rows was judged.

## An existing defect this run found and fixed

The first attempt failed AT THE HANDSHAKE. The host logged

`Handshake from <guest> rejected: cuo.example claims Synchronized but the host does not run it — the host cannot arbitrate it.`

and the guest never entered the world (`gateText: "Waiting for the host to load…"`, `inWorld: false`).
The cause is not this ticket's change: the example mod's deployed DLL was dated 2026-08-16 while the CUO
assemblies were the current build, so the host logged `[Mods] cuo.example failed to load — skipped` and
the two sides advertised different mod sets. Rebuilding `src/CasualtiesUnknownOnline.ModExample` and
refreshing its deployed DLL made the host load it (`cuo.example loaded (bind + initialize + start in the
discovery frame)`) and the handshake succeeded on the next run, with the host then reporting
`Waiting for 1 player(s) to load…`.

Unresolved detail, stated rather than papered over: why the SAME stale DLL registered on one side and
failed on the other is not explained. The observable fact is "the stale build breaks the handshake", not a
mechanism. `tools/deploy.ps1` does not deploy the example mod at all, which is how it drifted — residual 2.

## Second attempt (same batch): the driver landed, and it found where the chain stops

`tools/acceptance/recipes/grip-slip-drive.cs` (commit `11567fa0`) now drives the slip through the plugin's
own seams by reflection, in separate modes so a failure names the seam: `probe` (the bridge, its domain
field and the session's member list), `open`, `start`, `break`, and `auto` for the last three in one call.
What it established on a live pair:

- the reflection path resolves — `GameAdapterBridge` → its `<domains>P` field → `SessionService` →
  `Members`, whose `SteamId` is a FIELD rather than a property — and the peer id comes out as
  `<guest>` against a local `<host>`;
- `RemoteMedicalView.Open` followed by `TryStartRemoteShrapnelSpecial` starts the shared session: the host
  logs `[Shrapnel] session 2 created for <guest> limb 1 (3 pieces)` and `operator ... joined session 2`;
- the minigame's held slot plus its public `BreakGrasp()` really does run the native effect on the
  DISPLAYED limb: the driven slip moved it by `5.621` skin / `0.555` bleed / `11.353` pain, all inside the
  native `4-6` / `0.4-1` / `9-16` ranges, which is the first half of row 6 (what the operator's screen
  holds).

**Where it stops.** The session does not survive the call that starts it: the host log shows
`operator ... left session 2 (Cancelled)` in the SAME millisecond as `session 2 created`, so by the time
`BreakGrasp` runs the minigame is already gone and the patch's `IsActiveShrapnelMinigame` guard refuses.
The authority therefore never drew, and rows 1, 6 and 7 are still unjudged. Whether the cancel comes from
this driver opening and starting inside one call, or from something in the chain a real mouse gesture would
not hit, is NOT established — finding it is the next step, and it may be a real defect rather than a
driver artefact.

## Third attempt (same batch): the session survives, the report does not arrive

Opening the NATIVE panel first is what the earlier attempt missed. The coordinator's pump tears a remote
focus down while the native WoundView is closed, and that teardown cancels the in-flight session — hence
`session created` and `left session (Cancelled)` in one millisecond. With the panel opened first
(`PlayerCamera.WoundViewButton()` plus `WoundView.view.body = displayBody`, then the focus), the session
SURVIVES: the host logs `session 4 created` and `operator ... joined session 4` with no cancel line.

A driven slip then satisfies BOTH of the patch's guards — the recipe reports `viewOpen: true` and
`minigameActive: true` at the instant `BreakGrasp` runs, and the native effect lands on the displayed limb
(`4.555` / `0.623` / `9.062`, all in range) — and the authority still logs nothing for it.

**The blocker is now narrow and named.** Nothing appears between
`RemoteMedicalOperationHandler.ReportShrapnelUpdate` and `ShrapnelOperationSessionService.HandleUpdate`,
and `HandleUpdate`'s refusals (`unknown/wrong session`, `non-owner move`, `piece removed`) all log at
Warn/Information — so it is not being reached at all, rather than rejecting. A GRAB report posted through
the same seam does not set an owner either, which points at `SendUpdate`'s own guard or at the sender
delegate rather than at the authority's rules. That single question is what stands between this batch and
rows 1, 6 and 7.

## Row verdicts

Every behavioural row is `blocked`, and the blocking condition is ONE missing capability rather than a
failed check. The rows are those of the ticket: 1-5 under *Acceptance shape*, 6-8 under *The target the
user set*.

| # | Row | Verdict | Why |
|---|---|---|---|
| 1 | bare-hand pull: the affected client's skin/bleed/pain agree with what the host committed, and the operator's screen ends on the same values | blocked | no grip slip can be driven (see below) |
| 2 | the same pull replayed for a late joiner | blocked | same |
| 3 | an injection whose liquid carries a per-call roll agrees on both screens | blocked | same |
| 4 | a drink whose liquid carries a per-call roll agrees on both screens | blocked | same |
| 5 | an unrelated local roll is not perturbed | blocked | same |
| 6 | neither screen shows a number that later changes | blocked | same |
| 7 | the derived-value comparison stays empty | blocked | same |
| 8 | neither action reads as a stall | blocked | same |

### The missing capability, named exactly

Driving one grip slip needs four things the driver's vocabulary does not have, and no weaker check
substitutes for them (they are the subject of the rows, not a convenience):

1. opening the remote medical view — `RemoteMedicalView.Open(Body, ulong, string)` is an internal static,
   and the native path into it is `PlayerCamera.TryPerformSpecialUIAction`, which the patch intercepts and
   which no recipe drives;
2. a peer's SteamId, which `Open` needs and which **no patch-bridge surface exposes**: `PatchBridge.Impl`
   carries the LOCAL id (`LocalSteamId`) and `SessionSurface` carries two UI flags and nothing else. A
   driver has to reach the session's member list — or the renderer's SteamId-to-body map — through the
   assembly's internals as well;
3. starting the shared shrapnel session on a target limb — `GameAdapterBridge.TryStartRemoteShrapnelSpecial`
   / `TryStartRemoteShrapnelUse`, which read the target from `RemoteMedicalView.TargetSteamId` and are
   reached today only from the wound-view release gesture;
4. putting the native `ShrapnelMinigame` into the held state and calling its public `BreakGrasp()`, because
   the game reaches `BreakGrasp` from its own `Update` on mouse motion (`ShrapnelMinigame.cs:109-118`), and
   this project drives no OS-level input.

A recipe that did all three through the assembly's own seams would make rows 1, 2, 5, 6, 7 and 8 runnable;
row 3 and row 4 additionally need a liquid that carries a per-call roll to be in a container the operator
can offer.

## What the run did NOT prove, beyond the rows

- That the operator's window really puts the engine stream on the derived state and the native call really
  draws from it — this is the mechanism's own runtime claim and it needs the driven slip above.
- That the gore clip pick and the pitch shift are the only two extra draws inside the native call. The
  offline gate `ShrapnelGripSlipDrawOrderGateTests` pins CUO's script; only a driven slip confirms the
  game's side of it.
- Whether `Resources.Load<AudioClip>("Sounds/gore1"…"gore5")` is non-null in a live install, which the
  authority's script assumes.

## Residuals

1. **The drive capability above.** Until it exists, this ticket's behavioural rows stay `blocked` and the
   ticket stays in `review/`.
2. **`tools/deploy.ps1` does not deploy the example mod**, so a stale copy in `<game-dir>` silently breaks
   the next run's handshake. Either the deploy owns it or a preflight row reports its staleness.
3. **A `.old` backup of the replaced example-mod DLL is left in `<game-dir>`** by this run; the closing
   cleanup removes it (nothing loads a non-`.dll` file).

## Lessons

- **A green preflight is not a working session.** Every dependency row passed while the session could not
  start at all, because the blocker was a content mismatch inside the install rather than a missing tool.
- **When a log line already names the cause, follow it before the history.** The host said in plain words
  that the guest claimed a mod the host was not running; the first diagnosis went after a remembered
  "member missed the world-entry edge" instead, and cost a cycle.
- **A stale deployed artefact can be invisible to every gate.** The example mod's DLL is not built into the
  plugins tree by any tool, so nothing compares it with the tree it came from.
