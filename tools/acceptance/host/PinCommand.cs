using System;
using System.Globalization;

namespace CasualtiesUnknownOnline.AcceptanceHost;

/// <summary>
/// Keeps every game window at the bottom of the desktop and unable to take the foreground, for the given number of
/// seconds. Started detached by the launcher so the pin runs through startup, when the window is created.
/// </summary>
internal static class PinCommand
{
	public static int Run(string[] args)
	{
		var seconds = 300;
		for (var index = 0; index < args.Length; index++)
		{
			if (args[index] == "--seconds" && index + 1 < args.Length
				&& int.TryParse(args[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
				&& parsed > 0)
			{
				seconds = parsed;
				index++;
			}
		}

		WindowPin.Run(TimeSpan.FromSeconds(seconds));
		return 0;
	}
}
