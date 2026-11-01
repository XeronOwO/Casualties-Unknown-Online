using System;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

public sealed class OnlineUiMemberLabelTests
{
	[Fact]
	public void DeadMember_AppendsDeadSuffixToContextTitle()
		=> Assert.Equal("Alice(dead)", OnlineUiMemberLabel.FormatContextTitle("Alice", true, "(dead)"));

	[Fact]
	public void LivingMember_DoesNotAppendDeadSuffixToContextTitle()
		=> Assert.Equal("Alice", OnlineUiMemberLabel.FormatContextTitle("Alice", false, "(dead)"));

	[Fact]
	public void UnconsciousMember_DoesNotAppendDeadSuffixToContextTitle()
		=> Assert.Equal("Alice", OnlineUiMemberLabel.FormatContextTitle("Alice", false, "(dead)"));

	[Fact]
	public void LongDeadName_KeepsTheSuffixAndShortensTheName()
	{
		// The panel is a fixed 240 px wide and the title is one line: a long name used to push the marker off the edge, so the
		// one piece the player acts on disappeared. The marker survives at any name length.
		var title = OnlineUiMemberLabel.FormatContextTitle("A name far longer than the panel can draw", true, "(dead)");

		Assert.EndsWith("(dead)", title, StringComparison.Ordinal);
		Assert.True(title.Length <= OnlineUiMemberLabel.MaxTitleChars, $"title length {title.Length}");
		Assert.Contains("…", title, StringComparison.Ordinal);
	}

	[Fact]
	public void ShortDeadName_IsNotShortened()
		=> Assert.Equal("Alice(dead)", OnlineUiMemberLabel.FormatContextTitle("Alice", true, "(dead)"));
}
