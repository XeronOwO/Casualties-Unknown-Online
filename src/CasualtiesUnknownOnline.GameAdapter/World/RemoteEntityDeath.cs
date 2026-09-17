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

	/// <summary>
	/// Mark an entity as dying from a REMOTE event: its <c>BuildingEntity.Update</c>
	/// must not roll its own drop set (the side that owns the death already rolled
	/// and reported the drops) and only removes the entity after replaying the same
	/// destruction visuals. <paramref name="replayAnimalDeath"/> distinguishes a
	/// live remote death from a late-joiner snapshot application, and an
	/// already-marked live death is never downgraded — a resend landing in the same
	/// window would otherwise suppress the creature-specific replay.
	/// </summary>
	internal static void Mark(BuildingEntity entity, bool replayAnimalDeath)
	{
		var death = entity.gameObject.GetComponent<RemoteEntityDeath>();
		if (death == null) // Unity object — ==
		{
			death = entity.gameObject.AddComponent<RemoteEntityDeath>();
		}

		if (replayAnimalDeath)
		{
			death.ReplayAnimalDeath = true;
		}
	}
}
