using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Versioning;
using CasualtiesUnknownOnline.Protocol.Wire;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// Host-side command handling for the Phase C kernel protocol. Kept separate
/// from <see cref="KernelProtocolService"/> so the transport/journal service
/// stays under the architecture size gate while the command decoding and
/// reject-notification logic stays in one narrow owner.
///
/// <para>
/// THE CREATION-BEFORE-OPERATION INVARIANT lives here on the judging side: the
/// host executes an operation on an item only after that item's creation has
/// been judged — accepted, or refused with a remembered reason. An operation
/// whose item this host has never judged is NOT held and NOT guessed about: it
/// is refused at once, loudly, because the sender broke the rule that its
/// creation report precedes every operation on the same item
/// (the sending half is the pending-creations table). Before this, a
/// pickup could be parked for a fixed 500 ms window and then answered with a
/// less precise reason; that window is gone rather than kept as a fallback.
/// </para>
/// </summary>
internal sealed class KernelProtocolCommandHandler(
	IKernelSessionFacts session,
	IKernelFrameSender sender,
	IKernelItemFacts items,
	IKernelCommandExecution execution,
	IKernelCheckpointSource checkpointSource,
	RefusedItemCreations refusedCreations,
	KernelCommandGateway gateway,
	IKernelItemDataNormalizer itemDataNormalizer,
	ICarriedRegistrationFacts carriedRegistrations,
	ILogger log)
{
	private readonly IKernelSessionFacts _session = session;
	private readonly IKernelFrameSender _sender = sender;
	private readonly IKernelItemFacts _items = items;
	private readonly IKernelCommandExecution _execution = execution;
	private readonly IKernelCheckpointSource _checkpointSource = checkpointSource;
	private readonly RefusedItemCreations _refusedCreations = refusedCreations;
	private readonly KernelCommandGateway _gateway = gateway;
	private readonly IKernelItemDataNormalizer _itemDataNormalizer = itemDataNormalizer;
	private readonly ICarriedRegistrationFacts _carriedRegistrations = carriedRegistrations;
	private readonly ILogger _log = log;

	public void Handle(ulong sender, CommandEnvelope envelope)
	{
		// THE CARRY-REGISTRATION HEAL — an explicit EXCEPTION to this invariant, not
		// a use of it. The reporter's own carried container may never have been
		// registered at all (a swallowed CarriedInventory report — see
		// done/carried-inventory-registration-re-report.md), so the host takes the
		// reporter's first report AS the creation judgement and materializes the
		// parent as the reporter's OWN carried item, then executes the operation.
		// Nothing is waited for and nothing is guessed about a third party's item;
		// a REFUSED creation is still answered precisely before this path
		// (TryRefuseRefusedCreation), and the invariant's check deliberately does
		// not cover this kind (see IsItemOperation).
		if (envelope.Command.Kind == WireCommandKind.ItemContainerSync)
		{
			HandleContainerSync(sender, envelope);
			return;
		}

		// The carried-item half of the same heal, and it must run BEFORE the
		// invariant's check below (which would otherwise answer the generic
		// protocol-violation refusal): an unknown carried id is adopted as
		// ItemLocation.Carried(sender) from the reporter's own report. The host
		// cannot verify the item really sits in that reporter's body — the wire
		// carries no such proof, and this pre-release trust model is exactly what
		// done/carried-inventory-registration-re-report.md and
		// future/strict-validation-anti-cheat.md own.
		//
		// It is bounded by the one fact that makes it an exception rather than a hole:
		// the member's carried-set statement (NetMsg.CarriedInventory) is the creation
		// claim for what that member carries — a root item and, recursively, its
		// container contents — and this host records whether it has judged one. Until it
		// has, this heal IS the claim (the swallowed-registration window the fix exists
		// for). After it has, a report naming an item the statement did not carry is not
		// a creation this host may honour: it would let any report materialize any item
		// at all, so the report is refused by name and ANSWERED, and the member settles
		// by re-stating the set — the absolute report that is already re-sent on its own
		// cadence, which the refusal makes due at once.
		var updateForAnUnjudgedId = envelope.Command.Kind is WireCommandKind.ItemUpdateState or WireCommandKind.ItemUse
			&& envelope.Command.Data is not null
			&& _items.FindItem(envelope.Command.Identity.InstanceId) is null;
		if (updateForAnUnjudgedId)
		{
			// A refused CREATION answers first on both paths: the member's own report
			// must never resurrect one, and that answer is the precise reason the
			// client rolls its local copy back (the drop-rollback family).
			if (TryRefuseRefusedCreation(sender, envelope))
			{
				return;
			}

			if (!_carriedRegistrations.HasJudgedCarriedSet(sender))
			{
				HandleMissingCarriedUpdate(sender, envelope);
				return;
			}

			_log.LogWarning("Kernel command from {Sender} refused: item {ItemId} was never judged, and that member's carried-set statement already was — a use report is not the creation claim for what a member carries, so the member re-states the set instead.",
				sender, envelope.Command.Identity.InstanceId);
			SendCommandRejected(sender, envelope.Command, RejectionReason.UnclaimedItem);
			return;
		}

		// A CONTAINER TAKE-OUT is a RELOCATION and never a creation claim: the item it names
		// has to be a row this host already holds, and an id it has never judged has nothing
		// to move. It is answered with the reason the carried-set statement settles (that
		// statement IS the creation claim for what a member carries) rather than with the
		// protocol-violation answer below, and deliberately WITHOUT the state report's
		// accept-first heal: a relocation that creates its own target would be the creation
		// claim this kind does not carry — its payload is an identity and an owner, with no
		// data to state a row from. The distinction is not cosmetic either: the member's
		// statement is a CADENCE (sync-coverage row I8), so an item that entered a container
		// since the last one legitimately has no judged creation yet.
		if (envelope.Command.Kind == WireCommandKind.ItemContainerTakeOut
			&& _items.FindItem(envelope.Command.Identity.InstanceId) is null)
		{
			if (TryRefuseRefusedCreation(sender, envelope))
			{
				return;
			}

			_log.LogInformation("Container take-out from {Sender} names item {ItemId}, which this host never judged — the relocation has nothing to move, and the carried-set statement is what claims the item; answering the reason the statement settles.",
				sender, envelope.Command.Identity.InstanceId);
			SendCommandRejected(sender, envelope.Command, RejectionReason.UnclaimedItem);
			return;
		}

		if (TryRefuseUnjudgedOperation(sender, envelope))
		{
			return;
		}

		// Admission: one seam decides whether a member may submit this command at
		// all — the actor/sender binding, the declared authority policy, and the
		// destroy eligibility rule that used to sit at this exact position. The
		// position is part of the contract: the protocol heals and the
		// creation-before-operation invariant above still refuse first, and an id
		// this host never judged still goes to the kernel for its own verdict.
		var command = KernelWireMapper.FromWireCommand(envelope.Command, envelope.Header);
		var admission = _gateway.AdmitMemberSubmission(sender, envelope.Command.Kind, command);
		if (!admission.IsAdmitted)
		{
			if (admission.AnswersSender && admission.Reason is { } reason)
			{
				SendCommandRejected(sender, envelope.Command, reason);
			}

			return;
		}

		command = ResolveCommandRevision(command);
		if (!_execution.TryExecuteCommand(command, sender, out _, out var rejection))
		{
			if (envelope.Command.Kind == WireCommandKind.ItemSpawn)
			{
				// A refused creation is remembered: later operations on this id get
				// this precise reason instead of "unknown item".
				_refusedCreations.Record(envelope.Command.Identity.InstanceId, rejection!.Reason);
			}

			_log.LogWarning("Kernel command from {Sender} rejected: {Reason} ({Message}).",
				sender, rejection!.Reason, rejection.Message);
			SendCommandRejected(sender, envelope.Command, rejection.Reason);
		}
	}

	/// <summary>
	/// The invariant's host half: an operation on an item whose creation this
	/// host has not judged is refused AT ONCE — with the remembered reason when
	/// the creation was refused, and as a logged protocol violation when the
	/// sender simply never reported the creation. Nothing is queued, nothing
	/// waits, and no window stands between the report and the verdict.
	/// </summary>
	private bool TryRefuseUnjudgedOperation(ulong sender, CommandEnvelope envelope)
	{
		var kind = envelope.Command.Kind;
		if (!IsItemOperation(kind))
		{
			return false;
		}

		var itemId = envelope.Command.Identity.InstanceId;
		if (_items.FindItem(itemId) is not null)
		{
			return false;
		}

		if (_refusedCreations.TryGet(itemId, out var refusal))
		{
			_log.LogWarning("Kernel command from {Sender} refused: the creation of item {ItemId} was refused ({Reason}) — answering the precise reason immediately.",
				sender, itemId, refusal);
			SendCommandRejected(sender, envelope.Command, refusal);
			return true;
		}

		_log.LogError("Protocol violation: {Sender} reported {Kind} on item {ItemId} whose creation this host has never judged. Refused at once — a sender must report an item's creation before any operation on it (creation-before-operation).",
			sender, kind, itemId);
		SendCommandRejected(sender, envelope.Command, RejectionReason.UnknownAggregate);
		return true;
	}

	/// <summary>
	/// The bare tombstone check used by the container-sync path and by the state report's
	/// unjudged-id path: a refused creation is answered with its precise reason and an id
	/// this host never saw keeps its existing verdict.
	/// </summary>
	private bool TryRefuseRefusedCreation(ulong sender, CommandEnvelope envelope)
	{
		if (_items.FindItem(envelope.Command.Identity.InstanceId) is not null)
		{
			return false;
		}

		if (!_refusedCreations.TryGet(envelope.Command.Identity.InstanceId, out var refusal))
		{
			return false;
		}

		_log.LogWarning("Kernel {Kind} from {Sender} refused: the creation of item {ItemId} was refused ({Reason}) — answering the precise reason immediately.",
			envelope.Command.Kind, sender, envelope.Command.Identity.InstanceId, refusal);
		SendCommandRejected(sender, envelope.Command, refusal);
		return true;
	}

	/// <summary>
	/// The container sync's verdict, in the two kinds of fact this host can hold about it.
	///
	/// <para>
	/// FIRST the record's own rows, which need no statement: the kernel's container arm rewrites the
	/// parent's facts, MOVES every listed child to the parent THE REPORT names and DESTROYS the
	/// descendants the report leaves out, so every row this report can write to must be the sender's
	/// own. Three destinations are judged, all against rows the host already holds — a row it does not
	/// know is not a fact to refuse here, because the same report may be creating it:
	/// the PARENT (a world row has no production sender for this kind, and naming one would let a
	/// report rewrite a world container and destroy its contents; the seam deliberately admits a world
	/// row because a destroy report may name world content), each child's DESTINATION (the kernel
	/// writes <c>child.ParentItemId</c> verbatim, so a listed child could otherwise be hung under
	/// another member's row), and a known child's own HOME (a child the record holds inside another
	/// member's container is not this sender's to move). The membership test is the family's own —
	/// <c>Location.Owner.Value == sender</c> — because a nested container is recorded as CONTAINED
	/// with that owner, never as carried.
	/// </para>
	///
	/// <para>
	/// THEN the statement, which is the same creation claim the state report's refusals rest on: the
	/// member's carried-set statement speaks for what that member carries — a carried container AND,
	/// recursively, what is inside it — so once this host has judged one, an id the statement did not
	/// carry is not a creation it may honour, and the member's way back is re-stating the set. Until it
	/// has judged one, the heal IS the claim (the swallowed-registration window this path exists for)
	/// and the report passes through.
	/// </para>
	/// </summary>
	private bool TryRefuseUnclaimedContainer(ulong sender, CommandEnvelope envelope)
	{
		var parentId = envelope.Command.Identity.InstanceId;
		var parent = _items.FindItem(parentId);
		if (parent is not null && parent.Value.Location.Owner.Value != sender)
		{
			_log.LogWarning("Container sync from {Sender} refused: container {ItemId} is recorded as {Kind} and is not this member's own row — nothing applied, nothing destroyed, nothing relayed.",
				sender, parentId, parent.Value.Location.Kind);
			SendCommandRejected(sender, envelope.Command, RejectionReason.UnclaimedItem);
			return true;
		}

		foreach (var child in envelope.Command.ContainerChildren)
		{
			var childId = child.Identity.InstanceId;
			if (childId == 0)
			{
				continue;
			}

			var destination = _items.FindItem(child.ParentItemId);
			if (destination is not null && destination.Value.Location.Owner.Value != sender)
			{
				_log.LogWarning("Container sync from {Sender} refused: it lists content item {ItemId} under {ParentId}, which this kernel does not record as that member's own row — nothing created, nothing applied, nothing relayed.",
					sender, childId, child.ParentItemId);
				SendCommandRejected(sender, envelope.Command, RejectionReason.UnclaimedItem);
				return true;
			}

			var current = _items.FindItem(childId);
			if (current is null)
			{
				continue; // created by this very report: the statement arm below judges it
			}

			if (current.Value.Location.Kind != ItemLocationKind.Contained)
			{
				continue; // carried/installed rows are the seam's MayEnterOwnContainer rule
			}

			var home = _items.FindItem(current.Value.Location.ParentItemId);
			if (home is not null
				&& home.Value.Location.Owner.Value != sender
				&& !ItemLocationChain.IsWorldRooted(home.Value, _items.FindItem))
			{
				_log.LogWarning("Container sync from {Sender} refused: it lists content item {ItemId}, which this kernel records inside {HomeId} — another member's container, so it is not this sender's to move. Nothing applied, nothing relayed.",
					sender, childId, current.Value.Location.ParentItemId);
				SendCommandRejected(sender, envelope.Command, RejectionReason.UnclaimedItem);
				return true;
			}
		}

		if (!_carriedRegistrations.HasJudgedCarriedSet(sender))
		{
			return false;
		}

		if (parent is null)
		{
			_log.LogWarning("Container sync from {Sender} refused: container {ItemId} was never judged, and that member's carried-set statement already was — a container report is not the creation claim for what a member carries, so the member re-states the set instead.",
				sender, parentId);
			SendCommandRejected(sender, envelope.Command, RejectionReason.UnclaimedItem);
			return true;
		}

		foreach (var child in envelope.Command.ContainerChildren)
		{
			var childId = child.Identity.InstanceId;
			if (childId == 0 || _items.FindItem(childId) is not null)
			{
				continue;
			}

			_log.LogWarning("Container sync from {Sender} refused: content item {ItemId} of container {ParentId} was never judged, and that member's carried-set statement already was — the statement carries a carried container's contents, so the member re-states the set instead.",
				sender, childId, parentId);
			SendCommandRejected(sender, envelope.Command, RejectionReason.UnclaimedItem);
			return true;
		}

		return false;
	}

	/// <summary>
	/// Every wire kind that OPERATES on an existing item — the family the invariant
	/// governs (a creation kind, <c>ItemSpawn</c>, is the judgement itself).
	/// <c>ItemContainerSync</c> is deliberately absent: that kind carries the
	/// carry-registration heal instead, which the host answers by materializing the
	/// reporter's own carried parent (see Handle), and a refused creation there is
	/// still answered precisely by <see cref="TryRefuseRefusedCreation"/>.
	/// <c>ItemDataWrite</c> IS one, and it takes no heal: the console resolves its
	/// target from the kernel record, so a legitimate console write always names an
	/// item this host has already judged, and an unjudged id under this kind is a
	/// report the sender cannot have made.
	/// </summary>
	private static bool IsItemOperation(WireCommandKind kind) => kind switch
	{
		WireCommandKind.ItemPickup => true,
		WireCommandKind.ItemContainerTakeOut => true,
		WireCommandKind.ItemDrop => true,
		WireCommandKind.ItemTransfer => true,
		WireCommandKind.ItemDestroy => true,
		WireCommandKind.ItemUpdateState => true,
		WireCommandKind.ItemUse => true,
		WireCommandKind.ItemDataWrite => true,
		WireCommandKind.ItemApplyLimb => true,
		WireCommandKind.ItemRemoveLimb => true,
		_ => false,
	};

	private void HandleMissingCarriedUpdate(ulong sender, CommandEnvelope envelope)
	{
		var command = envelope.Command;
		var kernelData = ToKernelData(command.Identity, command.Data);
		var spawn = new SpawnItemCommand(
			new OperationId(envelope.Header.OperationId),
			new ActorId(sender),
			new RunEpoch(envelope.Header.RunEpoch),
			AuthorityKind.OwnerPredictedHostValidated,
			KernelWireMapper.FromWireIdentity(command.Identity),
			ItemLocation.Carried(new ActorId(sender)),
			0,
			kernelData);
		if (_execution.TryExecuteCommand(spawn, sender, out _, out var rejection))
		{
			_log.LogInformation("Accepted-first missing carried update for item {ItemId} from {Sender}: spawned carried fact.",
				command.Identity.InstanceId, sender);
		}
		else
		{
			_log.LogWarning("Missing carried update for item {ItemId} from {Sender} was rejected: {Reason} ({Message}).",
				command.Identity.InstanceId, sender, rejection!.Reason, rejection.Message);
		}
	}

	/// <summary>
	/// The container sync's whole host half, in the ONE order its four verdicts have to run in.
	///
	/// <para>
	/// 1. A refused CREATION answers first, with its own reason (the tombstone).
	/// 2. The admission seam judges the report as every other member command is judged — it is what
	/// decides whether the report is even talked back to (<c>AnswersSender</c>): a report naming
	/// another member's container is IGNORED without an answer, because the sender's own copy is not
	/// the fact a refusal would settle. The creation bounds below MUST NOT move ahead of it: they
	/// answer, and answering in the seam's place breaks that documented contract.
	/// 3. The creation bounds (<see cref="TryRefuseUnclaimedContainer"/>) judge the rows this report
	/// can write and the creation claim its member's carried-set statement is, and they answer.
	/// 4. Only then is the command executed: the kernel's <c>DecideSyncContainer</c> rewrites the
	/// parent's data, moves the listed children to the parent the report names and destroys the
	/// descendants the report left out.
	/// </para>
	/// </summary>
	private void HandleContainerSync(ulong sender, CommandEnvelope envelope)
	{
		if (TryRefuseRefusedCreation(sender, envelope))
		{
			return;
		}

		var command = envelope.Command;
		var parentId = command.Identity.InstanceId;
		var sync = KernelWireMapper.FromWireCommand(command, envelope.Header);
		var admission = _gateway.AdmitMemberSubmission(sender, command.Kind, sync);
		if (!admission.IsAdmitted)
		{
			if (admission.AnswersSender && admission.Reason is { } reason)
			{
				SendCommandRejected(sender, envelope.Command, reason);
			}

			return;
		}

		if (TryRefuseUnclaimedContainer(sender, envelope))
		{
			return;
		}

		if (!_execution.TryExecuteCommand(sync, sender, out _, out var rejection))
		{
			_log.LogWarning("Container sync for {ItemId} from {Sender} rejected: {Reason} ({Message}).",
				parentId, sender, rejection!.Reason, rejection.Message);
			return;
		}

		_log.LogInformation("Container sync for {ItemId} from {Sender}: {Children} child fact(s).",
			parentId, sender, command.ContainerChildren.Count);
	}

	/// <summary>
	/// The three-argument form its Runtime callers use when no wire command is in
	/// hand: the only caller is a refused item CREATION (the block-drop family
	/// remembers the tombstone and answers later operations with it), so the answer
	/// names <see cref="WireCommandKind.ItemSpawn"/> as the report it judged.
	/// </summary>
	internal void SendCommandRejected(ulong targetSteamId, ulong itemId, RejectionReason reason) =>
		SendCommandRejected(targetSteamId, new WireCommand
		{
			Kind = WireCommandKind.ItemSpawn,
			Identity = new WireItemIdentity { InstanceId = itemId },
		}, reason);

	private void SendCommandRejected(ulong targetSteamId, WireCommand original, RejectionReason reason)
	{
		if (!_session.IsHost || !_session.SessionActive || targetSteamId == 0)
		{
			return;
		}

		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.Command,
			Command = new CommandEnvelope
			{
				Header = new EnvelopeHeader
				{
					ProtocolVersion = ProtocolConstants.EnvelopeVersion,
					RunEpoch = _checkpointSource.CreateCheckpoint().RunEpoch.Value,
					SenderId = _session.LocalSteamId,
					MessageId = 0,
					PayloadType = WirePayloadType.CommandRejected,
				},
				Command = new WireCommand
				{
					Kind = WireCommandKind.CommandRejected,
					Identity = new WireItemIdentity { InstanceId = original.Identity.InstanceId },
					RejectionReason = (int)reason,
					// WHICH report the verdict answers: several of one member's
					// reports can name the same item, and they settle differently on
					// its side (a refused console write puts the local value back), so
					// the answer names the kind rather than leaving the sender to
					// guess from the item id.
					RefusedKind = original.Kind,
				},
			},
		};
		_sender.Send(targetSteamId, frame);
	}

	private GameCommand ResolveCommandRevision(GameCommand command)
	{
		switch (command)
		{
			case PickUpItemCommand c:
				{
					var current = FindRevision(c.InstanceId);
					if (current is not null)
					{
						return c with { ExpectedRevision = current.Value.Revision };
					}

					break;
				}
			case DropItemCommand c:
				{
					var current = FindRevision(c.InstanceId);
					if (current is not null)
					{
						return c with { ExpectedRevision = current.Value.Revision };
					}

					break;
				}
			case DestroyItemCommand c:
				{
					var current = FindRevision(c.InstanceId);
					if (current is not null)
					{
						return c with { ExpectedRevision = current.Value.Revision };
					}

					break;
				}
			case UpdateItemStateCommand c:
				{
					var current = FindRevision(c.InstanceId);
					if (current is not null)
					{
						return c with { ExpectedRevision = current.Value.Revision };
					}

					break;
				}
			case TransferItemCommand c:
				{
					var current = FindRevision(c.InstanceId);
					if (current is not null)
					{
						return c with { ExpectedRevision = current.Value.Revision };
					}

					break;
				}
			case ApplyLimbItemCommand c:
				{
					var current = FindRevision(c.InstanceId);
					if (current is not null)
					{
						return c with { ExpectedRevision = current.Value.Revision };
					}

					break;
				}
			case RemoveLimbItemCommand c:
				{
					var current = FindRevision(c.InstanceId);
					if (current is not null)
					{
						return c with { ExpectedRevision = current.Value.Revision };
					}

					break;
				}
		}

		return command;
	}

	private ItemState? FindRevision(ulong itemId) => _items.FindItem(itemId);

	private ItemData ToKernelData(WireItemIdentity identity, WireItemData? data)
	{
		var state = new ItemState(
			KernelWireMapper.FromWireIdentity(identity),
			0,
			ItemLocation.Terminal())
		{
			Data = data is null ? ItemData.Empty : KernelWireMapper.FromWireData(data),
		};
		return _itemDataNormalizer.ToKernelItemData(state);
	}
}
