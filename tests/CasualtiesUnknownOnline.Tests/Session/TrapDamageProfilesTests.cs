using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Locks the destructive trap-kind classification used by
/// <see cref="EntityEventSync"/> when folding the trap entity's zero health
/// into the atomic trap trigger batch. A new destructive trap kind must be
/// deliberately added here.
/// </summary>
public class TrapDamageProfilesTests
{
	[Theory]
	[InlineData(EntityEventKind.MineExploded)]
	[InlineData(EntityEventKind.TurretSelfDestructed)]
	[InlineData(EntityEventKind.CrystalFragileBroken)]
	[InlineData(EntityEventKind.CrystalUnstableExploded)]
	public void DestructiveKinds_AreClassified(EntityEventKind kind) =>
		Assert.True(TrapDamageProfiles.IsDestructive(kind));

	[Theory]
	[InlineData(EntityEventKind.MinePressed)]
	[InlineData(EntityEventKind.SpikeStabbed)]
	[InlineData(EntityEventKind.BearTrapClamped)]
	[InlineData(EntityEventKind.TurretFired)]
	[InlineData(EntityEventKind.CrystalMetamorphicTriggered)]
	[InlineData(EntityEventKind.CrystalMimicTriggered)]
	public void NonDestructiveKinds_RemainUnclassified(EntityEventKind kind) =>
		Assert.False(TrapDamageProfiles.IsDestructive(kind));
}
