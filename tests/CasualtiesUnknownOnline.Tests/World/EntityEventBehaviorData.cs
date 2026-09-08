using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The combinatorial data source for the entity-event behavior families: the
/// archive (<see cref="EntityEventArchives"/> — one row per kind) projected
/// into xUnit MemberData. Shared by every behavior-family class so a new kind
/// automatically runs every family (the archive's coverage guard guarantees it
/// cannot be added without a row). The families live in separate classes
/// deliberately: xUnit v2 runs every test of one class strictly serially, so
/// one class holding all families would serialize the whole cross-product and
/// become the suite's critical path.
/// <para>
/// Each family is additionally sharded by entity domain — crystal, trap/hazard
/// and machine/station — because a single 33-row class was still the longest
/// class in the run. <see cref="Families"/> is a PARTITION of
/// <see cref="EntityEventArchives.AllKinds"/> (asserted by
/// EntityEventArchivesTests): a kind appears in exactly one shard, so no row is
/// duplicated and no kind is dropped.
/// </para>
/// </summary>
internal static class EntityEventBehaviorData
{
	/// <summary>Crystal entities: the shared crystal latch/shock/teleport events.</summary>
	internal static readonly IReadOnlyList<EntityEventKind> Crystal =
	[
		EntityEventKind.CrystalElectricShocked,
		EntityEventKind.CrystalFragileBroken,
		EntityEventKind.CrystalUnstableExploded,
		EntityEventKind.CrystalUnstableTicked,
		EntityEventKind.CrystalMetamorphicTriggered,
		EntityEventKind.CrystalShySwapped,
		EntityEventKind.CrystalEMPActivated,
		EntityEventKind.CrystalMimicTriggered,
		EntityEventKind.CrystalTeleportTriggered,
	];

	/// <summary>Trap/hazard entities: mines, spikestabber, bear trap, fence, coil, cactus, pad, stalactite, plants, grabber.</summary>
	internal static readonly IReadOnlyList<EntityEventKind> Trap =
	[
		EntityEventKind.MineExploded,
		EntityEventKind.MinePressed,
		EntityEventKind.SpikeStabbed,
		EntityEventKind.BearTrapClamped,
		EntityEventKind.BarbedFenceHit,
		EntityEventKind.CoilShocked,
		EntityEventKind.CactusHit,
		EntityEventKind.JumpPadLaunched,
		EntityEventKind.StalactiteDropped,
		EntityEventKind.BananaPlantSlip,
		EntityEventKind.GrabberGrabbed,
		EntityEventKind.BearTrapReleased,
	];

	/// <summary>Machine/station entities: geyser, cannon, turrets, cave ticks, shuttle, lifepods, terminals, scrap eater, med station, battery.</summary>
	internal static readonly IReadOnlyList<EntityEventKind> Machine =
	[
		EntityEventKind.GeyserActivated,
		EntityEventKind.SoundCannonFired,
		EntityEventKind.TurretFired,
		EntityEventKind.TurretSelfDestructed,
		EntityEventKind.CaveTicksSpawned,
		EntityEventKind.ShuttleDoorOpened,
		EntityEventKind.LifepodHeatChanged,
		EntityEventKind.LifepodShowerActivated,
		EntityEventKind.BioTerminalUnlocked,
		EntityEventKind.ScrapEaterProgress,
		EntityEventKind.MedStationHealed,
		EntityEventKind.BatteryInserted,
	];

	/// <summary>The three domain shards — a partition of the archive.</summary>
	internal static IReadOnlyList<IReadOnlyList<EntityEventKind>> Families => [Crystal, Trap, Machine];

	public static IEnumerable<object[]> AllKinds() =>
		EntityEventArchives.AllKinds.Select(k => new object[] { k });

	public static IEnumerable<object[]> OneShotKinds() =>
		EntityEventArchives.AllKinds.Where(EntityEventArchives.IsOneShot).Select(k => new object[] { k });

	public static IEnumerable<object[]> CrystalKinds() => Shard(Crystal);

	public static IEnumerable<object[]> TrapKinds() => Shard(Trap);

	public static IEnumerable<object[]> MachineKinds() => Shard(Machine);

	public static IEnumerable<object[]> CrystalOneShotKinds() => Shard(Crystal, oneShotOnly: true);

	public static IEnumerable<object[]> TrapOneShotKinds() => Shard(Trap, oneShotOnly: true);

	public static IEnumerable<object[]> MachineOneShotKinds() => Shard(Machine, oneShotOnly: true);

	private static IEnumerable<object[]> Shard(IEnumerable<EntityEventKind> kinds, bool oneShotOnly = false) =>
		kinds
			.Where(kind => !oneShotOnly || EntityEventArchives.IsOneShot(kind))
			.Select(kind => new object[] { kind });
}
