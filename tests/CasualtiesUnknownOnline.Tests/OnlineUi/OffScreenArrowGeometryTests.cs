using System;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

public class OffScreenArrowGeometryTests
{
	private const float Width = 1280f;
	private const float Height = 720f;
	private const float Margin = 24f;

	[Fact]
	public void Center_IsOnScreen()
	{
		var p = OffScreenArrowGeometry.Place(640f, 360f, Width, Height, Margin);

		Assert.Equal(OffScreenArrowDirection.None, p.Direction);
		Assert.True(Math.Abs(p.X - 640f) < 0.001f);
		Assert.True(Math.Abs(p.Y - 360f) < 0.001f);
		Assert.True(OffScreenArrowGeometry.IsOnScreen(640f, 360f, Width, Height, Margin));
	}

	[Fact]
	public void InsideMargin_IsOnScreen()
	{
		var p = OffScreenArrowGeometry.Place(30f, 30f, Width, Height, Margin);

		Assert.Equal(OffScreenArrowDirection.None, p.Direction);
	}

	[Fact]
	public void LeftOfScreen_PinsLeftEdge()
	{
		var p = OffScreenArrowGeometry.Place(-100f, 360f, Width, Height, Margin);

		Assert.Equal(OffScreenArrowDirection.Left, p.Direction);
		Assert.True(Math.Abs(p.X - Margin) < 0.001f);
		Assert.InRange(p.Y, Margin, Height - Margin);
	}

	[Fact]
	public void RightOfScreen_PinsRightEdge()
	{
		var p = OffScreenArrowGeometry.Place(2000f, 360f, Width, Height, Margin);

		Assert.Equal(OffScreenArrowDirection.Right, p.Direction);
		Assert.True(Math.Abs(p.X - (Width - Margin)) < 0.001f);
		Assert.InRange(p.Y, Margin, Height - Margin);
	}

	[Fact]
	public void AboveScreen_PinsTopEdge()
	{
		var p = OffScreenArrowGeometry.Place(640f, -100f, Width, Height, Margin);

		Assert.Equal(OffScreenArrowDirection.Up, p.Direction);
		Assert.True(Math.Abs(p.Y - Margin) < 0.001f);
		Assert.InRange(p.X, Margin, Width - Margin);
	}

	[Fact]
	public void BelowScreen_PinsBottomEdge()
	{
		var p = OffScreenArrowGeometry.Place(640f, 1000f, Width, Height, Margin);

		Assert.Equal(OffScreenArrowDirection.Down, p.Direction);
		Assert.True(Math.Abs(p.Y - (Height - Margin)) < 0.001f);
		Assert.InRange(p.X, Margin, Width - Margin);
	}

	[Fact]
	public void Corner_OutsideLeftAndTop_PinsOneEdge()
	{
		var p = OffScreenArrowGeometry.Place(-200f, -200f, Width, Height, Margin);

		Assert.NotEqual(OffScreenArrowDirection.None, p.Direction);
		Assert.InRange(p.X, Margin, Width - Margin);
		Assert.InRange(p.Y, Margin, Height - Margin);
	}

	[Fact]
	public void InvalidBounds_ReturnsOnScreenNoop()
	{
		var p = OffScreenArrowGeometry.Place(10f, 10f, 0f, 0f, Margin);

		Assert.Equal(OffScreenArrowDirection.None, p.Direction);
	}
}
