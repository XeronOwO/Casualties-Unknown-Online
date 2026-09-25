using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>One in-world player an enemy may target — engine-agnostic input for the host-side combat arbitration.</summary>
public readonly struct EnemyTargetFact(ulong steamId, NetVector2 position)
{
	public readonly ulong SteamId = steamId;

	public readonly NetVector2 Position = position;
}
