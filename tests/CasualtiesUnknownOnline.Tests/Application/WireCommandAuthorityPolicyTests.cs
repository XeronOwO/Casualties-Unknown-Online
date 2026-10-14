using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.Protocol.Wire;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Application;

/// <summary>
/// The per-kind authority a member's wire submission carries
/// (<c>WireCommandAuthorityPolicy</c>) — the half the admission seam reads, and
/// the half that used to carry no member-versus-host information at all.
///
/// <para>
/// The table below IS the census: every value of <see cref="WireCommandKind"/>
/// must appear in it, so a kind added later cannot inherit a policy nobody chose
/// for it. The mapper actually stamping the declaration, and the seam's verdict
/// on either side of it, are pinned next door in <c>KernelCommandGatewayTests</c>
/// and <c>CommandAdmissionIntegrationTests</c>.
/// </para>
/// </summary>
public class WireCommandAuthorityPolicyTests
{
	/// <summary>Every mapped wire kind, with the authority a member's submission of it declares. The three groups are the policy's own: member-authored owner-predicted, member-authored trigger-observed, and host-only.</summary>
	private static readonly Dictionary<WireCommandKind, AuthorityKind> Expected = new()
	{
		[WireCommandKind.ItemPickup] = AuthorityKind.OwnerPredictedHostValidated,
		[WireCommandKind.ItemDrop] = AuthorityKind.OwnerPredictedHostValidated,
		[WireCommandKind.ItemDestroy] = AuthorityKind.OwnerPredictedHostValidated,
		[WireCommandKind.ItemUpdateState] = AuthorityKind.OwnerPredictedHostValidated,
		[WireCommandKind.ItemContainerSync] = AuthorityKind.OwnerPredictedHostValidated,
		[WireCommandKind.ItemApplyLimb] = AuthorityKind.OwnerPredictedHostValidated,
		[WireCommandKind.ItemRemoveLimb] = AuthorityKind.OwnerPredictedHostValidated,
		[WireCommandKind.ItemSpawn] = AuthorityKind.TriggerObservedHostCommitted,
		[WireCommandKind.RecordEnemyBite] = AuthorityKind.TriggerObservedHostCommitted,
		[WireCommandKind.RecordEnemyLunge] = AuthorityKind.TriggerObservedHostCommitted,
		[WireCommandKind.RecordEnemyEffect] = AuthorityKind.TriggerObservedHostCommitted,
		[WireCommandKind.RunStart] = AuthorityKind.HostOnly,
		[WireCommandKind.AdvanceLayer] = AuthorityKind.HostOnly,
		[WireCommandKind.RecordTrapConsumed] = AuthorityKind.HostOnly,
		[WireCommandKind.RecordBuildingEntityHealth] = AuthorityKind.HostOnly,
		[WireCommandKind.RecordOpenedEntity] = AuthorityKind.HostOnly,
		[WireCommandKind.RecordTrapState] = AuthorityKind.HostOnly,
		[WireCommandKind.UpdatePlayerStatus] = AuthorityKind.HostOnly,
		[WireCommandKind.SetPlayerCarry] = AuthorityKind.HostOnly,
		[WireCommandKind.ClearPlayerCarry] = AuthorityKind.HostOnly,
		[WireCommandKind.UpsertEnemy] = AuthorityKind.HostOnly,
		[WireCommandKind.RemoveEnemy] = AuthorityKind.HostOnly,
		[WireCommandKind.UpdateFluidRegion] = AuthorityKind.HostOnly,
		[WireCommandKind.ItemTransfer] = AuthorityKind.HostOnly,
	};

	public static IEnumerable<object[]> DeclaredKinds =>
		Expected.Select(entry => new object[] { entry.Key, entry.Value });

	[Theory]
	[MemberData(nameof(DeclaredKinds))]
	public void Kind_DeclaresTheAuthorityAMemberSubmitsItUnder(WireCommandKind kind, AuthorityKind expected) =>
		Assert.Equal(expected, WireCommandAuthorityPolicy.Declared(kind));

	[Fact]
	public void EveryWireKindIsClassifiedOrDeliberatelyRefused()
	{
		// The two control kinds are not commands (a range request is answered by
		// the protocol service, a rejection is the host's own answer), so neither
		// is classified as a member's declaration. Every OTHER kind must be:
		// failing here is the reminder that a new wire kind decides this question
		// rather than inheriting the old blanket value.
		foreach (var kind in Enum.GetValues(typeof(WireCommandKind)).Cast<WireCommandKind>())
		{
			if (kind is WireCommandKind.RangeRequest or WireCommandKind.CommandRejected)
			{
				Assert.Throws<ArgumentOutOfRangeException>(() => WireCommandAuthorityPolicy.Declared(kind));
				continue;
			}

			Assert.True(Expected.ContainsKey(kind),
				$"{kind} is a wire command kind with no declared member authority — add it to the policy's table and to this census");
		}
	}

	[Fact]
	public void TheDeclarationIsNotTheOldBlanketValue()
	{
		// The defect this policy removes: every kind read one value, so the seam
		// could not tell a member's RunStart from a member's item use. Both sides
		// must be present for that distinction to exist at all.
		Assert.Contains(AuthorityKind.HostOnly, Expected.Values);
		Assert.Contains(AuthorityKind.OwnerPredictedHostValidated, Expected.Values);
	}
}
