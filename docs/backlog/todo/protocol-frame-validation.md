# Protocol frame envelope validation

- Status: Todo
- Priority: High
- Category: Protocol / reliability / robustness
- Source: Loomi architecture review (2026-09-04)

`ProtocolFrame` has a `Kind` plus four nullable envelope slots. Receive paths
currently route by `Kind` and mostly use the first non-null header; there is no
single validation pass that guarantees the frame shape is internally consistent.

Goal: add a unified `ProtocolFrameValidator` before business handlers that checks:

- Exactly one envelope is non-null and it matches `Kind`.
- `Header.PayloadType` matches the concrete payload discriminator.
- Envelope `SenderId` is consistent with the transport sender (or is explicitly
  allowed for host/relay cases).
- Chunk metadata and collection sizes are sane.

Add tests for malformed frames, multiple envelopes, forged sender IDs, oversized
collections, and invalid chunk metadata. Keep the existing unknown-presentation
payload policy non-fatal.
