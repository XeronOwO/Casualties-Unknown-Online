using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

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
	/// Applies host-authoritative painkiller component state to the LOCAL body.
	/// The component is created only when a non-zero state arrives; the game's
	/// own <see cref="Painkillers.Update"/> then evolves tolerance/reception and
	/// self-destroys when the opiate clears.
	/// </summary>
	internal static void Apply(Body body, CharacterHealthMsg? health)
	{
		if (health is null)
		{
			return;
		}

		var hasState = health.OpiateAmount != 0f
			|| health.OpiateTolerance != 0f
			|| health.OpiateReception != 0f
			|| health.AntagonistAmount != 0f
			|| health.ActualOpiateReception != 0f;
		if (!hasState)
		{
			return;
		}

		var painkillers = body.GetComponent<Painkillers>();
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
