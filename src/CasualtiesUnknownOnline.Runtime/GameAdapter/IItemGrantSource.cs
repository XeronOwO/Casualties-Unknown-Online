using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The one scene capability the <c>/give</c> verb needs from the Game Adapter:
/// turn an item DEFINITION id into the fresh, save-shaped item fact the item
/// domain can carry — and give it an instance id this client's id space really
/// owns.
///
/// <para>
/// The verb itself is control plane: it parses the operator's words, judges the
/// destination and the properties, and hands the resulting fact to the custody
/// path a cross-player transfer already takes. What it cannot do is name an item
/// the game can materialize: the definition table and the prefab resolution live
/// in the game assembly (<c>Item.GlobalItems</c>, <c>Resources.Load</c>) and only
/// the adapter may read them. The same is true of the instance id: ids are
/// (counter, account id) and the counter lives in the adapter's
/// <c>ItemIdAllocator</c>, so an id minted anywhere else would be an identity the
/// item domain never reported.
/// </para>
///
/// <para>
/// The port is deliberately one member. Which definition exists, what its prefab
/// carries and how an id is allocated are one answer — "a fact for this
/// definition" — because a caller that could ask half of it would hold a fact
/// with no id, or an id with no item.
/// </para>
///
/// <para>
/// A build without the Game Adapter — a test host, a headless one — registers
/// <see cref="UnavailableItemGrantSource"/> instead, so the verb refuses by name
/// rather than pretending the client cannot see the definition.
/// </para>
/// </summary>
public interface IItemGrantSource
{
	/// <summary>
	/// A fresh fact for <paramref name="definitionId"/>: the prefab's own captured state
	/// (condition, liquid stacks, component fields), with <paramref name="state"/> overlaid
	/// onto it when it is not null — compounds overlay recursively, everything else
	/// replaces — a newly allocated instance id and no location.
	/// <see cref="CharacterItemMsg.SlotIndex"/> is -1: where the item goes is the caller's
	/// decision, not this answer's. False with <paramref name="refusal"/> naming the ground
	/// when this build cannot create that definition (an id the game's own table does not
	/// know, a definition whose prefab cannot be loaded) or when the overlaid state names a
	/// key, component, field or value the definition's capture does not carry. The state is
	/// bound BEFORE the id is allocated, so a refusal creates nothing.
	/// </summary>
	bool TryCreate(string definitionId, SnbtValue? state, out CharacterItemMsg? item, out string? refusal);
}
