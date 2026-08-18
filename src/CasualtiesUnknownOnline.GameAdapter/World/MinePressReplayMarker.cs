using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Marks that the 0.8 s mine-press visual was already replayed on this side.
/// The remote replay deliberately does NOT write the game's private `pressed`
/// latch (that would make the local MineScript.Update count down and explode
/// the mine naturally, double-applying the world effects); the marker is the
/// duplicate guard for the transient press event and is destroyed with the
/// mine when the MineExploded replay consumes it.
/// </summary>
internal sealed class MinePressReplayMarker : MonoBehaviour
{
}
