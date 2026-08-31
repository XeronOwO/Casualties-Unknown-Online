using System;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public class ConsoleFadePolicyTests
{
	private static readonly TimeSpan Hold = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan Fade = TimeSpan.FromSeconds(5);

	[Fact]
	public void BeforeHold_ReturnsFullOpacity()
	{
		var alpha = ConsoleFadePolicy.ComputeAlpha(TimeSpan.FromSeconds(4), Hold, Fade);

		Assert.Equal(1f, alpha);
	}

	[Fact]
	public void AtHold_ReturnsFullOpacity()
	{
		var alpha = ConsoleFadePolicy.ComputeAlpha(Hold, Hold, Fade);

		Assert.Equal(1f, alpha);
	}

	[Fact]
	public void MiddleOfFade_ReturnsFraction()
	{
		var alpha = ConsoleFadePolicy.ComputeAlpha(Hold + TimeSpan.FromMilliseconds(Fade.TotalMilliseconds / 2), Hold, Fade);

		Assert.True(Math.Abs(alpha - 0.5f) < 0.001f, $"Expected 0.5, got {alpha}");
	}

	[Fact]
	public void AfterFade_ReturnsZero()
	{
		var alpha = ConsoleFadePolicy.ComputeAlpha(Hold + Fade + TimeSpan.FromMilliseconds(1), Hold, Fade);

		Assert.Equal(0f, alpha);
	}
}
