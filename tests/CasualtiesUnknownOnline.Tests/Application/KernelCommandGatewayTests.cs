using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Application;

/// <summary>
/// The admission seam (Application layer), in isolation: a member's submission
/// is judged by the actor/sender binding, the per-kind authority declaration, the
/// host's own rule for the console item-data write verb, and the limb-item rules —
/// and by nothing else. The end-to-end halves (the host's real command path and the
/// guest's rollback) are pinned by <c>ItemDestroyAuthorityTests</c>,
/// <c>KernelProtocolServiceTests</c>, <c>CommandAdmissionIntegrationTests</c> and
/// <c>CommandConsoleMemberItemDataTests</c>, which run the same production
/// composition root.
///
/// <para>
/// The item-REPORT eligibility family (the destroy report, the state report and
/// the container sync) lives in <c>KernelItemReportEligibilityTests</c>, split out
/// because one class may carry at most
/// <c>TestClassSizeGateTests.MaxCasesPerClass</c> cases; the two suites share
/// <see cref="FakeItemFacts"/> and <c>AdmissionProbe</c>.
/// </para>
/// </summary>
public class KernelCommandGatewayTests
{
	private const ulong Member = 2001;
	private const ulong OtherMember = 3001;

	[Fact]
	public void CommandWhoseActorIsNotTheSender_IsRefusedAndAnswered()
	{
		var decision = Gateway().Admit(Member,
			Destroy(instanceId: 42, actor: OtherMember, AuthorityKind.OwnerPredictedHostValidated));

		Assert.Equal(CommandAdmissionKind.Refused, decision.Kind);
		Assert.Equal(RejectionReason.NotAuthorized, decision.Reason);
		Assert.True(decision.AnswersSender, "a member that submits under another actor's name is told why");
	}

	[Fact]
	public void HostOnlyCommandFromAMember_IsRefusedAndAnswered()
	{
		var command = new ResetWorldItemsCommand(new OperationId(1), new ActorId(Member), new RunEpoch(1), AuthorityKind.HostOnly);

		var decision = Gateway().Admit(Member, command);

		Assert.Equal(CommandAdmissionKind.Refused, decision.Kind);
		Assert.Equal(RejectionReason.NotAuthorized, decision.Reason);
	}

	[Fact]
	public void PresentationOnlyCommandFromAMember_IsRefusedAndAnswered()
	{
		var command = new DropItemCommand(
			new OperationId(1), new ActorId(Member), new RunEpoch(1), AuthorityKind.PresentationOnly, 42, ItemLocation.World(1f, 2f), 0);

		var decision = Gateway().Admit(Member, command);

		Assert.Equal(CommandAdmissionKind.Refused, decision.Kind);
		Assert.Equal(RejectionReason.NotAuthorized, decision.Reason);
	}

	[Fact]
	public void OwnerPredictedCommandFromItsOwnActor_IsAdmitted()
	{
		var command = new DropItemCommand(
			new OperationId(1), new ActorId(Member), new RunEpoch(1), AuthorityKind.OwnerPredictedHostValidated, 42, ItemLocation.World(1f, 2f), 0);

		Assert.True(Gateway().Admit(Member, command).IsAdmitted,
			"an owner-predicted command from its own actor is the normal member submission");
	}

	[Fact]
	public void ObservedCommandFromAMember_IsAdmitted()
	{
		var command = new DropItemCommand(
			new OperationId(1), new ActorId(Member), new RunEpoch(1), AuthorityKind.TriggerObservedHostCommitted, 42, ItemLocation.World(1f, 2f), 0);

		Assert.True(Gateway().Admit(Member, command).IsAdmitted,
			"the observing member submits and the host commits");
	}

