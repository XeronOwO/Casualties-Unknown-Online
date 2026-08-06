using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Marks a Body as remote-managed: a frozen render proxy. All its physics and
/// game logic (FixedUpdate/Update) are skipped; only the session's reported
/// state is written onto it each frame.
/// </summary>
internal sealed class RemoteBodyDriver : MonoBehaviour
{
}
