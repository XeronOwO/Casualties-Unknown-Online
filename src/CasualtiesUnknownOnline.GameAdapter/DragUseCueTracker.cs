using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Answers the gesture's cue every frame the game is dragging something. It reads the same
/// facts the release reads — the dragged item, the family gate's verdict on it, the
/// gesture's ONE target (the last-entered detection range, <see cref="CrossPlayerDragUse"/>),
/// and the SAME displayed-backpack resolution the release uses — and hands the plugin a
/// decision it can draw. The cue shows only while the release would be a USE: the cursor is
/// inside the target's range and not on the displayed backpack (where the release is a give).
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
	/// dragged item and UI hits, which are the only places they are known.
	/// </summary>
	internal void Update(Item? dragItem, List<RaycastResult>? uiCasts)
	{
		var target = domains.DragUse.Observe(dragItem);
		if (dragItem == null) // Unity object — ==
		{
			_cue = DragUseCue.Hidden;
			return;
		}

		// `IsUseItem` is the same gate the release passes, so an item that would be dropped
		// shows no cue: the cue teaches what can be handed over rather than promising a target
		// it will not use. The displayed backpack is the ONE give resolution the release uses,
		// so a release that would give shows no USE label.
		var eligible = LocalUseItemEligibility.IsUseItem(
			dragItem, domains.LimbUseSemantics, domains.ConsumeSemantics, domains.WearSemantics, domains.SolidFoodSemantics);
		var onDisplayedBackpack = eligible
			&& DragGiveTargetResolver.Resolve(dragItem, PlayerCamera.main, uiCasts) is not null;

		// The gesture's own release condition, read from the game's binding (never the label's own idea of
		// the input): a CUO console or window suspends PlayerCamera.HandleInput, so a release made under one
		// is never seen by HandleDragging's GetKeyUp branch and the drag item stays set with the button up.
		var interactHeld = Input.GetKey(KeyBinds.GetBind("iteminteract"));
		_cue = DragUseCuePolicy.Decide(
			domains.Session.SessionActive && domains.Session.LocalInWorld,
			true,
			interactHeld,
			eligible,
			!onDisplayedBackpack && eligible && domains.DragUse.IsTargetInRange ? target : null);
	}

	/// <summary>Clear the cue when the drag ends, so a label cannot outlive the gesture that produced it.</summary>
	internal void Clear() => _cue = DragUseCue.Hidden;
}
