using System;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Marks a handler class for the given message type (read by the router at
/// registration) and locks the message's transport direction. The direction is
/// required explicit — a new handler without a direction is a compile-time
/// error instead of silently defaulting to bidirectional (the old fail-open
/// behavior).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PacketHandlerAttribute(NetMsg msg, NetMessageDirection direction) : Attribute
{
	public NetMsg Msg { get; } = msg;

	public NetMessageDirection Direction { get; } = direction;
}
