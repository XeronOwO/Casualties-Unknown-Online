using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The admission seam for a command a MEMBER submitted over the wire: one owner
/// for "who may submit this" on the mapped kernel-command path. It sits in the
/// Application layer because eligibility is a session-level decision, not a
/// kernel-domain one.
///
/// <para>
/// SCOPE —the 2026-09-18 ruling binds it: the gateway decides ELIGIBILITY only
/// (who may submit), never what happens to another player's body, reach or
/// timing. Ownership here means "the item this member is reporting on is one it
/// may report on at all" —a world item (it saw the item in the world, or used
/// it in place) or the member's own carried, installed or consumed item —the
/// same rule the item domain applies to a drop; nothing about distance, state or
/// sequence is judged here, and the kernel still owns every domain verdict.
/// </para>
///
/// <para>
/// WHAT IS OUTSIDE IT: a guest's range request is answered in
/// <c>KernelProtocolService</c> and never becomes a command. The container-sync
/// report DOES reach this seam like any other member command —for a parent this
/// host already judged it is a member-authored fact, and the kernel's
/// <c>DecideSyncContainer</c> rewrites that parent's data, moves the listed
/// children in and destroys the parent's descendants the report left out —so it
/// has its own rule here. The one arm that stays the host's own materialization is
/// the MISSING-parent heal: nothing names that parent yet, so the domain creates
/// it as the reporter's own carried item, accept-first, exactly as before. The
/// handler's other heal (an update for an unknown carried id) does not pass here
/// either, and it names nothing this seam could judge.
/// </para>
///
/// <para>
/// ORDER is part of the contract, because a refusal that runs earlier can mask
/// one the kernel would have produced:
/// <list type="number">
/// <item>the command's actor must be the submitting member —the frame validator
/// already guarantees it for a direct peer, and this seam keeps the binding
/// local instead of three files away;</item>
/// <item>the declared <see cref="AuthorityKind"/> must be one a member may
/// author: a <see cref="AuthorityKind.HostOnly"/> fact is authored by the host's
/// own simulation and <see cref="AuthorityKind.PresentationOnly"/> is never a
/// kernel fact at all. WHICH kind declares what is
/// <see cref="WireCommandAuthorityPolicy"/>'s table, filled from the senders this
/// tree actually has —so a member's <c>RunStart</c> is refused here while its
/// item use is not;</item>
/// <item>the CHEAT FAMILY's two host rulings, in this order: the host's cheat SWITCH
/// must be open to members (the family is closed regardless of who has a grant), and
/// then the host's cheat ROSTER must name the submitting member (the switch alone
/// admits nobody). Both are read per submission, none is cached, and each refusal is
/// ANSWERED with its own ground —the sender has already changed its own object for
/// the console write, so a verdict it never sees would leave that change in place.
/// The order is switch-then-roster on purpose: when both are shut the operator is told
/// about the switch, which is the remedy that unlocks the family at all;</item>
/// <item>a report that puts an item into another member's hands, or writes enemy
/// damage onto another member's body, must name the SENDER as that member: the
/// pickup destination and the enemy-combat victim are judged against the transport
/// sender —the trusted fact —before the item rules below, because neither needs a
/// fact this seam has to look up;</item>
/// <item>the item a report NAMES must be one this member may speak for —the
/// shared rule (<see cref="MayReportOn"/>) behind a destroy report, a state
/// report, a container take-out and a container sync's parent: world content
/// (the item lies on the ground, or inside a container that ultimately does), the
/// member's own carried, installed or contained item, or an id this host never
/// judged. The destroy half is the handler's former <c>CanDestroy</c> check,
/// moved here;</item>
/// <item>a report that says it is a NATIVE USE must name an item whose own data
/// permits one (<see cref="IKernelItemUseFacts"/>): both of the game's use call
/// sites run their delegate behind the item's own <c>ItemInfo.usable</c>, so a
/// use of an item that flag does not admit cannot have happened. The rule reads
/// the DEFINITION the host's own record holds, never the report's identity, and
/// it ANSWERS the refusal —the use already ran on the sender's own client, so
/// the recorded state is put back there (<c>ItemStateSettlement</c>). It sits
/// after the eligibility rule so a report on another member's item keeps the
/// verdict that family already gives it;</item>
/// <item>a container sync's listed CHILDREN must not be items the record puts in
/// another member's hands or on another member's limb
/// (<see cref="MayEnterOwnContainer"/>): the parent rule alone would still let a
/// report pull a peer's carried item into the reporter's own container;</item>
/// <item>the applied family's two reports must name the member's own item and the
/// member's own limb —a report that installs somebody else's item, or installs
/// on somebody else's limb, is the same remote-clone shape;</item>
/// <item>an id this host never judged is NOT this seam's to answer, so it is
/// admitted here. That branch is defensive rather than the production path: for
/// every item rule below, <c>KernelProtocolCommandHandler.
/// TryRefuseUnjudgedOperation</c> runs above this seam and already refuses an
/// unjudged id with its own reason, so a member never reaches this seam with one.
/// The guard exists so the seam can never INVENT a verdict for state it does not
/// hold if the ordering above it ever changes. A container sync's MISSING parent
/// is the same shape and is admitted for the same reason: the domain's own
/// accept-first heal is what creates it.</item>
/// </list>
/// </para>
/// </summary>
public sealed class KernelCommandGateway(IKernelItemFacts items, IMemberCommandRules memberCommandRules, IKernelItemUseFacts itemUseFacts, ILogger<KernelCommandGateway> log)
{
	private readonly IKernelItemFacts _items = items;
	private readonly IMemberCommandRules _memberCommandRules = memberCommandRules;
	private readonly IKernelItemUseFacts _itemUseFacts = itemUseFacts;
	private readonly ILogger<KernelCommandGateway> _log = log;

