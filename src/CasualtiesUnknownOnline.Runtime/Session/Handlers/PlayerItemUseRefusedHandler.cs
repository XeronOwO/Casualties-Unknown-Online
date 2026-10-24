using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → the operator whose cross-player use it refused: the refusal the host decided is shown on the screen of the
/// player who performed the gesture (the user's 2026-10-24 ruling — a line plus the game's own deny sound, never a
/// gesture that silently did nothing).
///
/// <para>
/// Same two receiving-side checks as the inventory-intent refusal: this process must be the member, and the frame
/// must come from this session's host, because a host-to-guest frame naming another sender is a claim.
/// </para>
/// </summary>
[PacketHandler(NetMsg.PlayerItemUseRefused, NetMessageDirection.HostToGuest)]
public sealed class PlayerItemUseRefusedHandler(
	IPlayerInteractionControl interaction,
	ISessionNotices notices,
	ILocalizationService localization,
	ILogger<PlayerItemUseRefusedHandler> log)
	: PacketHandlerBase<PlayerItemUseRefusedMsg, ISessionHandlerContext>
{
	private readonly IPlayerInteractionControl _interaction = interaction;
	private readonly ISessionNotices _notices = notices;
	private readonly ILocalizationService _localization = localization;
	private readonly ILogger<PlayerItemUseRefusedHandler> _log = log;

	protected override void Handle(ulong sender, PlayerItemUseRefusedMsg msg, ISessionHandlerContext ctx)
	{
		if (ctx.Session.Role != SessionRole.Guest)
		{
			_log.LogWarning("[ItemUse] a refusal arrived from {Sender} while this process is {Role} — dropped.", sender, ctx.Session.Role);
			return;
		}

		if (sender != ctx.Session.HostSteamId)
		{
			_log.LogWarning("[ItemUse] a refusal arrived from {Sender}, which is not this session's host — dropped.", sender);
			return;
		}

		// Warning: this is the one place the player's own gesture was contradicted, next to the capture line that
		// preceded it.
		_log.LogWarning("[ItemUse] the host refused the use on {Target} (item {Item}): {Reason} — nothing on your side changed.",
			msg.TargetSteamId, msg.ItemInstanceId, msg.Reason);

		_notices.Publish(_localization.Format("hud.use_refused", msg.Reason));
		_interaction.FireUseRefused();
	}
}
