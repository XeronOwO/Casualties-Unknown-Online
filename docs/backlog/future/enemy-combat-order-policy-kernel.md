# EnemyCombatOrderPolicy kernel-process follow-up

- Status: Future
- Priority: Low
- Category: Future / architecture

The host-side enemy decisions do not yet feed a kernel process/event. Since the 2026-09-18
ruling the attack paths themselves are announcements judged by the client the effect lands
on (`review/enemy-hit-determination-local.md`), so what remains to kernelize here is the
item-vs-enemy fallback (`EnemyCombatOrderPolicy.DecideItemHit`) and the host's targeting /
bite-action gates (`EnemyCombatArbitration`).
