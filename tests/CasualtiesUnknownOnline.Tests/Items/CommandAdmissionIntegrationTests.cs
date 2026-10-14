using System.Linq;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The admission seam against the kernel's own verdicts, through the production
/// composition root: the seam sits at the position the old destroy check held,
/// so a command it ADMITS still gets the kernel's answer, and the shape it
/// refuses silently still produces no answer at all. These two facts are the
/// "no refusal is masked, no new answer appears" half of the seam's contract;
/// <c>KernelCommandGatewayTests</c> pins the seam itself.
///
/// <para>
/// The three cases below are the seam's three verdict families end to end, each
/// against the HOST's own kernel row: a host-only kind refused, another member's
/// item state refused, and another member's container sync refused. Each also
/// reads the seam's own audit line — for a "no answer" verdict that line is the
/// only trace the dropped report leaves.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class CommandAdmissionIntegrationTests
{
	private static CharacterItemMsg Item() => new() { ItemId = "test_item", Condition = 1f };

	private static CharacterItemMsg Container() => new() { ItemId = "test_container", Condition = 0.8f };

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

	[Fact]
	public void AHostOnlyReportFromAMember_NeverReachesItsDomain()
	{
		var gatewayLog = new RecordingLogger<KernelCommandGateway>();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<ILogger<KernelCommandGateway>>(gatewayLog));
		var revisionBefore = KernelRevision(w);

		// A world-entity health fact is the host's to record (matrix E7): a guest
		// reports the underlying game event on its own message, never this command.
		// The world-entity domain would ACCEPT this report, which is what makes the
		// seam the only thing between a member and the fact — so the observable is
		// that nothing committed. Before the per-kind declaration every mapped wire
		// kind read OwnerPredictedHostValidated, and this report committed.
		Report(w.G2, new WireCommand
		{
			Kind = WireCommandKind.RecordBuildingEntityHealth,
			EntityPosition = new WireEntityPosition { X = 5, Y = 6 },
			Health = 50f,
		}, WirePayloadType.RecordBuildingEntityHealthCommand);
		w.Driver.Tick(33);

		// The sender is answered (the refusal rides its own kernel envelope), the
		// refusal is the seam's own named line, and the host's kernel did not move.
		Assert.Contains(w.Rejects(w.G2), reject => reject.Rejection == ItemRejectMsg.Reason.UnknownItem);
		Assert.True(gatewayLog.HasWarning("a host-only fact is authored by the host's own simulation"),
			"the refusal must be the SEAM's, not a domain verdict that happens to land the same way");
		Assert.Equal(revisionBefore, KernelRevision(w));
	}

	[Fact]
	public void AStateReportForAnotherMembersItem_ChangesNoRowAndIsNotAnswered()
	{
		var gatewayLog = new RecordingLogger<KernelCommandGateway>();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<ILogger<KernelCommandGateway>>(gatewayLog));
		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(33);
		Assert.True(w.TransferredOf(w.G1, 42), "setup: G1 owns the item");

		// The item domain's own update rule (ItemDomainModule.DecideUpdate) checks
		// existence, a terminal row and the revision — and the handler re-stamps the
		// revision from its own record, so that check cannot refuse this. Nothing
		// below the admission seam is a gate here; the row is what the seam protects.
		var before = KernelRow(w, 42).Data.Condition;
		Report(w.G2, new WireCommand
		{
			Kind = WireCommandKind.ItemUpdateState,
			Identity = new WireItemIdentity { InstanceId = 42, DefinitionId = "test_item" },
			Data = new WireItemData { Condition = 0.01f },
		}, WirePayloadType.ItemUpdateStateCommand);
		w.Driver.Tick(33);

		Assert.Equal(before, KernelRow(w, 42).Data.Condition);
		Assert.True(gatewayLog.HasWarning("so its state is not the sender's to report"));
		Assert.Empty(w.Rejects(w.G2));
	}

	[Fact]
	public void AContainerSyncForAnotherMembersContainer_MovesNothingAndIsNotAnswered()
	{
		var gatewayLog = new RecordingLogger<KernelCommandGateway>();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<ILogger<KernelCommandGateway>>(gatewayLog));
		w.Spawn(w.G1, 42, Container());
		w.Driver.Tick(33);
		w.Pickup(w.G1, 42, Container());
		w.Driver.Tick(33);
		w.Spawn(w.G1, 43, Item());
		w.Driver.Tick(33);
		w.Pickup(w.G1, 43, Item());
		w.Driver.Tick(33);
		Assert.True(w.TransferredOf(w.G1, 42) && w.TransferredOf(w.G1, 43), "setup: G1 owns the container and its child");

		// The container kind is the family's worst member: it reached the kernel
		// WITHOUT passing the admission seam, and DecideSyncContainer rewrites the
		// parent's data, moves every listed child into the reporter's own container
		// and destroys the descendants the report leaves out. So this one report
		// would rewrite G1's container and take G1's item — nothing below the seam
		// refuses either half.
		var parentBefore = KernelRow(w, 42).Data.Condition;
		Report(w.G2, new WireCommand
		{
			Kind = WireCommandKind.ItemContainerSync,
			Identity = new WireItemIdentity { InstanceId = 42, DefinitionId = "test_container" },
			Data = new WireItemData { Condition = 0.5f },
			ContainerChildren = [new WireContainerChild
			{
				Identity = new WireItemIdentity { InstanceId = 43, DefinitionId = "test_item" },
				ParentItemId = 42,
				Data = new WireItemData { Condition = 0.2f },
			}],
		}, WirePayloadType.ItemContainerSyncCommand);
		w.Driver.Tick(33);

		Assert.Equal(parentBefore, KernelRow(w, 42).Data.Condition);
		Assert.Equal(ItemLocationKind.Carried, KernelRow(w, 43).Location.Kind);
		Assert.Equal(w.G1.SteamId, KernelRow(w, 43).Location.Owner.Value);
		Assert.True(gatewayLog.HasWarning("so its contents are not the sender's to rewrite"));
		Assert.Empty(w.Rejects(w.G2));
	}

	/// <summary>
	/// The host's own rule for the console write, through the production composition
	/// root: the same report the seam admits for a native use reaches the host under
	/// the console kind, and the host's rule — closed, which is the default a host
	/// ships with — is what refuses it. The refusal is ANSWERED (the member's own
	/// local object already carries the value it reported), and the row the report
	/// would have written is untouched.
	/// </summary>
	[Fact]
	public void AConsoleWriteFromAMember_IsRefusedByTheHostRuleAndAnswered()
	{
		var gatewayLog = new RecordingLogger<KernelCommandGateway>();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<ILogger<KernelCommandGateway>>(gatewayLog));
		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(33);
		Assert.True(w.TransferredOf(w.G1, 42), "setup: G1 owns the item");

		var before = KernelRow(w, 42).Data.Condition;
		Report(w.G1, new WireCommand
		{
			Kind = WireCommandKind.ItemDataWrite,
			Identity = new WireItemIdentity { InstanceId = 42, DefinitionId = "test_item" },
			Data = new WireItemData { Condition = 0.01f },
		}, WirePayloadType.ItemDataWriteCommand);
		w.Driver.Tick(33);

		Assert.Equal(before, KernelRow(w, 42).Data.Condition);
		Assert.True(gatewayLog.HasWarning("the host's rule does not open the item-data write verbs"));
		Assert.Equal(1, w.CommandRejectedFrames(w.G1));
	}

	/// <summary>
	/// The other side: a host whose rule is open commits exactly what the member
	/// reported — the row a console write leaves is the row a native use leaves, which
	/// is why the kind exists only for the rule's sake.
	/// </summary>
	[Fact]
	public void AConsoleWriteFromAMember_CommitsWhenTheHostRuleIsOpen()
	{
		var rules = new MutableOptionsMonitor<HostRulesOptions>(new HostRulesOptions { AllowMemberItemDataWrite = true });
		using var w = ItemSimWorld.Create(services => services.Replace(
			ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(rules)));
		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(33);

		Report(w.G1, new WireCommand
		{
			Kind = WireCommandKind.ItemDataWrite,
			Identity = new WireItemIdentity { InstanceId = 42, DefinitionId = "test_item" },
			Data = new WireItemData { Condition = 0.25f },
		}, WirePayloadType.ItemDataWriteCommand);
		w.Driver.Tick(33);

		Assert.Equal(0.25f, KernelRow(w, 42).Data.Condition);
		Assert.Equal(0, w.CommandRejectedFrames(w.G1));
	}

	/// <summary>One raw kernel command, framed the way a member's own client frames it — the shape a modified client has, and the only way a test can report a kind no CUO gesture emits.</summary>
	private static void Report(TestNode node, WireCommand command, WirePayloadType payloadType) =>
		node.Services.GetRequiredService<IKernelProtocolControl>().SendCommand(command, payloadType);

	/// <summary>The host's committed revision — the observable a report must not move.</summary>
	private static ulong KernelRevision(ItemSimWorld world) =>
		world.Host.Services.GetRequiredService<IKernelCheckpointSource>().CurrentGlobalRevision;

	/// <summary>The host's own kernel row for one item (the record a member's report writes through).</summary>
	private static ItemState KernelRow(ItemSimWorld world, ulong itemId) =>
		world.Host.Services.GetRequiredService<IKernelItemFacts>().FindItem(itemId)!.Value;
}
