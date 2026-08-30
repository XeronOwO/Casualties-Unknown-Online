# Guest-mined block leaves ghost fragments on host

- Status: Todo
- Type: Bug
- Category: World / block sync
- Source: user report 2026-08-31

When a guest digs a block to completion, the host correctly sees the block
removed, but the broken/fragmented digging effect remains in the air at the
mined position. The host effectively sees "fragmented air".
