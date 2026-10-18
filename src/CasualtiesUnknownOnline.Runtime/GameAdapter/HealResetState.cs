using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The FULL body reset the console cheat verb <c>/heal</c> applies to one player: the healthy
/// value of every field the game's own <c>heal</c> command writes, the extra fields KrokMP's
/// <c>ResetHealth</c> adds, and the three fields the game's own revive baseline clears so the
/// revived body can stand up.
///
/// <para>
/// The table is DATA, not a game call: the Game Adapter owns the live <see cref="Body"/> and
/// applies these values through <see cref="ApplyTo"/> plus the structural effects (severed-limb
/// restore, hollow clear, component removal) its own code owns. Keeping the values here, in a
/// game-free assembly, is what makes the field list testable: a missing or wrong field fails a
/// unit test instead of only showing up on a two-client run.
/// </para>
///
/// <para>
/// Field-by-field sources: <c>reversing/Assembly-CSharp/Assembly-CSharp/ConsoleScript.cs</c> (the
/// native <c>heal</c> command, the limb fields plus the body fields through
/// <c>bloodPressureChangeFromMedicine</c>/<c>caffeinated</c>/<c>clawRegrowTime</c> and the three
/// medication components it destroys), <c>reversing/KrokMP/.../Util_BodyExtensions.cs</c>
/// (<c>ResetHealth</c>: <c>furBloodAmount</c>, <c>bothEyesGone</c>, <c>reversedControls</c>,
/// <c>splint</c>/<c>tourniquet</c> removal, <c>MindwipeScript</c> removal), and
/// <c>src/CasualtiesUnknownOnline.Runtime/Session/World/TraderRecruitPolicy.cs</c>
/// (<c>PrepareRevive</c>: <c>Shock</c>/<c>PainShock</c>/<c>FibrillationForced</c> at zero, which is
/// what lets the native <c>Body.Stand</c> lift a revived body that is still lying down).
/// </para>
///
/// <para>
/// It deliberately does NOT touch the fields the two reference paths leave alone — movement
/// parameters, presentation latches, mood histories, skills and inventory. The Game Adapter applies
/// this table onto a CAPTURE of the live body, so every field named here is overwritten and every
/// field not named here keeps its live value.
/// </para>
/// </summary>
public static class HealResetState
{
	// ---- Body: the native heal's field list (ConsoleScript.cs) ----

	public const float BrainHealth = 100f;

	public const float BloodVolume = 100f;

	public const float BloodOxygen = 100f;

	public const float BloodPressure = 120f;

	public const float HeartRate = 70f;

	public const float BloodVesselSize = 1f;

	public const float BloodViscosity = 0f;

	public const float RespiratoryRate = 100f;

	public const float StrokeAmount = 0f;

	public const bool HasPulmonaryEmbolism = false;

	public const float FibrillationProgress = 0f;

	public const float Hunger = 100f;

	public const float Thirst = 100f;

	public const float SepticShock = 0f;

	public const float Temperature = 37f;

	public const float SicknessAmount = 0f;

	public const float Consciousness = 100f;

	public const float Stamina = 100f;

	public const float Energy = 100f;

	public const float Happiness = 0f;

	public const float WeightOffset = 0f;

	public const float RadiationSickness = 0f;

	public const bool Disfigured = false;

	public const bool EyeGone = false;

	public const float InternalBleeding = 0f;

	public const float Hemothorax = 0f;

	public const float TraumaAmount = 0f;

	public const float Dirtyness = 0f;

	public const float Wetness = 0f;

	public const float BadSleepAmount = 0f;

	public const float HearingLoss = 0f;

	/// <summary>A <see cref="Body"/> field, not a component field: the native heal zeroes it and destroys the component that feeds it.</summary>
	public const float AntidepressantHappiness = 0f;

	/// <summary>The same body field for the opiate term.</summary>
	public const float OpiateHappiness = 0f;

	public const float AntibioticImmunityTime = 0f;

