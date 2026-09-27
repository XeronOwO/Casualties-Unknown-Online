# Protocol frame envelope validation

- Status: Done
- Priority: High
- Category: Protocol / reliability / robustness
- Source: Loomi architecture review (2026-09-04)
- Acceptance record: `docs/evidence/acceptance/protocol-frame-validation-20260927.md`

Landed a unified `ProtocolFrameValidator` in front of `KernelProtocolService.HandleFrame`.
It validates exactly one envelope + kind match, payload discriminator, transport
sender consistency, checkpoint metadata, and collection bounds. Unknown
presentation payloads remain non-fatal.

Tests: `ProtocolFrameValidatorTests` (22) plus malformed/forged-frame integration
cases in `KernelProtocolServiceTests`; full suite green.
