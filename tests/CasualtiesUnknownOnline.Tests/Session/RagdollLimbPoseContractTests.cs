using System;
using System.Reflection;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Contract gate for the exact ragdoll limb-pose sync path: the runtime entity
/// and the high-frequency wire state must both carry per-limb pose facts before
/// the Game Adapter can reproduce the owner's ragdoll on a frozen clone.
/// </summary>
public class RagdollLimbPoseContractTests
{
	[Fact]
	public void PlayerEntity_ExposesLimbPoses()
	{
		var property = typeof(PlayerEntity).GetProperty("LimbPoses", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("PlayerEntity.LimbPoses not found; exact ragdoll limb pose sync is missing.");
		Assert.NotNull(property.PropertyType);
	}

	[Fact]
	public void WirePlayerStreamState_ExposesLimbPoses()
	{
		var property = typeof(WirePlayerStreamState).GetProperty("LimbPoses", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("WirePlayerStreamState.LimbPoses not found; exact ragdoll limb pose sync is missing on the wire.");
		Assert.NotNull(property.PropertyType);
	}

	[Fact]
	public void PlayerLimbPose_UsesWorldSpacePosition()
	{
		var world = typeof(PlayerLimbPose).GetProperty("WorldPosition", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("PlayerLimbPose.WorldPosition not found; ragdoll limb pose sync must carry world-space positions.");
		Assert.NotNull(world.PropertyType);

		var local = typeof(PlayerLimbPose).GetProperty("LocalPosition", BindingFlags.Instance | BindingFlags.Public);
		Assert.Null(local);
	}

	[Fact]
	public void WirePlayerLimbPose_UsesWorldSpacePosition()
	{
		var world = typeof(WirePlayerLimbPose).GetProperty("WorldPosition", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("WirePlayerLimbPose.WorldPosition not found; wire ragdoll limb pose sync must carry world-space positions.");
		Assert.NotNull(world.PropertyType);

		var local = typeof(WirePlayerLimbPose).GetProperty("LocalPosition", BindingFlags.Instance | BindingFlags.Public);
		Assert.Null(local);
	}
}
