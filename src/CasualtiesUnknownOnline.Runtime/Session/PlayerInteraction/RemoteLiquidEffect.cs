namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// One drinkable liquid's host-authoritative cross-player effect, expressed per
/// 100 ml (the game's WaterContainerItem.Drink emits 100 ml per use). The
/// values mirror the <c>LiquidType.onDrink</c> delegates in Liquids.cs for the
/// curated first slice. Timed/random/presentation-only branches are excluded.
/// Pure data — no game assembly dependency, no state.
/// </summary>
public sealed record RemoteLiquidEffect(
	string LiquidId,
	float ThirstPer100Ml = 0f,
	float HungerPer100Ml = 0f,
	float WeightPer100Ml = 0f,
	float StaminaPer100Ml = 0f,
	float EnergyPer100Ml = 0f,
	float HappinessPer100Ml = 0f,
	float TemperaturePer100Ml = 0f,
	float SicknessPer100Ml = 0f,
	float CaffeinatedPer100Ml = 0f,
	float BloodVolumePer100Ml = 0f,
	float RadiationSicknessPer100Ml = 0f);
