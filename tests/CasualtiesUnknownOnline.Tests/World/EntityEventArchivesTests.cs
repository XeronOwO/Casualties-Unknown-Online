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
}
