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

## Fourth attempt: a fourth gate, and this one is the game's own

The drive now reports the send side's own state (`activeOpId`, `senderPresent`) so a silent no-op names
its cause, and this attempt hit a DIFFERENT refusal before reaching that point:

`[Shrapnel] refused locally: <host> cannot see <guest> on this client.` — the family's line-of-sight gate,
which is a REAL precondition of the feature (the two clients must see each other in the world) and not a
driver artefact. One attempt in this batch passed it and later ones did not, so the scenario needs the two
clients placed within sight of each other before a slip can be driven at all.

That makes FOUR independent gates this run has had to clear in order, three of them now understood:

1. seeding a limb — solved by `seed-shrapnel`;
2. keeping the shared session alive — solved by opening the NATIVE panel before setting the focus;
3. line of sight — NOT yet solved; the clients start wherever the run places them;
4. where a posted report goes after `RemoteMedicalOperationHandler.ReportShrapnelUpdate` — still open.

## Fifth attempt: all four gates pass, and the patch still does not run

Placing BOTH clients at the same point clears the line-of-sight gate (the refusal disappears from the
log), and a driven slip then reports everything the patch's own guards ask for:

```
"viewOpen": true, "minigameActive": true, "senderPresent": true,
"skinDelta": 5.743, "bleedDelta": 0.695, "painDelta": 12.16
```

The native effect lands on the displayed limb with every value in range — and the authority logs
NOTHING: no `grip slip`, no `DISAGREE`, and none of `HandleUpdate`'s own refusals
(`unknown/wrong session`, `non-owner move`, `piece removed`), which all log at Warn/Information. The
report therefore never leaves the operator's client, and since both of the patch's guards report true at
the instant `BreakGrasp` runs, the working hypothesis is now that
`RemoteShrapnelMinigamePatch.BreakGraspPatch` does not execute at all for this call path — the next thing
to confirm is whether its Prefix/Postfix run and what they see.

One diagnostic defect of this batch's own making is worth recording, because it cost a cycle: the recipe's
`activeOpId` probe reads `_activeShrapnel` off a type it resolves by name, and reported `no-opid-field`
while the same handler answered `minigameActive: true` — i.e. the probe was reading a different object
than the one the guard queries. **A reflection diagnostic has to prove it reached the same instance the
production code uses**, or it reports its own mistake as a finding.

## Sixth attempt: the patch runs, and the session has no operation id

`mode=patches` settles the first question — `BreakGraspPatch.Prefix`, `.Postfix` and its finalizer ARE
attached to the native `BreakGrasp`. Probing the patch's own entry point (`TryBeginGripSlip`, invoked with
the very minigame the patch would pass) returns NULL, and the corrected send-side probe says why:

```
"viewOpen": true, "minigameActive": true, "activeOpId": "0", "senderPresent": true, "tryBegin": "null"
```

**The client's session never received the operation id the host allocates**, so `TryBeginGripSlip`'s first
guard (`session.OperationId == 0`) returns before the window is ever opened — which is why the authority
logs nothing at all: there is nothing to log. That is the root cause of the whole chain of "the report
never arrives", and it is one probe away from an answer:
`RemoteShrapnelOperationHandler.OnStartAckReceived` is what assigns `session.OperationId`, so either it
does not run or its target/item guard rejects the ack it gets. **This one may be a real defect rather than
a driver artefact**, and it is not yet known which.

Note the diagnostic that misled a cycle: reading `OperationId` as a FIELD on the active session returned
`no-opid-field`, because the client-side session type is `RemoteShrapnelUseSession`, whose `OperationId` is
a PROPERTY. The probe now reads both and names the type when neither exists.

## Seventh attempt: the mechanism is verified in the game

Two things had to be true, and both are. **The ack is ASYNCHRONOUS** — the host parks the start request
and asks the target's client first, so the operation id lands about 200 ms later, and a start and a break
cannot share one call. And **`Open` must not be re-run for the break**: it calls `Close`, and `Close`
cancels the very session the break belongs to, so the focus is restored by writing its two static fields.
With the start and the break in separate calls, the run produced the ticket's own evidence:

```
[Shrapnel] session 5 grip slip 2 on piece 0 by <host> (head=False, seed -790031595): derived skin 4.024037 bleed 0.9653509 pain 15.61053 brain 0.
[Shrapnel] session 5 grip slip 2 on piece 0: both sides derived the same values.
[Shrapnel] session 5 break-grasp applied to <guest> limb 1: skin -4.024037 bleed +0.9653509 pain +15.61053 brain -0.
```

For that same slip the operator's own screen reported `skinDelta 4.024, bleedDelta 0.965, painDelta 15.611`
— the three values the authority derived and committed, to the precision the driver prints. The operator
did not wait for the authority, and the two agree.

## Row verdicts after this run

| # | Row | Verdict | Evidence |
|---|---|---|---|
| 1 | bare-hand pull: the affected client's values agree with what the host committed, and the operator's screen ends on the same values | **PARTIAL** | the authority committed exactly the derived values and the operator agrees (above). What is NOT shown is the PATIENT's own body: re-reading limb 1 on the patient's client still returns `skinHealth 100, bleedAmount 0, pain 0`. Whether the affected side is meant to receive them through the state message, and does not, is a NEW question this run opened and did not answer |
| 2 | the same pull replayed for a late joiner | unjudged | needs a third client |
| 3 | an injection whose liquid carries a per-call roll agrees on both screens | unjudged | the census already shows no measurement site rolls; driving one was not attempted |
| 4 | a drink whose liquid carries a per-call roll agrees on both screens | unjudged | same |
| 5 | an unrelated local roll is not perturbed | unjudged | needs a probe on both clients |
| 6 | neither screen shows a number that later changes | **PASS** | the operator's screen and the authority hold the same three numbers for the slip; nothing is corrected afterwards |
| 7 | the derived-value comparison stays empty | **PASS** | `both sides derived the same values`, and no `DISAGREE` line exists anywhere in the run |
| 8 | neither action reads as a stall | unjudged | a feel row; needs the user's own judgement |

The ticket therefore stays in `review/`: two of its eight rows pass on this run's evidence, one is partial
and the rest are unjudged.

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
