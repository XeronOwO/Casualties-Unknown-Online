using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The owner-side <see cref="Painkillers"/> component state that Mapster cannot
/// map because it lives on a component, not on <see cref="Body"/>. The
/// component drives limb pain reduction, opiate happiness, withdrawal and
/// overdose presentation, so it must be captured into the character snapshot
/// for cross-player opiate use and reconnect restore, and applied back when a
/// host-authoritative health result or restore reaches the local simulated
/// body. Pure static helper — no state.
/// </summary>
internal static class PainkillersSync
{
	/// <summary>Captures the owner-side painkiller component fields into the health wire message.</summary>
	internal static void Capture(Body body, CharacterHealthMsg health)
	{
		var painkillers = body.GetComponent<Painkillers>();
		if (painkillers == null) // Unity object — ==
		{
			return;
		}

		health.OpiateAmount = painkillers.opiateAmount;
		health.OpiateTolerance = painkillers.opiateTolerance;
		health.OpiateReception = painkillers.opiateReception;
		health.AntagonistAmount = painkillers.antagonistAmount;
		health.ActualOpiateReception = painkillers.actualOpiateReception;
	}

	/// <summary>
	/// Applies host-authoritative painkiller component state to the LOCAL body, in BOTH
	/// directions: the component is created or updated when the owner carries state, and
	/// REMOVED when the owner does not. The game's own <see cref="Painkillers.Update"/> then
	/// evolves tolerance/reception and self-destroys when the opiate clears — so removing a
	/// copy the owner no longer has reaches the state the sender is already in, where the
	/// old code left the receiver's stale tolerance standing. One residual the removal does not
	/// reproduce: the sender zeroes <c>body.opiateHappiness</c> on its way out, and the receiver
	/// carries the sender's happiness on the wire anyway, so that additive term clears on its own.
	/// </summary>
	internal static void Apply(Body body, CharacterHealthMsg? health)
	{
		if (health is null)
		{
			return;
		}

		var presence = MedicationComponentPolicy.ForPainkillers(
			health.OpiateAmount,
			health.OpiateTolerance,
			health.OpiateReception,
			health.AntagonistAmount,
			health.ActualOpiateReception);

		var painkillers = body.GetComponent<Painkillers>();
		if (presence == MedicationComponentPresence.Absent)
		{
			if (painkillers != null) // Unity object — ==
			{
				Object.Destroy(painkillers);
			}

			return;
		}

		if (painkillers == null) // Unity object — ==
		{
			painkillers = body.gameObject.AddComponent<Painkillers>();
		}

		painkillers.opiateAmount = health.OpiateAmount;
		painkillers.opiateTolerance = health.OpiateTolerance;
		painkillers.opiateReception = health.OpiateReception;
		painkillers.antagonistAmount = health.AntagonistAmount;
		painkillers.actualOpiateReception = health.ActualOpiateReception;
	}
}
