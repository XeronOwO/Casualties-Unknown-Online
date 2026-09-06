namespace CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;

/// <summary>
/// Declarative metadata for one adaptive stream. The catalog is the single
/// source of truth for which streams may be rate-adapted and how conservative
/// the adaptation may be. Priority is 1 = highest, 3 = lowest.
/// <c>MaxBytesPerSecond</c> is an optional per-peer/per-stream byte budget; 0
/// means no byte-budget cap.
/// <c>BaseHz</c> drives Hz-based streams (frame-level moving streams).
/// <c>BaseIntervalMs</c> drives interval-based streams (low-frequency
/// reconciliation/fallback streams); when non-zero it takes precedence over
/// <c>BaseHz</c>. <c>MaxIntervalMs</c> is the optional hard upper bound for an
/// interval-based stream under pressure; 0 means no explicit interval cap.
/// When a byte budget requires an even longer interval than the hard cap, the
/// hard cap wins and byte preservation is best-effort (the same tradeoff as
/// the existing Hz min-clamp).
/// </summary>
public sealed record AdaptiveStreamProfile(
	AdaptiveStreamId Id,
	string Name,
	AdaptiveStreamDeliveryMode DeliveryMode,
	int MinHz,
	int MaxHz,
	int Priority,
	long MaxBytesPerSecond = 0,
	int BaseHz = 0,
	int BaseIntervalMs = 0,
	int MaxIntervalMs = 0);
