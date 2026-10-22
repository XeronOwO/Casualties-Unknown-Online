using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// KrokMP-style cross-player item use by drag: when the local player releases a
/// usable inventory item over an in-world remote player, this routes the existing
/// cross-player use request instead of letting the native drop path run. Remote
/// render clones deliberately have no colliders, so overlap is a world-space radius
/// around the authoritative stream position rather than Physics2D.OverlapPoint.
///
/// <para>
/// The target is the user's 2026-10-22 rule (<see cref="DragTargetSelection"/>): the player whose detection
/// range the cursor entered last and still occupies. The same selection also opens that player's backpack
/// for the drag, which is the give gesture's entry: entering a range opens the backpack, releasing inside
/// the range uses the item on the player, and moving onto the displayed backpack gives it to them. One
/// resolution feeds the release, the cue and the open, so the three cannot disagree about the target.
/// </para>
/// </summary>
internal sealed class CrossPlayerDragUse(GameAdapterDomains domains)
{
	private const float OverlapRadius = 1.5f;

	private readonly DragTargetSelection _selection = new();

	private bool _dragActive;
	private ulong _openedFor;

	/// <summary>Whether the cursor is inside the current target's detection range — the condition for a USE.</summary>
	internal bool IsTargetInRange => _selection.IsTargetInRange;

	/// <summary>
	/// The single per-frame read of the gesture: starts a drag, ends one, and returns this frame's target.
	/// Both the while-dragging cue and the automatic backpack open call it; the second call in one frame sees
	/// the same ranges and returns the same target.
	/// </summary>
	internal ulong? Observe(Item? dragItem)
	{
		if (dragItem == null) // Unity object — ==
		{
			EndDrag();
			return null;
		}

		if (!_dragActive)
		{
			_dragActive = true;
			_openedFor = 0;
			_selection.Begin();
		}

		var camera = Camera.main;
		if (camera == null) // Unity object — ==
		{
			return _selection.Target;
		}

		var mouseWorld = camera.ScreenToWorldPoint(Input.mousePosition);
		_selection.BeginFrame(mouseWorld.x, mouseWorld.y, OverlapRadius);
		var remotes = domains.Entities.RemotePlayers;
		for (var i = 0; i < remotes.Count; i++)
		{
			var remote = remotes[i];
			if (remote.IsLocal || !domains.Session.IsRemoteInWorld(remote.SteamId))
			{
				continue;
			}

			_selection.Consider(remote.SteamId, remote.Position.X, remote.Position.Y);
		}

		_selection.EndFrame();
		return _selection.Target;
	}

	/// <summary>
	/// The give gesture's entry: a range entered by a LOCAL item's drag opens that player's backpack, so the
	/// player never has to know a panel hotkey to give something away. A proxy drag (the take direction)
	/// opens nothing, and a target already displayed is not re-opened. The open applies the same access rule
	/// and line-of-sight check every other entry uses.
	/// </summary>
	internal void AutoOpenFor(Item? dragItem)
	{
		if (dragItem == null) // Unity object — ==
		{
			EndDrag();
			return;
		}

		if (dragItem.GetComponent<RemoteCloneRender>() != null) // Unity object — ==
		{
			return;
		}

		if (!domains.Session.SessionActive || !domains.Session.LocalInWorld)
		{
			return;
		}

		if (Observe(dragItem) is not { } target || target == _openedFor)
		{
			return;
		}

		if (RemoteBackpackView.IsOpen && RemoteBackpackView.FocusedSteamId == target)
		{
			_openedFor = target;
			return;
		}

		// The render clone is the one failure that resolves itself a frame or two later (the remote body
		// streams in), so it must not mark the target as attempted: the retry is silent and cheap. Every
		// other refusal (the access rule, line of sight) is answered by the coordinator once and is not
		// retried for this target — a per-frame refusal line would be noise, not information.
		if (!domains.Renderer.TryGetRemoteBody(target, out var body) || body == null) // Unity object — ==
		{
			domains.Log.LogDebug("[BackpackView] the give gesture's target {SteamId} has no render clone yet; the open waits for it.", target);
			return;
		}

		_openedFor = target;
		var displayName = domains.Session.TryGetMember(target, out var member) ? member.DisplayName : "";
		if (!domains.RemoteBackpack.Open(target, displayName))
		{
			return;
		}

		RemoteBackpackView.MarkAutoOpened();
		if (PlayerCamera.main is { } camera) // Unity object — ==
		{
			// The view opened mid-drag, so the native ring-activation path that would otherwise anchor the
			// radial and rebuild the wear strip does not run: do both here, or the first while-dragging frame
			// can close the far-away ring against the 600-px rule and the wear strip keeps showing THIS
			// player's worn items on somebody else's backpack.
			if (RemoteBackpackView.FocusedBody is { } focused)
			{
				RemoteBackpackView.AnchorRadialToFocus(camera, focused);
			}

			camera.UpdateWearables();
		}

		domains.Log.LogInformation(
			"[BackpackView] opened {SteamId}'s backpack because the dragged item entered its detection range (the give gesture's entry).",
			target);
	}

