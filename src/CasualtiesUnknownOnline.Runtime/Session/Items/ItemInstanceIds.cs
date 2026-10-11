namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The item instance-id format, in one place: ids are
/// <c>(counter &lt;&lt; 32) | account-low</c> — the allocating peer's own counter and the
/// LOW 32 BITS of its account id (<c>_session.LocalSteamId</c>, or the peer id
/// IP-direct mode assigns). That split is what makes an id globally unique per
/// session without host allocation: the counter half belongs to the side that
/// allocated, and the id space is therefore per account.
///
/// The Game Adapter and the interaction services COMPOSE an id when they allocate
/// (<c>ItemIdAllocator</c>); the Runtime READS the halves when it must know whose
/// counter an id came from — the per-space watermarks, and the resume point a restore seeds
/// from the ids an archive carries in BOTH of its carriers (<see cref="ItemIdCoordinator"/>,
/// which the save layer feeds through <see cref="IRestoredItemIdSeed"/>). Both directions live
/// here so the encode and the decode cannot drift apart — and so that no caller compares an
/// id's owner half against a full SteamId, which never matches: the account id is 17 digits,
/// and its low 32 bits are one of <see cref="SpaceOf"/>'s values.
/// <para>
/// There is exactly ONE namespace. A second, synthesized one lived here for the ids a
/// host-side removal award minted; it is gone with the award, because an applied item
/// keeps its id across the round trip and nothing host-side needs an id of its own any
/// more.
/// </para>
/// </summary>
internal static class ItemInstanceIds
{
	/// <summary>One allocation's id: the allocating side's counter and its id space.</summary>
	internal static ulong Compose(ulong counter, ulong accountId) => (counter << 32) | SpaceOf(accountId);

	/// <summary>Which counter of its space the id was allocated from.</summary>
	internal static ulong CounterOf(ulong instanceId) => instanceId >> 32;

	/// <summary>
	/// The id space an account's allocations live in: the low 32 bits of its account id.
	/// This is the key the per-account watermarks are held under, and the value an id's
	/// owner half carries — the two are the same value on purpose, so a restore that
	/// knows only the ids can still address the right entry.
	/// </summary>
	internal static ulong SpaceOf(ulong accountId) => (uint)accountId;

	/// <summary>The id space the id belongs to (its owner half).</summary>
	internal static ulong OwnerOf(ulong instanceId) => SpaceOf(instanceId);
}
