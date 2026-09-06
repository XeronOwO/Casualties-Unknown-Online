using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Guest-side coalescing buffer for reliable medical injection deltas. The
/// native syringe minigame emits a small delta on many frames; the first
/// report of a burst is sent immediately for low latency and the remaining
/// reports within the adaptive interval are accumulated into one reliable
/// <c>MedicalOperationUpdate</c>. Because the transport stays reliable, a
/// slow treatment never accumulates a lost intermediate delta into a wrong
/// final dose; the terminal EndRequest total is the final reconciliation.
/// </summary>
internal sealed class MedicalInjectionReportBuffer
{
	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly AdaptiveStreamRateService _adaptiveRates;
	private readonly ITimeSource _time;
	private readonly ILogger _log;
	private readonly Dictionary<ulong, Pending> _pending = [];

	internal MedicalInjectionReportBuffer(
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

	internal void Queue(ulong operationId, float deltaMl)
	{
		if (!_pending.TryGetValue(operationId, out var pending))
		{
			pending = new Pending();
			_pending[operationId] = pending;
			Send(operationId, deltaMl, pending);
			return;
		}

		pending.AccumulatedMl += deltaMl;
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
			if (pending.AccumulatedMl <= 0f || now < pending.NextFlushMs)
			{
				continue;
			}

			Send(entry.Key, pending.AccumulatedMl, pending);
		}
	}

	internal void FlushBeforeTerminal(ulong operationId)
	{
		if (!_pending.TryGetValue(operationId, out var pending))
		{
			return;
		}

		if (pending.AccumulatedMl > 0f)
		{
			Send(operationId, pending.AccumulatedMl, pending);
		}

		_pending.Remove(operationId);
	}

	internal void Clear(ulong operationId) => _pending.Remove(operationId);

	internal void ClearAll() => _pending.Clear();

	private void Send(ulong operationId, float deltaMl, Pending pending)
	{
		var msg = new MedicalOperationUpdateMsg
		{
			OperationId = operationId,
			PieceIndex = -1,
			DeltaMl = deltaMl,
		};

		_sender.Send(_session.HostSteamId, NetMsg.MedicalOperationUpdate, msg, reliable: true);
		var intervalMs = _adaptiveRates.GetSendIntervalMs(
			AdaptiveStreamId.MedicalInjectionReport,
			_session.HostSteamId);
		pending.AccumulatedMl = 0f;
		pending.NextFlushMs = _time.NowMs + intervalMs;
		_log.LogDebug(
			"[MedicalOps] coalesced injection report {OperationId} sent {Delta:F3} ml (next {Next} ms).",
			operationId, deltaMl, intervalMs);
	}

	private sealed class Pending
	{
		internal float AccumulatedMl;
		internal long NextFlushMs;
	}
}
