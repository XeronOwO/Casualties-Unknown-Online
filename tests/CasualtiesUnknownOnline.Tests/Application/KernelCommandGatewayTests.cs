using System.Collections.Generic;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Application;

/// <summary>
/// The admission seam (Application layer), in isolation: a member's submission
/// is judged by the actor/sender binding, the declared authority policy and the
/// item-destroy eligibility rule — and by nothing else. The end-to-end halves
/// (the host's real command path and the guest's rollback) are pinned by
/// <c>ItemDestroyAuthorityTests</c>, <c>KernelProtocolServiceTests</c> and the
/// item simulations, which run the same production composition root.
/// </summary>
public class KernelCommandGatewayTests
{
	private const ulong Member = 2001;
	private const ulong OtherMember = 3001;

	[Fact]
	public void CommandWhoseActorIsNotTheSender_IsRefusedAndAnswered()
	{
		var decision = Gateway().AdmitMemberSubmission(Member,
			Destroy(instanceId: 42, actor: OtherMember, AuthorityKind.OwnerPredictedHostValidated));

		Assert.Equal(CommandAdmissionKind.Refused, decision.Kind);
		Assert.Equal(RejectionReason.NotAuthorized, decision.Reason);
		Assert.True(decision.AnswersSender, "a member that submits under another actor's name is told why");
	}

	[Fact]
	public void HostOnlyCommandFromAMember_IsRefusedAndAnswered()
	{
		var command = new ResetWorldItemsCommand(new OperationId(1), new ActorId(Member), new RunEpoch(1), AuthorityKind.HostOnly);

		var decision = Gateway().AdmitMemberSubmission(Member, command);

		Assert.Equal(CommandAdmissionKind.Refused, decision.Kind);
		Assert.Equal(RejectionReason.NotAuthorized, decision.Reason);
	}

	[Fact]
	public void PresentationOnlyCommandFromAMember_IsRefusedAndAnswered()
	{
		var command = new DropItemCommand(
			new OperationId(1), new ActorId(Member), new RunEpoch(1), AuthorityKind.PresentationOnly, 42, ItemLocation.World(1f, 2f), 0);

		var decision = Gateway().AdmitMemberSubmission(Member, command);

		Assert.Equal(CommandAdmissionKind.Refused, decision.Kind);
		Assert.Equal(RejectionReason.NotAuthorized, decision.Reason);
	}

	[Fact]
	public void OwnerPredictedCommandFromItsOwnActor_IsAdmitted()
	{
		var command = new DropItemCommand(
			new OperationId(1), new ActorId(Member), new RunEpoch(1), AuthorityKind.OwnerPredictedHostValidated, 42, ItemLocation.World(1f, 2f), 0);

		Assert.True(Gateway().AdmitMemberSubmission(Member, command).IsAdmitted,
			"an owner-predicted command from its own actor is the normal member submission");
	}

	[Fact]
	public void ObservedCommandFromAMember_IsAdmitted()
	{
		var command = new DropItemCommand(
			new OperationId(1), new ActorId(Member), new RunEpoch(1), AuthorityKind.TriggerObservedHostCommitted, 42, ItemLocation.World(1f, 2f), 0);

		Assert.True(Gateway().AdmitMemberSubmission(Member, command).IsAdmitted,
			"the observing member submits and the host commits");
	}

	[Fact]
	public void DestroyOfAnotherMembersCarriedItem_IsIgnoredWithoutAnAnswer()
	{
		var facts = new FakeItemFacts();
		facts.CarriedBy(42, OtherMember);

		var decision = Gateway(facts).AdmitMemberSubmission(Member,
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

		Assert.True(Gateway(facts).AdmitMemberSubmission(Member,
			Destroy(instanceId: 42, actor: Member, AuthorityKind.OwnerPredictedHostValidated)).IsAdmitted);
	}

	[Fact]
	public void DestroyOfAWorldItem_IsAdmitted()
	{
		var facts = new FakeItemFacts();
		facts.InWorld(42);

		Assert.True(Gateway(facts).AdmitMemberSubmission(Member,
			Destroy(instanceId: 42, actor: Member, AuthorityKind.OwnerPredictedHostValidated)).IsAdmitted);
	}

	[Fact]
	public void DestroyOfAnIdThisHostNeverJudged_IsNotTheSeamsToAnswer()
	{
		// A conservative guard, not the production path: on the real path the
		// handler's creation-before-operation invariant runs above this seam and
		// refuses an unjudged id itself (CommandAdmissionIntegrationTests pins
		// that). The seam must never INVENT a verdict for state it does not hold.
		var decision = Gateway().AdmitMemberSubmission(Member,
			Destroy(instanceId: 42, actor: Member, AuthorityKind.OwnerPredictedHostValidated));

		Assert.True(decision.IsAdmitted, "the seam holds no judgement for an unjudged id, so it must not produce one");
	}

	[Fact]
	public void Refusal_IsAuditedOnceWithTheCommandActorSenderAndReason()
	{
		var logger = new RecordingLogger<KernelCommandGateway>();

		Gateway(logger).AdmitMemberSubmission(Member,
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

		Gateway(logger).AdmitMemberSubmission(Member,
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

		Gateway(facts, logger).AdmitMemberSubmission(Member,
			Destroy(instanceId: 4242, actor: Member, AuthorityKind.OwnerPredictedHostValidated));

		var entry = Assert.Single(logger.Entries);
		Assert.Contains("4242", entry.Message);
		Assert.Contains(nameof(RejectionReason.NotAuthorized), entry.Message);
	}

	private static KernelCommandGateway Gateway() => new(new FakeItemFacts(), new RecordingLogger<KernelCommandGateway>());

	private static KernelCommandGateway Gateway(FakeItemFacts facts) => new(facts, new RecordingLogger<KernelCommandGateway>());

	private static KernelCommandGateway Gateway(RecordingLogger<KernelCommandGateway> logger) => new(new FakeItemFacts(), logger);

	private static KernelCommandGateway Gateway(FakeItemFacts facts, RecordingLogger<KernelCommandGateway> logger) => new(facts, logger);

	private static DestroyItemCommand Destroy(ulong instanceId, ulong actor, AuthorityKind authority) =>
		new(new OperationId(1), new ActorId(actor), new RunEpoch(1), authority, instanceId, TerminalKind.Consumed, 0);

	/// <summary>The kernel's item read model, faked: the gateway only ever reads a location.</summary>
	private sealed class FakeItemFacts : IKernelItemFacts
	{
		private readonly Dictionary<ulong, ItemState> _items = [];

		public ItemState? FindItem(ulong instanceId) => _items.TryGetValue(instanceId, out var item) ? item : null;

		internal void CarriedBy(ulong instanceId, ulong owner) =>
			_items[instanceId] = new ItemState(new ItemIdentity(instanceId, "test_item"), 1, ItemLocation.Carried(new ActorId(owner)));

		internal void InWorld(ulong instanceId) =>
			_items[instanceId] = new ItemState(new ItemIdentity(instanceId, "test_item"), 1, ItemLocation.World(1f, 2f));
	}
}
