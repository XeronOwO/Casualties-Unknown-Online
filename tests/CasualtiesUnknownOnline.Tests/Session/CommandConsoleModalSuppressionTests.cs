using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public class CommandConsoleModalSuppressionTests
{
	[Fact]
	public void StaysOpen_DoesNotSuppress()
	{
		var suppression = new CommandConsoleModalSuppression();

		Assert.False(suppression.Update(consoleOpen: true));
		Assert.False(suppression.Update(consoleOpen: true));
	}

	[Fact]
	public void Close_SuppressesExactlyOneFrame()
	{
		var suppression = new CommandConsoleModalSuppression();

		Assert.False(suppression.Update(consoleOpen: true));
		Assert.True(suppression.Update(consoleOpen: false));
		Assert.False(suppression.Update(consoleOpen: false));
	}

	[Fact]
	public void ClosedFromStart_DoesNotSuppress()
	{
		var suppression = new CommandConsoleModalSuppression();

		Assert.False(suppression.Update(consoleOpen: false));
		Assert.False(suppression.Update(consoleOpen: false));
	}

	[Fact]
	public void ReopenThenClose_SuppressesCloseFrame()
	{
		var suppression = new CommandConsoleModalSuppression();

		Assert.False(suppression.Update(consoleOpen: false));
		Assert.False(suppression.Update(consoleOpen: true));
		Assert.True(suppression.Update(consoleOpen: false));
	}
}