	/// <summary>
	/// Judges whether a member's submission may reach the kernel, and audits
	/// every refusal with one uniform line naming the command, the item it names
	/// (when it names one), the actor, the sender and the reason.
	/// </summary>
	/// <param name="sender">The member that submitted the frame.</param>
	/// <param name="kind">
	/// The WIRE kind it submitted. The mapped command cannot carry this: a console
	/// write and a native use are the same kernel fact by design, and the host's
	/// own rule for the verb is a property of the kind rather than of the fact.
	/// </param>
	/// <param name="command">The mapped kernel command.</param>
	public CommandAdmission AdmitMemberSubmission(ulong sender, WireCommandKind kind, GameCommand command)
	{
		if (command.Actor.Value != sender)
		{
			return Refuse(sender, command, RejectionReason.NotAuthorized,
				"the command names an actor other than the submitting member");
		}

		switch (command.Authority)
		{
			case AuthorityKind.HostOnly:
				return Refuse(sender, command, RejectionReason.NotAuthorized,
					"a host-only fact is authored by the host's own simulation, never by a member");
			case AuthorityKind.PresentationOnly:
				return Refuse(sender, command, RejectionReason.NotAuthorized,
					"a presentation-only command is never a kernel fact");
		}

		if (kind == WireCommandKind.ItemDataWrite && !_memberCommandRules.CheatVerbsOpenToMembers)
		{
			// The broader of the cheat family's two rulings, judged FIRST: with the
			// switch closed the family is shut to every member, granted or not, so the
			// operator's remedy is the switch rather than a grant. Answered on purpose
			// (the console write has already changed the sender's own object, and it
			// settles that change back on receipt).
			return Refuse(sender, command, RejectionReason.HostRuleDenied,
				"the host's cheat switch is closed to members, so no member may run the console write verbs");
		}

		if (kind == WireCommandKind.ItemDataWrite && !_memberCommandRules.AllowsCheatVerb(sender))
		{
			// The WHO half of the same pair: the switch is open, but this host's roster
			// does not name the sender —a grant must still be asked for. Answered for
			// the same reason.
			return Refuse(sender, command, RejectionReason.CheatPermissionDenied,
				"the host's cheat-permission roster does not name this member, so the console write verbs are closed to it");
		}

		// A report that puts an item into another member's hands, or writes enemy
		// damage onto another member's body, is the same family as the item rules
		// below and is judged FIRST because it needs no fact this seam has to look
		// up: the TRANSPORT SENDER is the trusted fact (the rule CraftSyncService
		// already writes down), and the identity the payload carries is a claim.
		// Both shapes were the census's unbounded rows, and both are decided by the
		// record this seam already holds —nothing about distance, reach, sequence
		// or magnitude is judged here, and every magnitude still reaches the kernel
		// as the reporter's own once the body is its own.
		if (command is PickUpItemCommand pickup && pickup.NewOwner.Value != sender)
		{
			return Ignore(sender, command, RejectionReason.NotAuthorized,
				$"item {pickup.InstanceId} is handed to {pickup.NewOwner.Value} instead of the sender");
		}

		// The same family, one fact deeper, for BOTH pickup-shaped kinds: the plain pickup and the
		// container take-out are one kernel command —a member takes an item into its own hands —so
		// the item the report names must be one this member may speak for. Without this the same
		// command under the OTHER kind was an alias: a report could relocate an item out of another
		// member's container into the sender's hands, and the eligibility rule would only cover the
		// kind that happened to be checked. ANSWERED, not ignored: the sender's copy is an item it is
		// holding or saw in the world, and the verdict is what reaches the family's settlements (the
		// pickup's world rollback, the take-out's re-stated carried set).
		if (command is PickUpItemCommand claimed && !MayTakeIntoOwnHands(sender, claimed.InstanceId))
		{
			return Refuse(sender, command, RejectionReason.NotAuthorized,
				$"item {claimed.InstanceId} is another member's (its carrier, container or holder says so), so it is not the sender's to take");
		}

		if (ForeignVictimDetail(sender, command) is { } victimDetail)
		{
			// The AFFECTED member's client is the judge of its own body
			// (done/health-simulation-and-host-authority.md), and every production
			// sender names itself —EnemyCombatReplay and EnemyProximitySync both set
			// msg.VictimSteamId = _session.LocalSteamId —so a report naming somebody
			// else is not a report this host can place.
			return Ignore(sender, command, RejectionReason.NotAuthorized, victimDetail);
		}

		if (command is DestroyItemCommand destroy && !MayReportOn(sender, destroy.InstanceId))
		{
			// The item id rides the detail: for this verdict the sender is NOT
			// answered, so this audit line is the only trace a dropped report
			// leaves, and an operator needs to know WHICH item it named.
			return Ignore(sender, command, RejectionReason.NotAuthorized,
				$"item {destroy.InstanceId} is neither world content nor the sender's own (carried, installed or contained)");
		}

		if (command is UpdateItemStateCommand state && !MayReportOn(sender, state.InstanceId))
		{
			// The ONE kind this branch answers, and it has to: a console write is
			// applied to the member's own local object before it is reported, so a
			// verdict the sender never sees would leave that object changed for an
			// item the host refuses to let it speak for. Every other shape is dropped
			// in silence for the reason the destroy rule gives above: the
			// remote-clone display proxy, where answering would start a guest
			// rollback on a copy that is not the item's fact.
			var detail = $"item {state.InstanceId} is another member's (carried, installed or contained), so its state is not the sender's to report";
			return kind == WireCommandKind.ItemDataWrite
				? Refuse(sender, command, RejectionReason.NotAuthorized, detail)
				: Ignore(sender, command, RejectionReason.NotAuthorized, detail);
		}

		if (kind == WireCommandKind.ItemUse
			&& command is UpdateItemStateCommand use
			&& NativeUseRefusal(use) is { } useDetail)
		{
			// Answered on purpose: the use already ran on the sender's own client, so a
			// verdict it never sees would leave that object changed for good. Only the
			// USE kind reaches this rule —a slot move or a gun transition reports the
			// same shape under ItemUpdateState and claims no use, so no use precondition
			// applies to it.
			return Refuse(sender, command, RejectionReason.UseNotPermitted, useDetail);
		}

		if (command is SyncContainerItemsCommand sync && ContainerSyncRefusal(sender, sync) is { } containerDetail)
		{
			return Ignore(sender, command, RejectionReason.NotAuthorized, containerDetail);
		}

		if (command is ApplyLimbItemCommand apply && !MayInstallOnOwnLimb(sender, apply))
		{
			return Ignore(sender, command, RejectionReason.NotAuthorized,
				$"item {apply.InstanceId} is not carried by the sender, or the report installs it on somebody else's limb {apply.LimbIndex}");
		}

		if (command is RemoveLimbItemCommand remove && !MayTakeOffOwnLimb(sender, remove))
		{
			return Ignore(sender, command, RejectionReason.NotAuthorized,
				$"item {remove.InstanceId} is not applied to limb {remove.LimbIndex} of the sender, or the removal hands it to somebody else");
		}

		return CommandAdmission.Admit();
	}

