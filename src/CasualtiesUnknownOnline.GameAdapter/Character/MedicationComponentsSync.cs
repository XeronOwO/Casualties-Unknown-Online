using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

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
	/// Applies host-authoritative medication component state to the LOCAL body.
	/// Components are created only when non-zero/non-empty state arrives; the
	/// game's own Update methods then evolve/self-destroy them.
	/// </summary>
	internal static void Apply(Body body, CharacterHealthMsg? health)
	{
		if (health is null)
		{
			return;
		}

		if (health.SleepingPillsAmount != 0f)
		{
			var sleeping = body.GetComponent<SleepingPills>();
			if (sleeping == null) // Unity object — ==
			{
				sleeping = body.gameObject.AddComponent<SleepingPills>();
			}

			sleeping.amount = health.SleepingPillsAmount;
		}

		if (health.AntidepressantsAmount != 0f || health.AntidepressantsCurrentAmount != 0f)
		{
			var antidepressants = body.GetComponent<Antidepressants>();
			if (antidepressants == null) // Unity object — ==
			{
				antidepressants = body.gameObject.AddComponent<Antidepressants>();
			}

			antidepressants.amount = health.AntidepressantsAmount;
			antidepressants.currentAmount = health.AntidepressantsCurrentAmount;
		}

		if (health.MindwipeScriptPresent)
		{
			var mindwipe = body.GetComponent<MindwipeScript>();
			if (mindwipe == null) // Unity object — ==
			{
				mindwipe = body.gameObject.AddComponent<MindwipeScript>();
			}

			mindwipe.active = health.MindwipeScriptActive;
		}
	}
}
