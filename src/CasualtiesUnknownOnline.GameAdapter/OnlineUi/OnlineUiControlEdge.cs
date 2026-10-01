using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// Marks the hairline CUO draws inside a control's own edge (ticket online-ui-layout-and-input-detail-pass and
/// the user's "one box per control" call of 2026-10-01). It exists so the pass that hides the game's prefab box
/// art can tell CUO's own edge from it: the prefab's boxes are hidden, this one is not.
/// </summary>
internal sealed class OnlineUiControlEdge : MonoBehaviour
{
}
