using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The shared entity-event kind archive — the declared truth for EVERY kind:
/// one-shot (a consumption recorded for the late-joiner snapshot, duplicate-
/// guarded per entity) vs repeatable (each side's copy re-arms naturally).
/// The single source for the profile tests (cross-checked against the
/// Runtime EntityEventProfiles table) and the phase-5 combinatorial behavior
/// tests (the [Theory] data source — a new kind automatically runs every
/// scenario family). One row per enum value, deliberately classified, the
/// comments carrying the reasoning.
/// </summary>
internal static class EntityEventArchives
{
	internal static readonly (EntityEventKind Kind, bool OneShot)[] Declared =
	[
		(EntityEventKind.MineExploded, true), // landmine — destroyed + consumed
		(EntityEventKind.MinePressed, false), // landmine press visual — a transient one-way edge, NOT a durable snapshot consumption (MineExploded is); duplicate suppression is the local MinePressReplayMarker
		(EntityEventKind.SpikeStabbed, true), // spikestabber one-shot activated
		(EntityEventKind.BearTrapClamped, false), // clamp is reversible
		(EntityEventKind.BarbedFenceHit, false), // repeatable hit
		(EntityEventKind.CoilShocked, false), // repeatable shock
		(EntityEventKind.CactusHit, false), // repeatable bump
		(EntityEventKind.JumpPadLaunched, false), // repeatable launch
		(EntityEventKind.StalactiteDropped, true), // one-shot drop
		(EntityEventKind.GeyserActivated, false), // repeatable eruption
		(EntityEventKind.SoundCannonFired, true), // one-shot spent
		(EntityEventKind.TurretFired, false), // repeatable beam
		(EntityEventKind.TurretSelfDestructed, true), // destroyed + consumed
		(EntityEventKind.CrystalElectricShocked, false), // repeatable shock
		(EntityEventKind.CrystalFragileBroken, true), // broken + consumed
		(EntityEventKind.CaveTicksSpawned, true), // hatched + consumed
		(EntityEventKind.BananaPlantSlip, false), // repeatable slip
		(EntityEventKind.GrabberGrabbed, false), // repeatable grab
		(EntityEventKind.BearTrapReleased, false), // the release half of the clamp
		(EntityEventKind.ShuttleDoorOpened, true), // the doors open once
		(EntityEventKind.LifepodHeatChanged, false), // heat state toggles
		(EntityEventKind.LifepodShowerActivated, true), // one-shot activated
		(EntityEventKind.BioTerminalUnlocked, true), // one-shot unlock
		(EntityEventKind.ScrapEaterProgress, true), // one-shot at 100
		(EntityEventKind.MedStationHealed, true), // one-shot heal
		(EntityEventKind.BatteryInserted, true), // one-shot firstTime consumption
		(EntityEventKind.CrystalUnstableExploded, true), // destroyed by its own explosion — one-shot
		(EntityEventKind.CrystalUnstableTicked, false), // the unstable crystal's ticking START — a transient one-way edge, NOT a durable snapshot consumption (CrystalUnstableExploded is); duplicate suppression is the local CrystalTickingReplay component
		(EntityEventKind.CrystalMetamorphicTriggered, true), // activated latch — one-shot
		(EntityEventKind.CrystalShySwapped, true), // activated latch — one-shot
		(EntityEventKind.CrystalEMPActivated, true), // activated latch — one-shot
		(EntityEventKind.CrystalMimicTriggered, true), // activated latch — one-shot (the crystalenemy spawns ride EntitySpawned)
		(EntityEventKind.CrystalTeleportTriggered, false), // repeatable teleport — no latch; the body teleport rides the 20 Hz player stream, this event only replays the shared laugh/flash
	];

	/// <summary>
	/// Repeatable cooldown-driven trap-state events that are transient
	/// presentation, not durable kernel state. The runtime profile must agree
	/// with this set; the profile tests assert the cross-check for every kind.
	/// </summary>
	internal static readonly HashSet<EntityEventKind> TransientTrapStates =
	[
		EntityEventKind.GeyserActivated,
		EntityEventKind.TurretFired,
	];

	/// <summary>The archive kinds, one row per value — the combinatorial data source.</summary>
	internal static IEnumerable<EntityEventKind> AllKinds => Declared.Select(row => row.Kind);

	internal static bool IsOneShot(EntityEventKind kind) => Declared.Single(row => row.Kind == kind).OneShot;

	internal static bool IsTransientTrapState(EntityEventKind kind) => TransientTrapStates.Contains(kind);
}
