using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Character;

/// <summary>
/// The <c>/heal</c> reset's field table, field by field: the values the game's own <c>heal</c>
/// command writes, KrokMP's additions, and the revive baseline's three clears. The list is asserted
/// against the reference reading (the native command, KrokMP's <c>ResetHealth</c> and
/// <c>TraderRecruitPolicy.PrepareRevive</c>) rather than against the table itself, so a field that
/// silently leaves the reset fails here before any two-client run could notice.
///
/// <para>
/// The adapter's application of the table is the runtime's half (a test host has no game
/// <c>Body</c>); these cases pin the DATA and the two directions the application depends on: the
/// values written into the captured message, and the removal direction (components, hollow, limb
/// latches) that must arrive as absence rather than as a zero.
/// </para>
/// </summary>
public class HealResetStateTests
{
	/// <summary>The reference field set, name → the value the reset must carry. Body first, then limb; the order is the reading's order.</summary>
	private static readonly Dictionary<string, object> Expected = new(StringComparer.Ordinal)
	{
		// Native heal: body fields (ConsoleScript.cs).
		["BrainHealth"] = 100f,
		["BloodVolume"] = 100f,
		["BloodOxygen"] = 100f,
		["BloodPressure"] = 120f,
		["HeartRate"] = 70f,
		["BloodVesselSize"] = 1f,
		["BloodViscosity"] = 0f,
		["RespiratoryRate"] = 100f,
		["StrokeAmount"] = 0f,
		["HasPulmonaryEmbolism"] = false,
		["FibrillationProgress"] = 0f,
		["Hunger"] = 100f,
		["Thirst"] = 100f,
		["SepticShock"] = 0f,
		["Temperature"] = 37f,
		["SicknessAmount"] = 0f,
		["Consciousness"] = 100f,
		["Stamina"] = 100f,
		["Energy"] = 100f,
		["Happiness"] = 0f,
		["WeightOffset"] = 0f,
		["RadiationSickness"] = 0f,
		["Disfigured"] = false,
		["EyeGone"] = false,
		["InternalBleeding"] = 0f,
		["Hemothorax"] = 0f,
		["TraumaAmount"] = 0f,
		["Dirtyness"] = 0f,
		["Wetness"] = 0f,
		["BadSleepAmount"] = 0f,
		["HearingLoss"] = 0f,
		["AntidepressantHappiness"] = 0f,
		["OpiateHappiness"] = 0f,
		["AntibioticImmunityTime"] = 0f,
		["BrainGrowSickness"] = 0f,
		["TriedRollingLastStand"] = false,
		["SuccesfullyRolledLastStand"] = false,
		["LastStandTime"] = -1000f,
		["Adrenaline"] = 0f,
		["CurAdrenaline"] = 0f,
		["VenomCurrent"] = 0f,
		["VenomTotal"] = 0f,
		["ClawHealth"] = 100f,
		["BloodPressureChangeFromMedicine"] = 0f,
		["Caffeinated"] = 0f,
		["ClawRegrowTime"] = 0f,
		// KrokMP ResetHealth additions.
		["BothEyesGone"] = false,
		["ReversedControls"] = false,
		// The game's own revive baseline (TraderRecruitPolicy.PrepareRevive).
		["Shock"] = 0f,
		["PainShock"] = 0f,
		["FibrillationForced"] = false,
		// The medication components the native heal destroys: absence, expressed as the cleared
		// component fields (the component policy reads them).
		["OpiateAmount"] = 0f,
		["OpiateTolerance"] = 0f,
		["OpiateReception"] = 0f,
		["AntagonistAmount"] = 0f,
		["ActualOpiateReception"] = 0f,
		["SleepingPillsAmount"] = 0f,
		["AntidepressantsAmount"] = 0f,
		["AntidepressantsCurrentAmount"] = 0f,
		["MindwipeScriptPresent"] = false,
		["MindwipeScriptActive"] = false,
		// The native heal's nine limb fields plus KrokMP's furBloodAmount.
		["LimbMuscleHealth"] = 100f,
		["LimbSkinHealth"] = 100f,
		["LimbBoneHealTimer"] = 0f,
		["LimbDislocationTimer"] = 0f,
		["LimbInfectionAmount"] = 0f,
		["LimbBleedAmount"] = 0f,
		["LimbPain"] = 0f,
		["LimbShrapnel"] = 0,
		["LimbInfected"] = false,
		["LimbFurBloodAmount"] = 0f,
		// The limb's own removal direction (the restore's flags and the healing timers).
		["LimbSkinHealAmount"] = 0f,
		["LimbBandageSlowAmount"] = 0f,
		["LimbDisinfectionTime"] = 0f,
		["LimbDismembered"] = false,
		["LimbBroken"] = false,
		["LimbDislocated"] = false,
		["LimbSplinted"] = false,
		["LimbBlockedBleeding"] = false,
	};

