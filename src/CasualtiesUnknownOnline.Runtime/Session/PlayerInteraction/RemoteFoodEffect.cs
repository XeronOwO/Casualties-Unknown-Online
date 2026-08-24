namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// One solid consumable's host-authoritative cross-player use effect. The
/// values mirror the item's one-shot <c>useAction</c> body mutations from the
/// game's Item table (Item.cs); timed, random, and presentation-only branches
/// are deliberately outside this first slice. Pure data — no game assembly
/// dependency, no state.
/// </summary>
public sealed record RemoteFoodEffect(
	string ItemId,
	float ConditionCost,
	float Hunger = 0f,
	float Thirst = 0f,
	float WeightOffset = 0f,
	float Stamina = 0f,
	float Energy = 0f,
	float Happiness = 0f,
	float Temperature = 0f,
	float Sickness = 0f,
	float Caffeinated = 0f,
	float RadiationSickness = 0f,
	float BloodVolume = 0f,
	float SepticShock = 0f,
	float HearingLoss = 0f);
