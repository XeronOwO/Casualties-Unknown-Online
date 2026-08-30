using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Pure mapping from the entity-event channel's <see cref="EntityEventKind"/>
/// to the destructive trap kinds whose host application writes the trap
/// entity's building health to zero. These kinds fold the destroyed-health
/// fact into the atomic trap trigger batch; all other kinds deliberately do
/// not because they carry no deterministic health write.
/// </summary>
internal static class TrapDamageProfiles
{
	private static readonly HashSet<EntityEventKind> DestructiveKinds =
	[
		EntityEventKind.MineExploded,
		EntityEventKind.TurretSelfDestructed,
		EntityEventKind.CrystalFragileBroken,
		EntityEventKind.CrystalUnstableExploded,
	];

	internal static bool IsDestructive(EntityEventKind kind) => DestructiveKinds.Contains(kind);
}
