# Mod-authored effects: the code registration face and the operation surface it writes through

- Status: Todo — **promoted 2026-10-08 by the user's directive**, which supersedes the ceiling ticket's
  Stage 3 gate. The gate said "only if stage 2 leaves a real need ... trigger is a second real consumer";
  the user's ruling is that a highly customisable effect surface is the point of the mod platform, so the
  consumer count is no longer the trigger. The old gate is recorded as SUPERSEDED (not deleted) in
  `mod-content-ceiling.md`'s Stage 3 paragraph, with this ticket as its successor.
- Priority: High (user-promoted)
- Category: Mod platform / mod API
- Parent: `docs/backlog/todo/mod-content-ceiling.md` (Part 2 Stage 3 and Part 3.A's effect gap)
- Related: `docs/backlog/review/mod-declared-behaviour-with-no-function.md` (the cycle that makes a
  declaration without a function non-fatal, and whose inert default is this ticket's no-effect-registered
  path), `docs/en/reference/mod-api.md`, `docs/en/reference/modification-policy.md` (the tiers and the
  promotion funnel), `docs/decisions/active.md` (184: the affected side judges on its own picture),
  `docs/en/reference/modification-policy.md` (the `Abstractions` no-game-type rule)
- Source: the user's 2026-10-08 rulings, in their own words: "既然做了抽象层，开放了抽象接口，为什么不把可能用
  到的功能都做成接口 api 下放呢？你就应该考虑到，有玩家想拓展高度自定义的效果", and then "可以将 GameObject
  这种基础类型通过接口属性暴露出来，假如有实力的开发者就想获取游戏原生对象，他就用 as 去取" (the
  engine-typed hatch, design point 4)

## Why this ticket exists

A mod liquid can declare `HealthUsable` / `Injectable`, but the API cannot carry the function those flags
gate, and the game calls it unguarded — today that is a crash, and
`mod-declared-behaviour-with-no-function.md` makes it a defined, inert outcome. That ticket deliberately
stops at "no function is expressible". This one is the other end: a mod SHOULD be able to author the effect,
and the platform should hand it enough to write one.

## The design this ticket is built on (decided, not open)

1. **The effect is registered in CODE, keyed by content id.** Content travels as an opaque payload to every
   peer; a function is not data and cannot ride it. What travels stays the content's data (id, colour, value,
   flags, qualities); the behaviour is code each peer already has, looked up by the id it was registered
   under. That is also what makes the cross-player families work with no wire change: they already send the
   liquid id and the amount, and the affected player's own client runs the game's own delegate.
2. **The callback stays CUO-typed.** `Abstractions` keeps its rule — no game assembly, no Unity type — so the
   effect's signature names CUO's own context, never `Body` / `Limb` / `ItemInfo`. The registration face
   lives in `Abstractions` (a mod registers through `IModContext`), and the Game Adapter is what bridges it
   onto the vanilla delegates.
3. **The primary path is NAMED OPERATIONS.** What the mod may write is reached through CUO's own named
   operations, grown from the game's own effects by the census below, so the write set is enumerable,
   documented, permission-checkable and stable across a game update.
4. **An ENGINE-typed escape hatch sits beside them** (user ruling 2026-10-08: "可以将 GameObject 这种基础类型
   通过接口属性暴露出来，假如有实力的开发者就想获取游戏原生对象，他就用 as 去取"). The effect context
   carries the engine object the effect landed on, typed with the ENGINE's own type (`GameObject`) — never a
   game type, so the contract's surface does not churn with the game; only the mod's own `as` /
   `GetComponent` cast can, and that is the native-binding risk the mod already declares. It is usable
   rather than decorative: `Body`, `Limb` and `Item` are each `MonoBehaviour`
   (`reversing/Assembly-CSharp/Assembly-CSharp/` — `public class Body : MonoBehaviour`, `Limb.cs`,
   `Item.cs`), so the object recovers them; the plain-class content (`ItemInfo`, `LiquidType`) needs no
   handle at all, because it is reachable by id from the game's own registries.
   - The rejected shape is the UNTYPED hole (`object` as the surface): it cannot be documented, cannot be
     type-checked, and says nothing about what it is. An engine type says what it is.
   - The hatch's own rules, written into the contract: it is the object of the state THIS client owns
     (decision 184 — a fact about another player is not read here), Unity null semantics apply (`== null`),
     and a mod that uses it is outside the API's promise and declares a `NativeBinding`.
   - **The payload side does not change**: registrations and content definitions stay data-only (no Unity
     type, no game type), because they travel to every peer. The repository already draws the line where
     this needs it — only `Assembly-CSharp` is reserved for the adapter, engine modules are allowed
     (`GameAssemblyReferenceGateTests`: "every other framework project compiles against the adapter's ports,
     the Runtime and the engine modules instead of the game's own code") — and `Abstractions` simply carries
     no engine reference today, so adding one is this ticket's reviewed change to the contract's
     dependencies, with the docs updated in the same change.
5. **The effect runs on the client that owns the affected state** (decision 184): the liquid chains already
   run the liquid's own delegate on the affected player's client, so a registered effect lands there with no
   new message.
6. **Permission and consistency have their own answers.** Writing another player's body is a new capability
   class: it needs a `ModPermission` gate of its own, and the handshake's existing mod-id / version /
   `NativeBinding` parity is what makes "same content, same code" true across peers. Both get designed in
   this ticket, not assumed.

## First deliverable (before any code): the operation census and the interface shape

The exposure list is evidence, not taste. Step 1 is to read every vanilla liquid effect the game ships —
each `onDrink` / `onHealthUse` body in the game's own liquid registry — and census exactly which writes
they perform: hydration, temperature, happiness, sickness, blood viscosity, the component doses (opiate,
painkillers, medication scripts), the timed bodies (`CoUtils.DoTimedOp`), limb state (disinfect, pain,
muscle health, bleeding), the per-call random rolls and the clips. Step 2 turns that census into the
operation list plus the effect context (which liquid, how much, which target, which limb when the native
call has one), and step 3 brings the shape to the user for approval as this ticket's own scope. Only then
does code get written.

The item half (`useAction`, and the limb action Part 3.A lists) is the declared follow-on, not this stage;
the census decides how much of it the same operation surface already covers.

## Non-goals

- Not an UNTYPED game-object hole (`object` as the surface), and not a Unity or game type in any payload:
  the hatch is engine-typed and runtime-only (design points 4 and its payload half).
- Not the crash fix and the inert default — that is `mod-declared-behaviour-with-no-function.md`.
- Not a new wire family: the liquid chains already carry what the effect needs. If a later stage needs one,
  it takes its own message and its own protocol decision.
