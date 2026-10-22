using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The drag gesture's patch port: the one call a static Harmony patch needs to keep the
/// gesture's cue current. The aggregate does not declare it, so
/// <see cref="PatchBridge.DragUseCue"/> is the only way the patch layer reaches the cue —
/// a patch states which domain it depends on and cannot reach the rest through that
/// reference.
/// <para>
/// It reports the game's own dragged item and the frame's UI raycasts rather than deciding
/// anything: a null item means the drag has ended, which is what clears the cue, and the
/// raycasts are what the cue's give check reads — the SAME resolution the release uses, so
/// the label cannot promise a use where the release would give.
/// </para>
/// </summary>
internal interface IDragUseCuePatchPort
{
	/// <summary>Recompute the gesture's cue from the frame's dragged item and UI hits; a null item clears it.</summary>
	void UpdateDragUseCue(Item? dragItem, List<RaycastResult>? uiCasts);
}
