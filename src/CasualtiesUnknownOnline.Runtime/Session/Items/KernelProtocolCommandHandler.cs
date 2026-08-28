using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Versioning;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Host-side command handling for the Phase C kernel protocol. Kept separate
/// from <see cref="KernelProtocolService"/> so the transport/journal service
/// stays under the architecture size gate while the command decoding and
/// reject-notification logic stays in one narrow owner.
/// </summary>
internal sealed class KernelProtocolCommandHandler(
	ISessionControl session,
	PacketSender sender,
	ItemKernelAuthority authority,
	ITimeSource time,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ItemKernelAuthority _authority = authority;
	private readonly ITimeSource _time = time;
	private readonly ILogger _log = log;
	private readonly PendingPickupQueue _pendingPickups = new(PendingPickupQueue.DefaultHoldMs);
	private readonly Dictionary<(ulong Sender, ulong ItemId), CommandEnvelope> _pendingEnvelopes = [];

	public void Handle(ulong sender, CommandEnvelope envelope)
	{
		if (envelope.Command.Kind == WireCommandKind.ItemContainerSync)
		{
			HandleItemContainerSync(sender, envelope);
			return;
		}

		if (envelope.Command.Kind == WireCommandKind.ItemUpdateState
			&& envelope.Command.Data is not null
			&& _authority.FindItem(envelope.Command.Identity.InstanceId) is null)
		{
			HandleMissingCarriedUpdate(sender, envelope);
			return;
		}

		if (envelope.Command.Kind == WireCommandKind.ItemPickup
			&& _authority.FindItem(envelope.Command.Identity.InstanceId) is null)
		{
			EnqueuePickup(sender, envelope);
			return;
		}

		var command = ResolveCommandRevision(KernelWireMapper.FromWireCommand(envelope.Command, envelope.Header));
		if (!_authority.TryExecuteCommand(command, sender, out _, out var rejection))
		{
			_log.LogWarning("Kernel command from {Sender} rejected: {Reason} ({Message}).",
				sender, rejection!.Reason, rejection.Message);
			SendCommandRejected(sender, envelope.Command, rejection.Reason);
			return;
		}

		SettlePendingPickups(envelope.Command.Identity.InstanceId);
	}

	public void PumpPendingPickups(long nowMs)
	{
		foreach (var pending in _pendingPickups.TakeExpired(nowMs))
		{
			_pendingEnvelopes.Remove((pending.Sender, pending.ItemId));
			if (_authority.FindItem(pending.ItemId) is null)
			{
				SendCommandRejected(pending.Sender, PendingIdentity(pending.ItemId), RejectionReason.UnknownAggregate);
			}
			else
			{
				SettlePendingPickups(pending.ItemId);
			}
		}
	}

	public void Reset() => _pendingPickups.Reset();

	private void EnqueuePickup(ulong sender, CommandEnvelope envelope)
	{
		var itemId = envelope.Command.Identity.InstanceId;
		if (_pendingPickups.TryEnqueue(sender, itemId, null, _time.NowMs))
		{
			_pendingEnvelopes[(sender, itemId)] = envelope;
			_log.LogInformation("Item pickup {ItemId} from {Sender} queued — registration has not arrived yet (hold {HoldMs} ms).",
				itemId, sender, PendingPickupQueue.DefaultHoldMs);
		}
		else
		{
			_log.LogWarning("Item pickup {ItemId} from {Sender} already queued — duplicate claim dropped silently.", itemId, sender);
		}
	}

	private void SettlePendingPickups(ulong itemId)
	{
		while (true)
		{
			var pending = _pendingPickups.TryTakeFirst(itemId);
			if (pending is null)
			{
				return;
			}

			var key = (pending.Sender, itemId);
			if (!_pendingEnvelopes.TryGetValue(key, out var envelope))
			{
				continue;
			}

			_pendingEnvelopes.Remove(key);

			if (_authority.FindItem(itemId) is null)
			{
				SendCommandRejected(pending.Sender, envelope.Command, RejectionReason.UnknownAggregate);
				continue;
			}

			var command = ResolveCommandRevision(KernelWireMapper.FromWireCommand(envelope.Command, envelope.Header));
			if (_authority.TryExecuteCommand(command, pending.Sender, out _, out var rejection))
			{
				foreach (var loser in _pendingPickups.TakeByItem(itemId))
				{
					_pendingEnvelopes.Remove((loser.Sender, itemId));
					SendCommandRejected(loser.Sender, envelope.Command, RejectionReason.Conflict);
				}

				return;
			}

			SendCommandRejected(pending.Sender, envelope.Command, rejection!.Reason);
		}
	}

	private static WireCommand PendingIdentity(ulong itemId) =>
		new() { Identity = new WireItemIdentity { InstanceId = itemId } };

	private void HandleMissingCarriedUpdate(ulong sender, CommandEnvelope envelope)
	{
		var command = envelope.Command;
		var parent = ToCharacterItem(command.Identity, command.Data);
		var spawn = new SpawnItemCommand(
			new OperationId(envelope.Header.OperationId),
			new ActorId(sender),
			new RunEpoch(envelope.Header.RunEpoch),
			AuthorityKind.OwnerPredictedHostValidated,
			KernelWireMapper.FromWireIdentity(command.Identity),
			ItemLocation.Carried(new ActorId(sender)),
			0,
			ItemKernelAuthority.ToKernelData(parent));
		if (_authority.TryExecuteCommand(spawn, sender, out _, out var rejection))
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
		var sync = KernelWireMapper.FromWireCommand(command, envelope.Header);
		if (!_authority.TryExecuteCommand(sync, sender, out _, out var rejection))
		{
			_log.LogWarning("Container sync for {ItemId} from {Sender} rejected: {Reason} ({Message}).",
				parentId, sender, rejection!.Reason, rejection.Message);
			return;
		}

		_log.LogInformation("Container sync for {ItemId} from {Sender}: {Children} child fact(s).",
			parentId, sender, command.ContainerChildren.Count);
	}

	private void SendCommandRejected(ulong targetSteamId, WireCommand original, RejectionReason reason)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || targetSteamId == 0)
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
					RunEpoch = _authority.CreateCheckpoint().RunEpoch.Value,
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
		_sender.Send(targetSteamId, NetMsg.KernelEnvelope, frame);
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

	private ItemState? FindRevision(ulong itemId) => _authority.FindItem(itemId);

	private static CharacterItemMsg ToCharacterItem(WireItemIdentity identity, WireItemData? data)
	{
		var state = new ItemState(
			KernelWireMapper.FromWireIdentity(identity),
			0,
			ItemLocation.Terminal())
		{
			Data = data is null ? ItemData.Empty : KernelWireMapper.FromWireData(data),
		};
		return ItemKernelAuthority.ToCharacterItem(state);
	}
}
