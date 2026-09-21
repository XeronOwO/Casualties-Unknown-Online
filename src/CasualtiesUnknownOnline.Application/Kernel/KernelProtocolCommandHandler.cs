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
	IKernelWireCodec codec,
	ILogger log)
{
	private readonly IKernelSessionFacts _session = session;
	private readonly IKernelFrameSender _sender = sender;
	private readonly IKernelItemFacts _items = items;
	private readonly IKernelCommandExecution _execution = execution;
	private readonly IKernelCheckpointSource _checkpointSource = checkpointSource;
	private readonly RefusedItemCreations _refusedCreations = refusedCreations;
	private readonly KernelCommandGateway _gateway = gateway;
	private readonly IKernelWireCodec _codec = codec;
	private readonly ILogger _log = log;

	public void Handle(ulong sender, CommandEnvelope envelope)
	{
		// THE CARRY-REGISTRATION HEAL — an explicit EXCEPTION to this invariant, not
		// a use of it. The reporter's own carried container may never have been
		// registered at all (a swallowed CarriedInventory report — see
		// review/carried-inventory-registration-re-report.md), so the host takes the
		// reporter's first report AS the creation judgement and materializes the
		// parent as the reporter's OWN carried item, then executes the operation.
		// Nothing is waited for and nothing is guessed about a third party's item;
		// a REFUSED creation is still answered precisely before this path
		// (TryRefuseRefusedCreation), and the invariant's check deliberately does
		// not cover this kind (see IsItemOperation).
		if (envelope.Command.Kind == WireCommandKind.ItemContainerSync)
		{
			if (!TryRefuseRefusedCreation(sender, envelope))
			{
				HandleItemContainerSync(sender, envelope);
			}

			return;
		}

		// The carried-item half of the same heal, and it must run BEFORE the
		// invariant's check below (which would otherwise answer the generic
		// protocol-violation refusal): an unknown carried id is adopted as
		// ItemLocation.Carried(sender) from the reporter's own report. The host
		// cannot verify the item really sits in that reporter's body — the wire
		// carries no such proof, and this pre-release trust model is exactly what
		// review/carried-inventory-registration-re-report.md and
		// future/strict-validation-anti-cheat.md own.
		if (envelope.Command.Kind == WireCommandKind.ItemUpdateState
			&& envelope.Command.Data is not null
			&& _items.FindItem(envelope.Command.Identity.InstanceId) is null)
		{
			HandleMissingCarriedUpdate(sender, envelope);
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
		var command = _codec.FromWireCommand(envelope.Command, envelope.Header);
		var admission = _gateway.AdmitMemberSubmission(sender, command);
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
	/// The bare tombstone check used by the container-sync path, whose unknown-id
	/// handling stays with the kernel: a refused creation is answered with its
	/// precise reason, an id this host never saw keeps its existing verdict.
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

		_log.LogWarning("Container sync from {Sender} refused: the creation of item {ItemId} was refused ({Reason}) — answering the precise reason immediately.",
			sender, envelope.Command.Identity.InstanceId, refusal);
		SendCommandRejected(sender, envelope.Command, refusal);
		return true;
	}

	/// <summary>
	/// Every wire kind that OPERATES on an existing item — the family the invariant
	/// governs (a creation kind, <c>ItemSpawn</c>, is the judgement itself).
	/// <c>ItemContainerSync</c> is deliberately absent: that kind carries the
	/// carry-registration heal instead, which the host answers by materializing the
	/// reporter's own carried parent (see Handle), and a refused creation there is
	/// still answered precisely by <see cref="TryRefuseRefusedCreation"/>.
	/// </summary>
	private static bool IsItemOperation(WireCommandKind kind) => kind switch
	{
		WireCommandKind.ItemPickup => true,
		WireCommandKind.ItemDrop => true,
		WireCommandKind.ItemTransfer => true,
		WireCommandKind.ItemDestroy => true,
		WireCommandKind.ItemUpdateState => true,
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
			_codec.FromWireIdentity(command.Identity),
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

	private void HandleItemContainerSync(ulong sender, CommandEnvelope envelope)
	{
		var command = envelope.Command;
		var parentId = command.Identity.InstanceId;
		var sync = _codec.FromWireCommand(command, envelope.Header);
		if (!_execution.TryExecuteCommand(sync, sender, out _, out var rejection))
		{
			_log.LogWarning("Container sync for {ItemId} from {Sender} rejected: {Reason} ({Message}).",
				parentId, sender, rejection!.Reason, rejection.Message);
			return;
		}

		_log.LogInformation("Container sync for {ItemId} from {Sender}: {Children} child fact(s).",
			parentId, sender, command.ContainerChildren.Count);
	}

	internal void SendCommandRejected(ulong targetSteamId, ulong itemId, RejectionReason reason) =>
		SendCommandRejected(targetSteamId, new WireCommand
		{
			Kind = WireCommandKind.CommandRejected,
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
		}

		return command;
	}

	private ItemState? FindRevision(ulong itemId) => _items.FindItem(itemId);

	private ItemData ToKernelData(WireItemIdentity identity, WireItemData? data)
	{
		var state = new ItemState(
			_codec.FromWireIdentity(identity),
			0,
			ItemLocation.Terminal())
		{
			Data = data is null ? ItemData.Empty : _codec.FromWireData(data),
		};
		return _codec.ToKernelItemData(state);
	}
}
