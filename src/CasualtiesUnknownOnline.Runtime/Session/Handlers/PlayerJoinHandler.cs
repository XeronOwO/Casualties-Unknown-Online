using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Steam;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest side: self-activation (the host assigned our id) or a roster
/// announcement (another member joined — upsert with its spawn anchor).
/// The entity domain owns both paths (ProcessPlayerJoin).
/// </summary>
[PacketHandler(NetMsg.PlayerJoin, NetMessageDirection.HostToGuest)]
public sealed class PlayerJoinHandler : PacketHandlerBase<PlayerJoinMsg, IEntitySessionHandlerContext>
{
	protected override void Handle(ulong sender, PlayerJoinMsg msg, IEntitySessionHandlerContext ctx)
	{
		if (ctx.Session.Role != SessionRole.Guest)
		{
			return;
		}

		// Store the roster display name and selected color on the presence table
		// so IP-direct UIs can render the custom name without a Steam persona
		// lookup and every peer can show the owner's chosen marker color.
		if (msg.GuestSteamId != 0)
		{
			var member = ctx.Session.GetOrCreateMember(msg.GuestSteamId);
			if (!string.IsNullOrWhiteSpace(msg.DisplayName))
			{
				member.DisplayName = IpDisplayNamePolicy.Normalize(msg.DisplayName);
			}

			member.SelectedColor = msg.HasColor ? msg.Color.ToNetColorRgba() : (NetColorRgba?)null;
		}

		ctx.Entities.ProcessPlayerJoin(msg);
	}
}
