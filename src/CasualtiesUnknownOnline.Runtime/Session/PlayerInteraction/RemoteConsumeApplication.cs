using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Pure application of the cross-player SOLID-food effect to a character
/// snapshot. No game assembly, no state, no I/O — the same code path is used by
/// the host authority and the L0 tests.
/// <para>
/// The drink half this type used to carry is gone with the catalog's liquid
/// table: a drink's drain is built by <see cref="LiquidDrainPlan"/> and its
/// effect is the liquid's own <c>onDrink</c> delegate, run by the affected
/// side's client (<c>NativeDrinkApply</c>), so no host-side liquid arithmetic
/// remains.
/// </para>
/// </summary>
public static class RemoteConsumeApplication
{
	/// <summary>Apply one solid food effect to a target body-health snapshot.</summary>
	public static void ApplyFood(CharacterHealthMsg health, RemoteFoodEffect effect)
	{
		if (health is null)
		{
			return;
		}

		health.Hunger += effect.Hunger;
		health.Thirst += effect.Thirst;
		health.WeightOffset += effect.WeightOffset;
		health.Stamina += effect.Stamina;
		health.Energy += effect.Energy;
		health.Happiness += effect.Happiness;
		health.Temperature += effect.Temperature;
		health.SicknessAmount += effect.Sickness;
		health.Caffeinated += effect.Caffeinated;
		health.RadiationSickness += effect.RadiationSickness;
		health.BloodVolume += effect.BloodVolume;
		health.SepticShock += effect.SepticShock;
		health.HearingLoss += effect.HearingLoss;
	}
}
