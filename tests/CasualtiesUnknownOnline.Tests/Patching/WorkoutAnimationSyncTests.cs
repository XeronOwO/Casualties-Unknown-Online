using System;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Protocol.Wire;
using Xunit;
using System.Collections;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The workout/exercise presentation surface. The owner's Body.DoWorkout plays
/// one of three exercise clip sets (Body.cs:368-435); this test locks the pure
/// clip mapping, the local tracker/patch shape, the wire field and the clone
/// driver state so a game update cannot silently drop the visual.
/// </summary>
public class WorkoutAnimationSyncTests
{
	private static readonly Type Presentation = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.WorkoutPresentation",
		throwOnError: true)!;

	private static readonly Type Patch = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.BodyWorkoutPatch",
		throwOnError: true)!;

	private static readonly Type Tracker = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.LocalWorkoutTracker",
		throwOnError: true)!;

	private static readonly Type Driver = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.RemoteBodyDriver",
		throwOnError: true)!;

	private static string? BodyClip(byte workoutType)
	{
		var method = Presentation.GetMethod("BodyClip", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("WorkoutPresentation.BodyClip not found.");
		return (string?)method.Invoke(null, [workoutType]);
	}

	private static string? ArmsClip(byte workoutType)
	{
		var method = Presentation.GetMethod("ArmsClip", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("WorkoutPresentation.ArmsClip not found.");
		return (string?)method.Invoke(null, [workoutType]);
	}

	private static bool IsWorkout(byte workoutType)
	{
		var method = Presentation.GetMethod("IsWorkout", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("WorkoutPresentation.IsWorkout not found.");
		return (bool)method.Invoke(null, [workoutType])!;
	}

	private static byte FromGameValue(byte gameWorkoutType)
	{
		var method = Presentation.GetMethod("FromGameValue", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("WorkoutPresentation.FromGameValue not found.");
		return (byte)method.Invoke(null, [gameWorkoutType])!;
	}

	[Fact]
	public void ClipMapping_MatchesTheGameDoWorkoutSwitch()
	{
		Assert.Equal("ExperimentPushups", BodyClip(1));
		Assert.Equal("ArmsPushups", ArmsClip(1));
		Assert.Equal("ExperimentSquats", BodyClip(2));
		Assert.Equal("ArmsSquats", ArmsClip(2));
		Assert.Equal("ExperimentPlank", BodyClip(3));
		Assert.Equal("ArmsPlank", ArmsClip(3));
	}

	[Fact]
	public void ClipMapping_UnknownTypeReturnsNull_AndIsWorkoutFalse()
	{
		Assert.Null(BodyClip(0));
		Assert.Null(ArmsClip(0));
		Assert.False(IsWorkout(0));
		Assert.False(IsWorkout(99));
		Assert.True(IsWorkout(1));
		Assert.True(IsWorkout(2));
		Assert.True(IsWorkout(3));
	}

	[Fact]
	public void FromGameValue_MapsZeroBasedGameEnumToPositiveWireCodes()
	{
		// Body.WorkoutType is declaration-ordered: Pushups=0, Squats=1, Plank=2.
		// Wire 0 must stay reserved for "not exercising".
		Assert.Equal(1, FromGameValue(0));
		Assert.Equal(2, FromGameValue(1));
		Assert.Equal(3, FromGameValue(2));
		Assert.Equal(0, FromGameValue(99));
	}

	[Fact]
	public void Prefix_TargetsDoWorkoutWithInstanceAndType()
	{
		var prefix = Patch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("BodyWorkoutPatch.Prefix not found.");
		Assert.Equal(typeof(void), prefix.ReturnType);
		var parameters = prefix.GetParameters();
		Assert.True(parameters.Length == 2
			&& parameters[0].Name == "__instance"
			&& parameters[0].ParameterType.FullName == "Body"
			&& parameters[1].Name == "type"
			&& parameters[1].ParameterType.FullName == "Body+WorkoutType",
			$"Prefix must be (Body __instance, Body.WorkoutType type), got {parameters.Length} parameter(s)");
	}

	[Fact]
	public void LocalTracker_IsASmallStatefulMonoBehaviour()
	{
		Assert.Equal("UnityEngine.MonoBehaviour", Tracker.BaseType?.FullName);
		var field = Tracker.GetField("WorkoutType", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("LocalWorkoutTracker.WorkoutType not found.");
		Assert.Equal(typeof(byte), field.FieldType);
	}

	[Fact]
	public void RemoteDriver_HasWorkoutTransitionField()
	{
		var field = Driver.GetField("PrevWorkoutType", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("RemoteBodyDriver.PrevWorkoutType not found.");
		Assert.Equal(typeof(byte), field.FieldType);
	}

	[Fact]
	public void EntityStateMsg_HasWorkoutTypeOnTheWire()
	{
		var property = typeof(WirePlayerStreamState).GetProperty("WorkoutType", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("WirePlayerStreamState.WorkoutType not found.");
		Assert.Equal(typeof(byte), property.PropertyType);
	}

	[Fact]
	public void PatchInventory_DeclaresBodyDoWorkout()
	{
		var inventory = GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.Patches.PatchInventory")
			?? throw new InvalidOperationException("PatchInventory type not found.");
		var build = inventory.GetMethod("BuildContracts", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PatchInventory.BuildContracts not found.");
		var contracts = (IEnumerable)build.Invoke(null, null)!;
		var found = contracts.Cast<object>().Any(c =>
		{
			var type = c.GetType();
			var target = type.GetProperty("TargetType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c) as string;
			var method = type.GetProperty("MethodName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c) as string;
			return target == "Body" && method == "DoWorkout";
		});
		Assert.True(found, "PatchInventory must declare the Body.DoWorkout patch contract.");
	}
}
