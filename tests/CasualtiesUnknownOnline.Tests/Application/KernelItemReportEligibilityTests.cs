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

		Assert.True(Gateway(facts).Admit(Member, State(42, actor: Member)).IsAdmitted,
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

		var decision = Gateway(facts).Admit(Member, State(42, actor: Member));

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
			Gateway(facts).Admit(Member, State(42, actor: Member)).Kind);
	}

	/// <summary>
	/// The same ineligible item, reached by the CONSOLE verb: answered instead of
	/// ignored, and the difference is the sender's own copy. A native report of
	/// another member's item is the remote-clone display proxy, so answering it would
	/// start a rollback on a copy that is not the item's fact; a console write has
	/// already changed the writer's local object, so a verdict it never sees would
	/// leave that object changed for good.
	/// </summary>
	[Fact]
	public void ConsoleWriteForAnotherMembersItem_IsAnsweredRatherThanIgnored()
	{
		var rules = new FakeMemberCommandRules { AllowItemDataWrite = true };
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, OtherMember);

		var decision = Gateway(facts, rules).AdmitConsoleWrite(Member, State(42, actor: Member));

		Assert.Equal(CommandAdmissionKind.Refused, decision.Kind);
		Assert.Equal(RejectionReason.NotAuthorized, decision.Reason);
		Assert.True(decision.AnswersSender, "the writer's own local object already carries the value it reported");
	}

	[Fact]
	public void StateOfAnItemInstalledOnTheSendersOwnLimb_IsAdmitted()
	{
		var facts = new FakeItemFacts();
		facts.AppliedTo(42, Member, limbIndex: 3);

		Assert.True(Gateway(facts).Admit(Member, State(42, actor: Member)).IsAdmitted);
	}

	[Fact]
	public void StateOfAWorldItem_IsAdmitted()
	{
		// A world item used in place (a ground canister's own drink) is reported by
		// the member that used it, so the rule must let a world row through —
		// refusing it would refuse ordinary play.
		var facts = new FakeItemFacts();
		facts.InWorld(42);

		Assert.True(Gateway(facts).Admit(Member, State(42, actor: Member)).IsAdmitted);
	}

	[Fact]
	public void StateOfAnIdThisHostNeverJudged_IsNotTheSeamsToAnswer()
	{
		// Defensive, as for the destroy rule: the handler's creation-before-operation
		// invariant answers an unjudged id above this seam, and a missing-carried
		// report is adopted by the handler's own heal before reaching here.
		Assert.True(Gateway().Admit(Member, State(42, actor: Member)).IsAdmitted,
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
			Gateway(facts).Admit(Member, State(42, actor: Member)).Kind);
	}

	[Fact]
	public void StateOfAnItemInTheSendersOwnContainer_IsAdmitted()
	{
		var facts = new FakeItemFacts();
		facts.ContainedIn(42, Member, parentId: 77);

		Assert.True(Gateway(facts).Admit(Member, State(42, actor: Member)).IsAdmitted);
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

		Assert.True(Gateway(facts).Admit(Member, State(42, actor: Member)).IsAdmitted);
	}

	[Fact]
	public void DestroyOfAnItemContainedInAnotherMembersContainer_IsIgnored()
	{
		// The destroy rule asked the same question and shared the same fallback, so
		// the whole family moves together rather than one kind at a time.
		var facts = new FakeItemFacts();
		facts.ContainedIn(42, OtherMember, parentId: 77);

		Assert.Equal(CommandAdmissionKind.Ignored,
			Gateway(facts).Admit(Member,
				Destroy(instanceId: 42, actor: Member)).Kind);
	}

	[Fact]
	public void IgnoredStateReport_IsAuditedWithTheItemIdItNamed()
	{
		var logger = new RecordingLogger<KernelCommandGateway>();
		var facts = new FakeItemFacts();
		facts.CarriedBy(4242, OtherMember);

		Gateway(facts, logger).Admit(Member, State(4242, actor: Member));

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

		var decision = Gateway(facts).Admit(Member, Sync(parentId: 42, children: []));

		Assert.Equal(CommandAdmissionKind.Ignored, decision.Kind);
		Assert.Equal(RejectionReason.NotAuthorized, decision.Reason);
		Assert.False(decision.AnswersSender);
	}

	[Fact]
	public void ContainerSyncOfTheSendersOwnContainer_IsAdmitted()
	{
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, Member);

		Assert.True(Gateway(facts).Admit(Member, Sync(parentId: 42, children: [43])).IsAdmitted);
	}

	[Fact]
	public void ContainerSyncOfAContainerTheHostNeverJudged_IsAdmitted() =>
		// The carry-registration heal: the domain creates that parent as the
		// reporter's own carried item, so the seam has nothing to refuse.
		Assert.True(Gateway().Admit(Member, Sync(parentId: 42, children: [43])).IsAdmitted);

	[Fact]
	public void ContainerSyncListingAnotherMembersCarriedItem_IsIgnored()
	{
		// The parent rule alone would still let a report pull a peer's carried item
		// into the reporter's own container.
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, Member);
		facts.CarriedBy(43, OtherMember);

		Assert.Equal(CommandAdmissionKind.Ignored,
			Gateway(facts).Admit(Member, Sync(parentId: 42, children: [43])).Kind);
	}

	[Fact]
	public void ContainerSyncListingAnItemInstalledOnAnotherMember_IsIgnored()
	{
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, Member);
		facts.AppliedTo(43, OtherMember, limbIndex: 3);

		Assert.Equal(CommandAdmissionKind.Ignored,
			Gateway(facts).Admit(Member, Sync(parentId: 42, children: [43])).Kind);
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

		Assert.True(Gateway(facts).Admit(Member, Sync(parentId: 42, children: [43])).IsAdmitted);
	}

	[Fact]
	public void ContainerSyncReport_IsAuditedWithTheContainerItNamed()
	{
		var logger = new RecordingLogger<KernelCommandGateway>();
		var facts = new FakeItemFacts();
		facts.CarriedBy(4242, OtherMember);

		Gateway(facts, logger).Admit(Member, Sync(parentId: 4242, children: []));

		var entry = Assert.Single(logger.Entries);
		Assert.Contains("SyncContainerItemsCommand", entry.Message);
		Assert.Contains("4242", entry.Message);
		Assert.Contains(nameof(RejectionReason.NotAuthorized), entry.Message);
	}

	/// <summary>
	/// The native-use half of the family: a report that CLAIMS a use must name an item
	/// whose own data admits one. Both of the game's use call sites run their delegate
	/// behind <c>ItemInfo.usable</c>, so a use of a definition whose flag is clear is a
	/// report no client of this game could have made. The refusal is ANSWERED — the use
	/// already ran on the sender's own client — and it names the definition the HOST's
	/// record holds, never the report's.
	/// </summary>
	[Fact]
	public void NativeUseOfAnItemWhoseOwnDataForbidsIt_IsRefusedAndAnswered()
	{
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, Member);
		var logger = new RecordingLogger<KernelCommandGateway>();

		var decision = Gateway(facts, new FakeItemUseFacts().Forbid("test_item"), logger)
			.AdmitUse(Member, State(42, actor: Member));

		Assert.Equal(CommandAdmissionKind.Refused, decision.Kind);
		Assert.Equal(RejectionReason.UseNotPermitted, decision.Reason);
		Assert.True(decision.AnswersSender, "the use already ran on the sender's own client — a verdict it never sees would leave it changed");
		var entry = Assert.Single(logger.Entries);
		Assert.Equal(LogLevel.Warning, entry.Level);
		Assert.Contains("'test_item'", entry.Message);
		Assert.Contains(nameof(RejectionReason.UseNotPermitted), entry.Message);
	}

	/// <summary>The other side of the same answer: the item's own data admits the use, so the ordinary native use is admitted.</summary>
	[Fact]
	public void NativeUseOfAnItemWhoseOwnDataPermitsIt_IsAdmitted()
	{
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, Member);

		Assert.True(Gateway(facts, new FakeItemUseFacts().Permit("test_item"))
			.AdmitUse(Member, State(42, actor: Member)).IsAdmitted);
	}

	/// <summary>
	/// A definition this host cannot read is not evidence that the item is unusable: the
	/// read failed, so the report is admitted as it was before the rule existed. The
	/// case pins the three-valued port — folding "unknown" into "forbidden" would refuse
	/// every use of a mod item the host does not carry.
	/// </summary>
	[Fact]
	public void NativeUseOfADefinitionThisHostCannotRead_IsAdmitted()
	{
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, Member);

		Assert.True(Gateway(facts, new FakeItemUseFacts().Unknown("test_item"))
			.AdmitUse(Member, State(42, actor: Member)).IsAdmitted);
	}

	/// <summary>
	/// The order the two rules run in: a use report on ANOTHER member's item keeps the
	/// eligibility family's verdict — ignored, with no answer — even when the item's own
	/// data also forbids a use. Answering would start a guest rollback on a copy that is
	/// not the item's fact, which is what the eligibility rule exists to avoid.
	/// </summary>
	[Fact]
	public void NativeUseOfAnotherMembersItem_StaysTheEligibilityVerdict()
	{
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, OtherMember);

		var decision = Gateway(facts, new FakeItemUseFacts().Forbid("test_item"))
			.AdmitUse(Member, State(42, actor: Member));

		Assert.Equal(CommandAdmissionKind.Ignored, decision.Kind);
		Assert.Equal(RejectionReason.NotAuthorized, decision.Reason);
		Assert.False(decision.AnswersSender);
	}

	/// <summary>
	/// The bound is about the USE CLAIM, not about the item's state: the same payload
	/// arriving as a plain state report (a slot move) claims no use, so the item's
	/// <c>usable</c> flag has nothing to say about it and ordinary play stays untouched.
	/// </summary>
	[Fact]
	public void StateReportOfAnItemWhoseOwnDataForbidsAUse_IsStillAdmitted()
	{
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, Member);

		Assert.True(Gateway(facts, new FakeItemUseFacts().Forbid("test_item"))
			.Admit(Member, State(42, actor: Member)).IsAdmitted,
			"a slot move or a state drift is not a use claim; the record keeps taking its holder's own report");
	}

	private static KernelCommandGateway Gateway() => new(new FakeItemFacts(), new FakeMemberCommandRules(), new FakeItemUseFacts(), new RecordingLogger<KernelCommandGateway>());

	private static KernelCommandGateway Gateway(FakeItemFacts facts) => new(facts, new FakeMemberCommandRules(), new FakeItemUseFacts(), new RecordingLogger<KernelCommandGateway>());

	private static KernelCommandGateway Gateway(FakeItemFacts facts, RecordingLogger<KernelCommandGateway> logger) => new(facts, new FakeMemberCommandRules(), new FakeItemUseFacts(), logger);

	/// <summary>The seam with the host's rule OPEN, for the console-verb cases: the rule is judged before any item rule, so the cases below need it open to reach the rule they pin.</summary>
	private static KernelCommandGateway Gateway(FakeItemFacts facts, FakeMemberCommandRules rules) => new(facts, rules, new FakeItemUseFacts(), new RecordingLogger<KernelCommandGateway>());

	/// <summary>The seam with the game's own item data seeded — the native-use family's data anchor.</summary>
	private static KernelCommandGateway Gateway(FakeItemFacts facts, FakeItemUseFacts useFacts) => new(facts, new FakeMemberCommandRules(), useFacts, new RecordingLogger<KernelCommandGateway>());

	private static KernelCommandGateway Gateway(FakeItemFacts facts, FakeItemUseFacts useFacts, RecordingLogger<KernelCommandGateway> logger) => new(facts, new FakeMemberCommandRules(), useFacts, logger);

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
