using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Command console family: the host-only admin commands (kick, ban/unban, role refusal).
/// Split from the former single class so xUnit v2 (serial inside a class)
/// does not serialize this whole surface in one collection.
/// </summary>
public class CommandConsoleHostAdminTests
{
	[Fact]
	public void HostOnlyCommand_IsRefusedForGuest()
	{
		var (_, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var console = guest.Services.GetRequiredService<ICommandControl>();

		Assert.False(console.TryExecute("/kick 1"));
		Assert.Contains(console.Lines, l => l.Kind == ConsoleLineKind.Error && l.Text.Contains("host-only"));
	}

	[Fact]
	public void HostKick_RemovesMember()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute($"/kick {GuestId}"));
		Assert.DoesNotContain(host.Session.Members, m => m.SteamId == GuestId);
		Assert.Contains(console.Lines, l => l.Text.Contains("Kicked member"));
	}

	[Fact]
	public void HostBan_And_Unban_RoundTrip()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var console = host.Services.GetRequiredService<ICommandControl>();
		var hostBan = host.Services.GetRequiredService<IHostBanService>();

		Assert.True(console.TryExecute($"/ban {GuestId}"));
		Assert.True(hostBan.IsBanned(GuestId));
		Assert.Contains(console.Lines, l => l.Text.Contains("Banned member"));

		Assert.True(console.TryExecute($"/unban {GuestId}"));
		Assert.False(hostBan.IsBanned(GuestId));
		Assert.Contains(console.Lines, l => l.Text.Contains("Unbanned"));
	}

	[Fact]
	public void Suggest_ReturnsMemberIdForKickArgument()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var suggestions = completion.Suggest($"/kick {GuestId}");

		Assert.Contains(suggestions, s => s.Text == GuestId.ToString());
	}
}
