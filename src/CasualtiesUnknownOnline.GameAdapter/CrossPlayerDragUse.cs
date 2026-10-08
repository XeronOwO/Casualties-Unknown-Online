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

		if (!LocalUseItemEligibility.IsUseItem(dragItem, domains.LimbUseSemantics, domains.ConsumeSemantics, domains.WearSemantics, domains.SolidFoodSemantics))
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
		var remotePlayers = domains.Entities.RemotePlayers;
		for (var i = 0; i < remotePlayers.Count; i++)
		{
			var remote = remotePlayers[i];
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

		// The measurement follows the ONE family verdict (LocalUseItemEligibility.FamilyOf):
		// the DRINK family is the one measured here, because it is the only family this
		// gesture runs whose dose the host needs. An item the INJECTION rule claims is
		// measured by nothing — the host refuses it by name and this client must not run
		// its own action, which for the two vanilla blood bags would draw blood into the
		// operator's bag and out of the treated limb — and a WEARABLE lands in the default
		// arm for the same reason: it has no dose, and running its own use action here
		// would take the item out of the operator's hands for a gesture the host may still
		// refuse. The TOPICAL family does not reach this method at all: its action is the
		// wound view's, so `IsUseItem` refuses the release here and the native drop runs,
		// instead of a dose landing on a limb the operator never picked (decision 246).
		var doseMl = 0f;
		switch (LocalUseItemEligibility.FamilyOf(dragItem, domains.LimbUseSemantics, domains.ConsumeSemantics, domains.SolidFoodSemantics))
		{
			case LocalUseItemEligibility.Family.Drink:
				var drinker = ResolveDrinkBody(target.SteamId);
				if (drinker == null // Unity object — ==
					|| !RemoteDrinkUseHandler.TryMeasure(dragItem, drinker, domains.ConsumeSemantics, domains.Log, out doseMl))
				{
					domains.Log.LogWarning("[DragUse] refused: {ItemId} could not measure a drink dose for {Target}.",
						dragItem.id, target.SteamId);
					return true;
				}

				break;
			default:
				// Injection, solid food and None: nothing to measure, the host answers by
				// name. Solid food is the one family whose WHOLE action runs on the
				// affected side — measuring it here would feed the operator, take the
				// item out of their hands and play its sounds at the wrong body.
				break;
		}

		domains.PlayerInteraction.SendUseRequest(target.SteamId, instanceId.Id, doseMl: doseMl);
		domains.Log.LogInformation("[DragUse] dropped {ItemId} on {Target} (instance {Instance}).",
			dragItem.id, target.SteamId, instanceId.Id);
		return true;
	}

	/// <summary>
	/// The body a drink measurement runs against: the affected player's own render
	/// clone. Unlike the topical limb, this body is SEMANTIC — an item's use action
	/// may read it (mindwipe's item-level health gate is the vanilla instance) and
	/// only that player's own picture may answer it, so a clone that is not
	/// rendered yet refuses the gesture instead of answering the patient's facts
	/// from this client's own body.
	/// </summary>
	private Body? ResolveDrinkBody(ulong targetSteamId) =>
		domains.Renderer.TryGetRemoteBody(targetSteamId, out var body) && body != null // Unity object — ==
			? body
			: null;
}
