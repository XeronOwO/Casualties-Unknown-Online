using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The carried rider's back offset, on the built adapter (pure vector math), plus
/// the wiring pins for what the placement READS. The offset's height is a pose
/// quantity, not a flag: the carrier's crouch flag flips inside one frame while
/// its <c>crouchAmount</c> eases (Body.cs:3095-3099, the clone side eased to match
/// in <c>BodyUpdatePatch.UpdateCrouchAmount</c>), so an offset read from the flag
/// moved the rider the full 0.4 world units in a single frame every time the
/// carrier crouched or stood — an instant displacement of the rider, in the family
/// the rider-teleport ticket is about. The matrix below pins the height to the
/// eased pose and keeps it continuous across the range; the pins keep every
/// clone-backed view reading the pose instead of the flag.
/// </summary>
[Trait("Category", "Integration")]
public class CarriedRiderBackOffsetTests
{
	private const float Tolerance = 0.0001f;

	private static readonly Type Placement = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.CarriedBodyPlacement",
		throwOnError: true)!;

	private static readonly Type Vector3Type =
		Type.GetType("UnityEngine.Vector3, UnityEngine.CoreModule")
		?? throw new InvalidOperationException("UnityEngine.Vector3 type not found.");

	private static readonly MethodInfo BackOffset = Placement.GetMethod(
		"BackOffset",
		BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
		?? throw new InvalidOperationException("CarriedBodyPlacement.BackOffset not found.");

	[Fact]
	public void BackOffset_TakesTheCrouchPose_NotTheCrouchFlag()
	{
		// The signature is the fix: a bool here is what stepped the rider 0.4
		// world units in one frame whenever the carrier crouched or stood.
		var parameters = BackOffset.GetParameters();
		Assert.Equal(3, parameters.Length);
		Assert.Equal("Single", parameters[2].ParameterType.Name);
	}

	[Theory]
	[InlineData(false, 0f, 0.9f)]
	[InlineData(false, 0.25f, 0.8f)]
	[InlineData(false, 0.5f, 0.7f)]
	[InlineData(false, 1f, 0.5f)]
	[InlineData(true, 0f, 0.9f)]
	[InlineData(false, -1f, 0.9f)]
	[InlineData(false, 2f, 0.5f)]
	public void Height_FollowsTheCarrierCrouchPose(bool carrierIsRight, float crouchAmount, float expectedHeight)
	{
		var height = Component(Invoke(carrierIsRight, crouchAmount), "y");
		Assert.True(
			Math.Abs(height - expectedHeight) < Tolerance,
			$"rider height at crouchAmount={crouchAmount} was {height}, expected {expectedHeight}");
	}

	[Fact]
	public void Height_IsContinuousInTheCrouchPose()
	{
		// The defect this pins: the crouch FLAG moved the rider the whole 0.4
		// units in one frame. A two-percent step of the crouch POSE may only move
		// it by a fraction of that, so the rider tracks the carrier's own eased
		// crouch instead of stepping under it.
		var before = Component(Invoke(false, 0.49f), "y");
		var after = Component(Invoke(false, 0.51f), "y");
		Assert.True(
			Math.Abs(after - before) < 0.02f,
			$"a 2% crouch-pose step moved the rider {Math.Abs(after - before)} world units");
	}

	[Fact]
	public void Height_FallsBackToUprightWhenThePoseIsNotSet()
	{
		// A body whose pose is not set yet must not write NaN into the rider's
		// transform: a NaN position makes the rider vanish where the game renders it.
		var height = Component(Invoke(false, float.NaN), "y");
		Assert.True(!float.IsNaN(height), "the rider's height must never be NaN");
		Assert.True(
			Math.Abs(height - 0.9f) < Tolerance,
			"an unset crouch pose must fall back to the upright height");
	}

	[Theory]
	[InlineData(false, 0.35f)]
	[InlineData(true, -0.35f)]
	public void Side_FollowsTheCarrierFacing(bool carrierIsRight, float expectedX)
	{
		var x = Component(Invoke(carrierIsRight, 0f), "x");
		Assert.True(Math.Abs(x - expectedX) < Tolerance, $"rider side was {x}, expected {expectedX}");
	}

	[Fact]
	public void EveryCloneBackedView_PlacesTheRiderFromTheCrouchPose()
	{
		// Two of the three views resolve their anchor from a game Body (the local
		// carrier and the carrier's render clone), so both read the eased pose; the
		// third — the rider's own client before the carrier's clone exists — has the
		// entity buffer's flag only, and is the one place the flag is still honest.
		// The assertions are LINE-anchored: a WRAPPER around the value (`1f - amount`,
		// a rounding of its own) reproduces a step or a sink while an unanchored
		// substring check stays green, so the value's own line is what has to carry it.
		var presenter = ReadSource(Path.Combine("Character", "CarriedRiderPresenter.cs"));
		Assert.Contains("\n\t\t\t\t\tlocalBody.crouchAmount,\n", presenter);
		Assert.Contains("\n\t\t\t\t\tcarrierClone.crouchAmount,\n", presenter);
		Assert.DoesNotContain("crouching ?", presenter);

		var riderOwnView = ReadSource("PlayerInteractionApply.cs");
		Assert.Contains("\n\t\t\t\tcarrierBody.crouchAmount,\n", riderOwnView);
		// Exactly one flag-derived amount survives in the family — the pre-clone
		// fallback — and this states that rather than the expression it happens to
		// use today.
		Assert.Equal(1, Occurrences(riderOwnView, "Crouching ? "));

		var placement = ReadSource(Path.Combine("Character", "CarriedBodyPlacement.cs"));
		Assert.Contains(
			"body.transform.position = BackOffset(carrierPosition, carrierIsRight, carrierCrouchAmount);",
			placement);
	}

	[Fact]
	public void ACloneIsSeededWithTheOwnersCrouchPose()
	{
		// Independent review F1: a fresh clone seeded crouchAmount = 0 put a crouching
		// owner's rider 0.4 world units too high until the clone's local easing caught
		// up — the same one-frame step this rule removes, and one the riderDrift
		// reading cannot see, because a pin re-derives its offset after every
		// placement. The clone therefore starts at the owner's reported pose, while
		// the template's stale value still gets replaced rather than kept.
		var factory = ReadSource(Path.Combine("Character", "RemoteBodyFactory.cs"));
		Assert.Contains("body.crouchAmount = remote.Crouching ? 1f : 0f;", factory);
		Assert.DoesNotContain("body.crouchAmount = 0f;", factory);
	}

	private static object Invoke(bool carrierIsRight, float crouchAmount) =>
		BackOffset.Invoke(null, [NewVector3(0f, 0f, 0f), carrierIsRight, crouchAmount])!;

	private static object NewVector3(float x, float y, float z) =>
		Activator.CreateInstance(Vector3Type, x, y, z)!;

	private static float Component(object vector, string component)
	{
		var field = Vector3Type.GetField(component)
			?? throw new InvalidOperationException($"UnityEngine.Vector3.{component} not found.");
		return (float)field.GetValue(vector)!;
	}

	// The pinned sources are CRLF on disk; normalising here keeps the line anchors
	// above about the code rather than about the checkout's line endings.
	private static string ReadSource(string relativePath) =>
		File.ReadAllText(Path.Combine(
			FindRepositoryRoot(),
			"src",
			"CasualtiesUnknownOnline.GameAdapter",
			relativePath)).Replace("\r\n", "\n");

	private static int Occurrences(string text, string token)
	{
		var count = 0;
		for (var index = text.IndexOf(token, StringComparison.Ordinal);
			index >= 0;
			index = text.IndexOf(token, index + token.Length, StringComparison.Ordinal))
		{
			count++;
		}

		return count;
	}

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CasualtiesUnknownOnline.slnx")))
		{
			directory = directory.Parent;
		}

		if (directory is null)
		{
			throw new InvalidOperationException("could not locate repository root (CasualtiesUnknownOnline.slnx)");
		}

		return directory.FullName;
	}
}
