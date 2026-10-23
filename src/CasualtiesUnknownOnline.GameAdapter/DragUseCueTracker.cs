using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Answers the gesture's cue every frame the game is dragging something. It reads the same
/// facts the release reads — the dragged item, the family gate's verdict on it, and the
/// displayed backpack's centre (the use/wear target, user ruling 2026-10-23) — and hands the
/// plugin a decision it can draw. The cue shows only while the pointer is on that centre: a
/// release on a slot is a give, and a release anywhere else keeps the game's own drop.
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
	/// Recompute this frame's cue. Called once per while-dragging frame with the game's own dragged item, the only
	/// place it is known.
	/// </summary>
	internal void Update(Item? dragItem)
	{
		if (dragItem == null) // Unity object — ==
		{
			_cue = DragUseCue.Hidden;
			return;
		}

		// The centre is the release's own use/wear target: the cue names the displayed player only while the
		// pointer is on it, so the label and the release cannot disagree about what the release does.
		var target = RemoteBackpackView.IsOpen && DragUseCenter.IsUnderPointer(PlayerCamera.main)
			? RemoteBackpackView.FocusedSteamId
			: 0UL;
		var eligible = LocalUseItemEligibility.IsUseItem(
			dragItem, domains.LimbUseSemantics, domains.ConsumeSemantics, domains.WearSemantics, domains.SolidFoodSemantics);

		// The gesture's own release condition, read from the game's binding (never the label's own idea of
		// the input): a CUO console or window suspends PlayerCamera.HandleInput, so a release made under one
		// is never seen by HandleDragging's GetKeyUp branch and the drag item stays set with the button up.
		var interactHeld = Input.GetKey(KeyBinds.GetBind("iteminteract"));
		_cue = DragUseCuePolicy.Decide(
			domains.Session.SessionActive && domains.Session.LocalInWorld,
			true,
			interactHeld,
			eligible,
			target == 0 ? null : target);
	}

	/// <summary>Clear the cue when the drag ends, so a label cannot outlive the gesture that produced it.</summary>
	internal void Clear() => _cue = DragUseCue.Hidden;
}
