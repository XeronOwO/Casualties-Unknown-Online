namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// Resolves a stable, local-only player marker color from a SteamId. The color
/// is an automatic per-player assignment (no wire/sync surface): every peer
/// derives the same color for the same SteamId, so teammates are visually
/// distinguishable without exchanging preference data. The palette is chosen
/// for contrast on the dark CUO overlay background.
/// </summary>
public static class PlayerColorResolver
{
	private static readonly PlayerColorValue[] Palette =
	[
		new(0.90f, 0.30f, 0.28f), // red
		new(0.30f, 0.55f, 0.95f), // blue
		new(0.35f, 0.80f, 0.45f), // green
		new(0.95f, 0.60f, 0.25f), // orange
		new(0.72f, 0.45f, 0.90f), // purple
		new(0.30f, 0.78f, 0.80f), // cyan
		new(0.95f, 0.45f, 0.72f), // pink
		new(0.92f, 0.85f, 0.30f), // yellow
	];

	/// <summary>Returns the stable marker color for a player id.</summary>
	public static PlayerColorValue Resolve(ulong steamId)
	{
		var index = PaletteIndex(steamId);
		return Palette[index];
	}

	private static int PaletteIndex(ulong steamId)
	{
		unchecked
		{
			var h = steamId * 0x9E3779B97F4A7C15UL;
			h ^= h >> 32;
			return (int)(h % (ulong)Palette.Length);
		}
	}
}
