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
	/// <summary>The explicit classification of every kind — the shared archive
	/// (EntityEventArchives), the single source for the profile tests AND the
	/// combinatorial behavior tests (a new kind automatically runs everything).</summary>
	private static readonly (EntityEventKind Kind, bool OneShot)[] Declared = EntityEventArchives.Declared;

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
		Assert.Equal(18, Declared.Count(row => row.OneShot));

	[Fact]
	public void IsTransientTrapState_MatchesTheDeclaredTable()
	{
		foreach (var kind in (EntityEventKind[])Enum.GetValues(typeof(EntityEventKind)))
		{
			Assert.Equal(EntityEventArchives.IsTransientTrapState(kind), EntityEventProfiles.IsTransientTrapState(kind));
		}
	}

	[Fact]
	public void TransientTrapStates_AreRepeatableCooldownKinds()
	{
		// These two are repeatable cooldown-driven presentation: the native
		// entity re-arms, so the kernel must never project a permanent fact.
		Assert.True(EntityEventProfiles.IsTransientTrapState(EntityEventKind.GeyserActivated));
		Assert.True(EntityEventProfiles.IsTransientTrapState(EntityEventKind.TurretFired));

		// Durable repeatable state stays snapshotted/projected.
		Assert.False(EntityEventProfiles.IsTransientTrapState(EntityEventKind.BearTrapClamped));
		Assert.False(EntityEventProfiles.IsTransientTrapState(EntityEventKind.LifepodHeatChanged));

		// Transient entries must not be one-shot consumptions as well.
		Assert.False(EntityEventProfiles.IsOneShotConsumption(EntityEventKind.GeyserActivated));
		Assert.False(EntityEventProfiles.IsOneShotConsumption(EntityEventKind.TurretFired));
	}

	[Fact]
	public void UnknownKind_NotTransient() =>
		Assert.False(EntityEventProfiles.IsTransientTrapState((EntityEventKind)200), "an unclassified kind must not be treated as transient");
}
