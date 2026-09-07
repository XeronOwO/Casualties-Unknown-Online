using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Pure model tests for the unified remote-character presentation shape. This
/// is the L0 surface that the adapter display appliers consume; it must not
/// depend on Unity so the field set and derived readouts are lockable outside
/// the game.
/// </summary>
public class RemoteCharacterPresentationTests
{
	[Fact]
	public void From_CharacterDataMsg_BuildsAllDerivedStates()
	{
		var health = new CharacterHealthMsg
		{
			Disfigured = true,
			EyeGone = true,
			BothEyesGone = false,
			DisfiguredIndex = 3,
			DisfiguredTimeFullSkin = 12.5f,
			EyeTimeHealed = 7f,
			HeadMouth = HeadMouthState.Open,
			EatTime = 0.4f,
			Consciousness = 66f,
			Energy = 11f,
			LegSpeedMult = 0.35f,
			Alive = true,
			HeartRate = 74f,
			BloodPressure = 118f,
			RespiratoryRate = 80f,
			OpiateAmount = 10f,
			OpiateTolerance = 2f,
			OpiateReception = 1f,
			ActualOpiateReception = 1f,
			AntidepressantsAmount = 5f,
			AntidepressantsCurrentAmount = 3f,
			MindwipeScriptPresent = true,
		};
		var limb = new CharacterLimbMsg { Index = 0, IsHead = true };
		var item = new CharacterItemMsg { InstanceId = 9, ItemId = "scrap", Condition = 0.75f };
		var data = new CharacterDataMsg
		{
			Health = health,
			Limbs = [limb],
			Items = [item],
			HandSlot = 2,
		};

		var state = RemoteCharacterPresentation.State.From(data);

		Assert.NotNull(state.Face);
		Assert.True(state.Face!.Disfigured);
		Assert.True(state.Face.EyeGone);
		Assert.False(state.Face.BothEyesGone);
		Assert.Equal(3, state.Face.DisfiguredIndex);
		Assert.Equal(12.5f, state.Face.DisfiguredTimeFullSkin, 3);
		Assert.Equal(7f, state.Face.EyeTimeHealed, 3);
		Assert.Equal(0.4f, state.Face.EatTime, 3);
		Assert.Equal(HeadMouthState.Open, state.Face.HeadMouth);
		Assert.Equal(66f, state.Face.Vitals.Consciousness);
		Assert.NotNull(state.BodyPose);
		Assert.Equal(0.35f, state.BodyPose!.LegSpeedMult, 3);
		Assert.NotNull(state.Medical);
		Assert.Equal(74f, state.Medical!.HeartRate, 3);
		Assert.Equal(118f, state.Medical.BloodPressure, 3);
		Assert.True(state.Medical.Breathing);
		Assert.Equal("20/m", state.Medical.RespiratoryRateReadout);
		Assert.Equal(10f, state.Medical.OpiateAmount);
		Assert.Equal(2f, state.Medical.OpiateTolerance, 3);
		Assert.Equal(1f, state.Medical.ActualOpiateReception, 3);
		Assert.Equal(3f, state.Medical.AntidepressantsCurrentAmount, 3);
		Assert.True(state.Medical.MindwipeScriptPresent);
		Assert.NotNull(state.Inventory);
		Assert.Single(state.Inventory!.Items);
		Assert.Equal(0.75f, state.Inventory.Items[0].Condition, 3);
		Assert.Same(data, state.Source);
		Assert.Single(state.Limbs);
		Assert.Single(state.Items);
		Assert.Equal(2, state.HandSlot);
	}

	[Fact]
	public void MedicalState_BreathingUsesNativeAliveAndRespiratoryThreshold()
	{
		var normal = RemoteCharacterPresentation.MedicalState.From(new CharacterHealthMsg
		{
			Alive = true,
			RespiratoryRate = 80f,
		});
		var stopped = RemoteCharacterPresentation.MedicalState.From(new CharacterHealthMsg
		{
			Alive = true,
			RespiratoryRate = 0f,
		});
		var dead = RemoteCharacterPresentation.MedicalState.From(new CharacterHealthMsg
		{
			Alive = false,
			RespiratoryRate = 80f,
		});

		Assert.True(normal.Breathing);
		Assert.False(stopped.Breathing);
		Assert.False(dead.Breathing);
		Assert.Equal("0/m", stopped.RespiratoryRateReadout);
		Assert.Equal("20/m", normal.RespiratoryRateReadout);
	}

	[Fact]
	public void From_NullHealth_LeavesDerivedStatesNull()
	{
		var state = RemoteCharacterPresentation.State.From(new CharacterDataMsg());

		Assert.Null(state.Face);
		Assert.Null(state.BodyPose);
		Assert.Null(state.Medical);
		Assert.NotNull(state.Source);
		Assert.Empty(state.Limbs);
		Assert.Empty(state.Items);
	}

	[Fact]
	public void From_HealthAndLimbs_WorksForMedicalOperationDelta()
	{
		var health = new CharacterHealthMsg { Alive = false, HeartRate = 0f };
		var limbs = new[] { new CharacterLimbMsg { Index = 2, Pain = 7f } };

		var state = RemoteCharacterPresentation.State.From(health, [.. limbs]);

		Assert.Same(health, state.Health);
		Assert.Equal("CasualtiesUnknownOnline.Runtime.Protocol.Messages.CharacterLimbMsg",
			state.Limbs.Single().GetType().FullName);
		Assert.False(state.Medical!.Breathing);
	}
}
