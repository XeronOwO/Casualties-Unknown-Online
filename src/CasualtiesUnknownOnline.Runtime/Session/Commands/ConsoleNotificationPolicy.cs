using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// Which console lines a CLOSED console shows as transient notifications. It is
/// policy rather than rendering, so it lives beside <see cref="ConsoleFadePolicy"/>
/// and is machine-verified there; the UI owns only alpha and placement.
///
/// Two filters, and the first one is why this exists. A line that is not
/// notifiable is HISTORY-ONLY (<see cref="ConsoleLine.Notifiable"/>): a report
/// prints one headline and then its itemized detail, and without this the detail
/// would occupy the whole newest-few window, push the player's other notices out
/// of it, and re-announce one event several times. The second filter is the fade:
/// a line whose hold and fade have both run out is not shown at all — it stays in
/// the history, which is the record.
/// </summary>
public static class ConsoleNotificationPolicy
{
	/// <summary>Below this alpha a line is invisible, so showing it would only widen an empty panel.</summary>
	private const float VisibleAlpha = 0.01f;

	/// <summary>
	/// The newest <paramref name="max"/> notifiable lines that are still visible at
	/// <paramref name="nowUtc"/>, newest first.
	/// </summary>
	public static IReadOnlyList<ConsoleLine> Recent(
		IReadOnlyList<ConsoleLine> lines,
		DateTime nowUtc,
		TimeSpan hold,
		TimeSpan fade,
		int max)
	{
		if (max <= 0)
		{
			return [];
		}

		var visible = new List<ConsoleLine>(Math.Min(max, lines.Count));
		for (var i = lines.Count - 1; i >= 0 && visible.Count < max; i--)
		{
			var line = lines[i];
			if (!line.Notifiable)
			{
				continue;
			}

			var age = nowUtc - new DateTime(line.CreatedAtUtcTicks, DateTimeKind.Utc);
			if (ConsoleFadePolicy.ComputeAlpha(age, hold, fade) > VisibleAlpha)
			{
				visible.Add(line);
			}
		}

		return visible;
	}
}
