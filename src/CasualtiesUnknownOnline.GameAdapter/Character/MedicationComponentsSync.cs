using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The owner-side <see cref="SleepingPills"/>, <see cref="Antidepressants"/>
/// and <see cref="MindwipeScript"/> component state that Mapster cannot map
/// because those fields live on components, not on <see cref="Body"/>. The
/// components drive sleep/overdose, antidepressant happiness and the mindwipe
/// reset, so they must be captured into the character snapshot for the
/// cross-player drinkable-medicine slice and reconnect restore, and applied
/// back when a host-authoritative health result or restore reaches the local
/// simulated body. Pure static helper — no state.
/// </summary>
internal static class MedicationComponentsSync
{
	/// <summary>Captures the owner-side medication component fields into the health wire message.</summary>
	internal static void Capture(Body body, CharacterHealthMsg health)
	{
		var sleeping = body.GetComponent<SleepingPills>();
		if (sleeping != null) // Unity object — ==
		{
			health.SleepingPillsAmount = sleeping.amount;
		}

		var antidepressants = body.GetComponent<Antidepressants>();
		if (antidepressants != null) // Unity object — ==
		{
			health.AntidepressantsAmount = antidepressants.amount;
			health.AntidepressantsCurrentAmount = antidepressants.currentAmount;
		}

		var mindwipe = body.GetComponent<MindwipeScript>();
		if (mindwipe != null) // Unity object — ==
		{
			health.MindwipeScriptPresent = true;
			health.MindwipeScriptActive = mindwipe.active;
		}
	}

	/// <summary>
	/// Applies host-authoritative medication component state to the LOCAL body, in BOTH
	/// directions: a component is created or updated when the owner carries state, and
	/// REMOVED when the owner does not. Removing matters for all three — a dose that decayed
	/// away on the owner used to leave the receiver's copy standing, and a mindwipe the owner
	/// cleared could not be cleared here at all, which is what the heal's hollow half needs.
	/// </summary>
	internal static void Apply(Body body, CharacterHealthMsg? health)
	{
		if (health is null)
		{
			return;
		}

		var sleeping = body.GetComponent<SleepingPills>();
		if (MedicationComponentPolicy.ForSleepingPills(health.SleepingPillsAmount) == MedicationComponentPresence.Absent)
		{
			if (sleeping != null) // Unity object — ==
			{
				Object.Destroy(sleeping);
			}
		}
		else
		{
			if (sleeping == null) // Unity object — ==
			{
				sleeping = body.gameObject.AddComponent<SleepingPills>();
			}

			sleeping.amount = health.SleepingPillsAmount;
		}

		var antidepressants = body.GetComponent<Antidepressants>();
		if (MedicationComponentPolicy.ForAntidepressants(health.AntidepressantsAmount, health.AntidepressantsCurrentAmount) == MedicationComponentPresence.Absent)
		{
			if (antidepressants != null) // Unity object — ==
			{
				Object.Destroy(antidepressants);
			}
		}
		else
		{
			if (antidepressants == null) // Unity object — ==
			{
				antidepressants = body.gameObject.AddComponent<Antidepressants>();
			}

			antidepressants.amount = health.AntidepressantsAmount;
			antidepressants.currentAmount = health.AntidepressantsCurrentAmount;
		}

		var mindwipe = body.GetComponent<MindwipeScript>();
		switch (MedicationComponentPolicy.ForMindwipe(health.MindwipeScriptPresent, health.MindwipeScriptActive))
		{
			case MedicationComponentPresence.Absent:
				if (mindwipe != null) // Unity object — ==
				{
					Object.Destroy(mindwipe);
				}

				// The body CACHES this component in `mindWipe` and refreshes that handle on its own
				// half-second pass, so destroying the component is not enough by itself: until the next
				// pass the body still multiplies effective happiness by zero. The display projection
				// already clears the handle by hand for the same reason.
				body.mindWipe = null;
				break;
			case MedicationComponentPresence.Inactive:
			case MedicationComponentPresence.Active:
				if (mindwipe == null) // Unity object — ==
				{
					mindwipe = body.gameObject.AddComponent<MindwipeScript>();
				}

				mindwipe.active = health.MindwipeScriptActive;
				break;
		}
	}
}
