# Namespaced ID system

- Status: Todo
- Priority: Medium
- Category: Architecture / Mod API / Content IDs

CUO IDs should use the `namespace:id` form.

Requirements:

- The original/built-in namespace is `cu`.
- Mod authors should register their own namespace instead of colliding with the
  built-in one.
- The supported scope includes (but is not limited to) items and entities.
- The canonical result should look like `cu:fentanyl` for built-in content.
- Later feature work will build command completion and search on top of this ID
  vocabulary, so the ID model and namespace registration must be designed as a
  stable seam.

Not started; record only.
