# `/kill` — the game's own kill verb, for every player

- Status: Done — all six acceptance rows read on the deployed build (2026-11-02, 13:20). Row 1 self kill: brain health read before and after (`100 -> 0`), the peer body stayed alive. Row 2 member from host: the instruction travelled and the guest's own body died. Row 3 member-initiated: the guest's `/kill @s` was answered by the host with the instruction line and the guest's body died — a granted member reaches the same path. Row 5 refusal: before the grant the guest was refused locally with a line naming the fix (`/op`), and nothing was killed. Row 6 a dead body killed again: answered, and the log shows `0 -> 0` with no second death. Row 4 (selector) is the recorded gap below, not a match. Code half-written note kept for the record: (`BodyOperation`, the two message fields, `KillCommands`,
  `ILocalBodyKill` and its adapter are in the tree); the build is red on the interface rename
  (`IHealResetControl.Reset` → `Apply(targets, operation)`) and on the command-registration refactor
  below. Nothing is deployed.
- Priority: High (the trader-recruit acceptance row needs a dead teammate, and every future row that
  needs a body dead needs the same verb)
- Category: Console / cheat verb / body state
- Source: User direction, 2026-11-02 — quoted in translation: "then implement the kill command" and "why on earth is kill a local command",
  after the recruit row (`trade-domain-dual-side-runtime` row 8) turned out to need a dead member
  and the mod had no verb that produces one.

## Design
Read from `docs/design/mechanics/console.md` (the console verb family and its cheat gate) and `docs/design/cross-cutting/permissions-and-commands.md` (who may run a cheat verb): a new verb joins that family rather than inventing a second control plane.

**What the player does.** Types `/kill` (no argument: myself) or `/kill <selector>` (a teammate,
`@a`, a name) in the CUO console. They expect the named character to die at once, everywhere: the
target's own screen, every peer's view of that character, and the host's world model.

**What the game itself does.** The native console command is one write plus a log line:

```
ConsoleScript.Commands.Add(new Command("kill", "Instantly kills the player character by setting their brain health to 0.", delegate(string[] args)
{
    this.CheckForWorld();
    PlayerCamera.main.body.brainHealth = 0f;
    this.LogToConsole("Killed the player.");
}, null, Array.Empty<ValueTuple<string, string>>()));
```

(`reversing/Assembly-CSharp/Assembly-CSharp/ConsoleScript.cs`, the `kill` command registration.)
`Body.alive` is `brainHealth > 0f` (`reversing/Assembly-CSharp/Assembly-CSharp/Body.cs`), and the
bleeding, consciousness and death presentation all read that one field — which is why the native verb
writes nothing else. The native verb is SELF-only because it is single-player: it always targets
`PlayerCamera.main.body`.

**How every player's body reaches the same write.** A body exists on its owner's client, so the
target's own client is what writes `brainHealth = 0f` — the same ownership rule `/heal` follows.
That makes `/kill` the SAME exchange as `/heal` with a different operation, not a second protocol:

| step | heal | kill |
|---|---|---|
| console | `/heal <selector>` | `/kill [<selector>]` |
| member asks | `HealResetRequest` | same message, `Operation = Kill` |
| host judges + instructs | `HealResetInstruction` | same message, `Operation = Kill` |
| target applies | `ILocalBodyReset.Reset` | `ILocalBodyKill.Kill` (the native line above) |
| target reports | `HealResetApplied` (post-state) | same message — its health half carries brain health 0 |

Minecraft's current `/kill [<targets>]` template fixes the argument shape (optional targets, the
executor when absent). The deviation from the native self-only verb is the multiplayer one: a selector
may name any player, and the ownership rule is what keeps it honest.

## Work

1. `BodyOperation` on the request and the instruction (wire values 1 = heal, 2 = kill, 0 never sent).
2. `IHealResetControl.Apply(targets, operation)`; the service threads it through the local branch
   (`ILocalBodyKill.Kill`) and the member branch (instruction carries the operation).
3. `ILocalBodyKill` + `LocalBodyKill` (adapter): the native write, then the owner-state report so the
   host and every peer converge.
4. `KillCommands` bound to the same control as `/heal`.
5. **Command registration by attribute, not by hand** (user direction, same session): the command
   groups in `CommandConsoleService` are constructed one by one in a hard-coded list. Every group
   already declares its verbs with `[ConsoleCommand]`; the list itself should be discovered from the
   container (each group registered as itself, the console scans the resolved instances), so a new
   verb group is one registration and never an edit here.
6. Answer lines for the kill operation in `en.json`/`zh.json` (`kill.answer.*`).
7. Tests: the command's shape (self default, selector, no-match, cheat gate), the operation on the
   wire, and the target-side apply path.

## Acceptance rows

