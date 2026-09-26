using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The colour the adapter hands across the boundary, as the reading side spells it. Two facts matter
/// and both are cheap to get wrong: the hex is the eight-digit form the later colour work reads, and a
/// channel the game left out of range is CLAMPED — a probe that threw on a surprising colour would
/// lose the whole reading behind it.
/// </summary>
public sealed class OnlineUiNativeRgbaTests
{
	[Fact]
	public void OpaqueWhiteIsEightDigitsOfFF() =>
		Assert.Equal("#FFFFFFFF", new OnlineUiNativeRgba(1f, 1f, 1f, 1f).ToHex());

	[Fact]
	public void TransparentBlackIsEightDigitsOfZero() =>
		Assert.Equal("#00000000", new OnlineUiNativeRgba(0f, 0f, 0f, 0f).ToHex());

	[Fact]
	public void AnOutOfRangeChannelIsClampedRatherThanWrapped() =>
		Assert.Equal("#FF0080FF", new OnlineUiNativeRgba(1.5f, -1f, 0.5f, 2f).ToHex());

	[Fact]
	public void AHalfChannelRoundsToTheNearestByte() =>
		Assert.Equal("#808080FF", new OnlineUiNativeRgba(0.5f, 0.5f, 0.5f, 1f).ToHex());
}
