using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The CONTAINER TAKE-OUT report over the wire (ticket
/// <c>pickup-early-return-kernel-relocation</c>): one item released out of a container into the
/// reporting member's carried inventory. It is the same kernel relocation a pickup is, with its
/// own verdicts because the item is IN the reporting member's hands — so a refusal settles by
/// the carried-set statement and never by the pickup rollback that unparents the item into the
/// world.
/// </summary>
[Trait("Category", "Integration")]
public class ContainerTakeOutTests
{
	private static CharacterItemMsg Item(ulong instanceId, float condition = 1f) =>
		new() { InstanceId = instanceId, ItemId = "test_item", Condition = condition };

	/// <summary>
	/// A take-out is a RELOCATION, not a creation claim: an id this host never judged has
	/// nothing to move. The answer is the reason the carried-set statement settles, not the
	/// protocol-violation answer, and nothing is created — a relocation that materialized its
	/// own target would be the creation claim this kind does not carry (its payload is an
	/// identity and an owner, with no data to state a row from).
	/// </summary>
	[Fact]
	public void ATakeOutForAnUnjudgedItem_IsAnsweredWithTheStatementReason_AndCreatesNothing()
	{
		using var w = ItemSimWorld.Create();

		w.TakeOut(w.G1, 900, Item(900));
		w.Driver.Tick(33);

		var verdict = Assert.Single(w.CommandRejections(w.G1));
		Assert.Equal(RejectionReason.UnclaimedItem, (RejectionReason)verdict.RejectionReason);
		Assert.Equal(WireCommandKind.ItemContainerTakeOut, verdict.RefusedKind);
		Assert.Null(w.KernelRow(900));
		// The way back is the absolute carried-set statement, which carries the item AND its
		// place; the pickup family's rollback would unparent the only copy into the world.
		Assert.True(w.RegistrationDue(w.G1), "the refusal makes the next carried-set report due now");
		Assert.Empty(w.Rejects(w.G1));
	}

	/// <summary>
	/// The shape the report exists for: the child's row is the container's, so the slot report
	/// (data only) cannot move it — the take-out does. On a member whose carried set was judged
	/// (the normal case, the child was registered with the statement).
	/// </summary>
	[Fact]
	public void ATakeOutOfAContainedChild_MovesTheRowOutOfTheContainer()
	{
		using var w = ItemSimWorld.Create();
		var bag = Item(50);
		bag.Contents.Add(Item(51));
		w.CarriedSet(w.G1, bag);
		w.Driver.Tick(33);
		Assert.Equal(ItemLocationKind.Contained, w.KernelRow(51)!.Value.Location.Kind);

		w.TakeOut(w.G1, 51, Item(51));
		w.Slot(w.G1, 51, 3, Item(51));
		w.Driver.Tick(33);

		var row = Assert.NotNull(w.KernelRow(51));
		Assert.Equal(ItemLocationKind.Carried, row.Location.Kind);
		Assert.Equal(w.G1.SteamId, row.Location.Owner.Value);
		Assert.Equal(0, w.CommandRejectedFrames(w.G1));
	}

	/// <summary>
	/// And the container report that follows no longer reaches the child: the destroy walk
	/// follows CONTAINED descendants, and the child is carried. This is the whole point of the
	/// ticket — without the relocation, the bag's next report takes the child to
	/// <c>Terminal (ReplacedBy)</c>, which no later command may revive.
	/// </summary>
	[Fact]
	public void AChildTakenOutOfAContainer_SurvivesTheNextContainerReport()
	{
		using var w = ItemSimWorld.Create();
		var bag = Item(50);
		bag.Contents.Add(Item(51));
		w.CarriedSet(w.G1, bag);
		w.Driver.Tick(33);

		w.TakeOut(w.G1, 51, Item(51));
		w.Slot(w.G1, 51, 3, Item(51));
		w.Driver.Tick(33);

		// The bag's next report states the bag and nothing else (the child sits in a slot now).
		w.ContainerSync(w.G1, 50, Item(50));
		w.Driver.Tick(33);

		var row = Assert.NotNull(w.KernelRow(51));
		Assert.Equal(ItemLocationKind.Carried, row.Location.Kind);
		Assert.Equal(w.G1.SteamId, row.Location.Owner.Value);
	}

	/// <summary>
	/// The APPLIED half of the same survival: the wound-view apply destroys the item without unloading
	/// it, so the kernel row is still Contained when the limb report arrives. The seam admits it (the
	/// chain root is the sender's own bag), the domain relocates it, and the bag's next report no longer
	/// reaches it — the stale walk follows CONTAINED descendants only, and this row is Applied.
	/// </summary>
	[Fact]
	public void AnAppliedContainerChild_IsRelocatedAndSurvivesTheNextContainerReport()
	{
		using var w = ItemSimWorld.Create();
		var bag = Item(50);
		bag.Contents.Add(Item(51));
		w.CarriedSet(w.G1, bag);
		w.Driver.Tick(33);
		Assert.Equal(ItemLocationKind.Contained, w.KernelRow(51)!.Value.Location.Kind);

		w.ApplyLimb(w.G1, 51, limbIndex: 3);
		w.Driver.Tick(33);

		var applied = Assert.NotNull(w.KernelRow(51));
		Assert.Equal(ItemLocationKind.Applied, applied.Location.Kind);
		Assert.Equal(w.G1.SteamId, applied.Location.Owner.Value);
		Assert.Equal(3, applied.Location.LimbIndex);
		Assert.Equal(0, w.CommandRejectedFrames(w.G1));

		// The bag's next report states the bag and nothing else; the installed child is not a descendant.
		w.ContainerSync(w.G1, 50, Item(50));
		w.Driver.Tick(33);

		var after = Assert.NotNull(w.KernelRow(51));
		Assert.Equal(ItemLocationKind.Applied, after.Location.Kind);
		Assert.Equal(3, after.Location.LimbIndex);
	}

	/// <summary>
	/// A take-out of an item the host already records as THAT member's carried item is refused
	/// by the domain (the conflict a pickup gets, and for the same reason: the record is behind a
	/// real report). The settlement is what makes this kind different: the refusal re-states the
	/// carried set instead of rolling the item into the world, so a lagging record costs one
	/// statement round trip and never the item.
	/// </summary>
	[Fact]
	public void ATakeOutOfAnAlreadyCarriedItem_IsRefused_AndSettlesByTheStatement()
	{
		using var w = ItemSimWorld.Create();
		Assert.True(w.StageKernelRow(w.G1.SteamId, 60, "test_item", ItemLocation.Carried(new ActorId(w.G1.SteamId)), Item(60)));
		w.CarriedSet(w.G1, Item(42));
		w.Driver.Tick(33);
		Assert.False(w.RegistrationDue(w.G1), "no window is open before the refusal");

		w.TakeOut(w.G1, 60, Item(60));
		w.Driver.Tick(33);

		var verdict = Assert.Single(w.CommandRejections(w.G1));
		Assert.Equal(RejectionReason.Conflict, (RejectionReason)verdict.RejectionReason);
		Assert.Equal(WireCommandKind.ItemContainerTakeOut, verdict.RefusedKind);
		Assert.True(w.RegistrationDue(w.G1), "the refusal makes the next carried-set report due now");
		Assert.Empty(w.Rejects(w.G1)); // never the pickup rollback
		var row = Assert.NotNull(w.KernelRow(60));
		Assert.Equal(ItemLocationKind.Carried, row.Location.Kind);
	}

	/// <summary>
	/// The report is bounded by what the host's record already says (the census rule: a member's
	/// report is checked against a fact the receiver holds). An item the record puts inside
	/// ANOTHER member's container is not this sender's to take out, and the answer is the one
	/// that reaches the take-out's settlement (the carried-set statement) rather than the pickup
	/// rollback.
	/// </summary>
	[Fact]
	public void ATakeOutMayNotMoveAnotherMembersContainedItem()
	{
		using var w = ItemSimWorld.Create();
		var theirs = Item(80);
		theirs.Contents.Add(Item(81));
		w.CarriedSet(w.G2, theirs);
		w.Driver.Tick(33);
		Assert.Equal(ItemLocationKind.Contained, w.KernelRow(81)!.Value.Location.Kind);

		w.TakeOut(w.G1, 81, Item(81));
		w.Driver.Tick(33);

		var verdict = Assert.Single(w.CommandRejections(w.G1));
		Assert.Equal(RejectionReason.NotAuthorized, (RejectionReason)verdict.RejectionReason);
		Assert.Equal(WireCommandKind.ItemContainerTakeOut, verdict.RefusedKind);
		Assert.Empty(w.Rejects(w.G1));
		var row = Assert.NotNull(w.KernelRow(81));
		Assert.Equal(ItemLocationKind.Contained, row.Location.Kind);
		Assert.Equal(80ul, row.Location.ParentItemId);
		Assert.Equal(w.G2.SteamId, row.Location.Owner.Value);
	}

	/// <summary>
	/// The eligibility rule reads the row the child SITS IN, never the child's own recorded owner — and
	/// this is the case that tells them apart. A bag taken from another member moves the bag's ROW only
	/// (the child's `Contained` row keeps the previous holder, because the kernel stamps that owner at
	/// creation and never re-stamps it), yet the child is inside the SENDER's bag now, so its take-out is
	/// ordinary play. A rule that read the child's owner would refuse the receiver's own bag, and the
	/// bag's next report would then terminalize the child — the very outcome this report exists to stop.
	/// </summary>
	[Fact]
	public void ATakeOutOutOfABagThatChangedHands_IsAccepted()
	{
		using var w = ItemSimWorld.Create();
		Assert.True(w.StageKernelRow(w.G2.SteamId, 80, "test_item", ItemLocation.Carried(new ActorId(w.G2.SteamId)), Item(80)));
		Assert.True(w.StageKernelRow(w.G2.SteamId, 81, "test_item", ItemLocation.Contained(new ActorId(w.G2.SteamId), 80), Item(81)));

		// The cross-player take: the host's own commit relocates the container's ROOT only.
		w.Items.AdoptTransferredItem(w.G1.SteamId, 80, Item(80));
		Assert.Equal(w.G1.SteamId, w.KernelRow(80)!.Value.Location.Owner.Value);
		Assert.Equal(w.G2.SteamId, w.KernelRow(81)!.Value.Location.Owner.Value); // the stale owner the rule must not read

		w.TakeOut(w.G1, 81, Item(81));
		w.Driver.Tick(33);

		Assert.Equal(0, w.CommandRejectedFrames(w.G1));
		var row = Assert.NotNull(w.KernelRow(81));
		Assert.Equal(ItemLocationKind.Carried, row.Location.Kind);
		Assert.Equal(w.G1.SteamId, row.Location.Owner.Value);
	}

	/// <summary>
	/// The same eligibility rule on the family's OTHER kind, because the two are one kernel command: a
	/// plain pickup may not take an item out of another member's container either. Refused and ANSWERED,
	/// so the family's own settlement still runs (the item reject the adapter rolls back on) — the guard
	/// closes the alias, not the answer.
	/// </summary>
	[Fact]
	public void APlainPickupMayNotTakeAnotherMembersContainedItemEither()
	{
		using var w = ItemSimWorld.Create();
		var theirs = Item(80);
		theirs.Contents.Add(Item(81));
		w.CarriedSet(w.G2, theirs);
		w.Driver.Tick(33);

		w.Pickup(w.G1, 81, Item(81));
		w.Driver.Tick(33);

		var verdict = Assert.Single(w.CommandRejections(w.G1));
		Assert.Equal(RejectionReason.NotAuthorized, (RejectionReason)verdict.RejectionReason);
		Assert.Equal(WireCommandKind.ItemPickup, verdict.RefusedKind);
		var reject = Assert.Single(w.Rejects(w.G1));
		Assert.Equal(81ul, reject.ItemId);
		var row = Assert.NotNull(w.KernelRow(81));
		Assert.Equal(ItemLocationKind.Contained, row.Location.Kind);
		Assert.Equal(80ul, row.Location.ParentItemId);
		Assert.Equal(w.G2.SteamId, row.Location.Owner.Value);
	}

	/// <summary>
	/// The WORLD home's production shape: a child of a container that lies on the ground is a WORLD row
	/// carrying the container id (the drop report into a container sets it), so looting the container is
	/// a relocation off a world row — the same statement, under the pickup's kind (the policy's own
	/// table, because a refused world item belongs back in the world, not behind a re-stated carried
	/// set). The world table's own row is removed by the pickup projection, which the world-pickup
	/// cases already pin; this case pins the kernel arm the new report depends on.
	/// </summary>
	[Fact]
	public void ATakeOutOfAWorldContainersChild_RelocatesTheWorldRow()
	{
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 70, Item(70));
		w.Driver.Tick(33);
		Assert.True(w.HostTable(70), "the world container must exist before this case means anything");
		Assert.True(w.StageKernelRow(w.G1.SteamId, 71, "test_item", ItemLocation.World(10, 10, 70), Item(71)));

		w.TakeOut(w.G1, 71, Item(71));
		w.Driver.Tick(33);

		var row = Assert.NotNull(w.KernelRow(71));
		Assert.Equal(ItemLocationKind.Carried, row.Location.Kind);
		Assert.Equal(w.G1.SteamId, row.Location.Owner.Value);
		Assert.False(w.HostTable(71), "a carried item is not a world row");
	}

	/// <summary>
	/// The trait that keeps the family apart: the PLAIN pickup of an unjudged id is still the
	/// protocol violation it always was (its local copy may be a world object the host refused,
	/// so the pickup rollback is the right settlement) — the take-out's own answer must not leak
	/// into it.
	/// </summary>
	[Fact]
	public void APlainPickupForAnUnjudgedItem_KeepsItsProtocolViolationAnswer()
	{
		using var w = ItemSimWorld.Create();

		w.Pickup(w.G1, 901, Item(901));
		w.Driver.Tick(33);

		var reject = Assert.Single(w.Rejects(w.G1));
		Assert.Equal(901ul, reject.ItemId);
		Assert.Equal(ItemRejectMsg.Reason.UnknownItem, reject.Rejection);
		Assert.Single(w.CommandRejections(w.G1), verdict => verdict.RefusedKind == WireCommandKind.ItemPickup);
	}
}
