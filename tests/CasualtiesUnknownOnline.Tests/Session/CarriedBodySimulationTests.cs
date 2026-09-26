using System;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// A carried body's own client keeps the part of the game's per-frame simulation
/// its pose can hold. The reported flat ECG and twitching limbs came from the
/// carry presentation skipping the whole simulation on the rider's own client,
/// so the rule that decides who simulates is pinned here: a remote clone never
/// simulates, a conscious/alive rider runs the whole native pass, and a dead or
/// unconscious carrier-posed body keeps the VITALS half of it — bleeding,
/// temperature, radiation, the periodic checks and limb wound/infection state —
/// under the pinned-ragdoll pose the carry relation owns.
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
			"the pinned-ragdoll carried body keeps the frozen pose instead of the whole native pass");
	}

	[Fact]
	public void DeadCarriedBody_KeepsThePinnedRagdollPresentation()
	{
		Assert.False(
			CarriedBodySimulation.KeepsNativeSimulation(isLocalCarriedBody: true, alive: false, conscious: false),
			"a corpse cannot hold the standing pose the carry presentation assumes");
	}

	[Fact]
	public void UnconsciousCarriedBody_RunsTheVitalsSubset()
	{
		Assert.True(
			CarriedBodySimulation.RunsVitalsSubset(isRemoteClone: false, isLocalCarriedBody: true, alive: true, conscious: false),
			"an unconscious carried body is still a body: bleeding, temperature, radiation, the periodic checks and limb wound/infection state must keep advancing on its own client while its pose stays the pinned ragdoll");
	}

	[Fact]
	public void DeadCarriedBody_RunsTheVitalsSubset()
	{
		Assert.True(
			CarriedBodySimulation.RunsVitalsSubset(isRemoteClone: false, isLocalCarriedBody: true, alive: false, conscious: false),
			"a carried corpse keeps the vitals half too — the game itself runs Body.Update and Limb.Update for a corpse, neither of which carries an alive guard");
	}

	[Fact]
	public void ConsciousCarriedRider_RunsTheWholePassAndNotTheSubset()
	{
		Assert.Equal(
			CarriedBodySimulation.Mode.Full,
			CarriedBodySimulation.Treatment(isRemoteClone: false, isLocalCarriedBody: true, alive: true, conscious: true));
		Assert.False(
			CarriedBodySimulation.RunsVitalsSubset(isRemoteClone: false, isLocalCarriedBody: true, alive: true, conscious: true),
			"the subset must not run on top of the whole pass — that would advance the same stages twice in one frame");
	}

	[Fact]
	public void RemoteClone_RunsNeitherThePassNorTheSubset()
	{
		Assert.Equal(
			CarriedBodySimulation.Mode.Proxy,
			CarriedBodySimulation.Treatment(isRemoteClone: true, isLocalCarriedBody: false, alive: true, conscious: true));
		Assert.False(
			CarriedBodySimulation.RunsVitalsSubset(isRemoteClone: true, isLocalCarriedBody: false, alive: true, conscious: true),
			"a clone's vitals are not simulated on this client at all");
		Assert.False(
			CarriedBodySimulation.RunsVitalsSubset(isRemoteClone: true, isLocalCarriedBody: true, alive: true, conscious: true),
			"a clone stays a proxy even if the carry flags say carried: the clone test comes first");
	}

	[Fact]
	public void OrdinaryLocalBody_RunsTheWholePass()
	{
		Assert.Equal(
			CarriedBodySimulation.Mode.Full,
			CarriedBodySimulation.Treatment(isRemoteClone: false, isLocalCarriedBody: false, alive: true, conscious: true));
		Assert.False(
			CarriedBodySimulation.RunsVitalsSubset(isRemoteClone: false, isLocalCarriedBody: false, alive: true, conscious: true),
			"an ordinary local body is not on the vitals-subset path");
	}

	[Fact]
	public void EveryLocalBody_RunsItsLimbsAndOnlyACloneSkips()
	{
		Assert.True(
			CarriedBodySimulation.RunsLimbSimulation(isRemoteClone: false),
			"every LOCAL body's limbs keep simulating wound/infection state — carried or not, corpse or not");
		Assert.False(
			CarriedBodySimulation.RunsLimbSimulation(isRemoteClone: true),
			"a remote clone's limb pass is presentation only; its vitals are not simulated here");
	}

	[Fact]
	public void Treatment_ClassifiesEveryCombinationAndAgreesWithItsDerivedViews()
	{
		// The whole input space, all 16 combinations of the four flags (2^4): the
		// derived predicates each client reads must be exactly the mode's view of
		// the same decision, so no patch can disagree with another about a body.
		(bool IsRemoteClone, bool IsLocalCarriedBody, bool Alive, bool Conscious, CarriedBodySimulation.Mode Expected)[] matrix =
		[
			(false, false, true, true, CarriedBodySimulation.Mode.Full),
			(false, false, true, false, CarriedBodySimulation.Mode.Full),
			(false, false, false, true, CarriedBodySimulation.Mode.Full),
			(false, false, false, false, CarriedBodySimulation.Mode.Full),
			(false, true, true, true, CarriedBodySimulation.Mode.Full),
			(false, true, true, false, CarriedBodySimulation.Mode.VitalsOnly),
			(false, true, false, true, CarriedBodySimulation.Mode.VitalsOnly),
			(false, true, false, false, CarriedBodySimulation.Mode.VitalsOnly),
			(true, false, true, true, CarriedBodySimulation.Mode.Proxy),
			(true, false, true, false, CarriedBodySimulation.Mode.Proxy),
			(true, false, false, true, CarriedBodySimulation.Mode.Proxy),
			(true, false, false, false, CarriedBodySimulation.Mode.Proxy),
			(true, true, true, true, CarriedBodySimulation.Mode.Proxy),
			(true, true, true, false, CarriedBodySimulation.Mode.Proxy),
			(true, true, false, true, CarriedBodySimulation.Mode.Proxy),
			(true, true, false, false, CarriedBodySimulation.Mode.Proxy),
		];
		Assert.Equal(16, matrix.Length);

		foreach (var row in matrix)
		{
			var where = $"clone={row.IsRemoteClone} carried={row.IsLocalCarriedBody} alive={row.Alive} conscious={row.Conscious}";
			var mode = CarriedBodySimulation.Treatment(row.IsRemoteClone, row.IsLocalCarriedBody, row.Alive, row.Conscious);
			Assert.True(mode == row.Expected, $"Treatment({where}) = {mode}, expected {row.Expected}");
			Assert.True(
				CarriedBodySimulation.SkipsNativeSimulation(row.IsRemoteClone, row.IsLocalCarriedBody, row.Alive, row.Conscious)
					== (mode != CarriedBodySimulation.Mode.Full),
				$"SkipsNativeSimulation must be the whole-pass view of Treatment({where})");
			Assert.True(
				CarriedBodySimulation.RunsVitalsSubset(row.IsRemoteClone, row.IsLocalCarriedBody, row.Alive, row.Conscious)
					== (mode == CarriedBodySimulation.Mode.VitalsOnly),
				$"RunsVitalsSubset must be the vitals view of Treatment({where})");
			Assert.True(
				CarriedBodySimulation.KeepsNativeSimulation(row.IsLocalCarriedBody, row.Alive, row.Conscious)
					== (row.IsLocalCarriedBody && row.Alive && row.Conscious),
				$"KeepsNativeSimulation must stay the carried-rider predicate for {where}");
			Assert.True(
				CarriedBodySimulation.SuppressesMovement(row.IsLocalCarriedBody, row.Alive, row.Conscious)
					== (row.IsLocalCarriedBody && row.Alive && row.Conscious),
				$"SuppressesMovement must be the carried-rider view of Treatment({where})");
			Assert.True(
				CarriedBodySimulation.CarrierOwnsGroundContact(row.IsLocalCarriedBody, row.Alive, row.Conscious)
					== (row.IsLocalCarriedBody && row.Alive && row.Conscious),
				$"CarrierOwnsGroundContact must be the carried-rider view of Treatment({where})");
			// The limb half does not read the mode (a limb pass carries no pose), so
			// its agreement with the mode is asserted rather than assumed.
			Assert.True(
				CarriedBodySimulation.RunsLimbSimulation(row.IsRemoteClone)
					== (mode != CarriedBodySimulation.Mode.Proxy),
				$"RunsLimbSimulation must agree with Treatment({where})");
		}
	}

	[Fact]
	public void Treatment_HasExactlyTheThreeClassifiedModes()
	{
		// A fourth treatment is a decision, not an implementation detail: adding
		// one without classifying it here must fail rather than silently fall
		// through the patches' default branch.
		var names = Enum.GetNames(typeof(CarriedBodySimulation.Mode));
		Assert.Equal(3, names.Length);
		Assert.Contains("Proxy", names);
		Assert.Contains("VitalsOnly", names);
		Assert.Contains("Full", names);
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
	public void PinnedCarriedBody_NeverReachesTheNativeGroundPass()
	{
		Assert.False(
			CarriedBodySimulation.CarrierOwnsGroundContact(isLocalCarriedBody: true, alive: true, conscious: false),
			"a pinned-ragdoll carried body skips the ground stage of its pass, so the carrier keeps the ground contact");
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