	/// <summary>
	/// The pickup family's own eligibility rule (the plain pickup and the container take-out, one
	/// kernel command): the item a report takes into the sender's hands must be one the record lets
	/// that member speak for.
	/// <list type="bullet">
	/// <item>world content —the item lies on the ground, or inside a container that ultimately does:
	/// every peer may take it;</item>
	/// <item>the member's OWN carried or installed item (its recorded owner is the sender);</item>
	/// <item>an item inside a container the SENDER holds: the CONTAINED arm reads the row the child
	/// SITS IN, never the child's own recorded owner. The kernel stamps a child's owner when it is
	/// created and does not re-stamp it when the container changes hands, so a bag that was taken
	/// from another member still records its contents under the previous holder
	/// (<c>AContainerSync_AfterTheContainerChangedHands_StillAcceptsTheRecordedContents</c> pins that
	/// for the container report, and <c>docs/backlog/done/member-report-bounds.md</c> reached the
	/// same conclusion: the row the child sits in must be the sender's own, never the child's owner);</item>
	/// <item>an id this host never judged, and a terminal row: nothing here to judge, and the handler's
	/// own rules above answer it (the creation-before-operation invariant, the take-out's
	/// <c>UnclaimedItem</c> answer) or the domain does (a terminal row's own reason).</item>
	/// </list>
	/// What is left —an item in another member's hands, on another member's limb, or inside another
	/// member's non-world container —is the remote-clone display proxy's shape: admitting it let a
	/// report take an item out of somebody else's bag.
	/// </summary>
	private bool MayTakeIntoOwnHands(ulong sender, ulong itemId)
	{
		var current = _items.FindItem(itemId);
		if (current is null)
		{
			// Defensive, not the production path: the handler above already answers an id whose
			// creation this host has never judged.
			return true;
		}

		var row = current.Value;
		return row.Location.Kind switch
		{
			ItemLocationKind.World or ItemLocationKind.Terminal => true,
			ItemLocationKind.Contained => ItemLocationChain.HolderOf(row, _items.FindItem) == sender
				|| ItemLocationChain.IsWorldRooted(row, _items.FindItem),
			_ => row.Location.Owner.Value == sender,
		};
	}

