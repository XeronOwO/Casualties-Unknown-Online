using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The entity-event kind profiles (EntityEventProfiles): which events are
/// ONE-SHOT consumptions (recorded for the late-joiner snapshot, duplicate-
/// guarded per entity) vs repeatable (each side's copy re-arms naturally).
/// The table is explicit and fully covered — a new kind must be classified
/// here deliberately, or the coverage test fails.
/// </summary>
public class EntityEventProfilesTests
{
	/// <summary>The explicit classification of every kind (the runtime's table
	/// is a HashSet of the one-shots; this is the declared truth the tests
	/// cross-check against, one row per enum value).</summary>
	private static readonly (EntityEventKind Kind, bool OneShot)[] Declared =
	[
		(EntityEventKind.MineExploded, true), // landmine — destroyed + consumed
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
	];

	[Fact]
	public void DeclaredTable_CoversEveryEnumValue()
	{
		var kinds = (EntityEventKind[])Enum.GetValues(typeof(EntityEventKind));
		Assert.Equal(kinds.Length, Declared.Length);

		foreach (var kind in kinds)
		{
			Assert.Contains(Declared, row => row.Kind == kind);
		}
	}

	[Fact]
	public void IsOneShotConsumption_MatchesTheDeclaredTable()
	{
		foreach (var (kind, oneShot) in Declared)
		{
			Assert.Equal(oneShot, EntityEventProfiles.IsOneShotConsumption(kind));
		}
	}

	[Fact]
	public void UnknownKind_NotOneShot() =>
		Assert.False(EntityEventProfiles.IsOneShotConsumption((EntityEventKind)200), "an unclassified kind must not consume a snapshot slot");

	[Fact]
	public void OneShotCount_Matches() =>
		// The count is an audit line: a classification change must be deliberate.
		Assert.Equal(13, Declared.Count(row => row.OneShot));
}