	[Fact]
	public void TheFieldTable_CoversExactlyTheReferenceFields()
	{
		var actual = typeof(HealResetState)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Where(field => field.IsLiteral)
			.Select(field => field.Name)
			.OrderBy(name => name, StringComparer.Ordinal)
			.ToArray();
		var expected = Expected.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray();

		Assert.Equal(expected, actual);
	}

	[Fact]
	public void TheFieldTable_CarriesEveryReferenceValue()
	{
		var failures = new List<string>();
		foreach (var pair in Expected)
		{
			var field = typeof(HealResetState).GetField(pair.Key, BindingFlags.Public | BindingFlags.Static);
			if (field is null)
			{
				failures.Add($"{pair.Key}: missing");
				continue;
			}

			var actual = field.GetValue(null);
			if (!Equals(pair.Value, actual))
			{
				failures.Add($"{pair.Key}: expected {pair.Value}, was {actual}");
			}
		}

		Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void ApplyTo_WritesTheHealthyStateOntoTheCapturedBodyAndLimbs()
	{
		var health = SentinelHealth();
		var limbs = new List<CharacterLimbMsg> { SentinelLimb(0), SentinelLimb(1) };

		HealResetState.ApplyTo(health, limbs);

		Assert.Equal(100f, health.BrainHealth);
		Assert.Equal(120f, health.BloodPressure);
		Assert.Equal(37f, health.Temperature);
		Assert.Equal(-1000f, health.LastStandTime);
		Assert.Equal(0f, health.OpiateAmount);
		Assert.False(health.MindwipeScriptPresent);
		foreach (var limb in limbs)
		{
			Assert.Equal(100f, limb.MuscleHealth);
			Assert.Equal(100f, limb.SkinHealth);
			Assert.Equal(0f, limb.BleedAmount);
			Assert.Equal(0, limb.Shrapnel);
			Assert.False(limb.Dismembered);
			Assert.False(limb.Broken);
			Assert.False(limb.Dislocated);
			Assert.False(limb.Splinted);
			Assert.False(limb.BlockedBleeding);
			Assert.Empty(limb.Components);
		}
	}

	/// <summary>
	/// Every constant that has a message property of the same name is wired to THAT property: the
	/// sampled assertions above can catch a value, not a constant wired onto the wrong field. The
	/// three body-only extras (`ReversedControls`, `OpiateHappiness`, `AntidepressantHappiness`) have
	/// no wire property and are written by the Game Adapter; the table is their only source.
	/// </summary>
	[Fact]
	public void ApplyTo_WritesEveryMessageBackedTableField()
	{
		var health = SentinelHealth();
		var limbs = new List<CharacterLimbMsg> { SentinelLimb(0) };

		HealResetState.ApplyTo(health, limbs);

		var failures = new List<string>();
		foreach (var field in typeof(HealResetState).GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.IsLiteral))
		{
			var expected = field.GetValue(null);
			if (field.Name.StartsWith("Limb", StringComparison.Ordinal))
			{
				var property = typeof(CharacterLimbMsg).GetProperty(field.Name.Substring("Limb".Length));
				if (property is null)
				{
					continue;
				}

				var actual = property.GetValue(limbs[0]);
				if (!Equals(expected, actual))
				{
					failures.Add($"{field.Name}: wrote {actual} onto CharacterLimbMsg.{property.Name}, expected {expected}");
				}

				continue;
			}

			var bodyProperty = typeof(CharacterHealthMsg).GetProperty(field.Name);
			if (bodyProperty is null)
			{
				continue;
			}

			var bodyValue = bodyProperty.GetValue(health);
			if (!Equals(expected, bodyValue))
			{
				failures.Add($"{field.Name}: wrote {bodyValue} onto CharacterHealthMsg.{bodyProperty.Name}, expected {expected}");
			}
		}

		Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
	}

	/// <summary>
	/// The capture-then-apply contract: the table owns only the fields it names. Movement
	/// parameters, pose presentation and mood history are the live body's and must survive the
	/// reset — writing them from a defaulted message would freeze or blank the player.
	/// </summary>
	[Fact]
	public void ApplyTo_LeavesTheFieldsTheReferencePathsDoNotResetAlone()
	{
		var health = SentinelHealth();
		var limbs = new List<CharacterLimbMsg> { SentinelLimb(0) };

		HealResetState.ApplyTo(health, limbs);

		Assert.Equal(-1f, health.MaxSpeed);
		Assert.Equal(-1f, health.JumpSpeed);
		Assert.Equal(-1f, health.TemporarySlowdown);
		Assert.Equal(-1f, health.MoveForce);
		Assert.Equal(-1f, health.SlowdownAmount);
		Assert.Equal(-1f, health.LegSpeedMult);
		Assert.Equal(-1f, health.EatTime);
		Assert.Equal(-1f, health.HorrifiedLevel);
		Assert.Equal(-1f, health.FocusedLevel);
		Assert.Equal(-1f, health.EyePanicTime);
		Assert.Equal(-1f, health.DesensitizedMult);
		Assert.Equal(-1, health.CorpsesSeen);
		Assert.Equal(-1f, health.GoodSleepTime);
		Assert.Equal(-1f, health.SnowAmount);
		Assert.Equal(-1f, health.Immunity);
		Assert.False(health.UsedNeuralBooster);
		Assert.Equal(-1, health.DisfiguredIndex);
		Assert.Equal(-1f, health.DisfiguredTimeFullSkin);
		Assert.Equal(-1f, health.EyeTimeHealed);
	}

