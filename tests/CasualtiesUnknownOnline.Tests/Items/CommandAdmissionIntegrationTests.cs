using System.Linq;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
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
/// The cases below are the seam's verdict families end to end, each against the
/// HOST's own kernel row: a host-only kind refused, another member's item state
/// refused, another member's container sync refused, the console verb judged by the
/// host's rule, and the native-use claim judged by the item's own data (with the
/// sender's local object settled back). Each also reads the seam's own audit line —
/// for a "no answer" verdict that line is the only trace the dropped report leaves.
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
	/// The host's cheat SWITCH, through the production composition root, on the side
	/// that matters for the user's ruling: a member that already HOLDS the grant is
	/// still refused while the switch is closed. The refusal is ANSWERED (the member's
	/// own local object already carries the value it reported), and the row the report
	/// would have written is untouched.
	/// </summary>
	[Fact]
	public void AConsoleWriteFromAPromotedMember_IsStillRefusedWhileTheSwitchIsClosed()
	{
		var gatewayLog = new RecordingLogger<KernelCommandGateway>();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<ILogger<KernelCommandGateway>>(gatewayLog));
		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(33);
		Assert.True(w.TransferredOf(w.G1, 42), "setup: G1 owns the item");
		Assert.True(w.GrantCheatVerbs(w.G1), "setup: the member holds the cheat grant — the switch is the only closed ground");

		var before = KernelRow(w, 42).Data.Condition;
		Report(w.G1, new WireCommand
		{
			Kind = WireCommandKind.ItemDataWrite,
			Identity = new WireItemIdentity { InstanceId = 42, DefinitionId = "test_item" },
			Data = new WireItemData { Condition = 0.01f },
		}, WirePayloadType.ItemDataWriteCommand);
		w.Driver.Tick(33);

		Assert.Equal(before, KernelRow(w, 42).Data.Condition);
		Assert.True(gatewayLog.HasWarning("cheat switch is closed to members"));
		Assert.Equal(1, w.CommandRejectedFrames(w.G1));
	}

	/// <summary>
	/// The ORDER of the two cheat rulings through the same composition root: with the
	/// switch closed AND the roster empty the verdict must name the switch, not the
	/// roster. Reversing the gateway's two checks would make the second assertion fail.
	/// </summary>
	[Fact]
	public void AConsoleWriteFromAMemberWithNeitherGrantNorSwitch_IsRefusedByTheSwitch()
	{
		var gatewayLog = new RecordingLogger<KernelCommandGateway>();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<ILogger<KernelCommandGateway>>(gatewayLog));
		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(33);
		Assert.True(w.TransferredOf(w.G1, 42), "setup: G1 owns the item");

		Report(w.G1, new WireCommand
		{
			Kind = WireCommandKind.ItemDataWrite,
			Identity = new WireItemIdentity { InstanceId = 42, DefinitionId = "test_item" },
			Data = new WireItemData { Condition = 0.01f },
		}, WirePayloadType.ItemDataWriteCommand);
		w.Driver.Tick(33);

		Assert.True(gatewayLog.HasWarning("cheat switch is closed to members"));
		Assert.False(gatewayLog.HasWarning("cheat-permission roster does not name this member"), "the switch's ground must win when both are shut");
		Assert.Equal(1, w.CommandRejectedFrames(w.G1));
	}

	/// <summary>
	/// The WHO ruling, through the same composition root: a member the host's cheat
	/// roster does not name is refused even with the switch open, the refusal is
	/// answered (its local object already carries the value), and the row is
	/// untouched. This is the ruling the switch sits BESIDE, not instead of.
	/// </summary>
	[Fact]
	public void AConsoleWriteFromAMemberTheRosterDoesNotName_IsRefusedByTheRosterAndAnswered()
	{
		var gatewayLog = new RecordingLogger<KernelCommandGateway>();
		var rules = new MutableOptionsMonitor<HostRulesOptions>(new HostRulesOptions { AllowMemberCheatVerbs = true });
		using var w = ItemSimWorld.Create(services =>
		{
			services.AddSingleton<ILogger<KernelCommandGateway>>(gatewayLog);
			services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(rules));
		});
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
		Assert.True(gatewayLog.HasWarning("cheat-permission roster does not name this member"));
		Assert.Equal(1, w.CommandRejectedFrames(w.G1));
	}

	/// <summary>
	/// The other side: a host whose switch is OPEN, admitting a member it has GRANTED,
	/// commits exactly what the member reported — the row a console write leaves is the
	/// row a native use leaves, which is why the kind exists only for the rulings' sake.
	/// </summary>
	[Fact]
	public void AConsoleWriteFromAMember_CommitsWhenTheSwitchIsOpenAndTheMemberIsGranted()
	{
		var rules = new MutableOptionsMonitor<HostRulesOptions>(new HostRulesOptions { AllowMemberCheatVerbs = true });
		using var w = ItemSimWorld.Create(services => services.Replace(
			ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(rules)));
		Assert.True(w.GrantCheatVerbs(w.G1), "setup: the member holds the cheat grant");
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

	/// <summary>
	/// The native-use bound end to end, against the host's own row and the sender's own
	/// local object: a report that CLAIMS a native use of an item whose own data says no
	/// use exists (<c>ItemInfo.usable</c> clear) is refused by name and ANSWERED, the
	/// host's record does not move, and the sender settles the state it had already
	/// applied back to the recorded one. It is deliberately not the pickup/block-drop
	/// rollback: that one destroys the local copy, and this report is not about a pickup.
	/// </summary>
	[Fact]
	public void ANativeUseOfAnItemWhoseOwnDataForbidsIt_IsRefusedAnsweredAndSettled()
	{
		var local = new FakeLocalItemAccess();
		var useFacts = new FakeItemUseFacts().Forbid("test_item");
		using var w = ItemSimWorld.Create(services =>
		{
			services.Replace(ServiceDescriptor.Singleton<IKernelItemUseFacts>(useFacts));
			services.Replace(ServiceDescriptor.Singleton<ILocalItemAccess>(local));
		});
		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(33);
		Assert.True(w.TransferredOf(w.G1, 42), "setup: G1 owns the item");
		Assert.Equal(1f, KernelRow(w, 42).Data.Condition, 3);

		var used = Item();
		used.InstanceId = 42;
		used.Condition = 0.5f;
		local.Hold(used); // the use already ran here, which is what a refusal has to settle
		w.Use(w.G1, 42, used);
		w.Driver.Tick(33);

		Assert.Equal(1f, KernelRow(w, 42).Data.Condition, 3);
		var verdict = Assert.Single(w.CommandRejections(w.G1));
		Assert.Equal(RejectionReason.UseNotPermitted, (RejectionReason)verdict.RejectionReason);
		Assert.Equal(WireCommandKind.ItemUse, verdict.RefusedKind);
		Assert.Equal(1f, local.Held(42)!.Condition, 3);
		Assert.Empty(w.Rejects(w.G1));
	}

	/// <summary>
	/// The other side of the same bound: the item's own data admits the use, so the
	/// ordinary native report is recorded and nothing is refused.
	/// </summary>
	[Fact]
	public void ANativeUseOfAnItemWhoseOwnDataPermitsIt_IsRecorded()
	{
		using var w = ItemSimWorld.Create(services =>
			services.Replace(ServiceDescriptor.Singleton<IKernelItemUseFacts>(new FakeItemUseFacts().Permit("test_item"))));
		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(33);

		var used = Item();
		used.Condition = 0.5f;
		w.Use(w.G1, 42, used);
		w.Driver.Tick(33);

		Assert.Equal(0.5f, KernelRow(w, 42).Data.Condition, 3);
		Assert.Empty(w.CommandRejections(w.G1));
	}

	/// <summary>
	/// And the read that FAILED is not a refusal: a definition this host's registry does
	/// not carry (a mod it does not have) is admitted as the pre-change behaviour, so the
	/// three-valued port cannot turn "no fact" into "unusable".
	/// </summary>
	[Fact]
	public void ANativeUseOfADefinitionThisHostCannotRead_IsRecorded()
	{
		using var w = ItemSimWorld.Create(services =>
			services.Replace(ServiceDescriptor.Singleton<IKernelItemUseFacts>(new FakeItemUseFacts().Unknown("test_item"))));
		w.Spawn(w.G1, 42, Item());
		w.Driver.Tick(33);
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(33);

		var used = Item();
		used.Condition = 0.5f;
		w.Use(w.G1, 42, used);
		w.Driver.Tick(33);

		Assert.Equal(0.5f, KernelRow(w, 42).Data.Condition, 3);
		Assert.Empty(w.CommandRejections(w.G1));
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
