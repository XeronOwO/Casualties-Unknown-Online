using System;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

public class NameplateLayoutTests
{
	[Fact]
	public void AboveHead_IsCenteredHorizontallyAndSitsAboveHead()
	{
		var rect = NameplateLayout.AboveHead(100f, 200f);

		Assert.True(Math.Abs(rect.X - (100f - (NameplateLayout.Width * 0.5f))) < 0.001f);
		Assert.True(Math.Abs(rect.Y - (200f - NameplateLayout.Height - NameplateLayout.HeadGapPx)) < 0.001f);
		Assert.Equal(NameplateLayout.Width, rect.Width);
		Assert.Equal(NameplateLayout.Height, rect.Height);
	}

	[Fact]
	public void AboveHead_LeavesHeadGapBetweenLabelBottomAndHead()
	{
		var rect = NameplateLayout.AboveHead(50f, 60f);

		var bottom = rect.Y + rect.Height;
		Assert.True(Math.Abs(bottom - (60f - NameplateLayout.HeadGapPx)) < 0.001f);
	}

	[Fact]
	public void AboveHead_WorksAtScreenOrigin()
	{
		var rect = NameplateLayout.AboveHead(0f, 0f);

		Assert.True(Math.Abs(rect.X - (-(NameplateLayout.Width * 0.5f))) < 0.001f);
		Assert.True(Math.Abs(rect.Y - (-NameplateLayout.Height - NameplateLayout.HeadGapPx)) < 0.001f);
	}
}
