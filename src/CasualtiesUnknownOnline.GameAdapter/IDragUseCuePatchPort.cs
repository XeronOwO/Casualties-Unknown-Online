namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The drag gesture's patch port: the one call a static Harmony patch needs to keep the
/// gesture's cue current. The aggregate does not declare it, so
/// <see cref="PatchBridge.DragUseCue"/> is the only way the patch layer reaches the cue —
/// a patch states which domain it depends on and cannot reach the rest through that
/// reference.
/// <para>
/// It reports the game's own dragged item and decides nothing: a null item means the drag has ended, which is what
/// clears the cue. The cue's use target is the displayed backpack's centre, read from the game's live ring geometry
/// by the tracker (user ruling 2026-10-23), so the label can never promise a use where the release would give.
/// </para>
/// </summary>
internal interface IDragUseCuePatchPort
{
	/// <summary>Recompute the gesture's cue from the frame's dragged item; a null item clears it.</summary>
	void UpdateDragUseCue(Item? dragItem);
}
