namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// What the pointer names on THIS client's own scene, as far as an instance id can
/// carry it. The item that is there but has no id is classified apart from "no item
/// at all", because the two have different remedies.
/// </summary>
public enum LocalCursorItemKind
{
	/// <summary>No item is under the pointer — it is over the world, a body, a building, or this build has no scene.</summary>
	None,

	/// <summary>
	/// An item with no CUO instance id on this client yet. The id arrives when the
	/// world item enters the item domain: the host's publish at the end of a world
	/// generation assigns one to every generation-time item, its item snapshot carries
	/// them to a guest, and a runtime item (dropped, thrown, unloaded from a container)
	/// gets one as it enters.
	/// </summary>
	NoInstanceId,

	/// <summary>An item with a live instance id in <see cref="ILocalItemAccess.ResolveCursorItem"/>'s out parameter.</summary>
	Resolved,
}