	/// <summary>Ends the drag's selection and the open state that belongs to it. Idempotent.</summary>
	private void EndDrag()
	{
		if (!_dragActive)
		{
			return;
		}

		_dragActive = false;
		_openedFor = 0;
		_selection.Begin();

		// The drag's own view outlives the drag after a give (the item's landing is what the giver is looking
		// at); its "opened by the drag" flag must not, or the NEXT drag's release would close it as its own.
		RemoteBackpackView.ClearAutoOpened();
	}

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

		if (Camera.main == null) // Unity object — ==
		{
			return false;
		}

		// The release point counts: a release on a player the cursor has just reached is aimed at THAT player.
		if (Observe(dragItem) is not { } targetSteamId || !IsTargetInRange)
		{
			return false;
		}

		// The measurement follows the ONE family verdict for THIS gesture's entry
		// (LocalUseItemEligibility.FamilyOf with ItemUseEntry.Inventory): the DRINK
		// family is the one measured here, because it is the only family this gesture
		// runs whose dose the host needs. An item the INJECTION rule claims is measured
		// by nothing — the host refuses it by name and this client must not run its own
		// action, which for the two vanilla blood bags would draw blood into the
		// operator's bag and out of the treated limb — and a WEARABLE lands in the
		// default arm for the same reason: it has no dose, and running its own use
		// action here would take the item out of the operator's hands for a gesture the
		// host may still refuse. The TOPICAL family is not asked on this entry at all
		// (decision 246), so a container whose data also admits a limb application is
		// measured as the drink its own `useAction` is — the wound view keeps its
		// topical half, and no dose lands on a limb this gesture never picked.
		var doseMl = 0f;
		switch (LocalUseItemEligibility.FamilyOf(dragItem, ItemUseEntry.Inventory, domains.LimbUseSemantics, domains.ConsumeSemantics, domains.WearSemantics, domains.SolidFoodSemantics))
		{
			case ItemUseFamily.Drink:
				var drinker = ResolveDrinkBody(targetSteamId);
				if (drinker == null // Unity object — ==
					|| !RemoteDrinkUseHandler.TryMeasure(dragItem, drinker, domains.ConsumeSemantics, domains.Log, out doseMl))
				{
					domains.Log.LogWarning("[DragUse] refused: {ItemId} could not measure a drink dose for {Target}.",
						dragItem.id, targetSteamId);
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

		domains.PlayerInteraction.SendUseRequest(targetSteamId, instanceId.Id, doseMl: doseMl);
		domains.Log.LogInformation("[DragUse] dropped {ItemId} on {Target} (instance {Instance}).",
			dragItem.id, targetSteamId, instanceId.Id);

		// The use ends the gesture's backpack (user's 2026-10-22 ruling): the release inside the range is a
		// USE, not a look at the player's inventory.
		if (RemoteBackpackView.IsOpen && RemoteBackpackView.FocusedSteamId == targetSteamId)
		{
			RemoteBackpackView.Close();
		}

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
