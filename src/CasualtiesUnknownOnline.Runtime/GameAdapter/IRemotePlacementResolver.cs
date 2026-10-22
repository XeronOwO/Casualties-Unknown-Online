using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The receiving client's own placement rules, asked as a QUESTION: where would this item go on this body, and
/// would the named container take it? It is the answer the give's custody waits for (the user's 2026-10-22
/// ruling: the item stays with the giver when the destination cannot take it), so it must run the same rules
/// the placement runs — a free body slot, the hand-only rule, a container's own <c>CanHoldItem</c> — WITHOUT
/// materializing the item into either player's inventory.
/// <para>
/// The adapter answers; the Runtime decides what the answer means. The port's shape is deliberately one call
/// with one result, because the check and the placement must be one resolution: two that can drift are how the
/// host's row and the receiver's tree stopped agreeing in the first place.
/// </para>
/// </summary>
public interface IRemotePlacementResolver
{
	/// <summary>Resolve where this item would land on THIS client's own body, or why it cannot.</summary>
	RemotePlacementResolution Resolve(CharacterItemMsg item, int requestedSlot, ulong requestedContainerInstanceId);
}
