using System.Collections.Generic;
using System.Linq;

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
/// </summary>
internal static class EntityEventBehaviorData
{
	public static IEnumerable<object[]> AllKinds() =>
		EntityEventArchives.AllKinds.Select(k => new object[] { k });

	public static IEnumerable<object[]> OneShotKinds() =>
		EntityEventArchives.AllKinds.Where(EntityEventArchives.IsOneShot).Select(k => new object[] { k });
}
