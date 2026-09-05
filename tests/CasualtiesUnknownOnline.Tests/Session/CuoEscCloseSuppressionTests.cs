using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public class CuoEscCloseSuppressionTests
{
	[Fact]
	public void CommandConsoleClose_SuppressesExactlyOneFrame()
	{
		var suppression = new CuoEscCloseSuppression();

		Assert.False(suppression.Update(commandConsoleOpen: true, onlineWindowVisible: false, quickPanelVisible: false));
		Assert.True(suppression.Update(commandConsoleOpen: false, onlineWindowVisible: false, quickPanelVisible: false));
		Assert.False(suppression.Update(commandConsoleOpen: false, onlineWindowVisible: false, quickPanelVisible: false));
	}

	[Fact]
	public void OnlineWindowClose_SuppressesExactlyOneFrame()
	{
		var suppression = new CuoEscCloseSuppression();

		Assert.False(suppression.Update(commandConsoleOpen: false, onlineWindowVisible: true, quickPanelVisible: false));
		Assert.True(suppression.Update(commandConsoleOpen: false, onlineWindowVisible: false, quickPanelVisible: false));
		Assert.False(suppression.Update(commandConsoleOpen: false, onlineWindowVisible: false, quickPanelVisible: false));
	}

	[Fact]
	public void QuickPanelClose_SuppressesExactlyOneFrame()
	{
		var suppression = new CuoEscCloseSuppression();

		Assert.False(suppression.Update(commandConsoleOpen: false, onlineWindowVisible: false, quickPanelVisible: true));
		Assert.True(suppression.Update(commandConsoleOpen: false, onlineWindowVisible: false, quickPanelVisible: false));
		Assert.False(suppression.Update(commandConsoleOpen: false, onlineWindowVisible: false, quickPanelVisible: false));
	}

	[Fact]
	public void DangerousOnGuiBeforeUpdateOrder_SuppressesCloseFrame()
	{
		var suppression = new CuoEscCloseSuppression();

		// Frame N-1: console is open when Plugin.Update runs.
		suppression.Update(commandConsoleOpen: true, onlineWindowVisible: false, quickPanelVisible: false);

		// Frame N: OnGUI closes the console before Plugin.Update; Plugin.Update
		// sees the closed state and must keep the modal guard active.
		Assert.True(suppression.Update(commandConsoleOpen: false, onlineWindowVisible: false, quickPanelVisible: false));
	}

	[Fact]
	public void NextFrame_AfterClose_ReleasesSuppression()
	{
		var suppression = new CuoEscCloseSuppression();

		suppression.Update(commandConsoleOpen: true, onlineWindowVisible: false, quickPanelVisible: false);
		Assert.True(suppression.Update(commandConsoleOpen: false, onlineWindowVisible: false, quickPanelVisible: false));
		Assert.False(suppression.Update(commandConsoleOpen: false, onlineWindowVisible: false, quickPanelVisible: false));
	}

	[Fact]
	public void SurfaceStaysOpen_DoesNotSuppress()
	{
		var suppression = new CuoEscCloseSuppression();

		Assert.False(suppression.Update(commandConsoleOpen: true, onlineWindowVisible: true, quickPanelVisible: true));
		Assert.False(suppression.Update(commandConsoleOpen: true, onlineWindowVisible: true, quickPanelVisible: true));
	}

	[Fact]
	public void OneSurfaceClosingWhileAnotherStaysOpen_StillSuppresses()
	{
		var suppression = new CuoEscCloseSuppression();

		Assert.False(suppression.Update(commandConsoleOpen: false, onlineWindowVisible: true, quickPanelVisible: true));
		Assert.True(suppression.Update(commandConsoleOpen: false, onlineWindowVisible: false, quickPanelVisible: true));
		Assert.False(suppression.Update(commandConsoleOpen: false, onlineWindowVisible: false, quickPanelVisible: true));
	}

	[Fact]
	public void ClosedFromStart_DoesNotSuppress()
	{
		var suppression = new CuoEscCloseSuppression();

		Assert.False(suppression.Update(commandConsoleOpen: false, onlineWindowVisible: false, quickPanelVisible: false));
		Assert.False(suppression.Update(commandConsoleOpen: false, onlineWindowVisible: false, quickPanelVisible: false));
	}

	[Fact]
	public void ReopenThenClose_SuppressesCloseFrame()
	{
		var suppression = new CuoEscCloseSuppression();

		Assert.False(suppression.Update(commandConsoleOpen: false, onlineWindowVisible: false, quickPanelVisible: false));
		Assert.False(suppression.Update(commandConsoleOpen: true, onlineWindowVisible: false, quickPanelVisible: false));
		Assert.True(suppression.Update(commandConsoleOpen: false, onlineWindowVisible: false, quickPanelVisible: false));
	}
}
