using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Marker on an entity a side created as a REPLAY of the spawn channel (the
/// EntitySpawned relay or the late-joiner runtime-enemy materialization) — its
/// own Start must not report it as a new local creation. The RemoteApply scope
/// is synchronous, but Start runs later, so the scope check alone cannot see
/// the create; the marker is the cross-frame fact.
/// </summary>
internal sealed class SpawnReplayMarker : MonoBehaviour
{
}
