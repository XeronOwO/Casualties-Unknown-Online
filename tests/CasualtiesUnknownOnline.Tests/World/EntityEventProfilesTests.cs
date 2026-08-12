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
		Assert.Equal(17, Declared.Count(row => row.OneShot));
}
