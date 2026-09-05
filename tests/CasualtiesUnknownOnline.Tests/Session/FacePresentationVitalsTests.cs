using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public class FacePresentationVitalsTests
{
	[Fact]
	public void From_MapsEveryFaceDrivingVital()
	{
		var health = new CharacterHealthMsg
		{
			Consciousness = 65f,
			Energy = 12f,
			BadSleepAmount = 150f,
			RadiationSickness = 3f,
			Shock = 22f,
			Adrenaline = 4f,
			SicknessAmount = 33f,
			Temperature = 36.5f,
			InternalBleeding = 7f,
			BloodPressure = 118f,
			Happiness = -35f,
			HeadMouth = HeadMouthState.HalfOpen,
		};

		var vitals = FacePresentationVitals.From(health);

		Assert.Equal(65f, vitals.Consciousness);
		Assert.Equal(12f, vitals.Energy);
		Assert.Equal(150f, vitals.BadSleepAmount);
		Assert.Equal(3f, vitals.RadiationSickness);
		Assert.Equal(22f, vitals.Shock);
		Assert.Equal(4f, vitals.Adrenaline);
		Assert.Equal(33f, vitals.SicknessAmount);
		Assert.Equal(36.5f, vitals.Temperature);
		Assert.Equal(7f, vitals.InternalBleeding);
		Assert.Equal(118f, vitals.BloodPressure);
		Assert.Equal(-35f, vitals.Happiness);
		Assert.Equal(HeadMouthState.HalfOpen, vitals.HeadMouth);
	}

	[Fact]
	public void From_HealthWithDefaults_KeepsDefaults()
	{
		var vitals = FacePresentationVitals.From(new CharacterHealthMsg());

		Assert.Equal(0f, vitals.Consciousness);
		Assert.Equal(0f, vitals.Energy);
		Assert.Equal(0f, vitals.BadSleepAmount);
		Assert.Equal(0f, vitals.Happiness);
		Assert.Equal(HeadMouthState.Closed, vitals.HeadMouth);
	}
}
