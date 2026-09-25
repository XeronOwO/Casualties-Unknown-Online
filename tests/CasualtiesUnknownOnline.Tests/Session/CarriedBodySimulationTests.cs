using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// A carried rider's own body keeps the game's own per-frame simulation. The
/// reported flat ECG and twitching limbs came from the carry presentation
/// skipping that simulation on the rider's own client, so the rule that decides
/// who simulates is pinned here: a remote clone never simulates, a
/// conscious/alive rider always does, and a dead/unconscious carried body keeps
/// the pinned-ragdoll presentation.
/// </summary>
public class CarriedBodySimulationTests
{
	[Fact]
	public void ConsciousCarriedRider_KeepsItsNativeSimulation()
	{
		Assert.True(
			CarriedBodySimulation.KeepsNativeSimulation(isLocalCarriedBody: true, alive: true, conscious: true),
			"a conscious/alive carried rider must keep the native per-frame simulation that advances vitals and the ECG animation");
	}

	[Fact]
	public void ConsciousCarriedRider_IsNotTreatedAsARenderProxy()
	{
		Assert.False(
			CarriedBodySimulation.SkipsNativeSimulation(isRemoteClone: false, isLocalCarriedBody: true, alive: true, conscious: true),
			"the render-proxy skip must not apply to a local carried rider");
	}

	[Fact]
	public void RemoteClone_SkipsItsNativeSimulation()
	{
		Assert.True(
			CarriedBodySimulation.SkipsNativeSimulation(isRemoteClone: true, isLocalCarriedBody: false, alive: true, conscious: true),
			"a remote clone is a presentation proxy and never runs the game's own simulation");
	}

	[Fact]
	public void UnconsciousCarriedBody_KeepsThePinnedRagdollPresentation()
	{
		Assert.False(
			CarriedBodySimulation.KeepsNativeSimulation(isLocalCarriedBody: true, alive: true, conscious: false),
			"a comatose carried body is a pinned ragdoll, not a standing rider");
		Assert.True(
			CarriedBodySimulation.SkipsNativeSimulation(isRemoteClone: false, isLocalCarriedBody: true, alive: true, conscious: false),
			"the pinned-ragdoll carried body keeps the frozen presentation");
	}

	[Fact]
	public void DeadCarriedBody_KeepsThePinnedRagdollPresentation()
	{
		Assert.False(
			CarriedBodySimulation.KeepsNativeSimulation(isLocalCarriedBody: true, alive: false, conscious: false),
			"a corpse cannot hold the standing pose the carry presentation assumes");
	}

	[Fact]
	public void CarriedRider_MovementInputIsSuppressed()
	{
		Assert.True(
			CarriedBodySimulation.SuppressesMovement(isLocalCarriedBody: true, alive: true, conscious: true),
			"the carrier drives the position; the rider's own movement input must not fight it");
	}

	[Fact]
	public void CarriedRider_GroundContactBelongsToTheCarrier()
	{
		Assert.True(
			CarriedBodySimulation.CarrierOwnsGroundContact(isLocalCarriedBody: true, alive: true, conscious: true),
			"a carried body does not touch the terrain: the native ground pass (surface effects and the low-health-block damage query) stays with the carrier");
	}

	[Fact]
	public void PinnedCarriedBody_KeepsItsNativeGroundPass()
	{
		Assert.False(
			CarriedBodySimulation.CarrierOwnsGroundContact(isLocalCarriedBody: true, alive: true, conscious: false),
			"a pinned-ragdoll carried body still skips its whole simulation, so it never reaches the ground pass either way");
	}

	[Fact]
	public void OrdinaryLocalBody_OwnsItsOwnGroundContact()
	{
		Assert.False(
			CarriedBodySimulation.CarrierOwnsGroundContact(isLocalCarriedBody: false, alive: true, conscious: true),
			"an ordinary local body keeps the game's own ground pass");
	}

	[Fact]
	public void OrdinaryLocalBody_KeepsSimulationAndMovement()
	{
		Assert.False(
			CarriedBodySimulation.SkipsNativeSimulation(isRemoteClone: false, isLocalCarriedBody: false, alive: true, conscious: true),
			"an ordinary local body keeps its native simulation");
		Assert.False(
			CarriedBodySimulation.SuppressesMovement(isLocalCarriedBody: false, alive: true, conscious: true),
			"an ordinary local body keeps its movement input");
	}
}
