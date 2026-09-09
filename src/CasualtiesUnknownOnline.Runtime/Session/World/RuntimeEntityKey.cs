using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The creation-record identity of a runtime-created world entity: prefab id +
/// the block-sized cell of its CREATION position (the position the live report
/// carries, not the entity's current position) + the creation-instance token
/// (creator SteamId + monotonic sequence).
/// <para>
/// The cell alone is NOT enough: two identical prefabs created 1.0-1.4 m apart
/// share a cell, and a cell-keyed record made the second report overwrite the
/// first copy instead of materializing it (round-3 finding). The token is
/// stamped by the creating side and travels unchanged through the relay, the
/// absolute snapshot and every fallback re-report, so it is what the host's
/// accepted-creation table, the guest's pending-report table and the per-entity
/// <c>RuntimeEntityCreation</c> marker all index by.
/// </para>
/// </summary>
public readonly record struct RuntimeEntityKey(string Id, int X, int Y, ulong CreatorSteamId, uint CreationSequence)
{
	/// <summary>The key of a creation record (its own creation position and token).</summary>
	internal static RuntimeEntityKey From(EntitySpawnedMsg msg) =>
		new(msg.Id, (int)Math.Floor(msg.Position.X), (int)Math.Floor(msg.Position.Y), msg.CreatorSteamId, msg.CreationSequence);

	/// <summary>The key carried by a snapshot's acknowledgement list.</summary>
	internal static RuntimeEntityKey FromKeyMsg(RuntimeEntityKeyMsg msg) =>
		new(msg.Id, msg.CellX, msg.CellY, msg.CreatorSteamId, msg.CreationSequence);

	/// <summary>The wire form of this key (the snapshot's acknowledgement list).</summary>
	internal RuntimeEntityKeyMsg ToKeyMsg() => new()
	{
		Id = Id,
		CellX = X,
		CellY = Y,
		CreatorSteamId = CreatorSteamId,
		CreationSequence = CreationSequence,
	};
}
