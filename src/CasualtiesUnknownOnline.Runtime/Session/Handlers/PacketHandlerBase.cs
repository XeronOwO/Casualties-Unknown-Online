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
	/// <summary>
	/// The wire byte length of the frame currently being processed. Set only for
	/// the duration of <see cref="Handle"/>; observability consumers (for
	/// example the kernel traffic classifier) use it without changing the packet
	/// handler contract.
	/// </summary>
	protected int CurrentFrameLength { get; private set; }

	public void Process(ulong sender, byte[] frame, HandlerContext ctx)
	{
		if (ctx is not TContext narrow)
		{
			throw new InvalidOperationException(
				$"Packet handler {GetType().Name} requires a {typeof(TContext).Name} handler context, "
				+ $"but received {ctx.GetType().Name}.");
		}

		CurrentFrameLength = frame.Length;
		try
		{
			Handle(sender, NetPacket.DecodePayload<TPacket>(frame), narrow);
		}
		finally
		{
			CurrentFrameLength = 0;
		}
	}

	protected abstract void Handle(ulong sender, TPacket msg, TContext ctx);
}