	public const float BrainGrowSickness = 0f;

	public const bool TriedRollingLastStand = false;

	public const bool SuccesfullyRolledLastStand = false;

	public const float LastStandTime = -1000f;

	public const float Adrenaline = 0f;

	public const float CurAdrenaline = 0f;

	public const float VenomCurrent = 0f;

	public const float VenomTotal = 0f;

	public const float ClawHealth = 100f;

	public const float BloodPressureChangeFromMedicine = 0f;

	public const float Caffeinated = 0f;

	public const float ClawRegrowTime = 0f;

	// ---- Body: KrokMP's ResetHealth additions ----

	public const bool BothEyesGone = false;

	/// <summary>KrokMP clears it; the native heal does not. A left-over reversal is a control debuff no heal should keep.</summary>
	public const bool ReversedControls = false;

	// ---- Body: the game's own revive baseline (TraderRecruitPolicy.PrepareRevive) ----

	/// <summary>Zero is what lets the revived body stand: <c>Body.Stand</c> needs <c>shock &lt; 10</c>.</summary>
	public const float Shock = 0f;

	/// <summary>Clear of the pain-shock latch, which otherwise forces consciousness to zero.</summary>
	public const float PainShock = 0f;

	public const bool FibrillationForced = false;

	// ---- Medication components: the native heal destroys the three it names ----
	// The mindwipe fields sit here because they travel in the same health message, but the
	// removal itself is KrokMP's `unmindwipe` arm and CUO's `HollowClear` (component + overlay):
	// the native heal never touches MindwipeScript.

	public const float OpiateAmount = 0f;

	public const float OpiateTolerance = 0f;

	public const float OpiateReception = 0f;

	public const float AntagonistAmount = 0f;

	public const float ActualOpiateReception = 0f;

	public const float SleepingPillsAmount = 0f;

	public const float AntidepressantsAmount = 0f;

	public const float AntidepressantsCurrentAmount = 0f;

	public const bool MindwipeScriptPresent = false;

	public const bool MindwipeScriptActive = false;

	// ---- Limb: the native heal's nine fields plus KrokMP's furBloodAmount ----

	public const float LimbMuscleHealth = 100f;

	public const float LimbSkinHealth = 100f;

	public const float LimbBoneHealTimer = 0f;

	public const float LimbDislocationTimer = 0f;

	public const float LimbInfectionAmount = 0f;

	public const float LimbBleedAmount = 0f;

	public const float LimbPain = 0f;

	public const int LimbShrapnel = 0;

	public const bool LimbInfected = false;

	public const float LimbFurBloodAmount = 0f;

	public const float LimbSkinHealAmount = 0f;

	public const float LimbBandageSlowAmount = 0f;

	public const float LimbDisinfectionTime = 0f;

	/// <summary>The severed-limb restore's own field: cleared so the limb's reappearance and its state agree.</summary>
	public const bool LimbDismembered = false;

	/// <summary>Cleared with <c>MendBone</c>; the applier runs the native clear so the broken-bone sprite goes too.</summary>
	public const bool LimbBroken = false;

	/// <summary>Cleared with <c>UnDislocate</c>, the native call <c>Limb.Update</c> makes at timer zero.</summary>
	public const bool LimbDislocated = false;

	public const bool LimbSplinted = false;

	public const bool LimbBlockedBleeding = false;

