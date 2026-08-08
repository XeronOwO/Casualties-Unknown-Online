using System;
using System.Collections.Generic;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Message → handler lookup. Built once at startup from the DI-registered
/// handlers (each carries a <see cref="PacketHandlerAttribute"/>); the
/// dictionary is read-only afterwards — every received frame is one O(1) lookup.
/// </summary>
public sealed class PacketRouter
{
	private readonly Dictionary<NetMsg, IPacketHandler> _routes = [];

	public PacketRouter(IEnumerable<IPacketHandler> handlers)
	{
		foreach (var handler in handlers)
		{
			var msg = handler.GetType().GetCustomAttribute<PacketHandlerAttribute>(inherit: false)?.Msg
				?? throw new InvalidOperationException(
					$"Packet handler {handler.GetType().Name} lacks a [PacketHandler] attribute.");
			// Dictionary.TryAdd is netstandard2.1+ — net48 needs the two-step form.
			if (_routes.ContainsKey(msg))
			{
				throw new InvalidOperationException($"Duplicate packet handler for {msg}.");
			}

			_routes.Add(msg, handler);
		}
	}

	/// <summary>Dispatches the frame to its handler; false when no handler is registered for the message id.</summary>
	public bool TryDispatch(ulong sender, byte[] frame)
	{
		var msgId = (NetMsg)frame[0];
		if (_routes.TryGetValue(msgId, out var handler))
		{
			handler.Process(sender, frame);
			return true;
		}

		return false;
	}
}
