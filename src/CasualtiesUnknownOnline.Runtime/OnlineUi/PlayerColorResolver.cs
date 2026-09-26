using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// Resolves a stable, local-only player marker color from a SteamId. The color
/// is an automatic per-player assignment (no wire/sync surface): every peer
/// derives the same color for the same SteamId, so teammates are visually
/// distinguishable without exchanging preference data. The palette is chosen
/// for contrast on the dark CUO overlay background.
///
/// <para>
/// The same palette is also the picker's presets (ticket online-ui-art-and-controls-overhaul, S3), which
/// is why a colour and the name it is offered under live in one entry here: a second list of names in
/// the UI would be free to drift out of step with the colours it names. Every name is the tail of a
/// catalogue key (<c>prefs.color.red</c>), which a test holds the Runtime's list and the catalogue to.
/// </para>
/// </summary>
public static class PlayerColorResolver
{
	private static readonly (string Name, PlayerColorValue Color)[] Palette =
	[
		("red", new(0.90f, 0.30f, 0.28f)),
		("blue", new(0.30f, 0.55f, 0.95f)),
		("green", new(0.35f, 0.80f, 0.45f)),
		("orange", new(0.95f, 0.60f, 0.25f)),
		("purple", new(0.72f, 0.45f, 0.90f)),
		("cyan", new(0.30f, 0.78f, 0.80f)),
		("pink", new(0.95f, 0.45f, 0.72f)),
		("yellow", new(0.92f, 0.85f, 0.30f)),
	];

	/// <summary>The selectable palette in display order: the picker's swatches, and the same colours the
	/// automatic resolver assigns from.</summary>
	public static IReadOnlyList<PlayerColorValue> PaletteValues { get; } = [.. Palette.Select(static entry => entry.Color)];

	/// <summary>The palette's names in the same order, one per <see cref="PaletteValues"/> entry: the tail
	/// of each colour's catalogue key, so the picker can name the colour it is showing.</summary>
	public static IReadOnlyList<string> PaletteNames { get; } = [.. Palette.Select(static entry => entry.Name)];

	/// <summary>Returns the stable marker color for a player id.</summary>
	public static PlayerColorValue Resolve(ulong steamId)
	{
		var index = PaletteIndex(steamId);
		return Palette[index].Color;
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
