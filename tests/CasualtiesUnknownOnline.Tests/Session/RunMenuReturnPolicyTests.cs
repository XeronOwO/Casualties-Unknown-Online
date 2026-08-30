using CasualtiesUnknownOnline.Runtime.Session;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// L0 locks for the post-session menu-return decision: only a host leaving a
/// live world persists the native run save; a guest returns without saving.
/// This is the save-authority half of the host-close-room fix.
/// </summary>
public class RunMenuReturnPolicyTests
{
	[Fact]
	public void HostInWorld_ReturnsSaveAndMenu()
	{
		var mode = RunMenuReturnPolicy.Decide(SessionRole.Host, inWorld: true);

		Assert.Equal(RunMenuReturnMode.SaveAndMenu, mode);
	}

	[Fact]
	public void GuestInWorld_ReturnsMenuOnly()
	{
		var mode = RunMenuReturnPolicy.Decide(SessionRole.Guest, inWorld: true);

		Assert.Equal(RunMenuReturnMode.MenuOnly, mode);
	}

	[Fact]
	public void NoRoleInWorld_ReturnsMenuOnly()
	{
		var mode = RunMenuReturnPolicy.Decide(SessionRole.None, inWorld: true);

		Assert.Equal(RunMenuReturnMode.MenuOnly, mode);
	}

	[Fact]
	public void InMenu_ReturnsNone_ForAnyRole()
	{
		Assert.Equal(RunMenuReturnMode.None, RunMenuReturnPolicy.Decide(SessionRole.Host, inWorld: false));
		Assert.Equal(RunMenuReturnMode.None, RunMenuReturnPolicy.Decide(SessionRole.Guest, inWorld: false));
		Assert.Equal(RunMenuReturnMode.None, RunMenuReturnPolicy.Decide(SessionRole.None, inWorld: false));
	}
}
