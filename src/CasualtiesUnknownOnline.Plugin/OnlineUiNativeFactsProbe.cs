using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Drives the read-only probe of the game's own UI (ticket online-ui-art-and-controls-overhaul, S1) and
/// reports it once. The split is deliberate: the adapter owns what can only be read from inside the game
/// (the canvas, the game's settings rows, the font asset, the chrome styles,
/// <c>PlayerCamera.uiScale</c>), the Runtime owns when to stop asking
/// (<see cref="OnlineUiNativeFactsCapturePolicy"/>) and how the reading reads
/// (<see cref="OnlineUiNativeFactsReport"/>), and this class is only the wire between them.
///
/// <para>
/// It runs on the plugin's update pump and disappears from the log after one reading — the probe is
/// diagnostic, not a per-frame cost. The port is optional exactly like the adapter's other ports, so a
/// composition without an adapter logs nothing instead of failing; and the four values it prints come
/// from a real game run, which is why the probe exists at all.
/// </para>
/// </summary>
internal sealed class OnlineUiNativeFactsProbe
{
	private readonly ILogger _log;
	private readonly IOnlineUiNativeFactsQuery? _query;
	private readonly OnlineUiNativeFactsCapturePolicy _policy = OnlineUiNativeFactsCapturePolicy.Default();

	internal OnlineUiNativeFactsProbe(ILogger log, IOnlineUiNativeFactsQuery? query)
	{
		_log = log;
		_query = query;
	}

	/// <summary>One frame's chance to probe: it asks only when the policy says the interval has passed,
	/// and it logs the whole reading exactly once.</summary>
	internal void Update(long nowMs)
	{
		if (_query is null || !_policy.ShouldAttempt(nowMs))
		{
			return;
		}

		var facts = _query.Capture();
		if (_policy.NoteAttempt(facts, nowMs) == OnlineUiNativeFactsOutcome.Pending)
		{
			return;
		}

		foreach (var line in OnlineUiNativeFactsReport.Describe(facts))
		{
			_log.LogInformation("{Line}", line);
		}

		if (_policy.Outcome != OnlineUiNativeFactsOutcome.Complete)
		{
			_log.LogWarning(
				"CUO UI native facts: the probe ended {Outcome} after {Attempts} attempt(s) — the missing parts named above are what the Online UI overhaul still has to learn from a game run.",
				_policy.Outcome,
				_policy.Attempts);
		}
	}
}
