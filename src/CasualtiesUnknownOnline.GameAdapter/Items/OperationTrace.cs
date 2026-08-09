using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Operation-level trace for the item sync paths: one player operation spans
/// several hooks (drop → throw / re-pick / frame-end flush), and the old
/// per-hook logs could not reconstruct which events belonged to one operation.
/// The trace emits a BEGIN line when a cross-frame operation starts and ONE
/// aggregated END line when it resolves — an audit-ready trail
/// (op=.., item=.., origin=.., result=.., events=[..]). The op id is allocated
/// at the operation start (the first hook, AFTER the reentry guards — remote
/// echoes are not player operations and never get an op) and carried in the
/// pending-drop state when the operation spans frames. A cross-frame operation
/// that never resolves (a pending drop that stays pending) shows up as a begin
/// without an end — the leak the baseline asserts on. Observability only; no
/// behavior. The class itself is stateless (no registry, so no leak and no
/// cleanup); the long counter is single-threaded (all hooks run on the main
/// thread).
/// </summary>
internal sealed class OperationTrace(ILogger<OperationTrace> log)
{
	private readonly ILogger<OperationTrace> _log = log;

	/// <summary>Operation ids are session-global counters — one id identifies ONE player operation across all its hooks.</summary>
	private long _nextOp;

	internal long NextOperationId() => _nextOp++;

	/// <summary>Opens a cross-frame operation (drop → throw / flush / re-pick). The id must be kept by the caller (e.g. in the pending-drop state) until it resolves.</summary>
	internal void Begin(long op, ulong itemId, string origin, string eventName) =>
		_log.LogInformation("[ItemTrace] op={Op} begin item={ItemId} origin={Origin} event={Event}", op, itemId, origin, eventName);

	/// <summary>Closes an operation with one aggregated line. Events are the decision chain in order (e.g. ["Drop", "Throw"]); result carries the disposition ("Reported"/"Cancelled"/"Skipped") plus the network message count for reported ones.</summary>
	internal void End(long op, ulong itemId, string origin, string result, params string[] events) =>
		_log.LogInformation("[ItemTrace] op={Op} item={ItemId} origin={Origin} result={Result} events=[{Events}]",
			op, itemId, origin, result, string.Join(", ", events));

	/// <summary>Instance id of an item for tracing (0 when it has none). Unity == — a destroyed component must not yield a stale id.</summary>
	internal static ulong IdOf(Item item)
	{
		var idComp = item.GetComponent<ItemInstanceId>();
		return idComp != null ? idComp.Id : 0; // Unity object — ==
	}
}
