using System;
using System.IO;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// A carried rider clone's exact limb poses are measured against the body root
/// the ride pose pinned, and nothing repositions them: the shipped hierarchy
/// carries a clone's limbs with its root (they are transform children and the
/// clone's limb rigidbodies are frozen), and the carried LOCAL rider — root
/// teleported every frame with every rigidbody frozen, nothing re-anchoring it —
/// is the control case that shows a body does not come apart that way. The
/// reference shape and the read-only measurement are pinned here so a real
/// session can settle the ticket's "limbs left behind" hypothesis with a number.
/// </summary>
public class CarriedLimbAnchorTests
{
	[Fact]
	public void CarriedRiderRenderingExactLimbPoses_IsMeasuredAgainstThePinnedRoot() =>
		Assert.True(
			CarriedLimbAnchor.ShouldMeasureAgainstPinnedRoot(isCarriedRider: true, hasExactLimbPose: true),
			"a carried rider clone whose exact limb poses are rendered is the one combination the root/limb question is about");

	[Fact]
	public void NonCarriedClone_IsNotMeasured() =>
		Assert.False(
			CarriedLimbAnchor.ShouldMeasureAgainstPinnedRoot(isCarriedRider: false, hasExactLimbPose: true),
			"an ordinary ragdoll clone takes root and limbs from the same snapshot, so there is no pinned root to measure against");

	[Fact]
	public void CarriedRiderWithoutExactPoses_IsNotMeasured() =>
		Assert.False(
			CarriedLimbAnchor.ShouldMeasureAgainstPinnedRoot(isCarriedRider: true, hasExactLimbPose: false),
			"without exact poses the animator drives the limbs from the root, so there is no world-space pose to compare");

	[Fact]
	public void CapturedShape_TargetsTheRootRelativePosition()
	{
		var anchor = new CarriedLimbAnchor();
		anchor.Capture(limbIndex: 3, limbX: 11f, limbY: 6f, rootX: 10f, rootY: 5f);

		Assert.True(anchor.TryTarget(order: 0, rootX: 13f, rootY: 4f, out var index, out var x, out var y));
		Assert.Equal(3, index);
		Assert.True(Math.Abs(x - 14f) < 0.0001f, "the reference keeps the limb's offset from the root: 11 + (13 - 10)");
		Assert.True(Math.Abs(y - 5f) < 0.0001f, "the reference keeps the limb's offset from the root: 6 + (4 - 5)");
	}

	[Fact]
	public void NestedLimbs_KeepTheirParentsFirstCaptureOrder()
	{
		var anchor = new CarriedLimbAnchor();
		anchor.Capture(limbIndex: 2, limbX: 10f, limbY: 5f, rootX: 0f, rootY: 0f);
		anchor.Capture(limbIndex: 3, limbX: 11f, limbY: 4f, rootX: 0f, rootY: 0f);

		Assert.True(anchor.TryTarget(order: 0, rootX: 3f, rootY: 0f, out var parent, out var parentX, out var parentY));
		Assert.True(anchor.TryTarget(order: 1, rootX: 3f, rootY: 0f, out var child, out var childX, out var childY));
		Assert.Equal(2, parent);
		Assert.Equal(3, child);
		Assert.True(Math.Abs(parentX - 13f) < 0.0001f && Math.Abs(parentY - 5f) < 0.0001f, "the parent limb is compared first");
		Assert.True(Math.Abs(childX - 14f) < 0.0001f && Math.Abs(childY - 4f) < 0.0001f, "the nested child is compared after the parent whose move it inherits");
	}

