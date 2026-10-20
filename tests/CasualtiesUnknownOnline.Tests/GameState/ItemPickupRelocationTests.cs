using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

/// <summary>
/// The kernel relocation a CONTAINER TAKE-OUT states (ticket
/// <c>pickup-early-return-kernel-relocation</c>): one item moves out of the container its row
/// still records it inside, into the reporting member's carried inventory, and keeps its
/// identity. These are PINS of the kernel's existing relocation and conflict verdicts — the fix
/// itself is the report the adapter now sends and the seam rules around it, tested over the wire
/// in <c>ContainerTakeOutTests</c>; this file states the kernel half those reports rely on, and
/// the container report's destructive walk they avoid.
/// <para>
/// The consequence the relocation prevents is the container report that follows: a container's
/// report states what the container HOLDS, and the domain destroys the descendants a report
/// leaves out (<c>DecideSyncContainer</c>). A child whose departure nobody stated is therefore
/// taken to <c>Terminal (ReplacedBy)</c> by the very next report about its parent — the last
/// case here is that shape.
/// </para>
/// </summary>
public class ItemPickupRelocationTests
{
	private const ulong Member = 2001;
	private const ulong OtherMember = 3001;

	private static ItemKernelAuthority AuthorityWithCarriedBag(ulong bagId, ulong childId, out ItemKernelAuthority authority)
	{
		authority = new ItemKernelAuthority(new RecordingLogger<ItemKernelAuthority>());
		var bag = new CharacterItemMsg { InstanceId = bagId, ItemId = "bag", Condition = 1f };
		Assert.True(authority.TrySpawn(Member, new ItemIdentity(bagId, "bag"), ItemLocation.Carried(new ActorId(Member)), bag, out _, out _));

		var withChild = new CharacterItemMsg
		{
			InstanceId = bagId,
			ItemId = "bag",
			Contents = [new CharacterItemMsg { InstanceId = childId, ItemId = "water", Condition = 0.5f }],
		};
		Assert.True(authority.TrySyncContainerFacts(Member, withChild, out _, out var rejection), rejection?.Message ?? "rejected without message");

		var child = authority.FindItem(childId)!.Value;
		Assert.Equal(ItemLocationKind.Contained, child.Location.Kind);
		Assert.Equal(bagId, child.Location.ParentItemId);
		return authority;
	}

	[Fact]
	public void APickup_OfAContainedChild_RelocatesItOutOfTheContainer()
	{
		var authority = AuthorityWithCarriedBag(50, 51, out _);

		Assert.True(authority.TryPickup(Member, 51, new ActorId(Member), out _, out var rejection), rejection?.Message ?? "rejected without message");

		var child = authority.FindItem(51)!.Value;
		Assert.Equal(ItemLocationKind.Carried, child.Location.Kind);
		Assert.Equal(Member, child.Location.Owner.Value);
		Assert.Equal(0ul, child.Location.ParentItemId);
	}

	/// <summary>
	/// Carried by the reporting member is still a CONFLICT, and that verdict is load-bearing: a
	/// pickup behind a LOST drop must be refused so the sender's copy rolls back and the older
	/// drop report (which the sender keeps queued, sync-coverage row I5) converges both sides.
	/// Accepting the second report as a re-assertion was tried and rejected — it let a stale
	/// pickup stand while the older report moved the item to the world underneath it.
	/// </summary>
	[Fact]
	public void APickup_OfAnItemTheReportingMemberAlreadyCarries_IsStillTheOlderRecordsConflict()
	{
		var authority = AuthorityWithCarriedBag(50, 51, out _);
		Assert.True(authority.TryPickup(Member, 51, new ActorId(Member), out _, out _));
		var before = authority.FindItem(51)!.Value;

		Assert.False(authority.TryPickup(Member, 51, new ActorId(Member), out _, out var rejection));
		Assert.Equal(RejectionReason.Conflict, rejection!.Reason);

		var after = authority.FindItem(51)!.Value;
		Assert.Equal(before.Location, after.Location);
		Assert.Equal(before.Revision, after.Revision);
	}

	[Fact]
	public void APickup_OfAnItemAnotherMemberCarries_IsStillAConflict()
	{
		var authority = new ItemKernelAuthority(new RecordingLogger<ItemKernelAuthority>());
		Assert.True(authority.TrySpawn(OtherMember, new ItemIdentity(60, "test_item"), ItemLocation.Carried(new ActorId(OtherMember)),
			new CharacterItemMsg { InstanceId = 60, ItemId = "test_item" }, out _, out _));

		Assert.False(authority.TryPickup(Member, 60, new ActorId(Member), out _, out var rejection));
		Assert.Equal(RejectionReason.Conflict, rejection!.Reason);
		var row = authority.FindItem(60)!.Value;
		Assert.Equal(OtherMember, row.Location.Owner.Value);
	}

	/// <summary>
	/// The mechanism this ticket exists for, in the kernel's own terms: once the child's arrival
	/// is stated, the container report that no longer names it cannot reach it — the destroy walk
	/// follows CONTAINED descendants only, and the child is carried now. Without the relocation
	/// the next report takes the child to Terminal, and Terminal is one-way.
	/// </summary>
	[Fact]
	public void AChildTakenOutOfAContainer_SurvivesTheNextContainerReport()
	{
		var authority = AuthorityWithCarriedBag(50, 51, out _);
		Assert.True(authority.TryPickup(Member, 51, new ActorId(Member), out _, out _));

		// The bag's next report states the bag and nothing else (the child is in a slot now).
		Assert.True(authority.TrySyncContainerFacts(Member, new CharacterItemMsg { InstanceId = 50, ItemId = "bag" }, out _, out var rejection),
			rejection?.Message ?? "rejected without message");

		var child = authority.FindItem(51)!.Value;
		Assert.Equal(ItemLocationKind.Carried, child.Location.Kind);
		Assert.Equal(Member, child.Location.Owner.Value);
	}
}
