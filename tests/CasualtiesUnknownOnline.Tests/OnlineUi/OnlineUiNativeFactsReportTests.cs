using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The probe's log lines. A reading is only useful if a human can read the four unknowns out of the log in
/// one pass, so the cases pin the summary line's fields, the <c>unavailable</c> spelling that keeps an
/// unread fact from reading as a zero, the missing-part list in the ticket's order, the 9-slice and count
/// facts on a row line, the cap marker that stops a sampled census from reading as a complete one, and the
/// note line of an attempt that has something to explain.
/// </summary>
public sealed class OnlineUiNativeFactsReportTests
{
	[Fact]
	public void TheSummaryLineCarriesEveryFactUnderTheGrepPrefix()
	{
		var lines = OnlineUiNativeFactsReport.Describe(Complete());

		var summary = lines[0];
		Assert.True(summary.StartsWith(OnlineUiNativeFactsReport.Prefix, StringComparison.Ordinal), summary);
		Assert.True(summary.Contains("canvas=Canvas/SettingsMenu", StringComparison.Ordinal), summary);
		Assert.True(summary.Contains("font=LiberationSans SDF", StringComparison.Ordinal), summary);
		Assert.True(summary.Contains("uiScale=1.5", StringComparison.Ordinal), summary);
		Assert.True(summary.Contains("rows=1", StringComparison.Ordinal), summary);
		Assert.True(summary.Contains("chrome=2", StringComparison.Ordinal), summary);
	}

	[Fact]
	public void AnUnreadFactIsSpelledUnavailableRatherThanZero()
	{
		var summary = OnlineUiNativeFactsReport.Describe(OnlineUiNativeFacts.Unavailable("the game has no main canvas yet"))[0];

		Assert.True(summary.Contains("canvas=unavailable", StringComparison.Ordinal), summary);
		Assert.True(summary.Contains("font=unavailable", StringComparison.Ordinal), summary);
		Assert.True(summary.Contains("uiScale=unavailable", StringComparison.Ordinal), summary);
		Assert.True(summary.Contains("rows=unavailable", StringComparison.Ordinal), summary);
		Assert.True(summary.Contains("chrome=unavailable", StringComparison.Ordinal), summary);
	}

	[Fact]
	public void ACappedChromeCensusSaysSoInsteadOfReadingAsTheWholeCanvas()
	{
		var chrome = Enumerable.Range(0, OnlineUiNativeStyleCensus.DefaultCap)
			.Select(index => Image($"Sprite{index}"))
			.ToList();
		var facts = Complete() with { ChromeStyles = OnlineUiNativeStyleCensus.CollapseImageStyles(chrome) };

		var summary = OnlineUiNativeFactsReport.Describe(facts)[0];

		Assert.True(
			summary.Contains(
				$"chrome={OnlineUiNativeStyleCensus.DefaultCap} (capped at {OnlineUiNativeStyleCensus.DefaultCap})",
				StringComparison.Ordinal),
			summary);
	}

	[Fact]
	public void TheMissingListNamesTheUnknownsInTheTicketsOrder()
	{
		var missing = OnlineUiNativeFactsReport.MissingParts(OnlineUiNativeFacts.Unavailable("the game has no main canvas yet"));

		Assert.True(
			string.Join(",", missing) == "canvas,font,rowStyle,chrome,uiScale",
			$"the missing list must name the unknowns in the ticket's order, got {string.Join(",", missing)}");
	}

	[Fact]
	public void AReadingThatLacksOnlyTheRunFactsNamesExactlyThose()
	{
		var partial = Partial();
		var missing = OnlineUiNativeFactsReport.MissingParts(partial);

		Assert.True(
			string.Join(",", missing) == "font,chrome,uiScale",
			$"only the unread facts belong here, got {string.Join(",", missing)}");
		Assert.True(
			OnlineUiNativeFactsReport.Describe(partial).Any(line => line.Contains("missing: font, chrome, uiScale", StringComparison.Ordinal)),
			"the report must spell the missing parts out on their own line");
	}

	[Fact]
	public void AnEmptyChromeCensusIsMissingRatherThanComplete()
	{
		Assert.False(
			(Complete() with { ChromeStyles = [] }).IsComplete,
			"a sweep that found no chrome has not answered the chrome question, so the reading is not complete");
	}

	[Fact]
	public void ARowLineCarriesTheNineSliceFactAndTheCount()
	{
		var rowLine = OnlineUiNativeFactsReport
			.Describe(Complete())
			.Single(line => line.Contains(" row sprite=UIPanel ", StringComparison.Ordinal));

		Assert.True(rowLine.Contains("nineSlice=true", StringComparison.Ordinal), rowLine);
		Assert.True(rowLine.Contains("x3", StringComparison.Ordinal), rowLine);
		Assert.True(rowLine.Contains("border=12,12,12,12", StringComparison.Ordinal), rowLine);
	}

	[Fact]
	public void AnAttemptWithSomethingToExplainPrintsItAsANote()
	{
		var lines = OnlineUiNativeFactsReport.Describe(
			Complete() with { Note = "row GameSettingDropdown has 1 children — the game's settings row layout moved" });

		Assert.True(
			lines.Any(line => line.Contains("note: row GameSettingDropdown has 1 children", StringComparison.Ordinal)),
			"an attempt with a diagnostic must say it on its own line, and not as a failure of a complete reading");
		Assert.DoesNotContain(lines, line => line.Contains("failure:", StringComparison.Ordinal));
	}

	[Fact]
	public void ACompleteReadingCarriesNoMissingLine()
	{
		Assert.DoesNotContain(
			OnlineUiNativeFactsReport.Describe(Complete()),
			line => line.Contains("missing:", StringComparison.Ordinal));
	}

	private static OnlineUiNativeFacts Complete() => new(
		"Canvas/SettingsMenu",
		[Image("UIPanel", border: 12f, occurrences: 3)],
		[Image("UIButton", occurrences: 7), Image("UIPanel", border: 12f, occurrences: 3)],
		new OnlineUiNativeTextStyle(
			"Special/GameSettingDropdown",
			"CUO Online UI Native Host/Label",
			"LiberationSans SDF",
			14f,
			new OnlineUiNativeRgba(1f, 1f, 1f, 1f),
			1),
		1.5f,
		null);

	private static OnlineUiNativeFacts Partial() => new(
		"Canvas/SettingsMenu",
		[Image("UIPanel", border: 12f, occurrences: 3)],
		[],
		null,
		null,
		null);

	private static OnlineUiNativeImageStyle Image(string sprite, float border = 0f, int occurrences = 1) => new(
		"Special/GameSettingDropdown",
		$"CUO Online UI Native Host/{sprite}",
		sprite,
		"Sliced",
		1f,
		border,
		border,
		border,
		border,
		new OnlineUiNativeRgba(0.1f, 0.2f, 0.3f, 1f),
		occurrences);
}