	/// <summary>
	/// The item a member's report NAMES is one the record lets that member speak
	/// for. Three report families ask the same question —a destroy, a state
	/// update and a container sync's parent —so they share this one answer:
	/// <list type="bullet">
	/// <item>a WORLD item, and any item whose container chain is world-rooted: every
	/// peer may pick it up, use it in place or see it leave, so the reporter claims
	/// nobody's possession;</item>
	/// <item>the member's OWN carried, installed or contained item —an item's
	/// recorded state and its end are its holder's own report, which is the
	/// pre-release trust model this seam does not question;</item>
	/// <item>an id this host never judged: nothing here to judge, and the handler's
	/// own rules above answer it (the creation-before-operation invariant, or the
	/// accept-first heals);</item>
	/// <item>a terminal row: its end is already recorded, and the domain answers a
	/// report about it with its own reason.</item>
	/// </list>
	/// What is left —an item the record puts in another member's hands, on another
	/// member's limb or in another member's container —is the remote-clone display
	/// proxy's shape: admitting it rewrote a real owner's row, and for a destroy it
	/// emptied a real owner's bag from a viewer's side
	/// (<c>ItemDestroyAuthorityTests</c> pins that half).
	/// <para>
	/// The chain walk is the kernel's own (<see cref="ItemLocationChain"/>), the same
	/// one the layer reset and the save archive read, so "in the world" means one
	/// thing in this tree: a container that changed hands leaves its old contents
	/// recorded under the previous holder, and only the walk can tell those apart
	/// from an item somebody is really holding.
	/// </para>
	/// </summary>
	private bool MayReportOn(ulong sender, ulong itemId)
	{
		var current = _items.FindItem(itemId);
		if (current is null)
		{
			// Defensive, not the production path: the creation-before-operation
			// invariant above this seam already refuses an id with no judged
			// creation, and this seam holds no state to answer it with.
			return true;
		}

		var row = current.Value;
		return row.Location.Kind switch
		{
			ItemLocationKind.World or ItemLocationKind.Terminal => true,
			ItemLocationKind.Contained => row.Location.Owner.Value == sender
				|| ItemLocationChain.IsWorldRooted(row, _items.FindItem),
			_ => row.Location.Owner.Value == sender,
		};
	}

