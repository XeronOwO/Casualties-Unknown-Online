# Runtime log errors (2026-08-30)

- Status: Resolved
- Type: Bug / investigation
- Category: Runtime observability

Investigated from the captured host/guest logs. The only reproduced error is a
HotRepl startup `TypeLoadException` from NJsonSchema/Newtonsoft resolving
`System.ComponentModel.DataAnnotations.RequiredAttribute`
(`System.ComponentModel.Annotations, Version=5.0.0.0`) — not CUO code, and CUO
continues loading normally afterward. The reported
`OnlineUiOverlay.Draw` ArgumentException ("Getting control 1's position...") is
not present in the captured log set; no CUO code action was taken.