	/// <summary>
	/// Write the table onto a captured health message and its limb messages. The caller captures
	/// the live body FIRST, so every field this method does not name keeps the live value; the
	/// Game Adapter then applies the result through the same mapper the heal result uses.
	/// </summary>
	public static void ApplyTo(CharacterHealthMsg health, IReadOnlyList<CharacterLimbMsg> limbs)
	{
		health.BrainHealth = BrainHealth;
		health.BloodVolume = BloodVolume;
		health.BloodOxygen = BloodOxygen;
		health.BloodPressure = BloodPressure;
		health.HeartRate = HeartRate;
		health.BloodVesselSize = BloodVesselSize;
		health.BloodViscosity = BloodViscosity;
		health.RespiratoryRate = RespiratoryRate;
		health.StrokeAmount = StrokeAmount;
		health.HasPulmonaryEmbolism = HasPulmonaryEmbolism;
		health.FibrillationProgress = FibrillationProgress;
		health.Hunger = Hunger;
		health.Thirst = Thirst;
		health.SepticShock = SepticShock;
		health.Temperature = Temperature;
		health.SicknessAmount = SicknessAmount;
		health.Consciousness = Consciousness;
		health.Stamina = Stamina;
		health.Energy = Energy;
		health.Happiness = Happiness;
		health.WeightOffset = WeightOffset;
		health.RadiationSickness = RadiationSickness;
		health.Disfigured = Disfigured;
		health.EyeGone = EyeGone;
		health.InternalBleeding = InternalBleeding;
		health.Hemothorax = Hemothorax;
		health.TraumaAmount = TraumaAmount;
		health.Dirtyness = Dirtyness;
		health.Wetness = Wetness;
		health.BadSleepAmount = BadSleepAmount;
		health.HearingLoss = HearingLoss;
		health.AntibioticImmunityTime = AntibioticImmunityTime;
		health.BrainGrowSickness = BrainGrowSickness;
		health.TriedRollingLastStand = TriedRollingLastStand;
		health.SuccesfullyRolledLastStand = SuccesfullyRolledLastStand;
		health.LastStandTime = LastStandTime;
		health.Adrenaline = Adrenaline;
		health.CurAdrenaline = CurAdrenaline;
		health.VenomCurrent = VenomCurrent;
		health.VenomTotal = VenomTotal;
		health.ClawHealth = ClawHealth;
		health.BloodPressureChangeFromMedicine = BloodPressureChangeFromMedicine;
		health.Caffeinated = Caffeinated;
		health.ClawRegrowTime = ClawRegrowTime;
		health.BothEyesGone = BothEyesGone;
		health.Shock = Shock;
		health.PainShock = PainShock;
		health.FibrillationForced = FibrillationForced;
		health.OpiateAmount = OpiateAmount;
		health.OpiateTolerance = OpiateTolerance;
		health.OpiateReception = OpiateReception;
		health.AntagonistAmount = AntagonistAmount;
		health.ActualOpiateReception = ActualOpiateReception;
		health.SleepingPillsAmount = SleepingPillsAmount;
		health.AntidepressantsAmount = AntidepressantsAmount;
		health.AntidepressantsCurrentAmount = AntidepressantsCurrentAmount;
		health.MindwipeScriptPresent = MindwipeScriptPresent;
		health.MindwipeScriptActive = MindwipeScriptActive;

		foreach (var limb in limbs)
		{
			limb.MuscleHealth = LimbMuscleHealth;
			limb.SkinHealth = LimbSkinHealth;
			limb.BoneHealTimer = LimbBoneHealTimer;
			limb.DislocationTimer = LimbDislocationTimer;
			limb.InfectionAmount = LimbInfectionAmount;
			limb.BleedAmount = LimbBleedAmount;
			limb.Pain = LimbPain;
			limb.Shrapnel = LimbShrapnel;
			limb.Infected = LimbInfected;
			limb.FurBloodAmount = LimbFurBloodAmount;
			limb.SkinHealAmount = LimbSkinHealAmount;
			limb.BandageSlowAmount = LimbBandageSlowAmount;
			limb.DisinfectionTime = LimbDisinfectionTime;
			limb.Dismembered = LimbDismembered;
			limb.Broken = LimbBroken;
			limb.Dislocated = LimbDislocated;
			limb.Splinted = LimbSplinted;
			limb.BlockedBleeding = LimbBlockedBleeding;
			// The component list is the removal direction: the codec treats an omitted type as
			// gone, so clearing it is what removes splints, tourniquets and chilled limbs.
			limb.Components.Clear();
		}
	}
}