	/// <summary>
	/// The native-use family's own data anchor: a report that says it is a use must
	/// name an item the game's own data lets a use run on. The answer is asked of the
	/// RECORD's definition id —the identity this host already judged —never the
	/// report's, and the three values are kept apart: only
	/// <see cref="ItemUsePermission.Forbidden"/> refuses, because
	/// <see cref="ItemUsePermission.Unknown"/> means this host holds no fact about
	/// that definition (a mod it does not have) and refusing on a missing fact is the
	/// behaviour the census forbids. An id this host never judged returns no detail:
	/// the creation-before-operation invariant above this seam answers it.
	/// </summary>
	private string? NativeUseRefusal(UpdateItemStateCommand command)
	{
		var current = _items.FindItem(command.InstanceId);
		if (current is null)
		{
			return null;
		}

		var definitionId = current.Value.Identity.DefinitionId;
		return _itemUseFacts.NativeUse(definitionId) switch
		{
			ItemUsePermission.Forbidden =>
				$"item {command.InstanceId} is '{definitionId}', whose own data says no native use exists (ItemInfo.usable is clear) —both of the game's use call sites would have run something else",
			_ => null,
		};
	}

	/// <summary>
	/// A container sync's CHILDREN: the report claims each named item is inside the
	/// container it sends, and the domain moves it there (creating it when the host
	/// has never judged it). The one thing a child may not be is an item the record
	/// puts in another member's hands or on another member's limb —the same
	/// remote-clone shape, and without this rule a report could pull a peer's
	/// carried item into the reporter's own container. A row that is merely
	/// CONTAINED elsewhere is admitted on purpose: that is the container-changed-
	/// hands case above, and refusing it would refuse ordinary play. What the CHILD'S
	/// own owner cannot answer is whose row it sits in and whose row it is being moved
	/// to —the handler's container-sync bounds judge those two destinations
	/// (<c>KernelProtocolCommandHandler.TryRefuseUnclaimedContainer</c>), so this seam
	/// stays an eligibility test and never becomes the ownership rule.
	/// </summary>
	private bool MayEnterOwnContainer(ulong sender, ulong itemId)
	{
		var current = _items.FindItem(itemId);
		if (current is null)
		{
			return true;
		}

		return current.Value.Location.Kind switch
		{
			ItemLocationKind.Carried or ItemLocationKind.Applied => current.Value.Location.Owner.Value == sender,
			_ => true,
		};
	}

	/// <summary>
	/// The container sync's report detail when it is not this member's to send: the
	/// parent first (the whole subtree hangs off it), then the first child the record
	/// puts in another member's hands. <c>null</c> means admitted.
	/// </summary>
	private string? ContainerSyncRefusal(ulong sender, SyncContainerItemsCommand command)
	{
		if (!MayReportOn(sender, command.ParentIdentity.InstanceId))
		{
			return $"container {command.ParentIdentity.InstanceId} is another member's (carried, installed or contained), so its contents are not the sender's to rewrite";
		}

		foreach (var child in command.Children)
		{
			if (!MayEnterOwnContainer(sender, child.InstanceId))
			{
				return $"child {child.InstanceId} is another member's (carried or installed), so it cannot be moved into the sender's container";
			}
		}

		return null;
	}

