using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The admission seam against the kernel's own verdicts, through the production
/// composition root: the seam sits at the position the old destroy check held,
/// so a command it ADMITS still gets the kernel's answer, and the shape it
/// refuses silently still produces no answer at all. These two facts are the
/// "no refusal is masked, no new answer appears" half of the seam's contract;
/// <c>KernelCommandGatewayTests</c> pins the seam itself.
/// </summary>
[Trait("Category", "Integration")]
public class CommandAdmissionIntegrationTests
{
	private static CharacterItemMsg Item() => new() { ItemId = "test_item", Condition = 1f };

	[Fact]
	public void NonOwnerDrop_IsStillRefusedByTheKernelWithItsOwnReason()
	{
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(33);
		Assert.True(w.TransferredOf(w.G1, 42), "setup: G1 owns the item");

		// G2 drops G1's carried item. Ownership of a drop is NOT the admission
		// seam's judgement — the item domain refuses it — so the kernel's answer
		// must still reach the sender unchanged.
		w.Drop(w.G2, 42, Item());
		w.Driver.Tick(33);

		Assert.True(w.Rejects(w.G2).Any(reject => reject.ItemId == 42), "the kernel's drop refusal still reaches the sender");
		Assert.Empty(w.Rejects(w.G1));
		Assert.True(w.TransferredOf(w.G1, 42), "the refused drop leaves the owner's transfer entry alone");
	}

	[Fact]
	public void NonOwnerDestroy_ProducesNoAnswerAtAll()
	{
		using var w = ItemSimWorld.Create();
		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(33);
		Assert.True(w.TransferredOf(w.G1, 42), "setup: G1 owns the item");

		// The shape the admission seam drops without answering: answering it
		// would start a guest rollback that never happened before the seam
		// existed (the remote-clone display proxy case, ItemDestroyAuthorityTests).
		w.Destroy(w.G2, 42);
		w.Driver.Tick(33);

		Assert.Empty(w.Rejects(w.G2));
		Assert.True(w.TransferredOf(w.G1, 42), "the non-owner destroy must not remove the owner's transfer entry");
	}

	[Fact]
	public void DestroyOfAnItemNobodyReported_IsRefusedByTheCreationBeforeOperationInvariant()
	{
		using var w = ItemSimWorld.Create();

		// The id was never reported by anyone. The handler's creation-before-
		// operation rule runs ABOVE the admission seam and refuses it, so the
		// member is answered with that verdict: the seam's own "unjudged id" guard
		// is defensive, not the production path for this input
		// (KernelCommandGatewayTests pins the guard in isolation).
		w.Destroy(w.G2, 4242);
		w.Driver.Tick(33);

		var reject = Assert.Single(w.Rejects(w.G2));
		Assert.Equal(4242ul, reject.ItemId);
		Assert.Equal(ItemRejectMsg.Reason.UnknownItem, reject.Rejection);
	}
}
