using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public class ConsoleImeStateTests
{
	[Fact]
	public void InitialState_IsNotComposing()
	{
		var state = new ConsoleImeState();

		Assert.False(state.IsComposing);
		Assert.Equal("", state.Composition);
	}

	[Fact]
	public void NonEmptyComposition_IsComposingAndExposesComposition()
	{
		var state = new ConsoleImeState();
		state.Update("nihao");

		Assert.True(state.IsComposing);
		Assert.Equal("nihao", state.Composition);
	}

	[Fact]
	public void EmptyAfterComposition_StopsComposing()
	{
		var state = new ConsoleImeState();
		state.Update("nihao");
		state.Update("");

		Assert.False(state.IsComposing);
	}

	[Fact]
	public void NullComposition_IsTreatedAsEmpty()
	{
		var state = new ConsoleImeState();
		state.Update(null);

		Assert.False(state.IsComposing);
		Assert.Equal("", state.Composition);
	}

	[Fact]
	public void Clear_ResetsComposition()
	{
		var state = new ConsoleImeState();
		state.Update("nihao");
		state.Clear();

		Assert.False(state.IsComposing);
		Assert.Equal("", state.Composition);
	}
}
