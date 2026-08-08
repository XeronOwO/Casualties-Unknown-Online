using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.Handlers;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The receive dispatch: subscribes to the receiver's direction-valid frames
/// and routes them to the per-message handlers with the handler context.
/// SessionService does not take part in the receive path — the dispatcher is
/// resolved after the session (and the entity/data domains it depends on) are
/// built, so the handler context factories resolve safely; the constructor
/// graph stays acyclic (abstract extraction, user rule).
/// </summary>
public sealed class PacketDispatcher : ICuoService
{
	private readonly PacketReceiver _receiver;
	private readonly PacketRouter _router;
	private readonly HandlerContext _context;
	private readonly ILogger<PacketDispatcher> _log;

	public PacketDispatcher(
		PacketReceiver receiver, PacketRouter router, HandlerContext context, ILogger<PacketDispatcher> log)
	{
		_receiver = receiver;
		_router = router;
		_context = context;
		_log = log;
		receiver.MessageArrived += OnMessageArrived;
	}

	private void OnMessageArrived(ulong sender, byte[] frame)
	{
		if (_router.TryDispatch(sender, frame, _context))
		{
			return;
		}

		_log.LogWarning("No handler for {Msg} from {Sender}.", (NetMsg)frame[0], sender);
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

	void ICuoService.Dispose() => _receiver.MessageArrived -= OnMessageArrived;
}
