using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// A UI surface's "say this once" log: the same fact reported on every frame is noise, and the fact that a marker vanished or a label does not fit is worth
/// exactly one line per surface. The key is what makes two reports independent — a surface that reports a missing camera and a refused projection says both
/// once, not one of them.
/// </summary>
/// <remarks>
/// Its own type because the world overlay carried three such methods and a set of keys, and the aggregate line limit counts methods that only exist to wrap a
/// logger. Any other overlay surface can hold one of these instead of growing its own set.
/// </remarks>
internal sealed class ReportOnce(ILogger log)
{
	private readonly HashSet<string> _reported = [];

	/// <summary>Logs at Debug the first time this key is reported; every later call with the same key is silent.</summary>
	internal void Debug(string key, string message)
	{
		if (_reported.Add(key))
		{
			log.LogDebug("{Message}", message);
		}
	}

	/// <summary>Logs a warning the first time this key is reported; every later call with the same key is silent.</summary>
	internal void Warning(string key, string message)
	{
		if (_reported.Add(key))
		{
			log.LogWarning("{Message}", message);
		}
	}
}
