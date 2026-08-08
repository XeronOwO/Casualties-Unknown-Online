using System;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Marks a handler class for the given message type (read by the router at registration).</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PacketHandlerAttribute(NetMsg msg) : Attribute
{
	public NetMsg Msg { get; } = msg;
}
