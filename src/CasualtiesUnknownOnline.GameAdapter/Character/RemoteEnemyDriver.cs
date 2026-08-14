using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Marks an enemy as remote-managed: a frozen render copy on the guest. Its
/// AI/update scripts are skipped (the freeze patches check this marker, same
/// pattern as <see cref="RemoteBodyDriver"/> for the player clones) and its
/// position/rotation/health are driven from the host's snapshot each frame.
/// </summary>
internal sealed class RemoteEnemyDriver : MonoBehaviour
{
}
