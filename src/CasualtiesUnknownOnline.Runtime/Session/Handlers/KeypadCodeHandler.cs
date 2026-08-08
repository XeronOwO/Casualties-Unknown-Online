using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The host's keypad codes arrived — write them onto the local Openables
/// (the game lazy-generates per side otherwise, giving every side its own
/// code). Host → guest only (direction-validated by PacketReceiver).
/// </summary>
[PacketHandler(NetMsg.KeypadCode)]
public sealed class KeypadCodeHandler : PacketHandlerBase<KeypadCodeMsg>
{
	protected override void Handle(ulong sender, KeypadCodeMsg msg, HandlerContext ctx) =>
		ctx.World.FireKeypadCodeReceived(msg.Codes);
}
