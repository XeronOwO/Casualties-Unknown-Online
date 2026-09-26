using System.Collections.Generic;
using System.Globalization;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// The probe's log lines, built as a pure function of the reading so the wording, the order and the
/// missing-part list are testable without a game. The first line is the summary an operator greps for,
/// one line follows per census row, and a reading that is not complete names exactly which of the
/// ticket's four unknowns is still missing — or what the attempt noted.
/// </summary>
public static class OnlineUiNativeFactsReport
{
	/// <summary>The prefix every probe line carries, so one grep finds a whole reading.</summary>
	public const string Prefix = "CUO UI native facts";

	/// <summary>How an unread value is spelled, so an empty reading is never read as a zero.</summary>
	public const string Unavailable = "unavailable";

	public static IReadOnlyList<string> Describe(OnlineUiNativeFacts facts)
	{
		var lines = new List<string>
		{
			string.Format(
				CultureInfo.InvariantCulture,
				"{0}: canvas={1} font={2} uiScale={3} rows={4} chrome={5}",
				Prefix,
				facts.CanvasAttached ? facts.CanvasPath : Unavailable,
				facts.Text is { } text ? text.FontAssetName : Unavailable,
				facts.UiScale is { } scale ? Number(scale) : Unavailable,
				Count(facts.RowStyles.Count),
				ChromeCount(facts.ChromeStyles.Count)),
		};

		if (facts.Text is { } font)
		{
			lines.Add(Indent(string.Format(
				CultureInfo.InvariantCulture,
				"text font={0} size={1} color={2} of={3} at {4}",
				font.FontAssetName,
				Number(font.FontSize),
				font.Color.ToHex(),
				font.Source,
				font.ObjectPath)));
		}

		foreach (var style in facts.RowStyles)
		{
			lines.Add(Indent(DescribeImage("row", style)));
		}

		foreach (var style in facts.ChromeStyles)
		{
			lines.Add(Indent(DescribeImage("chrome", style)));
		}

		var missing = MissingParts(facts);
		if (missing.Count > 0)
		{
			lines.Add(Indent(string.Format(CultureInfo.InvariantCulture, "missing: {0}", string.Join(", ", missing))));
		}

		if (facts.Note is { Length: > 0 } note)
		{
			lines.Add(Indent(string.Format(CultureInfo.InvariantCulture, "note: {0}", note)));
		}

		return lines;
	}

	/// <summary>
	/// What this reading does not have, in the ticket's own order — the font asset, a settings row's image
	/// style, the chrome colours, the canvas scale — behind the surface all four need. The list is what the
	/// probe's warning names, so a partial reading says what S2 still lacks instead of only how many rows
	/// came back.
	/// </summary>
	public static IReadOnlyList<string> MissingParts(OnlineUiNativeFacts facts)
	{
		var missing = new List<string>();
		if (!facts.CanvasAttached)
		{
			missing.Add("canvas");
		}

		if (facts.Text is null)
		{
			missing.Add("font");
		}

		if (facts.RowStyles.Count == 0)
		{
			missing.Add("rowStyle");
		}

		if (facts.ChromeStyles.Count == 0)
		{
			missing.Add("chrome");
		}

		if (facts.UiScale is null)
		{
			missing.Add("uiScale");
		}

		return missing;
	}

	private static string DescribeImage(string kind, OnlineUiNativeImageStyle style) => string.Format(
		CultureInfo.InvariantCulture,
		"{0} sprite={1} type={2} ppu={3} border={4},{5},{6},{7} nineSlice={8} color={9} x{10} of={11} at {12}",
		kind,
		style.SpriteName.Length == 0 ? "none" : style.SpriteName,
		style.ImageType,
		Number(style.PixelsPerUnitMultiplier),
		Number(style.BorderLeft),
		Number(style.BorderBottom),
		Number(style.BorderRight),
		Number(style.BorderTop),
		style.IsNineSliced ? "true" : "false",
		style.Color.ToHex(),
		style.Occurrences,
		style.Source,
		style.ObjectPath);

	/// <summary>A census size, or <see cref="Unavailable"/> when it is empty: an empty census is an unread
	/// fact, not the fact that the game has none.</summary>
	private static string Count(int count) => count == 0 ? Unavailable : count.ToString(CultureInfo.InvariantCulture);

	/// <summary>The chrome census size, MARKED when it reached the cap, so a sampled census cannot be read
	/// as a complete one — the reading says it is a sample instead of leaving S2 to guess.</summary>
	private static string ChromeCount(int count) =>
		count == 0
			? Unavailable
			: count >= OnlineUiNativeStyleCensus.DefaultCap
				? string.Format(CultureInfo.InvariantCulture, "{0} (capped at {1})", count, OnlineUiNativeStyleCensus.DefaultCap)
				: count.ToString(CultureInfo.InvariantCulture);

	/// <summary>One number, invariant: the game's decimal comma must not split a log line into two fields.</summary>
	private static string Number(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);

	private static string Indent(string line) => "  " + line;
}
