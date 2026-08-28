using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Versioning;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
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
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ItemKernelAuthority _authority = authority;
	private readonly ILogger _log = log;

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

		var command = ResolveCommandRevision(KernelWireMapper.FromWireCommand(envelope.Command, envelope.Header));
		if (!_authority.TryExecuteCommand(command, sender, out _, out var rejection))
		{
			_log.LogWarning("Kernel command from {Sender} rejected: {Reason} ({Message}).",
				sender, rejection!.Reason, rejection.Message);
			SendCommandRejected(sender, envelope.Command, rejection.Reason);
		}
	}

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
