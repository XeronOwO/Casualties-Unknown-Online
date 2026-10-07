using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Marks a local item object as a STANDING ITEM OBJECT — the incarnation of an item the
/// authoritative data carries as a member's CARRIED row, on a client that does not hold that item
/// itself. It is the third item category beside a world item (whose position and physics the host
/// simulates and our copy follows) and this client's own carried item (parented under its own body,
/// its own fact): the object runs its whole lifecycle and is written by the data, but it is never a
/// source of truth and never touches the world.
/// <para>
/// The marker records what the object was CREATED as. The category's decision is not this marker —
/// <see cref="StandingItems.Is"/> asks the item data as well, so an object whose id the data has
/// since moved into the world stops being a standing object, and an unmarked object (every family
/// in play today, a local drop whose report the host has not accepted yet included) never becomes
/// one. The proxy lesson is why: <c>CloneInventoryRenderer</c> retires a stale display proxy by
/// DEACTIVATING it before its deferred destroy, and a scene marker that is invisible to a default
/// component lookup misclassified exactly the object that was one frame from death (batch
/// <c>20261006-h</c>). A table lookup answers for an id whatever the scene currently holds.
/// </para>
/// <para>
/// This marker is NOT the display-proxy marker (<c>RemoteCloneRender</c> /
/// <c>RemoteInventoryItemId</c>): a display proxy is presentation owned by the clone renderer, this
/// object is the data's local incarnation, and the two must never be mixed — the classification
/// sites read the two separately on purpose.
/// </para>
/// </summary>
internal sealed class StandingItemObject : MonoBehaviour
{
}
