using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Application;

/// <summary>
/// The admission seam's MEMBER-IDENTITY family: a report that puts an item into a
/// member's hands or writes enemy damage onto a member's body must be THAT
/// member's own report. The transport sender is the trusted fact (the same rule
/// <c>CraftSyncService</c> already writes down: "the transport sender is the
/// trusted fact"), while the identity the payload carries is a claim — and these
/// two families keyed their write on the claim.
///
/// <para>
/// The item half: <c>ItemDomainModule.DecidePickup</c> commits
/// <c>ItemLocation.Carried(command.NewOwner)</c> with nothing comparing that owner
/// to the sender, and the seam's eligibility family covered a destroy report, a
/// state report and a container sync but not this kind — so a report could put a
/// world item into another member's hands.
/// </para>
///
/// <para>
/// The enemy half: the entity domain accepts a bite, a lunge and an effect
/// outright, so the victim, the limb and the magnitudes were the reporter's own.
/// The design intent is that the AFFECTED side's client is the judge
/// (<c>review/health-simulation-and-host-authority.md</c>), and every production
/// sender names itself — <c>EnemyCombatReplay</c> and <c>EnemyProximitySync</c>
/// both set <c>msg.VictimSteamId = _session.LocalSteamId</c> — which is exactly
/// why the victim identity has to be the sender rather than a field.
/// </para>
///
/// <para>
/// Both are the same verdict shape the seam already uses for another member's
/// item: IGNORED, with nothing answered, because the reporter's own copy is not
/// the fact a refusal would have to settle and the ordinary snapshot converges
/// it. The audit line is the only trace, so it names what the report claimed.
/// </para>
/// </summary>
public class KernelMemberIdentityReportTests
{
	private const ulong Member = 2001;
	private const ulong OtherMember = 3001;

	[Fact]
	public void PickupIntoTheSendersOwnHands_IsAdmitted()
	{
		var facts = new FakeItemFacts();
		facts.InWorld(42);

		Assert.True(Gateway(facts).Admit(Member, Pickup(42, actor: Member, newOwner: Member)).IsAdmitted,
			"the member that picked a world item up is the one that reports it");
	}

	[Fact]
	public void PickupIntoAnotherMembersHands_IsIgnoredWithoutAnAnswer()
	{
		// The gap the census row named: the seam bound the actor to the sender and
		// the domain checked existence, a terminal row, an installed item and the
		// revision — but nothing tied the DESTINATION to the sender, so a report
		// could hand a world item to somebody else.
		var facts = new FakeItemFacts();
		facts.InWorld(42);

		var decision = Gateway(facts).Admit(Member, Pickup(42, actor: Member, newOwner: OtherMember));

		Assert.Equal(CommandAdmissionKind.Ignored, decision.Kind);
		Assert.Equal(RejectionReason.NotAuthorized, decision.Reason);
		Assert.False(decision.AnswersSender, "the reporter's own picture is not the fact a refusal would settle; the reconciliation budget, not a verdict, is what bounds the repeat");
	}

	[Fact]
	public void BiteOnTheSendersOwnBody_IsAdmitted()
	{
		Assert.True(Gateway().Admit(Member, Bite(actor: Member, victim: Member)).IsAdmitted,
			"the bitten member's client is the judge of its own body, so its own report must reach the kernel");
	}

	[Fact]
	public void BiteNamingAnotherMembersBody_IsIgnoredWithoutAnAnswer()
	{
		var decision = Gateway().Admit(Member, Bite(actor: Member, victim: OtherMember));

		Assert.Equal(CommandAdmissionKind.Ignored, decision.Kind);
		Assert.Equal(RejectionReason.NotAuthorized, decision.Reason);
		Assert.False(decision.AnswersSender);
	}

	[Fact]
	public void LungeNamingAnotherMembersBody_IsIgnored() =>
		Assert.Equal(CommandAdmissionKind.Ignored,
			Gateway().Admit(Member, Lunge(actor: Member, victim: OtherMember)).Kind);

