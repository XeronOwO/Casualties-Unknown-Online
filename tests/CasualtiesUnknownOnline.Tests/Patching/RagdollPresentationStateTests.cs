using System;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Reflection locks for the ragdoll one-shot race fix: the render driver must
/// carry the collapse latch used by <c>SessionStatePump</c>, and the ragdoll
/// sync domain must expose the clone-creation queue flush.
/// </summary>
public class RagdollPresentationStateTests
{
	private static readonly Type Driver = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.RemoteBodyDriver",
		throwOnError: true)!;

	private static readonly Type RagdollSync = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.CharacterRagdollSync",
		throwOnError: true)!;

	[Fact]
	public void RemoteBodyDriver_HasRagdollCollapseLatchFields()
	{
		var pending = Driver.GetField("RagdollCollapsePending", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("RemoteBodyDriver.RagdollCollapsePending not found.");
		Assert.Equal(typeof(bool), pending.FieldType);

		var confirmed = Driver.GetField("RagdollCollapseConfirmed", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("RemoteBodyDriver.RagdollCollapseConfirmed not found.");
		Assert.Equal(typeof(bool), confirmed.FieldType);

		var ms = Driver.GetField("RagdollCollapseMs", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("RemoteBodyDriver.RagdollCollapseMs not found.");
		Assert.Equal(typeof(long), ms.FieldType);

		var poseActive = Driver.GetField("RagdollPoseActive", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("RemoteBodyDriver.RagdollPoseActive not found.");
		Assert.Equal(typeof(bool), poseActive.FieldType);
	}

	[Fact]
	public void RagdollSync_HasCloneCreationFlush_AndStillReportsTheOneShot()
	{
		var update = RagdollSync.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("CharacterRagdollSync.Update not found.");
		Assert.Equal(typeof(void), update.ReturnType);

		var report = RagdollSync.GetMethod("Report", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("CharacterRagdollSync.Report not found.");
		Assert.Equal(typeof(void), report.ReturnType);
	}

	[Fact]
	public void RagdollPoseGate_SuppressionWindowIsPositiveAndFinite()
	{
		Assert.True(RagdollPoseGate.SuppressWindowMs > 0);
		Assert.True(RagdollPoseGate.SuppressWindowMs < 5000);
	}
}
