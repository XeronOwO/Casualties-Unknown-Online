# Cheat permission, `/op` and `/deop`: who may run the cheat verbs

- Status: Todo — **filed 2026-10-18** from the user's ruling on the `/give` verb's authority question (stage C of
  `docs/backlog/todo/item-and-entity-data-commands.md`). The ruling in the user's own words: `/give` is made
  HOST-ONLY first, and a command-permission system is then required — "`/op` `/deop` 这种命令能给他人授权或剥夺作弊
  权限(拓展:将某人加入作弊权限白名单,需要同时考虑 Steam ID 与 IP 直连场景,对于 IP 直连场景可能需要额外评估玩家名
  伪造问题),主机的作弊权限通过联机开关控制,授权与剥夺只允许主机对其余客机执行".
- Priority: Medium
- Category: Command surface / session authority / permissions
- Related: `docs/backlog/todo/item-and-entity-data-commands.md` (the cheat verb family this gates — `give` now,
  `spawn` next), `docs/backlog/done/host-rules-replication.md` (the announcement pattern a member's client reads
  to refuse a closed rule locally), `docs/backlog/todo/remote-backpack-access-policy.md` (a host policy that gates
  a gesture, the same shape one level down), `docs/en/internals/permissions-and-security.md` (the admission seam
  and what a console verb may assume), `docs/decisions/active.md` decision 262 (a member's report is bounded by
  data; a permission list is a host ruling, not a client-enforced boundary)
- Source: the user's 2026-10-18 ruling (above), given as the answer to "who may run `/give`".

## What is asked

1. `/op <player>` and `/deop <player>`: the host grants or revokes another player's cheat permission. Only the
   host may run either, and only ON OTHER guests — a member cannot op itself or another member, and the host's own
   standing is not something a verb changes.
2. A cheat-permission WHITELIST rather than a one-shot flag: the list holds who may run the cheat verbs, and
   `/op`/`/deop` edit it.
3. Identity: Steam ID is the normal key. The IP-direct join has no Steam identity, so that case needs its own
   evaluation — a display name there is claimed, not proven, and a whitelist keyed on it would be spoofable.
4. The host's own cheat permission is governed by the online switch (the session/online toggle), not by `/op`.
5. The verbs the permission gates are the cheat family: the item-data verbs (`/data`), `/give`, and the `/spawn`
   stage when it lands; the exact list is part of this ticket.

## Open questions the ticket must answer before implementing

- **The IP-direct identity question** — what a whitelist keyed on a Steam ID does with a peer that has none:
  refuse cheat permission outright for IP-direct peers, key them on the address the transport itself authenticated
  (and accept that a NAT/reconnect can change it), or key them on a host-granted session token. Each has a
  different failure mode; the user's wording ("may need an extra evaluation of player-name forgeries") points at
  the name not being an identity.
- **Session scope** — does the whitelist live for the session, the world, or the config profile? A run restart or
  a rejoin must not silently re-grant or leak a permission; the host-rules announcement is the precedent for how
  the decision reaches the members.
- **The refusal and its answer** — a member typing a cheat verb it lacks must be refused BY NAME on its own
  console (the family's rule), which means the member's client needs the list (or its own entry) announced, and a
  refusal that arrives after the fact needs the same treatment as the item-data write's way back.
- **What the permission is NOT** — it is a host ruling on ordinary clients, not an anti-cheat boundary: a modified
  client can type its own console. Every cheat verb still rides the host's admission seam and the host still judges
  what it accepts (`docs/en/internals/permissions-and-security.md`; decision 262), so this ticket adds a
  who-may-ask rule, never a who-can-prevent rule.

## Non-goals

- Not an anti-cheat mechanism (above); not a replacement for the host's per-verb rules (`HostRulesOptions`).
- Not a general account system: no persistence outside what the host explicitly saves, no remote administration.
- Not the `/give` implementation itself: that verb ships HOST-ONLY first, and this ticket opens it to the
  whitelist when it lands.
