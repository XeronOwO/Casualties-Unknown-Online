using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Marks an enemy as remote-managed: a frozen render copy on the guest. Its
/// AI/update/physics/collision scripts are skipped (the freeze patches check
/// this marker, same pattern as <see cref="RemoteBodyDriver"/> for the player
/// clones) and its position/rotation/health are driven from the host's
/// snapshot. Remote-player attacks never use the frozen copy's own collision
/// callbacks — the host's EnemyCombatDirector orders them through the
/// EnemyAttack command instead, so one attack has exactly one apply path.
/// </summary>
internal sealed class RemoteEnemyDriver : MonoBehaviour
{
}
