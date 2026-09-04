using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The entity-event kind profiles (PURE — no Unity): the per-kind properties
/// the event chain's decisions read. Today one property: IS one-shot
/// (consumption recorded for the late-joiner snapshot, repeated triggers are
/// duplicate-guarded per entity). The table is EXPLICIT — every kind has a
/// row, the tests assert the enum is fully covered, so a new kind is
/// deliberately classified instead of silently inheriting the default.
/// </summary>
internal static class EntityEventProfiles
{
	/// <summary>One-shot consumptions land in the late-joiner snapshot; repeatable events do not (each side's copy re-arms naturally, the vanilla behaviour).</summary>
	private static readonly HashSet<EntityEventKind> OneShotConsumptions =
	[
		EntityEventKind.MineExploded,
		EntityEventKind.SpikeStabbed,
		EntityEventKind.StalactiteDropped,
		EntityEventKind.SoundCannonFired,
		EntityEventKind.TurretSelfDestructed,
		EntityEventKind.CrystalFragileBroken,
		EntityEventKind.CaveTicksSpawned,
		EntityEventKind.ShuttleDoorOpened,
		EntityEventKind.LifepodShowerActivated,
		EntityEventKind.BioTerminalUnlocked,
		EntityEventKind.MedStationHealed,
		EntityEventKind.ScrapEaterProgress,
		EntityEventKind.BatteryInserted,
		EntityEventKind.CrystalUnstableExploded,
		EntityEventKind.CrystalMetamorphicTriggered,
		EntityEventKind.CrystalShySwapped,
		EntityEventKind.CrystalEMPActivated,
		EntityEventKind.CrystalMimicTriggered,
	];

	/// <summary>
	/// Repeatable TRAP-STATE events that are transient presentation only: the
	/// entity's native cooldown/reload re-arms it, so the kernel must not keep
	/// a permanent Triggered fact and replay it on every periodic checkpoint.
	/// Durable repeatable state (bear-trap clamp, lifepod heat) is NOT here.
	/// </summary>
	private static readonly HashSet<EntityEventKind> TransientTrapStates =
	[
		EntityEventKind.GeyserActivated,
		EntityEventKind.TurretFired,
	];

	internal static bool IsOneShotConsumption(EntityEventKind kind) => OneShotConsumptions.Contains(kind);

	/// <summary>True for repeatable trap-state events that must not be snapshotted/projected as durable state.</summary>
	internal static bool IsTransientTrapState(EntityEventKind kind) => TransientTrapStates.Contains(kind);
}
