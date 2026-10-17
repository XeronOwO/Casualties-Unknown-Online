using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Answers the gesture's cue every frame the game is dragging something. It reads the same
/// facts the release reads — the dragged item, the family gate's verdict on it, and the
/// nearest remote player under the cursor — and hands the plugin a decision it can draw.
/// <para>
/// The reading is taken in the while-dragging frame rather than in the release, because the
/// cue has to appear and move BEFORE the operator lets go: a cue that only existed at the
/// release would be a confirmation, not a warning.
/// </para>
/// </summary>
internal sealed class DragUseCueTracker(GameAdapterDomains domains) : IDragUseCueState
{
	private DragUseCue _cue = DragUseCue.Hidden;

	public bool Showing => _cue.Showing;

	public ulong TargetSteamId => _cue.TargetSteamId;

	/// <summary>
	/// Recompute this frame's cue. Called once per while-dragging frame with the game's own
	/// dragged item, which is the only place it is known.
	/// </summary>
	internal void Update(Item? dragItem)
	{
		if (dragItem == null) // Unity object — ==
		{
			_cue = DragUseCue.Hidden;
			return;
		}

		// `IsUseItem` is the same gate the release passes, so an item that would be dropped
		// shows no cue: the cue teaches what can be handed over rather than promising a target
		// it will not use.
		var eligible = LocalUseItemEligibility.IsUseItem(
			dragItem, domains.LimbUseSemantics, domains.ConsumeSemantics, domains.WearSemantics, domains.SolidFoodSemantics);
		var target = ResolveTarget(dragItem, eligible);
		_cue = DragUseCuePolicy.Decide(
			domains.Session.SessionActive && domains.Session.LocalInWorld, true, eligible, target);
	}

	/// <summary>
	/// The player under the cursor, asked of the gesture's OWN resolution when the item is
	/// eligible. An ineligible item answers no target at all rather than a target it would
	/// not use.
	/// </summary>
	private ulong? ResolveTarget(Item dragItem, bool eligible)
	{
		if (!eligible || domains.Session.SessionActive is false)
		{
			return null;
		}

		var camera = Camera.main;
		if (camera == null) // Unity object — ==
		{
			return null;
		}

		return domains.DragUse.ResolveTargetSteamId(camera.ScreenToWorldPoint(Input.mousePosition));
	}

	/// <summary>Clear the cue when the drag ends, so a label cannot outlive the gesture that produced it.</summary>
	internal void Clear() => _cue = DragUseCue.Hidden;
}
