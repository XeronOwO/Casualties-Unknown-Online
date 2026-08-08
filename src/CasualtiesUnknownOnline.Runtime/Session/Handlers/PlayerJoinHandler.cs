using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest side: self-activation (the host assigned our id) or a roster
/// announcement (another member joined — upsert with its spawn anchor).
/// The entity domain owns both paths (ProcessPlayerJoin).
/// </summary>
[PacketHandler(NetMsg.PlayerJoin)]
public sealed class PlayerJoinHandler(SessionService session, EntitySyncService entities)
	: PacketHandlerBase<PlayerJoinMsg>(session)
{
	private readonly EntitySyncService _entities = entities;

	protected override void Handle(ulong sender, PlayerJoinMsg msg)
	{
		if (Session.Role != SessionRole.Guest)
		{
			return;
		}

		_entities.ProcessPlayerJoin(msg);
	}
}
