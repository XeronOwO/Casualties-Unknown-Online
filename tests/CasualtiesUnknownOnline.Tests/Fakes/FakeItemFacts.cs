using System.Collections.Generic;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The kernel's item read model, faked: the admission seam only ever reads a
/// location, so each seeding helper places one row and nothing else. Shared by
/// the seam's own suites, which are split by report family because one class may
/// carry at most <c>TestClassSizeGateTests.MaxCasesPerClass</c> cases.
/// </summary>
internal sealed class FakeItemFacts : IKernelItemFacts
{
	private readonly Dictionary<ulong, ItemState> _items = [];

	public ItemState? FindItem(ulong instanceId) => _items.TryGetValue(instanceId, out var item) ? item : null;

	public IReadOnlyDictionary<ulong, ItemState> QueryItems() => _items;

	internal void CarriedBy(ulong instanceId, ulong owner) =>
		_items[instanceId] = new ItemState(new ItemIdentity(instanceId, "test_item"), 1, ItemLocation.Carried(new ActorId(owner)));

	internal void InWorld(ulong instanceId) =>
		_items[instanceId] = new ItemState(new ItemIdentity(instanceId, "test_item"), 1, ItemLocation.World(1f, 2f));

	internal void AppliedTo(ulong instanceId, ulong owner, int limbIndex) =>
		_items[instanceId] = new ItemState(new ItemIdentity(instanceId, "test_item"), 1, ItemLocation.Applied(new ActorId(owner), limbIndex));

	internal void ContainedIn(ulong instanceId, ulong owner, ulong parentId) =>
		_items[instanceId] = new ItemState(new ItemIdentity(instanceId, "test_item"), 1, ItemLocation.Contained(new ActorId(owner), parentId));

	/// <summary>A WORLD row that names the world container it sits in — the shape a world crate's child has (its location is World, and the parent id is the crate).</summary>
	internal void WorldChildOf(ulong instanceId, ulong parentId) =>
		_items[instanceId] = new ItemState(new ItemIdentity(instanceId, "test_item"), 1, ItemLocation.World(1f, 2f, parentId));
}
