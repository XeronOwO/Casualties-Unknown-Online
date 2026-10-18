using System;
using System.Collections;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The pure rules of the host-clone vitals pass: the step cap, the fixed-step check, the pass
/// scope and the empty coroutine. The pass itself needs a live <c>Body</c>; its call site and the
/// order of its guards are pinned by <see cref="HostVitalsSuppressionPinTests"/>, and the real
/// proof is the deployed host with a remote player (recorded in the ticket).
/// </summary>
public class HostVitalsPassTests
{
	[Fact]
	public void StepWithinCap_AcceptsOnlyAPositiveFiniteStepUpToTheCap()
	{
		Assert.True(StepWithinCap(0.02f));
		Assert.True(StepWithinCap(0.25f));
		Assert.False(StepWithinCap(0.2501f));
		Assert.False(StepWithinCap(0f));
		Assert.False(StepWithinCap(-0.02f));
		Assert.False(StepWithinCap(float.NaN));
		Assert.False(StepWithinCap(float.PositiveInfinity));
	}

	[Fact]
	public void IsFixedStep_AcceptsTheReportedFixedStepAndRejectsAFrameDelta()
	{
		Assert.True(IsFixedStep(0.02f, 0.02f));
		Assert.True(IsFixedStep(0.0204f, 0.02f));
		Assert.False(IsFixedStep(0.03f, 0.02f));
		Assert.False(IsFixedStep(0.0167f, 0.02f)); // a 60 fps frame delta is not the fixed step
		Assert.False(IsFixedStep(float.NaN, 0.02f));
	}

	[Fact]
	public void TheScope_IsActiveOnlyWhileHeld_AndASecondDisposeIsHarmless()
	{
		Assert.False(IsActive());
		var scope = EnterScope();
		Assert.True(IsActive());

		scope.Dispose();
		Assert.False(IsActive());

		scope.Dispose();
		Assert.False(IsActive());
	}

	[Fact]
	public void EmptyRoutine_CompletesImmediately()
	{
		var routine = (IEnumerator)Invoke("EmptyRoutine")!;

		Assert.False(routine.MoveNext());
		Assert.Null(routine.Current);
	}

	private static bool StepWithinCap(float dt) => (bool)Invoke("StepWithinCap", dt)!;

	private static bool IsFixedStep(float dt, float fixedDt) => (bool)Invoke("IsFixedStep", dt, fixedDt)!;

	private static bool IsActive() => (bool)Type.GetProperty("IsActive", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(null)!;

	private static IDisposable EnterScope() => (IDisposable)Invoke("EnterScope")!;

	private static object? Invoke(string name, params object?[] args) =>
		Type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.Invoke(null, args);

	private static Type Type => GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.HostVitalsPass", throwOnError: true)!;
}
