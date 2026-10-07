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

		if (!LocalUseItemEligibility.IsUseItem(dragItem, domains.LimbUseSemantics, domains.ConsumeSemantics))
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

		// The measurement follows the host chain's family order, asked through the
		// ONE family verdict (LocalUseItemEligibility.FamilyOf): only the topical
		// and drink families are measured, because they are the ones whose dose the
		// host needs, and an item the INJECTION rule claims is measured by nothing
		// here — the host refuses it by name and this client must not run its own
		// action, which for the two vanilla blood bags would draw blood into the
		// operator's bag and out of the treated limb. The limb index the host
		// receives stays -1 so the PATIENT still picks the treated limb on its own
		// body, exactly as before the migration.
		var doseMl = 0f;
		switch (LocalUseItemEligibility.FamilyOf(dragItem, domains.LimbUseSemantics, domains.ConsumeSemantics))
		{
			case LocalUseItemEligibility.Family.Topical:
				var limb = ResolveMeasureLimb(target.SteamId);
				if (limb == null // Unity object — ==
					|| !RemoteTopicalUseHandler.TryMeasure(dragItem, limb, domains.LimbUseSemantics, domains.Log, out doseMl))
				{
					// Consume the release instead of falling through to the native drop:
					// a refused remote use must not become a world drop.
					domains.Log.LogWarning("[DragUse] refused: {ItemId} could not measure a topical dose for {Target}.",
						dragItem.id, target.SteamId);
					return true;
				}

				break;
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
				// Injection and None: nothing to measure, the host answers by name.
				break;
		}

		domains.PlayerInteraction.SendUseRequest(target.SteamId, instanceId.Id, doseMl: doseMl);
		domains.Log.LogInformation("[DragUse] dropped {ItemId} on {Target} (instance {Instance}).",
			dragItem.id, target.SteamId, instanceId.Id);
		return true;
	}

	/// <summary>
	/// The limb a topical measurement runs on. The remote player's own render
	/// clone is the right one — the native delegate plays its clip at that limb,
	/// so the relayed position is next to the patient the peers can see — and its
	/// most-injured attached limb mirrors the automatic pick this gesture has
	/// always had. A clone that is not rendered yet (a joiner mid-entry) falls back
	/// to the limb the native call itself would have used, which keeps the gesture
	/// measurable instead of turning it into a silent no-op.
	/// </summary>
	private Limb? ResolveMeasureLimb(ulong targetSteamId)
	{
		if (domains.Renderer.TryGetRemoteBody(targetSteamId, out var body) && body != null) // Unity object — ==
		{
			return NativeLimbTarget.Resolve(body, -1);
		}

		return PlayerCamera.main != null ? PlayerCamera.main.selectedLimb : null; // Unity objects — ==
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