	[Fact]
	public void HostOnlyWireKindFromAMember_IsRefusedAndAnswered()
	{
		// The declaration the MAPPER stamps (not a hand-built authority) is what the
		// seam judges: before the per-kind table this report read
		// OwnerPredictedHostValidated — the same value as a member's item use — and
		// a member could start a run, advance a layer or write another player's
		// status straight into the kernel. The run baseline is the ticket's own
		// example of the two readings having to differ.
		var command = KernelWireMapper.FromWireCommand(
			new WireCommand { Kind = WireCommandKind.RunStart, RunState = new WireRunState { RunId = 7 } },
			Header(Member));

		Assert.Equal(AuthorityKind.HostOnly, command.Authority);

		var decision = Gateway().Admit(Member, command);

		Assert.Equal(CommandAdmissionKind.Refused, decision.Kind);
		Assert.Equal(RejectionReason.NotAuthorized, decision.Reason);
		Assert.True(decision.AnswersSender, "a member that reports a host-only fact is told why");
	}

	[Fact]
	public void MemberAuthoredWireKind_IsStillAdmitted()
	{
		// The other side of the same table: the report a member's own client really
		// emits keeps its member-authorable declaration and reaches the kernel.
		var command = KernelWireMapper.FromWireCommand(
			new WireCommand
			{
				Kind = WireCommandKind.ItemUpdateState,
				Identity = new WireItemIdentity { InstanceId = 42, DefinitionId = "test_item" },
				Data = new WireItemData { Condition = 0.5f },
			},
			Header(Member));

		Assert.Equal(AuthorityKind.OwnerPredictedHostValidated, command.Authority);
		Assert.True(Gateway().Admit(Member, command).IsAdmitted);
	}

	[Fact]
	public void DestroyOfAnotherMembersCarriedItem_IsIgnoredWithoutAnAnswer()
	{
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, OtherMember);

		var decision = Gateway(facts).Admit(Member,
			Destroy(instanceId: 42, actor: Member, AuthorityKind.OwnerPredictedHostValidated));

