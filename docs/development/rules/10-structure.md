# Step 10 — structure review

Read this file and `10-structure.local.md` completely before the commit.

## Shape

One top-level type per file; aggregates stay inside their size and boolean-flag ceilings. Responsibilities are single:
control plane and data plane do not share a class, state is owned by whoever holds it, dependencies point one way and
never cycle. A type that both holds state and does I/O is a smell to split before it grows.

## Registration and wiring

A family registers through discovery (see step 4). A DI container is not a global mutable state store; it wires, it does
not become the place where a decision lives.

## The change says what it did not touch

A structural change names the call sites it deliberately left alone and why. Silence about a sibling is how a family
drifts.
