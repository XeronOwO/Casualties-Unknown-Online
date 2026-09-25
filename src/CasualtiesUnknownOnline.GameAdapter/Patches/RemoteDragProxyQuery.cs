using CasualtiesUnknownOnline.GameAdapter.Character;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Display-proxy queries the release-window patches share: an item or container
/// component that belongs to another player's rendered inventory, and the
/// authoritative instance id its marker carries.
/// </summary>
internal static class RemoteDragProxyQuery
{
	/// <summary>True for an item rendered as another player's inventory display proxy.</summary>
	internal static bool IsProxy(Item? item) =>
		item != null && item.GetComponent<RemoteCloneRender>() != null; // Unity object — ==

	/// <summary>True for a component that belongs to a display proxy's hierarchy.</summary>
	internal static bool IsProxy(Component? component) =>
		component != null && component.GetComponent<RemoteCloneRender>() != null; // Unity object — ==

	/// <summary>The authoritative instance id the proxy carries (0 when unbound).</summary>
	internal static ulong InstanceId(Item item)
	{
		var marker = item.GetComponent<RemoteInventoryItemId>();
		return marker != null ? marker.Id : 0; // Unity object — ==
	}
}
