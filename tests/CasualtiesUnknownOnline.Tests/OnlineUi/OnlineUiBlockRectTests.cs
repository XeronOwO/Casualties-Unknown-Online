using CasualtiesUnknownOnline.Runtime.GameAdapter;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

public sealed class OnlineUiBlockRectTests
{
	[Fact]
	public void Contains_InsideAndEdges_AreTrue()
	{
		var block = new OnlineUiBlockRect(10f, 20f, 100f, 50f);

		Assert.True(block.Contains(10f, 20f));
		Assert.True(block.Contains(110f, 70f));
		Assert.True(block.Contains(55f, 45f));
	}

	[Fact]
	public void Contains_Outside_IsFalse()
	{
		var block = new OnlineUiBlockRect(10f, 20f, 100f, 50f);

		Assert.False(block.Contains(9.9f, 20f));
		Assert.False(block.Contains(10f, 19.9f));
		Assert.False(block.Contains(110.1f, 20f));
		Assert.False(block.Contains(10f, 70.1f));
	}

	[Fact]
	public void Contains_EmptyRect_OnlyAcceptsItsOrigin()
	{
		var block = new OnlineUiBlockRect(5f, 5f, 0f, 0f);

		Assert.True(block.Contains(5f, 5f));
		Assert.False(block.Contains(5.01f, 5f));
		Assert.False(block.Contains(5f, 5.01f));
	}
}
