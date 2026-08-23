using System;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Base class for packet handlers: <typeparamref name="TPacket"/> is the
/// protobuf message class and <typeparamref name="TContext"/> is the narrow
/// handler-context interface the concrete handler needs. <see cref="Process"/>
/// decodes the frame payload and hands the handler only the capability
/// interface it declared — the broad <see cref="HandlerContext"/> composition
/// root stays at the dispatch seam. Handlers take no service constructor
/// dependencies, which keeps the constructor graph acyclic (SessionService →
/// gateway → router → handlers would otherwise cycle back into the session).
/// </summary>
public abstract class PacketHandlerBase<TPacket, TContext> : IPacketHandler
	where TPacket : class
	where TContext : class
{
	public void Process(ulong sender, byte[] frame, HandlerContext ctx)
	{
		if (ctx is not TContext narrow)
		{
			throw new InvalidOperationException(
				$"Packet handler {GetType().Name} requires a {typeof(TContext).Name} handler context, "
				+ $"but received {ctx.GetType().Name}.");
		}

		Handle(sender, NetPacket.DecodePayload<TPacket>(frame), narrow);
	}

	protected abstract void Handle(ulong sender, TPacket msg, TContext ctx);
}
