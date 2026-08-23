using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Marks a BuildingEntity that died from a REMOTE hit (the peer's damage
/// stream applied the last points here). The attacker's side already rolled
/// and reported the drops (local compute — the entity's health is written
/// locally on every side, so both sides reach zero; only the attacker rolls),
/// so this side's BuildingEntity.Update must not roll again — it only removes
/// the entity. Added by the remote-damage application in GameAdapter, read by
/// BuildingEntityUpdatePatch.
/// <see cref="ReplayAnimalDeath"/> distinguishes a live remote death (set by
/// the live damage/open relay) from a late-joiner snapshot application, so the
/// creature-specific death effects are replayed only for deaths the peer
/// actually observed arriving, not for pre-existing dead entities materialized
/// from a world-entry snapshot.
/// </summary>
public sealed class RemoteEntityDeath : MonoBehaviour
{
	/// <summary>True when this marker came from a live remote damage/open relay
	/// and the receiver should replay the animal-specific death presentation.</summary>
	public bool ReplayAnimalDeath { get; set; }
}
