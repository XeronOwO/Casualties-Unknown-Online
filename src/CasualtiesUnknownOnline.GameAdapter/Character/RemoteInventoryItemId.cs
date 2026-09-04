using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The authoritative instance id carried by a remote clone inventory display
/// proxy. Remote clone items must never receive a domain <see cref="ItemInstanceId"/>
/// (the world-item lookup would confuse the presentation-only proxy with the
/// owner's real item), but the native remote-backpack UI still needs to know
/// which authoritative item a dragged proxy represents. This marker is the
/// display-domain counterpart: it is written only by the clone renderer, read
/// only by the remote-backpack release path, and intentionally invisible to the
/// item-domain lookup.
/// </summary>
internal sealed class RemoteInventoryItemId : MonoBehaviour
{
	internal ulong Id;

	/// <summary>
	/// The SteamId of the remote player whose authoritative item this display
	/// proxy represents. The take/release path needs the owner even after the
	/// remote view has closed (Tab-switch transfer into the local backpack).
	/// </summary>
	internal ulong OwnerSteamId;
}
