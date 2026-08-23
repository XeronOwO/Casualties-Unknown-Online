using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Diagnostics probe — echo the sender's tick back as a pong.</summary>
[PacketHandler(NetMsg.Ping, NetMessageDirection.Bidirectional)]
public sealed class PingHandler(PacketSender sender) : PacketHandlerBase<PingMsg, IEmptyHandlerContext>
{
	private readonly PacketSender _sender = sender;

	protected override void Handle(ulong sender, PingMsg msg, IEmptyHandlerContext ctx) =>
		_sender.Send(sender, NetMsg.Pong, new PongMsg { Ticks = msg.Ticks });
}
