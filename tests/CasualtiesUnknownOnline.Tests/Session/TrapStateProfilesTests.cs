using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Locks the live EntityEventKind → TrapPhase classification used by
/// <see cref="TrapStateRegistry"/>. A new trap kind must either be deliberately
/// classified as a state machine edge or deliberately left unclassified
/// (pure visual).
/// </summary>
public class TrapStateProfilesTests
{
	[Theory]
	[InlineData(EntityEventKind.MinePressed, TrapPhase.Warning)]
	[InlineData(EntityEventKind.CrystalUnstableTicked, TrapPhase.Warning)]
	[InlineData(EntityEventKind.MineExploded, TrapPhase.Triggered)]
	[InlineData(EntityEventKind.BearTrapClamped, TrapPhase.Triggered)]
	[InlineData(EntityEventKind.TurretSelfDestructed, TrapPhase.Disabled)]
	[InlineData(EntityEventKind.CrystalUnstableExploded, TrapPhase.Disabled)]
	[InlineData(EntityEventKind.BearTrapReleased, TrapPhase.Armed)]
	public void StatefulKinds_MapToExpectedPhase(EntityEventKind kind, TrapPhase expected) =>
		Assert.Equal(expected, TrapStateProfiles.Map(kind));

	[Theory]
	[InlineData(EntityEventKind.BarbedFenceHit)]
	[InlineData(EntityEventKind.CoilShocked)]
	[InlineData(EntityEventKind.CactusHit)]
	[InlineData(EntityEventKind.JumpPadLaunched)]
	[InlineData(EntityEventKind.BananaPlantSlip)]
	[InlineData(EntityEventKind.GrabberGrabbed)]
	[InlineData(EntityEventKind.CrystalElectricShocked)]
	[InlineData(EntityEventKind.CrystalTeleportTriggered)]
	public void VisualOnlyKinds_RemainUnclassified(EntityEventKind kind) =>
		Assert.Null(TrapStateProfiles.Map(kind));
}
