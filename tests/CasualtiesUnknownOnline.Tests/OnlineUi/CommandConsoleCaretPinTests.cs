using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The caret pin for the command console's input row (user report 2026-10-27, twice: "the cursor is still misaligned"). The row DRAWS its glyphs
/// token by token with <c>SegmentStyle()</c> — the input style with its padding zeroed — because a padded style insets what it draws; the caret, the
/// selection band, the IME line and the click-to-cursor hit test must measure with that SAME style, or each of them lands one padding away from the
/// character it belongs to. The defect was exact: they measured with <c>InputStyle()</c>, whose <c>CalcSize</c> lays the content out padding
/// included, so the caret sat a constant offset past the end of the text.
///
/// <para>
/// Reach, stated rather than implied: this is a SOURCE-SHAPE pin over one file. It catches a measurement added on the padded style (the shape the
/// defect had) and it cannot see a wrong <c>CalcSize</c> on the right style, a font mismatch, or how the row looks — the frame is the judge for
/// those, and the ticket carries it.
/// </para>
/// </summary>
public class CommandConsoleCaretPinTests
{
	private const string RendererPath = "src/CasualtiesUnknownOnline.Plugin/CommandConsoleInputRenderer.cs";

	[Fact]
	public void TheRowMeasuresWithTheStyleItDrawsWith()
	{
		var source = File.ReadAllText(RepositoryFile(RendererPath));

		// `style.CalcSize(` is legitimate in exactly one place: DrawSegment, whose `style` parameter IS the zero-padded drawing style its caller
		// passes. Everywhere else the measurement must name the drawing style, never the input field's own.
		var padded = Regex.Matches(source, @"\bstyle\.CalcSize\(").Count;
		Assert.True(
			padded == 1,
			$"{padded} measurement(s) use the input style instead of the drawing style; the caret, the selection band, the IME line and the click "
			+ "hit test must all measure with SegmentStyle() (the style the glyphs are drawn with). Only DrawSegment may measure with its `style` "
			+ "parameter, which is that same style.");

		Assert.Contains("SegmentStyle().CalcSize", source, StringComparison.Ordinal);
		Assert.Contains("var draw = SegmentStyle();", source, StringComparison.Ordinal);
	}

	private static string RepositoryFile(string relativePath)
	{
		var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CasualtiesUnknownOnline.slnx")))
		{
			directory = directory.Parent;
		}

		Assert.NotNull(directory);
		return Path.Combine(directory!.FullName, relativePath);
	}
}
