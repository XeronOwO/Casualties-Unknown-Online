using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Marks a Body as remote-managed. <see cref="simulated"/> is true on the host
/// (the clone runs full physics driven by guest input) and false on the guest
/// (render proxy — physics skipped, state written every frame).
/// </summary>
internal sealed class RemoteBodyDriver : MonoBehaviour
{
	public bool simulated;
}
