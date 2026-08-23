using System;
using System.Collections.Generic;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.Handlers;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The single protocol message registry. Built once from every
/// <see cref="IPacketHandler"/> in the Runtime assembly: the handler's
/// <see cref="PacketHandlerAttribute"/> supplies the wire id and locked
/// direction, and its <c>PacketHandlerBase&lt;TPacket&gt;</c> base supplies the
/// payload type. The receiver validates every incoming frame against this
/// registry and fails closed — a message id that is not registered is dropped.
/// No per-message switch exists anymore; a new message must be added as a
/// handler with an explicit direction or it will never be accepted.
/// </summary>
public static class NetMessageRegistry
{
	/// <summary>All protocol messages currently known to the runtime.</summary>
	public static IReadOnlyDictionary<NetMsg, NetMessageMetadata> All { get; } = Build();

	/// <summary>Gets the metadata for a registered message id.</summary>
	public static bool TryGet(NetMsg msg, out NetMessageMetadata metadata) => All.TryGetValue(msg, out metadata);

	private static Dictionary<NetMsg, NetMessageMetadata> Build()
	{
		var messages = new Dictionary<NetMsg, NetMessageMetadata>();
		foreach (var handlerType in typeof(NetMessageRegistry).Assembly.GetTypes())
		{
			if (handlerType.IsAbstract || handlerType.IsInterface || !typeof(IPacketHandler).IsAssignableFrom(handlerType))
			{
				continue;
			}

			var attribute = handlerType.GetCustomAttribute<PacketHandlerAttribute>(inherit: false)
				?? throw new InvalidOperationException(
					$"Packet handler {handlerType.Name} lacks a [PacketHandler] attribute.");

			var payloadType = FindPayloadType(handlerType)
				?? throw new InvalidOperationException(
					$"Packet handler {handlerType.Name} does not derive from PacketHandlerBase<TPacket>.");

			var metadata = new NetMessageMetadata(attribute.Msg, attribute.Direction, payloadType);
			if (messages.ContainsKey(metadata.Msg))
			{
				throw new InvalidOperationException($"Duplicate packet-handler registration for {metadata.Msg}.");
			}

			messages.Add(metadata.Msg, metadata);
		}

		return messages;
	}

	private static Type? FindPayloadType(Type handlerType)
	{
		for (var current = handlerType.BaseType; current is not null; current = current.BaseType)
		{
			if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(PacketHandlerBase<>))
			{
				return current.GetGenericArguments()[0];
			}
		}

		return null;
	}
}
