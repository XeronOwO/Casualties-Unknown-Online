# Duplicate unsynced item drops (guest-dug tree and world-spawned items)

- Status: Todo
- Type: Bug
- Category: Items / world sync
- Source: user report 2026-08-31

When a guest digs a world-grown tree entity, the resulting drops include one
copy that syncs with the host and two extra copies that do not sync with the
host. The two unsynced copies are fixed/immobile at a single point in the air.

The same reproduction also occurs for world-spawned drops such as bandages:
one copy syncs with the host and two extra copies are unsynced/frozen.