		Assert.Equal(CommandAdmissionKind.Ignored, decision.Kind);
		Assert.Equal(RejectionReason.NotAuthorized, decision.Reason);
		Assert.False(decision.AnswersSender,
			"this shape has always been dropped in silence; answering it would start a guest rollback that never happened before");
	}

	[Fact]
	public void DestroyOfTheSendersOwnCarriedItem_IsAdmitted()
	{
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, Member);

		Assert.True(Gateway(facts).Admit(Member,
			Destroy(instanceId: 42, actor: Member, AuthorityKind.OwnerPredictedHostValidated)).IsAdmitted);
	}

	[Fact]
	public void DestroyOfAWorldItem_IsAdmitted()
	{
		var facts = new FakeItemFacts();
		facts.InWorld(42);

		Assert.True(Gateway(facts).Admit(Member,
			Destroy(instanceId: 42, actor: Member, AuthorityKind.OwnerPredictedHostValidated)).IsAdmitted);
	}

	[Fact]
	public void DestroyOfAnotherMembersAppliedItem_IsIgnoredWithoutAnAnswer()
	{
		// An applied item is the second family whose end only its body's owner can
		// observe (the component decays to zero on that client), so it is judged
		// exactly like a carried item: a viewer's clone may not end it.
		var facts = new FakeItemFacts();
		facts.AppliedTo(42, OtherMember, limbIndex: 3);

		var decision = Gateway(facts).Admit(Member,
			Destroy(instanceId: 42, actor: Member, AuthorityKind.OwnerPredictedHostValidated));

		Assert.Equal(CommandAdmissionKind.Ignored, decision.Kind);
		Assert.Equal(RejectionReason.NotAuthorized, decision.Reason);
	}

	[Fact]
	public void DestroyOfAnItemAppliedToTheSendersOwnLimb_IsAdmitted()
	{
		var facts = new FakeItemFacts();
		facts.AppliedTo(42, Member, limbIndex: 3);

		Assert.True(Gateway(facts).Admit(Member,
			Destroy(instanceId: 42, actor: Member, AuthorityKind.OwnerPredictedHostValidated)).IsAdmitted);
	}

	[Fact]
	public void DestroyOfAnIdThisHostNeverJudged_IsNotTheSeamsToAnswer()
	{
		// A conservative guard, not the production path: on the real path the
		// handler's creation-before-operation invariant runs above this seam and
		// refuses an unjudged id itself (CommandAdmissionIntegrationTests pins
		// that). The seam must never INVENT a verdict for state it does not hold.
		var decision = Gateway().Admit(Member,
			Destroy(instanceId: 42, actor: Member, AuthorityKind.OwnerPredictedHostValidated));

		Assert.True(decision.IsAdmitted, "the seam holds no judgement for an unjudged id, so it must not produce one");
	}

	[Fact]
	public void InstallOfTheSendersOwnCarriedItemOnItsOwnLimb_IsAdmitted()
	{
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, Member);

		Assert.True(Gateway(facts).Admit(Member, Apply(42, owner: Member, limbIndex: 3)).IsAdmitted,
			"the applier acts on the body it simulates, so its own carried item on its own limb is the normal report");
	}

	[Fact]
	public void InstallOfAnotherMembersCarriedItem_IsIgnored()
	{
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, OtherMember);

		var decision = Gateway(facts).Admit(Member, Apply(42, owner: Member, limbIndex: 3));

		Assert.Equal(CommandAdmissionKind.Ignored, decision.Kind);
		Assert.Equal(RejectionReason.NotAuthorized, decision.Reason);
	}

	[Fact]
	public void InstallOnSomebodyElsesLimb_IsIgnored()
	{
		// The limb is the OTHER half of the same rule: an item may not be reported onto a
		// body this member does not simulate, however it came by the item.
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, Member);

		var decision = Gateway(facts).Admit(Member, Apply(42, owner: OtherMember, limbIndex: 3));

		Assert.Equal(CommandAdmissionKind.Ignored, decision.Kind);
		Assert.Equal(RejectionReason.NotAuthorized, decision.Reason);
	}

	[Fact]
	public void InstallReportedTwice_IsAdmitted()
	{
		// A repeated observation of the same applied fact is a re-assertion, not a
		// divergence: the kernel advances the revision and leaves the item on that limb.
		var facts = new FakeItemFacts();
		facts.AppliedTo(42, Member, limbIndex: 3);

		Assert.True(Gateway(facts).Admit(Member, Apply(42, owner: Member, limbIndex: 3)).IsAdmitted);
	}

	[Fact]
	public void InstallOfAnItemAlreadyAppliedToAnotherLimb_IsIgnored()
	{
		// The family's one way in is a relocation FROM a live location: an id already
		// installed elsewhere is not this report's to move, and reading it as a re-assertion
		// would silently move the row off the limb its component is really on.
		var facts = new FakeItemFacts();
		facts.AppliedTo(42, Member, limbIndex: 3);

		var decision = Gateway(facts).Admit(Member, Apply(42, owner: Member, limbIndex: 5));

		Assert.Equal(CommandAdmissionKind.Ignored, decision.Kind);
	}

	[Fact]
	public void TakeOffOfTheSendersOwnLimbComponent_IsAdmitted()
	{
		var facts = new FakeItemFacts();
		facts.AppliedTo(42, Member, limbIndex: 3);

		Assert.True(Gateway(facts).Admit(Member, Remove(42, appliedOwner: Member, limbIndex: 3, newOwner: Member)).IsAdmitted,
			"the component comes off the sender's own body and into the sender's own hands");
	}

	[Fact]
	public void TakeOffHandingTheItemToAThirdParty_IsIgnored()
	{
		// A cross-player removal is the HOST's commit, never a member's report.
		var facts = new FakeItemFacts();
		facts.AppliedTo(42, Member, limbIndex: 3);

		var decision = Gateway(facts).Admit(Member, Remove(42, appliedOwner: Member, limbIndex: 3, newOwner: OtherMember));

		Assert.Equal(CommandAdmissionKind.Ignored, decision.Kind);
		Assert.Equal(RejectionReason.NotAuthorized, decision.Reason);
	}

	[Fact]
	public void TakeOffNamingAnotherLimb_IsIgnored()
	{
		var facts = new FakeItemFacts();
		facts.AppliedTo(42, Member, limbIndex: 3);

		var decision = Gateway(facts).Admit(Member, Remove(42, appliedOwner: Member, limbIndex: 5, newOwner: Member));

		Assert.True(
			decision.Kind == CommandAdmissionKind.Ignored,
			"the removal names the limb it is the counterpart of, and the recorded location must agree");
	}

	[Fact]
	public void TakeOffOfSomebodyElsesLimbComponent_IsIgnored()
	{
		var facts = new FakeItemFacts();
		facts.AppliedTo(42, OtherMember, limbIndex: 3);

		var decision = Gateway(facts).Admit(Member, Remove(42, appliedOwner: Member, limbIndex: 3, newOwner: Member));

		Assert.Equal(CommandAdmissionKind.Ignored, decision.Kind);
	}

	[Fact]
	public void Refusal_IsAuditedOnceWithTheCommandActorSenderAndReason()
	{
		var logger = new RecordingLogger<KernelCommandGateway>();

		Gateway(logger).Admit(Member,
			Destroy(instanceId: 42, actor: OtherMember, AuthorityKind.OwnerPredictedHostValidated));

		var entry = Assert.Single(logger.Entries);
		Assert.Equal(LogLevel.Warning, entry.Level);
		Assert.Contains("DestroyItemCommand", entry.Message);
		Assert.Contains(OtherMember.ToString(), entry.Message);
		Assert.Contains(Member.ToString(), entry.Message);
		Assert.Contains(nameof(RejectionReason.NotAuthorized), entry.Message);
	}

	[Fact]
	public void Admission_IsNotAudited()
	{
		var logger = new RecordingLogger<KernelCommandGateway>();

		Gateway(logger).Admit(Member,
			Destroy(instanceId: 42, actor: Member, AuthorityKind.OwnerPredictedHostValidated));

		Assert.Empty(logger.Entries);
	}

	[Fact]
	public void IgnoredDestroyReport_IsAuditedWithTheItemIdItNamed()
	{
		// Nobody is answered for this verdict, so the audit line is the only trace
		// the dropped report leaves — it has to name the item.
		var logger = new RecordingLogger<KernelCommandGateway>();
		var facts = new FakeItemFacts();
		facts.CarriedBy(4242, OtherMember);

		Gateway(facts, logger).Admit(Member,
			Destroy(instanceId: 4242, actor: Member, AuthorityKind.OwnerPredictedHostValidated));

		var entry = Assert.Single(logger.Entries);
		Assert.Contains("4242", entry.Message);
		Assert.Contains(nameof(RejectionReason.NotAuthorized), entry.Message);
	}

	/// <summary>
	/// The host's own rule for the console verb, on the CLOSED side (the default a
	/// host ships with): the report is one the seam would otherwise admit — the
	/// member's own carried item, which is its own report — so the rule is the only
	/// thing between it and the kernel. It is ANSWERED, unlike the item-eligibility
	/// refusals beside it: the member's console has already applied the value to its
	/// own local object, and a verdict it never saw would leave that object changed.
	/// </summary>
	[Fact]
	public void ConsoleVerbWithTheHostRuleClosed_IsRefusedAndAnswered()
	{
		var logger = new RecordingLogger<KernelCommandGateway>();
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, Member);
		var state = State(42, actor: Member);

		var decision = Gateway(facts, logger).AdmitConsoleWrite(Member, state);

		Assert.Equal(CommandAdmissionKind.Refused, decision.Kind);
		Assert.Equal(RejectionReason.HostRuleDenied, decision.Reason);
		Assert.True(decision.AnswersSender, "a console write must be answered — its sender already changed its own copy");
		Assert.Contains("the host's rule does not open the item-data write verbs", Assert.Single(logger.Entries).Message);
	}

	/// <summary>The other side of the same verdict: with the host's rule open the very same report is admitted, so the rule — not the report's shape — is what refused it above.</summary>
	[Fact]
	public void ConsoleVerbWithTheHostRuleOpen_IsAdmitted()
	{
		var rules = new FakeMemberCommandRules { AllowItemDataWrite = true };
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, Member);

		Assert.True(Gateway(rules).AdmitConsoleWrite(Member, State(42, actor: Member)).IsAdmitted,
			"the host's rule is the whole difference between the two verdicts");
	}

	/// <summary>
	/// The rule is about the VERB, not about the item: the native report of the same
	/// item is admitted either way, which is what keeps a closed rule from touching
	/// ordinary play (the host's rule is read for the console kind alone).
	/// </summary>
	[Fact]
	public void NativeReportOfTheSameItem_IsUntouchedByTheHostRule()
	{
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, Member);

		Assert.True(Gateway(facts).Admit(Member, State(42, actor: Member)).IsAdmitted,
			"a carried item's state is its holder's own report — the rule closes the console verb, not the item");
	}

	private static KernelCommandGateway Gateway() => new(new FakeItemFacts(), ClosedRules(), new FakeItemUseFacts(), new RecordingLogger<KernelCommandGateway>());

	private static KernelCommandGateway Gateway(FakeItemFacts facts) => new(facts, ClosedRules(), new FakeItemUseFacts(), new RecordingLogger<KernelCommandGateway>());

	private static KernelCommandGateway Gateway(RecordingLogger<KernelCommandGateway> logger) => new(new FakeItemFacts(), ClosedRules(), new FakeItemUseFacts(), logger);

	private static KernelCommandGateway Gateway(FakeItemFacts facts, RecordingLogger<KernelCommandGateway> logger) => new(facts, ClosedRules(), new FakeItemUseFacts(), logger);

	/// <summary>The seam with the host's rule OPEN — the other side of the console-verb verdict.</summary>
	private static KernelCommandGateway Gateway(FakeMemberCommandRules rules) => new(new FakeItemFacts(), rules, new FakeItemUseFacts(), new RecordingLogger<KernelCommandGateway>());

	/// <summary>The host's own member-command rules as a host ships them: the item-data write verb is closed.</summary>
	private static FakeMemberCommandRules ClosedRules() => new();

	private static DestroyItemCommand Destroy(ulong instanceId, ulong actor, AuthorityKind authority) =>
		new(new OperationId(1), new ActorId(actor), new RunEpoch(1), authority, instanceId, TerminalKind.Consumed, 0);

	private static UpdateItemStateCommand State(ulong instanceId, ulong actor) =>
		new(new OperationId(1), new ActorId(actor), new RunEpoch(1), AuthorityKind.OwnerPredictedHostValidated, instanceId, ItemData.Empty, 0);

	private static ApplyLimbItemCommand Apply(ulong instanceId, ulong owner, int limbIndex) =>
		new(new OperationId(1), new ActorId(Member), new RunEpoch(1), AuthorityKind.OwnerPredictedHostValidated, instanceId, new ActorId(owner), limbIndex, 0);

	private static RemoveLimbItemCommand Remove(ulong instanceId, ulong appliedOwner, int limbIndex, ulong newOwner) =>
		new(new OperationId(1), new ActorId(Member), new RunEpoch(1), AuthorityKind.OwnerPredictedHostValidated, instanceId, new ActorId(appliedOwner), limbIndex, new ActorId(newOwner), 0);

	private static EnvelopeHeader Header(ulong sender) => new()
	{
		ProtocolVersion = 1,
		RunEpoch = 1,
		SenderId = sender,
		OperationId = 1,
	};
}