	[Fact]
	public void EffectNamingAnotherMembersBody_IsIgnored() =>
		Assert.Equal(CommandAdmissionKind.Ignored,
			Gateway().Admit(Member, Effect(actor: Member, victim: OtherMember)).Kind);

	[Fact]
	public void EnemyReportsOnTheSendersOwnBody_AreAllAdmitted()
	{
		// The whole family in one reading: the three magnitudes are the reporter's
		// own only when the body is too, so the bound is the same for each kind.
		Assert.True(Gateway().Admit(Member, Lunge(actor: Member, victim: Member)).IsAdmitted);
		Assert.True(Gateway().Admit(Member, Effect(actor: Member, victim: Member)).IsAdmitted);
	}

	[Fact]
	public void IgnoredEnemyReport_IsAuditedWithTheVictimItNamed()
	{
		var logger = new RecordingLogger<KernelCommandGateway>();

		Gateway(logger).Admit(Member, Bite(actor: Member, victim: 4242));

		var entry = Assert.Single(logger.Entries);
		Assert.Equal(LogLevel.Warning, entry.Level);
		Assert.Contains("RecordEnemyBiteCommand", entry.Message);
		Assert.Contains("4242", entry.Message);
		Assert.Contains(nameof(RejectionReason.NotAuthorized), entry.Message);
	}

	[Fact]
	public void IgnoredPickupReport_IsAuditedWithTheOwnerItNamed()
	{
		var logger = new RecordingLogger<KernelCommandGateway>();
		var facts = new FakeItemFacts();
		facts.InWorld(4242);

		Gateway(facts, logger).Admit(Member, Pickup(4242, actor: Member, newOwner: OtherMember));

		var entry = Assert.Single(logger.Entries);
		Assert.Contains("PickUpItemCommand", entry.Message);
		Assert.Contains("4242", entry.Message);
		Assert.Contains(nameof(RejectionReason.NotAuthorized), entry.Message);
	}

	private static KernelCommandGateway Gateway() => new(new FakeItemFacts(), new FakeMemberCommandRules(), new FakeItemUseFacts(), new RecordingLogger<KernelCommandGateway>());

	private static KernelCommandGateway Gateway(FakeItemFacts facts) => new(facts, new FakeMemberCommandRules(), new FakeItemUseFacts(), new RecordingLogger<KernelCommandGateway>());

	private static KernelCommandGateway Gateway(FakeItemFacts facts, RecordingLogger<KernelCommandGateway> logger) => new(facts, new FakeMemberCommandRules(), new FakeItemUseFacts(), logger);

	private static KernelCommandGateway Gateway(RecordingLogger<KernelCommandGateway> logger) => new(new FakeItemFacts(), new FakeMemberCommandRules(), new FakeItemUseFacts(), logger);

	private static PickUpItemCommand Pickup(ulong instanceId, ulong actor, ulong newOwner) =>
		new(new OperationId(1), new ActorId(actor), new RunEpoch(1), AuthorityKind.OwnerPredictedHostValidated,
			instanceId, new ActorId(newOwner), 0);

	private static RecordEnemyBiteCommand Bite(ulong actor, ulong victim) =>
		new(new OperationId(1), new ActorId(actor), new RunEpoch(1), AuthorityKind.TriggerObservedHostCommitted,
			victim, Limb(), 3f, 75f, -0.5f);

	private static RecordEnemyLungeCommand Lunge(ulong actor, ulong victim) =>
		new(new OperationId(1), new ActorId(actor), new RunEpoch(1), AuthorityKind.TriggerObservedHostCommitted,
			victim, Limb(), 70f, 100f);

	private static RecordEnemyEffectCommand Effect(ulong actor, ulong victim) =>
		new(new OperationId(1), new ActorId(actor), new RunEpoch(1), AuthorityKind.TriggerObservedHostCommitted,
			victim, EnemyCombatEffectKind.GrabberGrabbed, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 20f, 0.5f);

	private static EnemyCombatLimb Limb() => new()
	{
		Index = 2,
		SkinHealth = 80f,
		MuscleHealth = 90f,
		BleedAmount = 4f,
		Pain = 12f,
		Infected = true,
		InfectionAmount = 1f,
	};
}
