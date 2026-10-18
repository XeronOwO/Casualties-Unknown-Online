using System;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The host clone's model inputs: the owner's stream flags, the game's own walk threshold, and the
/// private setter the workout input goes through. A wrong threshold or a swapped flag would bend
/// the model's stamina/temperature branch silently, so the rule is pinned here and the wiring is
/// pinned by source in <see cref="HostVitalsSuppressionPinTests"/>.
/// </summary>
public class HostVitalsInputsTests
{
	[Fact]
	public void IsMoving_MatchesTheGamesStaminaThreshold()
	{
		Assert.True(IsMoving(1f));
		Assert.True(IsMoving(-1f));
		Assert.True(IsMoving(0.1f));
		Assert.True(IsMoving(-0.1f));
		Assert.False(IsMoving(0.09f));
		Assert.False(IsMoving(-0.09f));
		Assert.False(IsMoving(0f));
		// The native test is `Abs(x) < 0.1`; a NaN makes that FALSE, which selects the DRAIN branch —
		// so the derived verdict must say "not stationary", not "stationary".
		Assert.True(IsMoving(float.NaN));
	}

	[Fact]
	public void From_MapsEveryModelInputFlagFromTheStream()
	{
		var entity = new PlayerEntity(steamId: 1, entityId: default, isLocal: false)
		{
			InWater = true,
			Moving = true,
			Exercising = true,
			Climbing = true,
		};

		var inputs = From(entity);

		Assert.True(Property<bool>(inputs, "InWater"));
		Assert.True(Property<bool>(inputs, "Moving"));
		Assert.True(Property<bool>(inputs, "Exercising"));
		Assert.True(Property<bool>(inputs, "Climbing"));
	}

	[Fact]
	public void From_ANullEntity_IsTheNoInputDefault()
	{
		var inputs = From(null);

		Assert.False(Property<bool>(inputs, "InWater"));
		Assert.False(Property<bool>(inputs, "Moving"));
		Assert.False(Property<bool>(inputs, "Exercising"));
		Assert.False(Property<bool>(inputs, "Climbing"));
	}

	[Fact]
	public void TheExercisingInput_HasTheGamesPublicGetterAndPrivateSetter()
	{
		// The scope writes the input through this setter: a game update that makes it read-only must
		// fail here (and at first use) rather than silently dropping the workout input.
		var property = GameAssemblyHost.Game.GetType("Body", throwOnError: true)!.GetProperty("exercising");

		Assert.NotNull(property);
		Assert.True(property!.GetGetMethod(nonPublic: false) is not null);
		Assert.True(property.GetSetMethod(nonPublic: true) is not null);
		Assert.Null(property.GetSetMethod(nonPublic: false));
	}

	private static bool IsMoving(float moveX)
	{
		var method = InputsType.GetMethod("IsMoving", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("HostVitalsInputs.IsMoving not found.");
		return (bool)method.Invoke(null, [moveX])!;
	}

	private static object From(PlayerEntity? entity)
	{
		var method = InputsType.GetMethod("From", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("HostVitalsInputs.From not found.");
		return method.Invoke(null, [entity])!;
	}

	private static T Property<T>(object inputs, string name) =>
		(T)(InputsType.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException($"HostVitalsInputs.{name} not found.")).GetValue(inputs)!;

	private static Type InputsType => GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.HostVitalsInputs", throwOnError: true)!;
}
