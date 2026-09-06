using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Guest-side coalescing buffer for ordinary shrapnel held-piece position
/// reports. Each ordinary move is an absolute position for one piece, so only
/// the latest position per piece within the adaptive interval matters; the
/// first report after an ownership transition is sent immediately and the rest
/// are flushed together as one unreliable update per piece. Ownership
/// transitions are never routed through this buffer.
/// </summary>
internal sealed class ShrapnelPositionReportBuffer
{
	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly AdaptiveStreamRateService _adaptiveRates;
	private readonly ITimeSource _time;
	private readonly ILogger _log;
	private readonly Dictionary<(ulong OperationId, int PieceIndex), Pending> _pending = [];

	internal ShrapnelPositionReportBuffer(
		ISessionControl session,
		PacketSender sender,
		AdaptiveStreamRateService adaptiveRates,
		ITimeSource time,
		ILogger log)
	{
		_session = session;
		_sender = sender;
		_adaptiveRates = adaptiveRates;
		_time = time;
		_log = log;
	}

	internal void QueueOrdinary(ulong operationId, ShrapnelPieceUpdate update)
	{
		var key = (operationId, update.PieceIndex);
		if (!_pending.TryGetValue(key, out var pending))
		{
			pending = new Pending();
			_pending[key] = pending;
			Send(key, update, pending);
			return;
		}

		pending.X = update.X;
		pending.Y = update.Y;
		pending.HasPending = true;
	}

	internal void FlushDue()
	{
		if (_pending.Count == 0)
		{
			return;
		}

		var now = _time.NowMs;
		foreach (var entry in _pending.ToList())
		{
			var pending = entry.Value;
			if (!pending.HasPending || now < pending.NextFlushMs)
			{
				continue;
			}

			Send(entry.Key, new ShrapnelPieceUpdate
			{
				PieceIndex = entry.Key.PieceIndex,
				X = pending.X,
				Y = pending.Y,
				Grabbed = true,
			}, pending);
		}
	}

	internal void Flush(ulong operationId)
	{
		foreach (var entry in _pending.Where(e => e.Key.OperationId == operationId).ToList())
		{
			if (!entry.Value.HasPending)
			{
				continue;
			}

			Send(entry.Key, new ShrapnelPieceUpdate
			{
				PieceIndex = entry.Key.PieceIndex,
				X = entry.Value.X,
				Y = entry.Value.Y,
				Grabbed = true,
			}, entry.Value, reliable: true);
		}
	}

	internal void Clear(ulong operationId)
	{
		foreach (var key in _pending.Keys.Where(k => k.OperationId == operationId).ToList())
		{
			_pending.Remove(key);
		}
	}

	internal void FlushBeforeTerminal(ulong operationId)
	{
		Flush(operationId);
		Clear(operationId);
	}

	internal void ClearAll() => _pending.Clear();

	private void Send((ulong OperationId, int PieceIndex) key, ShrapnelPieceUpdate update, Pending pending, bool reliable = false)
	{
		var msg = new MedicalOperationUpdateMsg
		{
			OperationId = key.OperationId,
			PieceIndex = key.PieceIndex,
			X = update.X,
			Y = update.Y,
			Grabbed = true,
		};

		var intervalMs = _adaptiveRates.GetSendIntervalMs(
			AdaptiveStreamId.ShrapnelPositionReport,
			_session.HostSteamId);
		_sender.Send(_session.HostSteamId, NetMsg.MedicalOperationUpdate, msg, reliable);
		pending.HasPending = false;
		pending.NextFlushMs = _time.NowMs + intervalMs;
		_log.LogDebug(
			"[Shrapnel] coalesced position {OperationId} piece {Piece} ({X:F2},{Y:F2}) next {Next} ms.",
			key.OperationId, key.PieceIndex, update.X, update.Y, intervalMs);
	}

	private sealed class Pending
	{
		internal float X;
		internal float Y;
		internal long NextFlushMs;
		internal bool HasPending;
	}
}
