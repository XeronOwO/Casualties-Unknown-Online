using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Marks a BuildingEntity that died from a REMOTE hit (the peer's damage
/// stream applied the last points here). The attacker's side already rolled
/// and reported the drops (local compute — the entity's health is written
/// locally on every side, so both sides reach zero; only the attacker rolls),
/// so this side's BuildingEntity.Update must not roll again — it only removes
/// the entity. Added by the remote-damage application in GameAdapter, read by
/// BuildingEntityUpdatePatch.
/// </summary>
public sealed class RemoteEntityDeath : MonoBehaviour
{
}
