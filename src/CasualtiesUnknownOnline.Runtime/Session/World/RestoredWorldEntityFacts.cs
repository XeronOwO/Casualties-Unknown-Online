using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// One restored cut's world-entity facts in the shape the live world takes them:
/// the three flat fact lists the Game Adapter's own appliers consume (the trap
/// snapshot replay, the opened-entity apply, the building-health apply).
///
/// It is ONE value because the host writes all three in one call, and it is a
/// RUNTIME shape because which kernel fact becomes which row — and how a
/// consumption's elapsed time is derived from the cut's timestamp — is part of
/// the projection, not of the layer that knows the game's types. The Game Adapter
/// therefore applies these rows exactly as it applies the guest's projection;
/// the mapping stays testable without a running game.
/// </summary>
public sealed record RestoredWorldEntityFacts(
	IReadOnlyList<EntityEventMsg> Traps,
	IReadOnlyList<NetVector2Msg> Opened,
	IReadOnlyList<BuildingEntityHealthEntryMsg> Health)
{
	/// <summary>A cut that carried no world-entity fact: the live-world write is a no-op, not a failure.</summary>
	public static readonly RestoredWorldEntityFacts Empty = new([], [], []);

	/// <summary>How many rows the three lists hold together — the count the restore report names.</summary>
	public int Count => Traps.Count + Opened.Count + Health.Count;
}
