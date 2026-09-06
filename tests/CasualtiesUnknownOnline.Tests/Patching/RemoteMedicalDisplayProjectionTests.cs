using System;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// L0 regression surface for the remote WoundView display-only body projection.
/// The adapter is compile-excluded (it binds Unity/game assemblies), so these
/// tests lock the pure display rules by reflection. They fail on the pre-fix
/// adapter because the projection type did not exist and the remote display
/// still followed the 1 Hz character snapshot for mood/breathing/ECG.
/// </summary>
public class RemoteMedicalDisplayProjectionTests
{
	private static readonly Type Projection = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.RemoteMedicalDisplayProjection",
		throwOnError: false)!;

	[Fact]
	public void ProjectionType_Exists() => Assert.NotNull(Projection);

	[Fact]
	public void ProjectBreathing_UsesAuthoritativeRespiratoryRate()
	{
		var method = GetStaticMethod("ProjectBreathing");

		var stopped = new CharacterHealthMsg { Alive = true, RespiratoryRate = 0f };
		var normal = new CharacterHealthMsg { Alive = true, RespiratoryRate = 80f };
		var threshold = new CharacterHealthMsg { Alive = true, RespiratoryRate = 10f };
		var dead = new CharacterHealthMsg { Alive = false, RespiratoryRate = 80f };

		Assert.False((bool)Invoke(method, stopped)!);
		Assert.True((bool)Invoke(method, normal)!);
		Assert.False((bool)Invoke(method, threshold)!);
		Assert.False((bool)Invoke(method, dead)!);
	}

	[Fact]
	public void ProjectRespiratoryRateReadout_UsesNativeQuarterFormula()
	{
		var method = GetStaticMethod("ProjectRespiratoryRateReadout");

		var normal = new CharacterHealthMsg { RespiratoryRate = 80f };
		var stopped = new CharacterHealthMsg { RespiratoryRate = 0f };

		Assert.Equal("20/m", (string)Invoke(method, normal)!);
		Assert.Equal("0/m", (string)Invoke(method, stopped)!);
	}

	[Fact]
	public void AdvanceOpiateReception_MovesTowardCommittedOpiateDose()
	{
		var method = GetStaticMethod("AdvanceOpiateReception");

		// This is the acceptance regression: after a remote fentanyl delta the
		// authoritative snapshot carries the newly committed OpiateAmount while
		// ActualOpiateReception may still be the stale 1 Hz value (0). The
		// display must not wait for the next 1 Hz snapshot to show the mood
		// rise; it should ramp toward the committed dose every frame (and reach
		// it when enough time has passed), just like the owner's live
		// Painkillers component.
		var before = new CharacterHealthMsg
		{
			OpiateAmount = 0f,
			OpiateTolerance = 0f,
			OpiateReception = 0f,
			ActualOpiateReception = 0f,
		};
		var after = new CharacterHealthMsg
		{
			OpiateAmount = 42f,
			OpiateTolerance = 0f,
			OpiateReception = 0f,
			ActualOpiateReception = 0f,
		};

		var beforeValue = (float)Invoke(method, before, 0f, 0f)!;
		var stepped = (float)Invoke(method, after, beforeValue, 0.1f)!;
		var caughtUp = (float)Invoke(method, after, beforeValue, 10f)!;

		Assert.Equal(0f, beforeValue, 3);
		Assert.True(stepped > beforeValue + 0.1f,
			$"Expected a small same-frame ramp, got {beforeValue} -> {stepped}.");
		Assert.True(caughtUp > beforeValue + 20f,
			$"Expected the committed opiate dose to be reached, got {beforeValue} -> {caughtUp}.");
	}

	[Fact]
	public void AdvanceOpiateReception_AccountsForTolerance()
	{
		var method = GetStaticMethod("AdvanceOpiateReception");

		var health = new CharacterHealthMsg
		{
			OpiateAmount = 42f,
			OpiateTolerance = 20f,
			OpiateReception = 0f,
			ActualOpiateReception = 0f,
		};

		var value = (float)Invoke(method, health, 0f, 10f)!;
		Assert.Equal(22f, value, 3);
	}

	[Fact]
	public void AdvanceOpiateReception_AllZeroClearsImmediately()
	{
		var method = GetStaticMethod("AdvanceOpiateReception");

		var clean = new CharacterHealthMsg
		{
			OpiateAmount = 0f,
			OpiateTolerance = 0f,
			ActualOpiateReception = 0f,
		};

		var value = (float)Invoke(method, clean, 80f, 0.1f)!;
		Assert.Equal(0f, value, 3);
	}

	[Fact]
	public void AdvanceOpiateReception_ActualAboveTargetMovesDownAtNativeRate()
	{
		var method = GetStaticMethod("AdvanceOpiateReception");

		// Tolerance equals amount, so the native target reception is 0 while
		// the body still carries a high actual: this is the decay/withdrawal
		// path and must not stay pinned at the old high value.
		var decay = new CharacterHealthMsg
		{
			OpiateAmount = 10f,
			OpiateTolerance = 10f,
			ActualOpiateReception = 80f,
		};

		var value = (float)Invoke(method, decay, 80f, 0.1f)!;
		Assert.True(value < 80f && value > 79f,
			$"Expected a 0.5-unit native downward step, got {value}.");
	}

	[Fact]
	public void OpiateHappinessFromReception_MatchesNativePositiveAndNegativeRules()
	{
		var method = GetStaticMethod("OpiateHappinessFromReception");

		Assert.Equal(42f, (float)Invoke(method, 42f)!, 3);
		Assert.Equal(-80f, (float)Invoke(method, -50f)!, 3);
	}

	[Fact]
	public void AdvanceHeartProgress_StopsAtZeroHeartRate()
	{
		var method = GetStaticMethod("AdvanceHeartProgress");

		var progress = (float)Invoke(method, 70f, 0.2f, 0.1f)!;
		Assert.True(progress > 0.2f, "A beating display heart must advance its ECG progress.");

		var stopped = (float)Invoke(method, 0f, 0.9f, 0.1f)!;
		Assert.Equal(0f, stopped, 3);
	}

	[Fact]
	public void EcgRedirectPatch_IsPostfixSoItReplacesNativeGetterResult()
	{
		var patches = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.Patches.RemoteMedicalPatches",
			throwOnError: true)!;
		var nested = patches.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public);
		var ecgPatch = Assert.Single(nested, t => t.Name == "RemoteMedicalEcgBodyPatch");

		Assert.NotNull(ecgPatch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic));
		Assert.Null(ecgPatch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic));
	}

	private static MethodInfo GetStaticMethod(string name)
	{
		Assert.NotNull(Projection);
		var method = Projection!.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException($"RemoteMedicalDisplayProjection.{name} not found.");
		Assert.True(method.IsStatic);
		return method;
	}

	private static object? Invoke(MethodInfo method, params object?[] args) => method.Invoke(null, args);
}
