using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The narrow ItemService surface the item-action flows (ItemActionSync)
/// compose — abstract extraction, user rule: the action flow needs four
/// ItemService facts (the world-table query, the world-entry adopt, the
/// carried-fact publish and the local correction apply), not ItemService
/// itself. ItemService implements it explicitly and hands itself over at
/// construction, so the dependency graph stays acyclic.
/// </summary>
internal interface IItemActionWorldAccess
{
	/// <summary>Read-only query: is the item in the authoritative world table.</summary>
	bool IsWorldItem(ulong itemId);

	/// <summary>Host only: adopt a changed world item's state into the table entry.</summary>
	void UpdateWorldItemState(ulong itemId, CharacterItemMsg state);

	/// <summary>Publish one carried item's adopted fact (local event + host broadcast — the broadcast self-guards host-only, so one method serves both roles).</summary>
	void PublishCarriedSyncFor(ulong owner, CharacterItemMsg item);

	/// <summary>Role-agnostic local correction apply (the wire entry stays guest-only).</summary>
	void FireCorrectionLocal(CharacterItemMsg item);
}