	/// <summary>The removal direction the reset must send: the medication/hollow component policy reads absence from the cleared fields.</summary>
	[Fact]
	public void ApplyTo_SendsTheComponentRemovalsAsAbsence()
	{
		var health = new CharacterHealthMsg
		{
			OpiateAmount = 4f,
			OpiateTolerance = 3f,
			SleepingPillsAmount = 2f,
			AntidepressantsAmount = 5f,
			MindwipeScriptPresent = true,
			MindwipeScriptActive = true,
		};

		HealResetState.ApplyTo(health, []);

		Assert.Equal(MedicationComponentPresence.Absent, MedicationComponentPolicy.ForPainkillers(
			health.OpiateAmount, health.OpiateTolerance, health.OpiateReception, health.AntagonistAmount, health.ActualOpiateReception));
		Assert.Equal(MedicationComponentPresence.Absent, MedicationComponentPolicy.ForSleepingPills(health.SleepingPillsAmount));
		Assert.Equal(MedicationComponentPresence.Absent, MedicationComponentPolicy.ForAntidepressants(health.AntidepressantsAmount, health.AntidepressantsCurrentAmount));
		Assert.Equal(MedicationComponentPresence.Absent, MedicationComponentPolicy.ForMindwipe(health.MindwipeScriptPresent, health.MindwipeScriptActive));
	}

	private static CharacterHealthMsg SentinelHealth() => new()
	{
		BrainHealth = -1f,
		BloodVolume = -1f,
		BloodOxygen = -1f,
		BloodPressure = -1f,
		HeartRate = -1f,
		BloodVesselSize = -1f,
		BloodViscosity = -1f,
		RespiratoryRate = -1f,
		StrokeAmount = -1f,
		HasPulmonaryEmbolism = true,
		FibrillationProgress = -1f,
		FibrillationForced = true,
		Shock = -1f,
		PainShock = -1f,
		Hunger = -1f,
		Thirst = -1f,
		SepticShock = -1f,
		Temperature = -1f,
		SicknessAmount = -1f,
		Consciousness = -1f,
		Stamina = -1f,
		Energy = -1f,
		Happiness = -1f,
		WeightOffset = -1f,
		RadiationSickness = -1f,
		Disfigured = true,
		EyeGone = true,
		BothEyesGone = true,
		InternalBleeding = -1f,
		Hemothorax = -1f,
		TraumaAmount = -1f,
		Dirtyness = -1f,
		Wetness = -1f,
		BadSleepAmount = -1f,
		HearingLoss = -1f,
		AntibioticImmunityTime = -1f,
		BrainGrowSickness = -1f,
		TriedRollingLastStand = true,
		SuccesfullyRolledLastStand = true,
		LastStandTime = -1f,
		Adrenaline = -1f,
		CurAdrenaline = -1f,
		VenomCurrent = -1f,
		VenomTotal = -1f,
		ClawHealth = -1f,
		BloodPressureChangeFromMedicine = -1f,
		Caffeinated = -1f,
		ClawRegrowTime = -1f,
		// Fields the reset must NOT touch.
		MaxSpeed = -1f,
		JumpSpeed = -1f,
		TemporarySlowdown = -1f,
		MoveForce = -1f,
		SlowdownAmount = -1f,
		LegSpeedMult = -1f,
		EatTime = -1f,
		HorrifiedLevel = -1f,
		FocusedLevel = -1f,
		EyePanicTime = -1f,
		DesensitizedMult = -1f,
		CorpsesSeen = -1,
		GoodSleepTime = -1f,
		SnowAmount = -1f,
		Immunity = -1f,
		UsedNeuralBooster = false,
		DisfiguredIndex = -1,
		DisfiguredTimeFullSkin = -1f,
		EyeTimeHealed = -1f,
		Alive = false,
		Conscious = false,
	};

	private static CharacterLimbMsg SentinelLimb(int index) => new()
	{
		Index = index,
		MuscleHealth = -1f,
		SkinHealth = -1f,
		BoneHealTimer = -1f,
		DislocationTimer = -1f,
		InfectionAmount = -1f,
		BleedAmount = -1f,
		Pain = -1f,
		Shrapnel = -1,
		Infected = true,
		FurBloodAmount = -1f,
		SkinHealAmount = -1f,
		BandageSlowAmount = -1f,
		DisinfectionTime = -1f,
		Dismembered = true,
		Broken = true,
		Dislocated = true,
		Splinted = true,
		BlockedBleeding = true,
		Components =
		[
			new ComponentStateMsg { TypeName = "SplintLimb" },
		],
	};
}
