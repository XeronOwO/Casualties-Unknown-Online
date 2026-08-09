using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Marker on a block-destroy drop: the exact spawn position the game's
/// Utils.Create was called with (a DETERMINED value — Item.Start runs a frame
/// later, when physics may already have moved the item, and the peer must
/// materialize the drop where the breaker's Random put it, not where physics
/// bounced it to). The item-domain hook reads it to fold the drop into the
/// pending block-break report instead of reporting a standalone spawn — the
/// break and its drops get ONE arbitration verdict.
/// </summary>
internal sealed class DropOrigin : MonoBehaviour
{
	public Vector2 SpawnPosition;
}
