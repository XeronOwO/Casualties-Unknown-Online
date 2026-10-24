using CasualtiesUnknownOnline.GameAdapter.Run;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Loading;

/// <summary>
/// The wait line's takeover of the game's own loading text: what is captured, what is handed back, and what a
/// release with nothing captured does. The Unity write is five lines of field access; THESE are the decisions that
/// can be wrong, which is why they live in a case instead of in the surface.
/// </summary>
[Trait("Category", "Unit")]
public class LoadingTextTakeoverTests
{
	[Fact]
	public void Take_CapturesTheGamesTextOnce()
	{
		var takeover = new LoadingTextTakeover();

		Assert.Equal("gencreatingterrain", takeover.Take("gencreatingterrain"));

		// A later write of the same takeover (the countdown ticks every second) must not lose the game's text.
		Assert.Equal("gencreatingterrain", takeover.Take("Waiting for the host to load… (3s)."));
		Assert.Equal("gencreatingterrain", takeover.Captured);
		Assert.True(takeover.IsActive);
	}

	[Fact]
	public void Release_HandsTheCapturedTextBackAndForgetsIt()
	{
		var takeover = new LoadingTextTakeover();
		takeover.Take("genplacingentities");

		Assert.Equal("genplacingentities", takeover.Release());
		Assert.False(takeover.IsActive);
		Assert.Null(takeover.Captured);

		// A second takeover is a new one: the game may have written another phase since.
		Assert.Equal("genplacingplayer", takeover.Take("genplacingplayer"));
	}

	[Fact]
	public void Release_WithNothingCaptured_WritesNothing()
	{
		var takeover = new LoadingTextTakeover();

		Assert.Null(takeover.Release());
		Assert.False(takeover.IsActive);
	}

	[Fact]
	public void Take_AnswersTheEmptyGameTextRatherThanLosingIt()
	{
		// An empty line is a fact about the game's field, not "no capture": the hand-back must restore the empty
		// string instead of leaving the wait line up forever.
		var takeover = new LoadingTextTakeover();

		Assert.Equal(string.Empty, takeover.Take(string.Empty));
		Assert.Equal(string.Empty, takeover.Release());
		Assert.False(takeover.IsActive);
	}
}
