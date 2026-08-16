using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The pure enemy terminal-state applier: bite/lunge/proximity-effect events
/// rebuild the exact terminal fields on a CharacterDataMsg — the host's saved
/// snapshot and the Game Adapter's clone fact table share this machine, so the
/// two records can never diverge in interpretation.
/// </summary>
public class EnemyTerminalStateApplierTests
{
	[Fact]
	public void ApplyBite_ReplacesTheLimbAndBodyFields()
	{
		var data = new CharacterDataMsg
		{
			Health = new CharacterHealthMsg { Happiness = 10f },
			Limbs = [new CharacterLimbMsg { Index = 2, Pain = 1f }],
		};

		EnemyTerminalStateApplier.ApplyBite(data, new EnemyBiteMsg
		{
			VictimSteamId = 7,
			Limb = new CharacterLimbMsg { Index = 2, Pain = 12f, SkinHealth = 80f },
			VenomTotal = 3f,
			Adrenaline = 75f,
			Happiness = -0.75f,
		});

		Assert.Equal(12f, data.Limbs[0].Pain);
		Assert.Equal(80f, data.Limbs[0].SkinHealth);
		Assert.Equal(3f, data.Health.VenomTotal);
		Assert.Equal(75f, data.Health!.Adrenaline);
		Assert.Equal(-0.75f, data.Health!.Happiness);
	}

	[Fact]
	public void ApplyLunge_ReplacesTheLimbAndBodyFields()
	{
		var data = new CharacterDataMsg();

		EnemyTerminalStateApplier.ApplyLunge(data, new EnemyLungeMsg
		{
			Limb = new CharacterLimbMsg { Index = 0, MuscleHealth = 65f, BleedAmount = 15f },
			Adrenaline = 70f,
			Stamina = 100f,
		});

		Assert.Single(data.Limbs);
		Assert.Equal(0, data.Limbs[0].Index);
		Assert.Equal(65f, data.Limbs[0].MuscleHealth);
		Assert.Equal(70f, data.Health!.Adrenaline);
		Assert.Equal(100f, data.Health!.Stamina);
	}

	[Fact]
	public void ApplyEffect_ElderHorrorTick_SetsOnlyItsFields()
	{
		var data = new CharacterDataMsg
		{
			Health = new CharacterHealthMsg { Happiness = 1f, SepticShock = 2f },
		};

		EnemyTerminalStateApplier.ApplyEffect(data, new EnemyEffectMsg
		{
			Kind = EnemyEffectKind.ElderHorrorTick,
			HorrifiedLevel = 100f,
			FocusedLevel = 100f,
			Adrenaline = 50f,
			Energy = 15f,
			Stamina = 61f,
		});

		Assert.Equal(100f, data.Health!.HorrifiedLevel);
		Assert.Equal(100f, data.Health!.FocusedLevel);
		Assert.Equal(50f, data.Health!.Adrenaline);
		Assert.Equal(15f, data.Health!.Energy);
		Assert.Equal(61f, data.Health!.Stamina);
		Assert.Equal(1f, data.Health!.Happiness); // untouched
		Assert.Equal(2f, data.Health!.SepticShock); // untouched
	}

	[Fact]
	public void ApplyEffect_ElderHorrorDefeat_SetsHorrorHappinessAndCaffeine()
	{
		var data = new CharacterDataMsg();

		EnemyTerminalStateApplier.ApplyEffect(data, new EnemyEffectMsg
		{
			Kind = EnemyEffectKind.ElderHorrorDefeat,
			HorrifiedLevel = 0f,
			Happiness = 40f,
			Caffeinated = 600f,
		});

		Assert.Equal(0f, data.Health!.HorrifiedLevel);
		Assert.Equal(40f, data.Health!.Happiness);
		Assert.Equal(600f, data.Health!.Caffeinated);
	}

	[Fact]
	public void ApplyEffect_XalorisSepticTick_SetsSepticShock()
	{
		var data = new CharacterDataMsg();

		EnemyTerminalStateApplier.ApplyEffect(data, new EnemyEffectMsg
		{
			Kind = EnemyEffectKind.XalorisSepticTick,
			SepticShock = 12.074f,
		});

		Assert.Equal(12.074f, data.Health!.SepticShock);
	}

	[Fact]
	public void ApplyEffect_GrabberGrabbed_SetsShockAndEyePanic()
	{
		var data = new CharacterDataMsg();

		EnemyTerminalStateApplier.ApplyEffect(data, new EnemyEffectMsg
		{
			Kind = EnemyEffectKind.GrabberGrabbed,
			Shock = 20f,
			EyePanicTime = 0.5f,
		});

		Assert.Equal(20f, data.Health!.Shock);
		Assert.Equal(0.5f, data.Health!.EyePanicTime);
	}

	[Fact]
	public void ApplyLimbState_ReplacesTheFullLimbSetAndBodyHealth()
	{
		var data = new CharacterDataMsg
		{
			Health = new CharacterHealthMsg { Happiness = 1f },
			Limbs =
			[
				new CharacterLimbMsg { Index = 0, Pain = 1f },
				new CharacterLimbMsg { Index = 3, Broken = true },
			],
		};

		EnemyTerminalStateApplier.ApplyLimbState(data, new LimbStateEventMsg
		{
			OwnerSteamId = 7,
			Limbs =
			[
				new CharacterLimbMsg { Index = 0, Pain = 12f, Dismembered = true },
				new CharacterLimbMsg { Index = 2, SkinHealth = 80f },
			],
			Health = new CharacterHealthMsg { Happiness = 40f, Adrenaline = 75f },
		});

		Assert.Equal(2, data.Limbs.Count);
		Assert.Equal(12f, data.Limbs[0].Pain);
		Assert.True(data.Limbs[0].Dismembered);
		Assert.Equal(2, data.Limbs[1].Index);
		Assert.Equal(80f, data.Limbs[1].SkinHealth);
		Assert.Equal(40f, data.Health!.Happiness);
		Assert.Equal(75f, data.Health!.Adrenaline);
	}

	[Fact]
	public void ApplyLimbState_WithoutBodyHealth_KeepsTheExistingBodyRecord()
	{
		var data = new CharacterDataMsg
		{
			Health = new CharacterHealthMsg { Happiness = 1f },
		};

		EnemyTerminalStateApplier.ApplyLimbState(data, new LimbStateEventMsg
		{
			Limbs = [new CharacterLimbMsg { Index = 1, Broken = true }],
		});

		Assert.True(data.Health!.Happiness == 1f, "an old-version event without Health must only rebuild limbs");
	}

	[Fact]
	public void ApplyEffect_UnknownKind_LeavesTheRecordUntouched()
	{
		var data = new CharacterDataMsg { Health = new CharacterHealthMsg { Shock = 3f } };

		EnemyTerminalStateApplier.ApplyEffect(data, new EnemyEffectMsg
		{
			Kind = (EnemyEffectKind)200,
			Shock = 99f,
		});

		Assert.Equal(3f, data.Health!.Shock);
	}
}
