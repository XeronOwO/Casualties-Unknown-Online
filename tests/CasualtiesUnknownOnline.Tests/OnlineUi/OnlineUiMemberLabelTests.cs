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
}
