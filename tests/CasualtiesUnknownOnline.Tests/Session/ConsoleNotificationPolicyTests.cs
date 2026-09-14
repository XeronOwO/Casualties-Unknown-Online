using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Which console lines a CLOSED console announces. The rule is policy rather than
/// rendering, so it is verified here instead of in the IMGUI overlay — and the
/// distinction it draws is one a player feels: a report's itemized detail must not
/// occupy the newest-few notification window that belongs to separate events.
/// </summary>
public class ConsoleNotificationPolicyTests
{
	private static readonly TimeSpan Hold = TimeSpan.FromSeconds(8);
	private static readonly TimeSpan Fade = TimeSpan.FromSeconds(5);
	private static readonly DateTime Now = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void Recent_ReturnsTheNewestLinesFirstUpToTheCap()
	{
		var lines = new List<ConsoleLine> { Line("first"), Line("second"), Line("third") };

		var visible = ConsoleNotificationPolicy.Recent(lines, Now, Hold, Fade, max: 2);

		Assert.Equal(["third", "second"], Texts(visible));
	}

	[Fact]
	public void Recent_DoesNotSpendTheBudgetOnHistoryOnlyLines()
	{
		// The case this policy exists for: a restore report prints one headline and then
		// its items. Without the filter the items would fill the window and the earlier
		// notice would never be seen.
		var lines = new List<ConsoleLine>
		{
			Line("notice"),
			Line("detail", notifiable: false),
			Line("detail", notifiable: false),
			Line("latest"),
		};

		var visible = ConsoleNotificationPolicy.Recent(lines, Now, Hold, Fade, max: 2);

		Assert.Equal(["latest", "notice"], Texts(visible));
	}

	[Fact]
	public void Recent_DropsALineWhoseHoldAndFadeHaveRunOut()
	{
		var lines = new List<ConsoleLine> { Line("expired", age: Hold + Fade + TimeSpan.FromSeconds(1)), Line("fresh") };

		var visible = ConsoleNotificationPolicy.Recent(lines, Now, Hold, Fade, max: 5);

		Assert.Equal(["fresh"], Texts(visible));
	}

	[Fact]
	public void Recent_WithNothingToShow_IsEmpty()
	{
		Assert.Empty(ConsoleNotificationPolicy.Recent([], Now, Hold, Fade, max: 5));
		Assert.Empty(ConsoleNotificationPolicy.Recent([Line("a")], Now, Hold, Fade, max: 0));
	}

	private static List<string> Texts(IReadOnlyList<ConsoleLine> lines)
	{
		var texts = new List<string>(lines.Count);
		foreach (var line in lines)
		{
			texts.Add(line.Text);
		}

		return texts;
	}

	private static ConsoleLine Line(string text, TimeSpan? age = null, bool notifiable = true) =>
		new(ConsoleLineKind.Info, text, Now.Subtract(age ?? TimeSpan.Zero).Ticks, notifiable);
}
