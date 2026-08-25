namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The host-authoritative healing effect of one carried medical item when it is
/// used on another player. The values mirror the limb-usable medical items'
/// one-shot effects (Item.cs SetupItems): the item consumes condition and
/// changes the target limb's healing/pain/timer state. Body-level component
/// effects (currently the <c>Painkillers</c> opiate state added by
/// analgesicgauze) are applied separately to the health snapshot. Pure data —
/// no game assembly dependency, no state.
/// </summary>
public sealed record RemoteHealProfile(
	string ItemId,
	float ConditionCost,
	float SkinHealAmount = 0f,
	float BandageSlowAmount = 0f,
	float Pain = 0f,
	float BoneHealTimer = 0f,
	float DislocationTimer = 0f,
	float DisinfectionTime = 0f,
	float BleedAmount = 0f,
	float SkinHealth = 0f,
	float MuscleHealth = 0f,
	float OpiateAmount = 0f);