| # | Action | Expectation |
|---|---|---|
| 1 | `/kill` on the caller | the caller's body dies at once; every peer sees it dead; the console answers with the brain health it had |
| 2 | host `/kill <member>` | that member's own client applies it; the member's screen shows death; a third client sees the body dead; the host's roster shows it dead |
| 3 | member `/kill <member>` (permitted) | the request travels, the host judges, the target applies — the same as the heal request |
| 4 | `/kill <unknown name>` | refused with the selector's no-match answer, nothing killed |
| 5 | without the cheat permission | refused before anything is looked up |
| 6 | a dead body killed again | answered, no second death presentation, no error |

## Read on the deployed build (2026-11-02, 12:25)

- **Row 1 — self.** `/kill` on the host answered (in translation) "killed the body: brain health to zero" and its body read `alive=False` immediately after. A peer's body read `alive=True`, so only the named body died.
- **Row 2 — a member from the host.** `/kill @a` answered (in translation) "sent the kill instruction to the guest; its own client applies it and reports back"; the guest's own body read `alive=False` while the host, revived with `/heal @s`, read `alive=True`. The operation therefore travels the request/instruction path and the target applies it.
- **Selector forms.** `@a` and `@s` resolve; a bare SteamID and a partial display name answered `no players match` — a selector gap worth a row of its own rather than a silent behaviour (the roster names are the strings `/players` prints, and the id form was assumed, not read, when the command was written).

Rows 3-6 stay open. The first defect this run found was the member answer reading like a reset; it was fixed in the same cycle (`kill.answer.sent`).

## The registration refactor, inventoried (2026-11-02, 12:40)

`docs/development/agent-reference.md` states the rule: for large families of similar registration code (commands, handlers, providers, packets) prefer discoverable attribute + reflection registration over hard-coded linear registration. The console breaks it: `CommandConsoleService` constructs thirteen groups in a fixed list, and three of the inputs it passes are locals built in the same constructor (`ConsoleCommandAuthority`, `ItemDataTargets`, `ItemDataWriteCommands`), so the list cannot simply be deleted.

The shape the refactor takes:

1. A marker `IConsoleCommandGroup` in `Runtime/Session/Commands`, implemented by every group class (they already carry `[ConsoleCommand]` on their methods; the marker is what makes a group discoverable).
2. The three locals become container services (they have no fields of their own beyond their inputs).
3. Each group registers as itself in the composition, and the console takes `IEnumerable<IConsoleCommandGroup>` and calls `AddBuiltIns` once per element — the list moves into the container, where a new verb group is one registration and never an edit to this class.
4. The thirteen groups: `HostAdminCommands`, `CheatAdminCommands`, `SpectatorCommands`, `GamemodeCommands`, `WorldSaveCommands`, `HostRuleCommands`, `ItemDataCommands`, `ItemDataWriteCommands`, `ItemGiveCommands`, `WorldSummonCommands`, `HealResetCommands`, `KillCommands`, `AcceptanceCommands` (plus this class itself, which registers its own verbs).

It is a separate commit from the verb itself: the refactor must land with the full suite and the gates green on its own, and the verb is already deployed and verified.

## The registration rule is satisfied (2026-11-02, 12:38)

`IConsoleCommandGroup` marks every verb group, the three helpers and the thirteen groups are container services, and `CommandConsoleService` registers whatever the container hands it — the only list left is the composition block, which is where a family is announced. Verified on the deployed build: `/kill @a` still answers and the guest's body reads dead, so discovery registers the same verbs the hard-coded list did.

Rows left: 3 (a member runs the verb, which needs the host's cheat grant), 5 (refusal without the grant), 6 (a dead body killed again). Row 4 was read as the selector gap above rather than as a match.

## Rows 3, 5 and 6 read (2026-11-02, 13:20)

- **Row 5 — no grant.** The guest typed `/kill @s`; its console answered (in translation) "refused locally: the host's announced cheat roster does not name you, so /kill cannot take effect; the host grants it with /op", and nothing was killed. The refusal is local, names the remedy, and happens before any request travels.
- **Row 3 — granted.** After `/op <guest>` the host announced the roster and the guest's `/kill @s` was answered by the HOST ("sent the kill instruction to the guest; its own client applies it and reports the body back") and the guest's own body read dead. One reading worth keeping: the first attempt right after `/op` was still refused locally — the announcement had not reached the guest yet — and the second attempt succeeded, so the timestamped roster is what the member gate reads, not the grant itself.
- **Row 6 — a dead body killed again.** A second `/kill @a` over the already-dead bodies was answered normally, and the acting client logged `brain health 0 -> 0 (alive=False)` with no second death and no error.

With these, every acceptance row is judged except row 4, which is the selector gap recorded above: `@a` and `@s` resolve, while a bare SteamID and a partial display name answer "no players match".