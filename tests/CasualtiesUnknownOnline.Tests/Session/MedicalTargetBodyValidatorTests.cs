using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.PlayerInteractionTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The target half's own rules, unit-level: one case per family's refusal, because these reason
/// strings travel to the operator and the seam that moved them must not have reworded any. No
/// session and no nodes — the validator is a pure function of one character snapshot, which is
/// exactly why the client that owns the body can run it.
/// </summary>
public class MedicalTargetBodyValidatorTests
{
	private const ulong Owner = 2001;

	[Fact]
	public void AnInjectionNeedsAResponsiveTargetAndNamesNoLimb()
	{
		Assert.False(Validate(Snapshot(Owner, conscious: false), MedicalOperationKind.Injection, -1, out var reason));
		Assert.Equal("Target is not conscious/alive.", reason);

		// An injection is not limb-scoped, so no limb index is required of it.
		Assert.True(Validate(Snapshot(Owner, conscious: true), MedicalOperationKind.Injection, -1, out _));
	}

	[Fact]
	public void ShrapnelNeedsAFragmentAndReportsTheCountItAccepted()
	{
		Assert.False(Validate(Limb(1), MedicalOperationKind.Shrapnel, 1, out var emptyReason));
		Assert.Equal("Target limb has no shrapnel.", emptyReason);

		var asleep = Limb(1, shrapnel: 3);
		asleep.Health!.Conscious = false;
		Assert.False(Validate(asleep, MedicalOperationKind.Shrapnel, 1, out var asleepReason));
		Assert.Equal("Target is not conscious/alive.", asleepReason);

		Assert.True(ValidateCount(Limb(1, shrapnel: 3), MedicalOperationKind.Shrapnel, 1, out var count, out _));
		Assert.Equal(3, count);
	}

	[Fact]
	public void BandageRefusesADismemberedLimb()
	{
		Assert.False(Validate(Limb(1, dismembered: true), MedicalOperationKind.Bandage, 1, out var reason));
		Assert.Equal("Target limb is dismembered.", reason);
		Assert.True(Validate(Limb(1), MedicalOperationKind.Bandage, 1, out _));
	}

	[Fact]
	public void RemovalsNeedTheComponentTheyRemove()
	{
		Assert.False(Validate(Limb(1), MedicalOperationKind.SplintRemoval, 1, out var noSplint));
		Assert.Equal("Target limb has no splint.", noSplint);
		Assert.True(Validate(Limb(1, component: "SplintLimb"), MedicalOperationKind.SplintRemoval, 1, out _));

		Assert.False(Validate(Limb(1), MedicalOperationKind.TourniquetRemoval, 1, out var noTourniquet));
		Assert.Equal("Target limb has no tourniquet.", noTourniquet);
		Assert.True(Validate(Limb(1, component: "TourniquetScript"), MedicalOperationKind.TourniquetRemoval, 1, out _));
	}

	[Fact]
	public void DislocationNeedsADislocatedLimb()
	{
		Assert.False(Validate(Limb(1), MedicalOperationKind.Dislocation, 1, out var reason));
		Assert.Equal("Target limb is not dislocated.", reason);
		Assert.True(Validate(Limb(1, dislocated: true), MedicalOperationKind.Dislocation, 1, out _));
	}

	[Fact]
	public void AmputationNeedsAnInfectedLimbThatCanStillBeCut()
	{
		Assert.False(Validate(Limb(1, infection: 80f, dismembered: true), MedicalOperationKind.Amputation, 1, out var gone));
		Assert.Equal("Target limb cannot be amputated.", gone);

		Assert.False(Validate(Limb(1, infection: 10f), MedicalOperationKind.Amputation, 1, out var clean));
		Assert.Equal("Target limb is not infected enough for amputation.", clean);

		Assert.True(Validate(Limb(1, infection: 80f), MedicalOperationKind.Amputation, 1, out _));
	}

	[Fact]
	public void DefibrillationStillNeedsARealLimb()
	{
		foreach (var kind in new[] { MedicalOperationKind.Aed, MedicalOperationKind.ManualDefib })
		{
			Assert.False(Validate(Limb(1), kind, limbIndex: 7, out var reason));
			Assert.Equal("Target limb not found.", reason);
			Assert.True(Validate(Limb(1), kind, 1, out _));
		}
	}

	[Fact]
	public void ALimbScopedActionOnADeadTargetIsRefusedAsSuch()
	{
		var data = Limb(1);
		data.Health!.Alive = false;

		// Only the injection/fragment families require a RESPONSIVE target; the rest are what an
		// unresponsive one needs, so they ask only for a living body.
		Assert.False(Validate(data, MedicalOperationKind.Bandage, 1, out var reason));
		Assert.Equal("Target is not alive.", reason);
	}

	[Fact]
	public void AnUnknownKindIsRefusedRatherThanAssumed()
	{
		Assert.False(Validate(Limb(1), (MedicalOperationKind)99, 1, out var reason));
		Assert.Equal("Unsupported operation.", reason);
	}

	/// <summary>A snapshot whose limb 1 carries the requested state; limbs are the three the shared fixture builds.</summary>
	private static CharacterDataMsg Limb(
		int index,
		bool dislocated = false,
		bool dismembered = false,
		float infection = 0f,
		int shrapnel = 0,
		string? component = null)
	{
		var data = SnapshotWithLimbs(Owner, conscious: true);
		var limb = data.Limbs[index];
		limb.Dislocated = dislocated;
		limb.Dismembered = dismembered;
		limb.InfectionAmount = infection;
		limb.Shrapnel = shrapnel;
		if (component is not null)
		{
			limb.Components = [new ComponentStateMsg { TypeName = component }];
		}

		return data;
	}

	private static bool Validate(CharacterDataMsg data, MedicalOperationKind kind, int limbIndex, out string reason) =>
		MedicalTargetBodyValidator.TryValidate(data, kind, limbIndex, out _, out reason);

	private static bool ValidateCount(CharacterDataMsg data, MedicalOperationKind kind, int limbIndex, out int shrapnelCount, out string reason) =>
		MedicalTargetBodyValidator.TryValidate(data, kind, limbIndex, out shrapnelCount, out reason);
}
