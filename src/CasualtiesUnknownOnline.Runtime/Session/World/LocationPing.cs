using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// One active transient location ping local to a CUO client. It is UI
/// presentation only: no wire identity, no authority, and no snapshot path.
/// </summary>
public sealed record LocationPing(
	ulong SenderSteamId,
	float X,
	float Y,
	LocationPingKind Kind,
	long PlacedAtMs,
	long ExpiresAtMs);
