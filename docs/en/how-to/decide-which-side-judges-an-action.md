# Decide which side judges an action

[Documentation](../../README.md) > [How to](README.md) > Decide which side judges an action

**After this page** you can place a new decision on the right machine — judgment ownership: which
client judges whether something happened, and what the host keeps for itself. Read
[Send a message to the other players](send-a-network-message.md) and
[Read game state](read-game-state.md) first — this page decides how the two are used.

## The rule

A judgment about what happens to a player belongs to **that player's own client**, on its own view and
its own timeline. The host owns the world it simulates and the arbitration of conflicting claims; it
does not decide the outcome of another player's body, reach or operation timing. Latency is never a
parameter of a judgment — not a tolerance window and not a measured round-trip time.

The reason is the cost of being wrong. A host verdict about somebody else's screen is paid on every
single hit, because it has to travel there and back before anything may happen; a rollback after a
refused claim is paid only when two players actually race, which is rare.

## Ask three questions

1. **Whose body, reach or timing is the outcome about?** Then that player's client judges it, against
   its own screen, and reports the terminal fact — it does not ask permission first.
2. **Is it a claim about the world?** An item's owner, a spawn, a consumed trap: the host arbitrates,
   first claim wins, and the answer comes back immediate and precise.
3. **Is it presentation only?** Then it stays local. It needs no wire, no verdict and no arbitration.

A design where the answer to all three is "the host" is not a stricter design; it is a design that
makes every player's own actions wait for someone else's machine.

## What the host still owns

- **The world it simulates.** World generation, shared entity creation and saves are host-only facts.
- **Arbitration between claims.** Two players claiming one item is settled first-writer-wins, and the
  loser rolls back.
- **Eligibility, not outcome.** A command submitted over the wire has its actor bound to the
  transport sender, and the admission seam decides whether that member was allowed to submit it at
  all. Eligibility is all it decides — never what happens to another player's body, reach or timing —
  and every domain verdict stays with the kernel.

## The families that landed, as worked examples

| Family | Who judges | What the host keeps |
|---|---|---|
| Enemy hit | each client, on its own screen: a real collider contact plus the game's own facing gate | the enemy's action and its own body's collision path; it announces the attack, never a victim or a limb |
| Interaction gates — line of sight, operation preconditions | the two clients involved | nothing: the gate is local to the operation |
| Medical operations on one victim | each operator, as a per-unit claim | arbitration between claims, instead of an exclusive lock |
| Creating an item mid-operation | the creating client registers the item **before** anything acts on it | the kernel's item identity; a refused creation leaves a tombstone instead of a guessed wait |
| World-time acceleration | started locally by whoever wants it | arbitration between competing requests, with a broadcast fallback |

These families are implemented and covered by simulation and static evidence; the two-client acceptance
run is still pending.

## Traps

- **A fixed tolerance window is the smell.** If the design needs "wait 500 ms and assume it arrived",
  the judgment sits on the wrong machine — and the window guesses instead of measuring.
- **Never feed the 1 Hz character snapshot into arbitration.** It is a read model; a reading is not a
  verdict, and a value that arrives a second late must not decide who owns an item.
- **A local judgment still reports its terminal fact.** Local does not mean private: death,
  unconsciousness, ownership transfer and consumption are kernel events, so every machine converges
  on the same world.
- **Rollback is the accepted cost, not a defect.** Design the rare race to be visible and recoverable
  rather than paying for it on every hit.

## Check that it worked

Read your change and name the machine that decides — and show that no latency value enters the
decision. In a session, a judgment on the wrong side shows up as the affected player reacting to
something they cannot see on their own screen: a hit through a wall, an action that completes and then
un-happens, an item that flickers back. The exact feel of that is a two-client run, which is an
acceptance step no test can replace.

## Related reading

- [Send a message to the other players](send-a-network-message.md) — how an announcement reaches the other clients
- [Read game state](read-game-state.md) — what a client may read, and why it is not a verdict
- [Declare permissions and host commands](declare-permissions-and-commands.md) — the host-side surface
- [Your first mod](../start/your-first-mod.md) — the lifecycle the decision lives in
- [Glossary](../reference/glossary.md) — kernel, event, projection, rollback

[Documentation](../../README.md) > [How to](README.md) > Decide which side judges an action
