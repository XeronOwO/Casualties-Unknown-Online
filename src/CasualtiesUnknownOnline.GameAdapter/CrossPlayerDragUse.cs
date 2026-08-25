using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// KrokMP-style cross-player item use by drag: when the local player releases a
/// usable inventory item over an in-world remote player's authoritative body
/// position, this routes the existing cross-player use request instead of
/// letting the native drop path run. Remote render clones deliberately have no
/// colliders, so overlap is a world-space radius around the authoritative
/// stream position rather than Physics2D.OverlapPoint.
/// </summary>
internal sealed class CrossPlayerDragUse(GameAdapterDomains domains)
{
	private const float OverlapRadius = 1.5f;

	public bool TryHandleRelease(Item? dragItem, Body? localBody)
	{
		if (dragItem == null || localBody == null) // Unity objects — ==
		{
			return false;
		}

		if (!domains.Session.SessionActive || !domains.Session.LocalInWorld)
		{
			return false;
		}

		if (!LocalUseItemEligibility.IsUseItem(dragItem))
		{
			return false;
		}

		var instanceId = dragItem.GetComponent<ItemInstanceId>();
		if (instanceId == null || instanceId.Id == 0) // Unity object — ==
		{
			return false;
		}

		var camera = Camera.main;
		if (camera == null) // Unity object — ==
		{
			return false;
		}

		var mouseWorld = camera.ScreenToWorldPoint(Input.mousePosition);
		PlayerEntity? target = null;
		var bestSquared = OverlapRadius * OverlapRadius;
		foreach (var remote in domains.Entities.RemotePlayers)
		{
			if (remote.IsLocal || !domains.Session.IsRemoteInWorld(remote.SteamId))
			{
				continue;
			}

			var dx = remote.Position.X - mouseWorld.x;
			var dy = remote.Position.Y - mouseWorld.y;
			var distanceSquared = (dx * dx) + (dy * dy);
			if (distanceSquared <= bestSquared)
			{
				bestSquared = distanceSquared;
				target = remote;
			}
		}

		if (target is null)
		{
			return false;
		}

		domains.PlayerInteraction.SendUseRequest(target.SteamId, instanceId.Id);
		domains.Log.LogInformation("[DragUse] dropped {ItemId} on {Target} (instance {Instance}).",
			dragItem.id, target.SteamId, instanceId.Id);
		return true;
	}
}
