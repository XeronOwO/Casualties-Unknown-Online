using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The event-kind archive completeness guard: every EntityEventKind value must
/// have a declared one-shot/repeatable classification. The archive is a
/// hand-written table — without this guard a new kind silently misses its
/// classification and both the replay matrix and the phase-5 combinatorial
/// tests skip it.
/// </summary>
public class EntityEventArchivesTests
{
	[Fact]
	public void EveryEntityEventKind_HasADeclaredClassification()
	{
		var all = Enum.GetValues(typeof(EntityEventKind)).Cast<EntityEventKind>().ToHashSet();
		var declared = EntityEventArchives.AllKinds.ToHashSet();

		var missing = all.Except(declared).ToList();
		Assert.True(missing.Count == 0,
			$"every EntityEventKind must be declared in EntityEventArchives (one-shot vs repeatable); missing: [{string.Join(", ", missing)}]");
	}

	[Fact]
	public void NoDuplicateClassification()
	{
		var duplicates = EntityEventArchives.Declared
			.GroupBy(d => d.Kind)
			.Where(g => g.Count() > 1)
			.Select(g => g.Key.ToString())
			.ToList();

		Assert.True(duplicates.Count == 0,
			$"each kind must be declared exactly once; duplicates: [{string.Join(", ", duplicates)}]");
	}

	[Fact]
	public void BehaviorFamilies_PartitionTheArchive()
	{
		// The behavior-family MemberData is sharded by entity domain (crystal /
		// trap / machine) to keep every class small. The shards must stay a
		// partition of the archive: no kind duplicated (a duplicated row would
		// run the same scenario twice) and no kind dropped (a dropped kind
		// would silently lose its combinatorial coverage).
		var sharded = EntityEventBehaviorData.Families.SelectMany(family => family).ToList();

		var duplicates = sharded
			.GroupBy(kind => kind)
			.Where(group => group.Count() > 1)
			.Select(group => group.Key.ToString())
			.ToList();
		Assert.True(duplicates.Count == 0,
			$"a kind must belong to exactly one behavior shard; duplicated: [{string.Join(", ", duplicates)}]");

		var missing = EntityEventArchives.AllKinds.Except(sharded).ToList();
		Assert.True(missing.Count == 0,
			$"every archived kind must belong to exactly one behavior shard; missing: [{string.Join(", ", missing)}]");

		var unknown = sharded.Except(EntityEventArchives.AllKinds).ToList();
		Assert.True(unknown.Count == 0,
			$"a behavior shard must only contain archived kinds; unknown: [{string.Join(", ", unknown)}]");
	}
}
