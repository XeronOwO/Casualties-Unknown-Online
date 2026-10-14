using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Application;

/// <summary>
/// The admission seam's ITEM-REPORT eligibility family, in isolation: the one rule
/// that answers whether a member may speak for the item a report NAMES — a destroy
/// report, a state report and a container sync's parent — plus the container sync's
/// own second half (a listed child may not be an item another member carries or
/// wears).
///
/// <para>
/// Split from <c>KernelCommandGatewayTests</c> (the actor binding, the authority
/// declaration, the limb family and the audit line) because one class may carry at
/// most <c>TestClassSizeGateTests.MaxCasesPerClass</c> cases; the two share
/// <see cref="FakeItemFacts"/> and the seam's own constructor shape. The end-to-end
/// halves are in <c>CommandAdmissionIntegrationTests</c>, which reads the host's own
/// kernel rows.
/// </para>
/// </summary>
public class KernelItemReportEligibilityTests
{
	private const ulong Member = 2001;
	private const ulong OtherMember = 3001;

	[Fact]
	public void StateOfTheSendersOwnCarriedItem_IsAdmitted()
	{
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, Member);

		Assert.True(Gateway(facts).AdmitMemberSubmission(Member, State(42, actor: Member)).IsAdmitted,
			"a held item's state is its holder's own report — the native use and the slot move both ride it");
	}

	[Fact]
	public void StateOfAnotherMembersCarriedItem_IsIgnoredWithoutAnAnswer()
	{
		// The gap S1 closes: the item domain's DecideUpdate checks existence, a
		// terminal row and the revision (which the handler re-stamps from its own
		// record), so before this rule ANY member could rewrite ANY live row — the
		// other player's carried item included. Answering is refused for the destroy
		// rule's reason: the shape is the remote-clone display proxy, and a rejection
		// would start a guest rollback on a copy that is not the item's fact.
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, OtherMember);

		var decision = Gateway(facts).AdmitMemberSubmission(Member, State(42, actor: Member));

		Assert.Equal(CommandAdmissionKind.Ignored, decision.Kind);
		Assert.Equal(RejectionReason.NotAuthorized, decision.Reason);
		Assert.False(decision.AnswersSender);
	}

	[Fact]
	public void StateOfAnItemInstalledOnAnotherMember_IsIgnored()
	{
		var facts = new FakeItemFacts();
		facts.AppliedTo(42, OtherMember, limbIndex: 3);

		Assert.Equal(CommandAdmissionKind.Ignored,
			Gateway(facts).AdmitMemberSubmission(Member, State(42, actor: Member)).Kind);
	}

	[Fact]
	public void StateOfAnItemInstalledOnTheSendersOwnLimb_IsAdmitted()
	{
		var facts = new FakeItemFacts();
		facts.AppliedTo(42, Member, limbIndex: 3);

		Assert.True(Gateway(facts).AdmitMemberSubmission(Member, State(42, actor: Member)).IsAdmitted);
	}

	[Fact]
	public void StateOfAWorldItem_IsAdmitted()
	{
		// A world item used in place (a ground canister's own drink) is reported by
		// the member that used it, so the rule must let a world row through —
		// refusing it would refuse ordinary play.
		var facts = new FakeItemFacts();
		facts.InWorld(42);

		Assert.True(Gateway(facts).AdmitMemberSubmission(Member, State(42, actor: Member)).IsAdmitted);
	}

	[Fact]
	public void StateOfAnIdThisHostNeverJudged_IsNotTheSeamsToAnswer()
	{
		// Defensive, as for the destroy rule: the handler's creation-before-operation
		// invariant answers an unjudged id above this seam, and a missing-carried
		// report is adopted by the handler's own heal before reaching here.
		Assert.True(Gateway().AdmitMemberSubmission(Member, State(42, actor: Member)).IsAdmitted,
			"the seam holds no judgement for an unjudged id, so it must not produce one");
	}

	[Fact]
	public void StateOfAnItemContainedInAnotherMembersContainer_IsIgnored()
	{
		// The nested half of the same rule: a bag's contents are recorded under the
		// member that holds it, so a viewer's copy may not rewrite them either.
		var facts = new FakeItemFacts();
		facts.ContainedIn(42, OtherMember, parentId: 77);

		Assert.Equal(CommandAdmissionKind.Ignored,
			Gateway(facts).AdmitMemberSubmission(Member, State(42, actor: Member)).Kind);
	}

	[Fact]
	public void StateOfAnItemInTheSendersOwnContainer_IsAdmitted()
	{
		var facts = new FakeItemFacts();
		facts.ContainedIn(42, Member, parentId: 77);

		Assert.True(Gateway(facts).AdmitMemberSubmission(Member, State(42, actor: Member)).IsAdmitted);
	}

	[Fact]
	public void StateOfAnItemInsideAWorldContainer_IsAdmitted()
	{
		// The chain walk decides "in the world": a child of a container lying on the
		// ground is world content however its own row spells the holder it was
		// recorded under, and every peer may report on world content.
		var facts = new FakeItemFacts();
		facts.InWorld(77);
		facts.ContainedIn(42, OtherMember, parentId: 77);

		Assert.True(Gateway(facts).AdmitMemberSubmission(Member, State(42, actor: Member)).IsAdmitted);
	}

	[Fact]
	public void DestroyOfAnItemContainedInAnotherMembersContainer_IsIgnored()
	{
		// The destroy rule asked the same question and shared the same fallback, so
		// the whole family moves together rather than one kind at a time.
		var facts = new FakeItemFacts();
		facts.ContainedIn(42, OtherMember, parentId: 77);

		Assert.Equal(CommandAdmissionKind.Ignored,
			Gateway(facts).AdmitMemberSubmission(Member,
				Destroy(instanceId: 42, actor: Member)).Kind);
	}

	[Fact]
	public void IgnoredStateReport_IsAuditedWithTheItemIdItNamed()
	{
		var logger = new RecordingLogger<KernelCommandGateway>();
		var facts = new FakeItemFacts();
		facts.CarriedBy(4242, OtherMember);

		Gateway(facts, logger).AdmitMemberSubmission(Member, State(4242, actor: Member));

		var entry = Assert.Single(logger.Entries);
		Assert.Equal(LogLevel.Warning, entry.Level);
		Assert.Contains("UpdateItemStateCommand", entry.Message);
		Assert.Contains("4242", entry.Message);
		Assert.Contains(nameof(RejectionReason.NotAuthorized), entry.Message);
	}

	[Fact]
	public void ContainerSyncOfAnotherMembersContainer_IsIgnoredWithoutAnAnswer()
	{
		// The kind's own hole: it reached the kernel without passing the seam at all,
		// and the domain rewrites the parent's data, moves the listed children in and
		// destroys the descendants the report leaves out.
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, OtherMember);

		var decision = Gateway(facts).AdmitMemberSubmission(Member, Sync(parentId: 42, children: []));

		Assert.Equal(CommandAdmissionKind.Ignored, decision.Kind);
		Assert.Equal(RejectionReason.NotAuthorized, decision.Reason);
		Assert.False(decision.AnswersSender);
	}

	[Fact]
	public void ContainerSyncOfTheSendersOwnContainer_IsAdmitted()
	{
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, Member);

		Assert.True(Gateway(facts).AdmitMemberSubmission(Member, Sync(parentId: 42, children: [43])).IsAdmitted);
	}

	[Fact]
	public void ContainerSyncOfAContainerTheHostNeverJudged_IsAdmitted() =>
		// The carry-registration heal: the domain creates that parent as the
		// reporter's own carried item, so the seam has nothing to refuse.
		Assert.True(Gateway().AdmitMemberSubmission(Member, Sync(parentId: 42, children: [43])).IsAdmitted);

	[Fact]
	public void ContainerSyncListingAnotherMembersCarriedItem_IsIgnored()
	{
		// The parent rule alone would still let a report pull a peer's carried item
		// into the reporter's own container.
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, Member);
		facts.CarriedBy(43, OtherMember);

		Assert.Equal(CommandAdmissionKind.Ignored,
			Gateway(facts).AdmitMemberSubmission(Member, Sync(parentId: 42, children: [43])).Kind);
	}

	[Fact]
	public void ContainerSyncListingAnItemInstalledOnAnotherMember_IsIgnored()
	{
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, Member);
		facts.AppliedTo(43, OtherMember, limbIndex: 3);

		Assert.Equal(CommandAdmissionKind.Ignored,
			Gateway(facts).AdmitMemberSubmission(Member, Sync(parentId: 42, children: [43])).Kind);
	}

	[Fact]
	public void ContainerSyncListingAnItemRecordedInAnotherContainer_IsAdmitted()
	{
		// A container that changed hands leaves its old contents recorded under the
		// previous holder, so a CONTAINED row is not evidence of possession and
		// refusing it would refuse ordinary play.
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, Member);
		facts.ContainedIn(43, OtherMember, parentId: 77);

		Assert.True(Gateway(facts).AdmitMemberSubmission(Member, Sync(parentId: 42, children: [43])).IsAdmitted);
	}

	[Fact]
	public void ContainerSyncReport_IsAuditedWithTheContainerItNamed()
	{
		var logger = new RecordingLogger<KernelCommandGateway>();
		var facts = new FakeItemFacts();
		facts.CarriedBy(4242, OtherMember);

		Gateway(facts, logger).AdmitMemberSubmission(Member, Sync(parentId: 4242, children: []));

		var entry = Assert.Single(logger.Entries);
		Assert.Contains("SyncContainerItemsCommand", entry.Message);
		Assert.Contains("4242", entry.Message);
		Assert.Contains(nameof(RejectionReason.NotAuthorized), entry.Message);
	}

	private static KernelCommandGateway Gateway() => new(new FakeItemFacts(), new RecordingLogger<KernelCommandGateway>());

	private static KernelCommandGateway Gateway(FakeItemFacts facts) => new(facts, new RecordingLogger<KernelCommandGateway>());

	private static KernelCommandGateway Gateway(FakeItemFacts facts, RecordingLogger<KernelCommandGateway> logger) => new(facts, logger);

	private static DestroyItemCommand Destroy(ulong instanceId, ulong actor) =>
		new(new OperationId(1), new ActorId(actor), new RunEpoch(1), AuthorityKind.OwnerPredictedHostValidated, instanceId, TerminalKind.Consumed, 0);

	private static UpdateItemStateCommand State(ulong instanceId, ulong actor) =>
		new(new OperationId(1), new ActorId(actor), new RunEpoch(1), AuthorityKind.OwnerPredictedHostValidated, instanceId, ItemData.Empty, 0);

	private static SyncContainerItemsCommand Sync(ulong parentId, IReadOnlyList<ulong> children) =>
		new(new OperationId(1), new ActorId(Member), new RunEpoch(1), AuthorityKind.OwnerPredictedHostValidated,
			new ItemIdentity(parentId, "test_container"),
			ItemData.Empty,
			[.. children.Select(id => new ContainerChildFact(id, "test_item", parentId, ItemData.Empty))]);
}