	/// <summary>
	/// The applied family's own entry rule: a member may report that an item IT
	/// carries became the installed form of one of ITS OWN limbs. Both halves are
	/// checked against the item's recorded location rather than trusted from the
	/// command, because the gesture itself cannot carry either fact —the applier
	/// acts on the body it simulates —so a report that names somebody else's item
	/// or somebody else's limb is the remote-clone shape this seam exists to drop.
	/// A repeated observation of the same applied fact is admitted: it is a
	/// re-assertion, and the domain advances the revision for it.
	/// </summary>
	private bool MayInstallOnOwnLimb(ulong sender, ApplyLimbItemCommand command)
	{
		if (command.Owner.Value != sender)
		{
			return false;
		}

		var current = _items.FindItem(command.InstanceId);
		if (current is null)
		{
			// Defensive, as for the destroy rule above: the creation-before-operation
			// invariant answers an unjudged id before this seam sees it.
			return true;
		}

		return current.Value.Location.Kind switch
		{
			ItemLocationKind.Carried => current.Value.Location.Owner.Value == sender,
			ItemLocationKind.Applied => current.Value.Location.Owner.Value == sender && current.Value.Location.LimbIndex == command.LimbIndex,
			_ => false,
		};
	}

	/// <summary>
	/// The counterpart's rule: the limb is the SENDER's own, the destination is the
	/// sender (the component came off this client's own body and into its own
	/// hands), and the item really is installed on that limb. A removal that hands
	/// the item to a third party is a cross-player removal, and that one is the
	/// HOST's commit —it never travels as a member's report.
	/// </summary>
	private bool MayTakeOffOwnLimb(ulong sender, RemoveLimbItemCommand command)
	{
		if (command.AppliedOwner.Value != sender || command.NewOwner.Value != sender)
		{
			return false;
		}

		var current = _items.FindItem(command.InstanceId);
		if (current is null)
		{
			return true;
		}

		return current.Value.Location.Kind switch
		{
			ItemLocationKind.Applied => current.Value.Location.Owner.Value == sender
				&& current.Value.Location.LimbIndex == command.LimbIndex,
			// The already-taken-off shape, admitted on purpose: this seam decides ELIGIBILITY, and
			// the item is still the sender's own, so a RE-SENT report (the reconciliation window's
			// repeat of one operation) must reach the kernel and be answered from its operation
			// window instead of being dropped here. A plain carried item the sender really does
			// carry is refused by the domain by name, which is where that verdict belongs.
			ItemLocationKind.Carried => current.Value.Location.Owner.Value == sender,
			_ => false,
		};
	}

	/// <summary>
	/// The enemy-combat family's own identity rule: the victim a bite, a lunge or an
	/// effect names must be the member that reported it. The three kinds are one
	/// answer because the affected side's client judges its own body —the limb, the
	/// venom, the adrenaline and the effect magnitudes are all its own reading —so
	/// the victim identity is the one part of the report that can never be a field.
	/// The detail names the kind by its own message so the audit line says WHICH
	/// report claimed a foreign body.
	/// </summary>
	private static string? ForeignVictimDetail(ulong sender, GameCommand command) => command switch
	{
		RecordEnemyBiteCommand bite when bite.VictimSteamId != sender =>
			$"the bite names victim {bite.VictimSteamId} instead of the sender",
		RecordEnemyLungeCommand lunge when lunge.VictimSteamId != sender =>
			$"the lunge names victim {lunge.VictimSteamId} instead of the sender",
		RecordEnemyEffectCommand effect when effect.VictimSteamId != sender =>
			$"the effect names victim {effect.VictimSteamId} instead of the sender",
		_ => null,
	};

	private CommandAdmission Refuse(ulong sender, GameCommand command, RejectionReason reason, string detail)
	{
		Audit(sender, command, reason, detail, answered: true);
		return CommandAdmission.Refuse(reason);
	}

	private CommandAdmission Ignore(ulong sender, GameCommand command, RejectionReason reason, string detail)
	{
		Audit(sender, command, reason, detail, answered: false);
		return CommandAdmission.Ignore(reason);
	}

	private void Audit(ulong sender, GameCommand command, RejectionReason reason, string detail, bool answered) =>
		_log.LogWarning("Command admission refused ({Answer}): {Command} actor {Actor} sender {Sender} —{Reason}: {Detail}.",
			answered ? "answered" : "no answer, as before",
			command.GetType().Name,
			command.Actor.Value,
			sender,
			reason,
			detail);
}
