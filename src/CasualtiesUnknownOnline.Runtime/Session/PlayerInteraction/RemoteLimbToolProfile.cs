namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The host-authoritative effect of one non-liquid limb tool when it is used on
/// another player. The values mirror the immediate parts of the game's
/// <c>ItemInfo.useLimbAction</c> delegates (Item.cs SetupItems) plus the
/// component-bearing subset (splint/tourniquet/icepack), the shrapnel-removal
/// minigame tool (tweezers) and the timed tool (medicalsuture). Pure data — no
/// game assembly dependency, no state.
/// </summary>
public sealed record RemoteLimbToolProfile(
	string ItemId,
	float ConditionCost,
	float SkinHealth = 0f,
	float MuscleHealth = 0f,
	float Pain = 0f,
	float BleedAmount = 0f,
	float BoneHealTimer = 0f,
	float DislocationTimer = 0f,
	float SkinHealAmount = 0f,
	float BandageSlowAmount = 0f,
	float BoneHealTimerMultiplier = 1f,
	float BleedAmountMultiplier = 1f,
	int RequiredLimbIndex = -1,
	float BloodViscosity = 0f,
	float Hemothorax = 0f,
	float Temperature = 0f,
	RemoteLimbComponentKind ComponentKind = RemoteLimbComponentKind.None,
	float ComponentConditionLossMinute = 0f,
	float ComponentTimeLeft = 0f,
	float ComponentMaxTime = 0f,
	bool DestroyAtZero = true,
	bool RequiresShrapnel = false,
	float TimedBleedPerSecond = 0f,
	float TimedBleedDurationSeconds = 0f);
