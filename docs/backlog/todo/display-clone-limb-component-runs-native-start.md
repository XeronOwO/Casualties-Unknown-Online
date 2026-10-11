# A peer's limb component on a display clone runs the game's own Start and throws every frame

- Status: Todo — **found 2026-10-11 by the acceptance run of batch `20261011-b`**, while it set up the
  Stage-3 removal scenario: the moment a peer's limb carried a splint, the client that had the remote
  medical panel open threw a NullReferenceException **every frame** until the panel was closed.
- Priority: High — a per-frame exception storm on a normal, user-reachable surface (open the medical panel
  on a teammate who is splinted), thousands of log lines a minute, and the panel's own icon bookkeeping
  ends in a broken state. It did not corrupt state in this run, but it is one live object away from doing
  so, and it makes the client's log useless for anything else while it lasts.
- Category: Remote medical / display clone / native component behaviour
- Related: `docs/backlog/todo/medical-removal-limb-component-not-applied.md` (the same component-state
  plumbing, opposite direction: what a removal must take OFF a body), `docs/backlog/review/remote-medical-stage-3-other-actions.md`,
  `docs/evidence/acceptance/medical-state-names-no-kind-20261011-b.md` (this run's record).
- Source: the agent's own two-client run, batch `20261011-b`.

## What the run showed

The host placed a splint on the guest's limb 2 (a declared setup substitution). The guest's next character
report carried the component, and the host — whose medical panel was open on that peer — began logging
this, one per frame:

```
[ERR] [CasualtiesUnknownOnline.Plugin] [Unity:Exception] NullReferenceException
UnityEngine.Component.GetComponent[T] () (at <c39a522eee05469b8171a6cfb646c59>:0)
SplintLimb+<>c.<Start>b__4_0 (Limb x) (at <ab887c07f51841c98ea015c9c29ae1af>:0)
OnLimbWound.Update () (at <ab887c07f51841c98ea015c9c29ae1af>:0)
```

- **5487 exceptions** in one window (12:26:47 → 12:28:28, first storm of the run), and **391** more in a
  25-second window during the real splint-removal drive.
- The storm **stops the instant the native panel is closed** and **resumes the instant it is reopened**
  (both observed in the run: last exception 12:28:07.530, panel closed, then the first exception back at
  12:28:19.905 right after the reopen) — so the thrower is the open panel's per-frame work, not the
  component's own `Update`.
- The other two clients were clean; only the client that had the panel open on the splinted peer flooded.

## Root cause

The display path materialises a real game component on a body CUO only renders:

- `RemoteMedicalCoordinator` applies a peer's limb state to the operator's/observer's display body through
  `LimbComponentStateCodec.Apply`, which does `limb.gameObject.AddComponent<SplintLimb>()` for a limb the
  message says is splinted.
- That is the **game's own component**, so the game's own `SplintLimb.Start` runs and registers a wound-view
  icon: `WoundView.view.AddImageToLimb(this.limb, sprite, false, (Limb x) => !x.GetComponent<SplintLimb>())`.
- The display body is rebuilt under the focus (the run logs `Remote body created for <guest>` right after a
  session starts), so the icon list holds a limb that is no longer alive; `OnLimbWound.Update` then calls
  the icon's removal predicate, `GetComponent<T>` runs on a null `Limb`, and the exception is thrown inside
  the panel's own `Update` — every frame, for as long as the panel stays open.

## What the fix has to do

Keep native component behaviour off display clones:

- the display path needs the component STATE (the wound view already renders `splinted`, and the fact table
  carries the limb facts), not a live instance of the game's component; either the display apply stores the
  state without instantiating the component, or the component is created in a way that cannot register
  native behaviour (no `Start`, no wound-view icon).
- The removal half of the same plumbing
  (`docs/backlog/todo/medical-removal-limb-component-not-applied.md`) must be written with this in mind: a
  destructive apply on a display clone must not run the game's own teardown either.
- A regression case can pin the shape without a game: the display path must not `AddComponent` a type whose
  `Start` touches `WoundView.view`.

## Acceptance rows

| # | Row | Fails when |
|---|---|---|
| 1 | with a peer whose limb carries a splint, one client keeps the remote medical panel open on that peer for a full minute and logs zero `[Unity:Exception]` lines | any exception appears |
| 2 | in the same window the panel still shows the splint (the icon/state is not lost by the fix) | the display stops rendering the splint |
| 3 | the same holds for a tourniquet (`TourniquetScript`) | the sibling component floods |
| 4 | leaving and re-entering the focus on that peer (the display body is rebuilt) stays quiet | the storm returns on the rebuild |

## Limits

- The run observed the storm only on the client that had the panel open; a client that never opens the
  panel was quiet in the same session, which is consistent with the stack but does not prove the icon is
  the only trigger after a display-body rebuild.
- The log evidence is a stack plus the open/close correlation, not a debugger: the null `Limb` is read from
  the game's own predicate, and the candidate mechanism (the rebuilt display body) is the run's own
  observation of `Remote body created` immediately before the first storm.
