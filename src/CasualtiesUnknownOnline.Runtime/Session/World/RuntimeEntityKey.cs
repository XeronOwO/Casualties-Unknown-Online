using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The creation-record identity of a runtime-created world entity: prefab id +
/// the block-sized cell of its CREATION position (the position the live report
/// carries, not the entity's current position). The world-entity channel has no
/// per-instance wire id, so this key is what the host's accepted-creation table
/// and the guest's pending-report table both index by: a re-report or a
/// re-broadcast for the same creation maps to the same key, while two instances
/// of one prefab created close together stay distinct (the same reason the
/// live channel's match radius is 1 m, not 3 m).
/// </summary>
internal readonly record struct RuntimeEntityKey(string Id, int X, int Y)
{
	internal static RuntimeEntityKey From(EntitySpawnedMsg msg) =>
		new(msg.Id, (int)Math.Floor(msg.Position.X), (int)Math.Floor(msg.Position.Y));
}
