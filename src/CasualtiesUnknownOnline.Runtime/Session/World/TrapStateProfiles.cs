using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Pure mapping from the entity-event channel's <see cref="EntityEventKind"/>
/// to the kernel trap-state phase. Events that are only transient presentation
/// (fence hit, cactus bump, jump-pad flash, ...) return null and do not move
/// the kernel state machine.
/// </summary>
internal static class TrapStateProfiles
{
	internal static TrapPhase? Map(EntityEventKind kind) => kind switch
	{
		// Pre-trigger warning edges.
		EntityEventKind.MinePressed => TrapPhase.Warning,
		EntityEventKind.CrystalUnstableTicked => TrapPhase.Warning,

		// Trigger/consumption edges.
		EntityEventKind.MineExploded => TrapPhase.Triggered,
		EntityEventKind.SpikeStabbed => TrapPhase.Triggered,
		EntityEventKind.BearTrapClamped => TrapPhase.Triggered,
		EntityEventKind.StalactiteDropped => TrapPhase.Triggered,
		EntityEventKind.GeyserActivated => TrapPhase.Triggered,
		EntityEventKind.SoundCannonFired => TrapPhase.Triggered,
		EntityEventKind.TurretFired => TrapPhase.Triggered,
		EntityEventKind.CrystalFragileBroken => TrapPhase.Triggered,
		EntityEventKind.CaveTicksSpawned => TrapPhase.Triggered,
		EntityEventKind.ShuttleDoorOpened => TrapPhase.Triggered,
		EntityEventKind.LifepodHeatChanged => TrapPhase.Triggered,
		EntityEventKind.LifepodShowerActivated => TrapPhase.Triggered,
		EntityEventKind.BioTerminalUnlocked => TrapPhase.Triggered,
		EntityEventKind.ScrapEaterProgress => TrapPhase.Triggered,
		EntityEventKind.MedStationHealed => TrapPhase.Triggered,
		EntityEventKind.BatteryInserted => TrapPhase.Triggered,
		EntityEventKind.CrystalMetamorphicTriggered => TrapPhase.Triggered,
		EntityEventKind.CrystalShySwapped => TrapPhase.Triggered,
		EntityEventKind.CrystalEMPActivated => TrapPhase.Triggered,
		EntityEventKind.CrystalMimicTriggered => TrapPhase.Triggered,

		// Destroyed/terminal edges.
		EntityEventKind.TurretSelfDestructed => TrapPhase.Disabled,
		EntityEventKind.CrystalUnstableExploded => TrapPhase.Disabled,

		// Re-arm edges.
		EntityEventKind.BearTrapReleased => TrapPhase.Armed,

		_ => null,
	};
}
