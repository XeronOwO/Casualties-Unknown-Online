namespace CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;

/// <summary>
/// Declarative metadata for one adaptive stream. The catalog is the single
/// source of truth for which streams may be rate-adapted and how conservative
/// the adaptation may be. Priority is 1 = highest, 3 = lowest.
/// <c>MaxBytesPerSecond</c> is an optional per-peer/per-stream byte budget; 0
/// means no byte-budget cap.
/// </summary>
public sealed record AdaptiveStreamProfile(
	AdaptiveStreamId Id,
	string Name,
	AdaptiveStreamDeliveryMode DeliveryMode,
	int MinHz,
	int MaxHz,
	int Priority,
	long MaxBytesPerSecond = 0,
	int BaseHz = 0);
