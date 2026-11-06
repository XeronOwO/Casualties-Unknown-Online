// eval declaration: radline-timer
// declares: AcceptanceRadlineTimer
// serves: radiation-line-descent-rate-per-side (the host must own when the line comes down).
//
// Reads the two clocks that decide the vanilla activation (`WorldGeneration.Update`:
// `layerTimeSpent += deltaTime; if (layerTimeSpent > maxTimePerLayer && !line.active && biomeOverride == None) line.Activate()`),
// plus the line's own active flag, so a host/guest pair can be compared field for field instead of by eye.
using System;
using System.Reflection;

public static class AcceptanceRadlineTimer
{
	internal static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

	/// <summary>`layerTime:max:biome:active` or the reason it cannot be read.</summary>
	public static string Read()
	{
		var world = WorldGeneration.world;
		if (world == null)
		{
			return "no-world";
		}

		var line = RadiationLine.line;
		return world.layerTimeSpent.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
			+ ":" + world.maxTimePerLayer.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
			+ ":" + world.biomeOverride
			+ ":" + (line == null ? "no-line" : line.active ? "active" : "inactive");
	}
}