	[Fact]
	public void RepeatingPoseTicks_ReplaceTheCapturedShape()
	{
		var anchor = new CarriedLimbAnchor();
		anchor.Capture(limbIndex: 1, limbX: 1f, limbY: 1f, rootX: 0f, rootY: 0f);
		anchor.Clear();
		anchor.Capture(limbIndex: 4, limbX: 6f, limbY: 7f, rootX: 5f, rootY: 5f);

		Assert.Equal(1, anchor.Count);
		Assert.True(anchor.TryTarget(order: 0, rootX: 5f, rootY: 5f, out var index, out var x, out var y));
		Assert.Equal(4, index);
		Assert.True(Math.Abs(x - 6f) < 0.0001f && Math.Abs(y - 7f) < 0.0001f, "the second tick replaces the first tick's limb instead of appending to it");
	}

	[Fact]
	public void ClearedAnchor_HasNoReferenceShape()
	{
		var anchor = new CarriedLimbAnchor();
		anchor.Capture(limbIndex: 1, limbX: 1f, limbY: 1f, rootX: 0f, rootY: 0f);
		anchor.Clear();

		Assert.True(anchor.IsEmpty);
		Assert.Equal(0, anchor.Count);
		Assert.False(anchor.TryTarget(order: 0, rootX: 5f, rootY: 5f, out _, out _, out _));
	}

	[Fact]
	public void OrderOutsideTheCapturedShape_IsRefused()
	{
		var anchor = new CarriedLimbAnchor();
		anchor.Capture(limbIndex: 1, limbX: 1f, limbY: 1f, rootX: 0f, rootY: 0f);

		Assert.False(anchor.TryTarget(order: -1, rootX: 0f, rootY: 0f, out _, out _, out _));
		Assert.False(anchor.TryTarget(order: 1, rootX: 0f, rootY: 0f, out _, out _, out _));
	}

	[Fact]
	public void RidePose_MeasuresAfterWritingTheRoot()
	{
		// The pure matrix above cannot catch a missing or misplaced call site,
		// and the reading is only meaningful at the one point in the frame where
		// the ride pose has just written the root: the measurement must come
		// AFTER the carrier follow, never before it.
		var placement = ReadSource("CarriedBodyPlacement.cs");
		var follow = placement.IndexOf("ApplyCarrierFollow(body, carrierPosition", StringComparison.Ordinal);
		var measure = placement.IndexOf("RagdollPoseApplication.MeasurePinnedRootSeparation(body);", StringComparison.Ordinal);
		Assert.True(follow >= 0, "CarriedBodyPlacement.ApplyRidePose must place the root through ApplyCarrierFollow");
		Assert.True(measure > follow, "the limb measurement must run after the ride pose wrote the root, not before it");
	}

	[Fact]
	public void PinnedRootMeasurement_DoesNotMoveAnything()
	{
		// This is the property that keeps the probe honest: the shipped code
		// measures, it does not place. A fix may only be added once a session has
		// produced a non-zero reading, and turning this probe into that fix has to
		// be a deliberate change — this test is what makes it deliberate.
		var application = ReadSource("RagdollPoseApplication.cs");
		var start = application.IndexOf("internal static void MeasurePinnedRootSeparation", StringComparison.Ordinal);
		Assert.True(start >= 0, "RagdollPoseApplication.MeasurePinnedRootSeparation must exist");
		var end = application.IndexOf("private static int LimbDepth", start, StringComparison.Ordinal);
		Assert.True(end > start, "the measurement method must stay inside the class, before LimbDepth");
		var measurement = application.Substring(start, end - start);
		Assert.DoesNotContain("transform.position =", measurement);
		Assert.DoesNotContain("rb.position =", measurement);
	}

	[Fact]
	public void PoseApplication_CapturesAndClearsTheReferenceShape()
	{
		var application = ReadSource("RagdollPoseApplication.cs");
		Assert.Contains("driver.LimbAnchor.Capture(", application);
		Assert.Contains("driver.LimbAnchor.Clear();", application);
	}

	private static string ReadSource(string fileName) =>
		File.ReadAllText(Path.Combine(
			FindRepositoryRoot(),
			"src",
			"CasualtiesUnknownOnline.GameAdapter",
			"Character",
			fileName));

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
