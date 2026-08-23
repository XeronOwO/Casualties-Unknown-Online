using System;
using System.Collections.Generic;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.Handlers;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The receive dispatch: builds the msg → handler route table once at startup
/// (each handler carries a <see cref="PacketHandlerAttribute"/>; the dictionary
/// is read-only afterwards), subscribes to the receiver's direction-valid
/// frames and routes them to the per-message handlers with the handler
/// context — one O(1) lookup per frame. SessionService does not take part in
/// the receive path: the dispatcher is resolved after the session (and the
/// entity/data domains its context references) are built, so the handler
/// context factories resolve safely; the constructor graph stays acyclic
/// (abstract extraction, user rule).
/// </summary>
public sealed class PacketDispatcher : ICuoService
{
	private readonly PacketReceiver _receiver;
	private readonly HandlerContext _context;
	private readonly ILogger<PacketDispatcher> _log;
	private readonly Dictionary<NetMsg, IPacketHandler> _routes = [];

	public PacketDispatcher(PacketReceiver receiver, IEnumerable<IPacketHandler> handlers,
		HandlerContext context, ILogger<PacketDispatcher> log)
	{
		_receiver = receiver;
		_context = context;
		_log = log;
		foreach (var handler in handlers)
		{
			var attribute = handler.GetType().GetCustomAttribute<PacketHandlerAttribute>(inherit: false)
				?? throw new InvalidOperationException(
					$"Packet handler {handler.GetType().Name} lacks a [PacketHandler] attribute.");
			var msg = attribute.Msg;
			if (!NetMessageRegistry.TryGet(msg, out _))
			{
				throw new InvalidOperationException(
					$"Packet handler {handler.GetType().Name} is registered for {msg}, which is not in NetMessageRegistry.");
			}
			// Dictionary.TryAdd is netstandard2.1+ — net48 needs the two-step form.
			if (_routes.ContainsKey(msg))
			{
				throw new InvalidOperationException($"Duplicate packet handler for {msg}.");
			}

			_routes.Add(msg, handler);
		}

		receiver.MessageArrived += OnMessageArrived;
	}

	private void OnMessageArrived(ulong sender, byte[] frame)
	{
		var msgId = (NetMsg)frame[0];
		if (_routes.TryGetValue(msgId, out var handler))
		{
			handler.Process(sender, frame, _context);
			return;
		}

		_log.LogWarning("No handler for {Msg} from {Sender}.", msgId, sender);
	}

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Update()
	{
	}

	void ICuoService.Stop()
	{
	}

	void IDisposable.Dispose() => _receiver.MessageArrived -= OnMessageArrived;
}
